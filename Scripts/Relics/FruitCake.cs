using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「水果蛋糕」：每回合抽牌结束后（AfterPlayerTurnStart 紧随 Draw，见 CombatManager 回合流程），
/// 为随机一张手牌施加随机附魔 1 层（RandomEnchantService 默认均匀权重表；候选为空则无效果）。
/// 随机走 CombatCardSelection 流，读档/联机确定一致。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_FRUIT_CAKE
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class FruitCake : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/FruitCake.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/FruitCake.png",
        BigIconPath: "res://MoreEnchantments/images/relics/FruitCakeBig.png"
    );

    // 你的回合抽牌结束后：随机一张手牌施加随机附魔 1 层
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }
        List<CardModel> hand = CardPile.GetCards(Owner, PileType.Hand).ToList();
        if (hand.Count == 0)
        {
            return;
        }
        CardModel card = Owner.RunState.Rng.CombatCardSelection.NextItem(hand)!;
        EnchantmentModel? applied = RandomEnchantService.RollAndEnchant(card, 1m, Owner.RunState.Rng.CombatCardSelection);
        if (applied != null)
        {
            Flash();
            CardCmd.Preview(card);
        }
        await Task.CompletedTask;
    }
}
