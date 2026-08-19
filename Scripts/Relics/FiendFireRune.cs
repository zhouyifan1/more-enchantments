using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「恶魔火符文」：拾起时，从牌组选择至多 1 张非消耗牌，附魔「燃尽」2 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_FIEND_FIRE_RUNE
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class FiendFireRune : EnchantOnPickupRelicBase<BurntOutEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 2;
}
