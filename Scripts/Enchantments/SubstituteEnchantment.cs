using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「替死」：你死亡时阻止本次死亡并恢复 25% 最大生命值，随后随机移除一张附有「替死」的卡牌
/// （战斗克隆体与牌组原件一并移除）。触发位点在药水「瓶中精灵」之前（不消耗其充能），
/// 由 SubstituteDeathPreventionPatch 在 Hook.ShouldDie 前实现——参考瓶中精灵（ShouldDie 早段）
/// 与蜥蜴尾巴（ShouldDieLate 晚段）的原版两段式顺序。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_SUBSTITUTE_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class SubstituteEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 恢复的最大生命值比例（DynamicVar "HealPercent"，本地化用 {HealPercent} 占位）
    private const string _healPercentKey = "HealPercent";

    // 是否在卡牌上显示数值（死亡时触发型效果，不显示）
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本（设计：不加卡面文本，仅悬停提示）
    public override bool HasExtraCardText => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar(_healPercentKey, 25m)];

    // 决定可以附魔到哪张卡牌上：不能附魔到初始牌（基础稀有度）
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card.Rarity != CardRarity.Basic;
    }

    // 阻止死亡后由 Hook.AfterPreventingDeath 回调（本附魔实例作为 preventer）：回血 + 随机移除一张替死牌
    public override async Task AfterPreventingDeath(Creature creature)
    {
        decimal healAmount = Math.Max(1m, creature.MaxHp * (DynamicVars[_healPercentKey].BaseValue / 100m));
        await CreatureCmd.Heal(creature, healAmount);
        await RemoveRandomSubstituteCard(Card.Owner);
    }

    /// <summary>
    /// 随机移除一张带「替死」的卡牌：先移除其战斗克隆体（若在本场战斗中），再移除牌组原件。
    /// 同时刷新「玉佩」的失效显示。
    /// </summary>
    internal static async Task RemoveRandomSubstituteCard(Player player)
    {
        List<CardModel> deckCards = PileType.Deck.GetPile(player).Cards
            .Where((CardModel c) => EnchantLimitService.HasEnchantment<SubstituteEnchantment>(c))
            .ToList();
        if (deckCards.Count == 0)
        {
            return;
        }
        CardModel chosen = player.RunState.Rng.CombatCardSelection.NextItem(deckCards)!;
        if (player.PlayerCombatState != null)
        {
            CardModel? clone = player.PlayerCombatState.AllCards.FirstOrDefault((CardModel c) => c.DeckVersion == chosen);
            if (clone != null)
            {
                await CardPileCmd.RemoveFromCombat(clone);
            }
        }
        await CardPileCmd.RemoveFromDeck(chosen);
        foreach (JadePendant pendant in player.Relics.OfType<JadePendant>())
        {
            pendant.RefreshStatus();
        }
    }
}
