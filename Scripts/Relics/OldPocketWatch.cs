using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「老旧的怀表」（先古，达弗选项）：拾起时从牌组选择至多 12 张牌附魔「滴答」；
/// 这些牌每被打出一次计数 +1，计数达到 12 时获得 1 个额外回合。
/// 计数用 [SavedProperty] 持久化（跨战斗/读档继承，参考 HappyFlower.TurnsSeen）；
/// 额外回合参考 PaelsEye（ShouldTakeExtraTurn / AfterTakingExtraTurn）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_OLD_POCKET_WATCH
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class OldPocketWatch : EnchantOnPickupRelicBase<TickTockEnchantment>
{
    // 触发额外回合的计数阈值（DynamicVar "Threshold"，本地化用 {Threshold} 占位）
    private const string _thresholdKey = "Threshold";

    private bool _pendingExtraTurn;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override int MaxCards => 12;

    protected override int EnchantAmount => 1;

    // 遗物上显示计数角标
    public override bool ShowCounter => true;

    public override int DisplayAmount => Count;

    protected override IEnumerable<DynamicVar> CanonicalVars => base.CanonicalVars.Concat([new DynamicVar(_thresholdKey, 12m)]);

    // 滴答计数（跨战斗继承；随 run 存档）
    [SavedProperty]
    public int Count { get; private set; }

    // 附魔「滴答」的卡牌被打出时由 TickTockEnchantment 调用
    public void AddTick()
    {
        AssertMutable();
        Count++;
        InvokeDisplayAmountChanged();
        if (Count >= DynamicVars[_thresholdKey].IntValue)
        {
            Count = 0;
            _pendingExtraTurn = true;
            InvokeDisplayAmountChanged();
        }
    }

    // 计数满后本回合结束时获得额外回合（参考 PaelsEye）
    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner && _pendingExtraTurn;
    }

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }
        Flash();
        _pendingExtraTurn = false;
        return Task.CompletedTask;
    }
}
