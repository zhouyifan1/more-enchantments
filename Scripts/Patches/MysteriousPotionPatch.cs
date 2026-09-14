using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 「神秘药剂」翻倍补丁：CardCmd.Enchant 前缀把持有者的下一次附魔层数 ×2，随后遗物失效。
/// 用前缀修改 amount（而非事后改 Amount）：OnEnchant/ModifyCard/堆叠等全部按翻倍后的层数执行。
/// ⚠️ 执行顺序是关键：附魔上限补丁 EnchantLimitEnchantCommandPatch 同为该方法前缀，且在附加槽路径上
/// 会在前缀阶段就按当前 amount 完成 Attach——若它先执行，附加槽拿到的就是未翻倍的层数。
/// 因此本前缀必须最先执行（Priority.First），先于一切消费 amount 的前缀。
/// 已知取舍：若附魔最终抛异常（异型且无槽等），翻倍仍会被消耗——正常途径（选牌 UI 均预过滤）不会触发。
/// </summary>
public class MysteriousPotionPatch : IPatchMethod
{
    public static string PatchId => "mysterious_potion_enchant_doubling";

    public static string Description => "神秘药剂：下一次附魔的层数翻倍（CardCmd.Enchant 前缀修改 amount）";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(CardCmd), nameof(CardCmd.Enchant), [typeof(EnchantmentModel), typeof(CardModel), typeof(decimal)])];

    // Priority.First：必须先于附魔上限路由前缀执行，否则附加槽路径拿到未翻倍的层数
    [HarmonyPriority(Priority.First)]
    public static void Prefix(CardModel card, ref decimal amount)
    {
        MysteriousPotion? relic = card.Owner?.Relics.OfType<MysteriousPotion>()
            .FirstOrDefault(r => !r.HasDoublingBeenConsumed);
        if (relic == null)
        {
            return;
        }
        amount *= 2m;
        relic.ConsumeDoubling();
    }
}
