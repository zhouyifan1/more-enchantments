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
/// 遗物「半碗战纹涂料」：拾起时，随机升级 1 张技能牌（参考原版遗物 WarPaint）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_HALF_BOWL_WAR_PAINT
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class HalfBowlWarPaint : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    // 商店附魔遗物池统一定价：普通 75
    public override int MerchantCost => 75;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/HalfBowlWarPaint.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/HalfBowlWarPaint.png",
        BigIconPath: "res://MoreEnchantments/images/relics/HalfBowlWarPaintBig.png"
    );

    public override Task AfterObtained()
    {
        foreach (CardModel item in PileType.Deck.GetPile(Owner).Cards.Where((CardModel c) => c != null && c.Type == CardType.Skill && c.IsUpgradable).ToList().StableShuffle(Owner.RunState.Rng.Niche).Take(DynamicVars.Cards.IntValue))
        {
            CardCmd.Upgrade(item);
        }
        return Task.CompletedTask;
    }
}
