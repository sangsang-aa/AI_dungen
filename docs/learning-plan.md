# ETG AI 学习计划（个人版）

> 适用对象：不熟悉 AI 训练、但要独立看懂并调通本项目的开发者。
> 目标项目：`D:\AI_dungen` 的 Demo A（单房间纯躲避）。
> 建议投入：每天 1–2 小时，14 天建立直觉，之后边做边补。

---

## 0. 先回答：要不要先跑通一个样例？

**要，但只需要 1–2 天，而且目的不是"学会"，是建立"训练回路"的直觉。**

正确顺序：

```
跑通最小样例（CartPole）
      ↓
看懂训练曲线长什么样
      ↓
边做项目、边补理论
      ↓
遇到问题再回头查书/论文
```

反例：先读三个月书再动手的人，通常在第一次训练不收敛时就放弃了，因为他没见过"正常训练"是什么样。

你不需要"学完 RL"才能开始。你需要的是：

1. 知道一次训练循环里发生了什么；
2. 能看懂 `ep_rew_mean` 曲线；
3. 知道训练不涨时先查什么。

这三件事，**CartPole + LunarLander 两天就能给你**。

---

## 1. 你要学的 4 块内容

| 模块 | 学到什么程度 | 不用学什么 | 对应项目 |
|---|---|---|---|
| **RL 概念** | 状态/动作/奖励/回合、折扣、探索与利用、策略梯度直觉 | 从零推导贝尔曼方程 | obs / action / reward 设计 |
| **深度学习基础** | 张量、CNN 直觉、反向传播"是什么"、归一化 | 手写反向传播、CUDA | 栅格图输入 |
| **工具链** | Gymnasium API、SB3、TensorBoard | 自己造训练框架 | `EtgGymEnv` 封装 |
| **工程与调参** | 奖励塑形、课程学习、动作重复、debug 曲线 | 追 SOTA、读大量论文 | 让 AI 真的学会 |

### 明确不用学

- ❌ 不用先读完 Sutton & Barto 整本书（当字典查，重点第 3、4、13 章）。
- ❌ 不用自己实现 PPO（先用 SB3，再读 CleanRL）。
- ❌ 不用学 Transformer、LLM、扩散模型。
- ❌ 不用读几十篇论文，**PPO 一篇 + 37 个实现细节**就够。
- ❌ 不用追求"最新算法"，PPO 对这个项目足够。
- ❌ 不用把数学补到能做证明，够用即可。

---

## 2. 样例阶梯（从易到像）

不要一上来就接真游戏。按这个阶梯走，每一层只学一个新东西。

| 层级 | 环境 | 学到什么 | 时间 | 和项目的对应 |
|---|---|---|---|---|
| **L0** | `CartPole-v1` | Gymnasium API、PPO 跑通、看曲线 | 半天 | 无（建立直觉） |
| **L1** | `LunarLander-v3` | 连续动作、奖励塑形、调参 | 1–2 天 | 动作 / 奖励设计 |
| **L2** | 自建 `GridWorld` | 自己写 env、定义 obs/action space | 1 天 | `EtgGymEnv` 骨架 |
| **L3** | 假 ETG 环境（Dict + CNN） | `MultiInputPolicy`、图像+向量观测 | 2–3 天 | 观测设计 |
| **L4** | 真 ETG | IPC、延迟、可复现、重置 | 项目 S1+ | 正式项目 |

- **L0–L1 必做**：这是"见过正常训练"的最低成本。
- **L2–L3 强烈建议**：这是你和项目之间唯一的桥。
- **L4 就是项目本身**，不需要额外"学习样例"。

---

## 3. 14 天日程（每天 1–2 小时）

### 第 1 周：建立直觉 + 跑通工具链

| 天 | 学（30–45 min） | 练（30–60 min） | 验收 |
|---|---|---|---|
| **D1** | Spinning Up《Intro to RL》 | 用 5 句话写下 agent/env/reward | 能说清 state/action/reward |
| **D2** | SB3《Getting Started》 | 装环境，跑 CartPole PPO | `ep_rew_mean` > 200 |
| **D3** | Gymnasium《Basic Usage》《Spaces》 | 改 CartPole 的 reward，观察曲线 | 能解释曲线为什么变 |
| **D4** | Gymnasium《Wrappers》《Vector Env》 | 用 4 个并行环境训练 | 训练速度明显提升 |
| **D5** | SB3《Examples》 | 跑 LunarLander，保存/加载模型 | 能复现同一结果 |
| **D6** | Spinning Up《PPO》 | 对照 SB3 参数，画 PPO 数据流图 | 能说出每个超参作用 |
| **D7** | CleanRL `ppo.py` | 逐行注释，跑一遍 | 能解释 clip / GAE |

