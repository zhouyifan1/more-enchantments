using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「捕兽夹模型」：拾起时，从牌组选择至多 1 张牌，附魔「部署」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_TRAP_MODEL
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class TrapModel : ShopEnchantRelicBase<SetUpEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
