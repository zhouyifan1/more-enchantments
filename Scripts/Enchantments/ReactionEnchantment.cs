using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「反应」：打出时获得 Amount 层「下回合抽牌」状态，下回合开始时额外抽等量牌
/// （参考卡牌 Predator；状态层数可叠加，天然支持同回合多次打出）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_REACTION_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class ReactionEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「下回合抽牌」状态
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<DrawCardsNextTurnPower>()];

    // 当附魔的卡牌被打出时调用：给自己施加 Amount 层下回合抽牌状态
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Card.Owner.Creature, Amount, Card.Owner.Creature, Card);
    }
}
