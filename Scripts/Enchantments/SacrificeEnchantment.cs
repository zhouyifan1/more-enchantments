using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「血祭」：打出时失去 Amount 点生命，获得 2×Amount 点力量
/// （失去生命参考卡牌 Bloodletting：无格挡、无力量加成的自我伤害）。
/// 卡面文本用 {StrengthGain} 直接展示力量最终数值。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_SACRIFICE_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class SacrificeEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 力量最终数值动态变量的键
    private const string _strengthGainKey = "StrengthGain";

    // 是否在卡牌上显示数值
    public override bool ShowAmount => true;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 动态变量：本地化中用 {StrengthGain} 占位，直接展示力量最终数值（2×Amount）
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar(_strengthGainKey, 2m)];

    // 额外悬停提示：解释「力量」能力
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    // 当附魔被应用时调用：同步力量最终数值变量（降级/读档重放 OnEnchant 时同样重新同步）
    protected override void OnEnchant()
    {
        DynamicVars[_strengthGainKey].BaseValue = 2 * Amount;
    }

    // 当附魔的卡牌被打出时调用：先失去生命，再获得力量
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        await CreatureCmd.Damage(choiceContext, Card.Owner.Creature, Amount, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Card, cardPlay);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Card.Owner.Creature, DynamicVars[_strengthGainKey].BaseValue, Card.Owner.Creature, Card);
    }
}
