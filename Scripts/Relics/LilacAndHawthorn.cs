using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Data;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「丁香与醋栗」（先古，诺奴佩普选项）：每回合你第一次打出有附魔的牌时，获得 1 点能量。
/// 能量变量/悬停提示参考遗物 BlessedAntler（EnergyVar + IncludeEnergyHoverTip）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_LILAC_AND_HAWTHORN
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class LilacAndHawthorn : ModRelicTemplate
{
    private bool _usedThisTurn;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 能量悬停提示（RitsuLib 自动前置）
    protected override bool IncludeEnergyHoverTip => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://MoreEnchantments/images/relics/LilacAndHawthorn.png",
        IconOutlinePath: "res://MoreEnchantments/images/relics/LilacAndHawthorn.png",
        BigIconPath: "res://MoreEnchantments/images/relics/LilacAndHawthornBig.png"
    );

    // 每回合已触发标记（遗物实例整局存在，回合开始时复位即可；无需存档）
    private bool UsedThisTurn
    {
        get => _usedThisTurn;
        set
        {
            AssertMutable();
            _usedThisTurn = value;
        }
    }

    // 你的回合开始时复位触发标记
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            UsedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    // 任意卡牌打出后：本回合首次打出带附魔（主槽或附加槽）的牌则获得能量
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (UsedThisTurn || cardPlay.Card.Owner != Owner)
        {
            return;
        }
        if (EnchantLimitService.GetEnchantmentCount(cardPlay.Card) <= 0)
        {
            return;
        }
        UsedThisTurn = true;
        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}
