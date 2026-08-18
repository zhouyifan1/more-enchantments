# Hook 触发顺序速查

> 基于游戏 0.110.1 源码（`CombatManager`、`CreatureCmd`、`Hook`）。预测同一钩子内多个来源的先后、定位"为什么我的效果没按预期触发"时查此表。

## 遍历顺序（同一钩子内谁先被调用）

**战斗内**（`ICombatState.IterateHookListeners`），按生物依次：
1. 该生物的所有 Power（按挂载顺序）
2. 若是玩家：遗物（跳过融化的）→ 药水槽 → 充能球队列 → 所有牌堆每张卡（手牌/抽/弃/打出区/消耗区，每卡后跟其 Affliction 和 Enchantment）
3. 若是怪物：Monster 模型
4. 最后：全局 Modifiers → BadgeModels → MultiplayerScalingModel → 单例及其他

**战斗外**（`IRunState.IterateHookListeners`）：玩家牌组（卡+附魔）→ 遗物 → 药水 → 全局 Modifiers 等 → 外部订阅者 → 当前战斗监听者（递归）。

> 战斗结束/正在结束时 `Hook.IterateCombatHookListeners` 不派发监听者（死亡/结束序列内部直接调用的除外）。

## 回合流程（CombatManager.StartTurn）

每个生物回合开始：
1. `creature.BeforeTurnStart(side)`（记录每个 Power 的 AmountOnTurnStart）
2. `Hook.BeforeSideTurnStart`
3. `creature.AfterTurnStart(side)`（内部 ClearBlock，经 `Hook.ShouldClearBlock`）
4. `Hook.AfterBlockCleared`
5. 每个玩家 SetupPlayerTurn：
   - `Hook.ShouldPlayerResetEnergy` → 重置能量 → `Hook.AfterEnergyReset`
   - `Hook.BeforeHandDraw` → `Hook.ModifyHandDraw` → `Hook.AfterModifyingHandDraw`
   - 首回合特殊处理（附魔置底、固有置顶）
   - `CardPileCmd.Draw`（`Hook.ShouldDraw` → 洗牌 `Hook.ModifyShuffleOrder` → `Hook.AfterShuffle` → 每张 `Hook.AfterCardDrawn`）
   - `Hook.AfterPlayerTurnStart`
6. `Hook.AfterSideTurnStart`
7. 每个玩家充能球 `OrbQueue.AfterTurnStart`

## 出牌流程（CardModel.OnPlayWrapper）

1. `CardPileCmd.AddDuringManualCardPlay`（手牌→打出区）
2. `Hook.ModifyCardPlayResultLocation` → 各修改者 `AfterModifyingCardPlayResultLocation`
3. `GeneratePlayCount`（内部 `Hook.ModifyCardPlayCount`）
4. 每次打出循环：`Hook.BeforeCardPlayed` → `card.OnPlay(...)`（**你的卡牌效果在这**）→ 附魔 `Enchantment.OnPlay` → 感染 `Affliction.OnPlay` → `Hook.AfterCardPlayed`
5. 结算结果位置（弃牌/消耗/回手等）

## 伤害流程（CreatureCmd.Damage，每个目标依次）

1. `Hook.ModifyDamage`（附魔先于模型）→ `Hook.AfterModifyingDamageAmount`
   - 内部顺序：附魔加算 → 各监听者 `ModifyDamageAdditive` → `ModifyDamageMultiplicative` → `ModifyDamageCap`（取最小）
2. `Hook.BeforeDamageReceived`
3. 格挡结算 `DamageBlockInternal`
4. `Hook.ModifyHpLost`(BeforeOsty) → `Hook.AfterModifyingHpLostBeforeOsty`
5. `Hook.ModifyUnblockedDamageTarget`（未格挡伤害可转移奥斯蒂）
6. `Hook.ModifyHpLost`(AfterOsty) → `Hook.AfterModifyingHpLostAfterOsty`
7. 掉血 `LoseHpInternal`
8. 结算：破格挡 `Hook.AfterBlockBroken`；掉血后 `Hook.AfterCurrentHpChanged`；`Hook.AfterDamageGiven`（攻击者视角）；**目标存活** → `Hook.AfterDamageReceived`，**目标死亡** → 记入 killedCreatures
9. 循环结束：`Kill(killedCreatures)` → 死亡流程

> `AfterDamageReceived` 只在目标存活时调用；死亡时跳过错走死亡流程。

## 死亡流程（CreatureCmd.KillWithoutCheckingWinCondition）

1. HP>0 则先归零 + `Hook.AfterCurrentHpChanged`
2. `Hook.BeforeDeath`
3. `Hook.ShouldDie` 判定（可被防，如瓶中精灵）
   - 允许死亡：`Died` 事件 → 死亡动画 → **`Hook.AfterDeath`（此时能力还在！）** → 移出战斗 → `RemoveAllPowersAfterDeath` → 清理 Power → 主敌人连带击杀队友 → 玩家：清球、杀奥斯蒂、`DeactivateHooks`、`HandlePlayerDeath`
   - 被防止：`Hook.AfterDeath`(wasRemovalPrevented: true) → `Hook.AfterPreventingDeath` → 仍濒死则递归重试（最多 10 次）

## Power 施加（PowerCmd.Apply）

1. `Hook.BeforePowerAmountChanged`
2. `Hook.ModifyPowerAmountGiven`（施加者视角）
3. `Hook.ModifyPowerAmountReceived`（受击者视角）
4. 多人缩放（`ShouldScaleInMultiplayer`）
5. `power.BeforeApplied` → `ApplyInternal`（真正挂上）
6. `Hook.AfterModifyingPowerAmountGiven` + `AfterModifyingPowerAmountReceived`
7. `power.AfterApplied` → `Hook.AfterPowerAmountChanged`

## 玩家回合结束

Phase One（可触发玩家选择）：
1. `Hook.AfterAutoPostPlayPhaseEntered`
2. `Hook.BeforeSideTurnEnd`
3. 每玩家 `DoTurnEnd`：充能球 `BeforeTurnEnd` → 手牌 OnTurnEndInHand（先消耗虚无卡 `Hook.ShouldEtherealTrigger` → `CardCmd.Exhaust`，再逐张 `OnTurnEndInHandWrapper`）
4. `Hook.BeforeFlush`

Phase Two（纯清理）：
1. 每玩家 `FlushPlayerHand`：`Hook.ShouldFlush` → 弃牌（保留留手）→ `Hook.AfterFlush` → `EndOfTurnCleanup`
2. `Hook.AfterSideTurnEnd`
3. `Hook.ShouldTakeExtraTurn` → SwitchSides

敌人回合结束：`Hook.BeforeSideTurnEnd`（敌侧）→ 每玩家 `EndOfTurnCleanup` → `Hook.AfterSideTurnEnd`（敌侧）。

## 战斗结束（EndCombatInternal）

1. `turnState.IsInProgress = false`
2. 每玩家 `ReviveBeforeCombatEnd`
3. `Hook.AfterCombatEnd`
4. 清历史、房间收尾、写回放
5. 每玩家 `player.AfterCombatEnd()`
6. `Hook.AfterCombatVictory`
7. 保存进度、成就检查

> 奖励：`Hook.AfterCombatVictory` 之后进奖励选择，还有 `Hook.BeforeCombatRewardOffered` → `Hook.ModifyRewards` 系列。
