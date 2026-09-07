using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 附魔「潘多拉」：每当你抽到这张牌时，将其变化为一张随机卡牌，新卡牌继承此附魔。
/// 抽牌时机参考卡牌 KinglyPunch（AfterCardDrawn 过滤 card == Card）；
/// 变化参考状态 EntropyPower（CardCmd.TransformToRandom，走 CombatCardSelection 随机流）。
/// 变化仅作用于战斗克隆体，牌组中的原卡不受影响。
/// 控制台测试：enchant MORE_ENCHANTMENTS_ENCHANTMENT_PANDORA_ENCHANTMENT [层数] [手牌位置]
/// </summary>
[RegisterEnchantment]
public class PandoraEnchantment : MoreEnchantmentsEnchantmentBase
{
    // 是否在卡牌上显示数值（无数值效果，关闭）
    public override bool ShowAmount => false;

    // 是否会添加额外的卡牌描述文本
    public override bool HasExtraCardText => true;

    // 额外悬停提示：解释「变化」关键词（与原版 Entropy 相同）
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Transform)];

    // 任意卡牌被抽到时调用：是本卡则变化并继承附魔
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != Card)
        {
            return;
        }
        // 变化为随机卡牌（参考 EntropyPower；TransformToRandom 在原牌堆原位替换）
        var result = await CardCmd.TransformToRandom(Card, Card.Owner.RunState.Rng.CombatCardSelection);
        if (!result.success)
        {
            return;
        }
        // 新卡继承此附魔（含层数）；防御性检查 CanEnchant，避免极端情况下抛异常
        PandoraEnchantment canonical = ModelDb.Enchantment<PandoraEnchantment>();
        if (canonical.CanEnchant(result.cardAdded))
        {
            CardCmd.Enchant<PandoraEnchantment>(result.cardAdded, Amount);
        }
    }
}
