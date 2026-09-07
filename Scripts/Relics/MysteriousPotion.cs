using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「神秘药剂」：拾起时，卡牌附魔上限提高 1（调 EnchantLimitService.AddBonus，上限随 run 存档持久化）。
/// Shop 稀有度与原版 Brimstone 同款：不进战斗掉落抓袋，仅商店出售。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_MYSTERIOUS_POTION
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class MysteriousPotion : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/MysteriousPotion.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/MysteriousPotion.png",
        BigIconPath: "res://MoreEnchantments/images/relics/MysteriousPotionBig.png"
    );

    public override Task AfterObtained()
    {
        Flash();
        EnchantLimitService.AddBonus(Owner, 1);
        return Task.CompletedTask;
    }
}
