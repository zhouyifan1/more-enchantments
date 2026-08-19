using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「小块磨刀石」：拾起时，随机升级 1 张攻击牌（参考原版遗物 Whetstone）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SMALL_WHETSTONE
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class SmallWhetstone : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    // 商店附魔遗物池统一定价：普通 75
    public override int MerchantCost => 75;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/SmallWhetstone.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/SmallWhetstone.png",
        BigIconPath: "res://MoreEnchantments/images/relics/SmallWhetstoneBig.png"
    );

    public override Task AfterObtained()
    {
        foreach (CardModel item in PileType.Deck.GetPile(Owner).Cards.Where((CardModel c) => c != null && c.Type == CardType.Attack && c.IsUpgradable).ToList().StableShuffle(Owner.RunState.Rng.Niche).Take(DynamicVars.Cards.IntValue))
        {
            CardCmd.Upgrade(item);
        }
        return Task.CompletedTask;
    }
}
