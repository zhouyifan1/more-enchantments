using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「小锤子」：拾起时，从牌组选择至多 1 张牌，附魔「铸剑」6 层。（摄政王限定）
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SMALL_HAMMER
/// </summary>
[RegisterRelic(typeof(RegentRelicPool))]
public class SmallHammer : EnchantOnPickupRelicBase<ForgeEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 6;
}
