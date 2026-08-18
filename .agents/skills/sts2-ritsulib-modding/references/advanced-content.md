# 进阶内容：人物 / 怪物与遭遇 / 事件 / 先古之民 / 时间线 / 单例 / 组件 / 动画 / 计算动态变量 / 音频

> 目录：人物（池子/专属联动/模板/场景） → 角色动画 → 怪物 → 遭遇 → 事件 → 先古之民 → 时间线与解锁 → 单例 → 组件(Capability) → 计算动态变量 → FMOD 音频

## 新人物

人物需要大量资源；缺的直接注释掉即回退原版（`CharacterAssetProfiles.Merge(CharacterAssetProfiles.Ironclad(), new(...))` 以铁甲战士为底）。

1. **先建三个池**（见 content-creation.md 卡池一节）：`TestCardPool : TypeListCardPoolModel`、`TestRelicPool : TypeListRelicPoolModel`、`TestPotionPool : TypeListPotionPoolModel`，并把所有内容注册目标改到自己的池。

2. **人物类**：

```csharp
[RegisterCharacter]
public class TestCharacter : ModCharacterTemplate<TestCardPool, TestRelicPool, TestPotionPool>
{
    public override Color NameColor => new(0.5f, 0.5f, 1f);
    public override Color EnergyLabelOutlineColor => new(0.5f, 0.5f, 1f);
    public override Color MapDrawingColor => new(0.5f, 0.5f, 1f);
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override int StartingHp => 80;
    public override int StartingGold => 99;

    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(),
        new(
            Scenes: new(
                VisualsPath: "res://Test/scenes/test_character.tscn",        // 战斗模型
                EnergyCounterPath: "res://Test/scenes/test_energy_counter.tscn",
                MerchantAnimPath: "res://Test/scenes/test_character_merchant.tscn",
                RestSiteAnimPath: "res://Test/scenes/test_character_rest_site.tscn"
            ),
            Ui: new(
                IconTexturePath: "res://icon.svg",                           // 头像（图片）
                IconPath: "res://Test/scenes/test_icon.tscn",                // 左上角头像（场景）
                CharacterSelectBgPath: "res://Test/scenes/test_bg.tscn",     // 选人背景（Control，建议 2560x1200）
                CharacterSelectIconPath: "res://Test/images/char_select_test.png",
                CharacterSelectLockedIconPath: "res://Test/images/char_select_test_locked.png",
                // CharacterSelectTransitionPath: "...tres",
                MapMarkerPath: "res://icon.svg"
            ),
            Vfx: new( /* TrailPath 卡牌拖尾 */ ),
            Audio: new( /* AttackSfx / CastSfx / DeathSfx / CharacterSelectSfx: "event:/sfx/..." */ ),
            Multiplayer: new( /* ArmPointingTexturePath / ArmRock/Paper/Scissors */ )
            // Spine / VisualCues（帧动画静态图，见角色动画）/ WorldProceduralVisuals
            // VanillaRelicVisualOverrides: [ new(CharacterOwnedVanillaRelicModelId.YummyCookie, new("res://icon.svg")) ]
        ));

    public override float AttackAnimDelay => 0f;   // 对齐攻击动画
    public override float CastAnimDelay => 0f;
    public override bool RequiresEpochAndTimeline => false;  // 不做时间线小故事时加这句

    // 自动转换人物场景，免手动挂脚本，复制即可
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);

    // 初始卡组/遗物也可写在这里（或在内容类上用注解）：
    // protected override IEnumerable<StartingDeckEntry> StartingDeckEntries => [new(typeof(TestCard), 5)];
    // protected override IEnumerable<Type> StartingRelicTypes => [typeof(Akabeko)];

    public override List<string> GetArchitectAttackVfx() => [
        "vfx/vfx_attack_blunt", "vfx/vfx_heavy_blunt", "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact", "vfx/vfx_rock_shatter"
    ];
}
```

