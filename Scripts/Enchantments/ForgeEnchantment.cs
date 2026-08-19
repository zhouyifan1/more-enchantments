using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「铸剑」：打出时铸造 Amount（参考卡牌 TheSmith）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_FORGE_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class ForgeEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「铸造」机制
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromForge();

    // 当附魔的卡牌被打出时调用：铸造 Amount 点
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        await ForgeCmd.Forge(Amount, Card.Owner, Card);
    }
}
