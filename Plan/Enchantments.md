| 名称             | 效果                                 | 限定卡牌       | 备注                                                   | 添加卡面描述 |
| ---------------- | ------------------------------------ | -------------- | ------------------------------------------------------ | ------------ |
| 坚韧(Resilience) | 获得保留，每被保留一次，格挡+Amount  | 可获得格挡     | “被保留时”可参考遗物Bookmark                           | 是           |
| 沉重(Heavy)      | 耗能+1，伤害翻倍                     | 攻击卡         | 可参考附魔Instinct                                     | 否           |
| 粘手(Stick)      | 战斗中前Amount次被打出时回到手牌     | 非能力卡       | “回到手牌”可参考卡牌particle_wall                      | 是           |
| 燃尽(Burnt out)  | 获得消耗，获得重放Amount             | 非能力，非消耗 |                                                        | 否           |
| 战术(Tactics)    | 下回合开始时获得Amount点费用         | 无限定         | 应使用状态energy_next_turn实现，参考卡牌charge_battery | 是           |
| 反应(Reaction)   | 下回合开始时额外抽Amount张牌         | 无限定         | 应使用状态draw_cards_next_turn实现，参考卡牌 Predator  | 是           |
| 弱化(Weakening)  | 施加Amount层虚弱                     | 攻击卡         |                                                        | 是           |
| 锯齿( Serrated)  | 施加Amount层易伤                     | 攻击卡         |                                                        | 是           |
| 淬毒(Poisoned)   | 每造成1次伤害，施加Amount层中毒      | 攻击卡         | 参考状态ENVENOM                                        | 是           |
| 血祭(Sacrifice)  | 失去Amount点生命，获得2*Amount点力量 | 无限定         |                                                        | 是           |
| 诅咒(Cursed)     | 施加所造成伤害同等的灾厄             | 攻击卡         | 参考状态reaper_form                                    | 是           |
| 电动(Electric)   | 生成一个闪电充能球                   | 无限定         | 参考卡牌Zap                                            | 是           |
| 铸剑(Forge)      | 铸造Amount                           | 无限定         | 参考卡牌TheSmith                                       | 是           |

注：

优先使用RitsuLib中功能实现，若其中功能无法实现再考虑查询源代码

sts2.dll反编译结果在D:\Program Files\Godot\WorkPlace\STS2\ 中

遗物、卡牌、附魔、事件等：D:\Program Files\Godot\WorkPlace\STS2\MegaCrit.Sts2.Core.Models.类型\，请按需查询，我的备注仅说明对应物品有类似词条，不保证可以套用在这里，若你有更好实现思路，按照你的来

效果描述请你润色后增加到本地化中

附魔的图标请复制并重命名工作区根目录的icon64.png进行占位