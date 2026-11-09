using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「瓶装」：这张牌获得固有。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_BOTTLED_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class BottledEnchantment : MoreEnchantmentsEnchantmentBase
{
    public override bool ShowAmount => false;

    public override bool HasExtraCardText => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Innate)];

    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Innate);
    }
}
