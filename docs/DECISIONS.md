# 决策记录（DECISIONS）

> 只追加，不改历史。每条记录：背景 / 决定 / 理由 / 后果 / 替代方案。
> 目的：上下文丢失后，避免重新争论已经定过的事。

---

## ADR-001 项目范围：单房间纯躲避（Demo A）

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：完整通关需要导航、商店、换层、Boss、装备系统，工程量过大。
- **决定**：第一目标只做“固定房间 + 3 个 Bullet Kin + 只移动/翻滚 + 存活 60 秒”。
- **理由**：能最快验证整条链路，失败也不浪费，后续清房/导航可复用全部基础设施。
- **后果**：Demo A 不做射击、清房、导航、商店、换层。
- **替代**：直接做清房或全游戏（已否决，风险过高）。

---

## ADR-002 观测用结构化状态，不用纯像素

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：ETG 是实时弹幕游戏，纯像素方案样本效率低 10–100 倍。
- **决定**：从游戏内读玩家/敌人/子弹/墙，构造以玩家为中心的局部栅格 + 标量 + 射线。
- **理由**：样本效率高、可控、可解释；不需要处理粒子/震屏/UI 干扰。
- **后果**：必须写 C# 插件；观测设计与游戏内部 API 绑定。
- **替代**：DXcam + CNN（保留作对比实验，不作为主路线）。

---

## ADR-003 动作注入用游戏原生 forced-input，不用 ViGEm

- **日期**：2026-11-08
- **状态**：已采纳（S0 实测）
- **背景**：`BraveInput.ActiveActions` 是 InControl 的 `PlayerActionSet`，每帧被真实设备覆盖，没有公开写入接口。
- **决定**：使用 `PlayerController` 的原生通道：
  - 移动：`usingForcedInput = true; forcedInput = dir;`
  - 翻滚：`ForceStartDodgeRoll(Vector2)`
  - 射击：`forceFire` / `forceFireDown` / `forceFireUp`
  - 瞄准：`forceAimPoint`
- **理由**：稳定、无驱动依赖、无需管理员权限、延迟低；实测全部通过。
- **后果**：需要处理失焦暂停；射击会绕过 `BraveInput` 产生日志噪音（Demo A 不射击）。
- **替代**：ViGEmBus + vgamepad（已否决）。

---

## ADR-004 子弹观测用 `StaticReferenceManager.AllProjectiles`

- **日期**：2026-11-08
- **状态**：已采纳（S0 实测）
- **背景**：需要每帧拿到全部子弹；自己维护列表容易漏池化对象。
- **决定**：首选游戏自带的 `StaticReferenceManager.AllProjectiles`；Harmony 补丁只作交叉校验/兜底。
- **理由**：游戏自己维护生命周期，三者计数实测一致（`static=1, harmony=1, find=1`）。
- **后果**：观测需要按 `Owner` 过滤敌方/我方子弹。
- **替代**：`FindObjectsOfType<Projectile>()`（每帧开销大，仅调试）。

---

## ADR-005 重置用“房间软重置”，禁用 `QuickRestart`

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：`QuickRestart` 在主菜单调用会 NRE + 卡加载，并已复现写坏存档。
- **决定**：训练重置只做：清敌人、清子弹、复位玩家坐标/血量/状态、重新生成固定敌人；不重新加载整局。
- **理由**：安全、快、可控；避免存档风险。
- **后果**：需要自己处理敌人池化/引用清理。
- **替代**：`QuickRestart`（已禁用）。

---

## ADR-006 时间加速从 2x 起步，不直接上 4x

- **日期**：2026-11-08
- **状态**：已采纳（S0 实测 2x）
- **背景**：游戏是实时的，只有 `Time.timeScale` 能真正加速；4x 可能让子弹穿模。
- **决定**：S1/S2 用 2x；4x 需要单独做碰撞/动画断言后再启用。
- **理由**：2x 已实测 60 秒无异常，风险可控。
- **后果**：训练吞吐约为 1x 的 2 倍；不够再评估 3x/4x。
- **替代**：1x（保底）。

---

## ADR-007 技术栈：BepInEx 5.4.21 + net35 + Python 3.12

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：游戏是 Unity 2017.4.27f1 + Mono x64；本机只有 Python 3.14 和 .NET SDK 8/10。
- **决定**：
  - 插件：BepInEx 5.4.21.0 x64 + HarmonyX，C# net35。
  - 训练：uv 管理的 Python 3.12.16 + PyTorch 2.11.0+cu128 + SB3 2.9.2 + Gymnasium 1.4.0。
