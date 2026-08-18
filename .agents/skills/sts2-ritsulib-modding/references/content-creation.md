# 基础内容创建：卡牌 / 遗物 / 药水 / 能力 / 充能球 / 附魔 / 卡池 / 关键词 / 变量与描述

> 目录：卡牌 → 卡图与基类 → 遗物 → 能力（含临时能力） → 药水 → 充能球 → 附魔 → 卡池 → 关键词与tag → 动态变量与提示文本 → 描述文本写法（BBCode/占位变量/formatter）
>
> 前提：`Entry.Init()` 已调用 `ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly)`，否则 `[RegisterXxx]` 注解不生效。

## 卡牌

```csharp
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MyMod.Scripts;

[RegisterCard(typeof(ColorlessCardPool))]          // 注册到无色卡池；自定义人物换成自己的卡池
// [RegisterCharacterStarterCard(typeof(MyCharacter), 5)] // 作为起始卡×5（不需要就删）
public class TestCard : ModCardTemplate             // 继承 ModCardTemplate，不是 CardModel
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://Test/images/cards/{GetType().Name}.png"
        // FramePath / PortraitBorderPath / BannerTexturePath 按需
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(12, ValueProp.Move)
    ];

    public TestCard() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary: true) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)                 // 测试版: .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}
```

- `ValueProp` 是 bitflag：`Move`（卡牌造成的）、`Unpowered`（不吃力量等修正）、`Unblockable`（不可格挡）、`SkipHurtAnim`，可组合 `ValueProp.Unblockable | ValueProp.Unpowered`。
- 卡图任意尺寸无需裁剪，官方普通卡 250x190、先古卡 250x351。
- 统一卡图路径可写抽象基类并在其上标 `[RegisterCard(typeof(MyCardPool), Inherit = true)]`（自动注册所有子类；`FramePath` 可按 `Type` switch 区分攻击/技能/能力卡框）。
- 想做某种效果：反编译找原版类似卡牌参考。

本地化 `{ModId}/localization/zhs/cards.json`：

```json
{
    "TEST_CARD_TEST_CARD.title": "测试卡牌",
    "TEST_CARD_TEST_CARD.description": "造成{Damage:diff()}点伤害。"
}
```

验证：控制台 `card TEST_CARD_TEST_CARD`（仅战斗中）；图鉴里显示 `???` 是没遇到过的正常表现。

## 遗物

```csharp
[RegisterRelic(typeof(SharedRelicPool))]
// [RegisterCharacterStarterRelic(typeof(MyCharacter))] // 起始遗物
public class TestRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://Test/images/relics/{GetType().Name}.png",        // 小图标 85x85
        IconOutlinePath: $"res://Test/images/relics/{GetType().Name}.png", // 轮廓 85x85
        BigIconPath: $"res://Test/images/relics/{GetType().Name}.png"      // 大图 256x256
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }
}
```

本地化 `relics.json`：`title` / `description` / `flavor`（趣文）。

## 能力（Power）

```csharp
[RegisterPower]
public class TestPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;              // Buff / Debuff
    public override PowerStackType StackType => PowerStackType.Counter; // Counter 可叠层 / Single 不可
    // public override PowerInstanceType InstanceType => PowerInstanceType.Instanced; // 每次新建实例（如炸弹）

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://Test/images/powers/test_power.png",    // 小图 64x64
        BigIconPath: "res://Test/images/powers/test_power.png"  // 大图 256x256
    );

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
```

本地化 `powers.json` 三件套：`title`、`description`（静态）、`smartDescription`（可用 `{Amount}` 显示层数；联机他人施加时可用 `remoteDescription`）。
施加：`PowerCmd.Apply<TestPower>(...)` 或控制台 `power TEST_POWER_TEST_POWER 1 0`。
钩子时机直接重写对应虚方法（反编译原版 `Hook.cs` 看全部接口）。

### 临时能力（回合结束自动消失，带来源显示）

```csharp
[RegisterPower]
public class TempPower : ModTemporaryAppliedPowerTemplate<TestCard, StrengthPower> // <来源, 代表的能力>
{
    public override PowerAssetProfile AssetProfile => new(IconPath: ..., BigIconPath: ...);
    // protected override bool IsPositive => false;
    // protected override bool UntilEndOfOtherSideTurn => false; // true=另一方回合结束过期
    // protected override int LastForXExtraTurns => 0;
}
```

多来源共享：抽象基类标 `[RegisterPower(Inherit = true)]` 继承 `ModTemporaryAppliedPowerTemplate<T, StrengthPower>`（`T : AbstractModel`），重写 `Description` 用 `LocString` 共享文本，子类标记不同来源。

## 药水

