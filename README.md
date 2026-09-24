# More Enchantments（更多附魔）

《杀戮尖塔 2》（Slay the Spire 2）内容型 Mod，基于 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) 框架开发。

## 功能特性

- **更多附魔**：新增 18 个卡牌附魔，覆盖打出触发、数值修改、回合开始、保留成长、抽牌变身等多种触发点（设计清单见 `Plan/Enchantments.md`）。
- **附魔遗物**：新增 35 个与附魔机制联动的遗物，含角色限定池、共享池与先古之民选项扩展（设计文档按稀有度拆分在 `Plan/Relics/`）。
- **商店专属栏位**：商店在药水售卖下方新增 3 个「商店附魔遗物」栏位，专属遗物池（普通/罕见/稀有三档、独立定价），单局内可重复购买，同店不重复（`Plan/Relics/ShopEnchantRelics.md`）。
- **附魔上限扩展**：一张卡可附着多个附魔（默认上限 2，代码可调）；遗物/事件可在一局内继续提高上限且永不下降；同型附魔自动叠层不占槽；卡面多图标显示（`Plan/EnchantmentLimit.md`）。
- **附魔事件**：附魔主题事件作为附魔的获取/移除/博弈途径（`Plan/Events/`）。
- **随机附魔服务**：全附魔均匀权重表 + 局部调权的加权随机附魔 API（`Plan/RandomEnchantment.md`）。

## 安装（玩家）

1. 先安装运行时依赖 [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)（从其 Releases 下载，放入游戏 `mods/` 目录）。
2. 从本仓库 [Releases](../../releases) 下载 `MoreEnchantments.dll`、`MoreEnchantments.pck`、`MoreEnchantments.json` 三个文件，放入 `Slay the Spire 2/mods/MoreEnchantments/`。
3. 游戏内 Mod 管理界面启用。

要求游戏版本 ≥ 0.111.0（public-beta 分支）。游戏处于抢先体验，API 变动频繁，如遇兼容问题请携带游戏版本与日志反馈。

## 从源码构建

1. 安装 **.NET 9 SDK** 与 **Godot 4.5.1 Mono（.NET 版）**。
2. 克隆本仓库，编辑 `MoreEnchantments.csproj` 中的 `<Sts2Dir>` 为你的游戏安装目录。
3. `dotnet build`：编译并自动部署 dll/pdb/json 到游戏 `mods/MoreEnchantments/`。
4. 图标/本地化资源（PCK）：用 Godot 编辑器打开项目并导出到同一目录；**新增图片资源后必须重新导出**。
5. NuGet 会自动拉取 `STS2.RitsuLib` 编译包。

## 文档

| 目录/文件 | 内容 |
| --- | --- |
| `Plan/Enchantments.md` | 附魔设计清单与开发约定（含可堆叠约定） |
| `Plan/Relics/` | 遗物设计清单（按稀有度拆分，含商店附魔遗物池） |
| `Plan/Events/` | 事件设计文档 |
| `Plan/EnchantmentLimit.md` | 附魔上限（一卡多附魔）技术方案 |
| `Plan/RandomEnchantment.md` | 随机附魔服务方案 |
| `Plan/Hooks.md` | 已使用的游戏 Hook 与配套 API 笔记 |
| `AGENTS.md` | 仓库级开发约定 |

## 许可与声明

- 代码以 [MIT License](LICENSE) 发布。
- 占位图标基于 Godot 引擎默认图标（[CC-BY-4.0](https://godotengine.org/press/)，© Godot Engine contributors），正式美术资源将另行替换。
- 本 Mod 为玩家自制内容，与 MegaCrit 无关；仓库不包含任何游戏本体资源或反编译代码。
