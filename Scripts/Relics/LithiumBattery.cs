using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「锂电池」：拾起时，从牌组选择至多 1 张牌，附魔「电动」1 层。（故障机器人限定）
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_LITHIUM_BATTERY
/// </summary>
[RegisterRelic(typeof(DefectRelicPool))]
public class LithiumBattery : EnchantOnPickupRelicBase<ElectricEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
