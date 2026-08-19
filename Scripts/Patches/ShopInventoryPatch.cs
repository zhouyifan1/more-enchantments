using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 商店库存补丁：向每个商店的遗物售卖列表追加至多 3 个「商店附魔遗物池」遗物条目。
/// - 稀有度按原版权重（50/33/17，RelicFactory.RollRarity）摇取；池内没有该稀有度时按 Common→Uncommon→Rare 降级；
/// - 同一商店内不重复（chosen 排除已选）；池内可选项不足 3 个时栏位相应减少；池为空时不加任何条目（功能休眠）；
/// - 抽取走 PlayerRng.Shops：存档发生在进房间前，读档重进商店会确定性重放，栏位内容不变；多人各端同理一致；
/// - 每次 ToMutable() 出新实例、不经过 RelicGrabBag 去重，允许单局内跨商店/跨途径重复获得同一遗物。
/// 配套 UI：ShopUiSlotsPatch 会按"条目数 - 场景栏位数"补齐栏位节点，两边数量始终一致。
/// </summary>
public class ShopInventoryPatch : IPatchMethod
{
    // 专属栏位数
    public const int MaxExtraSlots = 3;

    // 摇中的稀有度在池内无货时的降级顺序
    private static readonly RelicRarity[] _rarityFallback = [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare];

    public static string PatchId => "shop_enchant_relic_inventory";

    public static string Description => "向商店库存追加商店附魔遗物池的遗物条目（药水下方 3 个专属栏位的数据侧）";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))];

    public static void Postfix(Player player, MerchantInventory __result)
    {
        try
        {
            AddEnchantRelicEntries(player, __result);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[ShopInventoryPatch] 追加商店附魔遗物条目失败，本次商店不含专属栏位: {ex}");
        }
    }

    private static void AddEnchantRelicEntries(Player player, MerchantInventory inventory)
    {
        Rng rng = player.PlayerRng.Shops;
        List<RelicModel> pool = ModelDb.RelicPool<ShopEnchantRelicPool>()
            .GetUnlockedRelics(player.UnlockState)
            .Where(r => r.IsAllowed(player.RunState))
            .ToList();
        if (pool.Count == 0)
            return;

        HashSet<ModelId> chosen = [];
        for (int i = 0; i < MaxExtraSlots; i++)
        {
            RelicModel? relic = PickRelic(pool, chosen, RelicFactory.RollRarity(rng), rng);
            if (relic == null)
                break;
            chosen.Add(relic.Id);
            // 该构造器不查重、不碰抓袋；定价 = MerchantCost × 0.85~1.15（走 Shops RNG），购买后栏位隐藏不补货。
            // 注：追加的条目未订阅 PurchaseCompleted → UpdateEntries（private），
            // 仅影响"买本池遗物后不触发全店价格刷新"这一原生次要行为；其他购买触发的全局刷新仍覆盖本条目。
            inventory.AddRelicEntry(new MerchantRelicEntry(relic.ToMutable(), player));
        }
    }

    internal static RelicModel? PickRelic(List<RelicModel> pool, HashSet<ModelId> chosen, RelicRarity rolled, Rng rng)
    {
        List<RelicModel> candidates = CandidatesOf(rolled);
        for (int i = 0; candidates.Count == 0 && i < _rarityFallback.Length; i++)
            candidates = CandidatesOf(_rarityFallback[i]);
        return candidates.Count > 0 ? rng.NextItem(candidates) : null;

        List<RelicModel> CandidatesOf(RelicRarity rarity) =>
            pool.Where(r => r.Rarity == rarity && !chosen.Contains(r.Id)).ToList();
    }
}

/// <summary>
/// 商店附魔遗物栏位的补货补丁：持有「送货员」(TheCourier) 等使商店条目补货的效果时，
/// 原生 MerchantRelicEntry.RestockAfterPurchase 会从原版遗物抓袋补货——本补丁拦截
/// “售出的遗物属于商店附魔遗物池”的情况，改为从本池补货（稀有度摇取/同店在架黑名单与原生一致）。
/// 本池全部可选遗物都在架上时无货可补，栏位按售罄隐藏；原版遗物栏位的补货逻辑不受影响。
/// </summary>
public class ShopRelicRestockPatch : IPatchMethod
{
    // Model 属性的私有 setter 背字段（MerchantRelicEntry.Model 无法从外部直接赋值）
    private static readonly FieldInfo? _modelBackingField =
        PrivateAccess.DeclaredField(typeof(MerchantRelicEntry), "<Model>k__BackingField");

    // MerchantEntry._player（protected readonly，声明于基类）
    private static readonly FieldInfo? _playerField =
        PrivateAccess.Field(typeof(MerchantRelicEntry), "_player");

    public static string PatchId => "shop_enchant_relic_restock";

    public static string Description => "商店附魔遗物池栏位售出后的补货改从本池抽取（兼容送货员）";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(MerchantRelicEntry), "RestockAfterPurchase")];

    public static bool Prefix(MerchantRelicEntry __instance, MerchantInventory? inventory)
    {
        try
        {
            RelicModel? sold = __instance.Model;
            // 只接管本池遗物的补货；原版条目返回 true 走原生逻辑
            if (sold == null || !ModelDb.RelicPool<ShopEnchantRelicPool>().AllRelicIds.Contains(sold.Id))
                return true;
            RestockFromPool(__instance, inventory);
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[ShopRelicRestockPatch] 补货失败: {ex}");
        }
        return false;
    }

    private static void RestockFromPool(MerchantRelicEntry entry, MerchantInventory? inventory)
    {
        Player player = (Player)(_playerField?.GetValue(entry)
            ?? throw new InvalidOperationException("无法访问 MerchantEntry._player"));
        Rng rng = player.PlayerRng.Shops;

        // 与原生一致的黑名单：当前在架的全部遗物（本池+原版栏位），保证同一商店同时在售不重复
        HashSet<ModelId> blacklist = inventory?.RelicEntries
            .Select(e => e.Model?.CanonicalInstance?.Id)
            .OfType<ModelId>()
            .ToHashSet() ?? [];

        List<RelicModel> pool = ModelDb.RelicPool<ShopEnchantRelicPool>()
            .GetUnlockedRelics(player.UnlockState)
            .Where(r => r.IsAllowed(player.RunState))
            .ToList();

        RelicModel? next = ShopInventoryPatch.PickRelic(pool, blacklist, RelicFactory.RollRarity(rng), rng);
        if (next == null)
        {
            SetModel(entry, null); // 本池无货可补 → 售罄，栏位隐藏
            return;
        }

        // 复刻原生 SetModel 流程（AssertMutable → 赋值 → CalcCost → 标记已见）
        RelicModel mutable = next.ToMutable();
        mutable.AssertMutable();
        SetModel(entry, mutable);
        entry.CalcCost();
        SaveManager.Instance.MarkRelicAsSeen(mutable);
    }

    private static void SetModel(MerchantRelicEntry entry, RelicModel? model)
    {
        if (_modelBackingField == null)
            throw new InvalidOperationException("无法访问 MerchantRelicEntry.Model 背字段");
        _modelBackingField.SetValue(entry, model);
    }
}