```csharp
[RegisterPotion(typeof(SharedPotionPool))]
public class TestPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly; // 仅战斗中
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<Soul>()];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://icon.svg",   // 本体；任何 Godot 可读成 Texture 的格式
        OutlinePath: "res://icon.svg"  // 轮廓
    );

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await Soul.CreateInHand(Owner, DynamicVars.Cards.IntValue, Owner.Creature.CombatState!);
    }
}
```

本地化 `potions.json`：`title` / `description`。

## 充能球（Orb）

```csharp
[RegisterOrb]
public class TestOrb : ModOrbTemplate
{
    public override decimal PassiveVal => ModifyOrbValue(1); // ModifyOrbValue 使其吃集中等修正
    public override decimal EvokeVal => ModifyOrbValue(2);

    // Contextual（平时被动值/预览激发时值，最常用）/ SinglePassive / SingleEvoke / Both（如黑暗球）
    public override ModOrbValueDisplayMode ValueDisplayMode => ModOrbValueDisplayMode.Contextual;
    public override Color DarkenedColor => new(0.1f, 0.2f, 0.5f);

    public override OrbAssetProfile AssetProfile => new(
        IconPath: "res://icon.svg",                              // 提示小图标
        VisualsScenePath: "res://Test/scenes/test_orb.tscn"      // 球的场景（Node2D + Sprite2D）
    );

    protected override Node2D? TryCreateOrbSprite() =>
        RitsuGodotNodeFactories.CreateFromScenePath<Node2D>(AssetProfile.VisualsScenePath!);

    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
        => await Passive(choiceContext, null);

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        Trigger();
        await CardPileCmd.Draw(choiceContext, PassiveVal, Owner);
    }

    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        PlayEvokeSfx();
        await CardPileCmd.Draw(playerChoiceContext, EvokeVal, Owner);
        return [Owner.Creature];
    }
}
```

本地化 `orbs.json`：`title` / `description` / `smartDescription`（可用 `{Passive}` `{Evoke}`）。生成：`await OrbCmd.Channel<TestOrb>(choiceContext, cardPlay.Card.Owner)`。

## 附魔（Enchantment）

```csharp
[RegisterEnchantment]
public class TestEnchantment : ModEnchantmentTemplate
{
    public override bool ShowAmount => true;        // 卡牌上显示数值
    public override bool HasExtraCardText => true;  // 附加额外卡牌描述

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    public override EnchantmentAssetProfile AssetProfile => new(IconPath: "res://icon.svg"); // 1:1，原版 64x64

    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.GainsBlock;

    protected override void OnEnchant() => Card.AddKeyword(CardKeyword.Retain);

    public override decimal EnchantBlockAdditive(decimal originalBlock) => Amount; // Amount=施加时指定层数

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Status == EnchantmentStatus.Normal)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Card.Owner);
            Status = EnchantmentStatus.Disabled;
        }
    }
}
```

本地化 `enchantments.json`：`title` / `extraCardText`（附加在卡牌上的文本） / `description`。
使用：控制台 `enchant TEST_ENCHANTMENT_TEST_ENCHANTMENT [层数] [手牌位置]`；代码 `CardCmd.Enchant<TestEnchantment>(card, 2m)`。

## 卡池

人物专属三池（卡牌/遗物/药水各一），在人物泛型里绑定：

```csharp
public class TestCardPool : TypeListCardPoolModel
{
    public override string Title => "test";              // 卡池ID，唯一防撞
    public override string EnergyColorName => "test";
    public override string? TextEnergyIconPath => "res://Test/images/energy_test.png";      // 24x24
    public override string? BigEnergyIconPath => "res://Test/images/energy_test_big.png";   // 74x74
    public override Color DeckEntryCardColor => new(0.5f, 0.5f, 1f);
    public override Color EnergyOutlineColor => new(0.5f, 0.5f, 1f);

    // 原版卡框换色调（推荐）；旧版本用 CreateRgbShaderMaterial；自定义卡框用 CreateUnmodulatedHsvShaderMaterial()
    private static readonly Material? _poolFrameMaterial = MaterialUtils.CreateReplaceHueShaderMaterial(0.5f, 0.5f, 1f);
    public override Material? PoolFrameMaterial => _poolFrameMaterial;

    public override bool IsColorless => false;           // 事件/状态等卡池为无色
}
```

`PoolFrameMaterial` 对池内所有卡生效，除非卡牌自指 `FrameMaterial`。
遗物池/药水池继承 `TypeListRelicPoolModel` / `TypeListPotionPoolModel`（只需图标路径和 `EnergyColorName`）。
**自建人物池后，把打击/防御等所有内容的注册目标改成自己的池子。**

