using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「滴答」：当你打出有「滴答」的卡牌时，为遗物「老旧的怀表」增加 1 点计数。
/// 计数与结算由遗物侧 OldPocketWatch 持有（计数可跨战斗继承，见遗物注释）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_TICK_TOCK_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class TickTockEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 任意卡牌打出后调用：是本卡则为持有者的「老旧的怀表」计数 +1（未持有该遗物时无事发生）
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card == Card)
        {
            Card.Owner.Relics.OfType<OldPocketWatch>().FirstOrDefault()?.AddTick();
        }
        return Task.CompletedTask;
    }
}
