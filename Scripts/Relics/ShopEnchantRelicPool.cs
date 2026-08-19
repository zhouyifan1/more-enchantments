using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 「商店附魔遗物」遗物池：池内遗物只通过商店新增的 3 个专属栏位售卖（见 Patches/ShopInventoryPatch）。
/// 用法：池内遗物标注 [RegisterRelic(typeof(ShopEnchantRelicPool))]，稀有度用 Common/Uncommon/Rare。
/// 机制说明：
/// - 常规掉落抓袋 RelicGrabBag 只读 SharedRelicPool + 角色池，本池遗物天然不进战斗掉落/宝箱/原生商店栏位；
/// - 商店栏位每次从池中 ToMutable() 出新实例、不经过抓袋去重，因此允许单局内重复获得同一遗物；
/// - 同一商店内不重复（由 ShopInventoryPatch 抽取时排除已选）。
/// </summary>
[RegisterSharedRelicPool]
public class ShopEnchantRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "colorless";
}
