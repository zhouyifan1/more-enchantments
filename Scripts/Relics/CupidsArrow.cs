using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「丘比特之箭」：拾起时，从牌组选择至多 2 张牌，附魔「丘比特」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_CUPIDS_ARROW
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class CupidsArrow : EnchantOnPickupRelicBase<CupidEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override int MaxCards => 2;

    protected override int EnchantAmount => 1;
}
