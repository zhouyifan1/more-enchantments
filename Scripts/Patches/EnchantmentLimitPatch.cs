using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 功能块 E 模型层补丁组（独立 patcher「enchantment-limit」，全部 IsCritical=false，失败仅降级）。
/// 架构见 Plan/EnchantmentLimit.md：原版单槽为主槽，附加槽由 ExtraEnchantmentStore 管理。
/// </summary>
public static class EnchantmentLimitPatchInfo
{
    public const string Group = "enchantment-limit";
}

// M1: 占用校验放行——拒绝原因仅为占槽且仍有免费槽时改判 true（驱动选牌界面过滤与 CardCmd.Enchant 前置校验）
public class EnchantLimitCanEnchantPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_can_enchant";
    public static string Description => "附魔占用校验支持多槽（仅 false→true 方向放行）";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))];

    public static void Postfix(EnchantmentModel __instance, CardModel card, ref bool __result)
    {
        try
        {
            if (__result || card.Enchantment == null)
                return;
            // 同型可堆叠：堆叠不占新槽位，无需免费槽（修复：满槽卡的同型堆叠曾被误过滤出选牌界面）
            bool sameTypeStack = EnchantLimitService.HasSameTypeEnchantment(card, __instance.GetType())
                                 && __instance.IsStackable;
            // 异型新附着：需要免费槽位 + 占用中立评估通过
            if (sameTypeStack
                || (EnchantLimitService.HasFreeSlot(card) && EnchantLimitService.EvaluateIgnoringOccupancy(__instance, card)))
                __result = true;
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitCanEnchantPatch] {ex}");
        }
    }
}

// M2+M13: 附魔路由——主槽空/原生可堆叠走原生；同型在附加槽则堆叠；有免费槽则附着附加槽；否则走原生（抛异常，与原版一致）
public class EnchantLimitEnchantCommandPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_enchant_command";
    public static string Description => "CardCmd.Enchant 多槽路由与历史记录";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardCmd), nameof(CardCmd.Enchant), [typeof(EnchantmentModel), typeof(CardModel), typeof(decimal)])];

    public static bool Prefix(EnchantmentModel enchantment, CardModel card, decimal amount, ref EnchantmentModel? __result)
    {
        try
        {
            // 主槽空位 → 原生路径
            if (card.Enchantment == null)
                return true;
            // 主槽同型 → 原生路径（原生自行处理堆叠或按 IsStackable 规则抛异常）。
            // 注意：不能用 enchantment.CanEnchant(card) 判断路由——M1 的 postfix 会为了放行而把它翻成 true，
            // 但原生方法体在异型时仍会抛 "already has enchantment"，必须在这里显式分流。
            if (card.Enchantment.GetType() == enchantment.GetType())
                return true;

            // 异型 + 同型在附加槽且可堆叠 → 堆叠到该实例
            EnchantmentModel? sameExtra = ExtraEnchantmentStore.FindSameType(card, enchantment.GetType());
            if (sameExtra != null && enchantment.IsStackable)
            {
                if (!EnchantLimitService.EvaluateIgnoringOccupancy(enchantment, card))
                    return true; // 自定义条件不满足 → 原生路径（抛出与原版一致的异常）
                ExtraEnchantmentStore.StackAmount(card, sameExtra, (int)amount);
                FinalizeAndRecord(card, enchantment);
                __result = sameExtra;
                return false;
            }

            // 异型 + 有免费槽且占用中立评估通过 → 附着附加槽
            if (EnchantLimitService.HasFreeSlot(card) && EnchantLimitService.EvaluateIgnoringOccupancy(enchantment, card))
            {
                ExtraEnchantmentStore.Attach(card, enchantment, amount);
                FinalizeAndRecord(card, enchantment);
                __result = enchantment;
                return false;
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitEnchantCommandPatch] {ex}");
        }
        return true; // 其余 → 原生路径（满槽异型时抛出与原版一致的异常）
    }

    // 等效原生 CardCmd.Enchant 末尾：FinalizeUpgradeInternal + 牌组历史记录（M13）
    private static void FinalizeAndRecord(CardModel card, EnchantmentModel enchantment)
    {
        card.FinalizeUpgradeInternal();
        if (card.Pile?.Type == PileType.Deck)
        {
            card.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(card.Owner.NetId)
                .CardsEnchanted.Add(new CardEnchantmentHistoryEntry(card, enchantment.Id));
        }
    }
}

