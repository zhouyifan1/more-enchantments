using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace MoreEnchantments.Scripts.Data;

/// <summary>
/// 附魔上限服务（功能块 E 的公开 API，供本 Mod 及联动 Mod 使用）。
/// 架构：原版单槽 Enchantment 作为 0 号主槽，第 2~N 个附魔存本 Mod 附加槽（见 ExtraEnchantmentStore）。
/// 上限语义：当前上限 = max(局内峰值, BaseLimit + 局内加成)；峰值随 run 存档持久化——
/// 局内只可提高（遗物/事件调 AddBonus），任何路径（含读档、BaseLimit 调低）都不会让上限下降。
/// </summary>
public static class EnchantLimitService
{
    /// <summary>局内上限数据（随 run 存档，按玩家独立）。加字段直接加属性，勿改注册 key。</summary>
    public class EnchantLimitData
    {
        public int Bonus { get; set; }
        public int Peak { get; set; }
    }

    /// <summary>
    /// 代码中可修改的默认上限（影响之后的新局；1 = 原版行为）。建议 1~3，默认 2。
    /// 注意：发布后调整视为平衡性改动，不影响存档兼容。
    /// </summary>
    public static int BaseLimit { get; set; } = 2;

    private static PlayerRunSavedData<EnchantLimitData>? _limitData;

    /// <summary>上限变化事件（UI/系统刷新用）；参数为玩家与新上限。</summary>
    public static event Action<Player, int>? LimitChanged;

    internal static void RegisterData()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            _limitData = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId)
                .RegisterPerPlayer("enchant_limit", () => new EnchantLimitData(),
                    new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenNonDefault });
        }
    }

    /// <summary>当前上限：max(局内峰值, BaseLimit)。局外/无 run 数据时返回 BaseLimit。</summary>
    public static int GetLimit(Player? player)
    {
        if (player?.RunState == null || _limitData == null)
            return BaseLimit;
        return Math.Max(BaseLimit, _limitData.Get(player).Peak);
    }

    /// <summary>局内动态提高上限（遗物/事件调用）。amount &lt;= 0 时忽略。</summary>
    public static void AddBonus(Player player, int amount)
    {
        if (amount <= 0 || player.RunState == null || _limitData == null)
            return;
        int newLimit = BaseLimit;
        _limitData.Modify(player, data =>
        {
            data.Bonus += amount;
            data.Peak = Math.Max(data.Peak, BaseLimit + data.Bonus);
            newLimit = Math.Max(BaseLimit, data.Peak);
        });
        LimitChanged?.Invoke(player, newLimit);
    }

    /// <summary>一张卡当前的附魔总数（主槽 + 附加槽）。</summary>
    public static int GetEnchantmentCount(CardModel card) =>
        (card.Enchantment != null ? 1 : 0) + ExtraEnchantmentStore.Get(card).Count;

    /// <summary>该卡是否还能再附着新类型附魔。</summary>
    public static bool HasFreeSlot(CardModel card) => GetEnchantmentCount(card) < GetLimit(card.Owner);

    /// <summary>附加槽列表（只读，按附着顺序）。</summary>
    public static IReadOnlyList<EnchantmentModel> GetExtraEnchantments(CardModel card) =>
        ExtraEnchantmentStore.Get(card);

    /// <summary>
    /// 「忽略占用」评估：回答"若该卡没有附魔占用限制，此附魔能否附着"。
    /// 同型规则优先：任一槽已有同型 → 仅 IsStackable 允许（堆叠语义，与原版一致）。
    /// 其余评估通过暂时置空主槽后调用真实 CanEnchant 完成——派生类的自定义条件
    /// （如 Resilience 要求格挡变量）走原始代码路径，结果精确。
    /// </summary>
    public static bool EvaluateIgnoringOccupancy(EnchantmentModel enchantment, CardModel card)
    {
        if (HasSameType(card, enchantment.GetType()))
            return enchantment.IsStackable;
        if (card.Enchantment == null)
            return enchantment.CanEnchant(card);
        ExtraEnchantmentStore.SwapPrimary(card, null, out EnchantmentModel? primary);
        try
        {
            return enchantment.CanEnchant(card);
        }
        finally
        {
            ExtraEnchantmentStore.SwapPrimary(card, primary, out _);
        }
    }

    private static bool HasSameType(CardModel card, Type type) =>
        card.Enchantment?.GetType() == type || ExtraEnchantmentStore.HasSameType(card, type);
}
