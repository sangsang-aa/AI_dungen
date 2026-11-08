# Enter the Gungeon S0 — 游戏 API 核实笔记

> 所有结论均来自本机 `EtG_Data\Managed\Assembly-CSharp.dll` 的 ILSpy 反编译，并在游戏内实测验证。
> 不要用 publicized 版 DLL 编译；运行时加载的是原版 DLL，私有成员必须用反射/Harmony。

## 0. S0 关键结论（直接影响 S1）

| 发现 | 对 S1 的影响 |
| --- | --- |
| `usingForcedInput` / `forcedInput` / `ForceStartDodgeRoll` / `forceFire*` 可用 | 动作层直接用这套；**不做 ViGEmBus 虚拟手柄** |
| `StaticReferenceManager.AllProjectiles` 已由游戏维护 | 子弹观测**首选它**；Harmony 只留作交叉校验/兜底 |
| `forceAimPoint` 实际是 `Vector2?` | 协议、C#、Python 端统一用 `Vector2`；不要写 `Vector3` |
| 失焦时 `Time.timeScale = 0` | S1 用 watchdog `ForceUnpause`；**绝不能设 `PreventPausing=true`**（会禁用瞄准/射击） |
| Steam 默认 `F12` 是截图热键 | 训练/自动化不要依赖 F 键；改用配置文件或模式开关 |
| `QuickRestart` 可能写坏存档 | 训练禁用；S1 改用“房间软重置” |

> 一句话：内部 forced-input 路线成立，不需要虚拟手柄；剩余不确定性是通信层 + 固定房间软重置。

## 1. 环境与版本

| 项目 | 实测值 |
| --- | --- |
| 游戏路径 | `D:\steam\steamapps\common\Enter the Gungeon` |
| Steam App ID | 311690 |
| Steam buildid | 3829397 |
| 游戏内版本字符串 | **2.1.9**（日志 `Version: 2.1.9`；与“2.1.7”说法不一致，以实际安装为准） |
| Unity | 2017.4.27f1 |
| Mono / CLR | 2.0.50727.1433，x64 |
| BepInEx | 5.4.21.0（BepInExPack_EtG 5.4.2101） |
| Harmony | HarmonyX，随 BepInEx 5.4.21 提供（`0Harmony.dll`） |
| 插件目标框架 | net35 |
| 游戏 Managed 目录 | `EtG_Data\Managed` |

## 2. 程序集与命名空间

- `Assembly-CSharp.dll`
  - 全局命名空间：`GameManager`、`PlayerController`、`AIActor`、`Projectile`、`HealthHaver`、`BraveInput`、`GungeonActions`、`StaticReferenceManager`、`EnemyDatabase`、`EnemyDatabaseEntry`
  - `Dungeonator` 命名空间：`RoomHandler`（**不是**全局 `RoomHandler`）
  - `InControl` 命名空间：`PlayerActionSet`、`PlayerAction`、`PlayerTwoAxisAction`、`InputDevice` 等（InControl 1.6.17 被编进 Assembly-CSharp，不是独立 DLL）
- `Assembly-CSharp-firstpass.dll`：不含 InControl。

## 3. GameManager

```csharp
public class GameManager : BraveBehaviour
{
    public static GameManager Instance { get; }        // 懒加载；不要假设永不为 null
    public PlayerController PrimaryPlayer { get; }
    public PlayerController SecondaryPlayer { get; }
    public PlayerController[] AllPlayers { get; }

    public bool IsPaused { get; }
    public bool IsLoadingLevel { get; private set; }
    public bool IsFoyer { get; }
    public bool PreventPausing { get; set; }           // 陷阱：true 会让 AcceptingNonMotionInput=false，禁用瞄准/射击

    public void Pause();
    public void ForceUnpause();
    public void QuickRestart(QuickRestartOptions options = default(QuickRestartOptions));

    public bool InTutorial { get; set; }
}
```

注意：

- `QuickRestart()` 只能在已有 `PrimaryPlayer` 的对局/大厅里调用。**在主菜单调用会抛 NullReference 并卡在“载入中”**（本机已复现，已恢复存档）。
- **不要在对局中设 `PreventPausing = true`**。它会让 `PlayerController.AcceptingNonMotionInput` 变成 false，而瞄准和射击都在这个分支里；表现是“朝向锁死、不能射击”。本机已实测复现。
- `Time.timeScale == 0` 时游戏暂停，`PlayerController.Update` 会提前返回，所有输入注入都无效。用 `ForceUnpause()` + watchdog 处理，不要用 `PreventPausing`。

## 4. PlayerController

