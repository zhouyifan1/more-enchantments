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
/// 附魔「诅咒」：这张牌造成攻击伤害时，施加与所造成伤害同等的灾厄
/// （触发方式参考原版状态 ReaperFormPower，但仅限本牌造成的伤害）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_CURSED_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class CursedEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「灾厄」能力
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<DoomPower>()];

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 任意模型造成伤害后触发：仅限本牌造成的攻击伤害，灾厄量 = 实际造成的总伤害
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (cardSource == Card && props.IsPoweredAttack() && result.TotalDamage > 0)
        {
            await PowerCmd.Apply<DoomPower>(choiceContext, target, result.TotalDamage, Card.Owner.Creature, Card);
        }
    }
}
