# Hooks 使用笔记 —— More Enchantments

> 本文档记录本 Mod 开发过程中**实际使用过**的游戏 Hook 与配套 API，供后续开发查阅与复用。
> **维护约定：每次开发新功能（附魔/遗物/事件/补丁等）时，把新用到的 Hook 补充进本文档。**
>
> - Hook 全量清单见反编译源码 `D:\Program Files\Godot\WorkPlace\STS2\MegaCrit.Sts2.Core.Models\AbstractModel.cs`（约 100+ 个 `public virtual` 方法）。
> - 附魔专属钩子见 `MegaCrit.Sts2.Core.Models\EnchantmentModel.cs`。
> - 触发顺序细节参考 skill 文档 `.agents/skills/sts2-ritsulib-modding/references/hook-order.md`。

---

## 0. Hook 触发机制基础（必读）

1. **Hook 来源**：所有模型（卡牌/附魔/遗物/能力……）的 Hook 都是 `AbstractModel` 的虚方法，直接 `override` 即可生效，无需手动注册。
2. **附魔的 Hook 生效条件**：`EnchantmentModel.ShouldReceiveCombatHooks => Card?.ShouldReceiveCombatHooks`，而卡牌侧为 `Pile?.IsCombatPile ?? false`——**附魔挂在的卡牌必须处于战斗牌堆（手牌/抽牌堆/弃牌堆/消耗堆/打出堆）中，Hook 才会触发**。
3. **战斗卡是牌组卡的克隆体**：战斗开始时 `Player.PopulateCombatState` 用 `CombatState.CloneCard(deckCard)` 生成克隆（`card.DeckVersion` 指回牌组卡）。推论：
   - **Per-combat 状态用普通字段即可**（如计数器）：牌组卡上的字段永远是初始值，克隆时通过浅拷贝带过去，每场战斗自动"归零"。本场战斗中对克隆体的修改（数值、关键词、费用）不会影响牌组。
   - 需要永久生效的修改必须显式作用于 `DeckVersion`。
4. **`OnEnchant` 的执行时机**（对实现"附魔时修改卡牌"至关重要）：
   - `CardCmd.Enchant<T>`（附魔时）→ `EnchantInternal` + `ModifyCard()` → 执行 `OnEnchant`；
   - **克隆时不重放** `OnEnchant`：`DeepCloneFields` 只克隆费用/关键词/动态变量等状态并 `EnchantInternal`（不调 `ModifyCard`）；
   - 降级（`DowngradeInternal`）与读档（`FromSerializable`）会先重置卡牌状态、再重放 `OnEnchant`——**不会重复叠加**。
   - 结论：`OnEnchant` 里可以安全地做"+1 费用"这类幂等性依赖单次执行的修改。
5. **多人注意**：任何带 `Player` 参数的 Hook 都要先判断 `player != Card.Owner`（或对应 Owner）再执行，参考原版遗物 `Bookmark`。

---

## 1. 已使用 Hook 总览

| Hook | 定义于 | 使用位置 | 用途 |
| --- | --- | --- | --- |
| `CanEnchantCardType(CardType)` | EnchantmentModel | Heavy/Stick/BurntOut/Weakening/Serrated/Poisoned/Cursed | 按卡牌类型限制附着 |
| `CanEnchant(CardModel)` | EnchantmentModel | Resilience/BurntOut | 按卡牌属性限制附着 |
| `OnEnchant()` | EnchantmentModel | Resilience/Heavy/BurntOut | 附魔应用时修改卡牌（关键词/费用） |
| `OnPlay(choiceContext, cardPlay)` | EnchantmentModel | Tactics/Reaction/Weakening/Serrated/Sacrifice/Electric/Forge | 卡牌打出时触发 |
| `EnchantDamageMultiplicative(decimal, ValueProp)` | EnchantmentModel | Heavy | 伤害乘算修改 |
| `EnchantPlayCount(int)` | EnchantmentModel | BurntOut | 修改打出次数（重放） |
| `ShowAmount` / `HasExtraCardText` | EnchantmentModel | 全部 | 数值角标 / 追加卡牌文本开关 |
| `DisplayAmount` | EnchantmentModel | Stick | 角标显示值覆写（实时倒数剩余次数） |
| `ExtraHoverTips` | EnchantmentModel | Resilience/BurntOut/Weakening/Serrated/Tactics/Reaction/Poisoned/Sacrifice/Cursed/Electric/Forge | 额外悬停提示 |
| `AfterFlush(choiceContext, player, flushed, retained)` | AbstractModel | Resilience | 回合结束弃牌/保留结算后 |
| `AfterCardPlayed(choiceContext, cardPlay)` | AbstractModel | Stick | 任意卡牌打出后（计数） |
| `AfterDamageGiven(choiceContext, dealer, result, props, target, cardSource)` | AbstractModel | Poisoned/Cursed | 任意模型造成伤害后（按 cardSource 过滤本牌） |
| `ModifyCardPlayResultLocation(card, isAutoPlay, resources, location)` | AbstractModel | Stick | 改写卡牌打出后的去向 |

