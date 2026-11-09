using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 功能块 E 首回合沉底（M12）：把 SetupPlayerTurn 中对主槽 ShouldStartAtBottomOfDrawPile
/// 的读取扩展为主槽与附加槽任一附魔请求沉底即生效。
/// </summary>
public class EnchantLimitBottomOfDrawPilePatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_bottom_of_draw_pile";

    public static string Description => "首回合沉底覆盖主槽与附加槽附魔";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CombatManager), "SetupPlayerTurn", MethodType.Async)];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target = typeof(EnchantmentModel).GetProperty(nameof(EnchantmentModel.ShouldStartAtBottomOfDrawPile))!.GetMethod!;
        MethodInfo replacement = typeof(EnchantLimitBottomOfDrawPilePatch).GetMethod(nameof(ShouldStartAtBottom))!;
        int count = 0;
        foreach (CodeInstruction code in instructions)
        {
            if ((code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call) && Equals(code.operand, target))
            {
                code.opcode = OpCodes.Call;
                code.operand = replacement;
                count++;
            }
            yield return code;
        }
        if (count == 0)
            Entry.Logger.Error("[EnchantLimitBottomOfDrawPilePatch] 未找到首回合沉底属性读取锚点，附加槽沉底未生效");
    }

    public static bool ShouldStartAtBottom(EnchantmentModel? primary)
    {
        if (primary == null)
            return false;
        if (primary.ShouldStartAtBottomOfDrawPile)
            return true;
        return primary.HasCard && EnchantLimitService.GetExtraEnchantments(primary.Card)
            .Any(e => e.ShouldStartAtBottomOfDrawPile);
    }
}