通用共享池：池类上标 `[RegisterSharedCardPool]`（默认不进图鉴；要进图鉴在 Init 里 `ModContentRegistry.For(ModId).RegisterCardLibraryCompendiumSharedPoolFilter<MyPool>("id", "res://icon.svg")`，悬浮文本写在 `card_library.json`，键 `{MODID}_POOLFILTER_{ID大写}`）。

## 关键词与 tag

关键词（消耗/虚无一类属性）：

```csharp
[RegisterOwnedCardKeyword(nameof(Unique), IconPath = "res://icon.svg",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public class MyKeywords   // 注意：不能用 static 类
{
    public static readonly CardKeyword Unique =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Unique)).GetModCardKeyword();
}
```

本地化 `card_keywords.json`：`TEST_KEYWORD_UNIQUE.title` / `.description`。
卡牌类里 `public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Unique /*, CardKeyword.Exhaust*/];`；判断 `Keywords.Contains(MyKeywords.Unique)`；逻辑可配合单例实现。

tag（打击/防御一类，影响打击木偶等联动）：

```csharp
[RegisterOwnedCardTag(nameof(Heavy))]
public class MyTags
{
    public static readonly CardTag Heavy =
        ModContentRegistry.GetQualifiedCardTagId(Entry.ModId, nameof(Heavy)).GetModCardTag();
}
```

卡牌类：`protected override HashSet<CardTag> CanonicalTags => [MyTags.Heavy /*, CardTag.Strike*/];`；判断 `Card.Tags.Any(t => t == MyTags.Heavy)`。**记得给打击/防御牌加原版 `CardTag.Strike` / `CardTag.Defend`。**

## 动态变量与提示文本

- 简单自定义数值：`ModCardVars.Int("Leech", 3)` 加进 `CanonicalVars`；要本地化提示就链 `.WithSharedTooltip("TEST_LEECH")`，文本写进 `static_hover_tips.json`（`TEST_LEECH.title` / `.description`），描述里用 `{Leech:diff()}`。
- 读取：`DynamicVars["Leech"].BaseValue` / `.IntValue`；原版便捷访问如 `DynamicVars.Damage`。
- 额外提示框（HoverTip）：

```csharp
protected override IEnumerable<IHoverTip> AdditionalHoverTips => [
    HoverTipFactory.FromCard<Shiv>(),
    HoverTipFactory.FromPower<TestPower>(),
    HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
];
```

- 需要按目标/升级/预览动态计算的数值：用 `ComputedDynamicVar`（见 advanced-content.md），不要用原版 `CalculatedVar`（繁琐且问题多）。

## 描述文本写法

描述是 `RichTextLabel`，支持 Godot BBCode（`[b]` `[i]` `[u]` `[color=]` `[font_size=]` 等）+ 游戏自定义 tag：
颜色类 `[gold]` `[red]` `[blue]` `[green]` `[orange]` `[purple]` `[pink]` `[aqua]`；动画类 `[jitter]` `[sine]` `[fade_in]` `[fly_in]` `[thinky_dots]` `[rainbow freq=0.3 sat=0.8 val=1]`；`[ancient_banner]` 先古横幅。

占位变量对应 `CanonicalVars` 里的 var（`{Damage}`↔`DamageVar`、`{Block}`、`{Cards}`、`{Energy}`、`{Repeat}`、`{Heal}`、`{HpLoss}`、`{MaxHp}`、`{Gold}`、`{Summon}`、`{Forge}`、`{Stars}`、`{StrengthPower}`↔`PowerVar<StrengthPower>` 等各能力、`{CalculatedDamage}`/`{CalculatedBlock}`）；`{energyPrefix}` 是固定能量数。

formatter（SmartFormat）：
- 游戏自定义：`diff()`（高于基础变绿/低于变红，升级预览用）、`inverseDiff()`、`energyIcons()`、`starIcons()`、`{IfUpgraded:show:升级文本|未升级文本}`、`abs`、`percentMore()`/`percentLess()`（1.25→25%）。
- SmartFormat 内置：`{X:cond:>0?生效|不生效}`、`{X:choose(1):一|{:diff()}}`、`plural`（英文复数）等。

卡牌独有上下文变量：`{singleStarIcon}`、`{InCombat:...|}`、`{IsTargeting:...|}`、`{OnTable:在场上|不在场上}`、`IfUpgraded`。

能力 `smartDescription`/`remoteDescription` 运行时变量：`{Amount}`、`{OnPlayer:你|该敌人}`、`{IsMultiplayer}`、`{PlayerCount}`、`{OwnerName}`、`{ApplierName}`、`{TargetName}`。

`LocString` 手写描述时：`new LocString("powers", Id.Entry + ".description")` → `.Add("Amount", value)` 注入变量 → `.GetFormattedText()`。
