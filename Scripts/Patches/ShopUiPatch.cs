using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 商店专属栏位的共享状态：本次商店界面由 ShopUiSlotsPatch 追加的栏位节点。
/// 商店界面每次打开都会重新 Initialize（prefix 里先 Clear），关闭后节点随场景销毁，不会泄漏。
/// </summary>
internal static class ShopEnchantShopState
{
    public static readonly List<NMerchantRelic> AddedSlots = [];
}

/// <summary>
/// 商店 UI 栏位补丁：在 NMerchantInventory.Initialize 之前，按"库存条目数 - 场景现有栏位数"
/// 向 %Relics 容器补齐 NMerchantRelic 栏位节点（摆在药水售卖栏位下方一行），
/// 之后原生 Initialize 循环自动完成 Initialize + FillSlot 绑定，购买/扣金/售出隐藏/联机同步全部走原生路径。
/// 库存侧不追加条目时（遗物池为空）needed = 0，本补丁不产生任何变化。
/// </summary>
public class ShopUiSlotsPatch : IPatchMethod
{
    // 推算不出药水行位置时的兜底行间距
    private const float FallbackRowGap = 160f;

    public static string PatchId => "shop_enchant_relic_ui_slots";

    public static string Description => "在商店药水栏位下方为商店附魔遗物池补齐售卖栏位节点";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))];

    public static void Prefix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        try
        {
            AddSlots(__instance, inventory);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[ShopUiSlotsPatch] 追加商店栏位节点失败: {ex}");
        }
    }

    private static void AddSlots(NMerchantInventory node, MerchantInventory inventory)
    {
        ShopEnchantShopState.AddedSlots.Clear();

        Control? relicContainer = node.GetNodeOrNull<Control>("%Relics");
        if (relicContainer == null)
            return;
        List<NMerchantRelic> existing = [.. relicContainer.GetChildren(false).OfType<NMerchantRelic>()];
        if (existing.Count == 0)
            return;

        int needed = inventory.RelicEntries.Count - existing.Count;
        if (needed <= 0)
            return;

        // 布局推算：新行 Y = 药水行 Y + (药水行 Y - 遗物行 Y)；X 与遗物行逐列对齐。
        // 栏位坐标烘焙在 merchant_room.tscn 里，只能从现有栏位的 GlobalPosition 推算；
        // 此刻货架整体在屏幕外（y=-1000），但相对间距即最终间距。
        Vector2 newRowPos = ComputeNewRowPosition(node, existing[0].GlobalPosition);

        for (int i = 0; i < needed; i++)
        {
            NMerchantRelic slot = CreateSlot(existing[0]);
            relicContainer.AddChild(slot);
            if (!IsSlotFunctional(slot))
            {
                // _Ready 未能解析子节点（如 Duplicate 丢了 Owner 映射）：移除并改用手工构建兜底
                Entry.Logger.Warn("[ShopUiSlotsPatch] 复制的栏位节点子节点解析失败，改用手工构建。");
                relicContainer.RemoveChild(slot);
                slot.QueueFree();
                slot = BuildSlotManually();
                relicContainer.AddChild(slot);
            }
            float x = existing[Math.Min(i, existing.Count - 1)].GlobalPosition.X;
            slot.GlobalPosition = new Vector2(x, newRowPos.Y);
            ShopEnchantShopState.AddedSlots.Add(slot);
        }
    }

    // _Ready 在 AddChild 时同步触发；%Hitbox / %RelicHolder 任一缺失都会在后续原生绑定流程中炸商店
    private static bool IsSlotFunctional(NMerchantRelic slot) =>
        slot.Hitbox != null && slot.GetNodeOrNull<Control>("%RelicHolder") != null;

    private static Vector2 ComputeNewRowPosition(NMerchantInventory node, Vector2 relicRowPos)
    {
        Control? potionContainer = node.GetNodeOrNull<Control>("%Potions");
        NMerchantPotion? potionSlot = potionContainer?.GetChildren(false).OfType<NMerchantPotion>().FirstOrDefault();
        if (potionSlot == null)
            return relicRowPos + new Vector2(0f, FallbackRowGap);
        float gap = potionSlot.GlobalPosition.Y - relicRowPos.Y;
        // 场景异常（行距非正数）时兜底，避免新行叠到遗物行上
        if (gap <= 0f)
            gap = FallbackRowGap;
        return new Vector2(relicRowPos.X, potionSlot.GlobalPosition.Y + gap);
    }

    // 三级兜底创建栏位节点，保证数量始终与库存条目匹配（否则原生 Initialize 的 GetChild(k) 会越界）
    private static NMerchantRelic CreateSlot(NMerchantRelic template)
    {
        // a. 栏位是场景实例时直接实例化原场景，最干净
        if (!string.IsNullOrEmpty(template.SceneFilePath))
        {
            try
            {
                return ResourceLoader.Load<PackedScene>(template.SceneFilePath).Instantiate<NMerchantRelic>();
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"[ShopUiSlotsPatch] 场景实例化失败，改用 Duplicate: {ex.Message}");
            }
        }

        // b. 复制现有栏位。此刻原生栏位尚未 FillSlot（_relicNode 为 null），不会复制出悬空的图标引用；
        //    Godot Duplicate 会重映射子树内 Owner，%Hitbox/%CostLabel/%RelicHolder 唯一名可正常解析。
        try
        {
            return (NMerchantRelic)template.Duplicate();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[ShopUiSlotsPatch] Duplicate 失败，改用手工构建: {ex.Message}");
        }

        // c. 手工构建最小子树（价格标签无原版样式，但结构与功能完整）
        return BuildSlotManually();
    }

    private static NMerchantRelic BuildSlotManually()
    {
        NMerchantRelic slot = new() { Name = "MerchantEnchantRelicSlot" };
        NClickableControl hitbox = new() { Name = "Hitbox", UniqueNameInOwner = true };
        MegaLabel costLabel = new() { Name = "CostLabel", UniqueNameInOwner = true };
        Control relicHolder = new() { Name = "RelicHolder", UniqueNameInOwner = true };
        slot.AddChild(hitbox);
        hitbox.Owner = slot;
        slot.AddChild(costLabel);
        costLabel.Owner = slot;
        slot.AddChild(relicHolder);
        relicHolder.Owner = slot;
        // 与商店原生遗物栏位一致用大图标（UpdateVisual 会按 Large 摆 128x128 图标并对齐 Hitbox）
        slot.Set(NMerchantRelic.PropertyName._iconSize, Variant.From((int)NRelic.IconSize.Large));
        return slot;
    }
}

