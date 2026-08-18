# 基础设施：注册 / 补丁 / 生命周期 / 存档 / 设置页 / 热键 / 更新检查 / 联动 / 网络

> 目录：三种注册方式 → 补丁系统 → 生命周期事件 → 数据保存三级 → 设置页 → 运行时热键 → 更新检查 → 数据遥测 → 模组联动 → 网络联机 → 常用工具 → BaseLib→RitsuLib 迁移对照

## 三种注册方式

**方式一：注解式**（最常用，内容和注册写在一起）：

```csharp
[RegisterCard(typeof(TestCardPool))]
[RegisterCharacterStarterCard(typeof(TestCharacter), 4)]
public sealed class BlazingStrike : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
```

**方式二：ContentPack**（集中批量注册）：

```csharp
RitsuLibFramework.CreateContentPack(ModId)
    .Character<TestCharacter>(character => character
        .AddStartingRelic<TestStarterRelic>(1)
        .AddStartingCard<BlazingStrike>(4))
    .Card<TestCardPool, BlazingStrike>()
    .Relic<TestRelicPool, TestStarterRelic>()
    .Power<TestPower>()
    .ActEncounter<TestAct, TestEncounter>()
    .Story<TestStory>()
    .Epoch<TestEpoch>()
    .StoryEpoch<TestStory, TestEpoch>()
    .RequireEpoch<TestRareCard, TestEpoch>()
    .UnlockEpochAfterWinAs<TestCharacter, TestEpoch>()
    .Apply();   // Apply() 只在最后调一次；被引用的模型先注册
```

**方式三：直接注册器**：

```csharp
var content = RitsuLibFramework.GetContentRegistry(ModId);   // 或 ModContentRegistry.For(ModId)
content.RegisterCard<TestCardPool, BlazingStrike>();

RitsuLibFramework.GetKeywordRegistry(ModId).RegisterCardKeywordOwnedByLocNamespace(
    "burning", iconPath: "res://Test/images/keywords/burning.png",
    cardDescriptionPlacement: ModKeywordCardDescriptionPlacement.BeforeCardDescription);

RitsuLibFramework.GetCardTagRegistry(ModId).RegisterOwned("heavy");
```

常用 `RitsuLibFramework` 入口：`CreateContentPack` / `CreatePatcher` / `SubscribeLifecycle<T>` / `BeginModDataRegistration` + `GetDataStore` / `GetRunSavedDataStore` / `RegisterModSettings` / `CreateLogger` / `EnsureGodotScriptsRegistered`。

## 补丁系统（Harmony 封装）

原始 Harmony 仍可用；中大型项目建议用 RitsuLib 封装（统一声明、注册、失败处理）：

```csharp
using STS2RitsuLib.Patching.Models;

public class LogReleaseGamePatch : IPatchMethod
{
    public static string PatchId => "test_log_release_game";      // 唯一防撞
    public static string Description => "Print IsReleaseGame";
    public static bool IsCritical => false;                        // true=失败会报错

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NGame), nameof(NGame.IsReleaseGame))];
        // new(typeof(NGame), "SomeOptionalMethod", ignoreIfMissing: true)  // 方法不存在则跳过
        // 可返回多个目标

    public static void Postfix(ref bool __result) { /* ... */ }    // Prefix/Postfix/Transpiler
}
```

```csharp
using STS2RitsuLib.Patching.Core;   // RegisterPatch<T> 扩展

var patcher = RitsuLibFramework.CreatePatcher(ModId, "core-patches");  // 每个逻辑区域一个 patcher
patcher.RegisterPatch<LogReleaseGamePatch>();
patcher.RegisterPatches<MyPatchSet>();   // 批量：class MyPatchSet : IModPatches { static void AddTo(ModPatcher p) {...} }
if (!patcher.PatchAll())                  // 登记完统一打一次
    throw new InvalidOperationException("Critical patches failed.");
// 或 RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod);
```

动态补丁（目标运行时发现）：`new DynamicPatchBuilder("my_dynamic").AddMethod(targetType, methodName, postfix: DynamicPatchBuilder.FromMethod(typeof(T), nameof(T.Postfix)), isCritical: false, description: "...")`，然后 `patcher.ApplyDynamic(builder, rollbackOnCriticalFailure: false)`。

