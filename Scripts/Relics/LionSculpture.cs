using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「狮子雕塑」：拾起时，从牌组选择至多 1 张攻击牌，附魔原版「本能」1 层。
/// 特殊定价 225（见 Plan/ShopEnchantRelics.md）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_LION_SCULPTURE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class LionSculpture : ShopEnchantRelicBase<Instinct>
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override int MerchantCost => 225;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