/// <summary>
/// 商店焦点导航补丁（手柄/键盘，鼠标用户不受影响）：
/// 原生 UpdateNavigation 把 %Relics 容器下全部栏位当作与无色卡牌同一行链接左右焦点、
/// 把新增栏位的上方焦点指向卡牌行。本补丁在其后修正：新行内部左右互链、上方指向 X 最近的
/// 药水栏位、下方自指；药水栏位下方指向新行；原生遗物行末位的右焦点跳过我方栏位。
/// </summary>
public class ShopUiNavigationPatch : IPatchMethod
{
    public static string PatchId => "shop_enchant_relic_ui_navigation";

    public static string Description => "修正商店附魔遗物专属栏位的手柄/键盘焦点导航";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NMerchantInventory), "UpdateNavigation")];

    public static void Postfix(NMerchantInventory __instance)
    {
        try
        {
            FixupNavigation(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[ShopUiNavigationPatch] 修正焦点导航失败: {ex}");
        }
    }

    private static void FixupNavigation(NMerchantInventory node)
    {
        List<NMerchantRelic> added = [.. ShopEnchantShopState.AddedSlots.Where(GodotObject.IsInstanceValid)];
        if (added.Count == 0)
            return;

        Control? relicContainer = node.GetNodeOrNull<Control>("%Relics");
        if (relicContainer == null)
            return;
        Control? potionContainer = node.GetNodeOrNull<Control>("%Potions");
        NMerchantCardRemoval? cardRemoval = node.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval");

        List<NMerchantRelic> nativeRelics = [.. relicContainer.GetChildren(false).OfType<NMerchantRelic>().Where(s => !added.Contains(s))];
        List<NMerchantPotion> potions = potionContainer == null
            ? []
            : [.. potionContainer.GetChildren(false).OfType<NMerchantPotion>()];

        List<NMerchantRelic> orderedAdded = [.. added.OrderBy(s => s.GlobalPosition.X)];
        List<NMerchantRelic> visibleAdded = [.. orderedAdded.Where(s => s.Visible)];
        List<NMerchantPotion> visiblePotions = [.. potions.Where(s => s.Visible)];

        // 新行：行内左右互链（首尾自指），上 → X 最近的药水栏位，下 → 自身（新行是最底行）
        for (int i = 0; i < orderedAdded.Count; i++)
        {
            NMerchantRelic slot = orderedAdded[i];
            slot.FocusNeighborLeft = (i > 0 ? orderedAdded[i - 1] : slot).GetPath();
            slot.FocusNeighborRight = (i < orderedAdded.Count - 1 ? orderedAdded[i + 1] : slot).GetPath();
            slot.FocusNeighborTop = (NearestByX(visiblePotions, slot.GlobalPosition.X) ?? (Control)slot).GetPath();
            slot.FocusNeighborBottom = slot.GetPath();
        }

        // 药水行：下 → 新行 X 最近栏位（新行全部售罄时保持原生自指）
        foreach (NMerchantPotion potion in visiblePotions)
        {
            NMerchantRelic? below = NearestByX(visibleAdded, potion.GlobalPosition.X);
            if (below != null)
                potion.FocusNeighborBottom = below.GetPath();
        }

        // 原生遗物行末位：右焦点改回删牌服务（或自指），删牌服务左焦点同理跳回，不串到药水下方的新行
        if (nativeRelics.Count > 0)
        {
            NMerchantRelic lastNative = nativeRelics.MaxBy(s => s.GlobalPosition.X)!;
            lastNative.FocusNeighborRight = (cardRemoval != null ? (Control)cardRemoval : lastNative).GetPath();
            if (cardRemoval != null)
                cardRemoval.FocusNeighborLeft = lastNative.GetPath();
        }
    }

    private static T? NearestByX<T>(IReadOnlyList<T> slots, float x) where T : Control =>
        slots.Count == 0 ? null : slots.MinBy(s => Math.Abs(s.GlobalPosition.X - x));
}
