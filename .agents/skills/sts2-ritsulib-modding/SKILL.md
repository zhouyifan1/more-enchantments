---
name: sts2-ritsulib-modding
description: Develop Slay the Spire 2 (杀戮尖塔2 / STS2) mods in C# with the RitsuLib framework. Use when the user wants to create, debug, or migrate an STS2 mod — adding cards (卡牌), relics (遗物), potions (药水), powers (能力), characters (人物), monsters (怪物), events (事件), enchantments (附魔), orbs (充能球), custom UI, persistence, settings pages, Harmony patches, or Steam Workshop upload. Covers environment setup (Godot 4.5.1 Mono + .NET 9), mod project structure, RitsuLib content registration, lifecycle events, patching, data saving, and localization. Triggers on: 杀戮尖塔2 mod, STS2 mod, RitsuLib, BaseLib 迁移, 尖塔2 模组开发.
---

# STS2 Mod Development based on RitsuLib

《杀戮尖塔2》(Slay the Spire 2, STS2) 原生支持模组。本 skill 指导基于 **RitsuLib** 框架的模组开发。

- 教程来源: https://tutorials.sts2modding.com/ （仓库 GlitchedReme/SlayTheSpire2ModdingTutorials）
- RitsuLib: https://github.com/BAKAOLC/STS2-RitsuLib ｜ 官方文档: https://sts2-ritsulib.ritsukage.com/
- 游戏处于抢先体验（public-beta），API 变动频繁；遇到与本 skill 冲突时以最新源码/文档为准。

## 核心心智模型

一个 STS2 mod 由三件套构成，放在 `游戏根目录/mods/{modid}/`：

| 文件 | 内容 | 何时重新生成 |
| --- | --- | --- |
| `{modid}.dll` | C# 代码（可选） | 改代码后 `dotnet build` |
| `{modid}.pck` | Godot 资源包：图片、场景、音频、本地化 json（可选） | 改资源后重新导出 |
| `{modid}.json` | mod 配置清单（**必须**） | 改配置后 |

**RitsuLib 内容 ID 规则**：通过 RitsuLib 注册的内容，ID 变为 `{MODID大写}_{类别}_{类名大写SNAKE_CASE}`。
例如 modid `Test`、类 `TestCard` → 卡牌 ID `TEST_CARD_TEST_CARD`。本地化键、控制台指令都用这个最终 ID。
（例外：遭遇 ID 用连字符形式 `TEST-TEST_ENCOUNTER`。）

**代码模式**：内容类继承 `ModXxxTemplate` 基类 + 标注 `[RegisterXxx]` 注解 + `AssetProfile` 配资源 + `CanonicalVars` 配数值 + 本地化 json 配文本。

## 标准开发流程

1. **搭环境**：Godot 4.5.1 Mono（.NET 版）+ .NET 9 SDK + Rider/VSCode → 见 [references/setup-and-project.md](references/setup-and-project.md)
2. **建项目**：Godot 新项目（Mobile 渲染器）→ 创建 C# 解决方案 → 写 `{modid}.json` → 改 `.csproj`（引用 `sts2.dll`、`0Harmony.dll`，加自动复制 target）→ 写 `Entry.cs`
3. **接 RitsuLib**：`<PackageReference Include="STS2.RitsuLib" Version="*" />`，`{modid}.json` 加 dependency `{ "id": "STS2-RitsuLib" }`，`Entry.Init` 里注册程序集（下方模板）
4. **加内容**：按内容类型查 references 路由表，照模板写类 + 资源 + 本地化
5. **调试**：`dotnet build` 自动部署 dll；F5 断点调试 / 热重载；游戏内 `~` 控制台（`card <ID>`、`power <ID> 1 0`、`fight <ID>` 等）；`--fastmp` 本地联机 → 见 setup-and-project.md 调试一节
6. **发布**：官方上传器 megacrit/sts2-mod-uploader → 见 setup-and-project.md 上传一节

## Entry.cs 初始化模板（RitsuLib 方式）

