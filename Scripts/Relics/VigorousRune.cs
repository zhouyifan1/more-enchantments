using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「活力符文」：拾起时，从牌组选择至多 1 张攻击牌，附魔原版「活力」8 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_VIGOROUS_RUNE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class VigorousRune : ShopEnchantRelicBase<Vigorous>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 8;
}