Harmony 要点速查（详见教程 Basics/10）：`[HarmonyPatch(typeof(T), nameof(T.M))]`；`Prefix` 返回 false 跳过原方法；注入参数 `__instance`（this）、`__result`（返回值，改要 ref）、`___字段`（私有字段）、`__0/__1`（按位置）、同名参数；特殊方法用 `MethodType.Getter/Constructor/Async`；async patch 的 `__instance` 是状态机（`Traverse.Create(__instance).Field("<>4__this")` 取原对象）；ref 参数重载要写 `ArgumentType.Ref`。

## 生命周期事件

```csharp
var sub = RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>(evt => { /* ... */ });
// sub.Dispose() 取消；第二参数 replayCurrentState: true 可立刻补发当前状态

// 或接口式：class MyObserver : ILifecycleObserver { public void OnEvent(IFrameworkLifecycleEvent evt) {...} }
// RitsuLibFramework.SubscribeLifecycle(new MyLifecycleObserver());
```

常用事件（全量在 RitsuLib 源码 `*LifecycleContracts.cs`）：

| 分类 | 事件 |
| --- | --- |
| 框架 | `FrameworkInitializedEvent`、`ProfileServicesInitializedEvent` |
| 引导 | `EssentialInitializationStarting/CompletedEvent`、`ContentRegistrationClosedEvent`（**此后不要再注册内容**）、`ModelRegistryInitializedEvent`、`ModelIdsInitializedEvent`（此后可用 `ModelDb.GetId<T>()`）、`GameTreeEnteredEvent`、`GameReadyEvent` |
| 局内 | `RunStartedEvent`、`RunLoadedEvent`、`RunEndedEvent(IsVictory, IsAbandoned)` |
| 房间/章节 | `RoomEntering/Entered/ExitedEvent`、`ActEntering/EnteredEvent`、`RewardsScreenContinuingEvent` |
| 战斗 | `CombatStarting/Victory/EndedEvent`、`SideTurnStarting/StartedEvent`、`CardPlaying/PlayedEvent`、`CardDrawn/Discarded/ExhaustedEvent`、`CardMovedBetweenPilesEvent`、`BeforeFlushEvent`、`CardsFlushedEvent`、`CreatureDying/DiedEvent` |
| 奖励 | `GoldGained/LostEvent`、`RelicObtained/RemovedEvent`、`PotionProcured/DiscardedEvent`、`RewardTakenEvent` |
| 解锁 | `EpochObtained/RevealedEvent`、`UnlockIncrementedEvent` |
| 存档 | `ProfileSwitching/SwitchedEvent`、`RunSaving/SavedEvent`、`ProgressSaving/SavedEvent`、`ProfileDataReadyEvent`（**可读写 ModDataStore**）、`ProfileDataChanged/InvalidatedEvent` |

## 数据保存（三级）

**1. 挂载对象局内保存**（卡牌/遗物等实例状态，随存档）：

```csharp
public static readonly SavedAttachedState<TestRelic, int> GameTurns = new("GameTurns", _ => 0);
// GameTurns[this]++;  读档无此值用默认 0
// 要显示在描述里需同时加 DynamicVar("GameTurns", GameTurns[this])
```

支持类型：`int`/`bool`/`string`/`ModelId`/枚举/`int[]`/枚举数组/`SerializableCard` 及其数组/List。
不需保存版：`AttachedState<TKey,TValue>`（ConditionalWeakTable，任意引用对象）。

**2. 一局全局数据**（RunSavedData，含大厅暂存与联机同步）：

```csharp
using (RitsuLibFramework.BeginModDataRegistration(ModId))
{
    var store = RitsuLibFramework.GetRunSavedDataStore(ModId);
    Challenge = store.Register("challenge", () => new ChallengeRunState(),   // 全局共享
        new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenNonDefault, SyncLobbyOnChange = true });
    Player = store.RegisterPerPlayer("player", () => new PlayerRunState(),   // 按玩家独立
        new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet, SyncLobbyOnChange = true });
}
```

