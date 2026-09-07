# 功能块 E：提高卡牌附魔上限 —— 技术方案

> 版本：v1.0（设计稿，基于反编译源码调查，未实现）
> 关联：`Plan/PROJECT_PLAN.md` §8；实现后按约定把新用到的 Hook 补进 `Plan/Hooks.md`。
> 目标：一卡多附魔；上限默认值代码可改；局内可由遗物等动态提高且**单局内不下降**；读档/多人兼容。

---

## 1. 源码调查结论（单槽架构事实清单）

原版**不存在"上限计数器"**——一卡一附魔是硬结构：

| 单槽硬编码点 | 位置 | 说明 |
| --- | --- | --- |
| 存储 | `CardModel.cs:525` | `public EnchantmentModel? Enchantment { get; private set; }` 单属性 |
| 校验① | `EnchantmentModel.cs:261` | `CanEnchant`：`card.Enchantment != null && (!IsStackable \|\| 类型不同)` → false |
| 校验② | `CardCmd.cs:434-438` | `Enchant`：空槽→附着；同型→`Amount+=`；异型→**抛异常** |
| 克隆 | `CardModel.cs:964-969` | `DeepCloneFields` 只克隆单个（`EnchantInternal`，**不重放 OnEnchant**） |
| 存档 | `SerializableCard` / `CardModel.cs:1819,1835` | 只序列化单个 `SerializableEnchantment{Id, Amount, Props}`；读档先附魔（`EnchantInternal`+`ModifyCard`）再重放升级 |
| 打出 | `CardModel.cs:1570-1578` | `OnPlayWrapper` 每个 replay 轮次内对单附魔调 `OnPlay`（卡效果之后、`Affliction.OnPlay` 与 `Hook.AfterCardPlayed` 之前） |
| 数值 | `Hook.cs:1021-1026`（格挡）、`1166-1179`（伤害） | 静态方法，先取 `cardSource.Enchantment` 做 `+=add` 再 `×=mult`，然后才进全局钩子；另有 6 个 DynamicVar 类（Damage/Block/CalculatedDamage/CalculatedBlock/ExtraDamage/OstyDamage）各自直读 `card.Enchantment` 算预览值 |
| 重放 | `CardModel.cs:900` | `GetEnchantedReplayCount()` 只问单附魔 `EnchantPlayCount` |
| 钩子广播 | `CombatState.cs:324-327`、`RunState.cs:379-381` | 枚举监听者时 `card` 后紧跟 `card.Enchantment`；资格过滤 `Contains` 按 `HasCard` 逐附魔判断 |
| UI | `NCard.cs:615-690` | 场景内单个 `%Enchantment` tab（Icon+Label），`_subscribedEnchantment` 单订阅字段（重复订阅抛异常），Disabled 置灰走 tab 的 ShaderMaterial |
| 杂项 | `PlayerCombatState.RecalculateCardValues` / `CombatManager` 首回合沉底 / `MysticLighter` / `ThievingHopper` / `Goopy` / `Claws`·`ArchaicTooth` 变形迁移 / `NCardEnchantVfx` / `NDeckHistoryEntry` / `NEnchantPreview` | 全部按单实例设计 |

关键事实补充：

- `EnchantmentModel.Card` 反向引用不可迁移（setter 二次赋值抛异常），但 `ApplyInternal(card, amount)` 与 `ModifyCard()` 都是 **public**，可在 `CardCmd.Enchant` 之外手动完成附着。
- 附魔**无需注册**即可收到战斗钩子：广播列表每次 Hook 调用时重建，只要附魔 `HasCard` 且宿主卡在战斗牌堆即入列。
- `SerializableEnchantment` 不存 `Status`/`DynamicVars`（读档恒回 `Normal`，数值靠 `RecalculateValues()` 重建）。
- 卸载容忍度高：未知 JSON 节点静默跳过；未知附魔 ID → `DeprecatedEnchantment` 占位不炸档。

---

## 2. 总体架构：主槽 + Mod 附加槽（推荐）

**不改写 `CardModel.Enchantment` 的单槽语义**，把它当作"0 号主槽"，第 2~N 个附魔存本 Mod 的按卡附加槽列表。理由：

- 原版与其他 Mod 读 `card.Enchantment` 的地方（30+ 处）零改动、行为不变——兼容性最大来源；
- 存档格式不变（主槽走原版序列化，附加槽走 RitsuLib `SavedAttachedState` 注入 `SavedProperties`），卸载 Mod 静默降级；
- 所有"多附魔生效"由本 Mod 的补丁显式分发，顺序语义自己定义，可控可测。