3. **战斗模型场景**（怪物同理）：`Node2D` 根，子节点 `Visuals`(Node2D)、`Bounds`(Control)、`IntentPos`/`CenterPos`/`TalkPos`(Marker2D)，**全部右键勾选"作为唯一名称访问"（出现 `%`），名字不要改**。`Bounds` 是 hitbox。人物显示在 x 轴上方。3D 模型：`Visuals→SubViewportContainer→SubViewport(Transparent=on)→Camera3D+模型`。

4. **人物专属联动**（`Entry.Init` 里注册）：
   - 古老牙齿（初始卡→先古升级）：`RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<TestCard, Shiv>();`
   - 欧洛巴斯之触（初始遗物升级）：`RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<TestRelic, Akabeko>();`
   - 尘封魔典：自动从池中先古卡选（去掉古老牙齿那张），多做一张先古卡即可。
   - 美味饼干图标：`VanillaRelicVisualOverrides`。
   - 海玻璃：`relics.json` 加 `SEA_GLASS.{人物ID}.title`。
   - 色彩哲学家事件：卡池实现 `IModColorfulPhilosophersCardPool`，`events.json` 加 `COLORFUL_PHILOSOPHERS.pages.INITIAL.options.{EnergyColorName大写}.title/.description`。

本地化键示例：`SEA_GLASS.TEST_CHARACTER_TEST_CHARACTER.title`。

## 角色动画

- **Spine**：把 `Visuals` 节点改成 `SpineSprite` 类型（不改名），模型需含 `idle_loop`/`attack`/`cast`/`hurt`/`die` 动画。无 SpineSprite 类型需先装 Spine Godot Extension（见教程 Visuals 章节）。
- **帧动画/静态图**：用 `AssetProfile` 的 `VisualCues`（`VisualCueSet` 状态机）。
- **动画状态机**：`CreatureAnimator` / `AnimationStateMachine` 支持给原版挂额外动画名。详见教程 `RitsuLib/01 - 添加基础内容/17 - 角色动画`。

## 怪物

```csharp
[RegisterMonster]
public class TestMonster : ModMonsterTemplate
{
    // 进阶缩放：进阶8+（ToughEnemies）血量提高
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 20, 15);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 30, 20);
    private int BasicDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://Test/scenes/test_monster.tscn"); // 场景结构同人物（Visuals/Bounds/IntentPos/CenterPos/TalkPos 唯一名）

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    public override async Task AfterAddedToRoom()  // 战斗开始上 buff
        => await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var basicAttack = new MoveState("BASIC_ATTACK", BasicAttackMove,
            new SingleAttackIntent(BasicDamage), new DefendIntent()); // 意图可多个，全部展示
        var heavyAttack = new MoveState("HEAVY_ATTACK",
            async targets => await DamageCmd.Attack(HeavyDamage).FromMonster(this)
                .WithAttackerFx(null, AttackSfx).WithHitFx("vfx/vfx_attack_blunt").Execute(null),
            new SingleAttackIntent(HeavyDamage));
        // 复杂逻辑用 RandomBranchState / ConditionalBranchState
        basicAttack.FollowUpState = heavyAttack;
        heavyAttack.FollowUpState = basicAttack;
        return new MonsterMoveStateMachine([basicAttack, heavyAttack], basicAttack);
    }

    private async Task BasicAttackMove(IReadOnlyList<Creature> targets)
    {
        TalkCmd.Play(L10NMonsterLookup("TEST_MONSTER_TEST_MONSTER.moves.BASIC_ATTACK.banter"), Creature, VfxColor.Blue);
        await DamageCmd.Attack(BasicDamage).FromMonster(this)
            // .WithAttackerAnim("Attack", 0.5f)
            .WithAttackerFx(null, AttackSfx).WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, 8, ValueProp.Move, null);
    }
}
```

本地化 `monsters.json`：`{ID}.name`、`{ID}.moves.{状态ID}.title`、`{ID}.moves.{状态ID}.banter`（对话）。

## 遭遇（让怪物出现在对局）

```csharp
[RegisterActEncounter(typeof(Glory))]     // 指定章节；自定义条件改用共享注册+重写
public class TestEncounter : ModEncounterTemplate
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<TestMonster>()];
    public override RoomType RoomType => RoomType.Monster;
    // public override bool IsWeak => false;              // 是否弱怪池
    // public override bool IsValidForAct(ActModel act) => act is Overgrowth;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => [
        (ModelDb.Monster<TestMonster>().ToMutable(), null)  // 必须 ToMutable()；槽位 null=自动分配
    ];
}
```

