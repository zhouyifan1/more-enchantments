# 玩法与 UI：卡堆 / 顶栏按钮 / Toast / 血条覆盖 / 手牌 / 角标 / 奖励 / 目标 / 右键 / 次要资源 / 节点附加

> 目录：自定义卡牌堆 → 顶栏按钮 → Toast 通知 → 血条覆盖 → 手牌上限 → 手牌泛光 → 额外角标 → 自定义奖励 → 自定义目标 → 右键交互 → 次要资源 → 节点附加

## 自定义卡牌堆

`Entry.Init` 注册，`PileType` 存静态字段：

```csharp
using STS2RitsuLib.CardPiles;

public static PileType VoidPile;

var registry = ModCardPileRegistry.For(ModId);
VoidPile = registry.RegisterOwned("void_pile", new ModCardPileSpec
{
    Scope = ModCardPileScope.CombatOnly,      // CombatOnly 战斗结束销毁 / RunPersistent 跨战斗（内存，需自行存档）
    Style = ModCardPileUiStyle.BottomLeft,    // Headless / TopBarDeck / BottomLeft / BottomRight / ExtraHand
    Anchor = ModCardPileAnchor.Default,
    IconPath = "res://Test/images/void_pile.png",
    OnOpen = ctx => ctx.ShowDefaultPileScreen(),
    VisibleWhen = ctx => ctx.Player != null,
}).PileType;
```

Anchor 写法：`ModCardPileAnchor.Default`；`new ModCardPileAnchor(ModCardPileAnchorKind.BottomLeftSecondary, new Vector2(0,-2))`；Custom 坐标 `ModCardPileAnchor.AtPosition/AtCenter/AtPivot(...)`。
Kind 与 Style 搭配：`BottomLeftPrimary/Secondary`（抽/弃牌堆右侧起）、`BottomRightPrimary/Secondary`（消耗堆左侧起）、`TopBarAfterDeck`、`TopBarBeforeModifiers`、`ExtraHandAbove/Below`、`Custom`。

操作牌堆（同原版 API）：

```csharp
await CardPileCmd.Add(card, Entry.VoidPile);
var pile = Entry.VoidPile.GetPile(player);
foreach (var c in pile.Cards) { /* ... */ }
```

本地化 `static_hover_tips.json`，键 `{MODID}_CARDPILE_{ID大写}`：`.title` / `.description` / `.empty`。

## 顶栏按钮

```csharp
[RegisterOwnedTopBarButton("recipes", IconPath = "res://Test/images/recipe_icon.png",
    ButtonOrder = 0 /*, OffsetX = 0, OffsetY = 0*/)]
public class RecipeButtonHandler : IModTopBarButtonHandler
{
    public void OnClick(ModTopBarButtonContext ctx)
    {
        // ctx.OpenCapstoneScreen(myScreen); ctx.ToggleCapstoneScreen(myScreen); ctx.CloseCapstoneScreen();
    }
    public bool IsVisible(ModTopBarButtonContext ctx) => ctx.Player != null;
    public bool IsOpen(ModTopBarButtonContext ctx) => ModScreenService.CurrentCapstoneScreen is MyRecipeScreen;
    public int GetCount(ModTopBarButtonContext ctx) => -1;   // 角标数字，-1 不显示
}
```

本地化 `static_hover_tips.json`：`{MODID}_TOPBARBUTTON_{ID大写}.title/.description`。
显式注册：`ModTopBarButtonRegistry.For(ModId).RegisterOwned("recipes", new ModTopBarButtonSpec { ... OnClick = ctx => ..., VisibleWhen = ctx => ... })`。

## Toast 通知

框架在 `GameReadyEvent` 后挂载，局内/UI 就绪后再调：

```csharp
using STS2RitsuLib.Ui.Toast;

RitsuToastService.ShowInfo("Mod 已加载");                          // 正文[, 标题, 点击动作]
RitsuToastService.ShowWarning("生命值过低", "警告");
RitsuToastService.ShowError("保存失败。", onClick: () => { });

// 完全自定义
RitsuToastService.Show(new RitsuToastRequest(
    body: "新配方已解锁！", title: "配方", image: myTexture,
    level: RitsuToastLevel.Info, durationSeconds: 5.0,   // null=默认3.5秒
    onClick: () => { },
    animationOverride: RitsuToastAnimationPreset.FadeScale)); // Fade / FadeSlide(默认) / FadeScale
```

## 血条覆盖（中毒/灾厄式）

能力类实现 `IHealthBarForecastSource`：