### 第 2 周：贴近自己的项目

| 天 | 学（30–45 min） | 练（30–60 min） | 验收 |
|---|---|---|---|
| **D8** | 《37 Implementation Details of PPO》 | 记 10 条"坑" | 能说出 PPO 难调在哪 |
| **D9** | SB3《Tips and Tricks》 | 做"随机策略 vs 训练策略"对比 | 会读 TensorBoard |
| **D10** | Gymnasium《Custom Environment》 | 写一个 `GridWorld`，`check_env` 通过 | 随机策略能跑 |
| **D11** | SB3 `MultiInputPolicy` 文档 | Dict obs（图像 + 向量）训练 | 曲线能涨 |
| **D12** | 奖励塑形（蘑菇书对应章节） | 故意设计错奖励，观察 reward hacking | 会排查"作弊" |
| **D13** | 项目文档 `docs/` | 写 ETG 的 obs/action/reward 草案 | 草案能自洽 |
| **D14** | 动作重复 / 15Hz 决策 | 假 ETG 环境 + action repeat 跑 10 万步 | 端到端跑通 |

> 如果某天没完成，不要顺延整个计划；把"练"补上，"学"可以压缩。

---

## 4. 资料清单（按优先级）

| 资源 | 类型 | 用途 / 读哪部分 | 优先级 |
|---|---|---|---|
| [OpenAI Spinning Up](https://spinningup.openai.com/) | 网页 | 先读 *Intro to RL*、*RL Algorithms*、*PPO* | ★★★★★ |
| [Stable-Baselines3 文档](https://stable-baselines3.readthedocs.io/) | 文档 | *Getting Started*、*Examples*、*Tips and Tricks*、*Custom Env* | ★★★★★ |
| [CleanRL `ppo.py`](https://github.com/vwxyzjn/cleanrl/blob/master/cleanrl/ppo.py) | 代码 | 逐行读，单文件 PPO | ★★★★★ |
| [37 Implementation Details of PPO](https://iclr-blog-track.github.io/2022/03/25/ppo-implementation-details/) | 博客 | 解释 PPO 为什么难调 | ★★★★★ |
| [Gymnasium 文档](https://gymnasium.farama.org/) | 文档 | *Basic Usage*、*Spaces*、*Custom Environment*、*Wrappers* | ★★★★☆ |
| [蘑菇书 EasyRL](https://github.com/datawhalechina/easy-rl) | 中文书 | 第 1、2、7、8 章（策略梯度 / PPO） | ★★★★☆ |
| [动手学强化学习](https://hrl.boyuai.com/) | 中文书 | 第 1–3 章 + PPO 章节 | ★★★★☆ |
| [李宏毅强化学习课程](https://speech.ee.ntu.edu.tw/~hylee/ml/2021-spring.php) | 视频 | 前 4 讲，建立直觉 | ★★★★☆ |
| [Sutton & Barto](http://incompleteideas.net/book/the-book-2nd.html) | 书 | 查概念；重点 Ch 3、4、13 | ★★★☆☆ |
| [Karpathy: Zero to Hero](https://karpathy.ai/zero-to-hero.html) | 视频 | 补神经网络/反向传播直觉（可选） | ★★☆☆☆ |
| [3Blue1Brown 神经网络系列](https://www.youtube.com/playlist?list=PLZHQObOWTQDNU6R1_67000Dx_ZCJB-3pi) | 视频 | 视觉化理解（可选） | ★★☆☆☆ |

**读法建议：**

- Spinning Up 不要通读，按"概念 → PPO"跳读。
- SB3 文档当手册，用到哪查到哪。
- CleanRL 只读 `ppo.py` 一个文件，不要读整个仓库。
- 中文书用来补直觉，英文资料用来对齐术语。

---

## 5. 概念速查表（和项目一一对应）

| 概念 | 一句话解释 | 在我们项目里对应什么 |
|---|---|---|
| **Agent / Environment** | AI 做动作，环境返回新状态和奖励 | 插件 + 游戏 |
| **State / Observation** | AI 看到的局面 | 栅格图 + 标量 + 射线 |
| **Action** | AI 能做的操作 | 移动/瞄准/射击/翻滚/换弹 |
| **Reward** | 每一步的分数 | 存活、伤害、击杀、清房 |
| **Episode** | 一局游戏 | 从 reset 到死亡/清房/超时 |
| **Discount (γ)** | 未来的奖励打几折 | 通常 0.99 |
| **Policy (π)** | 状态到动作的映射 | 神经网络 |
| **Value (V)** | 这个局面未来能拿多少分 | Critic 网络 |
| **Advantage (A)** | 这个动作比平均水平好多少 | PPO 的核心信号 |
| **Policy Gradient** | 提高"好动作"的概率 | PPO 的优化目标 |
| **PPO Clip** | 限制每次更新的幅度 | `clip_range=0.2` |
| **Entropy** | 鼓励探索 | `ent_coef=0.01` |
| **GAE** | 一种低方差的优势估计 | `gae_lambda=0.95` |
| **On-policy** | 只用当前策略的数据 | PPO 属于这一类 |
| **Reward Shaping** | 加中间奖励引导学习 | 伤害/击杀/存活分 |
| **Reward Hacking** | AI 钻奖励空子 | 贴墙转圈、不打敌人 |
| **Curriculum** | 先易后难 | 1 敌人 → 3 敌人 → 随机 |
| **Action Repeat** | 同一个动作重复几帧 | 每 4 帧决策一次（15Hz） |
| **Vector Env** | 同时跑多个环境 | 多开游戏实例（后期） |
| **Behavior Cloning** | 用示范数据监督学习 | 人类/bot 录像预训练 |

---

## 6. 自测 10 问

答不上来就回看对应资料，不用死磕。

| # | 问题 | 回看 |
|---|---|---|
| 1 | 为什么要有 discount factor？ | Spinning Up《Intro》 |
| 2 | policy gradient 到底在优化什么？ | Spinning Up《Vanilla Policy Gradient》 |
| 3 | advantage 为什么比 return 方差小？ | Spinning Up《GAE》 |
| 4 | PPO 的 clip 解决什么问题？ | Spinning Up《PPO》/ 37 details |
| 5 | on-policy 和 off-policy 的区别？ | Spinning Up《RL Algorithms》 |
| 6 | 为什么动作重复（action repeat）有用？ | 项目 `docs/` |
| 7 | reward shaping 的最大风险是什么？ | 蘑菇书 / 项目文档 |
| 8 | 为什么 obs 要做归一化？ | SB3《Tips and Tricks》 |
| 9 | 训练曲线不涨，你按什么顺序排查？ | SB3 Tips / 37 details |
| 10 | 什么是课程学习（curriculum）？ | 项目 `docs/` |

**7/10 能答上来，就可以开始接真游戏。**

---

## 7. 训练不涨时的排查顺序（背下来）

1. **环境有没有 bug？** 随机策略能不能跑？`check_env` 过不过？
2. **奖励有没有信号？** 把奖励打印出来，看看随机策略和专家差多少。
3. **观测有没有信息？** 把 obs 可视化，确认 AI 能"看见"关键物体。
4. **动作有没有生效？** 固定动作序列，看游戏里是否真的动。
5. **超参是否离谱？** 先抄 SB3 默认值，再小步改。
6. **是不是 reward hacking？** 看录像，不要只看分数。
7. **是不是探索不足？** 提高 `ent_coef`，或加示范数据。
8. **是不是训练太短？** 看曲线趋势，不要看单点。

---

## 8. 环境安装（Windows，RTX 5060 Ti）

> 项目 S0 不需要 Python；这一节是为 L0–L3 学习准备的。

```powershell
# 1) 安装 uv（比 pip 干净，能并存多个 Python）
winget install astral-sh.uv

# 2) 安装 Python 3.12（不要用系统的 3.14）
uv python install 3.12

# 3) 在项目里建虚拟环境
cd D:\AI_dungen
uv venv --python 3.12 .venv
.\.venv\Scripts\Activate.ps1

# 4) 先装支持 Blackwell（sm_120）的 PyTorch
uv pip install torch --index-url https://download.pytorch.org/whl/cu128

# 5) 再装 SB3 和 Gymnasium
uv pip install "stable-baselines3[extra]" gymnasium
```

验证：

```python
import torch
print(torch.__version__)
print(torch.cuda.is_available())
print(torch.cuda.get_device_capability())  # 应该是 (12, 0)
```

如果 `get_device_capability()` 报错或不是 `(12, 0)`，说明 PyTorch 版本不支持 5060 Ti，换 cu128 的 wheel。

---

## 9. 第一个样例（CartPole）

```python
from stable_baselines3 import PPO
import gymnasium as gym

env = gym.make("CartPole-v1")
model = PPO(
    "MlpPolicy",
    env,
    verbose=1,
    n_steps=1024,
    batch_size=64,
    learning_rate=3e-4,
    tensorboard_log="./tb",
)
model.learn(total_timesteps=50_000)
model.save("ppo_cartpole")

# 评估
obs, _ = env.reset()
done = False
total = 0
while not done:
    action, _ = model.predict(obs, deterministic=True)
    obs, reward, terminated, truncated, _ = env.step(action)
    total += reward
    done = terminated or truncated
print("score:", total)
```

验收：`ep_rew_mean` 能到 200 左右，评估分数 > 150。

---

## 10. Dict 观测 + CNN（最接近本项目）

```python
import numpy as np
import gymnasium as gym
from gymnasium import spaces
from stable_baselines3 import PPO

class FakeEtgEnv(gym.Env):
    def __init__(self):
        self.observation_space = spaces.Dict({
            "grid": spaces.Box(0, 1, (7, 64, 64), np.float32),
            "scalars": spaces.Box(-10, 10, (20,), np.float32),
        })
        self.action_space = spaces.MultiDiscrete([9, 16, 2, 2, 2, 2])
        self.t = 0

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        self.t = 0
        return self._obs(), {}

    def _obs(self):
        return {
            "grid": np.random.rand(7, 64, 64).astype(np.float32),
            "scalars": np.random.randn(20).astype(np.float32),
        }

    def step(self, action):
        self.t += 1
        reward = 0.1
        done = self.t >= 60
        return self._obs(), reward, done, False, {}

env = FakeEtgEnv()
model = PPO("MultiInputPolicy", env, verbose=1, n_steps=256, batch_size=64)
model.learn(total_timesteps=20_000)
```

验收：能跑完不报错；换成真实 obs 后，`MultiInputPolicy` 能正常训练。

---

## 11. 学习时怎么和项目同步

- **每个概念都去项目里找对应物**：学 reward shaping → 改 `docs` 里的奖励表；学 MultiInputPolicy → 改栅格图通道。
- **每周日复盘 30 分钟**：看曲线、看代码、写 5 行笔记。
- **理论放在晚上，项目放在白天**：不要用"学习"当拖延项目的借口。
- **不要打断 S0/S1**：游戏侧联调优先，理论学习可以并行，但不要停下来等学完。

---

## 12. 里程碑

| 里程碑 | 标准 | 对应日程 |
|---|---|---|
| **M1** | CartPole 评估 > 150 | D2 |
| **M2** | LunarLander 评估 > 100 | D5 |
| **M3** | 自建 env `check_env` 通过 | D10 |
| **M4** | 假 ETG 环境训练曲线能涨 | D11–D14 |
| **M5** | 接真游戏，随机策略 1 万步不崩 | 项目 S1 |

---

## 13. 常见问题

**Q：数学不好怎么办？**
A：只需要"期望、方差、梯度、链式法则"的直觉。遇到公式先看它想表达什么，不要卡在推导上。

**Q：英语不好怎么办？**
A：Spinning Up 和 SB3 文档都有中文社区笔记；关键术语用本文件第 5 节的速查表对齐即可。

**Q：要不要买课？**
A：不需要。上面的免费资料足够。付费课的价值主要是"有人催你"，不是知识本身。

**Q：要不要读论文？**
A：只读 PPO 原文 + 37 details。其他论文等你有 baseline 再说。

**Q：训练不涨怎么办？**
A：按第 7 节的 8 步排查，不要先怀疑算法。

**Q：多久能上手？**
A：2–3 周能跑通并看懂大概；1–2 个月能独立调参。项目本身是更好的老师。

**Q：需要 GPU 吗？**
A：需要，但你的 5060 Ti 16G 足够。这个项目的瓶颈是游戏速度，不是显卡。

---

## 14. 进度打卡

```
[ ] D1   RL 概念
[ ] D2   CartPole 跑通
[ ] D3   Gymnasium API
[ ] D4   并行环境
[ ] D5   LunarLander
[ ] D6   Spinning Up PPO
[ ] D7   CleanRL ppo.py
[ ] D8   37 details
[ ] D9   TensorBoard / 评估
[ ] D10  自建 GridWorld
[ ] D11  Dict + CNN 观测
[ ] D12  奖励塑形 / reward hacking
[ ] D13  ETG obs/action/reward 草案
[ ] D14  假 ETG 环境跑通
```

---

## 15. 一句话总结

**先花两天跑通 CartPole 和 LunarLander，建立"正常训练"的直觉；然后一边做项目、一边按需补理论。**
不要等学完再开始，也不要一上来就把真游戏当第一个 RL 项目。
