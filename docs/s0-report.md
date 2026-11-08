# S0 报告 — Enter the Gungeon 单房间纯躲避 Demo

日期：2026-11-08  
游戏：Enter the Gungeon，Steam buildid 3829397（游戏内版本 2.1.9），Unity 2017.4.27f1，Mono x64  
环境：BepInEx 5.4.21.0 x64 + HarmonyX，插件 `EtgGym 0.1.0`（net35）

## 结论总表

| 实验 | 结果 | 证据 | 失败退路 |
| --- | --- | --- | --- |
| E1 输入注入（移动/射击/翻滚） | **通过** | 移动 4 秒位移 5.563；翻滚 `ForceStartDodgeRoll=True`；射击生成 `bullets=1` | 原计划 ViGEmBus + vgamepad；实际用了更稳的游戏原生 forced-input，未需要虚拟手柄 |
| E2 瞄准 | **通过** | `forceAimPoint=(209.13,82.44)`，`unadjustedAimPoint` 稳定等于该值 | 右摇杆（未使用） |
| E3 敌人生成/清除 | **通过** | 生成 3 个 `BulletMan(Clone)`，`roomAll=3/roomClear=3`；清除后 `alive=0/roomAll=0/roomClear=0` | 直接用 `RoomHandler.AddSpecificEnemyToRoomProcedurally(guid)`（也已核实可用） |
| E4 子弹枚举 | **通过** | `static=1, harmony=1, find=1`；累计 `added=94, removed=93` | `FindObjectsOfType<Projectile>()`（已实测可用，但开销大） |
| E5 时间加速 | **通过** | `timescale=2.0` 运行 60 秒后切回 `1.0`，用户确认无穿墙/卡动画/翻滚异常；日志无 Exception/NaN | 保持 1x（未使用） |

## E1 — 输入注入

### 尝试与选择

- 目标原本是“用 `BraveInput` 控制移动/射击/翻滚”。
- 反编译确认：`BraveInput.ActiveActions` 是 InControl `PlayerActionSet`，动作值每帧由真实设备 `Update()` 并 `Commit()`；没有公开的写入接口，直接改 `PlayerAction` 会被下一帧覆盖。
- `PlayerController` 自带公开的强制输入字段，适合 cutscene/tutorial/自动化：
  - `usingForcedInput` + `forcedInput`：移动
  - `forceFire` / `forceFireDown` / `forceFireUp`：射击
  - `ForceStartDodgeRoll(Vector2)`：翻滚
- 因此实际方案是**游戏原生 forced-input 注入**，不是 ViGEmBus。它无驱动依赖、无需管理员权限，比虚拟手柄更适合 RL。

### 实测日志

移动：

```text
[E1-move] start pos=(209.69,80.44)
[E1-move] end   pos=(204.13,80.44) delta=(-5.56,0.00) moved=5.563
```

翻滚：

```text
[E1-roll] ForceStartDodgeRoll(right)=True rolling=True state=InAir
```

射击：

```text
[E1-fire] start gun=SPMA1_Alt ammo=350
[E1-fire] stop firing=False ammo=350 bullets=1 harmony=1
```

结论：

- 移动、翻滚、射击三条输入通道均可用。
- 射击会绕过 `BraveInput` 的缓冲动作，产生 `BraveInput.ConsumeButtonDown/Up(): No action for Shoot was found` 报错；不影响开火，但正式纯躲避 Demo 不启用射击。
- `ammo` 未变化是该枪的显示/弹药机制，`bullets=1` 已证明投射物生成。

## E2 — 瞄准

实测：

```text
[E2-aim] target=(209.13,82.44) unadjustedAimPoint=(209.13,82.44,0.00)
[E2-aim] end unadjustedAimPoint=(209.13,82.44,0.00)
```

关键修正：`PlayerController.forceAimPoint` 的实际类型是 **`Vector2?`**，不是 `Vector3`。`unadjustedAimPoint` 才是 `Vector3`。结束后设 `forceAimPoint = null` 恢复鼠标/右摇杆瞄准。

## E3 — 敌人生成/清除

Bullet Kin 的实测标识：

- prefab 名：`BulletMan`
- EnemyGuid：`01972dee89fc4404a5c408d50007dad5`
- 生成对象名：`BulletMan(Clone)`

生成日志：

```text
[E3-spawn] #1 name=BulletMan(Clone) guid=01972dee89fc4404a5c408d50007dad5 pos=(200.31,83.09)
[E3-spawn] #2 name=BulletMan(Clone) guid=01972dee89fc4404a5c408d50007dad5 pos=(203.31,84.09)
[E3-spawn] #3 name=BulletMan(Clone) guid=01972dee89fc4404a5c408d50007dad5 pos=(206.31,83.09)
[E3-spawn] requested=3 spawned=3 roomAll=3 roomClear=3
```

清除日志：

```text
[E3-clear] aliveBefore=3 roomAll=3 roomClear=3
[E3-clear] checkAlive=0 roomAll=0 roomClear=0
```

生成方式：`EnemyDatabase.GetOrLoadByName("BulletMan")` + `AIActor.Spawn(prefab, pos, room, correctForWalls:true, AwakenAnimationType.Spawn, autoEngage:true)`。  
清除方式：对 3 个 actor 调用 `healthHaver.ApplyDamage(99999, ..., ignoreDamageCaps:true)`。

