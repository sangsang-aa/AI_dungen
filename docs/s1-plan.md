# S1 计划与验收标准

> S0 已确认：内部 forced-input 路线成立，不需要 ViGEmBus；剩余不确定性是通信层 + 固定房间软重置。

## S1 范围

1. 固定房间、固定 3 个 Bullet Kin 出生点。
2. 房间软重置（不调用 `QuickRestart`，不重新加载整局）。
3. localhost TCP + JSON 通信层（长度前缀或换行分隔）。
4. Python 侧 action → C# 注入 → C# obs 回传的闭环。
5. 无人值守稳定性与暂停看门狗。

## 验收标准

### A. 固定房间

- 能稳定进入指定房间，3 个 Bullet Kin 的出生点固定、可重复。
- 连续重置 20 次，敌人数量/位置/房间状态一致。
- 不依赖随机房间生成；重启游戏后仍能回到同一房间配置。

### B. 房间软重置

- 重置内容：清除当前房间敌人、清除子弹、复位玩家坐标/血量/状态、恢复房间敌人。
- **禁止使用 `QuickRestart`**（可能写坏 `ActiveA.game`/`SlotA.save`）。
- 重置后 1 秒内玩家可移动/翻滚，房间敌人计数正确。
- 连续重置 20 次不损坏存档、不泄漏对象（`StaticReferenceManager.AllEnemies/AllProjectiles` 计数回到基线）。

### C. 通信层

- Python ↔ C# 通过 localhost TCP + JSON 稳定通信。
- 支持长度前缀或换行分隔；消息可解析、可版本化。
- 连续运行 10 分钟不断连；异常断开后能重连。
- 单步 action→obs 往返延迟有记录（目标 p95 < 5 ms，实测为准）。

### D. 动作与观测契约

- 动作：`MultiDiscrete([9,16,2,2,2,2])`，动作重复 4 帧。
- 观测：grid `7x64x64` + scalars `20` + rays `32`。
- C#/Python 两端的字段名、类型、维度一致；`forceAimPoint` 统一用 `Vector2`。
- 不依赖 F 键；训练模式通过配置文件/模式开关启用。

### E. 无人值守稳定性（新增硬验收）

- **连续 10 分钟无人值守，`Time.timeScale` 不得变成 0。**
- 插件必须能检测到暂停并自动恢复：每次检测到 `Time.timeScale <= 0.001` 时，在 1 秒内调用 `GameManager.Instance.ForceUnpause()` / 恢复时间缩放，并写日志。
- 10 分钟内无人工干预、无输入丢失、无存档异常。
- 日志至少记录：暂停检测次数、恢复时间、当时房间/玩家状态。

### F. 明确不做

- 不做 ViGEmBus / vgamepad 虚拟手柄。
- 不做 `QuickRestart` 训练重置。
- 不做射击/清房训练。
- 不做导航/商店/换层。

## 建议实现顺序

1. 固定房间 + 房间软重置（先不接 Python）。
2. C# TCP server + JSON 协议 + 动作注入。
3. Python client + Gymnasium `Env` 包装。
4. 10 分钟无人值守 + watchdog 压测。
5. 再接观测/奖励/训练。
