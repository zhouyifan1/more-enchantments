using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「沉重」：耗能 +1，攻击伤害翻倍（伤害部分参考原版附魔 Instinct）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_HEAVY_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class HeavyEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => false;

    // 决定可以附魔到哪类卡牌上：只能附魔到攻击牌
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    // 当附魔被应用时调用：基础耗能 +1（X 耗能卡与特殊费用卡不受影响）。
    // 克隆卡牌时费用状态随克隆复制、降级/读档时游戏先重置再重放 OnEnchant，均不会重复叠加。
    protected override void OnEnchant()
    {
        if (!Card.EnergyCost.CostsX && Card.EnergyCost.Canonical >= 0)
        {
            Card.EnergyCost.SetCustomBaseCost(Card.EnergyCost.GetWithModifiers(CostModifiers.None) + 1);
        }
    }

    // 攻击伤害翻倍；非攻击来源（如无伤害牌面、能力加成外的效果）不生效
    public override decimal EnchantDamageMultiplicative(decimal originalDamage, ValueProp props)
    {
        if (!props.IsPoweredAttack())
        {
            return 1m;
        }
        return 2m;
    }
}
