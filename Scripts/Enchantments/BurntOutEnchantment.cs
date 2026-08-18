using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「燃尽」：获得消耗，打出时额外重放 Amount 次（重放机制参考原版附魔 Glam/Spiral）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_BURNT_OUT_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class BurntOutEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => false;

    // 额外悬停提示：解释「消耗」关键词与「重放」机制
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.Static(StaticHoverTip.ReplayStatic),
    ];

    // 决定可以附魔到哪类卡牌上：不能附魔到能力牌（能力牌打出后离场，无法重放）
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType != CardType.Power;
    }

    // 决定是否可以附魔到某张卡牌上：本身已有消耗的卡不能再附着
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && !card.Keywords.Contains(CardKeyword.Exhaust);
    }

    // 当附魔被应用时调用：给卡牌添加消耗
    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Exhaust);
    }

    // 修改打出次数：额外重放 Amount 次
    public override int EnchantPlayCount(int originalPlayCount)
    {
        return originalPlayCount + Amount;
    }
}
