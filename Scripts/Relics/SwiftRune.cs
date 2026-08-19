using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「迅捷符文」：拾起时，从牌组选择至多 1 张牌，附魔原版「迅速」2 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SWIFT_RUNE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class SwiftRune : ShopEnchantRelicBase<Swift>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 2;
}