多怪物：重写 `AssetProfile`（`EncounterScenePath`）+ `Slots`（槽位名列表）+ `GenerateMonsters` 指定槽位名；场景用 `Marker2D` 标位置，可用 `GetCameraScaling()` 调缩放。
本地化 `encounters.json`（**注意 ID 用连字符**）：`TEST-TEST_ENCOUNTER.title` / `.loss`（`{character}`/`{encounter}` 占位）。

## 事件

```csharp
[RegisterActEvent(typeof(Glory))]     // 或 [RegisterSharedEvent] + 重写 IsAllowed
public sealed class TestEvent : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://images/events/x.png");

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Unblockable | ValueProp.Unpowered),
        new GoldVar(60)
    ];

    public override bool IsAllowed(IRunState runState)
        => runState.Players.All(p => p.Gold >= DynamicVars.Gold.BaseValue); // 出现条件

    protected override Task BeforeEventStarted(bool isPreFinished) { Owner!.CanRemovePotions = false; return Task.CompletedTask; }
    protected override void OnEventFinished() => Owner!.CanRemovePotions = true;

    // 初始页选项。textKey = Id.Entry + ".pages." + page + ".options." + Slugify(方法名)
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => [
        new EventOption(this, TakeDamage, InitialOptionKey("TAKE_DAMAGE")),
        new EventOption(this, LoseGold, InitialOptionKey("LOSE_GOLD")),
    ];

    private async Task TakeDamage()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars.Damage, null, null);
        ChooseRewardTypePage();
    }

    private void ChooseRewardTypePage()   // 第二页
        => SetEventState(L10NLookup($"{Id.Entry}.pages.CHOOSE_TYPE.description"), [
            new EventOption(this, ChoosePotions, ModOptionKey("CHOOSE_TYPE", "CHOOSE_POTIONS")),
            new EventOption(this, ChooseCards, ModOptionKey("CHOOSE_TYPE", "CHOOSE_CARDS")),
        ]);

    private async Task ChoosePotions()
    {
        await RewardsCmd.OfferCustom(Owner!, [new PotionReward(Owner!)]);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.POTIONS_CHOSEN.description"));
    }
}
```

战斗事件：加 `public override EventLayoutType LayoutType => EventLayoutType.Combat;` + `public override EncounterModel CanonicalEncounter => ModelDb.Encounter<TestEncounter>();`，选项里 `EnterCombatWithoutExitingEvent<TestEncounter>(额外奖励, shouldResumeAfterCombat: false)`。
本地化 `events.json`：`{ID}.title`、`{ID}.pages.INITIAL.description`、`{ID}.pages.INITIAL.options.{KEY}.title/.description`、各分页 `.description`。

## 先古之民（Ancient）

```csharp
[RegisterActAncient(typeof(Glory))]   // 或 [RegisterSharedAncient] + 重写 IsAllowed/IsValidForAct
public class TestAncient : ModAncientEventTemplate
{
    public override Color ButtonColor => new(0.12f, 0.2f, 0.8f, 0.5f);
    public override Color DialogueColor => new(0.12f, 0.2f, 0.8f);

    public override EventAssetProfile AssetProfile => new(BackgroundScenePath: "res://Test/scenes/test_ancient.tscn");
    public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile => new(
        MapIconPath: "res://icon.svg", MapIconOutlinePath: "res://icon.svg",
        RunHistoryIconPath: "res://icon.svg", RunHistoryIconOutlinePath: "res://icon.svg");

    private IReadOnlyList<EventOption> Pool1 => [CreateModRelicOption<Akabeko>(), CreateModRelicOption<Anchor>()];
    private WeightedList<EventOption> Pool3 => new() { { CreateModRelicOption<YummyCookie>(), 2 }, { CreateModRelicOption<WingCharm>(), 1 } };

    public override IEnumerable<EventOption> AllPossibleOptions => [.. Pool1, .. Pool3];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [Rng.NextItem(Pool1)!, Pool3.GetRandom(Rng)];
}
```

