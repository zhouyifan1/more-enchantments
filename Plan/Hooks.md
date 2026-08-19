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
| `AfterObtained()` | RelicModel | 全部遗物 | 遗物拾起时触发（选牌附魔流程） |
| `HasUponPickupEffect` | RelicModel | 全部遗物 | 标记拾起即生效（驱动拾起提示 UI） |
| `MerchantCost` | RelicModel | ShopEnchantRelicBase/SmallWhetstone/HalfBowlWarPaint/LionSculpture | 商店售价定价覆写 |

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

## 4. 遗物相关（RelicModel / RitsuLib ModRelicTemplate）

### 注册与配置
- `[RegisterRelic(typeof(PoolType))]`：遗物必须进入遗物池。通用遗物用 `SharedRelicPool`；**角色限定遗物进角色专属池**（原版即如此，如 SneckoSkull 在 `SilentRelicPool`）：`IroncladRelicPool` / `SilentRelicPool` / `DefectRelicPool` / `NecrobinderRelicPool` / `RegentRelicPool`（命名空间 `MegaCrit.Sts2.Core.Models.RelicPools`）。
- `RelicRarity`：`Starter / Common / Uncommon / Rare / Shop / Event / Ancient`。
- `RelicAssetProfile(IconPath, IconOutlinePath, BigIconPath)`（RitsuLib 类型，`STS2RitsuLib.Scaffolding.Content`）：小图/轮廓 85x85、大图 256x256。本项目约定 `{类名}.png` + `{类名}Big.png`。
- 本地化 `relics.json`：`.title` / `.description` / `.flavor`（趣文）；动态变量照常 `{Var}` 占位。
- ⚠️ **RitsuLib 的 `ModRelicTemplate` 将 `ExtraHoverTips` 密封**：改用 `protected override IEnumerable<IHoverTip> AdditionalHoverTips` 追加悬停提示（另有 `RegisteredKeywordIds`、`IncludeEnergyHoverTip` 两个扩展点）。注意 `HoverTipFactory.FromEnchantment<T>(amount)` 与 `FromForge()` 返回的是 `IEnumerable<IHoverTip>` 集合，直接作为属性值返回即可。

### `AfterObtained()` + `HasUponPickupEffect`
拾起遗物时触发。`HasUponPickupEffect => true` 驱动原版拾起提示 UI。
- 本项目全部遗物共用基类 `EnchantOnPickupRelicBase<TEnchantment>`（`Scripts/Relics/`）：拾起时从牌组选牌附魔，具体遗物只需声明 `Rarity` / `MaxCards` / `EnchantAmount`。

### 「从牌组选牌并附魔」流程（参考原版遗物 GnarledHammer）
```csharp
CardSelectorPrefs prefs = new(CardSelectorPrefs.EnchantSelectionPrompt, 0, maxCards)
{
    Cancelable = false,
    RequireManualConfirmation = true
};
TEnchantment canonical = ModelDb.Enchantment<TEnchantment>();
foreach (CardModel card in await CardSelectCmd.FromDeckForEnchantment(Owner, canonical, amount, prefs))
{
    CardCmd.Enchant(canonical.ToMutable(), card, amount);  // 实例重载：CardCmd.Enchant(EnchantmentModel, CardModel, decimal)
    CardCmd.Preview(card);                                  // 展示被附魔后的卡牌预览
}
```
- `CardSelectCmd.FromDeckForEnchantment(player, enchantment, amount, prefs)`：选牌界面**自动按该附魔的 `CanEnchant` 过滤牌组**——所以遗物的卡牌类型限定（攻击牌/非能力牌/非消耗牌等）不需要额外代码，由附魔类自身保证。
- `CardSelectorPrefs(prompt, min, max)`：min=0 即"至多 N 张"。
- `HoverTipFactory.FromEnchantment<TEnchantment>(amount)`：在遗物悬停提示中展示附魔说明。

### 商店附魔遗物池（19 个遗物，见 Plan/ShopEnchantRelics.md）