- **理由**：与游戏运行时一致；cu128 支持 RTX 5060 Ti（sm_120）；venv 隔离，不污染系统 Python。
- **后果**：不能用 publicized DLL 编译；私有成员用反射/Harmony。
- **替代**：.NET Framework 4.x（未验证，不采用）。

---

## ADR-008 `forceAimPoint` 统一用 `Vector2?`

- **日期**：2026-11-08
- **状态**：已采纳（S0 实测）
- **背景**：反编译与实测都表明 `PlayerController.forceAimPoint` 是 `Vector2?`；`unadjustedAimPoint` 才是 `Vector3`。
- **决定**：C#、JSON 协议、Python 端统一用二维点。
- **理由**：避免编译失败和协议错位。
- **后果**：瞄准动作输出为世界坐标二维点或角度。
- **替代**：无。

---

## ADR-009 记忆文件化 + 一任务一会话

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：长会话会被压缩，换会话后 AI 容易忘记决策和进度。
- **决定**：项目状态写入 `STATE.md` / `NEXT.md` / `DECISIONS.md` / `GLOSSARY.md` / `SESSION_LOG.md`；每次会话按 `AGENTS.md` 的开场/收尾协议执行。
- **理由**：文件不会随上下文丢失；短会话 + 文件交接比超长会话更稳。
- **后果**：每次会话多花 5 分钟写记录，但换来可恢复性。
- **替代**：依赖聊天记录（已否决）。

---

## ADR-010 训练不依赖 F 键，改用配置/模式开关

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：F12 被 Steam 截图占用；F 键在无人值守时不可用。
- **决定**：F 键只用于人工调试；训练模式由配置文件或 TCP 消息开启，并提供 watchdog。
- **理由**：可自动化、可复现、不与 Steam 热键冲突。
- **后果**：需要额外的模式管理代码。
- **替代**：`PostMessage` 模拟按键（仅调试备用）。

---

## ADR-011 观测/动作契约（S1 锁定）

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：需要 C#/Python 两端一致，避免后期返工。
- **决定**：
  - 观测：`grid 7×64×64`（墙/敌人/敌弹/我弹/拾取/陷阱/玩家）+ `scalars 20` + `rays 32`。
  - 动作：`MultiDiscrete([9,16,2,2,2,2])` = 移动/瞄准/射击/翻滚/换弹/交互。
  - 动作重复 4 帧（约 15Hz 决策）。
  - Demo A 只使用移动 + 翻滚，其余维度先保留但不启用。
- **理由**：与 SB3 `MultiInputPolicy` 兼容；后续清房不需要改协议。
- **后果**：S1 的 JSON schema 必须按此实现并版本化。
- **替代**：纯 Dict 连续动作（暂不采用）。

---

## ADR-012 先有随机/规则基线，再上 PPO

- **日期**：2026-11-08
- **状态**：已采纳（计划）
- **背景**：没有基线时，无法判断“训练不涨”是算法问题还是环境问题。
- **决定**：S2 先做随机策略 + 规则 bot（瞄最近敌人/靠近就翻滚），S3 再上 PPO，并保留对比曲线。
- **理由**：基线是排查环境 bug 的唯一参照。
- **后果**：Demo 报告必须包含随机/规则/PPO 三组数据。
- **替代**：直接 PPO（已否决）。

---

## ADR-013 训练角色固定为 Pilot

- **日期**：2026-11-08
- **状态**：已采纳
- **背景**：当前选中的角色带随机武器/装备，初始条件不可复现，无法用于重复训练。
- **决定**：
  - 所有训练对局固定使用 **Pilot**（prefab path `PlayerRogue`）。
  - Pilot 必须在**正常选人界面**选择；**禁止在对局/Breach 运行时换角色**（见 ADR-014）。
  - 角色选择界面按 F4：插件自动选中 Pilot，走正常选人流程（反射调用 `CharacterSelectController.Do()`），不使用 `QuickRestart`。
  - `ArenaController.Setup` 强制校验 `characterIdentity == PlayableCharacters.Pilot`；非 Pilot 拒绝布场并写日志。
- **理由**：固定角色 = 固定初始武器/道具/属性，训练结果可复现；避免随机装备污染 baseline。
- **后果**：不能测试其他角色；`ForcePilotAtCharacterSelect` 依赖 `CharacterSelectController` 的私有 `Do()`，游戏更新时可能失效。
- **替代**：手动选 Pilot（不可自动化，不采用）；任意角色（已否决）。

