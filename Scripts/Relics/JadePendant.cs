using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「玉佩」：拾起时，从牌组选择至多 1 张牌，附魔「替死」1 层。
/// 牌组中没有带「替死」的牌时显示为失效（AfterObtained/替死触发移除/牌组卡牌被移除时刷新）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_JADE_PENDANT
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class JadePendant : EnchantOnPickupRelicBase<SubstituteEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        RefreshStatus();
    }

    // 牌组卡牌被移除前刷新失效显示（排除即将被移除的这张）
    public override Task BeforeCardRemoved(CardModel card)
    {
        RefreshStatus(excluding: card);
        return Task.CompletedTask;
    }

    /// <summary>牌组中没有带「替死」的牌时置为失效，否则恢复。</summary>
    public void RefreshStatus(CardModel? excluding = null)
    {
        bool anySubstitute = PileType.Deck.GetPile(Owner).Cards
            .Any((CardModel c) => c != excluding && EnchantLimitService.HasEnchantment<SubstituteEnchantment>(c));
        Status = anySubstitute ? RelicStatus.Normal : RelicStatus.Disabled;
    }
}