---

## 2. 附魔专属钩子（EnchantmentModel）

### `CanEnchantCardType(CardType cardType)`
按卡牌类型粗筛。默认 `true`。`CardType` 枚举：`None, Attack, Skill, Power, Status, Curse, Quest`（基类 `CanEnchant` 已排除 Status/Curse/Quest）。
- 本项目用法：攻击牌限定 `cardType == CardType.Attack`（Heavy/Weakening/Serrated）；非能力牌 `cardType != CardType.Power`（Stick/BurntOut——能力牌打出后离场，回手/重放无意义）。

### `CanEnchant(CardModel card)`
按卡牌属性细筛，**务必带上 `base.CanEnchant(card)`**（基类已处理：不可叠加附魔冲突、卡组中不可打出卡等）。
- 本项目用法：`card.GainsBlock && card.DynamicVars.ContainsKey("Block")`（Resilience，防无 Block 变量的卡崩溃）；`!card.Keywords.Contains(CardKeyword.Exhaust)`（BurntOut，已有消耗的卡排除）。

### `OnEnchant()`
附魔应用时调用一次，用于给卡牌做持久修改（本场克隆生命周期内）。
- 加关键词：`Card.AddKeyword(CardKeyword.Retain)`（原版 Steady 同款）、`Card.AddKeyword(CardKeyword.Exhaust)`；
- 改费用：`Card.EnergyCost.SetCustomBaseCost(Card.EnergyCost.GetWithModifiers(CostModifiers.None) + 1)`（Heavy）。X 耗能/特殊费用卡需先判 `!CostsX && Canonical >= 0`。
- 时机细节见 §0.4；附魔被移除时**不会**自动回滚这些修改（原版惯例如此，规划中的"驱散"功能需注意）。

### `OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)`
附魔卡牌被打出时调用（重放序列中每次打出都会触发）。`cardPlay` 可空。
- `cardPlay.Target`：单体目标（`Creature?`）；群体攻击卡为 `null`，全体敌人用 `Card.CombatState.HittableEnemies`（参考卡牌 SweepingBeam）。
- 施加能力：`await PowerCmd.Apply<WeakPower>(choiceContext, target, Amount, Card.Owner.Creature, Card)`（参考卡牌 Comet/BeamCell；另有 `IEnumerable<Creature>` 重载用于群体）。

### `EnchantDamageMultiplicative(decimal originalDamage, ValueProp props)`
伤害乘算因子，默认 `1m`。
- Heavy：`props.IsPoweredAttack() ? 2m : 1m`（与原版 Instinct 一致；`IsPoweredAttack` 排除非攻击/无力量加成的伤害）。
- 同族钩子：`EnchantDamageAdditive` / `EnchantBlockAdditive` / `EnchantBlockMultiplicative`。

