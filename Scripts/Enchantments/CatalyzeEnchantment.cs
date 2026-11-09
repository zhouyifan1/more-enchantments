using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「催化」：卡牌效果后，随机翻倍目标敌人身上的一种减益；群体攻击对每个敌人分别生效。
/// 直接设置层数以保证严格翻倍，不触发神器等新减益施加交互。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_CATALYZE_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class CatalyzeEnchantment : MoreEnchantmentsEnchantmentBase
{
    public override bool ShowAmount => false;

    public override bool HasExtraCardText => true;

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    public override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (cardPlay?.Target != null)
        {
            DoubleRandomDebuff(cardPlay.Target);
        }
        else if (Card.CombatState != null)
        {
            foreach (Creature enemy in Card.CombatState.HittableEnemies)
            {
                DoubleRandomDebuff(enemy);
            }
        }
        return Task.CompletedTask;
    }

    private void DoubleRandomDebuff(Creature target)
    {
        List<PowerModel> debuffs = target.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff && p.Amount > 0 && p is not ITemporaryPower)
            .ToList();
        if (debuffs.Count == 0)
        {
            return;
        }
        PowerModel selected = Card.Owner.RunState.Rng.CombatCardSelection.NextItem(debuffs)!;
        selected.SetAmount(selected.Amount * 2);
    }
}
