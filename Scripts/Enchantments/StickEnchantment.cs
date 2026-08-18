using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「粘手」：每场战斗前 Amount 次被打出时回到手牌，而非进入弃牌堆
/// （落点改写参考卡牌 ParticleWall）。卡面文本与角标实时显示剩余次数，次数用完后图标置灰。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_STICK_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class StickEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 剩余回手次数动态变量的键
    private const string _returnsKey = "Returns";

    // 本场战斗已触发次数（战斗卡为克隆体，每场战斗自动归零）
    private int _playsThisCombat;

    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 角标显示剩余回手次数而非总次数
    public override int DisplayAmount => Math.Max(0, Amount - _playsThisCombat);

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 动态变量：本地化中用 {Returns} 占位，实时显示剩余回手次数
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar(_returnsKey, 1m)];

    // 决定可以附魔到哪类卡牌上：不能附魔到能力牌（能力牌打出后离场，没有回到手牌的意义）
    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType != CardType.Power;
    }

    // 当附魔被应用时调用：剩余次数初始化为 Amount（降级/读档重放 OnEnchant 时同样回到满次数）
    protected override void OnEnchant()
    {
        DynamicVars[_returnsKey].BaseValue = Amount;
    }

    // 修改卡牌打出后的结果位置：次数未用完时，弃牌堆改为手牌
    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)
    {
        if (card == Card && _playsThisCombat < Amount && cardLocation.pileType == PileType.Discard)
        {
            cardLocation.pileType = PileType.Hand;
        }
        return cardLocation;
    }

    // 卡牌打出后计数并刷新剩余次数变量，次数用完后置为 Disabled（图标置灰、额外文本隐藏）
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card == Card && _playsThisCombat < Amount)
        {
            _playsThisCombat++;
            DynamicVars[_returnsKey].BaseValue = Amount - _playsThisCombat;
            if (_playsThisCombat >= Amount)
            {
                Status = EnchantmentStatus.Disabled;
            }
        }
        return Task.CompletedTask;
    }
}
