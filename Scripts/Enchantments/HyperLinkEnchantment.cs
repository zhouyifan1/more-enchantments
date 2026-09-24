using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「超链接」：当你打出有「超链接」的卡牌时，自动打出牌堆各处（手牌/抽牌堆/弃牌堆）所有带「超链接」的卡牌。
/// 自动打出用 CardCmd.AutoPlay（单体目标卡自动随机选敌，参考 HellraiserPower/StampedePower 的 null target 用法）。
/// 静态重入守卫：被连锁打出的「超链接」牌不再触发拉取，避免无限循环。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_HYPER_LINK_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class HyperLinkEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 连锁解析中标记：防止被拉出的超链接牌再次触发拉取（同一进程内战斗结算顺序执行，finally 保证复位）
    private static bool _isResolving;

    // 是否在卡牌上显示数值（无数值效果，关闭）
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 决定可以附魔到哪类卡牌上：不能附魔到能力牌（能力牌打出后离场，连锁自动打出无意义）
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType != CardType.Power;
    }

    // 任意卡牌打出后调用：是本卡则自动打出其余所有「超链接」牌
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != Card || _isResolving || Card.Owner.PlayerCombatState == null)
        {
            return;
        }
        List<CardModel> links = Card.Owner.PlayerCombatState.AllCards
            .Where((CardModel c) => c != Card && HasHyperLink(c) && c.Pile != null &&
                (c.Pile.Type == PileType.Hand || c.Pile.Type == PileType.Draw || c.Pile.Type == PileType.Discard))
            .ToList();
        if (links.Count == 0)
        {
            return;
        }
        _isResolving = true;
        try
        {
            foreach (CardModel link in links)
            {
                await CardCmd.AutoPlay(choiceContext, link, null);
            }
        }
        finally
        {
            _isResolving = false;
        }
    }

    // 卡牌是否具有「超链接」附魔（主槽或附加槽，见 EnchantLimitService 多附魔机制）
    private static bool HasHyperLink(CardModel card)
    {
        return EnchantLimitService.HasEnchantment<HyperLinkEnchantment>(card);
    }
}
