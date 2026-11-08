# 项目状态（STATE）

> 这是项目的“唯一事实来源”。
> 每次会话结束更新；新会话先读它，不要靠聊天记录回忆。

- **更新时间**：2026-11-08
- **当前阶段**：S1-A/B 完成，S1-C TCP + JSON 待开始
- **一句话**：固定 seed 房间 + 3 敌人 + 20 次一致性、20 次软重置均已通过；下一步做 TCP 最小闭环。

---

## 看板

| 模块 | 状态 | 证据 / 文件 |
|---|---|---|
| BepInEx + 插件加载 | ✅ 完成 | `BepInEx/LogOutput.log`，`EtgGym 0.1.0 loaded` |
| E1 输入注入（移动/翻滚/射击） | ✅ 通过 | 位移 5.563；`ForceStartDodgeRoll=True`；射击 `bullets=1` |
| E2 瞄准 | ✅ 通过 | `forceAimPoint=(209.13,82.44)`，`unadjustedAimPoint` 稳定 |
| E3 生/清敌人 | ✅ 通过 | 3 个 `BulletMan(Clone)`，清理后 `roomAll=0` |
| E4 子弹枚举 | ✅ 通过 | `static=1, harmony=1, find=1`，累计 `added=94/removed=93` |
| E5 2x 加速 | ✅ 通过 | `timescale=2.0` 跑 60 秒，无穿模/卡动画 |
| Python 训练环境 | ✅ 完成 | Python 3.12.16，torch 2.11.0+cu128，SB3 2.9.2 |
| S1-A 固定房间 + 出生点 | ✅ 完成 | 固定 seed `20261108`；20/20；两轮重启 base/local 完全一致 |
| S1-B 房间软重置 | ✅ 完成 | `T` 20/20；清敌 3→0；子弹 after=0；退出再进成功 |
| S1-C TCP + JSON 通信 | ⬜ 未开始 | 长度前缀/换行分隔 |
| S1-D Python `EtgGymEnv` | ⬜ 未开始 | Gymnasium 封装 |
| S1-E 无人值守 + 看门狗 | ⬜ 未开始 | 10 分钟 `timeScale` 不为 0 |
| S2 观测 + 奖励 | ⬜ 未开始 | grid 7×64×64 + scalars 20 + rays 32 |
| S3 PPO 训练 | ⬜ 未开始 | SB3 `MultiInputPolicy` |

---

## 已完成（带证据）

### S0：游戏侧可行性

- 报告：`docs/s0-report.md`
- API 笔记：`docs/game-api-notes.md`
- 结论：内部 forced-input 路线成立，**不需要 ViGEmBus**。

### S1-A 固定房间（完成）

- 固定 seed `20261108`：两轮重启 `base=(11,84) dims=(20,20)`、玩家 local `(9.25,9.13)`、3 敌人 local 完全一致。
- 20/20 次一致性测试：`cycles=20/20`，`roomAll=3/0`。
- 事故与修复：运行时换角色导致枪/手错位（ADR-014）；`PreventPausing=true` 导致朝向/射击锁死（ADR-015）。
- 未验证项：真实选人界面按 F4 自动选 Pilot（手动选 Pilot 已验证）。

### 插件与控制

- 源码：`game/EtgGym/`
- 部署：`D:\steam\steamapps\common\Enter the Gungeon\BepInEx\plugins\EtgGym\EtgGym.dll`
- 构建：`scripts/build_deploy.ps1`
- 按键：F1 运行时换角色已禁用；K/L 修复/恢复；O 保存彩虹关闭；J 20 次一致性测试（仅调试）

### Python 环境

- 位置：`.venv\Scripts\python.exe`
- 版本：Python 3.12.16 / torch 2.11.0+cu128 / SB3 2.9.2 / Gymnasium 1.4.0 / TensorBoard 2.21.0
- 自检：`scripts/check_env.py` 输出 `ALL OK`
- GPU：RTX 5060 Ti，capability `(12, 0)`

### 文档与学习

- 学习计划：`docs/learning-plan.md`
- S1 计划：`docs/s1-plan.md`
- 工作协议：`AGENTS.md`

---

## 已知问题 / 坑

| 问题 | 影响 | 处理 |
|---|---|---|
| 失焦时 `Time.timeScale=0` | 所有注入失效 | S1 用 `ForceUnpause` watchdog；**禁用 `PreventPausing=true`**（会锁瞄准/射击） |
| `forceAimPoint` 是 `Vector2?` | 写成 `Vector3` 会编译失败 | 统一用 `Vector2` |
| F12 是 Steam 截图热键 | 调试键被截走 | 训练不依赖 F 键；改用配置/模式开关 |
| `QuickRestart` 可能写坏存档 | 存档异常 | **训练禁用**；改用房间软重置 |
| 运行时换角色（ClearPrimaryPlayer/prefab） | 玩家对象/枪挂点/输入损坏 | **禁止**；Pilot 只能在正常选人界面选（ADR-014） |
| BepInEx 控制台抢焦点 | 游戏暂停 | 关闭控制台，只写文件日志 |
| `XInputInterface32.dll` x86 报错 | 日志噪音 | ETG 自带，**无害**，不要修 |

---

## 不要做

- 不要用 `QuickRestart` 做重置。
- 不要引入 ViGEmBus / vgamepad。
- 不要改回纯像素输入（除非 `DECISIONS.md` 记录原因）。
- 不要在 Demo A 阶段做射击/清房/导航。
- 不要把 F 键当训练接口。
- 不要在暂停状态下判断注入失败。

---

## 关键路径

```text
游戏可控 ✅
   ↓
固定房间 + 固定出生点（S1-A）
   ↓
房间软重置（S1-B）
   ↓
TCP + JSON 闭环（S1-C）
   ↓
Python EtgGymEnv（S1-D）
   ↓
观测 + 奖励（S2）
   ↓
PPO 训练（S3）
   ↓
Demo A：存活 60 秒
```

---

## 下一步

见 `docs/NEXT.md`。