### `EnchantPlayCount(int originalPlayCount)`
修改打出次数，实现"重放"。卡牌最终打出次数 = `Enchantment.EnchantPlayCount(BaseReplayCount)`。
- BurntOut：`originalPlayCount + Amount`。参考原版 Glam（每场战斗限次，配 `_usedThisCombat` 字段 + `Status = Disabled`）与 Spiral（不限次）。
- 相关悬停提示：`HoverTipFactory.Static(StaticHoverTip.ReplayStatic)`（无数字）/ `ReplayDynamic`（带 DynamicVar 数字，Glam 用法）。

### 显示类属性
- `ShowAmount`：卡牌上是否显示 Amount 角标（Heavy 这类无数值的关掉）。
- `DisplayAmount`：角标显示值覆写。Stick 用 `Amount - _playsThisCombat` 让角标实时倒数剩余次数。
- `HasExtraCardText`：是否向卡牌描述追加 `.extraCardText` 本地化文本；`Status = Disabled` 时自动隐藏。
- `ExtraHoverTips`：额外悬停提示。关键词用 `HoverTipFactory.FromKeyword(CardKeyword.X)`，能力用 `HoverTipFactory.FromPower<XxxPower>()`。
- 本地化 `{Amount}` 占位符由 `EnchantmentModel` 自动注入，**不需要**为它声明 `CanonicalVars`。

### 卡面文本动态数值模式（运行时更新 `{占位符}`）
`DynamicExtraCardText`/`DynamicDescription` 注入 `{Amount}` 后还会执行 `DynamicVars.AddTo(...)`，因此**运行时修改附魔自己的 DynamicVar 即可让卡面文本实时变化**：
1. `CanonicalVars` 声明变量（如 `new IntVar("Returns", 1m)`），本地化写 `{Returns}`；
2. `OnEnchant()` 里把变量同步为初始值（`DynamicVars["Returns"].BaseValue = Amount`——此时 Amount 已被赋值；降级/读档重放 `OnEnchant` 也会自动复位）；
3. 战斗钩子中更新 `DynamicVars["Returns"].BaseValue`，卡牌下次重绘（回手/换堆等）即显示新值。
- Stick 用此模式实现"下{Returns}次打出时回到手牌"逐次递减。变量值随 `DeepCloneFields` 克隆传递，只影响战斗克隆体。
- 派生数值同理：Sacrifice 用 `IntVar("StrengthGain")` 在 `OnEnchant` 同步为 `2 * Amount`，卡面直接展示最终力量值（`{StrengthGain}`），效果结算时也读该变量，避免文本写 `{Amount}×2` 这类无法求值的表达式。

---

## 3. 通用战斗钩子（AbstractModel，附魔可直接 override）

### `AfterFlush(PlayerChoiceContext, Player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)`
回合结束弃牌/保留结算完成后调用。`retainedCards` 为被保留的卡牌集合——"被保留时"效果的正确挂点（参考原版遗物 Bookmark）。
- Resilience：`retainedCards.Contains(Card)` 则 `Card.DynamicVars` 的 Block 变量 `BaseValue += Amount`（成长仅作用于战斗克隆体，见 §0.3）。
- 多人需判 `player != Card.Owner`。

### `AfterCardPlayed(PlayerChoiceContext, CardPlay cardPlay)`
任意卡牌打出后触发（全模型广播），需自行过滤 `cardPlay.Card == Card`。
- Stick：每场战斗计数（`_playsThisCombat` 普通字段，克隆机制保证每场归零，见 §0.3），用尽后 `Status = EnchantmentStatus.Disabled`（图标置灰）。
- 同族钩子：`BeforeCardPlayed` / `AfterCardPlayedLate`。

### `AfterDamageGiven(PlayerChoiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)`
任意模型造成伤害后触发（全模型广播）。附魔实现"这张牌造成伤害时……"效果的挂点，参考原版状态 EnvenomPower / ReaperFormPower。
- **必须过滤 `cardSource == Card`**：原版状态用 `dealer == Owner` 即可，但附魔挂在卡上时只要卡处于战斗牌堆就接收 Hook，不过滤会变成"持卡即生效"的全局光环。
- Poisoned：再叠 `props.IsPoweredAttack() && result.UnblockedDamage > 0`，对 `target` 施加 `PoisonPower × Amount`（多段伤害逐段触发）。
- Cursed：用 `result.TotalDamage`（含格挡前的总伤害）作为灾厄施加量，参考 ReaperFormPower。
- `DamageResult` 在 `MegaCrit.Sts2.Core.Entities.Creatures` 命名空间。

