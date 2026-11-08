using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Dungeonator;
using EtgGym.Arena;
using HarmonyLib;
using UnityEngine;

namespace EtgGym
{
    [BepInPlugin(Guid, Name, Version)]
    public class EtgGymPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.etggym.demo";
        public const string Name = "EtgGym";
        public const string Version = "0.1.0";

        internal static ManualLogSource LogSource;

        private static readonly HashSet<Projectile> TrackedProjectiles = new HashSet<Projectile>();
        private static int ProjectilesAdded;
        private static int ProjectilesRemoved;

        private bool _verbose;
        private float _sinceStatus;

        // E1 movement test
        private bool _moveTest;
        private float _moveTestEnd;
        private float _moveTestStart;
        private Vector2 _moveTestStartPos;

        // E1 fire test
        private bool _fireTest;
        private float _fireStartAt;
        private float _fireTestEnd;
        private float _fireDownEnd;
        private bool _fireStarted;
        private bool _fireUpPending;
        private float _fireUpEnd;

        // E2 aim test
        private bool _aimTest;
        private float _aimTestEnd;

        // S1-A arena setup
        private bool _pendingArenaSetup;
        private int _pendingArenaPhase;
        private float _pendingArenaDeadline;
        private float _arenaCheckAt;
        private bool _consistencyRunning;

