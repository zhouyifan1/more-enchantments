using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 功能块 E 打出分发（M7）：原版 OnPlayWrapper 在重放循环中对单一附魔调用 OnPlay。
/// 本 transpiler（MethodType.Async，锚点 = EnchantmentModel.OnPlay 调用处）把该调用替换为
/// 静态 fold：先主槽、再按槽序对附加槽依次 OnPlay（含 InvokeExecutionFinished），
/// 时机与原生一致（卡效果之后、Affliction.OnPlay 与全局 AfterCardPlayed 之前，每 replay 一次）。
/// 失败降级：此补丁不生效时附加槽收不到 OnPlay（其余钩子广播不受影响），日志可见。
/// </summary>
public class EnchantOnPlayDispatchPatch : IPatchMethod
{
    public static string PatchId => "enchant_limit_on_play_dispatch";
    public static string Description => "打出时对主槽+附加槽附魔按槽序分发 OnPlay";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardModel), nameof(CardModel.OnPlayWrapper), MethodType.Async)];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target = typeof(EnchantmentModel).GetMethod(nameof(EnchantmentModel.OnPlay))!;
        MethodInfo replacement = typeof(EnchantOnPlayDispatchPatch).GetMethod(nameof(OnPlayFold))!;
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
            Entry.Logger.Error("[EnchantOnPlayDispatchPatch] 未找到 EnchantmentModel.OnPlay 调用锚点，附加槽 OnPlay 分发未生效");
    }

    // 堆栈效果与被替换的 callvirt 一致（实例作为第一个参数）
    public static async Task OnPlayFold(EnchantmentModel? primary, PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (primary == null)
            return;
        await primary.OnPlay(choiceContext, cardPlay);
        if (!primary.HasCard)
            return;
        foreach (EnchantmentModel extra in EnchantLimitService.GetExtraEnchantments(primary.Card))
        {
            await extra.OnPlay(choiceContext, cardPlay);
            extra.InvokeExecutionFinished();
        }
    }
}
