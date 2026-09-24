using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「钢剑与银剑」（先古，坦克斯选项）：打出攻击牌时计数 +1，计数在你的回合开始时清零。
/// 每回合第奇数次打出的攻击牌在打出前附魔「锋利」2，第偶数次打出的攻击牌在打出前附魔「弱化」1。
/// 附魔在 BeforeCardPlayed 施加（参考遗物 PaelsEye 的打出前钩子），本次打出的结算即生效；
/// 作用于战斗克隆体，不污染牌组。满槽/条件不满足时跳过（CanEnchant 已被多槽补丁扩展为全路由判定）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_STEEL_SWORD_AND_SILVER_SWORD
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class SteelSwordAndSilverSword : ModRelicTemplate
{
    private const string _sharpAmountKey = "SharpAmount";
    private const string _weakeningAmountKey = "WeakeningAmount";

    private int _attacksThisTurn;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 遗物上显示本回合打出的攻击牌计数
    public override bool ShowCounter => true;

    public override int DisplayAmount => AttacksThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar(_sharpAmountKey, 2m), new IntVar(_weakeningAmountKey, 1m)];

    // 悬停提示：展示两种将附着的附魔说明
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [.. HoverTipFactory.FromEnchantment<Sharp>(2), .. HoverTipFactory.FromEnchantment<WeakeningEnchantment>(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/SteelSwordAndSilverSword.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/SteelSwordAndSilverSword.png",
        BigIconPath: "res://MoreEnchantments/images/relics/SteelSwordAndSilverSwordBig.png"
    );

    // 本回合打出的攻击牌计数（遗物实例整局存在，回合开始清零；无需存档）
    private int AttacksThisTurn
    {
        get => _attacksThisTurn;
        set
        {
            AssertMutable();
            _attacksThisTurn = value;
            InvokeDisplayAmountChanged();
        }
    }

    // 你的回合开始时清零计数
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            AttacksThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    // 任意卡牌打出前：本牌为持有者的攻击牌则计数 +1，并按奇偶在打出前附着对应附魔
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;
        if (card.Owner != Owner || card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }
        AttacksThisTurn++;
        if (AttacksThisTurn % 2 == 1)
        {
            TryEnchant<Sharp>(card, DynamicVars[_sharpAmountKey].IntValue);
        }
        else
        {
            TryEnchant<WeakeningEnchantment>(card, DynamicVars[_weakeningAmountKey].IntValue);
        }
        return Task.CompletedTask;
    }

    // 打出前附着附魔：先以 CanEnchant 预判（多槽补丁已将其扩展为全路由"能否附着"判定），不可附着则跳过
    private static void TryEnchant<TEnchantment>(CardModel card, int amount) where TEnchantment : EnchantmentModel
    {
        if (ModelDb.Enchantment<TEnchantment>().CanEnchant(card))
        {
            CardCmd.Enchant<TEnchantment>(card, amount);
        }
    }
}
