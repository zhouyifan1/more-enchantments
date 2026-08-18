# 环境配置与项目骨架

> 目录：环境安装 → mod_manifest → .csproj → Entry → 构建与导出 → 调试/热重载 → 启动参数 → 控制台指令 → 查看源码 → 上传工坊 → 项目改名/mac 支持

## 环境安装

- **Godot 4.5.1 Mono**：https://godotengine.org/download/archive/4.5.1-stable/ ，必须选 **.NET** 版本。（制作组自改版 MegaDot: https://megadot.megacrit.com/ ，建议先用官方版。）
- **.NET SDK 9+**：https://dotnet.microsoft.com/zh-cn/download
- **IDE**：强烈推荐 Rider；VSCode 需装 C# Dev Kit（可选 Godot Tools），记得开自动保存。
- 前置知识：C# 基础、JSON、Godot 编辑器基本操作、图片处理。
- mod 模板：RitsuLib 模板 https://github.com/alkaid616/RitsuLibModTemplate ；BaseLib 模板 https://github.com/Alchyr/ModTemplate-StS2

## 创建 Godot 项目

新建项目，渲染器选 **Mobile/移动**（与游戏一致）→ 左上角"创建 C# 解决方案"。

## `{modid}.json`（mod_manifest）

在**项目文件夹**创建与项目同名的 json（如 `Test.json`），`{}` 为占位符需替换：

```json
{
  "id": "MyMod",
  "name": "我的 Mod",
  "author": "作者名",
  "description": "Mod 描述",
  "version": "0.1.0",
  "min_game_version": "0.107.1",
  "has_pck": true,
  "has_dll": true,
  "dependencies": [
    { "id": "STS2-RitsuLib", "min_version": "0.2.27" }
  ],
  "affects_gameplay": true
}
```

- `id` 必填且唯一，建议与项目名一致。版本号必须 semver 三段 `X.X.X`。
- `affects_gameplay`：多人模式是否影响内容；纯换皮/优化 mod 填 `false`，默认 `true`。
- 依赖写法：游戏 API 0.105.x+ 用对象 `{ "id": "STS2-RitsuLib" }`；更旧分支用字符串 `"STS2-RitsuLib"`（旧解析器不认对象）。

## `.csproj`

```xml
<Project Sdk="Godot.NET.Sdk/4.5.1">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <LangVersion>13.0</LangVersion>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>

    <!-- 改成你的杀戮尖塔2目录 -->
    <Sts2Dir>D:\xxx\Steam\steamapps\common\Slay the Spire 2</Sts2Dir>
    <Sts2DataDir>$(Sts2Dir)\data_sts2_windows_x86_64</Sts2DataDir>
    <!-- 让 RitsuLib NuGet 自动部署运行时到你的 mods 文件夹 -->
    <RitsuLibDeployDir>$(Sts2Dir)/mods/STS2-RitsuLib/</RitsuLibDeployDir>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="sts2">
      <HintPath>$(Sts2DataDir)\sts2.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(Sts2DataDir)\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- NuGet 获取 RitsuLib（推荐）；旧分支用 STS2.RitsuLib.Compat.<api-version> -->
    <PackageReference Include="STS2.RitsuLib" Version="*" />
  </ItemGroup>

  <!-- 自动复制 dll 和 json 到游戏 mods 文件夹 -->
  <Target Name="Copy Mod" AfterTargets="PostBuildEvent">
    <Message Text="Copying mod to Slay the Spire 2 mods folder..." Importance="high" />
    <MakeDir Directories="$(Sts2Dir)\mods\" />
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(Sts2Dir)\mods\$(MSBuildProjectName)\" />
    <Copy SourceFiles="$(MSBuildProjectName).json" DestinationFolder="$(Sts2Dir)/mods/$(MSBuildProjectName)/" />
  </Target>
</Project>
```

