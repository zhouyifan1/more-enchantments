using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「银白金属」（事件）：附魔「沉重」的耗能 +1 效果改为耗能 -1。
/// 机制：HeavyEnchantment.OnEnchant 实时读取持有者是否拥有本遗物决定费用增减方向；
/// 本遗物负责两个时点的存量修正——拾起时（AfterObtained）与读档后（SilverMetalLoadFixPatch，
/// 因为 Player.LoadInventory 先反序列化牌组重放 OnEnchant、后加载遗物，重放时一律按 +1 落账）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_SILVER_METAL
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class SilverMetal : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    // 拾起时触发效果（驱动原版拾起提示 UI）
    public override bool HasUponPickupEffect => true;

    // 悬停提示展示「沉重」附魔说明
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory.FromEnchantment<HeavyEnchantment>(1);

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/SilverMetal.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/SilverMetal.png",
        BigIconPath: "res://MoreEnchantments/images/relics/SilverMetalBig.png"
    );

    public override Task AfterObtained()
    {
        FlipHeavyCosts(Owner);
        return Task.CompletedTask;
    }

    /// <summary>把牌组中所有带「沉重」的卡牌耗能调整 -2（由 +1 翻转为 -1）。拾起时与读档后修正共用。</summary>
    internal static void FlipHeavyCosts(Player player)
    {
        foreach (CardModel card in PileType.Deck.GetPile(player).Cards)
        {
            if (!EnchantLimitService.HasEnchantment<HeavyEnchantment>(card))
            {
                continue;
            }
            if (card.EnergyCost.CostsX || card.EnergyCost.Canonical < 0)
            {
                continue;
            }
            card.EnergyCost.SetCustomBaseCost(card.EnergyCost.GetWithModifiers(CostModifiers.None) - 2);
        }
    }
}