**不变式**：附加槽非空 ⇒ 主槽非空。主槽被 `ClearEnchantment` 清除时，第一个附加槽晋升为主槽（只迁移引用，**不重放 OnEnchant**——它早已修改过卡牌）。

**槽位顺序语义**：槽位按附着时间排序（主槽=0 号）。所有分发按槽序 fold：数值链逐槽 `+=add`/`×=mult`、重放次数逐槽 `EnchantPlayCount(前值)`、`OnPlay` 逐槽依次调用、钩子广播在主槽附魔之后插入附加槽。同型附魔永远堆叠到既有实例（`Amount+=`，遵守该附魔的 `IsStackable`），不占新槽。

---

## 3. 上限服务 API（暴露接口）

`Scripts/Data/EnchantLimitService.cs`（public static，供本 Mod 及联动 Mod 使用）：

```csharp
public static class EnchantLimitService
{
    // 代码中可修改的默认值（影响之后的新局；建议 1~3，对应"原版 1 槽 + N-1 附加槽"）
    public static int BaseLimit { get; set; } = 1;

    // 当前上限：max(峰值, BaseLimit + 局内加成)。峰值持久化，保证单局内不下降（含读档）
    public static int GetLimit(Player player);

    // 局内动态提高（遗物/事件调用，amount 必须 > 0）；写入 RunSavedData，触发 LimitChanged
    public static void AddBonus(Player player, int amount);

    // 查询
    public static int GetEnchantmentCount(CardModel card);          // 主槽+附加槽总数
    public static bool HasFreeSlot(CardModel card);                 // count < GetLimit(card.Owner)
    public static IReadOnlyList<EnchantmentModel> GetExtraEnchantments(CardModel card);

    // 上限变化事件（UI/系统刷新用）
    public static event Action<Player, int>? LimitChanged;
}
```

- **持久化**：`RitsuLibFramework.GetRunSavedDataStore(ModId).RegisterPerPlayer("enchant_limit", ...)` 存 `{ Bonus, Peak }` 两个 int（`WritePolicy.WhenNonDefault`）。`GetLimit` 计算 `candidate = max(BaseLimit, BaseLimit + Bonus)`，回写 `Peak = max(Peak, candidate)` 后返回——任何路径都不会让上限下降。读档由 RitsuLib 自动还原；需要刷新 UI 处订阅 `RunLoadedEvent`。
- **遗物联动示例**：`AfterObtained()` 里 `EnchantLimitService.AddBonus(Owner, 1)`（规划 §5.2 的"栏位上限联动遗物"即此用法）。
- 可选：用 `RegisterModSettings` 提供玩家可调的 BaseLimit 设置页（规划 §8.2 的可选档），默认保守。

---

## 4. 附加槽存储与持久化

- **运行期**：`ConditionalWeakTable<CardModel, List<EnchantmentModel>>`（或 RitsuLib `SavedAttachedState` 的非持久形态）持有附加槽实例。附着 = `enchantment.ApplyInternal(card, amount)` + `enchantment.ModifyCard()`（与原版 `CardCmd.Enchant` 等效），随后触发 `CardModel.EnchantmentChanged`（驱动 UI 刷新）与本 Mod 的槽位变更事件。
- **持久化**：两个 `SavedAttachedState<CardModel, ...>`——`ModelId[]`（附加槽 ID 序列）+ `int[]`（对应 Amount）。RitsuLib 的 `SavedAttachedStatePatches` 会在 `SavedProperties.FromInternal/FillInternal` 自动把它们桥接进原版 `props`（联机走 `ModelIdSerializationCache`，**不要绕开它自改二进制**）。
- **读档重放**：patch `CardModel.FromSerializable` postfix——在原版主槽附魔+升级重放完成后，按数组顺序逐槽 `FromSerializable → ApplyInternal → ModifyCard()`，保持"主槽在前、附加槽按附着序"的重放顺序（与运行期顺序语义一致）。`Status`/`DynamicVars` 与原版一致：不持久化，`RecalculateValues()` 重建。
- **克隆**：patch `CardModel.DeepCloneFields` postfix——逐槽 `ClonePreservingMutability()` + `ApplyInternal`（**不调 `ModifyCard`**，与原版克隆不重放 OnEnchant 的语义一致；见 `Plan/Hooks.md` §0.4）。