```csharp
public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
{
    return HealthBarForecasts.Single(
        context.Creature.GetPowerAmount<TestPower2>(),     // 展示数量
        new Color(0.4f, 0.1f, 0.1f),                        // 颜色
        HealthBarForecastGrowthDirection.FromLeft           // 延伸方向
        // 0,                                               // 顺序，越大越远离血条边缘
        // PreloadManager.Cache.GetMaterial("res://x.tres") // 自定义材质
    );
}
```

## 手牌上限

模型（如 Power）实现 `IMaxHandSizeModifier`：

```csharp
public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
{
    if (player != Owner.Player) return currentMaxHandSize;
    return currentMaxHandSize + 2;
}
```

- `ModifyMaxHandSizeLate` 更晚执行，适合设固定值；不会小于 0（有兜底）。
- 读手牌上限用 `RitsuLibFramework.GetMaxHandSize(player)`，不要硬编码 10。

## 手牌泛光

原版金/红光：卡牌类重写 `ShouldGlowGoldInternal` / `ShouldGlowRedInternal`。
任意颜色（`Entry.Init` 注册）：

```csharp
ModCardHandOutlineRegistry.Register<TestCard>(ModCardHandOutlineRules.Fixed(
    card => card.Owner.Creature.CurrentHp <= 10,   // 条件
    Colors.Purple                                   // 颜色
    // 0,      // 优先级
    // false    // 不可打出时隐藏
));

// 动态颜色
ModCardHandOutlineRegistry.Register<TestCard>(ModCardHandOutlineRules.Dynamic(
    card => card.Owner.Creature.CurrentHp <= 10,
    card => card.Owner.Creature.CurrentHp <= 5 ? Colors.Red : Colors.Orange));
```

泛型类型可填卡牌基类让所有子类生效。

## 额外角标（图标角落多数字）

- 能力：实现 `IPowerExtraIconAmountLabelSpecsProvider`，返回 `[ExtraIconAmountLabelSpec.Plain(ExtraIconAmountLabelCorner.TopLeft, Amount.ToString()), ExtraIconAmountLabelSpec.RichText(ExtraIconAmountLabelCorner.BottomLeft, "[color=gold]x2[/color]")]`。
- 遗物：实现 `IRelicExtraIconAmountLabelSpecsProvider`；改值后调 `InvokeDisplayAmountChanged()` 刷新；不依赖 DisplayAmount 的另实现 `IRelicExtraIconAmountLabelsChangeSource` 触发 `RelicExtraIconAmountLabelsInvalidated`。
- 意图：`AbstractIntent` 子类实现 `IIntentExtraCornerAmountLabelsProvider`，返回 `ExtraIconAmountLabelSlot.At(corner, "+2")`。
- 角落：`TopLeft`/`TopRight`/`BottomLeft`/`BottomRight`（原版主计数常用，谨慎）/`Custom`（自供 Rect2）。

## 自定义奖励

```csharp
// 1. Init 注册 RewardType
public static RewardType TokenRewardType;
TokenRewardType = ModRewardRegistry.For(ModId)
    .RegisterOwned("token", (save, player, json) => new MyTokenReward(player))  // 生成 MYMOD_REWARD_TOKEN
    .RewardType;

// 2. 奖励类
public class MyTokenReward : ModCustomReward
{
    public MyTokenReward(Player player) : base(player) { }
    public override RewardType ModRewardType => Entry.TokenRewardType;
    protected override string? RewardIconPath => "res://MyMod/images/rewards/token.png";
    public override void MarkContentAsSeen() { }
    protected override async Task<bool> OnSelect()
    {
        await PlayerCmd.GainGold(25, Player);
        return true;   // false=取消，按钮保留
    }
}
```

本地化 `gameplay_ui.json`：`"MYMOD_REWARD_TOKEN": "获得 25 金币"`。
发放：战斗里 `RewardCmd` 系列或 `RewardsCmd.OfferCustom`；带 Payload 存档/联机同步规则见教程 `02 - 玩法基底/10 - 自定义奖励`。

## 自定义目标类型

预置（直接把卡牌 `TargetType` 填为这些，无需注册）：`CustomTargetType.Anyone`（任意存活生物单体）、`Everyone`、`AnyAttackingEnemy`/`AllAttackingEnemies`（攻击意图）、`AnyBlockingEnemy`/`AllBlockingEnemies`（护甲>0）、`AllHighestHpEnemies`/`AllLowestHpEnemies` 等。

注册自定义：

