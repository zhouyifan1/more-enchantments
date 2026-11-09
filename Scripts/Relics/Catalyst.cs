using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「催化剂」：拾起时，从牌组选择至多 1 张牌，附魔「催化」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_CATALYST
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class Catalyst : ShopEnchantRelicBase<CatalyzeEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
