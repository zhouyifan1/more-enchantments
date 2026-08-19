using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「战术纸」：拾起时，从牌组选择至多 1 张牌，附魔「战术」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_TACTICAL_PAPER
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class TacticalPaper : ShopEnchantRelicBase<TacticsEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
