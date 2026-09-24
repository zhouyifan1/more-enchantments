using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Events;

/// <summary>
/// 事件「古代科技」（Glory 章节限定）：摆弄巨大机器的残骸——把一张牌的附魔复制到一张无附魔的牌上、
/// 翻倍一张牌的附魔层数，或将遗物「陨铁石板」觉醒为「银白金属」。设计文档：Plan/Events/AncientTechnologyEvent.md。
/// 控制台测试：event MORE_ENCHANTMENTS_EVENT_ANCIENT_TECHNOLOGY_EVENT
/// </summary>
[RegisterActEvent(typeof(Glory))]
public sealed class AncientTechnologyEvent : ModEventTemplate
{
    // 立绘：未绘制前用占位图（PCK 资源，新增后需在 Godot 重新导出）
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://MoreEnchantments/images/events/AncientTechnologyEvent.png"
    );

    // 出现条件：牌组中至少有一张已附魔的牌与一张没有附魔的牌（保证 BUTTON / PULL_ROD 选项永远可结算）
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(p =>
        {
            IReadOnlyList<CardModel> deck = PileType.Deck.GetPile(p).Cards;
            return deck.Any(HasAnyEnchantment) && deck.Any(HasNoEnchantment);
        });

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        List<EventOption> options =
        [
            new EventOption(this, PressButton, InitialOptionKey("BUTTON")),
            new EventOption(this, PullRod, InitialOptionKey("PULL_ROD")),
        ];
        // ENLIGHTMENT 仅当持有遗物「陨铁石板」时出现
        if (Owner!.Relics.OfType<MeteoriteSlab>().Any())
            options.Add(new EventOption(this, Enlightment, InitialOptionKey("ENLIGHTMENT")));
        return options;
    }

    private static bool HasAnyEnchantment(CardModel card) => EnchantLimitService.GetEnchantmentCount(card) > 0;

    private static bool HasNoEnchantment(CardModel card) => EnchantLimitService.GetEnchantmentCount(card) == 0;

    // BUTTON：先选一张已附魔的牌（来源），再选一张没有附魔的牌（目标），把前者的附魔复制给后者（兼容才施加）
    private async Task PressButton()
    {
        CardModel? source = (await CardSelectCmd.FromDeckGeneric(Owner!,
            new CardSelectorPrefs(SelectPrompt("COPY_SOURCE"), 1, 1), HasAnyEnchantment)).FirstOrDefault();
        if (source != null)
        {
            CardModel? target = (await CardSelectCmd.FromDeckGeneric(Owner!,
                new CardSelectorPrefs(SelectPrompt("COPY_TARGET"), 1, 1), HasNoEnchantment)).FirstOrDefault();
            if (target != null)
            {
                EnchantLimitService.ApplyEnchantmentSnapshot(target, EnchantLimitService.GetEnchantmentSnapshot(source));
                CardCmd.Preview(target);
            }
        }
        SetEventFinished(PageDescription("BUTTON_END"));
    }

    // PULL_ROD：选择一张已附魔的牌，翻倍它的附魔层数
    private async Task PullRod()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckGeneric(Owner!,
            new CardSelectorPrefs(SelectPrompt("DOUBLE"), 1, 1), HasAnyEnchantment)).FirstOrDefault();
        if (selected != null)
        {
            EnchantLimitService.DoubleEnchantmentAmounts(selected);
            CardCmd.Preview(selected);
        }
        SetEventFinished(PageDescription("PULL_ROD_END"));
    }

    // ENLIGHTMENT：将「陨铁石板」变为「银白金属」（SilverMetal.AfterObtained 会自动翻转「沉重」的耗能）
    private async Task Enlightment()
    {
        RelicModel? slab = Owner!.Relics.OfType<MeteoriteSlab>().FirstOrDefault();
        if (slab != null)
            await RelicCmd.Replace(slab, ModelDb.Relic<SilverMetal>().ToMutable());
        SetEventFinished(PageDescription("ENLIGHTMENT_END"));
    }

    // 选牌界面标题（events.json 中的 {Entry}.selectPrompt.{KEY} 键）
    private LocString SelectPrompt(string key) => new("events", $"{Id.Entry}.selectPrompt.{key}");
}
