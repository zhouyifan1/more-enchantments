using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「陨铁」：拾起时，从牌组选择至多 1 张攻击牌，附魔「沉重」1 层。
/// 属商店附魔遗物池（陨铁更普遍、可重复出现；具有唯一性的「陨铁石板」在常规遗物池）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_METEORITE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class Meteorite : ShopEnchantRelicBase<HeavyEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