```csharp
public class PlayerController : GameActor
{
    public int PlayerIDX = -1;
    public bool IsPrimaryPlayer { get; }

    public Vector3 unadjustedAimPoint;        // 实际瞄准点（世界坐标）
    public Vector2? forceAimPoint;            // 强制瞄准点；注意是 Vector2?，不是 Vector3
    public bool usingForcedInput;             // true 时用 forcedInput 覆盖移动
    public Vector2 forcedInput;               // 归一化移动方向
    public bool forceFire;
    public bool forceFireDown;
    public bool forceFireUp;

    public bool ForceStartDodgeRoll(Vector2 vec);   // 直接触发翻滚，返回是否成功
    public bool ForceStartDodgeRoll();
    public bool IsDodgeRolling { get; }
    public DodgeRollState CurrentRollState { get; }

    public RoomHandler CurrentRoom { get; }
    public Gun CurrentGun { get; }
    public Gun CurrentSecondaryGun { get; }
    public bool IsFiring { get; }
    public void ChangeToGunSlot(int slotIndex, bool overrideGunLock = false);

    public PlayerInputState CurrentInputState { get; set; }
    public bool AcceptingAnyInput { get; }
    public bool AcceptingNonMotionInput { get; }

    // 继承自 GameActor
    public Vector2 CenterPosition { get; }
    // 继承自 BraveBehaviour
    public HealthHaver healthHaver;
}
```

实测结论：

- 移动：`usingForcedInput = true; forcedInput = dir;` 可以稳定控制移动。4 秒往返测试位移约 5.56 单位。
- 翻滚：`ForceStartDodgeRoll(Vector2.right)` 返回 `True`，`IsDodgeRolling == True`。
- 射击：`forceFireDown=true` 触发首次攻击，`forceFire=true` 持续，`forceFireUp=true` 结束。实测生成 1 个 `Projectile`。
  - 强制射击会绕过 `BraveInput` 的缓冲动作，日志里会出现 `BraveInput.ConsumeButtonDown/Up(): No action for Shoot was found`，不影响开火，但声音/日志很吵。
  - 纯躲避 Demo 不需要射击，建议不启用 `forceFire*`。
- 瞄准：`forceAimPoint = target;` 后 `unadjustedAimPoint` 会稳定等于 target；结束后设 `null` 恢复鼠标/右摇杆瞄准。

## 5. BraveInput / GungeonActions（InControl）

```csharp
public class BraveInput : MonoBehaviour
{
    public static BraveInput GetInstanceForPlayer(int id);
    public GungeonActions ActiveActions { get; }
    public bool IsKeyboardAndMouse(bool includeNone = false);
    public bool GetButtonDown(GungeonActions.GungeonActionType controlType);
    public bool GetButton(GungeonActions.GungeonActionType controlType);
    public bool GetButtonUp(GungeonActions.GungeonActionType controlType);
    public void ConsumeButtonDown(GungeonActions.GungeonActionType controlType);
    public void ConsumeButtonUp(GungeonActions.GungeonActionType controlType);
}

public class GungeonActions : PlayerActionSet
{
    public PlayerTwoAxisAction Move;          // Left/Right/Up/Down
    public PlayerTwoAxisAction Aim;
    public PlayerAction ShootAction;
    public PlayerAction DodgeRollAction;
    // ...
}
```

重要限制：

- `BraveInput.ActiveActions` 的 `PlayerAction` 值由 InControl 设备每帧 `PlayerActionSet.Update()` 驱动并 `Commit()`。没有公开的“写入动作值”API；直接改 `PlayerAction` 容易在下一次 `Update` 被覆盖。
- 因此 S0 没有走“直接注入 BraveInput 动作”的路线，而是使用 `PlayerController` 游戏自带的 forced-input 通道。它比 ViGEmBus 虚拟手柄更稳定、无驱动依赖，也不需要管理员权限。
- `GetInstanceForPlayer(player.PlayerIDX)` 的调用方式已确认可用，适合只读查询/输入设备判断。

## 6. AIActor / EnemyDatabase / Bullet Kin

```csharp
public class AIActor : BraveBehaviour
{
    public string EnemyGuid;
    public HealthHaver healthHaver;
    public RoomHandler parentRoom;

    public static AIActor Spawn(
        AIActor prefabActor,
        Vector2 position,
        RoomHandler source,
        bool correctForWalls = false,
        AwakenAnimationType awakenAnimType = AwakenAnimationType.Default,
        bool autoEngage = true);

    public static AIActor Spawn(
        AIActor prefabActor,
        IntVector2 position,
        RoomHandler source,
        bool correctForWalls = false,
        AwakenAnimationType awakenAnimType = AwakenAnimationType.Default,
        bool autoEngage = true);

    public void ForceDeath(Vector2 finalDamageDirection, bool allowCorpse = true);
}

public class EnemyDatabase : AssetBundleDatabase<AIActor, EnemyDatabaseEntry>
{
    public static EnemyDatabase Instance { get; }
    public static bool HasInstance { get; }
    public List<EnemyDatabaseEntry> Entries;
    public static AIActor GetOrLoadByName(string name);
    public static AIActor GetOrLoadByGuid(string guid);
}
```

