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
/// 附魔「贪婪」：伤害 +2，获得消耗；用这张牌斩杀敌人时获得 Amount 金币（参考卡牌 HandOfGreed 的斩杀判定与金币获取）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_GREEDY_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class GreedyEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值（金币数）
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「斩杀」（Fatal）与「消耗」关键词
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Fatal), HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

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

    // 伤害加算：+2（固定值，不随层数成长）
    public override decimal EnchantDamageAdditive(decimal originalDamage, ValueProp props)
    {
        return props.IsPoweredAttack() ? 2m : 0m;
    }

    // 任意模型造成伤害后调用：本卡造成的伤害斩杀目标时获得金币（多段伤害仅斩杀段触发）
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (cardSource == Card && result.WasTargetKilled)
        {
            await PlayerCmd.GainGold(Amount, Card.Owner);
        }
    }
}
