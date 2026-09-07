using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
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

    // 拾起时：从牌组选择至多 MaxCards 张卡牌并附魔（参考遗物 GnarledHammer / BeautifulBracelet）
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
            EnchantmentModel? applied = CardCmd.Enchant(canonicalEnchantment.ToMutable(), item, EnchantAmount);
            // 反馈二选一（原版惯例：GnarledHammer 只 Preview、BeautifulBracelet 只播 vfx，两者同放会叠加成"两个动画"）。
            // NCardEnchantVfx 只显示主槽图标，落在附加槽时播放会误显老附魔图标，故主槽附着播 vfx、附加槽附着用 Preview（功能块 E 已知裁剪 U5）
            if (applied != null && item.Enchantment == applied)
            {
                NCardEnchantVfx? vfx = NCardEnchantVfx.Create(item);
                if (vfx != null)
                    NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
            }
            else
            {
                CardCmd.Preview(item);
            }
        }
    }
}