// M3: 钩子广播——在主槽附魔之后按槽序插入附加槽（CombatState 与 RunState 两个枚举点），附加槽全量接收通用战斗钩子
public class EnchantLimitHookBroadcastPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_hook_broadcast";
    public static string Description => "战斗/run 钩子广播列表纳入附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CombatState), nameof(CombatState.IterateHookListeners)),
        new(typeof(RunState), nameof(RunState.IterateHookListeners)),
    ];

    public static void Postfix(ref IEnumerable<AbstractModel> __result)
    {
        try
        {
            __result = Wrap(__result);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitHookBroadcastPatch] {ex}");
        }
    }

    private static IEnumerable<AbstractModel> Wrap(IEnumerable<AbstractModel> original)
    {
        foreach (AbstractModel item in original)
        {
            yield return item;
            // 紧跟主槽附魔之后插入附加槽；资格过滤与原生 Contains 同规则（有卡/未移除/玩家活跃）
            if (item is EnchantmentModel primary && primary.HasCard && primary.Card.Enchantment == primary)
            {
                foreach (EnchantmentModel extra in EnchantLimitService.GetExtraEnchantments(primary.Card))
                {
                    if (extra.HasCard && !extra.Card.HasBeenRemovedFromState && extra.Card.Owner.IsActiveForHooks)
                        yield return extra;
                }
            }
        }
    }
}

// M8a: 悬停提示合并附加槽
public class EnchantLimitHoverTipsPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_hover_tips";
    public static string Description => "卡牌悬停提示合并附加槽附魔说明";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)];

    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        try
        {
            IReadOnlyList<EnchantmentModel> extras = EnchantLimitService.GetExtraEnchantments(__instance);
            if (extras.Count > 0)
                __result = __result.Concat(extras.SelectMany(e => e.HoverTips));
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitHoverTipsPatch] {ex}");
        }
    }
}

// M8b: 卡面附加文本合并附加槽（紫色行，追加在原生各行之后）
public class EnchantLimitExtraCardTextPatch : IPatchMethod
{
    // DescriptionPreviewType 是 CardModel 的私有嵌套枚举，反射取出用于精确匹配重载
    private static readonly Type[] _descriptionParams =
    [
        typeof(PileType),
        typeof(CardModel).GetNestedType("DescriptionPreviewType", BindingFlags.NonPublic)!,
        typeof(Creature),
    ];

    public static string PatchId => "enchant_limit_extra_card_text";
    public static string Description => "卡面描述合并附加槽附魔的 extraCardText";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardModel), "GetDescriptionForPile", _descriptionParams)];

    public static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            foreach (EnchantmentModel extra in EnchantLimitService.GetExtraEnchantments(__instance))
            {
                LocString? text = extra.DynamicExtraCardText;
                if (text != null)
                    __result += "\n[purple]" + text.GetFormattedText() + "[/purple]";
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitExtraCardTextPatch] {ex}");
        }
    }
}

// M8c: 金/红发光合并附加槽
public class EnchantLimitGlowPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_glow";
    public static string Description => "卡牌金/红发光合并附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardModel), nameof(CardModel.ShouldGlowGold), MethodType.Getter),
        new(typeof(CardModel), nameof(CardModel.ShouldGlowRed), MethodType.Getter),
    ];

    public static void Postfix(CardModel __instance, ref bool __result)
    {
        try
        {
            if (__result)
                return;
            IReadOnlyList<EnchantmentModel> extras = EnchantLimitService.GetExtraEnchantments(__instance);
            if (extras.Count == 0)
                return;
            // 两个 getter 共用本补丁：分别检查两种发光（开销可忽略，逻辑最简）
            __result = extras.Any(e => e.ShouldGlowGold) || extras.Any(e => e.ShouldGlowRed);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitGlowPatch] {ex}");
        }
    }
}

