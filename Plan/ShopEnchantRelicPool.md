# 商店附魔遗物池 —— 添加遗物指南

> 面向开发 Agent 的操作手册：如何向「商店附魔遗物池」添加新遗物。
> 机制实现细节见 `Plan/Hooks.md` §6；总体设计见 `Plan/PROJECT_PLAN.md` 功能块 B/C。

## 1. 机制速览（先读懂再动手）

- 池定义：`Scripts/Relics/ShopEnchantRelicPool.cs`（`TypeListRelicPoolModel` + `[RegisterSharedRelicPool]`）。
- 池内遗物**只**通过商店新增的 3 个专属栏位售卖（药水售卖栏位下方一行），不进战斗掉落/宝箱/原生商店栏位——常规抓袋 `RelicGrabBag` 只读 `SharedRelicPool` + 角色池，自定义池天然被排除。
- **允许单局内重复获得**：商店栏位每次从池中 `ToMutable()` 新实例，不经过抓袋去重；同一玩家可多次买到同一遗物。
- **同一商店内不重复**：`ShopInventoryPatch` 抽取时排除本店已选。
- 稀有度摇取：每栏位按原版权重 50% 普通 / 33% 罕见 / 17% 稀有（`RelicFactory.RollRarity`）；摇中的稀有度在池内无货时按 普通→罕见→稀有 降级。
- 定价：`RelicModel.MerchantCost` × 0.85~1.15 浮动（普通 175 / 罕见 225 / 稀有 275）。
- 购买后栏位隐藏、不补货；存档读档后栏位内容不变（确定性 RNG 重放）。
- 持有「送货员」(TheCourier) 时栏位售出后会补货——由 `ShopRelicRestockPatch` 接管，**补货仍从本池抽取**（同店在架不重复；本池无货可补时栏位售罄隐藏），不会混入原版遗物；送货员的 8 折价格修饰对本池遗物照常生效（与原生行为一致）。

## 2. 添加遗物的标准步骤

以「拾起时给卡牌附魔」类遗物为例（本 Mod 遗物的主形态）：

### 2.1 写遗物类

在 `Scripts/Relics/` 新建 `Xxx.cs`：

```csharp
using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「XXX」：拾起时，从牌组选择至多 N 张 <条件> 牌，附魔「YYY」M 层。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_XXX_RELIC
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]          // ← 关键：注册进商店附魔遗物池
public class XxxRelic : EnchantOnPickupRelicBase<YyyEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;  // 仅允许 Common / Uncommon / Rare

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 2;
}
```

要点：

- **`[RegisterRelic(typeof(ShopEnchantRelicPool))]` 是唯一正确的注册目标**。不要同时标 `SharedRelicPool` 或角色池——那会重新进入常规掉落，违背"商店专属"设计。
- **稀有度只能用 `Common / Uncommon / Rare`**：商店摇取与降级只认这三档；`Shop/Event/Ancient` 等会导致永不出现（且 `MerchantCost` 定价也不适用）。
- 非"拾起附魔"形态的遗物直接继承 `ModRelicTemplate` 即可（参考 `Plan/Hooks.md` §4 的注册与配置一节），注册注解不变。
- 附着条件（攻击牌/非能力牌等）不需要写在遗物里——`CardSelectCmd.FromDeckForEnchantment` 会按附魔自身的 `CanEnchant` 自动过滤牌组。

### 2.2 图标资源

- 复制占位图：`DefaultPics/icon85.png` → `MoreEnchantments/images/relics/{类名}.png`，`DefaultPics/icon256.png` → `MoreEnchantments/images/relics/{类名}Big.png`（基类 `EnchantOnPickupRelicBase.AssetProfile` 已按此命名约定取图）。
- **新增图片后必须在 Godot 编辑器重新导出 PCK** 到游戏 mods 目录，否则游戏内无图标。

### 2.3 本地化

`MoreEnchantments/localization/zhs/relics.json` 与 `eng/relics.json` 各加三键（键 = Entry ID + 后缀）：

```json
"MORE_ENCHANTMENTS_RELIC_XXX_RELIC.title": "XXX",
"MORE_ENCHANTMENTS_RELIC_XXX_RELIC.description": "拾起时，从牌组选择至多 {Cards} 张牌，附魔 {EnchantAmount} 层……",
"MORE_ENCHANTMENTS_RELIC_XXX_RELIC.flavor": "趣文"
```

- `{Cards}` / `{EnchantAmount}` 由基类 `CanonicalVars` 自动注入；附魔说明悬停提示由基类自动附加。

### 2.4 设计登记

在 `Plan/Relics/ShopEnchantRelics.md` 的设计表中补一行（名称/效果/稀有度/flavor 等）。

## 3. 可选覆写点

| 成员 | 作用 | 备注 |
| --- | --- | --- |
| `MerchantCost` | 覆写商店定价基准 | 默认按稀有度 175/225/275，一般不动（这是上一个Agent写的，默认都是要覆写的） |
| `IsAllowed(IRunState)` | 生成条件过滤（返回 false 则永不进商店栏位） | 只有 runState 没有 player 上下文；要做角色限定需自行检查 runState 内玩家角色，或扩展 `ShopInventoryPatch` 的过滤 |
| `IsStackable => true` | 重复获得时叠层计数而非多实例 | 默认 false（重复获得 = 遗物栏多个独立实例，与原版 Circlet 一致） |

`IsAllowedInShops` 对本池**无效**（原生商店抽袋才检查它，本池绕开抓袋）。

## 4. 验证清单

1. `dotnet build` 编译通过（自动部署 dll 到游戏 mods 目录；新图标需另导 PCK）。
2. 控制台 `relic add MORE_ENCHANTMENTS_RELIC_<TYPENAME>` 确认遗物本身可用。
3. 进入商店：专属栏位出现在药水售卖下方一行；同店 3 个栏位互不重复。
4. 跨商店/同局可重复买到同一遗物；购买扣金、售出隐藏、悬停提示正常。
5. 商店内保存退出再读档：栏位内容不变。
6. 稀有度覆盖检查：池内三档稀有度都至少有一个遗物时，三档才会都会出现（缺档自动降级，不会报错）。

## 5. 维护约定

- Entry ID（`MORE_ENCHANTMENTS_RELIC_<TYPENAME>`）发布后视为稳定 ID，改名用 `StableEntryStem` 兼容，不要直接改类名。
- 开发中用到了新的游戏 Hook/API，按约定补充进 `Plan/Hooks.md`。
- 池当前为空时商店不出现专属栏位（功能休眠），属正常现象。
