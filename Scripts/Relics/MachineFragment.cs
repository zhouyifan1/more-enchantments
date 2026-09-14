using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「机器残片」（先古，欧洛巴斯选项）：拾起时，从牌组选择至多 2 张耗能不大于 2 的卡牌，附魔「超链接」1 层。
/// 费用限制通过 CardSelectCmd.FromDeckForEnchantment 的 additionalFilter 实现（附魔本身不限定费用）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_MACHINE_FRAGMENT
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class MachineFragment : ModRelicTemplate
{
    // 可选择的最大卡牌数
    private const int _maxCards = 2;

    // 费用上限（耗能不大于 2）
    private const int _maxCost = 2;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 拾起时触发效果（驱动原版拾起提示 UI）
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(_maxCards)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/MachineFragment.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/MachineFragment.png",
        BigIconPath: "res://MoreEnchantments/images/relics/MachineFragmentBig.png"
    );

    // 拾起时：从牌组选择至多 2 张耗能 ≤2 的卡牌并附魔（流程参考 EnchantOnPickupRelicBase）
    public override async Task AfterObtained()
    {
        CardSelectorPrefs prefs = new(CardSelectorPrefs.EnchantSelectionPrompt, 0, _maxCards)
        {
            Cancelable = false,
            RequireManualConfirmation = true
        };
        HyperLinkEnchantment canonical = ModelDb.Enchantment<HyperLinkEnchantment>();
        foreach (CardModel item in await CardSelectCmd.FromDeckForEnchantment(Owner, canonical, 1, IsAffordable, prefs))
        {
            CardCmd.Enchant(canonical.ToMutable(), item, 1m);
            CardCmd.Preview(item);
        }
    }

    // 费用限制：耗能不大于 2（排除 X 耗能与特殊费用卡）
    internal static bool IsAffordable(CardModel? card)
    {
        return card != null && !card.EnergyCost.CostsX && card.EnergyCost.Canonical >= 0 && card.EnergyCost.Canonical <= _maxCost;
    }
}