### ~~`AfterPlayerTurnStart`~~ → 改用原版"下回合"状态（经验记录）
Tactics/Reaction 曾用 `OnPlay` 计数 + `AfterPlayerTurnStart` 自管结算，已重写为打出时施加原版状态：
`EnergyNextTurnPower`（参考卡牌 ChargeBattery）/ `DrawCardsNextTurnPower`（参考卡牌 Predator）。
**教训：游戏已有现成状态/机制时优先复用**——层数叠加、结算顺序、存档、UI 图标全部由原版处理，无需自管 per-combat 计数字段。

### `ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)`
Modify 类钩子：改写卡牌打出后的去向，返回新位置。
- Stick：参照卡牌 ParticleWall 的做法，仅当 `cardLocation.pileType == PileType.Discard` 时改为 `PileType.Hand`（保留消耗等其他去向的语义）。
- `CardLocation` 是可变 struct，改 `pileType` 字段后返回。
- 同族 Modify 钩子还有 `ModifyDamageAdditive/Multiplicative`、`ModifyBlockXxx`、`ModifyHandDraw`、`ModifyEnergyGain` 等（完整见 AbstractModel.cs）。

---

## 4. 配套命令与工具 API

| API | 用途 | 使用位置 |
| --- | --- | --- |
| `PowerCmd.Apply<TPower>(choiceContext, target/IEnumerable<Creature>, amount, applier, cardSource)` | 施加能力层数 | Weakening/Serrated/Tactics/Reaction/Poisoned/Sacrifice/Cursed |
| `CreatureCmd.Damage(choiceContext, target, amount, ValueProp.Unblockable \| Unpowered \| Move, cardSource, cardPlay)` | "失去生命"的标准实现（绕过格挡与力量，参考卡牌 Bloodletting） | Sacrifice |
| `OrbCmd.Channel<TOrb>(choiceContext, player)` | 生成充能球（参考卡牌 Zap） | Electric |
| `ForgeCmd.Forge(amount, player, source)` | 铸造（参考卡牌 TheSmith） | Forge |
| `Card.EnergyCost.SetCustomBaseCost(int)` / `GetWithModifiers(CostModifiers.None)` | 读/改卡牌基础费用 | Heavy |
| `Card.DynamicVars.TryGetValue("Block", out var)` / `ContainsKey` | 安全访问动态变量（`DynamicVarSet` 实现 `IReadOnlyDictionary`） | Resilience |
| `DynamicVar.BaseValue`（setter） | 修改数值变量基础值（自动刷新预览值） | Resilience/Stick |
| `EnchantmentStatus.Normal / Disabled` | 附魔状态：置灰图标、隐藏追加文本 | Stick（Glam 同用法） |
| `Card.CombatState.HittableEnemies` | 当前全体可攻击敌人 | Weakening/Serrated |
| `HoverTipFactory.FromKeyword / FromPower<T> / FromOrb<T> / FromForge() / Static(StaticHoverTip.X)` | 悬停提示工厂 | 各附魔 |

---

## 5. 遗留注意事项

- **附魔移除不回滚**：`ClearEnchantmentInternal` 只解除引用，`OnEnchant` 加的关键词/费用修改会残留。功能块 D（驱散之泉事件）实现移除时，需自行 `RemoveKeyword` / 重置费用（参考 `CardModel.DowngradeInternal` 的重置+重放模式）。
- **Stick 与重放的交互**：重放序列中每次打出都会计数，次数用尽后当次序列的后续打出不再回手（符合"前 Amount 次"语义）。
- **Weakening/Serrated 与自指目标**：攻击牌目标默认为敌人；若未来出现 `TargetType.Self` 的攻击牌，`cardPlay.Target` 可能是友方，届时需加目标阵营判断。
