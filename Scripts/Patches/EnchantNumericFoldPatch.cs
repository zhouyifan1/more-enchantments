using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 功能块 E 数值 fold（M4/M5/M6，一个 transpiler 覆盖全部目标）：
/// 原版数值链在 Hook.ModifyDamage/ModifyBlock、CardModel.GetEnchantedReplayCount 与 6 个 DynamicVar
/// 预览类中直读 card.Enchantment 调用 Enchant 系列方法。本补丁把这些调用点替换为静态 fold 助手——
/// 先算主槽、再按槽序对每个附加槽做同序运算（加性返回总增量、乘性返回总因子、重放逐槽折叠），
/// 效果等价于一个有序附魔列表，且主槽行为与原版完全一致。
/// 附魔未附着卡牌（canonical/悬停提示临时实例）时 fold 退化为原始调用。
/// </summary>
public class EnchantNumericFoldPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_numeric_fold";
    public static string Description => "伤害/格挡/重放/预览数值链折叠附加槽附魔";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(Hook), nameof(Hook.ModifyDamage)),
        new(typeof(Hook), nameof(Hook.ModifyBlock)),
        new(typeof(CardModel), nameof(CardModel.GetEnchantedReplayCount)),
        new(typeof(DamageVar), nameof(DamageVar.UpdateCardPreview)),
        new(typeof(BlockVar), nameof(BlockVar.UpdateCardPreview)),
        new(typeof(CalculatedDamageVar), nameof(CalculatedDamageVar.UpdateCardPreview)),
        new(typeof(CalculatedBlockVar), nameof(CalculatedBlockVar.UpdateCardPreview)),
        new(typeof(ExtraDamageVar), nameof(ExtraDamageVar.UpdateCardPreview)),
        new(typeof(OstyDamageVar), nameof(OstyDamageVar.UpdateCardPreview)),
    ];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction code in instructions)
        {
            if ((code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call)
                && code.operand is MethodInfo method
                && EnchantFold.TryGetReplacement(method, out MethodInfo? replacement))
            {
                code.opcode = OpCodes.Call;
                code.operand = replacement;
            }
            yield return code;
        }
    }
}

/// <summary>Enchant 系列实例方法的 fold 替换实现（堆栈效果与被替换的 callvirt 一致）。</summary>
public static class EnchantFold
{
    private static readonly Dictionary<MethodInfo, MethodInfo> _replacements = new()
    {
        [typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.EnchantDamageAdditive))!] =
            typeof(EnchantFold).GetMethod(nameof(DamageAdditive))!,
        [typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.EnchantDamageMultiplicative))!] =
            typeof(EnchantFold).GetMethod(nameof(DamageMultiplicative))!,
        [typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.EnchantBlockAdditive))!] =
            typeof(EnchantFold).GetMethod(nameof(BlockAdditive))!,
        [typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.EnchantBlockMultiplicative))!] =
            typeof(EnchantFold).GetMethod(nameof(BlockMultiplicative))!,
        [typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.EnchantPlayCount))!] =
            typeof(EnchantFold).GetMethod(nameof(PlayCount))!,
    };

    public static bool TryGetReplacement(MethodInfo method, out MethodInfo? replacement) =>
        _replacements.TryGetValue(method, out replacement);

    // 加性：返回总增量（调用点外层做 num += ...）。逐槽顺序：current 为“到目前为止的值”。
    public static decimal DamageAdditive(EnchantmentModel? primary, decimal value, ValueProp props)
    {
        if (primary == null)
            return 0m;
        decimal current = value + primary.EnchantDamageAdditive(value, props);
        foreach (EnchantmentModel extra in Extras(primary))
            current += extra.EnchantDamageAdditive(current, props);
        return current - value;
    }

    // 乘性：返回总因子（调用点外层做 num *= ...）。
    public static decimal DamageMultiplicative(EnchantmentModel? primary, decimal value, ValueProp props)
    {
        if (primary == null)
            return 1m;
        decimal factor = 1m;
        decimal current = value;
        foreach (EnchantmentModel slot in Slots(primary))
        {
            decimal f = slot.EnchantDamageMultiplicative(current, props);
            factor *= f;
            current *= f;
        }
        return factor;
    }

    public static decimal BlockAdditive(EnchantmentModel? primary, decimal value)
    {
        if (primary == null)
            return 0m;
        decimal current = value + primary.EnchantBlockAdditive(value);
        foreach (EnchantmentModel extra in Extras(primary))
            current += extra.EnchantBlockAdditive(current);
        return current - value;
    }

    public static decimal BlockMultiplicative(EnchantmentModel? primary, decimal value)
    {
        if (primary == null)
            return 1m;
        decimal factor = 1m;
        decimal current = value;
        foreach (EnchantmentModel slot in Slots(primary))
        {
            decimal f = slot.EnchantBlockMultiplicative(current);
            factor *= f;
            current *= f;
        }
        return factor;
    }

    // 重放：直接返回折叠后的次数（调用点直接赋值）。
    public static int PlayCount(EnchantmentModel? primary, int count)
    {
        if (primary == null)
            return count;
        int current = count;
        foreach (EnchantmentModel slot in Slots(primary))
            current = slot.EnchantPlayCount(current);
        return current;
    }

    private static IEnumerable<EnchantmentModel> Slots(EnchantmentModel primary)
    {
        yield return primary;
        foreach (EnchantmentModel extra in Extras(primary))
            yield return extra;
    }

    private static IReadOnlyList<EnchantmentModel> Extras(EnchantmentModel primary) =>
        primary.HasCard ? EnchantLimitService.GetExtraEnchantments(primary.Card) : [];
}
