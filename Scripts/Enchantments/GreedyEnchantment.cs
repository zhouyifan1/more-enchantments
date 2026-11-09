using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「点金」：伤害 +3，获得消耗；这张牌造成未被格挡的攻击伤害时，获得等量金币。
/// 类名与 Entry ID 保留 GreedyEnchantment，以兼容既有存档。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_GREEDY_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class GreedyEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 金币数由实际伤害决定，不在卡面显示附魔层数
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「消耗」关键词
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 当附魔被应用时调用：给卡牌添加消耗
    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Exhaust);
    }

    // 伤害加算：+3（固定值，不随层数成长）
    public override decimal EnchantDamageAdditive(decimal originalDamage, ValueProp props)
    {
        return props.IsPoweredAttack() ? 3m : 0m;
    }

    // 任意模型造成伤害后调用：本卡每造成一段未被格挡的攻击伤害，获得等量金币
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (cardSource == Card && props.IsPoweredAttack() && result.UnblockedDamage > 0)
        {
            await PlayerCmd.GainGold(result.UnblockedDamage, Card.Owner);
        }
    }
}