读写：`Challenge.Get(runState)` / `Challenge.Modify(runState, data => ...)`；玩家级 `Player.Get(player)` / `Player.Modify(runState, netId, ...)`。
大厅暂存（RunState 未建立）：`Challenge.Lobby.Modify(lobby, ...)`，`SyncLobbyOnChange=true` 自动同步主机队友。
写入策略：`WhenSet`（显式改过才写，默认）/`WhenNonDefault`（与默认值不同才写，defaultFactory 要稳定）/`AlwaysWhenRegistered`。
迁移：`SchemaVersion = 2` + `Migrations = [new MyV1ToV2Migration()]`（`IMigration.FromVersion/ToVersion/Migrate(JsonObject)`）。
事件：`RunSavedDataLobbyStagingEvent`（大厅预览）、`RunSavedDataPreparingEvent`（快照导出前补值）。
**发布后绝不改注册 key**；加字段直接加属性。

**3. 全游戏持久化**（ModDataStore，解锁进度/统计/设置）：

```csharp
using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
{
    RitsuLibFramework.GetDataStore(Entry.ModId).Register<ModProgressData>(
        key: "mod_progress", fileName: "test_mod_progress.json",
        scope: SaveScope.Global,              // Global 全存档共享 / Profile 按存档位隔离
        defaultFactory: () => new ModProgressData(),
        autoCreateIfMissing: true);
}
```

```csharp
var store = RitsuLibFramework.GetDataStore(Entry.ModId);
var progress = store.Get<ModProgressData>("mod_progress");
store.Modify<ModProgressData>("mod_progress", data => data.GlobalMonstersKilled += 1);
store.Save("mod_progress");          // ⚠️ 必须显式 Save 才写盘
store.HasExistingData("mod_progress");
```

## 设置页（Mod 配置 UI）

**方法一：代码流式（推荐）**——先注册 DataStore 再绑控件：

```csharp
private static readonly ModSettingsValueBinding<TestSettings, bool> EnabledBinding = new(
    Entry.ModId, "settings", SaveScope.Profile,
    static s => s.Enabled, static (s, v) => s.Enabled = v);

RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
    .WithTitle(ModSettingsText.Literal("Test"))
    .WithModDisplayName(ModSettingsText.Literal("Test Mod"))
    .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
    .AddSection("general", section => section
        .WithTitle(ModSettingsText.Literal("通用"))
        .AddToggle("enabled", ModSettingsText.Literal("启用"), EnabledBinding)
        .AddIntSlider("volume", ModSettingsText.Literal("音量"), VolumeBinding, 0, 100, 5,
            valueFormatter: static v => $"{v}%")
        .AddChoice("layout", ModSettingsText.Literal("布局"), LayoutBinding,
            [new("compact", ModSettingsText.Literal("紧凑"))],
            presentation: ModSettingsChoicePresentation.Dropdown)
        .AddButton("reset", ModSettingsText.Literal("音量"), ModSettingsText.Literal("重置"),
            host => { VolumeBinding.Write(80); host.MarkDirty(VolumeBinding); host.RequestRefresh(); },
            ModSettingsButtonTone.Accent)));
```

代码读写：`EnabledBinding.Read()` / `.Write(v)` / `.Save()`。临时值用 `InMemoryModSettingsValueBinding<T>`；多控件编同一对象用 `ProjectedModSettingsValueBinding` 投影。

**方法二：反射注册**（简单字段少）：

```csharp
[ModSettingsPage(Entry.ModId)]
[ModSettingsSection("general", Title = "通用")]
public static class TestReflectedSettings
{
    [ModSettingsToggle("enabled", "general")]
    [ModSettingsBinding(Source = ModSettingsReflectionBindingSource.Global)]  // Global/Profile/InMemory
    public static bool Enabled { get; set; } = true;

    [ModSettingsIntSlider("volume", "general", 0, 100, 5)]
    [ModSettingsBinding(Source = ModSettingsReflectionBindingSource.Global)]
    public static int Volume { get; set; } = 80;

    [ModSettingsButton("reset", "general", ButtonText = "重置音量")]
    public static void ResetVolume() => Volume = 80;   // 按钮需 static 方法
}
// Init: RitsuLibFramework.RegisterModSettingsReflectionProvider<TestReflectedSettings>();
```

控件注解：`ModSettingsToggle/Slider/IntSlider/String/MultilineString/Color/Choice/KeyBinding/Button`。