// M9: 数值重算广播覆盖附加槽
public class EnchantLimitRecalculatePatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_recalculate";
    public static string Description => "RecalculateCardValues 覆盖附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(PlayerCombatState), nameof(PlayerCombatState.RecalculateCardValues))];

    public static void Postfix(PlayerCombatState __instance)
    {
        try
        {
            foreach (CardModel card in __instance.AllCards)
            {
                foreach (EnchantmentModel extra in EnchantLimitService.GetExtraEnchantments(card))
                    extra.RecalculateValues();
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitRecalculatePatch] {ex}");
        }
    }
}

// M10: 降级重置后重放附加槽 ModifyCard（原生只重放主槽）
public class EnchantLimitDowngradePatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_downgrade";
    public static string Description => "降级后重放附加槽附魔的 ModifyCard";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardModel), nameof(CardModel.DowngradeInternal))];

    public static void Postfix(CardModel __instance)
    {
        try
        {
            foreach (EnchantmentModel extra in EnchantLimitService.GetExtraEnchantments(__instance))
                extra.ModifyCard();
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitDowngradePatch] {ex}");
        }
    }
}

// M11: 主槽被清除时晋升首个附加槽（维持不变式：附加槽非空 ⇒ 主槽非空）
public class EnchantLimitClearEnchantmentPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_clear_enchantment";
    public static string Description => "清除附魔后晋升附加槽到主槽";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardCmd), nameof(CardCmd.ClearEnchantment))];

    public static void Postfix(CardModel card)
    {
        try
        {
            ExtraEnchantmentStore.PromoteNextToPrimary(card);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitClearEnchantmentPatch] {ex}");
        }
    }
}

// §4-克隆：战斗克隆/变形（MutableClone）时把附加槽克隆到新卡（不重放 ModifyCard，与原生语义一致）
public class EnchantLimitClonePatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_clone";
    public static string Description => "卡牌克隆时迁移附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(AbstractModel), nameof(AbstractModel.MutableClone))];

    public static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        try
        {
            if (__instance is CardModel source && __result is CardModel clone)
                ExtraEnchantmentStore.CloneTo(source, clone);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitClonePatch] {ex}");
        }
    }
}

// §4-读档：在原版主槽附魔与升级重放完成后，按槽序恢复附加槽（ApplyInternal + ModifyCard）
public class EnchantLimitDeserializePatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_deserialize";
    public static string Description => "读档恢复附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardModel), nameof(CardModel.FromSerializable))];

    public static void Postfix(CardModel __result)
    {
        try
        {
            ExtraEnchantmentStore.RestoreFromSave(__result);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[EnchantLimitDeserializePatch] {ex}");
        }
    }
}

// 原版附魔堆叠放开：凡卡面显示层数角标（ShowAmount）的附魔统一视为可堆叠。
// 原版 IsStackable 默认 false 且原版附魔类多为 sealed 无法覆写，只能对 getter 做 postfix；
// 本 Mod 附魔不走此补丁（基类已按 IsStackable => ShowAmount 约定覆写，getter 不经过基类）。
public class StackableEnchantmentPatch : IPatchMethod
{
    public static string PatchId => "stackable_enchantment_show_amount";
    public static string Description => "卡面显示层数的附魔（含原版）统一视为可堆叠";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(EnchantmentModel), nameof(EnchantmentModel.IsStackable), MethodType.Getter)];

    public static void Postfix(EnchantmentModel __instance, ref bool __result)
    {
        try
        {
            if (!__result && __instance.ShowAmount)
                __result = true;
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[StackableEnchantmentPatch] {ex}");
        }
    }
}
