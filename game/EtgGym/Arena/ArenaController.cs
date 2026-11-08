using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using Dungeonator;
using UnityEngine;

namespace EtgGym.Arena
{
    /// <summary>
    /// S1-A: fixed entrance room, fixed player spot, 3 fixed Bullet Kin spawns.
    /// Uses the game's own entrance room and the same teleport sequence as DarkSoulsReset.
    /// </summary>
    public static class ArenaController
    {
        public const string BulletKinPrefab = "BulletMan";
        public const string BulletKinGuid = "01972dee89fc4404a5c408d50007dad5";
        public const int FixedRunSeed = 20261108;

        // Fixed offsets from the player's teleport spot. Room-local is logged as well.
        private static readonly Vector2[] EnemyOffsets =
        {
            new Vector2(-3f, 3f),
            new Vector2(0f, 4f),
            new Vector2(3f, 3f)
        };

        private static readonly List<AIActor> Spawned = new List<AIActor>();

        /// <summary>Debug-only: keeps the player alive during 20x arena consistency tests.</summary>
        public static bool TestMode;

        public static bool IsInDungeon()
        {
            GameManager gm = GameManager.Instance;
            return gm != null
                && !gm.IsFoyer
                && gm.Dungeon != null
                && gm.Dungeon.data != null
                && gm.Dungeon.data.Entrance != null;
        }

        public static bool IsPilot(PlayerController player)
        {
            return player != null && player.characterIdentity == PlayableCharacters.Pilot;
        }

        public static void ToggleTestMode()
        {
            SetTestMode(!TestMode);
        }

        public static void SetTestMode(bool on)
        {
            TestMode = on;
            PlayerController player = GameManager.Instance != null ? GameManager.Instance.PrimaryPlayer : null;
            if (player != null && player.healthHaver != null)
            {
                player.healthHaver.PreventAllDamage = on;
                if (on)
                {
                    player.healthHaver.ForceSetCurrentHealth(player.healthHaver.GetMaxHealth());
                }
            }
            Log("[S1-A] test mode " + (on ? "ON" : "OFF") + " (player invulnerable=" + on + ")");
        }

        /// <summary>
        /// At the character-select screen, force the Pilot (PlayerRogue) via the normal selection flow.
        /// Does not use QuickRestart.
        /// </summary>
        public static bool ForcePilotAtCharacterSelect()
        {
            if (CharacterSelectController.HasSelected)
            {
                return false;
            }

            CharacterSelectController controller = UnityEngine.Object.FindObjectOfType<CharacterSelectController>();
            if (controller == null || controller.playerPrefabPaths == null)
            {
                return false;
            }

            string pilotPath = CharacterSelectController.GetCharacterPathFromIdentity(PlayableCharacters.Pilot);
            int index = -1;
            for (int i = 0; i < controller.playerPrefabPaths.Length; i++)
            {
                if (controller.playerPrefabPaths[i] == pilotPath)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Log("[S1-A] force-Pilot failed: '" + pilotPath + "' not in character select list");
                return false;
            }

            controller.currentSelected = index;
            MethodInfo doMethod = typeof(CharacterSelectController).GetMethod("Do", BindingFlags.Instance | BindingFlags.NonPublic);
            if (doMethod == null)
            {
                Log("[S1-A] force-Pilot failed: CharacterSelectController.Do not found");
                return false;
            }

            doMethod.Invoke(controller, null);
            Log("[S1-A] force-Pilot requested at character select, index=" + index + " path=" + pilotPath);
            return true;
        }

        /// <summary>
        /// Reuses the normal Foyer -> Gungeon door path. NOT QuickRestart.
        /// </summary>
        public static bool StartFirstFloorFromFoyer()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsFoyer)
            {
                Log("[S1-A] start-first-floor ignored: not in Foyer");
                return false;
            }

            if (!IsPilot(gm.PrimaryPlayer))
            {
                Log("[S1-A] start-first-floor refused: primary player is not Pilot");
                return false;
            }

            Foyer foyer = Foyer.Instance;
            if (foyer == null)
            {
                Log("[S1-A] start-first-floor failed: Foyer.Instance is null");
                return false;
            }

