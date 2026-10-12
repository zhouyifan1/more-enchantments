using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「回旋」：这张牌被打出后，会在你的下个回合抽牌前返回手牌（参考卡牌 Bolas 的实现）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_FLY_BACK_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class FlyBackEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值（无数值效果，关闭）
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 抽牌前调用（参考 Bolas）：本卡上个我方回合被打出过且不在手牌 → 加入手牌
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Card.Owner)
        {
            return;
        }
        bool playedLastTurn = CombatManager.Instance.History.CardPlaysFinished
            .Any((CardPlayFinishedEntry e) => e.HappenedLastPlayerTurn(Card.Owner) && e.CardPlay.Card == Card);
        if (!playedLastTurn)
        {
            return;
        }
        if (Card.Pile == null || Card.Pile.Type != PileType.Hand)
        {
            await CardPileCmd.Add(Card, PileType.Hand);
        }
    }
}
