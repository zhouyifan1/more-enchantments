using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「三角尺」：拾起时，从牌组选择至多 1 张牌，附魔原版「稳定」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SET_SQUARE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class SetSquare : ShopEnchantRelicBase<Steady>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