---

## 5. 模型层补丁清单（按功能分组）

| # | 目标 | 方式 | 作用 |
| --- | --- | --- | --- |
| M1 | `EnchantmentModel.CanEnchant` | postfix | `__result == false` 且唯一拦截原因是占槽（重算 vanilla 其余条件：卡类型/不可打出/同型堆叠规则）→ 有免费槽则改 true。**版本敏感点**：游戏更新后需核对 `EnchantmentModel.cs:245-266` 原逻辑 |
| M2 | `CardCmd.Enchant` | prefix | 路由：主槽空/同型（主槽或附加槽）→ 放行原生（原生处理堆叠）；异型且 `HasFreeSlot` → 附加槽附着并跳原生；满槽 → 放行原生抛异常（与原版一致） |
| M3 | `CombatState.IterateHookListeners` / `RunState.IterateHookListeners` | postfix | 在主槽附魔之后按槽序插入附加槽实例——**全部通用战斗钩子（AfterCardPlayed/AfterDamageGiven/AfterFlush/ModifyXxx…）自动覆盖多附魔**，资格过滤 `Contains` 按 `HasCard` 天然适用 |
| M4 | `Hook.ModifyDamage` / `Hook.ModifyBlock` | postfix | 原生已处理主槽（先 `+=` 后 `×=`）；postfix 按槽序对每个附加槽重复同序运算，等价于一个有序附魔列表 |
| M5 | 6 个 DynamicVar 类（Damage/Block/CalculatedDamage/CalculatedBlock/ExtraDamage/OstyDamage） | postfix | 卡面预览值按槽序叠加附加槽修正（每个类方法体小、非 async，是好补丁目标） |
| M6 | `CardModel.GetEnchantedReplayCount` | postfix | `__result = fold(附加槽, EnchantPlayCount)`（主槽结果作为初值） |
| M7 | `CardModel.OnPlayWrapper`（async） | transpiler（`MethodType.Async`，锚点 = `Enchantment.OnPlay` 调用处） | 在主槽 `OnPlay` 之后按槽序插入附加槽 `OnPlay`+`InvokeExecutionFinished`。**降级**：transpiler 失败时附加槽改用 `AfterCardPlayed`（`cardPlay.Card == Card` 过滤）等效触发，差异仅时机（在全局广播而非卡效果紧后） |
| M8 | `CardModel.HoverTips` / `GetDescriptionForPile` / `ShouldGlowGold` / `ShouldGlowRed` | postfix | 合并附加槽的 HoverTips、extraCardText（`[purple]` 行）、发光态 |
| M9 | `PlayerCombatState.RecalculateCardValues` | postfix | 附加槽 `RecalculateValues()` |
| M10 | `CardModel.DowngradeInternal` | postfix | 降级重置后重放附加槽 `ModifyCard()`（原版只重放主槽） |
| M11 | `CardCmd.ClearEnchantment` | postfix | 主槽被清时晋升首个附加槽（仅迁移引用，不重放） |
| M12 | `CombatManager` 首回合沉底（`ShouldStartAtBottomOfDrawPile`） | postfix | 附加槽同判 |
| M13 | 历史记录 | M2 内 | 附加槽附着成功时补 `CardEnchantmentHistoryEntry`（与原生 444 行一致） |

---

## 6. UI 兼容方案（卡面附魔图标）

`NCard` 的 `%Enchantment` tab 是自包含容器（Icon+Label+ShaderMaterial），无列表抽象；**运行时 `Duplicate()` tab、水平排列**：

| # | 目标 | 方式 | 作用 |
| --- | --- | --- | --- |
| U1 | `NCard.UpdateEnchantmentVisuals` | postfix | 原生渲染主槽后，按附加槽数量复用/复制 tab，水平偏移（间距 = tab 宽 + 常量），逐槽填 `Icon`/`DisplayAmount`/`ShowAmount`；星标费用上移 45px 的逻辑同步到所有 tab |
| U2 | `NCard` 订阅管理 | 本 Mod 自管 | 附加槽的 `StatusChanged` 由本 Mod 订阅（**不动** `_subscribedEnchantment`，它重复订阅会抛异常）；`EnchantmentChanged` 时重订阅 |
| U3 | 置灰 | U1 内 | 复制的 tab 共享同一 ShaderMaterial 实例——必须逐 tab `Material.Duplicate()` 再分别设 h/s/v，否则改一个灰全部 |
| U4 | `NCard.OnReturnedFromPool` | postfix | 对象池回收时销毁复制的 tab 并退订，防泄漏/串卡 |
| U5 | `NCardEnchantVfx` | 可选 | 附魔烙印特效只播主槽图标；附加槽附着时手动再 `NCardEnchantVfx.Create` 一次或接受只播主槽（降级可接受） |
| U6 | `NDeckHistoryEntry` / `NEnchantPreview` | 可选 | 历史条目单图标显示主槽即可；预览屏 `Init` 直调 `EnchantInternal` 会覆盖主槽显示，多槽预览需单独适配（低优先级） |

