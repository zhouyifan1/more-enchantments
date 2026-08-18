using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「坚韧」：获得保留，本场战斗中每被保留一次，格挡值 +Amount。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_RESILIENCE_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class ResilienceEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「保留」关键词
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    // 决定是否可以附魔到某张卡牌上：可获得格挡、且拥有 Block 动态变量的卡牌
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card.GainsBlock && card.DynamicVars.ContainsKey("Block");
    }

    // 当附魔被应用时调用：给卡牌添加保留
    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Retain);
    }

    // 回合结束弃牌/保留结算后调用：本卡被保留则格挡值成长。
    // 战斗中的卡牌是牌组卡的克隆体，此成长仅作用于本场战斗。
    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (player != Card.Owner || !retainedCards.Contains(Card))
        {
            return Task.CompletedTask;
        }
        if (Card.DynamicVars.TryGetValue("Block", out DynamicVar? blockVar))
        {
            blockVar.BaseValue += Amount;
        }
        return Task.CompletedTask;
    }
}
