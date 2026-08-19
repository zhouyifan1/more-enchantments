using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「虚弱药水涂料」：拾起时，从牌组选择至多 1 张攻击牌，附魔「弱化」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_WEAK_PAINT
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class WeakPaint : ShopEnchantRelicBase<WeakeningEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