            gm.PreventPausing = false;
            gm.CurrentRunSeed = FixedRunSeed;
            Log("[S1-A] fixed run seed applied before load: " + FixedRunSeed);
            gm.DelayedLoadNextLevel(0.75f);
            foyer.OnDepartedFoyer();
            Log("[S1-A] Foyer door path started: DelayedLoadNextLevel(0.75) + OnDepartedFoyer");
            return true;
        }

        /// <summary>
        /// Teleports the player to the entrance room's centered clear spot and spawns 3 Bullet Kin.
        /// Safe to call repeatedly; previous tracked enemies are cleared first.
        /// </summary>
        public static bool Setup(PlayerController player)
        {
            GameManager gm = GameManager.Instance;
            if (player == null || gm == null)
            {
                Log("[S1-A] setup failed: player/GameManager null");
                return false;
            }
            if (!IsPilot(player))
            {
                Log("[S1-A] setup refused: character=" + player.characterIdentity + " (require Pilot)");
                return false;
            }
            if (gm.IsLoadingLevel)
            {
                Log("[S1-A] setup ignored: level loading");
                return false;
            }
            if (gm.IsFoyer || gm.Dungeon == null || gm.Dungeon.data == null || gm.Dungeon.data.Entrance == null)
            {
                Log("[S1-A] setup ignored: not in a dungeon (IsFoyer=" + gm.IsFoyer + ")");
                return false;
            }

            if (gm.IsPaused)
            {
                gm.ForceUnpause();
            }

            try
            {
                GameStatsManager gsm = GameStatsManager.Instance;
                if (gsm != null && gsm.rainbowRunToggled)
                {
                    gsm.rainbowRunToggled = false;
                    bool saved = GameStatsManager.Save();
                    Log("[S1-A] rainbow forced OFF + saved=" + saved);
                }
            }
            catch (Exception ex)
            {
                Log("[S1-A] rainbow force-off failed: " + ex.Message);
            }

            RoomHandler room = gm.Dungeon.data.Entrance;
            ClearSpawned(player);

            bool ok;
            IntVector2 cell = room.GetCenteredVisibleClearSpot(3, 3, out ok, restrictive: true);
            if (!ok)
            {
                cell = room.GetCenterCell();
                Log("[S1-A] centered clear spot failed; fallback GetCenterCell");
            }

            Vector3 target = new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.1f);
            Vector2 before = player.CenterPosition;

            player.transform.position = target;
            player.Reinitialize();
            if (player.CurrentRoom != room)
            {
                player.ForceChangeRoom(room);
            }
            player.CurrentInputState = PlayerInputState.AllInput;
            gm.MainCameraController.ForceToPlayerPosition(player);
            gm.PreventPausing = false;
            RepairPlayer(player);

            if (TestMode && player.healthHaver != null)
            {
                player.healthHaver.PreventAllDamage = true;
                player.healthHaver.ForceSetCurrentHealth(player.healthHaver.GetMaxHealth());
            }

            Vector2 local = player.CenterPosition - room.area.UnitBottomLeft;
            Log(string.Format(
                "[S1-A] player before=({0:F2},{1:F2}) cell=({2},{3}) world=({4:F2},{5:F2}) local=({6:F2},{7:F2}) clearSpotOk={8}",
                before.x, before.y, cell.x, cell.y,
                player.CenterPosition.x, player.CenterPosition.y,
                local.x, local.y, ok));

            LogRoomInfo("setup");

            AIActor prefab = EnemyDatabase.GetOrLoadByName(BulletKinPrefab);
            if (prefab == null)
            {
                Log("[S1-A] BulletMan prefab not found");
                return false;
            }

            Vector2 origin = player.CenterPosition;
            for (int i = 0; i < EnemyOffsets.Length; i++)
            {
                Vector2 desired = origin + EnemyOffsets[i];
                AIActor actor = AIActor.Spawn(prefab, desired, room, true, AIActor.AwakenAnimationType.Spawn, true);
                if (actor == null)
                {
                    Log("[S1-A] enemy#" + (i + 1) + " FAILED desired=(" + desired.x.ToString("F2") + "," + desired.y.ToString("F2") + ")");
                    continue;
                }

                Spawned.Add(actor);
                Vector2 actorLocal = actor.CenterPosition - room.area.UnitBottomLeft;
                Log(string.Format(
                    "[S1-A] enemy#{0} guid={1} desired=({2:F2},{3:F2}) actual=({4:F2},{5:F2}) local=({6:F2},{7:F2})",
                    i + 1, actor.EnemyGuid,
                    desired.x, desired.y,
                    actor.CenterPosition.x, actor.CenterPosition.y,
                    actorLocal.x, actorLocal.y));
            }

            LogCounts(player, "after-spawn");
            return true;
        }

        /// <summary>
        /// Re-bind the player's hand/gun after teleport or a broken character swap.
        /// Does not reset inventory unless CurrentGun is null.
        /// </summary>
        public static void RepairPlayer(PlayerController player)
        {
            if (player == null) return;

            GameManager gm = GameManager.Instance;
            bool pausedBefore = gm != null && gm.IsPaused;
            float timescaleBefore = Time.timeScale;
            bool hadForceAim = player.forceAimPoint.HasValue;
            Vector2 forceAim = hadForceAim ? player.forceAimPoint.Value : Vector2.zero;
            bool hadForcedInput = player.usingForcedInput;

            // A focus-loss pause freezes aiming and shooting even though input state looks fine.
            // Never set PreventPausing=true here: it makes PlayerController.AcceptingNonMotionInput
            // false, which disables aiming and shooting. Unpause instead.
            if (gm != null)
            {
                if (gm.IsPaused) gm.ForceUnpause();
                gm.PreventPausing = false;
            }
            if (Time.timeScale <= 0.001f) Time.timeScale = 1f;

            // Clear any forced-input / fire-test residue that can block normal shooting.
            player.usingForcedInput = false;
            player.forcedInput = Vector2.zero;
            player.forceFire = false;
            player.forceFireDown = false;
            player.forceFireUp = false;
            player.forceAimPoint = null;

            bool needGunReset = player.CurrentGun == null;
            if (!needGunReset && !player.CurrentGun.InfiniteAmmo && player.CurrentGun.CurrentAmmo <= 0)
            {
                needGunReset = true;
            }

            if (needGunReset)
            {
                Log("[S1-A] repair: gun null/empty; ReinitializeGuns()");
                player.ReinitializeGuns();
            }

            if (player.inventory != null)
            {
                player.inventory.ForceNoGun = false;
            }

            if (player.CurrentGun != null)
            {
                player.CurrentGun.gameObject.SetActive(true);
                if (player.gunAttachPoint != null && player.CurrentGun.transform.parent != player.gunAttachPoint)
                {
                    player.CurrentGun.transform.parent = player.gunAttachPoint;
                    player.CurrentGun.transform.localRotation = Quaternion.identity;
                    player.CurrentGun.UpdateAttachTransform();
                }
            }

            player.ProcessHandAttachment();
            player.CurrentInputState = PlayerInputState.AllInput;

            Log("[S1-A] repair: gun=" + (player.CurrentGun != null ? player.CurrentGun.name : "null")
                + " ammo=" + (player.CurrentGun != null ? player.CurrentGun.CurrentAmmo.ToString() : "?")
                + " infinite=" + (player.CurrentGun != null ? player.CurrentGun.InfiniteAmmo.ToString() : "?")
                + " parent=" + (player.CurrentGun != null && player.CurrentGun.transform.parent != null ? player.CurrentGun.transform.parent.name : "null")
                + " forceNoGun=" + (player.inventory != null ? player.inventory.ForceNoGun.ToString() : "?")
                + " input=" + player.CurrentInputState
                + " pausedBefore=" + pausedBefore
                + " timescale=" + timescaleBefore.ToString("F2") + "->" + Time.timeScale.ToString("F2")
                + " forceAimBefore=" + hadForceAim + (hadForceAim ? "(" + forceAim.x.ToString("F2") + "," + forceAim.y.ToString("F2") + ")" : "")
                + " forcedInputBefore=" + hadForcedInput);

            try
            {
                CameraController cam = GameManager.Instance != null ? GameManager.Instance.MainCameraController : null;
                BraveInput bi = BraveInput.GetInstanceForPlayer(player.PlayerIDX);
                bool braveNull = bi == null;
                bool actionsNull = bi == null || bi.ActiveActions == null;
                Vector2 move = actionsNull ? Vector2.zero : bi.ActiveActions.Move.Vector;
                Vector2 aim = actionsNull ? Vector2.zero : bi.ActiveActions.Aim.Vector;
                bool shoot = actionsNull ? false : bi.ActiveActions.ShootAction.IsPressed;
                object activeField = PrivateField(player, "m_activeActions");
                object falling = PrivateField(player, "m_isFalling");
                object overrideAngle = PrivateField(player, "m_overrideGunAngle");
                object currentAngle = PrivateField(player, "m_currentGunAngle");

                Log("[S1-A] diag: acceptingAny=" + player.AcceptingAnyInput
                    + " acceptingNonMotion=" + player.AcceptingNonMotionInput
                    + " falling=" + (falling ?? "?")
                    + " activeActionsFieldNull=" + (activeField == null)
                    + " braveNull=" + braveNull
                    + " actionsNull=" + actionsNull
                    + " move=" + V2(move) + " aim=" + V2(aim) + " shoot=" + shoot
                    + " overrideAngle=" + (overrideAngle ?? "null")
                    + " currentAngle=" + (currentAngle ?? "?")
                    + " camManual=" + (cam != null ? cam.ManualControl.ToString() : "?")
                    + " camLockX=" + (cam != null ? cam.LockX.ToString() : "?")
                    + " camLockY=" + (cam != null ? cam.LockY.ToString() : "?")
                    + " camLockRoom=" + (cam != null ? cam.LockToRoom.ToString() : "?")
                    + " camPreventAim=" + (cam != null ? cam.PreventAimLook.ToString() : "?")
                    + " vel=" + (player.specRigidbody != null ? V2(player.specRigidbody.Velocity) : "?")
                    + " rolling=" + player.IsDodgeRolling);
            }
            catch (Exception ex)
            {
                Log("[S1-A] diag failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Soft reset: clear all room enemies + all projectiles, heal player, then run Setup.
        /// Never uses QuickRestart.
        /// </summary>
        public static bool SoftReset(PlayerController player)
        {
            if (player == null || !IsPilot(player))
            {
                Log("[S1-B] soft reset refused: player null or not Pilot");
                return false;
            }
            if (!IsInDungeon())
            {
                Log("[S1-B] soft reset refused: not in dungeon");
                return false;
            }

            RoomHandler room = player.CurrentRoom;
            ClearAllEnemiesInRoom(room);
            ClearProjectiles();

            if (player.healthHaver != null)
            {
                player.healthHaver.PreventAllDamage = TestMode;
                player.healthHaver.ForceSetCurrentHealth(player.healthHaver.GetMaxHealth());
            }

            bool ok = Setup(player);
            Log("[S1-B] soft reset done ok=" + ok);
            return ok;
        }

        private static void ClearAllEnemiesInRoom(RoomHandler room)
        {
            if (room == null)
            {
                Log("[S1-B] clear enemies: room null");
                return;
            }

            List<AIActor> enemies = room.GetActiveEnemies(RoomHandler.ActiveEnemyType.All);
            if (enemies == null)
            {
                Log("[S1-B] clear enemies: activeEnemies null");
                return;
            }

            List<AIActor> copy = new List<AIActor>(enemies);
            int before = copy.Count;
            for (int i = 0; i < copy.Count; i++)
            {
                AIActor actor = copy[i];
                if (actor == null) continue;
                room.DeregisterEnemy(actor, true);
                actor.EraseFromExistence(true);
            }
            Log("[S1-B] clear enemies before=" + before + " roomAll=" + room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All));
        }

        private static void ClearProjectiles()
        {
            List<Projectile> copy = new List<Projectile>(StaticReferenceManager.AllProjectiles);
            int before = copy.Count;
            int despawned = 0;
            int destroyed = 0;
            for (int i = 0; i < copy.Count; i++)
            {
                Projectile p = copy[i];
                if (p == null) continue;
                // SpawnManager.Despawn only returns the object to its pool; it does not
                // necessarily invoke Projectile.OnDespawned, so explicitly remove it from
                // StaticReferenceManager.AllProjectiles first.
                p.OnDespawned();
                if (SpawnManager.Despawn(p.gameObject))
                {
                    despawned++;
                }
                else
                {
                    UnityEngine.Object.Destroy(p.gameObject);
                    destroyed++;
                }
            }
            Log("[S1-B] clear projectiles before=" + before + " despawned=" + despawned + " destroyed=" + destroyed
                + " after=" + StaticReferenceManager.AllProjectiles.Count);
        }

        public static int ClearSpawned(PlayerController player)
        {
            int before = CountAlive();
            RoomHandler room = player != null ? player.CurrentRoom : null;
            for (int i = 0; i < Spawned.Count; i++)
            {
                AIActor actor = Spawned[i];
                if (actor == null) continue;
                if (room != null)
                {
                    room.DeregisterEnemy(actor, true);
                }
                actor.EraseFromExistence(true);
            }
            Spawned.Clear();

            if (TestMode && player != null && player.healthHaver != null)
            {
                player.healthHaver.PreventAllDamage = true;
                player.healthHaver.ForceSetCurrentHealth(player.healthHaver.GetMaxHealth());
            }

            Log("[S1-A] clear trackedAliveBefore=" + before);
            LogCounts(player, "after-clear");
            return before;
        }

        public static int CountAlive()
        {
            int alive = 0;
            for (int i = 0; i < Spawned.Count; i++)
            {
                AIActor actor = Spawned[i];
                if (actor != null && actor.healthHaver != null && actor.healthHaver.IsAlive) alive++;
            }
            return alive;
        }

        public static void LogCounts(PlayerController player, string tag)
        {
            int floorAlive = 0;
            int floorTotal = 0;
            var all = StaticReferenceManager.AllEnemies;
            for (int i = 0; i < all.Count; i++)
            {
                AIActor actor = all[i];
                if (actor == null) continue;
                floorTotal++;
                if (actor.healthHaver != null && actor.healthHaver.IsAlive) floorAlive++;
            }

            RoomHandler room = player != null ? player.CurrentRoom : null;
            int roomAll = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.All) : -1;
            int roomClear = room != null ? room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear) : -1;

            Log(string.Format(
                "[S1-A] counts tag={0} trackedAlive={1}/{2} roomAll={3} roomClear={4} floorAlive={5}/{6} bullets={7}",
                tag, CountAlive(), Spawned.Count, roomAll, roomClear, floorAlive, floorTotal,
                StaticReferenceManager.AllProjectiles.Count));
        }

        public static void LogRoomInfo(string tag)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Dungeon == null || gm.Dungeon.data == null)
            {
                Log("[S1-A] room tag=" + tag + " unavailable (no Dungeon.data)");
                return;
            }

            RoomHandler room = gm.Dungeon.data.Entrance;
            if (room == null)
            {
                Log("[S1-A] room tag=" + tag + " Entrance=null");
                return;
            }

            CellArea area = room.area;
            Log(string.Format(
                "[S1-A] room tag={0} tileset={1} category={2} name={3} base=({4},{5}) dims=({6},{7}) center=({8:F2},{9:F2}) isEntrance={10}",
                tag,
                gm.Dungeon.tileIndices != null ? ((int)gm.Dungeon.tileIndices.tilesetId).ToString() : "?",
                area.PrototypeRoomCategory,
                area.PrototypeRoomName,
                area.basePosition.x, area.basePosition.y,
                area.dimensions.x, area.dimensions.y,
                area.UnitCenter.x, area.UnitCenter.y,
                room == gm.Dungeon.data.Entrance));
        }

        private static object PrivateField(object obj, string name)
        {
            if (obj == null) return null;
            FieldInfo field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(obj) : null;
        }

        private static string V2(Vector2 v)
        {
            return "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + ")";
        }

        private static void Log(string message)
        {
            ManualLogSource log = EtgGymPlugin.LogSource;
            if (log != null)
            {
                log.LogInfo(message);
            }
            else
            {
                Debug.Log(message);
            }
        }
    }
}