- **中间基类 `ShopEnchantRelicBase<TEnchantment>`**（`Scripts/Relics/`）：在 `EnchantOnPickupRelicBase` 上统一覆写 `MerchantCost`——普通/罕见/稀有 → 75/125/175（低于原版 175/225/275）。个别遗物再覆写（如 LionSculpture 定价 225）。
- **随机升级模式**（SmallWhetstone/HalfBowlWarPaint，参考原版 Whetstone/WarPaint，不走附魔基类）：
  ```csharp
  foreach (CardModel item in PileType.Deck.GetPile(Owner).Cards
      .Where(c => c != null && c.Type == CardType.Attack && c.IsUpgradable)
      .ToList().StableShuffle(Owner.RunState.Rng.Niche)
      .Take(DynamicVars.Cards.IntValue))
      CardCmd.Upgrade(item);
  ```
  - `StableShuffle(Owner.RunState.Rng.Niche)`：扩展方法（`MegaCrit.Sts2.Core.Extensions`），用 Niche 随机流洗牌，不污染主随机数。
  - `c.IsUpgradable` 过滤不可升级卡；HalfBowlWarPaint 把 `CardType.Attack` 换成 `CardType.Skill`。

---

## 5. 配套命令与工具 API

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

## 6. 商店扩展（功能块 C：商店附魔遗物专属栏位）

实现文件：`Scripts/Patches/ShopInventoryPatch.cs`（数据侧）、`Scripts/Patches/ShopUiPatch.cs`（UI 侧）；遗物池 `Scripts/Relics/ShopEnchantRelicPool.cs`（`TypeListRelicPoolModel` + `[RegisterSharedRelicPool]`，遗物用 `[RegisterRelic(typeof(ShopEnchantRelicPool))]` 入池）。

### 遗物池机制结论（重要）

- **常规掉落抓袋 `RelicGrabBag.Populate` 只读 `SharedRelicPool` + 角色池**（`RelicGrabBag.cs`），自定义共享池遗物天然不进战斗掉落/宝箱/原生商店栏位——"商店专属"无需额外屏蔽。
- 原版单局去重靠抓袋"抽取即移除"（`RelicFactory.PullNext*` + `RelicCmd.Obtain` 二次移除）；绕开抓袋、每次 `ToMutable()` 新实例即可**单局重复获得**同一遗物。
- `RelicModel.Pool` 是反向查找 `ModelDb.AllRelicPools`（硬编码列表，RitsuLib `AllRelicPoolsPatch` 追加 Mod 池）——自定义池必须走 `[RegisterSharedRelicPool]`，否则访问 `Pool` 抛异常。
- 稀有度权重硬编码在 `RelicFactory.RollRarity(Rng)`：50% Common / 33% Uncommon / 17% Rare；商店遗物定价 `RelicModel.MerchantCost`（virtual）：Common 175 / Uncommon 225 / Rare 275 / Shop 200。

### 商店库存（MerchantInventory）

- 创建点：`MerchantRoom.EnterInternal` → `MerchantInventory.CreateForNormalMerchant(Player)`（每玩家一份）。本 Mod postfix 此方法，用 `AddRelicEntry(MerchantRelicEntry)`（public）追加条目。
- `MerchantRelicEntry(RelicModel, Player)` 构造器直接售卖指定遗物：不查重、不碰抓袋；`CalcCost` 用 `PlayerRng.Shops` 浮动 ±15%；购买后默认 `ClearAfterPurchase`（栏位隐藏、不补货）。
- **存档兼容**：商店库存不序列化，读档后重进房间重新生成；存档发生在进房间前且 `PlayerRng.Shops` 已序列化——postfix 中全部随机走 `PlayerRng.Shops` 即确定性重放，栏位读档前后一致，多人各端一致。
- 追加的条目未订阅 `PurchaseCompleted → UpdateEntries`（private）：仅"买本池遗物不触发全店刷新"这一次要行为缺失，反向刷新正常。
- **补货（RestockAfterPurchase）**：「送货员」(TheCourier，`ShouldRefillMerchantEntry => true`) 使任意商店条目售出后补货；原生 `MerchantRelicEntry.RestockAfterPurchase` 固定从原版抓袋抽取（`FillSlot(RollRarity, 在架黑名单)`）。`MerchantRelicEntry` 是 sealed 无法继承覆写——本 Mod 用 prefix 补丁（`ShopRelicRestockPatch`）拦截"售出遗物属于本池"的情况改从本池补货：复刻原生 `SetModel` 流程时，`Model` 私有 setter 与基类 `_player` 字段用 RitsuLib `PrivateAccess.DeclaredField/Field`（背字段名 `<Model>k__BackingField`）访问，`CalcCost()` 为 public 可直接调；本池无货可补时置 `Model = null` 按售罄隐藏。UI 刷新无需额外事件：`PurchaseCompleted → NMerchantRelic.OnSuccessfulPurchase → UpdateVisual` 会检测 Model 变更重建图标。

