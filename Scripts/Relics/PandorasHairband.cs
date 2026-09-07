using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「潘多拉的发带」：拾起时，从牌组选择至多 1 张牌，附魔「潘多拉」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_PANDORAS_HAIRBAND
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class PandorasHairband : EnchantOnPickupRelicBase<PandoraEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