**降级策略**：UI 补丁全失败时仅显示主槽图标，逻辑层（M1-M13）完全不受影响——符合"UI 失败仅降级显示"的规划要求。

---

## 7. 生效顺序与兼容性专项

### 7.1 附魔生效顺序（定义即兼容）

- **槽序 = 附着顺序**，主槽恒为 0 号；同型堆叠不改变顺序。
- 数值链/重放/OnPlay/钩子广播全部按槽序 fold（见 §5 M3/M4/M6/M7），顺序在存档数组、克隆、晋升（被晋升者成为 0 号，其余顺移）中保持一致——**读档、战斗克隆、多人各端顺序确定**。
- 与原版模型的相对位置不变：附加槽在广播列表中紧跟主槽附魔（卡→折磨→主槽附魔→附加槽…），与其他 Mod 内容的相对顺序由原广播列表天然决定。
- 设计约束（写进附魔开发规范）：依赖"最先/最后结算"的附魔应在描述中写明，避免顺序敏感效果。

### 7.2 兼容性风险清单

| 风险 | 影响面 | 方案 |
| --- | --- | --- |
| 其他 Mod 读 `card.Enchantment` | 只看到主槽 | 有意为之（兼容特性）；不变式保证主槽永远有值 |
| 原版 `MysticLighter`（无附魔不加伤）/`ThievingHopper`（偷 Imbued 卡）/`Goopy`（`DeckVersion.Enchantment.Amount++`） | 只感知主槽 | 可接受；附加槽不受影响不报错 |
| 变形迁移（`Claws`/`ArchaicTooth` 的 `MutableClone`） | 原生只迁移主槽 | M-克隆补丁（§4）覆盖所有克隆路径，变形后附加槽保留 |
| 卸载 Mod 读档 | 附加槽数据在 `props` 未知名下被静默丢弃；上限加成消失回 BaseLimit | 不坏档（原版容忍机制已验证）；主槽若是本 Mod 附魔→`DeprecatedEnchantment` 空壳 |
| 多人联机 | 附加槽经 `SavedAttachedState` 进网络 ID 表；上限经 `RunSavedData.RegisterPerPlayer` 同步 | 两端 Mod 集合一致即可；**禁止**手改 `IPacketSerializable` |
| `CanEnchant` postfix 与其他 Mod 的同目标补丁 | 返回值互相覆盖 | 本补丁只在 `false→true` 方向放行，不改 `true`；冲突时以"更严格者胜"为预期，实测对齐 |
| 游戏版本更新 | M1（重算 vanilla 条件）与 M7（IL 锚点）最脆弱 | 独立 patcher `enchantment-limit`、`IsCritical=false`；M7 失败降级 §5 所述；启动审计日志 |
| 平衡性 | 上限放大附魔构筑 | BaseLimit 默认保守（2）；强力附魔后续可加"独占"标记（一张牌只能有这一个附魔，在附魔自身 `CanEnchant` 里实现，框架无需改动） |

---

## 8. 实施与验证

- **补丁组织**：全部进独立 patcher `enchantment-limit`（`IsCritical = false`），与 `core`/`shop` 并列；Entry 中失败仅降级。
- **测试清单**：控制台 `enchant` 连附异型（应成功至上限）→ 卡面多图标/角标/置灰 → 打出时各附魔按槽序触发（含重放次数叠加）→ 伤害/格挡/预览数值 fold 正确 → 战斗克隆后保留 → 保存/读档后附加槽与上限恢复 → 遗物 `AddBonus` 后上限提高且读档不降 → 主槽驱散后晋升 → 多人双端一致 → 卸载 Mod 读档不炸。
- **里程碑**：M1-M6+M13（核心逻辑）→ 存档/克隆 → M7（OnPlay transpiler）→ UI（U1-U4）→ U5/U6 与设置页打磨。
