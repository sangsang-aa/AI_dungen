# AGENTS.md — 项目工作协议

> 任何 AI 或人接手本项目，先读这里，再读 `docs/STATE.md` 和 `docs/NEXT.md`。
> 目标：让项目在上下文压缩、换会话、换人之后仍能继续。

---

## 项目一句话

用强化学习训练一个 AI，在《Enter the Gungeon》的单个固定房间里，只靠**移动 + 翻滚**存活 60 秒（Demo A）。

---

## 开始工作前（必须）

按顺序读：

1. `docs/STATE.md` — 现在在哪
2. `docs/NEXT.md` — 下一步做什么
3. `docs/DECISIONS.md` — 为什么这么做（至少读最近 10 条）
4. `docs/GLOSSARY.md` — 术语
5. 当前阶段相关：`docs/game-api-notes.md`、`docs/s1-plan.md`

然后用 **5 句话**复述：

- 项目目标
- 当前阶段
- 已完成什么（带证据）
- 下一步做什么（带验收）
- 已知坑 / 不要做的事

**复述完等用户确认，再开始第一个任务。**

---

## 工作规则

1. **一次只做一个任务**；先写验收标准，再写代码。
2. 不确定的游戏 API 先查 `docs/game-api-notes.md` 或 ILSpy 反编译，**不要猜**。
3. 完成后必须更新 `STATE.md`、`NEXT.md`、`SESSION_LOG.md`；新概念写进 `GLOSSARY.md`。
4. 不修改与当前任务无关的文件。
5. 不擅自换技术路线（纯视觉、ViGEm、`QuickRestart` 重置等），先写进 `DECISIONS.md` 讨论。
6. 每个阶段结束，用 3 句话说明：**改了什么 / 为什么 / 用户怎么验收**。
7. **没有写进文件的工作 = 没发生。**

---

## 技术栈（已定）

| 层 | 选择 |
|---|---|
| 游戏 | Enter the Gungeon 2.1.9（Steam build 3829397），Unity 2017.4.27f1，Mono x64 |
| Mod 框架 | BepInEx 5.4.21.0 x64 + HarmonyX |
| 插件 | C# **net35**，`game/EtgGym/` |
| 通信 | localhost TCP + JSON（S1） |
| 训练 | Python 3.12 + Gymnasium + stable-baselines3 + PyTorch cu128 |
| 观测 | grid `7×64×64` + scalars `20` + rays `32` |
| 动作 | `MultiDiscrete([9,16,2,2,2,2])`，动作重复 4 帧 |

---

## 路径

| 内容 | 路径 |
|---|---|
| 游戏 | `D:\steam\steamapps\common\Enter the Gungeon` |
| 插件源码 | `game/EtgGym/` |
| 部署目标 | `<游戏>\BepInEx\plugins\EtgGym\EtgGym.dll` |
| 日志 | `<游戏>\BepInEx\LogOutput.log` |
| Python venv | `.venv\Scripts\python.exe` |
| 存档备份 | `backup/saves_20261108_101346.zip` |
| 学习计划 | `docs/learning-plan.md` |

---

## 常用命令

完整命令见 `docs/RUNBOOK.md`。最常用的三条：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1
Get-Content "D:\steam\steamapps\common\Enter the Gungeon\BepInEx\LogOutput.log" -Tail 50
.\.venv\Scripts\python.exe .\scripts\check_env.py
```

---

## 不要做

- ❌ 不要用 `QuickRestart` 做训练重置（本机已复现写坏存档）。
- ❌ 不要用纯像素输入替代结构化观测（除非 `DECISIONS.md` 记录原因）。
- ❌ 不要引入 ViGEmBus / vgamepad。
- ❌ 不要提交游戏 DLL、`.venv`、`decompiled/`、`backup/`。
- ❌ Demo A 阶段不做射击/清房/导航/商店/换层。
- ❌ 不要把 F 键当训练接口（F12 与 Steam 截图冲突）。
- ❌ 不要在失焦/暂停状态下判断“注入失败”。

---

## 完成定义（DoD）

一个任务算完成，必须同时满足：

1. 有明确的验收命令或操作步骤；
2. 验收通过，并留下证据（日志 / 截图 / 输出）；
3. `docs/STATE.md` 看板已更新；
4. `docs/SESSION_LOG.md` 已追加；
5. 用户能用 3 句话复述这次改动的目的和结果。

---

## 收尾协议

每次会话结束前执行：

1. 更新 `docs/STATE.md`
2. 更新 `docs/NEXT.md`
3. 追加 `docs/SESSION_LOG.md`
4. 新概念写入 `docs/GLOSSARY.md`
5. 用 3 句话向用户汇报
