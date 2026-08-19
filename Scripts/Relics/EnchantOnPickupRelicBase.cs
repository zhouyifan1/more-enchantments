using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 本 Mod「拾起时为卡牌附魔」遗物的抽象基类：从牌组中选择至多 MaxCards 张卡牌，附加指定附魔。
/// 选择流程参考原版遗物 GnarledHammer；可选卡牌范围由选择器按附魔自身的 CanEnchant 自动过滤。
/// 注意：不要在此基类上标注 [RegisterRelic]，每个具体遗物类自行标注。
/// </summary>
public abstract class EnchantOnPickupRelicBase<TEnchantment> : ModRelicTemplate where TEnchantment : EnchantmentModel
{
    // 可选择的最大卡牌数
    protected abstract int MaxCards { get; }

    // 附加的附魔层数
    protected abstract int EnchantAmount { get; }

    // 拾起时触发效果（驱动原版拾起提示 UI）
    public override bool HasUponPickupEffect => true;

    // 动态变量：本地化中用 {Cards}（可选卡牌数）与 {EnchantAmount}（附魔层数）占位
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(MaxCards), new IntVar("EnchantAmount", EnchantAmount)];

    // 额外悬停提示：展示将附加的附魔说明（ModRelicTemplate 的 ExtraHoverTips 已密封，用 AdditionalHoverTips 扩展）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory.FromEnchantment<TEnchantment>(EnchantAmount);

    // 统一图标路径约定：res://MoreEnchantments/images/relics/{类名}.png（85x85）与 {类名}Big.png（256x256）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://MoreEnchantments/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://MoreEnchantments/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://MoreEnchantments/images/relics/{GetType().Name}Big.png"
    );

    // 拾起时：从牌组选择至多 MaxCards 张卡牌并附魔（参考遗物 GnarledHammer）
    public override async Task AfterObtained()
    {
        CardSelectorPrefs prefs = new(CardSelectorPrefs.EnchantSelectionPrompt, 0, MaxCards)
        {
            Cancelable = false,
            RequireManualConfirmation = true
        };
        TEnchantment canonicalEnchantment = ModelDb.Enchantment<TEnchantment>();
        foreach (CardModel item in await CardSelectCmd.FromDeckForEnchantment(Owner, canonicalEnchantment, EnchantAmount, prefs))
        {
            CardCmd.Enchant(canonicalEnchantment.ToMutable(), item, EnchantAmount);
            CardCmd.Preview(item);
        }
    }
}
