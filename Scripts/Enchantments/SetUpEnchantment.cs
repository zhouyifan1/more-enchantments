using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「部署」：每场战斗开始时位于抽牌堆底部，每场战斗第一次打出时费用为 0。
/// 原生 UntilPlayed 费用修饰随战斗克隆复制，并在首次打出后自动清除。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_SET_UP_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class SetUpEnchantment : MoreEnchantmentsEnchantmentBase
{
    public override bool ShowAmount => false;

    public override bool HasExtraCardText => false;

    public override bool ShouldStartAtBottomOfDrawPile => true;

    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card)
            && !card.EnergyCost.CostsX
            && !card.HasStarCostX
            && card.EnergyCost.Canonical >= 0;
    }

    protected override void OnEnchant()
    {
        Card.EnergyCost.SetUntilPlayed(0);
        Card.SetStarCostUntilPlayed(0);
    }
}
