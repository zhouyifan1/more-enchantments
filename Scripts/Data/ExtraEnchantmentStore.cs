using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Utils;

namespace MoreEnchantments.Scripts.Data;

/// <summary>
/// 附加附魔槽存储（功能块 E 内部实现，公开查询走 EnchantLimitService）。
/// - 运行期：ConditionalWeakTable 按卡持有附加槽实例列表（附着顺序 = 槽序）；
/// - 持久化：SavedAttachedState 把附加槽编码（"CAT.ENTRY:Amount;..."）桥接进卡牌 SavedProperties，
///   随 run 存档/联机同步（RitsuLib SavedAttachedStatePatches 处理，勿绕开自改二进制）；
/// - 不变式：附加槽非空 ⇒ 主槽非空（主槽被清除时由 PromoteNextToPrimary 晋升）。
/// </summary>
internal static class ExtraEnchantmentStore
{
    private static readonly ConditionalWeakTable<CardModel, List<EnchantmentModel>> _extras = new();

    // 附加槽的存档编码（SavedProperties 桶类型 string，JSON/联机二进制均兼容）
    private static readonly SavedAttachedState<CardModel, string> _persisted = new("ExtraEnchantments", _ => "");

    // CardModel.Enchantment 的私有 setter 背字段（仅用于 EvaluateIgnoringOccupancy 的临时置空/恢复）
    private static readonly FieldInfo? _enchantmentBackingField =
        PrivateAccess.DeclaredField(typeof(CardModel), "<Enchantment>k__BackingField");

    // CardModel.EnchantmentChanged 事件的背字段（附加槽附着后驱动 UI 刷新，等效原生 EnchantInternal 末尾的行为）
    private static readonly FieldInfo? _enchantmentChangedField =
        PrivateAccess.DeclaredField(typeof(CardModel), "EnchantmentChanged");

    public static IReadOnlyList<EnchantmentModel> Get(CardModel card) =>
        _extras.TryGetValue(card, out List<EnchantmentModel>? list) ? list : [];

    public static bool HasSameType(CardModel card, Type type) =>
        _extras.TryGetValue(card, out List<EnchantmentModel>? list) && list.Any(e => e.GetType() == type);

    public static EnchantmentModel? FindSameType(CardModel card, Type type) =>
        !_extras.TryGetValue(card, out List<EnchantmentModel>? list) ? null : list.FirstOrDefault(e => e.GetType() == type);

    /// <summary>附着到附加槽：ApplyInternal + ModifyCard（等效原生 CardCmd.Enchant 主槽路径）+ 入列 + 同步 + 事件。</summary>
    public static void Attach(CardModel card, EnchantmentModel enchantment, decimal amount)
    {
        enchantment.ApplyInternal(card, amount);
        enchantment.ModifyCard();
        _extras.GetOrCreateValue(card).Add(enchantment);
        Sync(card);
        FireEnchantmentChanged(card);
    }

    /// <summary>同型堆叠到既有附加槽实例（等效原生 Amount += 路径）。</summary>
    public static void StackAmount(CardModel card, EnchantmentModel extra, int amount)
    {
        extra.Amount += amount;
        Sync(card);
        FireEnchantmentChanged(card);
    }

    /// <summary>主槽被清除后晋升首个附加槽（只迁移引用，不重放 ModifyCard——它早已修改过卡牌）。</summary>
    public static void PromoteNextToPrimary(CardModel card)
    {
        if (card.Enchantment != null || !_extras.TryGetValue(card, out List<EnchantmentModel>? list) || list.Count == 0)
            return;
        EnchantmentModel next = list[0];
        list.RemoveAt(0);
        next.ClearInternal(); // 解除 Card 反引，EnchantInternal 才能重新 ApplyInternal
        card.EnchantInternal(next, next.Amount);
        Sync(card);
    }

