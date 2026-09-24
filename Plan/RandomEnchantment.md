# 「随机附魔」实现方案（v1.1 已实现）

> 需求：对特定卡牌，筛选可施加的附魔，按权重随机选取并施加；权重用哈希表承载且覆盖全部附魔；
> 命名空间内提供可引用的默认权重表（全附魔均匀权重），调用点只调一两条权重即可用，不重建整表。
>
> **已确认取舍**：① 候选为空时无效果（返回 null，无兜底附魔）；② 默认表保持全附魔均匀，权重调整全部由调用点 `WithOverrides` 完成。
> 实现：`Scripts/Data/RandomEnchantService.cs`。

## 1. 关键事实（已核实）

- 全量附魔枚举：`ModelDb.DebugEnchantments`（`ModelDb.cs:92`，按 `AbstractModel` 子类扫描，**含 Mod 附魔**——RitsuLib 注册即入扫描）。名字带 Debug 但目前是唯一全量入口，含 `DeprecatedEnchantment` 与 `*.Mocks` 命名空间的占位类，需默认权重 0 排除。
- 原版无现成"按权重随机附魔"机制（SelfHelpBook 等事件是写死候选集）。
- 抽取所需 RNG：`Rng.NextFloat(max)`（`Rng.cs:114`），由调用点传入，存档/联机确定性由 RNG 流保证。
- 可附魔筛选直接复用 `enchantment.CanEnchant(card)`——经功能块 E 的 M1 postfix，自动遵守多槽上限与同型堆叠规则（满槽异型不入选、同型可堆叠可入选）。
- 施加走 `CardCmd.Enchant(rolled.ToMutable(), card, amount)`，多槽路由/堆叠语义自动生效。

## 2. 落点与公开 API

文件 `Scripts/Data/RandomEnchantService.cs`，`public static class RandomEnchantService`（命名空间 `MoreEnchantments.Scripts.Data`，与 `EnchantLimitService` 并列）。

### 2.1 默认权重哈希表（可引用）

```csharp
// key = 附魔 ModelId（稳定 ID，与存档/本地化同源）；value = 权重（>0 入选，0 永不入选，负值按 0 处理）
public static IReadOnlyDictionary<ModelId, float> DefaultWeights { get; }
```

- **懒构建**：首次访问时由 `ModelDb.DebugEnchantments` 生成，全部附魔权重 = 1（均匀），排除项默认 0：
  `DeprecatedEnchantment`、类型命名空间含 `.Mocks` 的占位类。懒加载避开 ModelDb 初始化时序问题。
- 只读视图（`IReadOnlyDictionary`），调用点不得原地改表。

### 2.2 覆盖表（只调一两条，不重建整表）

```csharp
// 基于 DefaultWeights 的副本 + 局部覆盖；默认表不被污染
public static IReadOnlyDictionary<ModelId, float> WithOverrides(
    params (ModelId id, float weight)[] overrides);
public static IReadOnlyDictionary<ModelId, float> WithOverrides(
    IReadOnlyDictionary<ModelId, float>? baseTable,
    params (ModelId id, float weight)[] overrides);

// 泛型便捷：RandomEnchantService.IdOf<Swift>() → ModelId（等价 ModelDb.Enchantment<T>().Id）
public static ModelId IdOf<T>() where T : EnchantmentModel;
```

用法示例（事件/遗物中只调一条）：

```csharp
var weights = RandomEnchantService.WithOverrides(
    (RandomEnchantService.IdOf<Vigorous>(), 3f));   // 活力权重 ×3，其余照旧
var rolled = RandomEnchantService.RollAndEnchant(card, 1m, player.RunState.Rng.Niche, weights);
```

覆盖表中默认表不存在的 key 直接并入（便于给其他 Mod 的新附魔配权重）。

### 2.3 筛选 / 抽取 / 施加

```csharp
// 筛选：权重>0 且 CanEnchant(card) 的 canonical 附魔集合（公开，供 UI 预览候选）
public static IReadOnlyList<EnchantmentModel> GetEligible(
    CardModel card, IReadOnlyDictionary<ModelId, float>? weights = null);

// 抽取：按权重随机一个；无候选返回 null（调用点决定降级：无效果或换固定附魔）
public static EnchantmentModel? Roll(
    CardModel card, Rng rng, IReadOnlyDictionary<ModelId, float>? weights = null);

// 一步到位：抽取并施加（CardCmd.Enchant）；返回施加的附魔，未施加为 null
public static EnchantmentModel? RollAndEnchant(
    CardModel card, decimal amount, Rng rng, IReadOnlyDictionary<ModelId, float>? weights = null);
```

抽取算法：候选按表序遍历累加权重 W，`roll = rng.NextFloat(W)`，再遍历取首个累计值 > roll 者。均匀表即均匀抽取。

## 3. RNG 选择指引（调用点责任）

| 场景 | 建议 RNG | 理由 |
| --- | --- | --- |
| 事件选项 | `player.RunState.Rng.Niche` | 事件杂项流，不污染奖励序列 |
| 遗物/奖励联动 | `player.PlayerRng.Rewards` | 与奖励稀有度同流，读档重放一致 |
| 商店联动 | `player.PlayerRng.Shops` | 与商店生成同流（本 Mod 商店栏位惯例） |

多人：各端用同一 Rng 流抽取即确定一致；若需"主机决定、队友看结果"，由调用点抽取后经原版同步通道广播，服务本身不做同步。

## 4. 与现有系统的关系

- **功能块 E（多槽/堆叠）**：筛选经 `CanEnchant` 自动生效——满槽异型不入选、同型可堆叠入选后叠层、上限提高后候选自然变多。
- **功能块 D（事件）**：附魔祭坛/附魔商人等"随机附魔"选项的直接后端。
- **功能块 B（遗物）**：遗物中“随机附魔”选项的直接后端

## 5. 实施时的验证清单

- 构建日志打印默认表内容（总数 = 全附魔数，排除项为 0）；
- 控制台给测试卡反复 Roll：均匀性粗查、权重覆盖生效、满槽卡不产出异型、同型卡产出后叠层；
- 空候选（满槽且全部异型不可附）返回 null 不抛异常；
- 读档前后同一调用点结果一致（RNG 流确定性）。
