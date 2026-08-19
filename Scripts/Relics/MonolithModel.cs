using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「巨石模型」：拾起时，从牌组选择至多 1 张可获得格挡的牌，附魔「坚韧」2 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_MONOLITH_MODEL
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class MonolithModel : EnchantOnPickupRelicBase<ResilienceEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 2;
}