```csharp
using STS2RitsuLib.Combat.CardTargeting;

public static TargetType WoundedEnemy { get; private set; }

WoundedEnemy = CustomTargetType.RegisterSingleTargetType(Entry.ModId, "WOUNDED_ENEMY",
    creature => creature is { IsMonster: true, IsAlive: true } && creature.CurrentHp < creature.MaxHp);
// 群体: CustomTargetType.RegisterMultiTargetType(...)
```

标识字符串**发布后绝不修改**（写入存档）。
结算统一用 `this.GetTargets(cardPlay.Target)`（单体校验/群体按规则收集）：

```csharp
foreach (var target in this.GetTargets(cardPlay.Target))
    await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target).Execute(choiceContext);
```

## 右键交互（卡牌/遗物/能力/药水）

方式一：模型实现接口 `IModRightClickableCard` / `IModRightClickableRelic` / `IModRightClickablePower` / `IModRightClickablePotion`：

```csharp
public bool CanHandleRightClickLocal(ModRightClickContext context) => Amount > 0;  // 可选预检
public async Task OnRightClick(ModRightClickExecutionContext context)
{
    RitsuToastService.ShowInfo($"当前层数：{Amount}");   // 多人下全客户端同步执行
}
```

方式二：`ModRightClickRegistry.Register<CardModel>(ModId, "examine", canHandle: ctx => ..., execute: async ctx => ..., priority: 0)` 返回 `IDisposable`。
方式三：注册接口实现。框架自动处理多人同步、手柄兼容、优先级。

## 次要资源（第二套战斗资源，类星辉）

```csharp
using STS2RitsuLib.Combat.SecondaryResources;

var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);
ManaDefinition = registry.Register("mana", new SecondaryResourceDefinition(
    defaultAmount: 0, baseMaxAmount: 3,
    turnStartPolicy: SecondaryResourceTurnStartPolicy.AddMaxToCurrent, // None/ResetToMax/AddMaxToCurrent/Clear
    persistencePolicy: SecondaryResourcePersistencePolicy.Run,          // None/Combat/Run
    smallIconPath: "res://Test/images/resources/mana_small.png",
    largeIconPath: "res://Test/images/resources/mana_large.png"));
ManaId = ManaDefinition.Id;   // TEST_SECONDARY_RESOURCE_MANA
```

操作：`SecondaryResourceCmd.Get/GetMax/Gain/Lose/Set/Spend(不足返回false)/Reset(player, id, ...)`（Gain/Spend 过 Hook）。

卡牌费用（构造函数）：`this.SecondaryCosts().Set(ModResources.ManaId, 2);`；X 费用 `SecondaryResourceCost.X()` / `.X(2)`；时限 `SecondaryResourceCostDuration.ThisTurn/ThisCombat/UntilPlayed`；免费 `SecondaryResourceCost.Free`；`Clear(id)` 移除。
X 效果数值：`cardPlay.SecondaryResources().Value(ModResources.ManaId)`。
卡牌文本显示图标与战斗 UI 路由见教程 `02 - 玩法基底/13 - 次要资源`。

## 节点附加（给原版场景挂子节点）

显式注册（`Entry.Init`）：

```csharp
ModNodeAttachmentRegistry.For(ModId)
    .RegisterReadyChild<NCombatUi, TestCombatUiBadge>("combat_ui_badge",
        static _ => new TestCombatUiBadge(),
        static (parent, node) => node.Bind(parent),
        new NodeAttachmentOptions
        {
            Name = "TestCombatUiBadge",
            Order = 10,
            DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            SetupTiming = NodeAttachmentSetupTiming.AfterAdd,
        });
```

场景版：`RegisterReadyChildFromScene<TParent, TNode>(id, "res://...tscn", setup, options)`（场景根即 TNode）；需场景转换用 `RegisterReadyChildFromConvertedScene`（TNode 需公开无参构造）。

自动注册（子节点类上）：

```csharp
[RegisterNodeAttachment(typeof(NCombatUi), "turn_counter",
    NodeName = "TestTurnCounter", DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName)]
public sealed partial class TestTurnCounter : Label, INodeAttachmentSetup
{
    public void Setup(Node parent, Node node) { Text = "Turn"; }
}
```

取回：`TryGetAttached<NCombatUi, TestCombatUiBadge>(combatUi, "combat_ui_badge", out var badge)`（父节点 ready 才真正挂载）。
`NodeAttachmentOptions`：`Name`/`Order`/`DuplicatePolicy`/`AddMode`/`SetupTiming`/`ChildIndex`|`InsertBeforeName`|`InsertAfterName`（三选一）/`UniqueNameInOwner`/`IncludeDerivedParentTypes`。