本地化 `ancients.json`，ID 为 `{modId}_EVENT_{类名大写SNAKE}`；对话键规则（先古对话章节）：
`{ID}.title` / `.epithet`，台词 `{ID}.talk.{触发条件}.{页-句}.ancient|char|next`：
- 触发条件：`firstVisitEver`（首次）、`ANY`（任意）、或角色 ID（`IRONCLAD`/`SILENT`/`DEFECT`/`NECROBINDER`/`REGENT`）
- 句序号后缀 `r` 表示随机池（如 `1-0r`）；`.next` 是"继续"按钮文本
- 场景 `test_ancient.tscn` 是自由搭建的 `Control`（可加 Shader/粒子）。

## 时间线与解锁（Epoch/Story）

时期类型与解锁条件：拥有角色 / 打一局 `[UnlockEpochAfterRunAs]` / 赢一局 `[UnlockEpochAfterWinAs]` / 过一二三幕（按 ID `ActEpochKey`） / 累计 15 精英 `[UnlockEpochAfterEliteVictories]` / 累计 15 Boss `[UnlockEpochAfterBossVictories]` / 进阶 1 胜利 `[UnlockEpochAfterAscensionOneWin]`。

```csharp
[RegisterStory]
public class TestStory : ModStoryTemplate
{
    protected override string StoryKey => "test";
    internal static string ActEpochKey(int actNum) =>
        ModContentRegistry.GetFixedPublicEntry(Entry.ModId, typeof(TestCharacter)) + $"_{actNum + 1}_EPOCH";
}

[RegisterEpoch]
[RegisterStoryEpoch(typeof(TestStory), Order = 0)]
[AutoTimelineSlotBeforeColumn(EpochEra.Seeds0)]      // 时间线自动定位；另有 [AutoTimelineSlot] / After/In
[RequireAllCardsInPool(typeof(TestCardPool))]         // 需池内卡全发现才解锁
public class TestEpoch : CharacterUnlockEpochTemplate<TestCharacter>
{
    public override string Id => "TEST_CHARACTER_EPOCH";
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: "res://icon.svg", BigPortraitPath: "res://icon.svg");
    protected override IEnumerable<Type> ExpansionEpochTypes => [typeof(TestCardEpoch), /* ... */];
}

[RegisterEpoch]
[RegisterStoryEpoch(typeof(TestStory), Order = 1)]
[AutoTimelineSlot(EpochEra.Seeds0)]
[RegisterEpochCards(typeof(TestCard), typeof(TestCard2))]   // 该时期解锁的卡；另有 [RegisterEpochRelicsFromPool]
public class TestCardEpoch : PackDeclaredCardUnlockEpochTemplate { /* Id + AssetProfile */ }
```

相关模板基类：`PackDeclaredCardUnlockEpochTemplate` / `PackDeclaredRelicUnlockEpochTemplate` 等。ContentPack 写法见 infrastructure.md（`.Epoch<>()` `.StoryEpoch<>()` `.RequireEpoch<>()` `.UnlockEpochAfterWinAs<>()`）。

## 单例（SingletonModel，全局钩子）

```csharp
[RegisterSingleton]
public class TestSingleton : HookedSingletonModel
{
    public TestSingleton() : base(HookType.Combat) { }   // Combat / Run / None
    // 重写 AbstractModel 的虚函数监听游戏事件，接口与遗物等一致
    // public override async Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw) {...}
}
```

用途：全局影响（如自定义关键词效果）。可反编译原版 `Hook.cs` 看全部接口。

## 组件（ModelCapability，模块化行为注入）

类似塔1 cardmodifier，可挂任意 `AbstractModel`，可多个共存：

```csharp
[RegisterModelCapability]
public class DrawPowerCapability : CardCapability   // 卡牌组件基类
{
    protected override void OnAttach(CardModel model) { }
    protected override void OnDetach(CardModel model) { }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Owner != null && card == Owner)
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Owner.Creature, 1, Owner.Owner.Creature, null);
    }
}
```

