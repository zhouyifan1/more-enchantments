using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「神秘药剂」：拾起时，卡牌附魔上限提高 1（调 EnchantLimitService.AddBonus，上限随 run 存档持久化），
/// 并使你下一次为卡牌附魔的层数翻倍；翻倍生效后遗物失效。
/// 失效模式参考遗物 MawBank（IsUsedUp + [SavedProperty] + Status = Disabled）；
/// 翻倍由 MysteriousPotionPatch（CardCmd.Enchant 前缀，ref amount ×2）实现。
/// Shop 稀有度与原版 Brimstone 同款：不进战斗掉落抓袋，仅商店出售。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_MYSTERIOUS_POTION
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class MysteriousPotion : ModRelicTemplate
{
    private bool _hasDoublingBeenConsumed;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    // 翻倍生效后遗物失效（图标置灰，参考 MawBank）
    public override bool IsUsedUp => HasDoublingBeenConsumed;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/MysteriousPotion.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/MysteriousPotion.png",
        BigIconPath: "res://MoreEnchantments/images/relics/MysteriousPotionBig.png"
    );

    // 翻倍是否已消耗（随 run 存档，参考 MawBank.HasItemBeenBought）
    [SavedProperty]
    public bool HasDoublingBeenConsumed
    {
        get => _hasDoublingBeenConsumed;
        private set
        {
            AssertMutable();
            _hasDoublingBeenConsumed = value;
            if (IsUsedUp)
            {
                Status = RelicStatus.Disabled;
            }
        }
    }

    public override Task AfterObtained()
    {
        Flash();
        EnchantLimitService.AddBonus(Owner, 1);
        return Task.CompletedTask;
    }

    // 由 MysteriousPotionPatch 在翻倍生效时调用
    public void ConsumeDoubling()
    {
        Flash();
        HasDoublingBeenConsumed = true;
    }
}
