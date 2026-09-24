using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MoreEnchantments.Scripts.Enchantments;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Scaffolding.Ancients.Options;

namespace MoreEnchantments.Scripts.Data;

/// <summary>
/// 原版先古遗物池修改：池级注入（主路径）+ RitsuLib 追加式注册（兜底路径）。
/// 使用指导见 Plan/Hooks.md §4「原版先古之民遗物池修改指导」。
///
/// 主路径（见 Scripts/Patches/AncientRelicPoolPatches.cs）：
/// - 达弗：反射向静态 _validRelicSets 追加遗物组（InjectDarvRelicSet，懒执行，由补丁在首次生成选项前触发）；
/// - 捏奥/欧洛巴斯：私有选项池 getter 的 postfix 追加（CreateRelicOption 构建选项）。
/// 兜底路径：补丁失败时 Entry 调 RegisterFallback()，改用 RitsuLib RegisterAncientOption 追加
/// （出现在初始选项末尾，权重由工厂内掷骰近似；选项 textKey 与主路径一致以便去重）。
/// </summary>
internal static class AncientOptionRules
{
    // AncientEventModel.Done()（protected，无参）：选择选项后结束事件（CustomDonePage 保持 null → 默认 DONE 页）
    private static readonly MethodInfo _doneMethod = PrivateAccess.DeclaredMethod(typeof(AncientEventModel), "Done");

    // 达弗池注入是否已完成（防重入；注入在首次生成选项前由补丁懒触发）
    internal static bool DarvInjected { get; private set; }

    private static bool _fallbackRegistered;

    /// <summary>
    /// 达弗池注入：向静态 _validRelicSets 追加一组 { filter: 牌组可附魔（滴答）牌 ≥12, relics: [老旧的怀表] }。
    /// 注入后完全走原生"每组摇 1、洗牌取 3"流程，权重与出场条件由原生机制保证。
    /// 注意只能在 ModelDb 就绪后调用（读取静态字段会触发 Darv 静态构造，其内部即访问 ModelDb）。
    /// </summary>
    internal static void InjectDarvRelicSet()
    {
        if (DarvInjected)
        {
            return;
        }
        FieldInfo field = PrivateAccess.DeclaredField(typeof(Darv), "_validRelicSets")
            ?? throw new MissingFieldException("Darv", "_validRelicSets");
        if (field.GetValue(null) is not IList list)
        {
            throw new InvalidOperationException("Darv._validRelicSets 不是 IList，游戏版本可能已变更。");
        }
        Type setType = typeof(Darv).GetNestedType("ValidRelicSet", BindingFlags.NonPublic)
            ?? throw new MissingMemberException("Darv", "ValidRelicSet");
        Func<Player, bool> filter = owner => CountEnchantable<TickTockEnchantment>(owner, null) >= 12;
        object set = Activator.CreateInstance(setType, filter, new RelicModel[] { ModelDb.Relic<OldPocketWatch>() })
            ?? throw new InvalidOperationException("构造 Darv.ValidRelicSet 失败。");
        list.Add(set);
        DarvInjected = true;
    }

    /// <summary>兜底：三个先古遗物全部走 RitsuLib 追加式注册（幂等）。</summary>
    public static void RegisterFallback()
    {
        if (_fallbackRegistered)
        {
            return;
        }
        _fallbackRegistered = true;
        RegisterDarvFallback();
        RitsuLibFramework.RegisterAncientOption<Orobas>(Entry.ModId, ModAncientOptionRule.Single(
            ancient => Roll(ancient, 1f / 3f) ? CreateRelicOption<MachineFragment>(ancient) : null,
            ancient => ancient.Owner != null && CountEnchantable<HyperLinkEnchantment>(ancient.Owner, MachineFragment.IsAffordable) >= 2,
            0, true));
        RitsuLibFramework.RegisterAncientOption<Neow>(Entry.ModId, ModAncientOptionRule.Single(
            ancient => Roll(ancient, 0.1f) ? CreateRelicOption<TouchOfMidas>(ancient) : null,
            null, 0, true));
        // 诺奴佩普/坦克斯：单池 9 候选洗牌取 3，近似命中率 3/(9+1)
        RitsuLibFramework.RegisterAncientOption<Nonupeipe>(Entry.ModId, ModAncientOptionRule.Single(
            ancient => Roll(ancient, 0.3f) ? CreateRelicOption<LilacAndHawthorn>(ancient) : null,
            null, 0, true));
        RitsuLibFramework.RegisterAncientOption<Tanx>(Entry.ModId, ModAncientOptionRule.Single(
            ancient => Roll(ancient, 0.3f) ? CreateRelicOption<SteelSwordAndSilverSword>(ancient) : null,
            null, 0, true));
    }

    /// <summary>兜底：仅达弗（池注入运行期失败时由补丁调用；幂等）。</summary>
    public static void RegisterDarvFallback()
    {
        RitsuLibFramework.RegisterAncientOption<Darv>(Entry.ModId, ModAncientOptionRule.Single(
            ancient => Roll(ancient, 0.25f) ? CreateRelicOption<OldPocketWatch>(ancient) : null,
            ancient => ancient.Owner != null && CountEnchantable<TickTockEnchantment>(ancient.Owner, null) >= 12,
            0, true));
    }

    /// <summary>
    /// 构建遗物选项：选中后获得遗物并结束事件（等价原生 AncientEventModel.RelicOption 的 OnChosen）。
    /// textKey 按原生 OptionKey 约定生成；本地化缺失时 EventOption.FromRelic 回退为遗物自身标题/描述。
    /// ancient.Owner 为 null 时属于图鉴/控制台展示路径：仍构建选项（保证图鉴归属可见），仅跳过遗物 Owner 预赋值。
    /// </summary>
    internal static EventOption CreateRelicOption<TRelic>(AncientEventModel ancient) where TRelic : RelicModel
    {
        RelicModel relic = ModelDb.Relic<TRelic>().ToMutable();
        if (ancient.Owner != null)
        {
            relic.Owner = ancient.Owner;
        }
        string textKey = $"{ancient.Id.Entry}.pages.INITIAL.options.{relic.Id.Entry}";
        return EventOption.FromRelic(relic, ancient, OnChosen, textKey);

        async Task OnChosen()
        {
            Player owner = ancient.Owner ?? throw new InvalidOperationException("Ancient option chosen before owner assignment.");
            await RelicCmd.Obtain(relic, owner);
            _doneMethod.Invoke(ancient, null);
        }
    }

    /// <summary>牌组中可被指定附魔附着（忽略占用限制）且满足额外过滤的卡牌数。</summary>
    internal static int CountEnchantable<TEnchantment>(Player player, Func<CardModel?, bool>? additionalFilter)
        where TEnchantment : EnchantmentModel
    {
        TEnchantment canonical = ModelDb.Enchantment<TEnchantment>();
        return PileType.Deck.GetPile(player).Cards.Count((CardModel c) =>
            (additionalFilter == null || additionalFilter(c)) && EnchantLimitService.EvaluateIgnoringOccupancy(canonical, c));
    }

    // 兜底路径的权重掷骰：用先古事件自身的随机流（与原生选项生成同源，读档重放确定）
    private static bool Roll(AncientEventModel ancient, float probability)
    {
        return ancient.Rng.NextFloat() < probability;
    }
}