```csharp
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;

namespace MyMod.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "MyMod";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // 有挂在 .tscn 上的 C# 脚本才需要；纯模型/补丁 mod 可省略
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        // 必须：启用 [RegisterXxx] 注解自动注册
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // RitsuLib 封装补丁系统（可选但推荐）
        var patcher = RitsuLibFramework.CreatePatcher(ModId, "main");
        // patcher.RegisterPatches<MyPatchSet>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod);
    }

    private static void DisableMod() { /* 必要补丁失败时标记自己的 mod 停用 */ }
}
```

## 内容类型路由表

写具体内容前，先读对应 reference 文件：

| 需求 | 读取 |
| --- | --- |
| 环境配置、项目骨架、mod_manifest、构建/导出 PCK、调试热重载、启动参数、控制台指令、反编译看源码、上传工坊 | [references/setup-and-project.md](references/setup-and-project.md) |
| 卡牌、遗物、药水、能力（含临时能力）、充能球、附魔、卡池、关键词/tag、动态变量、提示文本、描述文本写法（BBCode/占位变量/formatter） | [references/content-creation.md](references/content-creation.md) |
| 新人物、怪物与遭遇、事件、先古之民、时间线/解锁、单例、组件(Capability)、角色动画、计算动态变量、FMOD 音频 | [references/advanced-content.md](references/advanced-content.md) |
| 自定义卡堆、顶栏按钮、Toast 通知、血条覆盖、手牌上限/泛光、额外角标、自定义奖励、自定义目标、右键交互、次要资源、节点附加 | [references/gameplay-and-ui.md](references/gameplay-and-ui.md) |
| 三种注册方式、补丁系统、生命周期事件、数据保存（ModDataStore/RunSavedData/SavedAttachedState）、设置页、运行时热键、更新检查、模组联动、网络联机 | [references/infrastructure.md](references/infrastructure.md) |
| Hook 触发顺序（回合/出牌/伤害/死亡/能力施加/战斗结束） | [references/hook-order.md](references/hook-order.md) |
| BaseLib → RitsuLib 迁移对照 | [references/infrastructure.md](references/infrastructure.md) 末尾 |

## 关键规则与易错点

- **每个文件都写 `namespace`**，且命名空间第一段用自有前缀（不要叫 `Test`）。
- **资源路径**：`res://{ModId}/...` 指向 Godot 项目里与 modid 同名的资源文件夹（不是项目根目录），例如 `res://Test/images/cards/TestCard.png`。本地化在 `{ModId}/localization/{lang}/cards.json`，`zhs` = 简体中文。
- **发布后不要改**：注册用的 key/ID 字符串（存档兼容性）；数值类改动直接加新属性。
- **版本字符串必须 semver 三段** `X.X.X`；游戏 API 0.105+ 的 dependencies 用对象写法 `{ "id": "STS2-RitsuLib" }`，旧分支用字符串写法。
- **目标旧游戏分支**时用 `STS2.RitsuLib.Compat.<api-version>` 包；分发给玩家可用 `STS2-RitsuLib.<ver>.variant-pack.zip`（自动选版本）。
- **效果逻辑用 async/await**（`OnPlay` 里 `await DamageCmd.Attack(...)...Execute(choiceContext)`），想做某种效果先反编译找原版类似实现参考。
- **反编译工具**：gdsdecomp（整个 pck）或 ILSpy/dnSpy（只看 `data_sts2_windows_x86_64/sts2.dll`）。内容代码在 `MegaCrit.Sts2.Core.Models.*`；先在 `localization/zhs/cards.json` 搜中文名定位类名。
- **热重载有限**：不能增删函数等大改；PCK 资源不能热重载。
- 随从/召唤类机制优先看 MinionLib，不要用 RitsuLib 硬造。

## 常用外部文档

- Godot: https://docs.godotengine.org/zh-cn/4.x/
- Harmony: https://harmony.pardeike.net/articles/intro.html
- C#: https://learn.microsoft.com/zh-cn/dotnet/csharp/tour-of-csharp/
- BaseLib（另一个基础库）: https://github.com/Alchyr/BaseLib-StS2