### 商店 UI（NMerchantInventory）

- `NMerchantInventory.Initialize(inventory, dialogue)` 按索引把 `RelicEntries[k]` 绑定到 `%Relics` 容器第 k 个 `NMerchantRelic` 子节点，**栏位数由场景预置子节点数决定**（原版 3 个；条目多于节点会 `GetChild(k)` 越界）。本 Mod 在 Initialize 的 prefix 按"条目数 - 节点数"补齐节点，绑定/购买/售出隐藏（`UpdateVisual`：`Model == null` → 隐藏栏位）全走原生路径。
- 栏位坐标烘焙在 `merchant_room.tscn`（C# 不可见）：新行位置从现有栏位 GlobalPosition 推算（新行 Y = 药水行 Y + 行距，X 与遗物行对齐）；货架开/关动画只是 `%SlotsContainer` 的 y 在 80/-1000 间 Tween。
- 复制栏位节点三级兜底：`SceneFilePath` 实例化 → `Duplicate()`（Initialize 前 `_relicNode` 为 null，无悬空图标引用；Duplicate 重映射子树 Owner，唯一名可解析）→ 手工构建（子节点 `%Hitbox` NClickableControl / `%CostLabel` MegaLabel / `%RelicHolder` Control，`UniqueNameInOwner = true` + `Owner = 栏位根`，并 `Set("_iconSize", NRelic.IconSize.Large)`）。
- `GetAllSlots()` 自动纳入 `%Relics` 下所有 `NMerchantRelic`（焦点转移、默认焦点等）；`UpdateNavigation`（protected virtual，可补丁）把遗物容器所有栏位与无色卡牌当同一行链焦点，新增独立行需 postfix 修正（按 GlobalPosition 就近计算，不依赖索引）。
- 栏位 `_Ready` 在 AddChild 时触发：`ConnectSignals()` 需要 `%Hitbox`；`NMerchantSlot.Initialize(rug)` 订阅 `Player.GoldChanged` 刷新价格颜色。

---

## 7. 遗留注意事项

- **商店栏位实机验证**：商店附魔遗物池已加入 19 个遗物（功能激活）；需实机检查：3 个栏位在药水下方、同店不重复、跨商店可重复、购买/售出隐藏正常、读档后栏位内容不变、75/125/175 定价生效。栏位坐标烘焙在 tscn 中，若新行超出屏幕需调整 `ShopUiSlotsPatch` 的行距推算。
- **附魔移除不回滚**：`ClearEnchantmentInternal` 只解除引用，`OnEnchant` 加的关键词/费用修改会残留。功能块 D（驱散之泉事件）实现移除时，需自行 `RemoveKeyword` / 重置费用（参考 `CardModel.DowngradeInternal` 的重置+重放模式）。
- **Stick 与重放的交互**：重放序列中每次打出都会计数，次数用尽后当次序列的后续打出不再回手（符合"前 Amount 次"语义）。
- **Weakening/Serrated 与自指目标**：攻击牌目标默认为敌人；若未来出现 `TargetType.Self` 的攻击牌，`cardPlay.Target` 可能是友方，届时需加目标阵营判断。