**方法三：Schema 注册**（跨框架、不想强依赖 RitsuLib 管理）：静态类实现 `CreateRitsuLibSettingsSchema()`（返回 json 路径或 Dictionary）、`GetRitsuLibSettingValue(key)`、`SetRitsuLibSettingValue(key, value)`、`SaveRitsuLibSettings()`、`InvokeRitsuLibSettingAction(key)`，并在 .csproj 加 `<AssemblyMetadata Include="RitsuLib.ModSettingsInterop.ProviderType" Value="命名空间.类名" />`。schema entry type：`toggle`/`slider`/`int-slider`/`choice`/`string`/`multiline-string`/`color`/`key-binding`/`button`/`header`/`paragraph`/`info-card`/`subpage`。

文本 `ModSettingsText` 四形式：`Literal("...")`、`LocString(table, key, fallback)`、`I18N(...)`、`Dynamic(() => $"...")`。

## 运行时热键

```csharp
using STS2RitsuLib.RuntimeInput;

_reloadHotkey = RuntimeHotkeyService.Register("Ctrl+Shift+R",   // 或数组 ["F5", "Ctrl+Shift+R"]
    () => Logger.Info("热键已触发！"),
    new RuntimeHotkeyOptions
    {
        Id = "my_mod_reload", DisplayName = "重新加载配置",
        Description = "...", Category = "My Mod",
        // MarkInputHandled / SuppressWhenTextInputFocused(默认true) / SuppressWhenDevConsoleVisible(默认true)
    });

_reloadHotkey?.TryRebind("Ctrl+Alt+R", out var normalized);   // 运行时改键
_reloadHotkey?.Unregister();
RuntimeHotkeyService.GetRegisteredHotkeyDetails();             // 查询全部
```

绑定字符串：`[修饰键+]主键`，修饰键 `Ctrl`/`Alt`/`Shift`/`Meta`，不区分大小写。

## 更新检查（只提示，不下载）

```csharp
RitsuLibFramework.RegisterModUpdateCheck(new()
{
    ModId = Entry.ModId, DisplayName = "Test Mod", CurrentVersion = "1.2.0",
    ManifestUri = new Uri("https://cdn.example.com/test-mod/update.json"),   // 必须 https
    ReleasePageUri = new Uri("https://example.com/test-mod/releases"),
});
```

manifest json：`{ "schema": "ritsulib.update.v1", "latest_version": "1.2.3", "release_page_url": "...", "localized": { "zhs": { "title": "...", "message": "...{latest_version}..." } } }`（占位符 `{display_name}` `{current_version}` `{latest_version}`）。
手动检查：`await RitsuLibFramework.CheckForModUpdateAsync(...)` 返回 `ModUpdateCheckStatus.UpdateAvailable/UpToDate/InvalidData/RequestFailed`。
无资源站点可用 GitHub Pages 搭（见教程 `03 - 模组工具/04 - 更新检查`）。

## 数据遥测

注册申请方后可发送自定义事件、捕获异常、自动上传一局数据，支持 contribution provider；自建后端可用 PostHog + Cloudflare 代理（见教程 `03 - 模组工具/09 - 数据遥测`）。

## 模组联动（可选依赖，弱引用调用其他 mod）

```csharp
using STS2RitsuLib.Interop;

[ModInterop("target-mod", "TargetMod.Api.PublicApi")]   // 目标 modid + 完整类名
public static class TargetModApiInterop
{
    public static bool IsReady => false;                 // 目标不在时的默认实现
    public static int GetBonusLevel(string playerId) => 0;
    public static void GrantBadge(string badgeId) => throw new NotSupportedException("Target mod is not loaded.");
}

// 名字不同/包装实例类：
[ModInterop("target-mod")]
public static class TargetCatalogInterop
{
    [InteropTarget("TargetMod.Api.Catalog", "FindById")]
    public static EntryRef Find(string id) => throw new NotSupportedException();

    [InteropTarget("TargetMod.Api.Entry")]
    public sealed class EntryRef : InteropClassWrapper { public EntryRef(string id) { } /* ... */ }
}
```

调用：`if (TargetModApiInterop.IsReady) { ... }`，目标不存在当正常分支处理。需已 `RegisterModAssembly`。
调用任意 CLR 程序集用 `[AssemblyInterop("Namespace.Type, AssemblyName")]`（更推荐），类型名带逗号自动走 AssemblyInterop。

## 网络与联机

