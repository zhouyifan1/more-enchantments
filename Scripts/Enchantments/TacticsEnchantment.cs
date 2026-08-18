using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「战术」：打出后，下回合开始时获得 Amount 点能量（同一回合多次打出可叠加）。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_TACTICS_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class TacticsEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 已打出、待在下回合开始时结算的次数（战斗卡为克隆体，每场战斗自动归零）
    private int _pendingTriggers;

    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 当附魔的卡牌被打出时调用：记录一次待结算触发
    public override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        _pendingTriggers++;
        return Task.CompletedTask;
    }

    // 回合开始时结算：获得 次数×Amount 点能量
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Card.Owner || _pendingTriggers <= 0)
        {
            return;
        }
        await PlayerCmd.GainEnergy(Amount * _pendingTriggers, player);
        _pendingTriggers = 0;
    }
}
