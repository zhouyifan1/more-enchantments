using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「回旋镖」：拾起时，从牌组选择至多 1 张攻击牌，附魔「回旋」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_BOOMERANG
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class Boomerang : EnchantOnPickupRelicBase<FlyBackEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
