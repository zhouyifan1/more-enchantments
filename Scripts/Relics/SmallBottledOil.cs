using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「小瓶油脂」：拾起时，从牌组选择至多 1 张可获得格挡的牌，附魔原版「灵巧」2 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SMALL_BOTTLED_OIL
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class SmallBottledOil : ShopEnchantRelicBase<Nimble>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 2;
}
