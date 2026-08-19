using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「奇怪的蓝色卡片」：拾起时，从牌组选择至多 2 张牌，附魔「反应」1 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_STRANGE_BLUE_CARD
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class StrangeBlueCard : EnchantOnPickupRelicBase<ReactionEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 2;

    protected override int EnchantAmount => 1;
}