        private void Awake()
        {
            LogSource = Logger;
            Logger.LogInfo(Name + " " + Version + " loaded.");
            Logger.LogInfo("Keys: F2 reset | K repair gun/hand/aim | L recover player state | O save rainbow mode OFF | F3 clear arena | F4 force Pilot (select screen) / setup arena (Foyer -> 1-1 or fixed entrance) | F5 timescale | H toggle test mode (invulnerable) | J 20x consistency test | F6 room/projectile info | F7 dump DB | F8 status | F9 move | F10 roll | F11 fire | F12 aim");

            try
            {
                Harmony harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(ProjectileStartPatch));
                harmony.PatchAll(typeof(ProjectileDespawnPatch));
                harmony.PatchAll(typeof(ProjectileDestroyPatch));
                Logger.LogInfo("[E4] Harmony lifecycle patches installed.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[E4] Harmony patch failed, falling back to StaticReferenceManager/FindObjectsOfType: " + ex);
            }
        }

        internal static void TrackAdd(Projectile projectile)
        {
            if (projectile == null) return;
            if (TrackedProjectiles.Add(projectile)) ProjectilesAdded++;
        }

        internal static void TrackRemove(Projectile projectile)
        {
            if (projectile == null) return;
            if (TrackedProjectiles.Remove(projectile)) ProjectilesRemoved++;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Logger.LogInfo("[S1-A] F1 runtime character swap disabled: use the normal character-select screen and pick Pilot");
            }
            if (Input.GetKeyDown(KeyCode.F2)) ResetOverrides();

            if (Input.GetKeyDown(KeyCode.F8))
            {
                _verbose = !_verbose;
                Logger.LogInfo("EtgGym status log: " + (_verbose ? "ON" : "OFF"));
                if (_verbose) LogStatus("manual");
            }

            if (Input.GetKeyDown(KeyCode.H)) ArenaController.ToggleTestMode();
            if (Input.GetKeyDown(KeyCode.J)) StartConsistencyTest();
            if (Input.GetKeyDown(KeyCode.K)) ArenaController.RepairPlayer(FindAnyPlayer());
            if (Input.GetKeyDown(KeyCode.L)) RecoverPlayerState();
            if (Input.GetKeyDown(KeyCode.O)) SaveRainbowOff();
            if (Input.GetKeyDown(KeyCode.F7)) DumpBulletKinEntries();
            if (Input.GetKeyDown(KeyCode.F4)) StartArenaSetup(FindAnyPlayer());

            PlayerController player = FindAnyPlayer();
            if (player == null)
            {
                if (_verbose) LogStatus("no-player");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                ArenaController.ClearSpawned(player);
                _arenaCheckAt = Time.unscaledTime + 1f;
            }
            if (Input.GetKeyDown(KeyCode.F5)) ToggleTimescale();
            if (Input.GetKeyDown(KeyCode.F6))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    ArenaController.ToggleTestMode();
                }
                else
                {
                    LogProjectileCounts(player);
                    ArenaController.LogRoomInfo("F6");
                    ArenaController.LogCounts(player, "F6");
                }
            }
            if (Input.GetKeyDown(KeyCode.F9)) StartMoveTest(player);
            if (Input.GetKeyDown(KeyCode.F10)) TestDodgeRoll(player);
            if (Input.GetKeyDown(KeyCode.F11)) StartFireTest(player);
            if (Input.GetKeyDown(KeyCode.F12)) StartAimTest(player);

            TickMoveTest(player);
            TickFireTest(player);
            TickAimTest(player);
            TickArenaSetup(player);
            TickArenaCheck(player);

            if (_verbose)
            {
                _sinceStatus += Time.unscaledDeltaTime;
                if (_sinceStatus >= 0.5f)
                {
                    _sinceStatus = 0f;
                    LogStatus("tick");
                }
            }
        }

        private static PlayerController GetPlayer()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return null;
            return gm.PrimaryPlayer;
        }

        private static PlayerController FindAnyPlayer()
        {
            PlayerController player = GetPlayer();
            if (player != null) return player;

            PlayerController[] all = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject != null && all[i].gameObject.activeInHierarchy)
                {
                    return all[i];
                }
            }
            return null;
        }

        private void SaveRainbowOff()
        {
            try
            {
                GameStatsManager gsm = GameStatsManager.Instance;
                if (gsm == null)
                {
                    Logger.LogInfo("[S1] rainbow save failed: GameStatsManager null");
                    return;
                }

                bool before = gsm.rainbowRunToggled;
                gsm.rainbowRunToggled = false;
                bool ok = GameStatsManager.Save();
                Logger.LogInfo("[S1] rainbowRunToggled " + before + " -> " + gsm.rainbowRunToggled
                    + " IsRainbowRun=" + gsm.IsRainbowRun
                    + "; GameStatsManager.Save()=" + ok);
            }
            catch (Exception ex)
            {
                Logger.LogError("[S1] rainbow save failed: " + ex);
            }
        }

        private void RecoverPlayerState()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.IsSelectingCharacter = false;
                GameManager.IsReturningToBreach = false;
            }

            PlayerController player = FindAnyPlayer();
            if (player != null)
            {
                ArenaController.RepairPlayer(player);
            }
            Logger.LogInfo("[S1-A] recover: player=" + (player != null ? player.characterIdentity.ToString() : "null"));
        }

        private void ResetOverrides()
        {
            _moveTest = false;
            _fireTest = false;
            _aimTest = false;
            _fireUpPending = false;
            _pendingArenaSetup = false;
            _arenaCheckAt = 0f;
            Time.timeScale = 1f;

            PlayerController player = GetPlayer();
            if (GameManager.Instance != null) GameManager.Instance.PreventPausing = false;
            if (player != null)
            {
                ArenaController.ClearSpawned(player);
                player.usingForcedInput = false;
                player.forcedInput = Vector2.zero;
                player.forceFire = false;
                player.forceFireDown = false;
                player.forceFireUp = false;
                player.forceAimPoint = null;
            }
            Logger.LogInfo("[reset] all overrides cleared, timescale=1");
        }

        private void LogStatus(string tag)
        {
            PlayerController player = FindAnyPlayer();
            if (player == null)
            {
                Logger.LogInfo("[status:" + tag + "] no player");
                return;
            }

            int aliveEnemies;
            int totalEnemies;
            CountEnemies(out aliveEnemies, out totalEnemies);

            RoomHandler room = player.CurrentRoom;
            int roomAll = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) : -1;
            int roomClear = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear) : -1;

            Vector2 pos = player.CenterPosition;
            HealthHaver hh = player.healthHaver;
            float hp = hh != null ? hh.GetCurrentHealth() : -1f;
            float maxHp = hh != null ? hh.GetMaxHealth() : -1f;

            Logger.LogInfo(string.Format(
                "[status:{0}] pos=({1:F2},{2:F2}) hp={3:F1}/{4:F1} floorEnemies={5}/{6} room={7}/{8} bullets={9} harmony={10} timescale={11:F2} rolling={12} aim=({13:F2},{14:F2})",
                tag, pos.x, pos.y, hp, maxHp, aliveEnemies, totalEnemies, roomAll, roomClear,
                StaticReferenceManager.AllProjectiles.Count, TrackedProjectiles.Count, Time.timeScale,
                player.IsDodgeRolling, player.unadjustedAimPoint.x, player.unadjustedAimPoint.y));
        }

        private static void CountEnemies(out int alive, out int total)
        {
            alive = 0;
            total = 0;
            var list = StaticReferenceManager.AllEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                AIActor actor = list[i];
                if (actor == null) continue;
                total++;
                if (actor.healthHaver != null && actor.healthHaver.IsAlive) alive++;
            }
        }

        private void StartMoveTest(PlayerController player)
        {
            _moveTest = true;
            _moveTestStart = Time.unscaledTime;
            _moveTestEnd = _moveTestStart + 4f;
            _moveTestStartPos = player.CenterPosition;
            Logger.LogInfo("[E1-move] start pos=" + Fmt(_moveTestStartPos));
        }

        private void TickMoveTest(PlayerController player)
        {
            if (!_moveTest) return;

            if (Time.unscaledTime >= _moveTestEnd)
            {
                player.usingForcedInput = false;
                player.forcedInput = Vector2.zero;
                _moveTest = false;
                Vector2 delta = player.CenterPosition - _moveTestStartPos;
                Logger.LogInfo("[E1-move] end pos=" + Fmt(player.CenterPosition) + " delta=" + Fmt(delta) + " moved=" + delta.magnitude.ToString("F3"));
                return;
            }

            float elapsed = Time.unscaledTime - _moveTestStart;
            Vector2 dir = (elapsed % 2f) < 1f ? Vector2.right : Vector2.left;
            player.usingForcedInput = true;
            player.forcedInput = dir;
        }

        private void TestDodgeRoll(PlayerController player)
        {
            bool ok = player.ForceStartDodgeRoll(Vector2.right);
            Logger.LogInfo("[E1-roll] ForceStartDodgeRoll(right)=" + ok + " rolling=" + player.IsDodgeRolling + " state=" + player.CurrentRollState);
        }

        private void StartFireTest(PlayerController player)
        {
            _fireTest = true;
            _fireStarted = false;
            _fireUpPending = false;
            _fireStartAt = Time.unscaledTime + 0.5f;
            _fireTestEnd = Time.unscaledTime + 4f;
            player.ChangeToGunSlot(0, true);
            Logger.LogInfo("[E1-fire] queued, switching to slot 0");
        }

        private void TickFireTest(PlayerController player)
        {
            if (_fireTest)
            {
                float now = Time.unscaledTime;
                if (!_fireStarted && now >= _fireStartAt)
                {
                    _fireStarted = true;
                    _fireDownEnd = now + 0.2f;
                    player.forceFireDown = true;
                    player.forceFire = true;
                    Logger.LogInfo("[E1-fire] start gun=" + (player.CurrentGun != null ? player.CurrentGun.name : "none") + " ammo=" + (player.CurrentGun != null ? player.CurrentGun.CurrentAmmo.ToString() : "?"));
                }

                if (_fireStarted && now >= _fireDownEnd)
                {
                    player.forceFireDown = false;
                }

                if (now >= _fireTestEnd)
                {
                    _fireTest = false;
                    player.forceFireDown = false;
                    player.forceFire = false;
                    player.forceFireUp = true;
                    _fireUpPending = true;
                    _fireUpEnd = now + 0.05f;
                    Logger.LogInfo("[E1-fire] stop firing=" + player.IsFiring + " ammo=" + (player.CurrentGun != null ? player.CurrentGun.CurrentAmmo.ToString() : "?") + " bullets=" + StaticReferenceManager.AllProjectiles.Count + " harmony=" + TrackedProjectiles.Count);
                }
            }
            else if (_fireUpPending && Time.unscaledTime >= _fireUpEnd)
            {
                player.forceFireUp = false;
                _fireUpPending = false;
            }
        }

        private void StartAimTest(PlayerController player)
        {
            _aimTest = true;
            _aimTestEnd = Time.unscaledTime + 3f;
            Logger.LogInfo("[E2-aim] start origin=" + Fmt(player.CenterPosition));
        }

        private void TickAimTest(PlayerController player)
        {
            if (!_aimTest) return;

            if (Time.unscaledTime >= _aimTestEnd)
            {
                _aimTest = false;
                player.forceAimPoint = null;
                Logger.LogInfo("[E2-aim] end unadjustedAimPoint=" + Fmt(player.unadjustedAimPoint));
                return;
            }

            Vector2 target = player.CenterPosition + new Vector2(5f, 2f);
            player.forceAimPoint = target;
            if (Time.frameCount % 30 == 0)
            {
                Logger.LogInfo("[E2-aim] target=" + Fmt(target) + " unadjustedAimPoint=" + Fmt(player.unadjustedAimPoint));
            }
        }

        private void StartArenaSetup(PlayerController player)
        {
            // At character select (PrimaryPlayer == null), force Pilot via the normal selection flow.
            if (ArenaController.ForcePilotAtCharacterSelect())
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Logger.LogInfo("[S1-A] setup failed: no GameManager");
                return;
            }

            if (gm.IsFoyer && (gm.IsSelectingCharacter || GameManager.IsReturningToBreach))
            {
                gm.IsSelectingCharacter = false;
                GameManager.IsReturningToBreach = false;
                Logger.LogInfo("[S1-A] cleared stale Foyer selecting/returning flags");
                player = GetPlayer();
            }

            if (player == null)
            {
                Logger.LogInfo("[S1-A] setup ignored: no player (menu/character select/loading)");
                return;
            }

            if (gm.IsFoyer)
            {
                if (!ArenaController.IsPilot(player))
                {
                    Logger.LogInfo("[S1-A] refused: current character=" + player.characterIdentity + ", require Pilot. Return to character select and press F4.");
                    return;
                }
                if (ArenaController.StartFirstFloorFromFoyer())
                {
                    _pendingArenaSetup = true;
                    _pendingArenaPhase = 0;
                    _pendingArenaDeadline = Time.unscaledTime + 25f;
                    Logger.LogInfo("[S1-A] waiting for 1-1 load...");
                }
                return;
            }

            if (ArenaController.Setup(player))
            {
                _arenaCheckAt = Time.unscaledTime + 1f;
            }
        }

        private void TickArenaSetup(PlayerController player)
        {
            if (!_pendingArenaSetup) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || Time.unscaledTime > _pendingArenaDeadline)
            {
                Logger.LogInfo("[S1-A] pending setup timed out/cancelled");
                _pendingArenaSetup = false;
                return;
            }

            if (_pendingArenaPhase == 0)
            {
                if (gm.IsLoadingLevel)
                {
                    _pendingArenaPhase = 1;
                    Logger.LogInfo("[S1-A] floor loading detected");
                }
                return;
            }

            if (_pendingArenaPhase == 1 && !gm.IsLoadingLevel)
            {
                _pendingArenaSetup = false;
                if (!ArenaController.IsInDungeon())
                {
                    Logger.LogInfo("[S1-A] load finished but not in a dungeon");
                    return;
                }

                PlayerController p = gm.PrimaryPlayer;
                if (ArenaController.Setup(p))
                {
                    _arenaCheckAt = Time.unscaledTime + 1f;
                }
            }
        }

        private void TickArenaCheck(PlayerController player)
        {
            if (_arenaCheckAt <= 0f || Time.unscaledTime < _arenaCheckAt) return;
            _arenaCheckAt = 0f;
            ArenaController.LogCounts(player, "1s-after-setup");
            ArenaController.LogRoomInfo("1s-after-setup");
        }

        private void StartConsistencyTest()
        {
            if (_consistencyRunning)
            {
                Logger.LogInfo("[S1-A] consistency test already running");
                return;
            }
            StartCoroutine(ConsistencyTestCR(20));
        }

        private IEnumerator ConsistencyTestCR(int cycles)
        {
            _consistencyRunning = true;
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Logger.LogInfo("[S1-A] consistency test: no GameManager");
                _consistencyRunning = false;
                yield break;
            }

            ArenaController.SetTestMode(true);

            if (gm.IsFoyer)
            {
                gm.IsSelectingCharacter = false;
                GameManager.IsReturningToBreach = false;
                if (!ArenaController.StartFirstFloorFromFoyer())
                {
                    Logger.LogInfo("[S1-A] consistency test: cannot start 1-1 (need Pilot)");
                    ArenaController.SetTestMode(false);
                    _consistencyRunning = false;
                    yield break;
                }

                Logger.LogInfo("[S1-A] consistency test: waiting for 1-1 load");
                float waited = 0f;
                while (!gm.IsLoadingLevel && waited < 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                while (gm.IsLoadingLevel)
                {
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(1f);
            }

            if (!ArenaController.IsInDungeon())
            {
                Logger.LogInfo("[S1-A] consistency test: not in dungeon");
                ArenaController.SetTestMode(false);
                _consistencyRunning = false;
                yield break;
            }

            int ok = 0;
            for (int i = 1; i <= cycles; i++)
            {
                PlayerController p = GetPlayer();
                if (!ArenaController.Setup(p))
                {
                    Logger.LogInfo("[S1-A] consistency test: setup failed at " + i);
                    break;
                }
                ok++;

                yield return new WaitForSecondsRealtime(0.3f);
                ArenaController.ClearSpawned(GetPlayer());
                yield return new WaitForSecondsRealtime(0.7f);
            }

            ArenaController.SetTestMode(false);
            Logger.LogInfo("[S1-A] consistency test DONE cycles=" + ok + "/" + cycles);
            _consistencyRunning = false;
        }

        private void ToggleTimescale()
        {
            float next = Mathf.Approximately(Time.timeScale, 1f) ? 2f : 1f;
            Time.timeScale = next;
            Logger.LogInfo("[E5-timescale] timescale=" + next.ToString("F1"));
        }

        private void LogProjectileCounts(PlayerController player)
        {
            int findCount = UnityEngine.Object.FindObjectsOfType<Projectile>().Length;
            int staticCount = StaticReferenceManager.AllProjectiles.Count;
            Logger.LogInfo(string.Format(
                "[E4-projectiles] static={0} harmony={1} find={2} added={3} removed={4} room={5}",
                staticCount, TrackedProjectiles.Count, findCount, ProjectilesAdded, ProjectilesRemoved,
                player.CurrentRoom != null ? player.CurrentRoom.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) : -1));
        }

        private void DumpBulletKinEntries()
        {
            if (!EnemyDatabase.HasInstance)
            {
                Logger.LogInfo("[E3-db] EnemyDatabase not loaded yet");
                return;
            }

            var entries = EnemyDatabase.Instance.Entries;
            int shown = 0;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                EnemyDatabaseEntry e = entries[i];
                if (e == null) continue;
                string n = e.name ?? "";
                if (n.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("kin", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                sb.AppendLine("  name=" + n + " guid=" + e.myGuid + " normal=" + e.isNormalEnemy);
                shown++;
            }
            Logger.LogInfo("[E3-db] matches=" + shown + "\n" + sb.ToString());
        }

        private static string Fmt(Vector2 v)
        {
            return "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + ")";
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2") + ")";
        }
    }

    [HarmonyPatch(typeof(Projectile), "Start")]
    internal static class ProjectileStartPatch
    {
        private static void Postfix(Projectile __instance)
        {
            EtgGymPlugin.TrackAdd(__instance);
        }
    }

    [HarmonyPatch(typeof(Projectile), "OnDespawned")]
    internal static class ProjectileDespawnPatch
    {
        private static void Postfix(Projectile __instance)
        {
            EtgGymPlugin.TrackRemove(__instance);
        }
    }

    [HarmonyPatch(typeof(Projectile), "OnDestroy")]
    internal static class ProjectileDestroyPatch
    {
        private static void Postfix(Projectile __instance)
        {
            EtgGymPlugin.TrackRemove(__instance);
        }
    }
}
