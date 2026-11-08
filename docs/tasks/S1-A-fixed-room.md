# S1-A 固定房间 + 固定出生点 + 3 个 Bullet Kin

> 状态：✅ 完成（固定 seed 方案；F4 在真实选人界面自动选 Pilot 仍未单独验证，手动选 Pilot 已验证）
> 上游：`docs/NEXT.md` 任务 1、`docs/s1-plan.md` 验收 A
> 本任务不接 TCP、不做软重置、不做训练。

## 目标

一键把玩家放到 1-1 的固定房间/固定坐标，并在固定偏移生成 3 个 Bullet Kin；同一配置可重复、可跨重启复现。

**角色固定为 Pilot（飞行员）**：禁止使用带随机武器/装备的角色；非 Pilot 时 `Setup` 必须拒绝布场并写日志。

## “固定房间”的定义（待 API 核实后锁定）

优先方案：**1-1 入口房间（entrance room）**。

- 选择规则：`RoomHandler.area.PrototypeRoomCategory == PrototypeDungeonRoom.RoomCategory.ENTRANCE`，
  或 `distanceFromStart == 0` 的标准房间；若两者都不唯一，再按房间索引/尺寸过滤。
- 固定标识：记录 `room.area.basePosition`、`room.area.dimensions`、房间名/类别、房间中心坐标。
- 不允许每次随机选一个房间；必须是可重复的同一选择规则。

> 如果入口房间在重启后坐标/尺寸会变，则改为“按房间类别+尺寸+相对位置选择”，并在日志中记录完整标识，保证同一构建下可复现。

## 验收标准（硬性）

- [x] 角色必须是 **Pilot**：`PlayerController.characterIdentity == PlayableCharacters.Pilot`；非 Pilot 时拒绝布场并写日志。
- [ ] 在角色选择界面按 F4 自动选 `PlayerRogue`（Pilot）——代码已实现，真实选人界面未单独验证；手动选 Pilot 已验证。
- [x] 操作路径：在**正常选人界面**手动选 Pilot → 进入 Breach → 按 `F4/J` 进入 1-1 并布场。**禁止运行时清玩家/换 prefab**（ADR-014）。
- [x] 触发后日志输出：固定房间标识（basePosition/dimensions/类别）、玩家固定坐标、3 个敌人固定坐标。
- [x] 玩家被传送到固定坐标，且能正常移动/翻滚（20 次循环无卡墙/输入丢失）。
- [x] 3 个 Bullet Kin 的 prefab 为 `BulletMan`，guid `01972dee89fc4404a5c408d50007dad5`，位置为固定偏移。
- [x] 连续执行 20 次“进入固定房间 + 生成 3 敌人”，敌人数量/位置/房间状态一致（`cycles=20/20`，`roomAll=3/0`）。
- [x] 重启游戏后，固定 seed `20261108` 下房间 `base/dims`、玩家/敌人 local 完全一致（两轮实测）。
- [x] 固定 seed 下可复现；未调用 `QuickRestart`。
- [x] 不写坏存档；测试后存档可读取（已另存 `backup/saves_rainbow_off_20261108_162816.zip`）。
- [ ] 证据：`BepInEx/LogOutput.log` 日志片段（必要时截图）。

## 已核实 API

- 角色固定：`CharacterSelectController.playerPrefabPaths` / `currentSelected` + 私有 `Do()`（反射调用）；Pilot 的 prefab path 是 `PlayerRogue`。
- 非 Pilot 守卫：`PlayerController.characterIdentity` + `PlayableCharacters.Pilot`。

## 待核实 API

- `GameManager.Dungeon` / `Dungeonator.DungeonData`：房间列表、按位置取房间、入口房间识别。
- `Dungeonator.RoomHandler`：`area`、`distanceFromStart`、房间类别/名称、房间中心坐标。
- `PlayerController`：安全传送方法（优先游戏自带 `TeleportToPoint`/`Warp`，避免直接改 transform 导致相机/房间状态不同步）。
- `GameManager`：从 Breach 进入 1-1 的合法入口（优先正常加载流程，不用 `QuickRestart`）。
- 房间进入事件：传送后是否会重复刷怪/触发 `OnEntered`。

## 证据记录

