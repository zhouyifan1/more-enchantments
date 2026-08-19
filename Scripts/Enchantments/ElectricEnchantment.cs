using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Orbs;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「电动」：打出时生成 Amount 个闪电充能球（参考卡牌 Zap）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_ELECTRIC_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class ElectricEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「生成充能球」机制与「闪电」充能球
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Channeling),
        HoverTipFactory.FromOrb<LightningOrb>(),
    ];

    // 当附魔的卡牌被打出时调用：逐个生成 Amount 个闪电充能球
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        for (int i = 0; i < Amount; i++)
        {
            await OrbCmd.Channel<LightningOrb>(choiceContext, Card.Owner);
        }
    }
}