---

## ADR-014 禁止运行时换角色（Pilot 只能在选人界面选）

- **日期**：2026-11-08
- **状态**：已采纳（事故后）
- **背景**：S1-A 测试中用 `ClearPrimaryPlayer + LoadCharacterSelect + PlayerPrefabForNewGame` 在 Breach 运行时把角色换成 Pilot。结果玩家对象被销毁/重建，`PrimaryPlayer` 返回 null，角色与枪的挂点错位，枪漂在角色旁边，无法射击。
- **决定**：
  - **禁止**在对局/Breach 运行时清玩家或换 prefab。
  - Pilot 必须在正常选人界面选定（`CharacterSelectController.Do()` 路径）。
  - `ArenaController.Setup` 强制校验 Pilot；非 Pilot 拒绝布场。
  - `K` 只做枪/手挂点修复；`L` 只做陈旧 Foyer 标志恢复 + 挂点修复，不作为换角色手段。
- **理由**：运行时换角色会破坏 Foyer 状态、手部/枪挂点和输入状态，且无法可靠恢复。
- **后果**：训练启动流程必须先正常选 Pilot；如果角色是 Paradox 等随机装备角色，拒绝布场。
- **替代**：运行时 `ClearPrimaryPlayer + LoadCharacterSelect`（已否决，已禁用）。

---

## ADR-015 禁止设置 `PreventPausing=true`

- **日期**：2026-11-08
- **状态**：已采纳（事故后）
- **背景**：失焦暂停需要处理，最初方案用 `GameManager.PreventPausing = true`。实测发现它会让 `PlayerController.AcceptingNonMotionInput` 变成 false，而瞄准/射击都在这个分支；表现是角色朝向锁死、不能开火。
- **决定**：
  - 对局中**永远不要**设 `PreventPausing = true`；保持 false。
  - 失焦暂停用 watchdog：检测 `IsPaused` / `Time.timeScale<=0.001`，调用 `ForceUnpause()` 并恢复 `Time.timeScale=1`，写日志。
  - `K/L` 修复只做 `ForceUnpause + PreventPausing=false`，不再设 true。
- **理由**：`AcceptingNonMotionInput` 明确依赖 `!PreventPausing`；这是游戏内部语义，不是“暂停开关”。
- **后果**：无人值守训练需要 watchdog；不能靠 PreventPausing 静默防暂停。
- **替代**：`PreventPausing=true`（已否决，已移除）。

---

## ADR-016 S1-A 用固定 run seed 20261108

- **日期**：2026-11-08
- **状态**：已采纳（S1-A）
- **背景**：入口房间类别/尺寸稳定，但程序化生成导致 `basePosition` 和 clear spot 跨重启变化；不满足“固定房间/固定坐标”。
- **决定**：在 `StartFirstFloorFromFoyer` / `J` 真正开始 1-1 前设置 `GameManager.CurrentRunSeed = 20261108`；**不在 Foyer 每帧强制**（在主菜单阶段初始化会崩溃）。
- **理由**：`Dungeon.GetDungeonSeed()` 使用 `CurrentRunSeed`，固定后两轮重启的 `base/dims`、玩家/敌人 local 完全一致。
- **后果**：掉落/敌人等内容也被固定；训练泛化后续再评估随机 seed 或课程学习。
- **替代**：固定 anchor（未使用）；随机 seed（不满足 S1-A）。

---

## ADR-017 软重置清子弹：先 OnDespawned 再回池

- **日期**：2026-11-08
- **状态**：已采纳（S1-B）
- **背景**：`SpawnManager.Despawn` 只把 GameObject 回池，不保证调用 `Projectile.OnDespawned`；直接回池会让 `StaticReferenceManager.AllProjectiles` 保留已回池子弹（实测 9 个累积不消失）。
- **决定**：软重置遍历 `AllProjectiles` 时，先 `p.OnDespawned()` 显式清理并从静态列表移除，再 `SpawnManager.Despawn`；失败再 `Destroy`。
- **理由**：保证观测计数归零，避免训练 episode 之间的幽灵子弹。
- **后果**：清弹逻辑依赖 `Projectile.OnDespawned` 的幂等性；已实测 20 次 after=0。
- **替代**：直接 Destroy（可用但破坏池化）；只 `Despawn`（已否决）。