注意：E3 验证的是“能刷 3 个并清掉”的能力；**固定房间选取尚未实现**，当前用的是玩家所在房间。固定房间/固定出生点属于下一阶段。

## E4 — 子弹枚举

方案：

- 游戏自带 `StaticReferenceManager.AllProjectiles`（`ReadOnlyCollection<Projectile>`），由 `Projectile.Start/OnDespawned/OnDestroy` 维护。
- 同时用 HarmonyX 挂 `Projectile.Start`、`OnDespawned`、`OnDestroy` 的 postfix，维护 `HashSet<Projectile>` 和 `added/removed` 计数。
- 按 F6 对比三种来源。

实测：

```text
[E4-projectiles] static=1 harmony=1 find=1 added=94 removed=93 room=3
```

结论：

- Harmony 生命周期列表与游戏内置列表、`FindObjectsOfType<Projectile>()` 三者一致。
- 正式 RL 观测优先用 `StaticReferenceManager.AllProjectiles`；Harmony 补丁保留作为交叉校验/备用。
- `FindObjectsOfType` 只适合调试，不适合每帧调用。

## E5 — 时间加速

实测：

```text
[E5-timescale] timescale=2.0
[E5-timescale] timescale=1.0
```

用户在前台以 2x 移动 + 翻滚 60 秒，确认无穿墙、动画卡住、翻滚距离异常；该时段 BepInEx 日志无 Exception/NaN。  
说明：E5 期间 F8 状态日志未开启，所以没有逐 tick 的 `timescale=2.00` 记录；判定依据是切换日志 + 60 秒人工观察 + 无异常日志。下一阶段建议在 2x 下加自动计时/断言。

## 交付物

1. `game/EtgGym/`：可编译 BepInEx 插件
   - `EtgGym.csproj`（net35，引用游戏原版 DLL）
   - `Plugin.cs`
   - 已部署到 `D:\steam\steamapps\common\Enter the Gungeon\BepInEx\plugins\EtgGym\EtgGym.dll`
2. `docs/game-api-notes.md`：已确认的类/字段/方法签名
3. `docs/s0-report.md`：本文件
4. `scripts/build_deploy.ps1`：一键构建并部署
5. `backup/saves_20261108_101346.zip`：S0 开始前的存档备份

## 插件按键

| 键 | 功能 |
| --- | --- |
| `F1` | QuickRestart（仅在有 `PrimaryPlayer` 时生效，避免主菜单误触） |
| `F2` | 紧急复位：清空移动/射击/瞄准注入，`Time.timeScale=1` |
| `F3` | 清除本次 F4 生成的 3 个 Bullet Kin |
| `F4` | 在玩家上方生成 3 个 Bullet Kin |
| `F5` | `Time.timeScale` 1x / 2x 切换 |
| `F6` | 打印子弹统计（Static / Harmony / FindObjectsOfType） |
| `F7` | 打印 EnemyDatabase 中名字含 Bullet/Kin 的条目 |
| `F8` | 状态日志开关（写文件，不显示在画面上） |
| `F9` | 移动注入测试（左右各 1 秒，共 4 秒） |
| `F10` | 翻滚注入测试（向右） |
| `F11` | 射击注入测试（切 0 号枪，持续约 3 秒） |
| `F12` | 瞄准注入测试（固定右上方）——**注意 Steam 默认用 F12 截图，可能截走按键** |

## 构建与部署

```powershell
cd D:\AI_dungen
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1
```

默认游戏目录：`D:\steam\steamapps\common\Enter the Gungeon`；可用 `-GameDir` 覆盖。部署前需关闭游戏，否则 DLL 被占用。

## 已知问题与处理

1. **主菜单调用 QuickRestart 会导致 NRE + 卡加载**  
   本机复现；已将 F1 加 `PrimaryPlayer != null` 保护。若再次卡住，恢复存档备份后重启。
2. **存档已被恢复**  
   恢复 QuickRestart 卡死期间写坏的存档后，已用 `backup/saves_20261108_101346.zip` 还原；当前存档为 S0 开始前状态。
3. **失焦自动暂停**  
   游戏失去焦点时 `Time.timeScale=0`，所有输入注入无效。测试/训练时保持游戏前台，或设 `GameManager.Instance.PreventPausing = true`。
4. **BepInEx 控制台抢焦点**  
   测试时隐藏 Console，只看 `BepInEx\LogOutput.log`。
5. **Steam F12 截图热键**  
   游戏内瞄准测试不要用物理 F12；用其他键或 `PostMessage` 直接发窗口消息。
6. **版本字符串差异**  
   本机实际为 2.1.9（build 3829397），不是 2.1.7；API 以本机反编译和实测为准。

## 下一阶段（不在 S0 范围）

- 固定房间/固定出生点与 3 个 Bullet Kin 的固定布局。
- TCP + JSON 通信层（长度前缀/换行分隔）。
- 观测：grid 7x64x64 + scalars 20 + rays 32。
- 动作：`MultiDiscrete([9,16,2,2,2,2])`，动作重复 4 帧。
- Python 3.12 + Gymnasium + SB3 + PyTorch cu128 环境（工作目录已有 `.venv`：Python 3.12.16、torch 2.11.0+cu128、RTX 5060 Ti 可用；S0 未使用）。
- 2x 时间加速下的稳定性自动断言（碰撞/动画/翻滚距离）。