挂载：`card.GetOrCreateCapability<DrawPowerCapability>()`。组件 ID：`{MODID}_MODEL_CAPABILITY_{类名大写SNAKE}`。
手动注册：`RitsuLibFramework.RegisterModelCapability<T>(ModId)` 或 `GetContentRegistry(ModId).RegisterModelCapability<T>()`。

基类：`CardCapability`（含 `OnOwnerCardUpgraded` 等）、`CardPlayCapability`（只处理自己所属牌打出）、`OneShotCardPlayCapability`（打出一次自移除）、`OrbCapability`、`RelicCapability`/`PotionCapability` 等、`CharacterCapability`（不收原版 hook）、`OwnerHookCapability<TModel>`、`UntilCombatEndCapability<TModel>`、`TurnLimitedCapability<TModel>`（剩余回合并持久化）。自定义 owner 类型：继承 `ModelCapability` / `ModelCapability<TModel>`。

贡献者接口（向 owner 注入内容）：如 `ICardDescriptionContributor` 在卡牌描述底部追加文本：

```csharp
public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) => [
    new CardDescriptionFragment(
        new LocString("cards", $"{Id.Entry}.exhaustHealDescription"),
        CardDescriptionFragmentPlacement.AfterBase)
];
```

（本地化写到 LocString 指定的表，如 `cards.json`：`TEST_MODELCAPABILITY_XXX.exhaustHealDescription`。）

## 计算动态变量（ComputedDynamicVar）

静态 var 不够用（依赖目标/升级/预览/上下文）时用，委托实时算：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [
    // 简单形式：(card, target) => 值
    ModCardVars.Computed("TestValue", 3,
        (card, target) => card.DynamicVars["TestValue"].BaseValue + (target?.GetPowerAmount<StrengthPower>() ?? 0)),
];

// 上下文形式（factory 是第二个参数，baseValue 放最后；建议 static 避免捕获卡牌实例）
ModCardVars.Computed("TestCtx", static ctx =>
{
    var heat = ctx.GetCardIntOrDefault("TestHeat");
    var targetBonus = ctx.HasTarget && ctx.IsInCombat ? ResolveTargetBonus(ctx.CombatState, ctx.Target) : 0m;
    return ctx.BaseValue + heat + targetBonus;
}, baseValue: 6);
```

- 基础值永远通过 `ctx.BaseValue` / `card.DynamicVars["x"].BaseValue` 读，升级用 `UpgradeValueBy` 改 BaseValue。
- `ComputedDynamicVarContext` 关键成员：状态 `IsCurrentValue`/`IsPreview`/`IsUpgradePreview`；卡牌 `Card`/`IsUpgraded`/`IsMutableCard`；作用域 `Player`/`SourceCreature`/`CombatState`/`IsInCombat`；变量读取 `GetCardIntOrDefault`/`EvaluateCardVarOrDefault`（带递归保护）。
- 包装：`ComputedDamage` / `ComputedOstyDamage` / `ComputedBlock`；能量/辉星/能力图标数量也有对应工厂。完整说明见教程 `RitsuLib/01 - 添加基础内容/19 - 计算动态变量`。

## FMOD 音频

方式一（bank）：FMOD Studio 工程建好 bank/event 并构建导出后：

```csharp
using STS2RitsuLib.Audio;

FmodStudioDeferredBankRegistration.RegisterBank("res://Test/audios/Test.bank");
FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings("res://Test/audios/GUIDs.txt");
```

使用：人物 `Audio: new(CharacterSelectSfx: "event:/sfx/xxx")`；攻击 `DamageCmd.Attack(...).WithHitFx(sfx: "event:/sfx/sword_slash")`；直接播 `SfxCmd.Play("event:/sfx/block_gain")`。

方式二（wav/ogg/mp3 文件）：

```csharp
FmodStudioStreamingFiles.TryPreloadAsSound("res://Test/audios/waveform.ogg");  // 可选预载
FmodStudioStreamingFiles.TryPlaySoundFile("res://Test/audios/waveform.ogg");   // 播放
```
