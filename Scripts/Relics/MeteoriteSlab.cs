using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「陨铁石板」：拾起时，从牌组选择至多 1 张攻击牌，附魔「沉重」1 层。
/// 属常规遗物池（SharedRelicPool，全局唯一；更普遍的「陨铁」在商店附魔遗物池重复出现）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_METEORITE_SLAB
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class MeteoriteSlab : EnchantOnPickupRelicBase<HeavyEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
