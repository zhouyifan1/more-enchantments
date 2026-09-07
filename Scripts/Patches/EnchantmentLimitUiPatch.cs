using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 功能块 E 卡面多附魔图标（U1-U4）：NCard 场景只有单个 %Enchantment tab（主槽），
/// 本补丁在原生 UpdateEnchantmentVisuals 之后为附加槽 Duplicate tab 并水平向左排列，
/// 逐槽填充图标/角标/置灰；附加槽的 StatusChanged 由本 Mod 自管订阅（原生 _subscribedEnchantment
/// 单订阅字段不可复用）；对象池回收（OnReturnedFromPool/OnFreedToPool）时销毁复制的 tab 并退订。
/// 失败降级：仅显示主槽图标，逻辑层完全不受影响。
/// </summary>
public class EnchantLimitCardUiPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_card_ui";
    public static string Description => "卡面附加槽附魔图标排布与置灰";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NCard), "UpdateEnchantmentVisuals"),
        new(typeof(NCard), nameof(NCard.OnReturnedFromPool)),
        new(typeof(NCard), nameof(NCard.OnFreedToPool)),
    ];

    public static void Postfix(NCard __instance, MethodBase __originalMethod)
    {
        try
        {
            if (__originalMethod.Name == "UpdateEnchantmentVisuals")
                NCardExtraEnchantmentTabs.Refresh(__instance);
            else
                NCardExtraEnchantmentTabs.Cleanup(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitCardUiPatch] {ex}");
        }
    }
}

/// <summary>附加槽附魔的战斗预览值刷新（等效原生对主槽附魔的 ClearPreview + UpdateDynamicVarPreview）。</summary>
public class EnchantLimitCardPreviewPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_card_preview";
    public static string Description => "附加槽附魔 DynamicVars 的卡面预览刷新";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NCard), nameof(NCard.UpdateVisuals))];

    public static void Postfix(NCard __instance, CardPreviewMode previewMode)
    {
        try
        {
            CardModel? model = __instance.Model;
            if (model == null || NCardExtraEnchantmentTabs.IsForceUnpoweredPreview(__instance))
                return;
            IReadOnlyList<EnchantmentModel> extras = EnchantLimitService.GetExtraEnchantments(model);
            if (extras.Count == 0)
                return;
            Creature? target = NCardExtraEnchantmentTabs.GetPreviewTarget(__instance) ?? model.CurrentTarget;
            foreach (EnchantmentModel extra in extras)
            {
                extra.DynamicVars.ClearPreview();
                model.UpdateDynamicVarPreview(previewMode, target, extra.DynamicVars);
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitCardPreviewPatch] {ex}");
        }
    }
}

/// <summary>NCard 附加槽 tab 的管理（复制/排布/置灰/订阅/回收）。</summary>
internal static class NCardExtraEnchantmentTabs
{
    private const float TabSpacing = 2f;
    private const float FallbackTabSize = 40f;

    private sealed class TabState
    {
        public readonly List<Control> Tabs = [];
        public readonly List<(EnchantmentModel Enchantment, Action Handler)> Subs = [];
    }

    private static readonly ConditionalWeakTable<NCard, TabState> _states = new();

    private static readonly FieldInfo? _tabField = PrivateAccess.DeclaredField(typeof(NCard), "_enchantmentTab");
    private static readonly FieldInfo? _forceUnpoweredField = PrivateAccess.DeclaredField(typeof(NCard), "_forceUnpoweredPreview");
    private static readonly FieldInfo? _previewTargetField = PrivateAccess.DeclaredField(typeof(NCard), "_previewTarget");
    private static readonly FieldInfo? _hField = PrivateAccess.DeclaredField(typeof(NCard), "_h");
    private static readonly FieldInfo? _sField = PrivateAccess.DeclaredField(typeof(NCard), "_s");
    private static readonly FieldInfo? _vField = PrivateAccess.DeclaredField(typeof(NCard), "_v");

