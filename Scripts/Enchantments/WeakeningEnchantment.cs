using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「弱化」：打出时对目标施加 Amount 层虚弱；无单体目标（如群体攻击）时对全体敌人施加。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_WEAKENING_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class WeakeningEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「虚弱」能力
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<WeakPower>()];

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 当附魔的卡牌被打出时调用：施加虚弱（施加方式参考卡牌 Comet/BeamCell）
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (cardPlay?.Target != null)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, Amount, Card.Owner.Creature, Card);
        }
        else if (Card.CombatState != null)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, Card.CombatState.HittableEnemies, Amount, Card.Owner.Creature, Card);
        }
    }
}