- 日志：`<游戏>\BepInEx\LogOutput.log`
- 任务完成后把关键日志追加到本文件，并更新 `STATE.md`、`NEXT.md`、`SESSION_LOG.md`。

## 实测结果（2026-11-08）

### 20 次一致性测试（J，单次运行内）

```text
consistency test DONE cycles=20/20
room base=(17,79) dims=(20,20)          # 20 次相同
player local=(9.25,9.13)                # 20 次相同
enemy local=(6.25,12.72) (9.25,13.72) (12.25,12.72)  # 3 个固定局部坐标
after-spawn roomAll=3                   # 20 次
after-clear roomAll=0                   # 20 次
```

结论：**单次运行内**固定房间/玩家/3 敌人局部坐标完全一致。

### 跨重启的发现

- 入口房间的类别/名称/尺寸稳定：`ENTRANCE / Gungeon Entrance / 20x20`。
- 但 `area.basePosition` 会随楼层生成变化（见过 `(11,129)`、`(102,159)`、`(25,21)`、`(17,79)`）。
- `GetCenteredVisibleClearSpot` 选出的玩家局部坐标也会变（见过 `(12.25,8.13)`、`(9.25,9.13)`），敌人局部坐标随玩家偏移变化。
- 因此“固定坐标”必须定义为**房间局部坐标**；若要跨重启完全一致，需要固定 run seed 或改成不依赖装饰物的固定 anchor。**目前跨重启完全一致尚未验收通过。**

## 跨重启确定性（下一步）

**方案 A（首选）**：固定 run seed。

- 在 Breach（Foyer）里、进入 1-1 之前设置 `GameManager.CurrentRunSeed = 20261108`。
- `Dungeon.GetDungeonSeed()` 会使用该 seed 生成楼层，理论上 `basePosition`/房间布局/clear spot 全部一致。
- 每次在 Foyer 发现 seed 不等于固定值时重新设置并写日志。

**验收**：

- [x] 连续两次重启游戏，从 Breach 进入 1-1，日志中的 `basePosition`、`dims`、玩家 local、3 敌人 local 完全一致。
- [x] 20 次 `J` 一致性测试仍为 `cycles=20/20`。
- [x] 固定 seed 下可复现（仍由游戏程序化生成，但结果确定）。

**实测（两轮重启）**：

```text
seed=20261108
room base=(11,84) dims=(20,20)
player local=(9.25,9.13)
enemies local=(6.25,12.72) (9.25,13.72) (12.25,12.72)
cycles=20/20
```

两轮完全相同。**固定 seed 方案通过。** 后续如需泛化，再评估随机 seed/课程学习，不作为 S1-A 验收项。

**退路**：固定 anchor（`area.basePosition + 固定偏移`，并校验 cell 可行走）未使用。

## 事故记录：运行时换角色导致枪/手错位

- **现象**：在 Breach 用 `ClearPrimaryPlayer + LoadCharacterSelect + PlayerPrefabForNewGame` 强制换 Pilot 后，角色与枪分离、枪漂在角色旁边、无法射击；`PrimaryPlayer` 返回 null。
- **原因**：运行时销毁/重建玩家破坏了 Foyer 状态、手部/枪挂点和输入状态。
- **修复**：禁用运行时换角色（ADR-014）；Pilot 只能在正常选人界面选；新增 `K`（修复枪/手）和 `L`（清陈旧标志 + 修复）。
- **教训**：不要在对局中 `ClearPrimaryPlayer` / 换 prefab；角色选择必须走游戏正常选人流程。

## 事故记录：`PreventPausing=true` 锁死瞄准/射击

- **现象**：进入 1-1 正常；按 J 跑 20 次后角色朝向锁死、不能射击；K/L 修复无效。
- **诊断**：`diag` 显示 `acceptingNonMotion=False`，枪/弹药/挂点/输入状态都正常。
- **原因**：`GameManager.PreventPausing=true` 会让 `PlayerController.AcceptingNonMotionInput=false`；瞄准/射击都在该分支，因此被禁用。J/K/L 之前反复设了 true。
- **修复**：移除所有 `PreventPausing=true`；K/L 改为 `PreventPausing=false` + `ForceUnpause()` + `timeScale=1`；失焦暂停交给 watchdog（ADR-015）。
- **验证**：用户确认“没有锁死了”。