    /// <summary>预览附着（NEnchantPreview 的克隆卡用）：只 ApplyInternal + 入列，不持久化、不触发事件、不调 ModifyCard（调用方设置 IsEnchantmentPreview 后自行 ModifyCard）。</summary>
    public static void AttachPreview(CardModel card, EnchantmentModel enchantment, decimal amount)
    {
        enchantment.ApplyInternal(card, amount);
        _extras.GetOrCreateValue(card).Add(enchantment);
    }

    /// <summary>克隆迁移（战斗克隆/变形）：复制附加槽到新卡，不重放 ModifyCard（与原生克隆语义一致）。</summary>
    public static void CloneTo(CardModel source, CardModel clone)
    {
        if (!_extras.TryGetValue(source, out List<EnchantmentModel>? list) || list.Count == 0)
            return;
        List<EnchantmentModel> cloned = _extras.GetOrCreateValue(clone);
        foreach (EnchantmentModel extra in list)
        {
            EnchantmentModel copy = (EnchantmentModel)extra.ClonePreservingMutability();
            copy.ApplyInternal(clone, copy.Amount);
            cloned.Add(copy);
        }
        Sync(clone);
    }

    /// <summary>读档恢复：从 SavedAttachedState 解码，按槽序 ApplyInternal + ModifyCard（在主槽与升级重放之后调用）。</summary>
    public static void RestoreFromSave(CardModel card)
    {
        if (!_persisted.TryGetValue(card, out string? encoded) || string.IsNullOrEmpty(encoded))
            return;
        List<EnchantmentModel> list = _extras.GetOrCreateValue(card);
        foreach (string item in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int amount))
            {
                Entry.Logger.Warn($"[ExtraEnchantmentStore] 无法解析的附加槽存档项: {item}");
                continue;
            }
            try
            {
                EnchantmentModel enchantment = SaveUtil.EnchantmentOrDeprecated(ModelId.Deserialize(parts[0])).ToMutable();
                enchantment.ApplyInternal(card, amount);
                enchantment.ModifyCard();
                list.Add(enchantment);
            }
            catch (Exception ex)
            {
                Entry.Logger.Error($"[ExtraEnchantmentStore] 恢复附加槽失败 {item}: {ex}");
            }
        }
        if (list.Count > 0)
            FireEnchantmentChanged(card);
    }

    /// <summary>摘下全部附加槽（事件交换附魔用）：清空列表并同步持久化；实例随调用方快照丢弃，不调 ClearInternal。</summary>
    public static void DetachAll(CardModel card)
    {
        if (_extras.TryGetValue(card, out List<EnchantmentModel>? list) && list.Count > 0)
        {
            list.Clear();
            Sync(card);
        }
    }

    /// <summary>附魔层数被外部直接修改后：同步附加槽持久化并刷新 UI（主槽层数由原版序列化负责）。</summary>
    public static void SyncAndRefresh(CardModel card)
    {
        Sync(card);
        FireEnchantmentChanged(card);
    }

    /// <summary>临时置换主槽引用（配合 try/finally 使用），绕过私有 setter。</summary>
    public static void SwapPrimary(CardModel card, EnchantmentModel? value, out EnchantmentModel? previous)
    {
        if (_enchantmentBackingField == null)
            throw new InvalidOperationException("无法访问 CardModel.Enchantment 背字段");
        previous = (EnchantmentModel?)_enchantmentBackingField.GetValue(card);
        _enchantmentBackingField.SetValue(card, value);
    }

    private static void Sync(CardModel card)
    {
        if (_extras.TryGetValue(card, out List<EnchantmentModel>? list) && list.Count > 0)
            _persisted.Set(card, string.Join(';', list.Select(e => $"{e.Id}:{e.Amount}")));
        else
            _persisted.Remove(card);
    }

    private static void FireEnchantmentChanged(CardModel card) =>
        (_enchantmentChangedField?.GetValue(card) as Action)?.Invoke();
}
