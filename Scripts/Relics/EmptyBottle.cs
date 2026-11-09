using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「空瓶子」：拾起时，从牌组选择至多 1 张牌，附魔「瓶装」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_EMPTY_BOTTLE
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class EmptyBottle : EnchantOnPickupRelicBase<BottledEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
