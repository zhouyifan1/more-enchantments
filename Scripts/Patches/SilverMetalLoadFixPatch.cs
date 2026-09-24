using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 「银白金属」读档修正：Player.LoadInventory 先反序列化牌组（重放附魔 OnEnchant，此时遗物尚未加载，
/// 「沉重」一律按 +1 落账）、后加载遗物——故在读档完成后对持有者的「沉重」牌统一翻转耗能（+1 → -1）。
/// </summary>
public class SilverMetalLoadFixPatch : IPatchMethod
{
    public static string PatchId => "silver_metal_load_fix";

    public static string Description => "读档后修正银白金属持有者的沉重附魔耗能方向";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Player), nameof(Player.FromSerializable))];

    public static void Postfix(Player __result)
    {
        try
        {
            if (__result.Relics.OfType<SilverMetal>().Any())
            {
                SilverMetal.FlipHeavyCosts(__result);
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[SilverMetalLoadFixPatch] 读档修正失败: {ex}");
        }
    }
}
