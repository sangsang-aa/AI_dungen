# 下一步（NEXT）

> 最多 3 个任务。按顺序做，完成一个再开下一个。
> 每个任务必须先写验收标准，再写代码。

- **更新时间**：2026-11-08
- **当前阶段**：S1
- **总目标**：固定房间 + 房间软重置 + TCP 最小闭环

---

## 任务 1：固定房间 + 固定出生点 + 3 个 Bullet Kin

**状态**：✅ 完成（2026-11-08，固定 seed `20261108`）  
**证据**：两轮重启 `base=(11,84) dims=(20,20)`、玩家 local `(9.25,9.13)`、3 敌人 local 完全一致；`cycles=20/20`，`roomAll=3/0`。  
**未验证**：真实选人界面按 F4 自动选 Pilot（手动选 Pilot 已验证）。

**目标**
一键进入 1-1 的指定房间，把玩家传送到固定坐标，在固定偏移生成 3 个 Bullet Kin。

**改哪些文件**
- `game/EtgGym/Plugin.cs`、`game/EtgGym/Arena/ArenaController.cs`

**验收标准**
- [x] 触发后，日志出现固定房间标识、玩家固定坐标、3 个敌人固定坐标（20/20）。
- [x] 连续重置 20 次，敌人数量/位置/房间状态一致（`roomAll=3/0`）。
- [x] 重启游戏后仍能回到同一房间配置（固定 seed `20261108`）。
- [x] 固定 seed 下可复现；未调用 `QuickRestart`。

**风险**
- 固定 seed 会固定掉落/敌人等内容；泛化训练后续再评估随机 seed。
- 传送后可能触发房间进入逻辑，已实测 20 次无重复刷怪。

---

## 任务 2：房间软重置

**状态**：⬜ 当前任务（Task 1 已完成）  
**已有基础**：`ArenaController.ClearSpawned` 已用 `DeregisterEnemy + EraseFromExistence`；玩家传送/枪修复已可用。  
**待做**：清子弹、复位玩家血量/状态、重新生成 3 敌人、20 次无泄漏/存档检查。

**目标**
不调用 `QuickRestart`，把当前房间恢复到初始状态：清敌人、清子弹、复位玩家坐标/血量/状态、重新生成 3 个 Bullet Kin。

**改哪些文件**
- `game/EtgGym/Plugin.cs`
- `game/EtgGym/Arena/`（新增重置逻辑）

**验收标准**
- [ ] 重置后 1 秒内玩家可移动/翻滚。
- [ ] 敌人数量、`StaticReferenceManager.AllEnemies/AllProjectiles` 计数回到基线。
- [ ] 连续重置 20 次不损坏存档、不泄漏对象。
- [ ] 全程不调用 `QuickRestart`。
- [ ] 存档仍可正常读取（重置后退出再进）。

**预计**：4–6 小时

**风险**
- 敌人是池化对象，直接 Destroy 可能留下引用；优先用 `DeregisterEnemy` / `ForceDeath`。
- 玩家状态覆盖可能被游戏每帧重建，需要参考 `PlayerRuntimeOverrideServiceBase` 的思路。

---

## 任务 3：TCP + JSON 最小闭环

**目标**
C# 插件作为 server，Python 作为 client；每 4 帧发送一次观测，收到动作后注入。先不接训练。

**改哪些文件**
- `game/EtgGym/Net/TcpServer.cs`（新增）
- `game/EtgGym/Plugin.cs`
- `python/etg_gym/ipc.py`（新增）
- `scripts/echo_test.py`（新增）

**验收标准**
- [ ] Python 能收到观测 JSON，字段与 `docs/s1-plan.md` 一致。
- [ ] Python 发送动作后，游戏内角色有可见反应。
- [ ] 连续运行 10 分钟不断连。
- [ ] 断开后能重连。
- [ ] 单步 action→obs 往返 p95 < 5 ms（记录实测值）。
- [ ] 无 F 键依赖。

**预计**：6–8 小时

**风险**
- Unity 主线程不能直接阻塞等网络；用“主线程读最新动作 + 后台线程收发”的异步模型。
- 失焦暂停会让闭环看起来“卡住”；先完成任务 4（看门狗）或测试时保持前台。

---

## 本轮不做

- ❌ `EtgGymEnv`（Gymnasium 封装）
- ❌ 观测栅格化 / 奖励函数
- ❌ PPO 训练
- ❌ 射击 / 清房 / 导航

这些等上面三个任务完成、并且 10 分钟无人值守稳定后再开。

---

## 完成一个任务后

1. 更新 `docs/STATE.md` 看板。
2. 追加 `docs/SESSION_LOG.md`。
3. 把新的 API/概念写进 `docs/GLOSSARY.md` / `docs/game-api-notes.md`。
4. 用 3 句话向用户汇报：改了什么 / 为什么 / 怎么验收。
