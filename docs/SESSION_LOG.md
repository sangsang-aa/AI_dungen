# 会话日志（SESSION_LOG）

> 只追加，不修改历史。
> 每次会话结束追加一条；没有记录的工作，视为没发生。

## 记录模板

```markdown
## YYYY-MM-DD 会话主题
- 做了什么：
- 为什么：
- 结果 / 证据：
- 遗留问题：
- 下一步：
```

---

## 2026-11-08 S0：游戏侧可行性验证

- **做了什么**：搭 BepInEx + `EtgGym 0.1.0` 插件，完成 E1–E5 五个实验，重新构建并部署。
- **为什么**：验证能否在不使用虚拟手柄的情况下稳定控制游戏；这是整个项目的 go/no-go。
- **结果 / 证据**：
  - E1 移动位移 5.563；`ForceStartDodgeRoll=True`；射击 `bullets=1`。
  - E2 `forceAimPoint=(209.13,82.44)`，`unadjustedAimPoint` 稳定。
  - E3 生成 3 个 `BulletMan(Clone)`，清理后 `roomAll=0`。
  - E4 `static=1, harmony=1, find=1`，累计 `added=94/removed=93`。
  - E5 `timescale=2.0` 跑 60 秒，无穿模/卡动画。
  - 详见 `docs/s0-report.md`、`docs/game-api-notes.md`。
- **遗留问题**：
  - 失焦会暂停（`Time.timeScale=0`），S1 必须加看门狗。
  - `QuickRestart` 可能写坏存档，训练禁用。
  - F12 与 Steam 截图冲突。
- **下一步**：S1 固定房间 + 软重置 + TCP。

---

## 2026-11-08 Python 训练环境安装

- **做了什么**：用 uv 安装 Python 3.12.16，在 `D:\AI_dungen\.venv` 建虚拟环境；安装 PyTorch 2.11.0+cu128、SB3 2.9.2、Gymnasium 1.4.0、TensorBoard 2.21.0。
- **为什么**：为 S2/S3 的训练和 CartPole/LunarLander 学习样例做准备；系统 Python 3.14 不动。
- **结果 / 证据**：
  - `scripts/check_env.py` 输出 `ALL OK`。
  - CUDA 可用，GPU 为 RTX 5060 Ti，capability `(12, 0)`。
  - `.venv` 约 4.45 GB，已加入 `.gitignore`。
- **遗留问题**：系统仍默认 Python 3.14；使用时要显式调用 `.venv\Scripts\python.exe`。
- **下一步**：按 `docs/learning-plan.md` 跑 CartPole。

---

## 2026-11-08 建立项目记忆系统

- **做了什么**：新增 `AGENTS.md`、`docs/STATE.md`、`docs/NEXT.md`、`docs/DECISIONS.md`、`docs/GLOSSARY.md`、`docs/SESSION_LOG.md`、`docs/RUNBOOK.md`。
- **为什么**：防止上下文压缩/换会话后丢失进度和决策；让项目可以靠文件恢复。
- **结果 / 证据**：新会话按 `AGENTS.md` 的开场协议，5 句话复述当前状态即可继续。
- **遗留问题**：需要每次会话坚持更新，否则文件会过期。
- **下一步**：S1 任务 1（固定房间 + 出生点）。

---

## 2026-11-08 S1-A：固定房间 + 出生点 + 20 次一致性

- **做了什么**：
  - 新增 `Arena/ArenaController.cs`：入口房间识别、玩家传送、3 个 Bullet Kin 固定偏移生成、清除、枪/手修复。
  - `J` 内置 20 次一致性测试；`H` 测试模式（无敌）；`O` 保存彩虹模式关闭。
- **为什么**：把“固定房间 + 3 敌人”做成可重复的 S1-A 基础。
- **结果 / 证据**：
  - `consistency test DONE cycles=20/20`；`roomAll` 生成后 3、清除后 0；玩家/敌人局部坐标 20 次一致。
  - 彩虹模式：`rainbowRunToggled True -> False IsRainbowRun=False; Save=True`。
  - 跨重启：固定 seed `20261108`，两轮 `base=(11,84) dims=(20,20)`、玩家/敌人 local 完全一致；Task 1 完成。
- **事故 / 修复**：
  - 运行时 `ClearPrimaryPlayer + LoadCharacterSelect` 换 Pilot → 玩家对象/枪挂点损坏、无法射击。已禁用运行时换角色（ADR-014）。
  - `PreventPausing=true` → `AcceptingNonMotionInput=false`，朝向/射击锁死。已移除全部赋值，改 `ForceUnpause` watchdog（ADR-015）。
- **遗留问题**：
  - `Knav3` 枪本身状态正常（ammo/infinite/挂点均正常）；锁定已修复，射击是否恢复待用户最终确认。后续用 Pilot 起始枪做基线。
- **下一步**：房间软重置。

---

## 2026-11-08 S1-B：房间软重置

- **做了什么**：
  - 新增 `ArenaController.SoftReset`：清当前房间全部敌人、清全部子弹、回满血、走 `Setup` 复位玩家并重新生成 3 敌人。
  - `P` 单次软重置；`T` 20 次软重置测试（测试模式无敌）。
- **为什么**：训练 episode 重置不能用 `QuickRestart`，必须房间内软重置。
- **结果 / 证据**：
  - `soft reset test DONE cycles=20/20`；`soft_reset_ok=20`。
  - 清敌 `3->0`（19 次；首轮 0->0）；`roomAll=3`、`bullets=0`。
  - 玩家世界坐标 20 次稳定 `(63.25,39.13)`；`clear projectiles after=0` 20 次。
  - 退出游戏后 Steam 重启成功，存档可读。
- **关键 API 发现**：`SpawnManager.Despawn` 只回池，不保证调用 `Projectile.OnDespawned`；必须先 `p.OnDespawned()`，否则 `AllProjectiles` 会累积已回池子弹（实测 9 个不消失）。
- **遗留问题**：无（Task 2 验收通过）。
- **下一步**：Task 3 TCP + JSON 最小闭环。