本地引用 RitsuLib 的替代写法（不推荐）：`<Reference Include="STS2-RitsuLib"><HintPath>$(Sts2Dir)/mods/RitsuLib/STS2-RitsuLib.dll</HintPath><Private>false</Private></Reference>`。
玩家侧安装：从 https://github.com/BAKAOLC/STS2-RitsuLib/releases 下稳定版（非 Development build），按游戏版本选（无后缀≈测试版，`Compat.0.103.2` 之类兼容旧版）；分发可打包 `variant-pack.zip`（自动检测游戏版本）。

## Entry.cs

见 SKILL.md 的初始化模板。纯 Harmony 方式（不用 RitsuLib 补丁系统时）：

```csharp
var harmony = new Harmony("sts2.reme.testmod"); // ID 不与他人撞车即可
harmony.PatchAll();
ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly); // 使 tscn 可加载自定义脚本
```

## 构建与导出

- **DLL**：`dotnet build`（VSCode `Ctrl+Shift+B` / Rider 构建），自动复制到 `mods/{modid}/`。
- **PCK**：Godot 编辑器 → 项目 → 导出 → 添加 Windows 预设 → **导出 PCK/ZIP**（必须是 pck），文件名 `{modid}.pck`，导出到 `mods/{modid}/`。可选：导出选项"资源"里排除 `{modid}.json`。
- **命令行导出 PCK**（不启动 Godot）：在 csproj 加
  ```xml
  <GodotExe>D:/path/Godot_v4.5.1-stable_mono_win64.exe</GodotExe>
  ...
  <Target Name="ExportPck">
    <Exec Command="&quot;$(GodotExe)&quot; --headless --export-pack &quot;Windows Desktop&quot; &quot;$(Sts2Dir)/mods/$(MSBuildProjectName)/$(MSBuildProjectName).pck&quot;"
      EnvironmentVariables="IsInnerGodotExport=true;MSBUILDDISABLENODEREUSE=1"
      ContinueOnError="WarnAndContinue" />
  </Target>
  ```
  然后 `dotnet build -t:ExportPck`（VSCode）；Rider 则把 `AfterTargets="Publish"` 后右键 Publish。
- **运行验证**：首次启动提示开启 mod → 是 → 游戏关闭再开，右下角"已加载模组"即成功。

## 调试与热重载

csproj 增加 Debug/Release 区分 + 复制 pdb：

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <Optimize>false</Optimize>
  <DebugType>portable</DebugType>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <Optimize>true</Optimize>
  <DebugType>none</DebugType>
  <PathMap>$(AppOutputBase)=.\</PathMap>
</PropertyGroup>
<!-- Copy Mod target 里加： -->
<Copy SourceFiles="$(TargetDir)$(TargetName).pdb" DestinationFolder="$(Sts2Dir)/mods/$(MSBuildProjectName)/"
      Condition="Exists('$(TargetDir)$(TargetName).pdb')" />
