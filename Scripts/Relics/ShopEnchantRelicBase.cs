using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 「商店附魔遗物」中间基类：在拾起附魔基类之上套用商店池定价
/// （普通/罕见/稀有 → 75/125/175，低于原版 175/225/275，见 Plan/ShopEnchantRelics.md）。
/// 个别需要特殊定价的遗物可再覆写 MerchantCost。
/// 注意：不要在此基类上标注 [RegisterRelic]，每个具体遗物类自行标注。
/// </summary>
public abstract class ShopEnchantRelicBase<TEnchantment> : EnchantOnPickupRelicBase<TEnchantment> where TEnchantment : EnchantmentModel
{
    // 商店附魔遗物池统一定价基准
    public override int MerchantCost => Rarity switch
    {
        RelicRarity.Common => 75,
        RelicRarity.Uncommon => 125,
        RelicRarity.Rare => 175,
        _ => base.MerchantCost,
    };
}
