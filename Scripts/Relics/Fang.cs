using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「蛇牙」：拾起时，从牌组选择至多 1 张攻击牌，附魔「淬毒」4 层。（静默猎手限定）
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_FANG
/// </summary>
[RegisterRelic(typeof(SilentRelicPool))]
public class Fang : EnchantOnPickupRelicBase<PoisonedEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 4;
}
