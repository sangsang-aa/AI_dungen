# 术语表（GLOSSARY）

> 规则：每引入一个新概念，就在这里加一行。
> 写法：一句话解释 + 在本项目里对应什么。不要写教科书长段。

---

## RL 术语

| 术语 | 一句话 | 在本项目里 |
|---|---|---|
| Agent | 做决策的 AI | 我们的 PPO 策略网络 |
| Environment | 接收动作、返回状态和奖励的世界 | 游戏 + BepInEx 插件 |
| Observation / State | AI 看到的局面 | 栅格图 + 标量 + 最近子弹射线 |
| Action | AI 能做的操作 | 移动 / 瞄准 / 射击 / 翻滚 / 换弹 / 交互 |
| Reward | 每一步的分数 | 存活加分、受伤扣分、死亡大扣分 |
| Episode | 一局 | reset 到死亡 / 清房 / 超时 |
| Policy (π) | 状态到动作的映射 | 神经网络 |
| Value (V) | 这个局面未来能拿多少分 | Critic 网络 |
| Advantage (A) | 这个动作比“平均水平”好多少 | PPO 的核心学习信号 |
| Return / Discount (γ) | 未来奖励打折后加起来 | γ=0.99 |
| Policy Gradient | 提高“好动作”的概率 | PPO 的优化目标 |
| PPO | 一种稳定、常用的策略梯度算法 | SB3 里的默认算法 |
| Clip | 限制每次策略更新的幅度 | `clip_range=0.2` |
| GAE | 一种低方差的优势估计 | `gae_lambda=0.95` |
| Entropy | 策略的随机程度，用来鼓励探索 | `ent_coef=0.01` |
| On-policy | 只用当前策略产生的数据 | PPO 属于这一类 |
| Off-policy | 可以用旧策略的数据 | DQN / SAC |
| Reward Shaping | 加中间奖励引导学习 | 伤害、击杀、存活分 |
| Reward Hacking | AI 钻奖励空子 | 贴墙转圈、不打敌人 |
| Curriculum | 先易后难 | 1 敌人 → 3 敌人 → 随机 |
| Action Repeat | 同一个动作重复几帧 | 每 4 帧决策一次（约 15Hz） |
| Vector Env | 同时跑多个环境 | 后期多开游戏实例 |
| Behavior Cloning (BC) | 用示范数据做监督学习 | 人类 / 规则 bot 录像预训练 |
| Baseline | 用来对照的简单策略 | 随机动作、规则 bot |
| TensorBoard | 看训练曲线的工具 | `tb/` 目录 |

---

## 项目术语

| 术语 | 一句话 | 在本项目里 |
|---|---|---|
| BepInEx | Unity Mono 游戏的 mod 框架 | 5.4.21.0 x64 |
| Harmony / HarmonyX | 运行时给游戏方法打补丁 | 交叉校验子弹生命周期 |
| net35 | .NET Framework 3.5 目标框架 | 插件必须用，和游戏运行时一致 |
| forced-input | 游戏自带的“强制输入”通道 | `usingForcedInput` / `forcedInput` |
| PreventPausing | 陷阱字段：true 会禁用非移动输入 | 会让 `AcceptingNonMotionInput=false`，瞄准/射击失效；训练中禁用 |
| `forceAimPoint` | 强制瞄准点 | 类型是 `Vector2?`，不是 `Vector3` |
| `StaticReferenceManager` | 游戏维护的全局对象列表 | `AllProjectiles` / `AllEnemies` |
| `AIActor` | 敌人基类 | Bullet Kin 也是 `AIActor` |
| Bullet Kin / BulletMan | 第一层基础敌人 | prefab 名 `BulletMan`，guid `01972dee…dad5` |
| `RoomHandler` | 房间逻辑（`Dungeonator` 命名空间） | `GetActiveEnemies` / `IsSealed` |
| Soft Reset | 房间软重置 | 清怪、清弹、复位玩家，不重载整局 |
| IPC | 进程间通信 | C# ↔ Python 的 TCP + JSON |
| Watchdog | 看门狗 | 检测暂停并在 1 秒内恢复 |
| `Time.timeScale` | 全局时间缩放 | 0 = 暂停；2 = 2 倍速 |
| Grid | 以玩家为中心的局部栅格 | `7×64×64` |
| Scalars | 一维数值向量 | 血量、弹药、冷却等 20 维 |
| Rays | 最近子弹的极坐标特征 | 最近 8 颗子弹 × 4 个值 = 32 |
| Pilot | 固定训练角色（飞行员） | prefab path `PlayerRogue`；所有训练只用 Pilot，非 Pilot 拒绝布场 |
| Character Select | 开局选人界面 | F4 自动选中 Pilot；走正常 `CharacterSelectController.Do()` |
| Demo A | 第一阶段目标 | 单房间、只移动/翻滚、存活 60 秒 |
| S0 / S1 / S2 / S3 | 项目阶段 | 可行性 / 通信 / 观测奖励 / 训练 |
| DoD | Definition of Done | 见 `AGENTS.md` |

---

## 容易混淆

| 说法 | 正确理解 |
|---|---|
| “AI 训练” | 本项目是**强化学习**，不是监督学习；没有标注数据，靠奖励试错 |
| “模型越大越好” | 不是；小 CNN 足够，瓶颈在环境和奖励 |
| “loss 下降 = 变强” | 不一定；看 `ep_rew_mean` 和评估结果 |
| “4x 加速一定更快” | 不一定；可能穿模/卡物理，先验证 2x |
| “F12 测试瞄准” | F12 被 Steam 截图占用，训练不依赖 F 键 |
