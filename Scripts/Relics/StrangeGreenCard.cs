using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「奇怪的绿色卡片」：拾起时，从牌组选择至多 1 张牌，附魔「反应」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_STRANGE_GREEN_CARD
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class StrangeGreenCard : ShopEnchantRelicBase<ReactionEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