原版：动作队列同步（ActionQueueSynchronizer）。RitsuLib 两套工具：

- **Sidecar（消息通信）**：定义消息 + 描述符 + 订阅接收 + 发送，适合皮肤同步等自定义消息。
- **ManagedNetAction（动作队列）**：定义描述符 + 执行逻辑 + 注册与请求，适合走原版动作队列的同步效果。

选型与代码见教程 `02 - 玩法基底/14 - 网络与联机`。本地联机测试用 `--fastmp` 启动参数（见 setup-and-project.md）。

## 常用工具

- `DynamicEnumValueRegistry<CardType>.For(ModId).RegisterOwned("FIELD").Value` —— 安全扩枚举（匹配逻辑/素材自理）。
- `DynamicEnumValueMinter<TEnum>`（32 位枚举）：`Tags.Mint("test:echo_card")` / `Tags.IsDynamic(tag)`。
- `WeightedList<T>`：带权列表，`GetRandom(rng)` / `GetRandom(rng, remove: true)` 不放回；元素实现 `IWeightedValue` 自动读权重；空列表用 `TryGetRandom`。
- `MaterialUtils`：`CreateReplaceHueShaderMaterial(r,g,b)`（原版卡框换色调，推荐）、`CreateHsvShaderMaterial`、`CreateUnmodulatedHsvShaderMaterial`（自定义卡框）、`CreateDoomBarShaderMaterial` 等灾厄血条材质。
- `HoverTipHelper.AddTipToOwner(owner, title, text)` / `AddCardTipsToOwner(owner, cards)` —— 往现有悬浮提示组追加（返回 false=无活动提示组，可忽略）。
- 未单独成章的公开 API（RitsuLib README 列出）：模型复制监听、卡牌转换监听、全局治疗/出牌/攻击命中钩子（`RegisterHealHookListener` 等）、`RegisterAncientOption<T>`、`GetModRunRng()/GetModPlayerRng()`、`RegisterRelicVisibilityRule`、独立本地化 `CreateLocalization()`、`[RegisterSmartFormatter]`、`[RegisterDefaultModelCapability]`、`[RegisterMutuallyExclusiveModifierGroup]` 等——需要时查 RitsuLib 源码/官方文档。

## BaseLib → RitsuLib 迁移对照

ID：BaseLib `{命名空间首段大写}-{原ID}`（`TEST-TEST_CARD`）→ RitsuLib `{ModId}_{类别}_{原ID}`（`TEST_CARD_TEST_CARD`）。

| 说明 | BaseLib | RitsuLib |
| --- | --- | --- |
| 卡牌/遗物/药水注册 | `[Pool(typeof(XxxPool))]` | `[RegisterCard/Relic/Potion(typeof(XxxPool))]` |
| 事件/先古注册 | 不用 | `[RegisterActEvent/Ancient(typeof(X))]` 或 Shared 版 |
| 各内容基类 | `CustomCardModel` 等 | `ModCardTemplate` 等（人物 `ModCharacterTemplate<卡,遗物,药水>`） |
| 提示文本 | `ExtraHoverTips` | `AdditionalHoverTips` |
| 关键词声明 | `[CustomEnum("UNIQUE")]` | `[RegisterOwnedCardKeyword("Unique", ...)]` |
| 动态变量提示绑定 | `.WithTooltip` | `.WithSharedTooltip` |
| 卡池基类 | `CustomCardPoolModel` | `TypeListCardPoolModel` |
| 初始卡组/遗物 | `StartingDeck`/`StartingRelics` | `StartingDeckEntries`/`StartingRelicTypes` 或注解 |
| 人物场景路径 | `CustomVisualPath` | `CustomVisualsPath` |
| 遭遇章节 | `IsValidForAct` | `[RegisterActEncounter(typeof(Glory))]` |
| 事件选项 | `Option(Fn, "PAGE")` | `new EventOption(this, Fn, ModOptionKey("PAGE", "KEY"))` |
| 事件页文本 | `PageDescription("PAGE")` | `L10NLookup($"{Id.Entry}.pages.PAGE.description")` |
| 先古选项池 | `MakeOptionPools` | `AllPossibleOptions` + `GenerateInitialOptions()` |

完整对照见教程 `Migrations/02 - BaseLib 至 RitsuLib`。
