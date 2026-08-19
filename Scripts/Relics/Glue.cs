using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「胶水」：拾起时，从牌组选择至多 2 张非能力牌，附魔「粘手」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_GLUE
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class Glue : EnchantOnPickupRelicBase<StickEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 2;

    protected override int EnchantAmount => 1;
}
