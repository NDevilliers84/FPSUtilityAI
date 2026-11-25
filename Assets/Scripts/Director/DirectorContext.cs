using System.Collections.Generic;
using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Everything the Director can read about the game. Refreshed right before each Tick.
    /// This is the input of your considerations / utility scores.
    /// </summary>
    public sealed class DirectorContext
    {
        // ---------------------------------------------------------------- Time & state

        /// <summary>Seconds since the run started (pauses excluded).</summary>
        public float GameTime { get; private set; }
        public GameState State { get; private set; }

        // ---------------------------------------------------------------- Player

        public PlayerInfo Player { get; } = new PlayerInfo();

        /// <summary>Raw performance history of the player (damage, kills, accuracy...).</summary>
        public GameStats Stats => gm.Stats;

        /// <summary>Gameplay events raised as they happen. Subscribe in OnGameStart.</summary>
        public GameEvents Events => gm.Events;

        // ---------------------------------------------------------------- Enemies

        /// <summary>All living enemies.</summary>
        public IReadOnlyList<EnemyInfo> Enemies => enemies;
        /// <summary>Spawn requests beyond this count are refused.</summary>
        public int MaxAliveEnemies => gm.spawner.maxAliveEnemies;
        /// <summary>Current multiplier applied to enemy damage (see DirectorActions.SetEnemyDamageMultiplier).</summary>
        public float EnemyDamageMultiplier => gm.EnemyDamageMultiplier;
        /// <summary>Current multiplier applied to enemy movement speed (see DirectorActions.SetEnemySpeedMultiplier).</summary>
        public float EnemySpeedMultiplier => gm.EnemySpeedMultiplier;

        // ---------------------------------------------------------------- Pickups

        /// <summary>All pickups currently lying in the level.</summary>
        public IReadOnlyList<PickupInfo> Pickups => pickups;
        /// <summary>Pickup spawn requests beyond this count are refused.</summary>
        public int MaxPickups => gm.spawner.maxPickups;

        // ---------------------------------------------------------------- Level

        /// <summary>Points where enemies can be spawned.</summary>
        public IReadOnlyList<SpawnPointInfo> EnemySpawnPoints => enemyPoints;
        /// <summary>Points where pickups can be spawned.</summary>
        public IReadOnlyList<SpawnPointInfo> PickupSpawnPoints => pickupPoints;
        /// <summary>Number of zones. Zone 0 is the start safe room, the last zone is the end safe room.</summary>
        public int ZoneCount => gm.level.ZoneCount;
        /// <summary>Total length of the level path in meters.</summary>
        public float PathLength => gm.level.PathLength;

        public string GetZoneName(int zone) => gm.level.GetZoneName(zone);

        // ---------------------------------------------------------------- World events

        /// <summary>State of the world events (horde, fog, blackout, adrenaline, supply drop).</summary>
        public WorldEventsInfo WorldEvents { get; }

        // ---------------------------------------------------------------- Helpers

        public int CountEnemies()
        {
            return enemies.Count;
        }

        public int CountEnemies(EnemyType type)
        {
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].Type == type) count++;
            return count;
        }

        /// <summary>Number of living enemies closer than radius to the player.</summary>
        public int CountEnemiesWithin(float radius)
        {
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].DistanceToPlayer <= radius) count++;
            return count;
        }

        /// <summary>Number of enemies currently chasing the player.</summary>
        public int CountAlertedEnemies()
        {
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].IsAlerted) count++;
            return count;
        }

        public int CountPickups(PickupType type)
        {
            int count = 0;
            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i].Type == type) count++;
            return count;
        }

        /// <summary>True if the position is inside the player's view and not hidden behind level geometry.</summary>
        public bool IsVisibleToPlayer(Vector3 position)
        {
            return gm.player != null && gm.player.CanSee(position + Vector3.up);
        }

        /// <summary>Progress (0..1) of any world position projected on the level path.</summary>
        public float GetProgress(Vector3 position)
        {
            return gm.level.GetProgress(position);
        }

        // ---------------------------------------------------------------- Internals

        readonly GameManager gm;
        readonly List<EnemyInfo> enemies = new List<EnemyInfo>();
        readonly List<PickupInfo> pickups = new List<PickupInfo>();
        readonly List<SpawnPointInfo> enemyPoints = new List<SpawnPointInfo>();
        readonly List<SpawnPointInfo> pickupPoints = new List<SpawnPointInfo>();

        internal DirectorContext(GameManager gm)
        {
            this.gm = gm;
            WorldEvents = new WorldEventsInfo(gm.worldEvents);
            foreach (SpawnPoint point in gm.level.SpawnPoints)
            {
                if (point.kind == SpawnPointKind.Enemy) enemyPoints.Add(point.Info);
                else pickupPoints.Add(point.Info);
            }
        }

        internal void Refresh()
        {
            GameTime = gm.GameTime;
            State = gm.State;

            PlayerController player = gm.player;
            Level level = gm.level;
            float now = Time.time;

            player.RefreshInfo(Player, level);
            Vector3 playerPos = Player.Position;
            float playerAlong = Player.DistanceAlongPath;

            enemies.Clear();
            foreach (Zombie zombie in Zombie.All)
            {
                zombie.RefreshInfo(playerPos, level, player);
                enemies.Add(zombie.Info);
            }

            pickups.Clear();
            foreach (Pickup pickup in Pickup.All)
            {
                pickup.RefreshInfo(playerPos, level);
                pickups.Add(pickup.Info);
            }

            foreach (SpawnPoint point in level.SpawnPoints)
                point.RefreshInfo(playerPos, playerAlong, player, now);
        }
    }

    /// <summary>Read-only state of the world events.</summary>
    public sealed class WorldEventsInfo
    {
        readonly WorldEvents events;

        internal WorldEventsInfo(WorldEvents events)
        {
            this.events = events;
        }

        /// <summary>
        /// Horde: true while horde zombies are still being spawned.
        /// Fog / Blackout / Adrenaline: true until their duration ends.
        /// SupplyDrop: always false (instantaneous).
        /// </summary>
        public bool IsActive(WorldEventType type) => events.IsActive(type);

        /// <summary>Seconds left for a timed event (Fog, Blackout, Adrenaline). 0 if inactive.</summary>
        public float GetRemainingTime(WorldEventType type) => events.GetRemainingTime(type);

        /// <summary>Seconds since the event was last triggered (infinity if never).</summary>
        public float TimeSinceLastTrigger(WorldEventType type) => events.TimeSinceLastTrigger(type);

        /// <summary>How many times the event has been triggered during this run.</summary>
        public int TriggerCount(WorldEventType type) => events.TriggerCount(type);

        /// <summary>Horde zombies queued but not spawned yet.</summary>
        public int PendingHordeEnemies => events.PendingHordeEnemies;
    }
}