```

- **VSCode**：`.vscode/launch.json`（coreclr launch，program 指向游戏 exe）、`tasks.json`（dotnet build Debug）、`settings.json` 配 `sts2.installDir` / `sts2.gameExeName` / `sts2.modId`；设置里启用 `Csharp › Experimental › Debug: Hot Reload`；F5 启动，改代码后点 🔥 应用热重载。
- **Rider**：Add Configuration → `.NET Executable` 指向游戏 exe，用 **Debug** 启动；改代码后点 🔥 / Apply Changes。
- 限制：热重载不能增删函数等大改；PCK 资源不可热重载；提示非 Steam 启动时在**游戏根目录**建 `steam_appid.txt` 写入 `2868840`，或加 `--force-steam=off`。
- **看日志**：控制台 `open logs`；或复制 `launch_xxx.bat` 加 `--log` 参数 + `steam_appid.txt` 双击运行。

## 启动参数

编辑游戏根目录 `launch_xxx.bat`：

| 参数 | 示例 | 作用 |
| --- | --- | --- |
| `--autoslay` / `--seed=x` / `--log-file=x` | `--autoslay` | 自动跑图测试（发行版需 patch `NGame.IsReleaseGame` 返回 false） |
| `--bootstrap` | | 启动后直接进入某场景（发行版需 patch） |
| `--fastmp` | `--fastmp=host` | 本地联机：`host`/`host_standard`/`host_daily`/`host_custom`/`load`/`join` |
| `--clientId` | `--clientId=1001` | 本地联机玩家 ID（join 必填，每个实例不同） |
| `+connect_lobby` | `+connect_lobby <steam大厅ID>` | 自动加入大厅 |
| `--nomods` | | 不启用 mod |
| `--force-steam=off` | | 跳过 Steam 初始化 |
| `-log` | `-log Net Info` | 设置日志级别 |
| `-wpos` | `-wpos 100 200` | 窗口位置 |

本地联机测试：两个 bat，一个 `--fastmp=host`，一个 `--fastmp=join --clientId=1001`；存档问题用管理员模式启动。

## 控制台指令（游戏内按 `~`）

`tab` 补全，`↑` 历史。目标索引 0=自己，之后为敌人（联机先是其他玩家）。

- 战斗：`damage <数值> [目标]`、`block <数值> [目标]`、`kill [目标|all]`、`die`、`win`、`heal <数值> [索引]`、`godmode`、`power <ID> <层数> <目标>`、`energy <数值>`、`gold <数值>`、`stars <数值>`、`relic [add|remove] <ID>`、`potion <ID>`
- 卡牌：`card <卡牌ID> [hand|draw|discard|exhaust|master_deck]`（默认 hand，**只能在战斗中**）、`remove_card`、`upgrade <手牌位置>`、`enchant <ID> [层数] [位置]`、`afflict`、`draw <数量>`
- 地图：`act <幕>`、`room <ID>`、`event <ID>`、`fight <ID>`、`travel`、`ancient <ID> [遗物ID]`
- 其他：`help [命令]`、`open logs|saves|root|build-logs|loc-override`、`unlock <cards|...|all>`、`dump`（打印全部 model ID）、`log [类型] <级别>`、`art <card|relic|...>`（列缺美术内容）、`instant`（加速跳过动画）、`bestiary`、`trailer`、`getlogs`

## 查看游戏源码

- **gdsdecomp**（反编译整个游戏）：https://github.com/GDRETools/gdsdecomp → Recover Project → 选 `SlayTheSpire2.pck` → Extract → Godot 导入 `project.godot`（不需要能运行）。网络问题就在 Export Settings 关掉 Download Plugins。
- **ILSpy / dnSpy**（只看代码）：打开 `data_sts2_windows_x86_64/sts2.dll`。
- 内容代码在 `MegaCrit.Sts2.Core.Models.*`（如 `.Cards`）。技巧：先在 `localization/zhs/cards.json` 搜中文名 → 得类名 → 全局搜索。
- 看 async 方法的原始逻辑：ILSpy 语言版本切到 C# 4.0 之前，可看到编译后的状态机。

## 上传工坊

官方上传器：https://github.com/megacrit/sts2-mod-uploader

1. 运行 `ModUploader.exe` 生成工作区文件夹并改名。
2. mod 文件（json/dll/pck，不用压缩）放进工作区 `Content/`。
3. `workspace.json` 建议只留 `tags`（tags 在工坊**无法修改**，先查常用 tag：`Characters`、`QoL`、`Cards`、`Relics`、`schinese`、`English` 等）；其他字段上传时会覆盖工坊内容。
4. 替换工作区 `image.jpg`（预览图，**≤1MB**，文件名不变）。
5. `ModUploader.exe upload -w <工作区文件夹名>`。更新时同命令（mod ID 从 `mod_id.txt` 读），可写 bat 自动化。
6. 别忘了在工坊改可见性；`dependencies` 填对方工坊 ID（不带引号）。

## 其他

- **存档分离**：mod 版存档独立。复制 `%AppData%\SlayTheSpire2\steam\<steamid>\profile1` 等到同级 `modded` 文件夹。
- **项目改名**：改 `project.godot` 的 `config/name` 与 `project/assembly_name`、csproj/json/sln 文件名与内容里的引用，删除旧 mod 文件夹后重新打包。
- **mac 兼容**：`export_presets.cfg` 里 `binary_format/architecture="x86_64"` 改为 `"msil"`。
- **mod 封面图**：mod 根目录建与 modid 同名文件夹，放 `mod_image.png`。
