using System;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 「替死」死亡预防补丁：Hook.ShouldDie 前缀，先检查将死玩家场上是否有带「替死」的牌，
/// 有则以该附魔实例为 preventer 阻止死亡并跳过原版判定（return false）。
/// 背景：原版死亡判定两段式——先轮 ShouldDie（瓶中精灵在此段）、再轮 ShouldDieLate（蜥蜴尾巴）；
/// 而监听器顺序中遗物/药水先于卡牌附魔（CombatState.IterateHookListeners），
/// 直接把 ShouldDie 写在附魔上会被瓶中精灵抢先消耗——故用前缀把「替死」提到一切之前。
/// 后续结算无需特判：原版流程会对 preventer 回调 AfterPreventingDeath（要求 preventer 是活动监听器，
/// 战斗克隆体上的附魔满足——附加槽由本 Mod 的钩子广播补丁纳入）。
/// </summary>
public class SubstituteDeathPreventionPatch : IPatchMethod
{
    public static string PatchId => "substitute_death_prevention";

    public static string Description => "替死：死亡判定最前段拦截（先于瓶中精灵），以替死附魔为 preventer";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Hook), nameof(Hook.ShouldDie))];

    public static bool Prefix(ICombatState? combatState, Creature creature, out AbstractModel? preventer, ref bool __result)
    {
        preventer = null;
        try
        {
            Player? player = creature.Player;
            if (player?.PlayerCombatState == null)
            {
                return true;
            }
            SubstituteEnchantment? substitute = player.PlayerCombatState.AllCards
                .SelectMany(EnchantLimitService.GetAllEnchantments)
                .OfType<SubstituteEnchantment>()
                .FirstOrDefault();
            if (substitute == null)
            {
                return true;
            }
            preventer = substitute;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[SubstituteDeathPreventionPatch] 死亡拦截失败，回退原版判定: {ex}");
            return true;
        }
    }
}