    public static bool IsForceUnpoweredPreview(NCard card) => _forceUnpoweredField?.GetValue(card) as bool? ?? false;

    public static Creature? GetPreviewTarget(NCard card) => _previewTargetField?.GetValue(card) as Creature;

    public static void Refresh(NCard card)
    {
        TabState state = _states.GetOrCreateValue(card);
        UnsubscribeAll(state);

        CardModel? model = card.Model;
        Control? primaryTab = _tabField?.GetValue(card) as Control;
        IReadOnlyList<EnchantmentModel> extras = model == null ? [] : EnchantLimitService.GetExtraEnchantments(model);
        if (primaryTab == null || extras.Count == 0)
        {
            HideAll(state);
            return;
        }

        float tabHeight = primaryTab.Size.Y > 0f ? primaryTab.Size.Y : FallbackTabSize;
        for (int i = 0; i < extras.Count; i++)
        {
            Control tab = GetOrCreateTab(card, state, primaryTab, i);
            tab.Visible = true;
            // 附加槽 tab 作为主槽 tab 的子节点垂直向下排布（略微挨着）：
            // 跟随主槽的显隐与位置调整（星标上移/vfx 隐藏），无需另行同步
            tab.Position = new Vector2(0f, (tabHeight + TabSpacing) * (i + 1));

            EnchantmentModel extra = extras[i];
            TextureRect? icon = tab.GetNodeOrNull<TextureRect>("Icon");
            MegaLabel? label = tab.GetNodeOrNull<MegaLabel>("Label");
            if (icon != null)
                icon.Texture = (Texture2D)extra.Icon;
            if (label != null)
            {
                label.SetTextAutoSize(extra.DisplayAmount.ToString());
                label.Visible = extra.ShowAmount;
            }
            ApplyStatus(tab, icon, label, extra.Status);

            Action handler = () => SafeRefresh(card);
            extra.StatusChanged += handler;
            state.Subs.Add((extra, handler));
        }

        for (int i = extras.Count; i < state.Tabs.Count; i++)
            state.Tabs[i].Visible = false;
    }

    public static void Cleanup(NCard card)
    {
        if (!_states.TryGetValue(card, out TabState? state))
            return;
        UnsubscribeAll(state);
        foreach (Control tab in state.Tabs)
        {
            if (GodotObject.IsInstanceValid(tab))
                tab.QueueFree();
        }
        state.Tabs.Clear();
        _states.Remove(card);
    }

    private static void SafeRefresh(NCard card)
    {
        try
        {
            if (GodotObject.IsInstanceValid(card) && card.IsInsideTree())
                Refresh(card);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[NCardExtraEnchantmentTabs] {ex}");
        }
    }

    private static Control GetOrCreateTab(NCard card, TabState state, Control primaryTab, int index)
    {
        if (index < state.Tabs.Count && GodotObject.IsInstanceValid(state.Tabs[index]))
            return state.Tabs[index];
        Control tab = (Control)primaryTab.Duplicate();
        // 复制的 tab 与主槽共享 ShaderMaterial，必须独立化才能逐槽置灰
        if (primaryTab.Material is ShaderMaterial material)
            tab.Material = (ShaderMaterial)material.Duplicate();
        // 挂到主槽 tab 下（而非同级）：位置相对主槽，主槽隐藏（附魔 vfx 等）时附加槽自动跟随隐藏
        primaryTab.AddChild(tab);
        if (index < state.Tabs.Count)
            state.Tabs[index] = tab;
        else
            state.Tabs.Add(tab);
        return tab;
    }

