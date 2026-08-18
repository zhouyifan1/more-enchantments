# More Enchantments —— STS2 附魔扩展 Mod

《杀戮尖塔 2》内容型 Mod，基于 RitsuLib 框架。项目规划见 `Plan/PROJECT_PLAN.md`。

## 开发约定

- **Hooks 文档维护**：`Plan/Hooks.md` 记录开发中实际使用过的游戏 Hook 与配套 API。每次开发新功能（附魔/遗物/事件/补丁等）时，必须把新用到的 Hook 补充进去。
- **Mod 开发规范**：一律遵循项目内 skill `.agents/skills/sts2-ritsulib-modding/`（入口 `SKILL.md`，含各专题 references）。
- **游戏反编译源码**：`D:\Program Files\Godot\WorkPlace\STS2\`（内容模型在 `MegaCrit.Sts2.Core.Models.*`）。优先用 RitsuLib 已有功能，无法实现再查源码。

## 构建与部署

- `dotnet build`：编译并自动部署 dll/pdb/json 到 `G:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MoreEnchantments\`（RitsuLib 运行时由 NuGet 自动部署）。
- PCK（图标/本地化资源）：需在 Godot 编辑器导出到同一 mods 目录；新增图片资源后要重新导出。

## 内容 ID 规则

- RitsuLib Entry 格式：`<MODID>_<CATEGORY>_<TYPENAME>`，如 `MoreEnchantments` 的 `ResilienceEnchantment` → `MORE_ENCHANTMENTS_ENCHANTMENT_RESILIENCE_ENCHANTMENT`。发布后视为稳定 ID，不得更改（改名用 `StableEntryStem` 兼容）。
- 本地化：`MoreEnchantments/localization/{zhs,eng}/*.json`，键 = Entry ID + 后缀；`{Amount}` 占位符由附魔基类自动注入。
