# More Enchantments —— 《杀戮尖塔 2》附魔扩展 Mod 项目规划

> 版本：v0.2（规划稿，新增事件与附魔栏位上限功能）
> 目标游戏版本：`public-beta` 分支（游戏处于抢先体验，API 变动频繁）
> 基础框架：[RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)
> 教程参考：[SlayTheSpire2 Modding Tutorials](https://tutorials.sts2modding.com/)

---

## 1. 项目概述

### 1.1 项目目标

为《杀戮尖塔 2》开发一个名为 **More Enchantments** 的内容型 Mod，包含五大功能块：

| 功能块 | 内容 | 技术难度 |
| --- | --- | --- |
| A. 更多附魔 | 新增一批卡牌附魔（Enchantment），含数值、触发逻辑、图标与文案 | ★★☆ |
| B. 附魔相关遗物 | 新增一批与附魔机制联动的遗物（Relic） | ★★☆ |
| C. 商店扩展 | 在商店中增加 3 个独立遗物栏位，专门售卖附魔相关遗物 | ★★★★ |
| D. 新增事件 | 新增一批附魔主题事件（Event），作为附魔的获取/移除/博弈途径 | ★★☆ |
| E. 附魔栏位上限 | 修改每张卡牌可附着附魔数量的上限，并适配卡牌 UI 显示 | ★★★☆ |

### 1.2 设计原则

- **基于 RitsuLib 构建**：内容注册、生命周期、补丁、本地化、持久化全部走 RitsuLib 约定，不重复造轮子。
- **Entry ID 稳定**：所有内容发布后的公开 Entry（`<MODID>_<CATEGORY>_<TYPENAME>`）视为永久稳定 ID，改名时用 `StableEntryStem` 兼容。
- **功能隔离、可降级**：商店扩展与附魔栏位上限属于高风险补丁，独立 patcher、非关键（`IsCritical = false`），失败时自动降级而不影响附魔与遗物主体功能。
- **中英文双语本地化**：本地化文件按 `{modId}/localization/{Language}/*.json` 组织，至少提供 `zhs` 与 `eng`。

---

## 2. 开发环境与技术栈

### 2.1 环境准备（对应教程 Basics 章节）

| 项目 | 说明 | 教程章节 |
| --- | --- | --- |
| 游戏本体 | 《杀戮尖塔 2》`public-beta` 分支 | Basics 01 - 环境配置 |
| 反编译工具 | dnSpy / ILSpy，用于阅读游戏源码（**商店改造与附魔栏位上限的前提**） | Basics 02 - 安装、看源码、修改 |
| 基础库选择 | RitsuLib（本项目选定），了解与 BaseLib 的差异 | Basics 03 - 选择基础库、Migrations 02 |
| 调试手段 | 热重载、启动参数、控制台指令 | Basics 07 / 08 / 09 |
| Patch 基础 | Harmony 补丁机制、触发顺序 | Basics 10 / 12 |

### 2.2 项目依赖

`.csproj` 中引用 NuGet 包：

```xml
<PackageReference Include="STS2.RitsuLib" />
```

`mod_manifest.json` 声明运行时依赖（游戏 API 0.105.x 及以上用对象写法）：

```json
{
  "dependencies": [
    { "id": "STS2-RitsuLib" }
  ]
}
```

- 若需兼容旧游戏分支，改用 `STS2.RitsuLib.Compat.<api-version>` 编译包；玩家侧可用 variant-pack 安装。
- 可选：安装第三方 Roslyn 分析器 `Miooowo.STS2RitsuLib.ModAnalyzers`，检查本地化键与资源路径。
- 玩家侧需从 GitHub Releases 安装 `STS2-RitsuLib` 运行时 Mod（本 Mod 是 DLL-only 之外的内容 Mod，需要在发布说明中写清依赖关系）。

### 2.3 推荐目录结构

```text
more-enchantments/
├── Scripts/
│   ├── Entry.cs                     # ModInitializer：程序集注册、patcher、生命周期订阅
│   ├── Enchantments/                # 附魔模型（每个附魔一个文件）
│   │   ├── EnchantmentBase.cs       # 可选：本 Mod 的附魔抽象基类
│   │   ├── GreedEnchantment.cs
│   │   └── ...
│   ├── Relics/                      # 遗物模型
│   │   ├── EnchantmentStone.cs
│   │   └── ...
│   ├── Events/                      # 事件模型
│   │   ├── EnchantingAltarEvent.cs
│   │   └── ...
│   ├── Patches/
│   │   ├── EnchantmentLimitPatch.cs # 附魔栏位上限补丁
│   │   ├── EnchantmentUiPatch.cs    # 卡牌多附魔图标 UI 补丁
│   │   ├── ShopInventoryPatch.cs    # 商店库存生成补丁
│   │   └── ShopUiPatch.cs           # 商店 UI 栏位补丁
│   └── Data/
│       └── EnchantmentShopData.cs   # 运行内数据（如需）
└── MoreEnchantments/                # 资源目录（随 pck 打包）
    ├── images/
    │   ├── enchantments/            # 附魔图标，1:1，原版 64x64
    │   ├── relics/                  # 遗物图标，小/轮廓 85x85，大 256x256
    │   └── events/                  # 事件立绘/背景图
    └── localization/
        ├── zhs/
        │   ├── enchantments.json
        │   ├── relics.json
        │   └── events.json
        └── eng/
            ├── enchantments.json
            ├── relics.json
            └── events.json
```

---

## 3. Mod 入口与初始化（对应 RitsuLib 文档 Getting Started）

`Entry.cs` 的标准初始化骨架：

```csharp
[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "MoreEnchantments";
    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Logger = RitsuLibFramework.CreateLogger(ModId);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        // 仅当有挂在 .tscn 上的 C# 脚本（商店 UI 扩展可能需要）时才调用：
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 关键补丁（附魔/遗物/事件主体逻辑）——失败则禁用 Mod
        var corePatcher = RitsuLibFramework.CreatePatcher(ModId, "core");
        corePatcher.RegisterPatches<CorePatches>();
        RitsuLibFramework.ApplyRequiredPatcher(corePatcher, DisableMod);

        // 附魔栏位上限补丁——独立 patcher，失败则恢复原版上限
        var limitPatcher = RitsuLibFramework.CreatePatcher(ModId, "enchantment-limit");
        limitPatcher.RegisterPatches<EnchantmentLimitPatch>();
        limitPatcher.RegisterPatches<EnchantmentUiPatch>();
        // 非关键补丁，失败仅降级

        // 商店扩展补丁——独立 patcher，失败只关闭商店功能
        var shopPatcher = RitsuLibFramework.CreatePatcher(ModId, "shop");
        shopPatcher.RegisterPatches<ShopInventoryPatch>();
        shopPatcher.RegisterPatches<ShopUiPatch>();
        // 非关键补丁，失败仅降级
    }

    private static void DisableMod() { /* 标记 Mod 禁用 */ }
}
```

要点：

- 内容注册优先用 **CLR 注解**（`[RegisterEnchantment]`、`[RegisterRelic]`、`[RegisterActEvent]` 等），注册点贴近模型类；批量内容或条件注册才用 `RitsuLibFramework.CreateContentPack(...)`。同一内容只用一种注册来源。
- CLR 类型命名避免全大写（如 `TESTCARD` 会被原版 Entry 解析错误拆分为 `T_ES_TC_AR_D`），使用 `PascalCase`。

---

## 4. 功能块 A：附魔系统（对应教程 RitsuLib 01-13 添加新附魔）

### 4.1 附魔基类

所有附魔继承 `ModEnchantmentTemplate`，并加 `[RegisterEnchantment]`。可抽一个本 Mod 的抽象基类统一公共资源路径与通用行为（注意：抽象基类上的注册注解需 `Inherit = true` 才会传给派生类）。

### 4.2 单个附魔的标准实现要素

| 覆写成员 | 作用 |
| --- | --- |
| `ShowAmount` / `DisplayAmount` | 是否在卡牌上显示数值及显示值 |
| `HasExtraCardText` | 是否向卡牌描述追加文本 |
| `CanonicalVars` | 动态变量（如 `CardsVar(2)`），供本地化 `{Cards}` 占位 |
| `ExtraHoverTips` | 额外悬停提示（如关键词解释） |
| `AssetProfile` | 图标路径（1:1，原版 64x64） |
| `CanEnchant(CardModel)` | 附魔可附着卡牌的限制条件 |
| `OnEnchant()` | 附魔被应用时的即时效果（如添加关键词） |
| `EnchantBlockAdditive(...)` 等数值钩子 | 修改卡牌数值 |
| `OnPlay(...)` | 卡牌被打出时的触发逻辑，配合 `EnchantmentStatus.Normal/Disabled` 控制次数 |

### 4.3 附魔设计清单（待填充，建议 8~12 个首发）

| ID（PascalCase） | 名称 | 附着条件（CanEnchant） | 效果摘要 | 稀有度/出现权重 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 示例 GreedEnchantment | 贪婪 | 攻击牌 | 打出时若击杀敌人，获得 {Gold} 金币 | 普通 | 待设计 |
| … | … | … | … | … | … |

设计要求：

- 每个附魔至少覆盖一种不同触发点（打出时 / 数值加成 / 回合开始 / 抽牌 / 消耗等），避免同质化。
- 利用 `Amount` 参数支持多档强度，控制台可用 `enchant <ENTRY> [数量] [手牌编号]` 直接测试。
- 本地化键格式：`<MODID>_ENCHANTMENT_<TYPENAME>.title / .extraCardText / .description`。
- 若功能块 E（附魔栏位上限）启用，需明确同一卡牌上多个附魔的叠加规则：同种附魔是否可重复附着、互斥组合（如攻击向与防御向）、触发顺序（参考教程 Basics 12 触发顺序）。

### 4.4 附魔获取途径

- 代码中可用 `CardCmd.Enchant<TEnchantment>(card, amount)` 给予附魔（供遗物、事件调用）。
- **事件**（功能块 D）是附魔的重要叙事化获取/移除途径。
- **商店专属遗物**（功能块 B/C）间接提供附魔获取与强化。

---

## 5. 功能块 B：附魔相关遗物（对应教程 RitsuLib 01-03 添加新遗物）

### 5.1 技术要点

- 继承 `ModRelicTemplate`，注解 `[RegisterRelic(typeof(SharedRelicPool))]` 或自定义遗物池。
- 配置 `Rarity`、`CanonicalVars`（数值）、`AssetProfile`（小图标 85x85 / 轮廓 85x85 / 大图标 256x256）。
- 触发逻辑优先使用模板钩子（如 `AfterPlayerTurnStart`）；复杂时机用 RitsuLib 生命周期事件（`CardPlayedEvent`、`RelicObtainedEvent`、`CombatStartingEvent` 等）或独立补丁。
- 本地化：`relics.json` 中 `.title / .description / .flavor`。

### 5.2 遗物设计清单（待填充，建议 6~10 个首发，与商店栏位联动）

| ID | 名称 | 稀有度 | 效果摘要 | 是否进入商店专属栏位 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 示例 EnchantmentStone | 附魔石 | Shop | 获得时，随机为一张攻击牌附加随机附魔 | 是 | 待设计 |
| 示例 WhetstoneCharm | 磨刀护符 | Uncommon | 每有一张被附魔的牌，其数值效果 +1 | 是 | 待设计 |
| … | … | … | … | … | … |

设计要求：

- 标记为「商店专属栏位售卖」的遗物不进入常规遗物掉落池（通过自定义遗物池 + 商店生成逻辑控制，避免常规房间奖励中出现）。
- 遗物与附魔形成构筑闭环：给予附魔 / 强化附魔 / 以附魔数量为条件触发。
- 至少设计 1 个与「附魔栏位上限」联动的遗物（例如：某张指定卡牌可额外容纳 1 个附魔），与功能块 E 呼应。

---

## 6. 功能块 C：商店三个附魔遗物栏位（高风险部分）

> ⚠️ 教程目前没有现成的商店改造章节，此部分需要反编译游戏源码自行设计，是整个项目技术风险最高的部分之一，建议放在附魔与遗物完成后进行。

### 6.1 预研（动手前必须先做）

1. 按教程 Basics 02 反编译游戏程序集，定位以下内容（类名以实际源码为准）：
   - **商店库存生成逻辑**：商店房间/商人库存的 Model 类，确定遗物售卖列表在哪里生成、价格如何计算。
   - **商店 UI 场景**：商店界面的 Godot 场景（`.tscn`）与对应 `N…` 节点脚本，确定栏位（slot）节点如何布局、是否可以动态实例化追加。
   - **购买流程**：点击遗物 → 扣金币 → 发放遗物的调用链。
2. 产出一份《商店逆向笔记》，记录：目标类名、方法签名（含重载参数类型）、UI 节点树结构、可挂接的锚点。

### 6.2 推荐实施方案（双层补丁）

**第 1 层：库存生成补丁（ShopInventoryPatch）**

- 用 Harmony postfix 挂在商店库存生成方法之后，向库存数据追加 3 个从「附魔遗物池」抽取的遗物条目。
- 抽取规则：固定 3 个栏位，价格可按原版 Shop 稀有度遗物定价上浮/独立定价；同一商店不重复，已售出/已拥有处理与原版一致。
- 存档兼容：确认商店库存是否随 run 存档序列化；若追加条目不被原版序列化覆盖，需用 `RitsuLibFramework.GetRunSavedDataStore(ModId)` 存储本 Mod 的栏位状态，在 `RunLoadedEvent` 时恢复。

**第 2 层：UI 栏位补丁（ShopUiPatch）**

- 在商店 UI 场景加载后动态实例化 3 个遗物栏位节点（复制原版遗物栏位的场景/节点结构），挂接到布局容器中，绑定对应的库存条目与购买回调。
- 若商店场景节点带 C# 脚本，入口需调用 `EnsureGodotScriptsRegistered(...)`。
- 补丁编写规范（RitsuLib patching-guide）：
  - 每个补丁有稳定 `PatchId`；目标方法有重载时填 `parameterTypes`。
  - 商店 UI 属可选功能：`IsCritical = false`、独立 patcher，失败仅降级为「无专属栏位」（附魔遗物改为通过其他途径发放）。
  - 访问私有字段/方法用 `PrivateAccess` 助手；脆弱的 IL 改写用 `HarmonyIlRewriter` / `HarmonyIlPattern` 并用 report 验证。
  - 优先锚点搜索（`TryFindAfter` / `TryFindBefore`），兼容其他 Mod 已修改过的方法。

### 6.3 降级预案

若 UI 补丁在游戏更新后失效且短期无法修复：

- 方案 1：附魔遗物改由自定义事件（功能块 D 的「附魔商人」事件）发放。
- 方案 2：利用 RitsuLib 的顶栏按钮 / 自定义界面能力做一个独立的「附魔商店」入口。
- 两方案都不影响功能块 A、B、D 的主体价值。

---

## 7. 功能块 D：新增事件（对应教程 RitsuLib 01-12 添加新事件）

### 7.1 技术要点

- 继承 `ModEventTemplate`，按生成范围选择注解：
  - `[RegisterActEvent(typeof(SomeAct))]`：只在指定章节生成；
  - `[RegisterSharedEvent]`：通用事件，通过覆写 `IsAllowed(IRunState)` 自定义生成条件。
- 多阶段事件的标准结构：
  - `GenerateInitialOptions()` 生成初始选项；
  - 选项方法内用 `SetEventState(...)` 推进到下一阶段页面、`SetEventFinished(...)` 结束事件；
  - 选项本地化键由方法名自动生成：`{Entry}.pages.{页面}.options.{Slugify(方法名)}`；
  - `BeforeEventStarted` / `OnEventFinished` 处理进入/离开时的状态（如禁止移除药水）。
- 数值用 `CanonicalVars`（如 `DamageVar(10m, ValueProp.Unblockable | ValueProp.Unpowered)`、`GoldVar(60)`），本地化中以 `{Damage}`、`{Gold}` 占位。
- 立绘/背景图走 `AssetProfile`（`InitialPortraitPath`）。
- 奖励发放用 `RewardsCmd.OfferCustom(...)`；给予附魔用 `CardCmd.Enchant<TEnchantment>(card, amount)`。
- 本地化集中在 `events.json`，键结构：`<MODID>_EVENT_<TYPENAME>.title / .pages.<PAGE>.description / .pages.<PAGE>.options.<OPTION>.title|description`。

### 7.2 事件设计清单（待填充，建议 3~5 个首发）

| ID | 名称 | 生成范围 | 阶段结构 | 效果摘要 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 示例 EnchantingAltarEvent | 附魔祭坛 | 通用（IsAllowed：牌组中至少一张可附魔卡牌） | 2 阶段 | 支付金币或生命，为一张选定卡牌附加随机/指定附魔 | 待设计 |
| 示例 CursedEnchanterEvent | 被诅咒的附魔师 | Act 限定 | 多阶段博弈 | 高风险选项：获得强力附魔但附加诅咒/负面效果 | 待设计 |
| 示例 DispelSpringEvent | 驱散之泉 | 通用 | 1~2 阶段 | 移除一张卡牌上的附魔并获得补偿 | 待设计 |
| 示例 EnchantMerchantEvent | 流浪附魔商人 | 通用 | 商店式多选项 | 商店专属栏位失效时的降级发放途径（见 6.3） | 待设计 |
| … | … | … | … | … | … |

设计要求：

- 事件是附魔获取途径的叙事化补充，与功能块 A 的附魔清单联动（事件文案中可引用具体附魔名称）。
- 每个事件的选项应形成明确的「代价 ↔ 收益」权衡，避免纯正面白给。
- 事件文案量较大，中文定稿后再翻译英文，避免反复改两套文本。

---

## 8. 功能块 E：修改卡牌附魔栏位上限（中高风险部分）

> ⚠️ 原版每张卡牌可附着的附魔数量存在上限，但具体上限值与实现位置（模型校验、UI 显示、存档结构）需要反编译确认，此部分与商店扩展同属「逆向依赖」工作。

### 8.1 预研

1. 反编译定位以下实现点（类名以实际源码为准）：
   - **上限校验点**：卡牌模型（CardModel）上附魔集合的添加入口 / `CanEnchant` 链路上的数量校验；附魔上限是硬编码常量还是配置值。
   - **UI 显示点**：卡牌上附魔图标的渲染节点，多附魔时图标的排布方式（并列、堆叠、还是只显示一个）。
   - **存档结构**：卡牌附魔的序列化格式，确认多附魔状态能否被原版存档结构无损保存（通常可以，因为原版就支持附魔集合，但需验证）。
2. 产出《附魔上限逆向笔记》，与《商店逆向笔记》合并为项目的逆向文档。

### 8.2 推荐实施方案

**第 1 层：上限修改补丁（EnchantmentLimitPatch）**

- 用 Harmony 补丁修改上限校验：若是常量比较，用 prefix/postfix 替换比较结果或用 transpiler 改常量；若是虚属性/getter，直接 postfix 覆写返回值。
- 上限值做成**可配置项**：用 `RitsuLibFramework.RegisterModSettings(ModId, configure)` 提供玩家可调的设置页（例如 1~5 档），默认值为适度提升（如 2 或 3），避免破坏平衡。
- 同种附魔重复附着规则、互斥规则在功能块 A 的附魔设计中明确（见 4.3）。

**第 2 层：多附魔 UI 适配（EnchantmentUiPatch）**

- 若原版 UI 只渲染单个附魔图标，需补丁卡牌渲染节点，支持多图标排布（横向并列或缩小堆叠）。
- 参考教程 RitsuLib 02 玩法基底中的 UI 类章节（手牌泛光、额外角标等）了解卡牌节点补丁的惯用手法。
- 此补丁与上限补丁同 patcher（`enchantment-limit`），`IsCritical = false`；UI 补丁失败时降级为「逻辑生效但图标只显示部分」，并在设置页提供关闭上限修改的开关。

### 8.3 设计注意事项

- 上限提升会显著放大附魔构筑强度，需要与附魔数值（功能块 A）整体平衡：建议上限提升的同时给部分强力附魔加「独占」标记（一张牌只能有这一个附魔）。
- 与遗物联动：至少 1 个遗物效果引用栏位上限（见 5.2）。
- 与事件联动：事件可以临时/永久突破单卡上限，作为稀有奖励（见 7.2）。

---

## 9. 里程碑计划

| 阶段 | 目标 | 主要产出 | 预估 |
| --- | --- | --- | --- |
| M0 环境搭建 | 空白 Mod 能进游戏、热重载可用、控制台可用 | Hello-world Mod、调试工作流 | 0.5~1 天 |
| M1 首个附魔 | 按教程完成 1 个完整附魔（含图标、本地化、控制台测试） | 附魔基类 + 1 个示例附魔 | 1 天 |
| M2 附魔批量开发 | 完成全部首发附魔（8~12 个）与平衡初调 | 附魔清单全部落地 | 3~5 天 |
| M3 遗物开发 | 完成全部首发遗物（6~10 个），与附魔联动 | 遗物清单全部落地 | 2~4 天 |
| M4 事件开发 | 完成全部首发事件（3~5 个），接入附魔获取/移除 | 事件清单全部落地 | 2~3 天 |
| M5 附魔栏位上限 | 逆向确认上限实现点；上限补丁 + UI 适配 + 设置项 | 《附魔上限逆向笔记》、可配置上限 | 2~3 天 |
| M6 商店预研 | 完成商店逆向笔记，确定补丁锚点 | 《商店逆向笔记》 | 1~2 天 |
| M7 商店实现 | 库存补丁 + UI 栏位补丁 + 存档兼容 | 3 个专属栏位可用 | 2~4 天 |
| M8 打磨 | 美术资源（教程 Visuals 章节）、音效、英文本地化、Mod 设置页 | 完整资源包 | 2~3 天 |
| M9 发布 | Steam 创意工坊上传（教程 Basics 11）、发布说明与依赖声明 | v1.0 发布 | 0.5 天 |

依赖关系说明：

- M4（事件）依赖 M2 的附魔清单基本定型（事件文案引用附魔）。
- M5（附魔上限）建议在 M2 之后进行，因为叠加规则影响附魔设计。
- M6/M7（商店）依赖 M3 的遗物清单定型。
- 每个里程碑完成后在真实 run 中至少完整游玩一层验证，而非仅用控制台点测。

---

## 10. 测试与调试方案

- **控制台点测**（教程 Basics 09）：`enchant <ENTRY> [数量] [手牌编号]` 快速验证附魔；遗物用对应控制台指令给予。
- **热重载**（教程 Basics 07）：逻辑迭代以热重载为主，减少重启次数。
- **触发顺序**（教程 Basics 12）：附魔/遗物与原版及其他 Mod 的触发顺序需实测确认；多附魔共存时的触发顺序要单独列出测试用例。
- **附魔上限专项测试**：同卡多附魔的叠加/互斥、战斗中状态流转、保存/读档后多附魔状态恢复、UI 多图标显示。
- **事件专项测试**：每个事件的全选项路径遍历、IsAllowed 条件边界、事件给予附魔后的状态一致性。
- **兼容矩阵**：至少验证「仅本 Mod」「本 Mod + RitsuLib 运行时」「本 Mod + 常见大型 Mod」三种组合。
- **存档回归**：商店栏位购买后保存/读档，验证栏位状态恢复正确。

---

## 11. 版本兼容与发布策略

- 主开发对齐游戏 `public-beta` 最高 API；若要支持稳定分支玩家，发布时附带 `STS2.RitsuLib.Compat.<api-version>` 对应构建。
- 利用 RitsuLib 诊断与兼容能力（capability gates、启动审计）在游戏 API 不匹配时给出明确日志而不是静默崩溃。
- 发布页写清：依赖 `STS2-RitsuLib` 运行时 Mod（附 GitHub Releases 链接）、支持的游戏分支、已知冲突。
- 语义化版本：附魔/遗物/事件新增为 minor，纯修复为 patch；任何 Entry ID 变更必须用 `StableEntryStem` 保持兼容。

---

## 12. 风险登记

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 游戏 EA 期间 API 频繁变动 | 补丁失效、编译错误 | 商店/上限补丁非关键化；关注 RitsuLib Releases；用 capability gates |
| 商店 UI 逆向锚点不稳定 | 栏位功能失效 | 降级预案（6.3：事件发放 / 独立商店入口）；锚点搜索而非宽区间替换 |
| 附魔上限校验点随版本变化 | 上限修改失效或崩溃 | 独立 patcher + 设置页开关；UI 补丁失败仅降级显示 |
| 多附魔叠加破坏平衡 | 玩家口碑 | 上限可配置且默认保守；强力附魔加「独占」标记；发布后收集反馈热修 |
| 事件文案量大、双语维护成本高 | 工期超期 | 中文定稿后再译英文；事件数量控制在 3~5 个 |
| 与其他内容 Mod 冲突 | 崩溃/重复注册 | 独立 patcher、稳定 PatchId、兼容矩阵测试 |
| 美术资源缺失 | 完成度低 | 先用占位图标上线逻辑，美术在 M8 集中补齐 |

---

## 13. 参考资料索引

- 模组教程总站：https://tutorials.sts2modding.com/
  - RitsuLib 01-13 添加新附魔、01-03 添加新遗物、01-12 添加新事件、02 玩法基底（UI 相关）
  - Basics 02 反编译看源码、07 热重载、09 控制台、10 Patch、11 上传工坊、12 触发顺序
- RitsuLib 仓库与文档：
  - GitHub：https://github.com/BAKAOLC/STS2-RitsuLib
  - Getting Started：https://sts2-ritsulib.ritsukage.com/guide/getting-started
  - 内容注册：https://sts2-ritsulib.ritsukage.com/guide/content-authoring-toolkit
  - 补丁指南：https://sts2-ritsulib.ritsukage.com/guide/patching-guide
  - 生命周期事件：https://sts2-ritsulib.ritsukage.com/guide/lifecycle-events
  - Mod 设置 / 持久化 / 诊断兼容：同站点 guide 下对应页面
- Godot 4 文档：https://docs.godotengine.org/zh-cn/4.x/
- Harmony 文档：https://harmony.pardeike.net/articles/intro.html
- 备用库（了解即可）：BaseLib-StS2 https://github.com/Alchyr/BaseLib-StS2