    // 复刻原生 NCard.SetEnchantmentStatus（参数值逐行对应）
    private static void ApplyStatus(Control tab, TextureRect? icon, MegaLabel? label, EnchantmentStatus status)
    {
        StringName? h = _hField?.GetValue(null) as StringName;
        StringName? s = _sField?.GetValue(null) as StringName;
        StringName? v = _vField?.GetValue(null) as StringName;
        bool disabled = status == EnchantmentStatus.Disabled;
        tab.Modulate = disabled ? new Color(1f, 1f, 1f, 0.9f) : Colors.White;
        if (tab.Material is ShaderMaterial mat && h != null && s != null && v != null)
        {
            mat.SetShaderParameter(h, 0.25);
            mat.SetShaderParameter(s, disabled ? 0.1 : 0.4);
            mat.SetShaderParameter(v, 0.6);
        }
        if (icon != null)
            icon.UseParentMaterial = disabled;
        if (label != null)
            label.SelfModulate = disabled ? StsColors.gray : Colors.White;
    }

    private static void UnsubscribeAll(TabState state)
    {
        foreach ((EnchantmentModel enchantment, Action handler) in state.Subs)
            enchantment.StatusChanged -= handler;
        state.Subs.Clear();
    }

    private static void HideAll(TabState state)
    {
        foreach (Control tab in state.Tabs)
        {
            if (GodotObject.IsInstanceValid(tab))
                tab.Visible = false;
        }
    }
}

/// <summary>
/// 附魔确认预览的多槽适配（U6）：原生 NEnchantPreview.Init 对克隆卡直接 EnchantInternal 覆盖主槽，
/// 预览只会显示新附魔。本补丁在"已有异型主槽"时复刻原生流程，但把新附魔以附加槽附着到克隆——
/// 预览同时显示既有附魔与新附魔；单槽或同型堆叠场景走原生。
/// </summary>
public class EnchantLimitPreviewPatch : IPatchMethod
{
    private static readonly MethodInfo? _removeExistingCards = PrivateAccess.DeclaredMethod(typeof(NEnchantPreview), "RemoveExistingCards");
    private static readonly FieldInfo? _beforeField = PrivateAccess.DeclaredField(typeof(NEnchantPreview), "_before");
    private static readonly FieldInfo? _afterField = PrivateAccess.DeclaredField(typeof(NEnchantPreview), "_after");

    public static string PatchId => "enchant_limit_preview";
    public static string Description => "附魔确认预览同时显示既有附魔与新附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NEnchantPreview), nameof(NEnchantPreview.Init))];

    public static bool Prefix(NEnchantPreview __instance, CardModel card, EnchantmentModel canonicalEnchantment, int amount)
    {
        try
        {
            // 单槽或同型堆叠 → 原生路径
            if (card.Enchantment == null || card.Enchantment.GetType() == canonicalEnchantment.GetType())
                return true;
            if (_removeExistingCards == null || _beforeField == null || _afterField == null)
                return true;

            // 以下逐行复刻原生 Init，唯一差异：新附魔以附加槽附着到克隆（不覆盖主槽）
            canonicalEnchantment.AssertCanonical();
            _removeExistingCards.Invoke(__instance, null);
            Control before = (Control)_beforeField.GetValue(__instance)!;
            Control after = (Control)_afterField.GetValue(__instance)!;

            NPreviewCardHolder beforeHolder = NPreviewCardHolder.Create(NCard.Create(card), showHoverTips: true, scaleOnHover: false);
            before.AddChildSafely(beforeHolder);
            beforeHolder.CardNode.UpdateVisuals(card.Pile.Type, CardPreviewMode.Normal);

            CardModel clone = card.CardScope.CloneCard(card);
            EnchantmentModel extra = canonicalEnchantment.ToMutable();
            ExtraEnchantmentStore.AttachPreview(clone, extra, amount);
            clone.IsEnchantmentPreview = true;
            extra.ModifyCard();

            NPreviewCardHolder afterHolder = NPreviewCardHolder.Create(NCard.Create(clone), showHoverTips: true, scaleOnHover: false);
            after.AddChildSafely(afterHolder);
            afterHolder.CardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            return false;
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitPreviewPatch] {ex}");
            return true;
        }
    }
}
