# 事件名称 (EventClassName)

> 一句话概述这个事件的叙事与玩法定位（获取/移除/博弈附魔？）。

## 基本信息

| 项 | 值 | 说明 |
| --- | --- | --- |
| 类名 (PascalCase) | `EnchantingAltarEvent` | 发布后 Entry ID 固定为 `MORE_ENCHANTMENTS_EVENT_ENCHANTING_ALTAR_EVENT`，不得更改 |
| 注册方式 | `[RegisterActEvent(typeof(Overgrowth))]`<br />`[RegisterActEvent(typeof(Underdocks))]` | 见下方「注册方式」一节，四选一 |
| 生成条件 (IsAllowed) | 牌组中至少有一张可附魔的牌 | 留空表示无条件（返回 base，即恒 true）；两种注册方式都可覆写 |
| 布局类型 | Normal / Combat | Combat 需另填「遭遇」并在备注中描述遭遇设计 |
| 立绘 | `res://MoreEnchantments/images/events/EnchantingAltarEvent.png` | 未绘制前复制重命名 `res://DefaultPics/icon256.png` 占位 |

## 动态变量 (CanonicalVars)

| 变量 | 类型 | 基础值 | 说明 |
| --- | --- | --- | --- |
| Damage | `DamageVar(10m, ValueProp.Unblockable \| ValueProp.Unpowered)` | 10 | 选项代价，本地化用 `{Damage}` 占位 |
| Gold | `GoldVar(60)` | 60 | 支付金币 |

## 页面与选项流程

> 页面名全大写（`INITIAL` 为入口页）。选项 KEY 全大写蛇形，对应代码中的 `InitialOptionKey("KEY")` / `ModOptionKey("页面", "KEY")`。

```text
INITIAL ──[TAKE_DAMAGE]──> CHOOSE_TYPE ──[CHOOSE_POTIONS]──> POTIONS_CHOSEN (结束)
         └─[LOSE_GOLD]───> (直接结束)
```

### INITIAL 页

描述文案（zhs）：

> 一座藤蔓缠绕的石祭坛立在路中央，坛面刻满了你从未见过的符文……

| 选项 KEY | 标题 | 描述 | 出现条件 | 效果 | 跳转 |
| --- | --- | --- | --- | --- | --- |
| TAKE_DAMAGE | 献上鲜血 | 失去 {Damage} 点生命 | 无 | 扣血后进入选奖励页 | CHOOSE_TYPE |
| LOSE_GOLD | 献上金币 | 失去 {Gold} 金币 | 金币 ≥ {Gold} | 扣金币，结束事件 | 结束 |

### CHOOSE_TYPE 页

描述文案（zhs）：

> 祭坛上的符文亮了起来，等待你的下一步选择。

| 选项 KEY | 标题 | 描述 | 出现条件 | 效果 | 跳转 |
| --- | --- | --- | --- | --- | --- |
| CHOOSE_POTIONS | 要药水 | 获得 1 瓶随机药水 | 无 | `RewardsCmd.OfferCustom` 发放药水 | POTIONS_CHOSEN |
| CHOOSE_CARDS | 要附魔 | 选择 1 张牌附加随机附魔 | 牌组有可附魔牌 | `CardCmd.Enchant<T>(card, amount)` | 结束 |

### POTIONS_CHOSEN 页（结束页）

描述文案（zhs）：

> 符文暗了下去，祭坛恢复了沉默。

（结束页用 `SetEventFinished(描述)` 关闭事件，无选项。）

## 注册方式

事件进入生成池的规则：一个章节的 run 事件池 = 该章节 `ActModel.AllEvents`（章节限定事件）+ `ModelDb.AllSharedEvents`（共享事件），洗牌后生成（`ActModel.GenerateRooms`）；事件房间实际开出前再过一次 `IsAllowed(IRunState)`。注册方式四选一：

| 方式 | 写法 | 效果 | 适用 |
| --- | --- | --- | --- |
| CLR 注解·章节限定（首选） | `[RegisterActEvent(typeof(Glory))]` | 只进入 Glory 章节的事件池 | 绝大多数事件；由 `ModTypeDiscoveryHub` 自动发现 |
| CLR 注解·共享 | `[RegisterSharedEvent]` | 进入**所有章节**的事件池 | 通用事件 |
| 代码注册·章节限定 | `ModContentRegistry.RegisterActEvent<TEvent, TAct>()`（或 `(eventType, actType)` 非泛型重载） | 同上，但注册时机/条件由代码控制 | 批量注册、条件注册（如按 Mod 设置开关） |
| 代码注册·共享 | `ModContentRegistry.RegisterSharedEvent<TEvent>()`（或 `(eventType)` 重载） | 同上 | 同上 |

说明：

- 本项目约定优先用 CLR 注解（注册点贴近模型类）；同一事件只用一种注册来源，不要注解和代码重复注册。
- 两种注册方式都可以覆写 `IsAllowed(IRunState)` 加生成条件（章节限定事件同样生效，如 `AncientTechnologyEvent`）；共享事件没有章节约束，生成条件全写在 `IsAllowed` 里。
- 原版「时间线/Epoch 解锁」过滤（`Event1Epoch` 等）只针对原版事件，不影响 Mod 事件。
- 先古之民（Ancient）是另一类内容（`ModAncientEventTemplate`），对应注解 `[RegisterActAncient(typeof(XxxAct))]` / `[RegisterSharedAncient]` + `IsAllowed`/`IsValidForAct`，不要把两者混淆。

可用章节类（`MegaCrit.Sts2.Core.Models.Acts`，按 run 内顺序，`ModelDb.Acts`）：

| 章节类 | 章节位置 |
| --- | --- |
| `Overgrowth` | 第一章（两个变体之一） |
| `Underdocks` | 第一章（两个变体之一） |
| `Hive` | 第二章 |
| `Glory` | 第三章 |

第一章会在 `Overgrowth` / `Underdocks` 两个变体中随机选取，想让事件"第一章必出"需要同时注册两个章节（写两个 `[RegisterActEvent]` 注解，或改用 `[RegisterSharedEvent]` + `IsAllowed` 里判断当前章节）。（另有 `DeprecatedAct` 为废弃占位，勿用。）

## 联动内容

| 类型 | 引用 | 说明 |
| --- | --- | --- |
| 附魔 | `ResilienceEnchantment` 等 | 事件给予的附魔，引用 `Plan/Enchantments.md` 中的名称 |
| 遗物/卡牌/遭遇 | … | 涉及的奖励或战斗遭遇 |

## 备注

- 代码参考：原版事件/机制实现参考 `D:\Program Files\Godot\WorkPlace\STS2\` 中的类名（如 `XxxEvent`），仅说明有类似词条，不保证可套用。
- 特殊生命周期：如进入事件时需 `Owner!.CanRemovePotions = false`（`BeforeEventStarted` / `OnEventFinished`），在此注明。
- 平衡意图：每个选项的「代价 ↔ 收益」权衡说明。
- 英文文案：中文定稿后由我统一翻译，不用在本文档维护。

## 状态

待设计 / 已定稿 / 已实现 / 已测试
