using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using Dungeonator;
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

        private const string BulletKinGuid = "01972dee89fc4404a5c408d50007dad5";

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

        // E3 spawn/clear
        private readonly List<AIActor> _spawnedEnemies = new List<AIActor>();
        private float _clearCheckAt;

        private void Awake()
        {
            Logger.LogInfo(Name + " " + Version + " loaded.");
            Logger.LogInfo("Keys: F1 quick restart | F2 reset | F3 clear spawned | F4 spawn 3 BulletKin | F5 timescale | F6 projectile counts | F7 dump DB | F8 status | F9 move | F10 roll | F11 fire | F12 aim");

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
            if (Input.GetKeyDown(KeyCode.F1)) QuickRestartTest();
            if (Input.GetKeyDown(KeyCode.F2)) ResetOverrides();

            if (Input.GetKeyDown(KeyCode.F8))
            {
                _verbose = !_verbose;
                Logger.LogInfo("EtgGym status log: " + (_verbose ? "ON" : "OFF"));
                if (_verbose) LogStatus("manual");
            }

            if (Input.GetKeyDown(KeyCode.F7)) DumpBulletKinEntries();

            PlayerController player = GetPlayer();
            if (player == null)
            {
                if (_verbose) LogStatus("no-player");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3)) ClearSpawnedEnemies(player);
            if (Input.GetKeyDown(KeyCode.F4)) SpawnBulletKin(player);
            if (Input.GetKeyDown(KeyCode.F5)) ToggleTimescale();
            if (Input.GetKeyDown(KeyCode.F6)) LogProjectileCounts(player);
            if (Input.GetKeyDown(KeyCode.F9)) StartMoveTest(player);
            if (Input.GetKeyDown(KeyCode.F10)) TestDodgeRoll(player);
            if (Input.GetKeyDown(KeyCode.F11)) StartFireTest(player);
            if (Input.GetKeyDown(KeyCode.F12)) StartAimTest(player);

            TickMoveTest(player);
            TickFireTest(player);
            TickAimTest(player);
            TickClearCheck(player);

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

        private void QuickRestartTest()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.PrimaryPlayer == null || gm.IsLoadingLevel)
            {
                Logger.LogInfo("[S0] QuickRestart ignored: no active run/player (menu or loading)");
                return;
            }
            gm.PreventPausing = true;
            gm.QuickRestart();
            Logger.LogInfo("[S0] QuickRestart requested; PreventPausing=true");
        }

        private void ResetOverrides()
        {
            _moveTest = false;
            _fireTest = false;
            _aimTest = false;
            _fireUpPending = false;
            Time.timeScale = 1f;

            PlayerController player = GetPlayer();
            if (GameManager.Instance != null) GameManager.Instance.PreventPausing = false;
            if (player != null)
            {
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
            PlayerController player = GetPlayer();
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

        private void SpawnBulletKin(PlayerController player)
        {
            RoomHandler room = player.CurrentRoom;
            if (room == null)
            {
                Logger.LogInfo("[E3-spawn] no current room");
                return;
            }

            AIActor prefab = EnemyDatabase.GetOrLoadByName("BulletMan");
            if (prefab == null)
            {
                Logger.LogInfo("[E3-spawn] BulletMan prefab not found");
                return;
            }

            _spawnedEnemies.Clear();
            Vector2 origin = player.CenterPosition;
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(-3f, 3f),
                new Vector2(0f, 4f),
                new Vector2(3f, 3f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                AIActor actor = AIActor.Spawn(prefab, origin + offsets[i], room, true, AIActor.AwakenAnimationType.Spawn, true);
                if (actor != null)
                {
                    _spawnedEnemies.Add(actor);
                    Logger.LogInfo("[E3-spawn] #" + (i + 1) + " name=" + actor.name + " guid=" + actor.EnemyGuid + " pos=" + Fmt(actor.CenterPosition));
                }
                else
                {
                    Logger.LogInfo("[E3-spawn] #" + (i + 1) + " FAILED");
                }
            }

            Logger.LogInfo("[E3-spawn] requested=3 spawned=" + _spawnedEnemies.Count + " roomAll=" + room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) + " roomClear=" + room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear));
        }

        private void ClearSpawnedEnemies(PlayerController player)
        {
            int before = CountAlive(_spawnedEnemies);
            for (int i = 0; i < _spawnedEnemies.Count; i++)
            {
                AIActor actor = _spawnedEnemies[i];
                if (actor == null || actor.healthHaver == null || !actor.healthHaver.IsAlive) continue;
                actor.healthHaver.ApplyDamage(99999f, Vector2.zero, "EtgGym", CoreDamageTypes.None, DamageCategory.Unstoppable, true, null, true);
            }

            RoomHandler room = player.CurrentRoom;
            int roomAll = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) : -1;
            int roomClear = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear) : -1;
            Logger.LogInfo("[E3-clear] aliveBefore=" + before + " roomAll=" + roomAll + " roomClear=" + roomClear);
            _clearCheckAt = Time.unscaledTime + 1f;
        }

        private void TickClearCheck(PlayerController player)
        {
            if (_clearCheckAt <= 0f || Time.unscaledTime < _clearCheckAt) return;
            _clearCheckAt = 0f;

            int alive = CountAlive(_spawnedEnemies);
            RoomHandler room = player.CurrentRoom;
            int roomAll = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) : -1;
            int roomClear = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear) : -1;
            Logger.LogInfo("[E3-clear] checkAlive=" + alive + " roomAll=" + roomAll + " roomClear=" + roomClear);
        }

        private static int CountAlive(List<AIActor> actors)
        {
            int alive = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                AIActor actor = actors[i];
                if (actor != null && actor.healthHaver != null && actor.healthHaver.IsAlive) alive++;
            }
            return alive;
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
