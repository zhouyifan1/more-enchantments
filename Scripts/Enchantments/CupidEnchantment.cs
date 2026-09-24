using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「丘比特」：每当你抽到这张牌时，将所有具有「丘比特」附魔的牌放入你的手牌。
/// 抽牌时机参考卡牌 KinglyPunch（AfterCardDrawn 过滤 card == Card）；
/// 放入手牌参考卡牌 SummonForth（PlayerCombatState.AllCards 过滤后 CardPileCmd.Add 到手牌）。
/// 移入手牌不算抽牌，不会连锁触发其他「丘比特」。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_CUPID_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class CupidEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值（无数值效果，关闭）
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 任意卡牌被抽到时调用：是本卡则把所有「丘比特」牌拉入手牌
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != Card || Card.Owner.PlayerCombatState == null)
        {
            return;
        }
        List<CardModel> cupidCards = Card.Owner.PlayerCombatState.AllCards
            .Where((CardModel c) => c != Card && HasCupid(c) && (c.Pile == null || c.Pile.Type != PileType.Hand))
            .ToList();
        if (cupidCards.Count > 0)
        {
            await CardPileCmd.Add(cupidCards, PileType.Hand);
        }
    }

    // 卡牌是否具有「丘比特」附魔（主槽或附加槽，见 EnchantLimitService 多附魔机制）
    private static bool HasCupid(CardModel card)
    {
        return EnchantLimitService.HasEnchantment<CupidEnchantment>(card);
    }
}
