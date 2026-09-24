using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Random;

namespace MoreEnchantments.Scripts.Data;

/// <summary>
/// 「随机附魔」服务：对特定卡牌筛选可施加的附魔（权重&gt;0 且 CanEnchant），按权重随机选取并施加。
/// 设计见 Plan/RandomEnchantment.md（v1.1 已确认：候选为空时无效果；默认表保持全附魔均匀，调用点用 WithOverrides 调权）。
/// 筛选复用 CanEnchant——多槽上限/同型堆叠规则（功能块 E）自动生效；施加走 CardCmd.Enchant，多槽路由自动生效。
/// </summary>
public static class RandomEnchantService
{
    private static IReadOnlyDictionary<ModelId, float>? _defaultWeights;

    /// <summary>
    /// 默认权重哈希表（可引用）：覆盖全部附魔（含 Mod 附魔，懒构建自 ModelDb.DebugEnchantments），
    /// 权重均匀 = 1；DeprecatedEnchantment 与 .Mocks 占位类为 0（永不入选）。只读视图，调权用 WithOverrides。
    /// </summary>
    public static IReadOnlyDictionary<ModelId, float> DefaultWeights => _defaultWeights ??= BuildDefaultWeights();

    /// <summary>泛型便捷取附魔 ModelId（配权重表用）。</summary>
    public static ModelId IdOf<T>() where T : EnchantmentModel => ModelDb.Enchantment<T>().Id;

    /// <summary>默认表副本 + 局部覆盖（只调一两条权重，不重建整表、不污染默认表）；表中不存在的 key 直接并入。</summary>
    public static IReadOnlyDictionary<ModelId, float> WithOverrides(params (ModelId id, float weight)[] overrides) =>
        WithOverrides(null, overrides);

    /// <summary>指定基表副本 + 局部覆盖；baseTable 为 null 时基于 DefaultWeights。</summary>
    public static IReadOnlyDictionary<ModelId, float> WithOverrides(
        IReadOnlyDictionary<ModelId, float>? baseTable,
        params (ModelId id, float weight)[] overrides)
    {
        Dictionary<ModelId, float> table = new(baseTable ?? DefaultWeights);
        foreach ((ModelId id, float weight) in overrides)
            table[id] = weight;
        return table;
    }

    /// <summary>筛选候选：权重&gt;0 且 CanEnchant(card) 的 canonical 附魔集合（供 UI 预览/调试）。</summary>
    public static IReadOnlyList<EnchantmentModel> GetEligible(CardModel card, IReadOnlyDictionary<ModelId, float>? weights = null)
    {
        IReadOnlyDictionary<ModelId, float> table = weights ?? DefaultWeights;
        return ModelDb.DebugEnchantments
            .Where(e => Weight(table, e) > 0f && e.CanEnchant(card))
            .ToList();
    }

    /// <summary>按权重随机一个附魔（canonical 实例）；无候选返回 null（确认语义：候选为空时无效果）。</summary>
    public static EnchantmentModel? Roll(CardModel card, Rng rng, IReadOnlyDictionary<ModelId, float>? weights = null)
    {
        IReadOnlyDictionary<ModelId, float> table = weights ?? DefaultWeights;
        IReadOnlyList<EnchantmentModel> eligible = GetEligible(card, table);
        if (eligible.Count == 0)
            return null;
        float total = eligible.Sum(e => Weight(table, e));
        float roll = rng.NextFloat(total);
        float accumulated = 0f;
        foreach (EnchantmentModel enchantment in eligible)
        {
            accumulated += Weight(table, enchantment);
            if (roll < accumulated)
                return enchantment;
        }
        return eligible[^1]; // 浮点边界兜底
    }

    /// <summary>抽取并施加（CardCmd.Enchant → 多槽路由/堆叠语义自动生效）；返回施加的附魔，未施加为 null。</summary>
    public static EnchantmentModel? RollAndEnchant(CardModel card, decimal amount, Rng rng, IReadOnlyDictionary<ModelId, float>? weights = null)
    {
        EnchantmentModel? rolled = Roll(card, rng, weights);
        return rolled == null ? null : CardCmd.Enchant(rolled.ToMutable(), card, amount);
    }

    // 表中缺失的 key 按权重 1 处理（与"全附魔均匀、未调整一致"语义一致）；负值按 0（永不入选）
    private static float Weight(IReadOnlyDictionary<ModelId, float> table, EnchantmentModel enchantment) =>
        Math.Max(0f, table.GetValueOrDefault(enchantment.Id, 1f));

    private static IReadOnlyDictionary<ModelId, float> BuildDefaultWeights()
    {
        Dictionary<ModelId, float> table = [];
        foreach (EnchantmentModel enchantment in ModelDb.DebugEnchantments)
        {
            Type type = enchantment.GetType();
            bool excluded = type == typeof(DeprecatedEnchantment) || (type.Namespace?.Contains(".Mocks") ?? false);
            table[enchantment.Id] = excluded ? 0f : 1f;
        }
        return table;
    }
}