**Bullet Kin 的实测标识：**

| 字段 | 值 |
| --- | --- |
| prefab 名 | `BulletMan` |
| EnemyGuid | `01972dee89fc4404a5c408d50007dad5` |
| 生成对象名 | `BulletMan(Clone)` |

生成/清除实测：

- `EnemyDatabase.GetOrLoadByName("BulletMan")` 返回 prefab。
- `AIActor.Spawn(prefab, player.CenterPosition + offset, room, true, AwakenAnimationType.Spawn, true)` 可生成并注册到当前房间。
- 生成 3 个后：`room.GetActiveEnemiesCount(All) == 3`，`RoomClear == 3`。
- `healthHaver.ApplyDamage(99999f, Vector2.zero, "EtgGym", CoreDamageTypes.None, DamageCategory.Unstoppable, true, null, true)` 可全部清除；1 秒后 `alive=0, roomAll=0, roomClear=0`。

## 7. Dungeonator.RoomHandler

```csharp
namespace Dungeonator
{
    public class RoomHandler
    {
        public enum ActiveEnemyType { RoomClear, All }

        public List<AIActor> GetActiveEnemies(ActiveEnemyType type);
        public void GetActiveEnemies(ActiveEnemyType type, ref List<AIActor> outList);
        public int GetActiveEnemiesCount(ActiveEnemyType type);
        public bool HasActiveEnemies(ActiveEnemyType type);

        public void AddSpecificEnemyToRoomProcedurally(
            string enemyGuid,
            bool reinforcementSpawn = false,
            Vector2? goalPosition = null);

        public bool IsSealed { get; }
        public Action OnEnemiesCleared;   // 字段，不是 event
        public void RegisterEnemy(AIActor enemy, bool ...);
        public void DeregisterEnemy(AIActor enemy, bool suppressClearChecks = false);
    }
}
```

注意：

- `GetActiveEnemies(ActiveEnemyType.All)` 返回的是内部 `activeEnemies` 列表本身（不是副本），遍历时不要边遍历边让敌人死亡/注销；需要复制时用 `new List<AIActor>(...)` 或 `GetActiveEnemies(type, ref outList)`。
- `RoomClear` 会过滤 `IgnoreForRoomClear` 的敌人（例如某些召唤物/炮台）。
- `StaticReferenceManager.AllEnemies` 是**全楼层**敌人，不是当前房间；当前房间请用 `player.CurrentRoom.GetActiveEnemiesCount(...)`。

## 8. Projectile / StaticReferenceManager

```csharp
public class Projectile : BraveBehaviour
{
    public virtual void Start();       // 内部 StaticReferenceManager.AddProjectile(this)
    public virtual void OnDespawned(); // 内部 Cleanup() -> RemoveProjectile
    protected override void OnDestroy(); // RemoveProjectile
    public void OnSpawned();           // 池复用时会再次调用 Start()

    public static float BaseEnemyBulletSpeedMultiplier { get; set; }
}

public static class StaticReferenceManager
{
    public static List<AIActor> AllEnemies;
    public static ReadOnlyCollection<Projectile> AllProjectiles { get; }
    public static event Action<Projectile> ProjectileAdded;
    public static event Action<Projectile> ProjectileRemoved;
}
```

实测结论：

- 游戏**自带** `StaticReferenceManager.AllProjectiles`，生命周期由 `Projectile.Start/OnDespawned/OnDestroy` 维护，可直接用于观测，不需要自己维护列表。
- **正式 RL 观测首选 `StaticReferenceManager.AllProjectiles`**；Harmony 只保留作交叉校验/兜底，不作为主数据源。
- 为了满足 E4 的 Harmony 验证，插件同时挂了 `Projectile.Start`、`OnDespawned`、`OnDestroy` 三个 postfix，维护 `HashSet<Projectile>`。
- 实测计数一致：`static=1, harmony=1, find=1`；累计 `added=94, removed=93`。
- `FindObjectsOfType<Projectile>()` 也能用，但每帧调用开销大，只适合调试/兜底。
- **清子弹时注意**：`SpawnManager.Despawn` 只把对象回池，**不保证调用** `Projectile.OnDespawned`；必须先显式 `p.OnDespawned()`，否则 `StaticReferenceManager.AllProjectiles` 会保留已回池子弹（S1-B 实测累积 9 个不消失）。

