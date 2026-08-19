using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「淬毒」：这张牌每造成一次攻击伤害，对目标施加 Amount 层中毒
/// （触发方式参考原版状态 EnvenomPower，但仅限本牌造成的伤害）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_POISONED_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class PoisonedEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「中毒」能力
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 任意模型造成伤害后触发：仅限本牌造成的攻击伤害（多段伤害逐段触发）
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (cardSource == Card && props.IsPoweredAttack() && result.UnblockedDamage > 0)
        {
            await PowerCmd.Apply<PoisonPower>(choiceContext, target, Amount, Card.Owner.Creature, Card);
        }
    }
}
