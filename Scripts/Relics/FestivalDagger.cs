using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「祭典匕首」：拾起时，从牌组选择至多 1 张攻击牌，附魔「血祭」1 层。（铁甲战士限定）
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_FESTIVAL_DAGGER
/// </summary>
[RegisterRelic(typeof(IroncladRelicPool))]
public class FestivalDagger : EnchantOnPickupRelicBase<SacrificeEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