## 9. HealthHaver

```csharp
public class HealthHaver : BraveBehaviour
{
    public float GetCurrentHealth();
    public float GetMaxHealth();
    public void SetHealthMaximum(float value, ...);
    public void ForceSetCurrentHealth(float h);

    public bool IsAlive { get; }      // Health > 0 || Armor > 0
    public bool IsDead { get; }
    public bool IsVulnerable { get; set; }
    public bool PreventAllDamage;

    public void ApplyDamage(
        float damage,
        Vector2 direction,
        string sourceName,
        CoreDamageTypes damageTypes = CoreDamageTypes.None,
        DamageCategory damageCategory = DamageCategory.Normal,
        bool ignoreInvulnerabilityFrames = false,
        PixelCollider hitPixelCollider = null,
        bool ignoreDamageCaps = false);
}
```

玩家血量实测：`PrimaryPlayer.healthHaver.GetCurrentHealth()/GetMaxHealth()`，例如 `3.0/3.0`、`4.0/4.0`。

## 10. 已知坑

1. **Steam F12 截图热键**会截走 `F12`，游戏收不到；测试瞄准时改用其他键，或直接 `PostMessage` 到游戏窗口。训练/自动化**不要依赖 F 键**，改用配置文件或模式开关。
2. **失焦自动暂停**：游戏失去焦点时 `Time.timeScale` 变 0，所有注入失效。用 watchdog `ForceUnpause()` 处理；**不要设 `PreventPausing=true`**（会让 `AcceptingNonMotionInput=false`，瞄准/射击被禁用）。详见第 11 节。
3. **BepInEx 控制台**窗口会抢焦点，测试时建议隐藏/关闭 Console 或只写文件日志。
4. **主菜单调用 `QuickRestart()`** 会 NRE + 卡加载；只能在有 `PrimaryPlayer` 时调用。插件已加保护。**训练中禁用 `QuickRestart`**（可能写坏存档），S1 改用“房间软重置”。
5. **`forceAimPoint` 是 `Vector2?`**，不要按 `Vector3` 赋值；`unadjustedAimPoint` 才是 `Vector3`。
6. **publicized DLL 不能用于编译**：运行时是原版私有成员，直接访问会 `FieldAccessException`。编译用原版，私有成员用反射/Harmony。
7. 游戏实际版本字符串是 **2.1.9**，不是 2.1.7；Steam buildid 3829397 与之后无更新这一点一致。

## 11. 失焦暂停与无人值守（S1 必做）

### 现象

- 游戏失去焦点时（Alt-Tab、终端抢焦点、BepInEx 控制台打开）会调用 `Pause()`，`Time.timeScale` 变 0。
- `PlayerController.Update` 在 `GameManager.Instance.IsPaused` 时提前返回，移动/翻滚/射击注入全部无效。
- 这不是插件 bug，是游戏行为；无人值守训练必须处理。

### 禁止使用 `PreventPausing = true`

`PreventPausing` 的 setter 实际设置 `m_preventUnpausing`；而 `PlayerController.AcceptingNonMotionInput` 的条件是：

```csharp
(CurrentInputState == AllInput || CurrentInputState == NoMovement)
&& !GameManager.Instance.PreventPausing
```

所以设 `true` 会直接禁用瞄准/射击，表现为“朝向锁死、不能开火”。S0/S1 已实测复现。**不要用它防失焦暂停。**

### 正确实现

1. 保持 `PreventPausing = false`。
2. Watchdog（建议 0.5–1 秒一次，或在 `Update` 里每 N 帧）：

   ```csharp
   if (trainingMode && !gm.IsLoadingLevel && Time.timeScale <= 0.001f)
   {
       gm.ForceUnpause();
       Time.timeScale = 1f;
       // 记录次数、时间、当前房间/玩家状态
   }
   ```

   - 只在训练模式启用；不要和玩家主动暂停菜单打架。
   - 每次恢复必须写日志/上报，不能静默。
   - 恢复后确认 `BraveTime.DeltaTime` 正常，再继续发送动作。

3. 验收：**连续 10 分钟无人值守，`Time.timeScale` 不得为 0**；若发生暂停，watchdog 必须在 1 秒内恢复并留下记录。

### 注意

- `Time.timeScale` 是全局的；游戏自身的 `BraveTime`/暂停系统也会改它。
- 不要用 `QuickRestart` 做重置；它可能写坏 `ActiveA.game`/`SlotA.save`。S1 用“房间软重置”：清敌人、清子弹、复位玩家坐标/血量/状态、重置房间敌人，不重新加载整局。
