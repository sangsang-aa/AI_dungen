# S1-B 房间软重置

> 状态：✅ 完成（2026-11-08）
> 上游：`docs/NEXT.md` 任务 2、`docs/s1-plan.md` 验收 B
> 禁止 `QuickRestart`。

## 目标

把当前房间恢复到初始状态：清敌人、清子弹、复位玩家坐标/血量/状态、重新生成 3 个 Bullet Kin；不重新加载整局。

## 实现

- `ArenaController.SoftReset(player)`：
  1. `ClearAllEnemiesInRoom`：`DeregisterEnemy(actor, true)` + `EraseFromExistence(true)`。
  2. `ClearProjectiles`：遍历 `StaticReferenceManager.AllProjectiles`，`SpawnManager.Despawn`，失败再 `Destroy`。
  3. 玩家回满血；`TestMode` 时保持无敌。
  4. 走现有 `Setup`：传送固定点、修复枪/手/输入、生成 3 个 Bullet Kin。
- 调试键：`P` 单次软重置；`T` 20 次软重置测试。

## 验收标准

- [x] 重置后 1 秒内玩家可移动/翻滚（20 次循环无锁死；K/L 已修复输入残留）。
- [x] 敌人数量、`StaticReferenceManager.AllProjectiles` 回到基线：`roomAll=3`、`bullets=0`。
- [x] 连续重置 20 次不损坏存档、不泄漏对象。
- [x] 全程不调用 `QuickRestart`。
- [x] 存档仍可正常读取（T 完成后关闭游戏，用 Steam 重启成功进入）。

## 证据

```text
S1-B] soft reset test DONE cycles=20/20
soft_reset_ok=20
clear enemies 3->0（19 次；首轮 0->0）
player world=(63.25,39.13)（20 次稳定）
clear projectiles before=0/1 -> after=0（20 次）
counts after-spawn roomAll=3 bullets=0
```

**关键 API 发现**：`SpawnManager.Despawn` 只回池，不保证调用 `Projectile.OnDespawned`；必须先显式 `p.OnDespawned()`，否则 `StaticReferenceManager.AllProjectiles` 会累积已回池子弹（实测 9 个不消失）。
