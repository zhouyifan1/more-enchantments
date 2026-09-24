# 多彩的附魔台 (ColorfulEnchantTableEvent)

## 基本信息

| 项 | 值 |
| --- | --- |
| 类名 (PascalCase) | `ColorfulEnchantTableEvent` |
| 注册方式 | `[RegisterActEvent(typeof(Overgrowth))]`<br />&&`[RegisterActEvent(typeof(Underdocks))]` |
| 生成条件 (IsAllowed) |  |
| 布局类型 | Normal |
| 立绘 | `res://MoreEnchantments/images/events/ColorfulEnchantTableEvent.png` |

## 动态变量 (CanonicalVars)

无

## 页面与选项流程

> 页面名全大写（`INITIAL` 为入口页）。选项 KEY 全大写蛇形，对应代码中的 `InitialOptionKey("KEY")` / `ModOptionKey("页面", "KEY")`。

```text
INITIAL{
	BUTTON -> BUTTON_END,
	PULL_ROD -> PULL_ROD_END,
	ENLIGHTENMENT -> ENLIGHHTMENT_END
}
BUTTON_END{}
PULL_ROD_END{}
ENLIGHHTMENT_END{}
```

### INITIAL 页

描述文案（zhs）：

> 你路过一个巨大的坑，里面躺着一块残破的银白色金属遗骸，看着像某种巨大机器的一部分残骸...

| 选项 KEY | 标题 | 描述 | 出现条件 | 效果 | 跳转 |
| --- | --- | --- | --- | --- | --- |
| BUTTON | 一个绿色的按钮 | 按下试试？ | 无 | 选择一张已附魔的牌（来源）与一张没有附魔的牌（目标），把前者的附魔复制给后者（仅保留兼容的附魔） | BUTTON_END |
| PULL_ROD | 一个红色的拉杆 | 拉下试试？ | 无 | 选择你卡牌中一张已附魔的牌，翻倍它的附魔层数 | PULL_ROD_END |
| ENLIGHTMENT | 这让你想起什么... | 你感到携带的一件物品在蠢蠢欲动 | 拥有遗物“陨铁石板” | 将“陨铁石板”变为“银白金属” | ENLIGHTMENT_END |

### BUTTON_END 页（结束页）

描述文案（zhs）：

> 你感到一股精妙的力量涌入身体，
>
> 你看向遗骸，它看起来失去了某种光泽

### PULL_ROD_END 页（结束页）

描述文案（zhs）：

> 你感到一股刚直的力量涌入身体，
>
> 你看向遗骸，它看起来失去了某种光泽

### ENLIGHTMENT_END 页（结束页）

描述文案（zhs）：

> 这块石板，它亮了起来！
>
> 它展现出了原本银白色的模样
>
> 你的脑海中闪回了许多画面：卫星高悬在星球上空，神圣的白鹿飞驰而来...
>
> 这是这个星球上发生的事情吗？
>
> 你抬头看向遗骸，感觉你们之间出现了某种连结

（结束页用 `SetEventFinished(描述)` 关闭事件，无选项。）

## 联动内容

| 类型 | 引用 | 说明 |
| --- | --- | --- |
| 遗物 | MeteoriteSlab | 普通遗物 |
| 遗物 | SilverMetal | 事件遗物 |

## 状态

已实现（`Scripts/Events/AncientTechnologyEvent.cs`
