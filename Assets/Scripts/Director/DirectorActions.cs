using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Everything the Director can do to the game. These are the outputs of your utility decisions.
    /// Every method returns false (and does nothing) when the request is invalid or refused.
    /// Every successful call is listed in the debug overlay (F1).
    /// </summary>
    public sealed class DirectorActions
    {
        readonly GameManager gm;

        internal DirectorActions(GameManager gm)
        {
            this.gm = gm;
        }

        // ---------------------------------------------------------------- Enemies

        /// <summary>
        /// Spawns one enemy on an enemy spawn point.
        /// Refused if the point is not an Enemy point or if MaxAliveEnemies is reached.
        /// </summary>
        /// <param name="alerted">If true the enemy immediately chases the player, otherwise it wanders until it notices them.</param>
        public bool SpawnEnemy(EnemyType type, SpawnPointInfo point, bool alerted = false)
        {
            return SpawnEnemies(type, 1, point, alerted) == 1;
        }

        /// <summary>
        /// Spawns a group of enemies around an enemy spawn point.
        /// Returns how many were actually spawned (limited by MaxAliveEnemies).
        /// </summary>
        public int SpawnEnemies(EnemyType type, int count, SpawnPointInfo point, bool alerted = false)
        {
            if (!IsRunning() || point == null || point.Kind != SpawnPointKind.Enemy || count <= 0)
                return 0;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float scatter = count > 1 ? 2f : 0f;
                if (gm.spawner.SpawnEnemy(type, point.Position, alerted, scatter) == null) break;
                spawned++;
            }

            if (spawned > 0)
            {
                point.point.lastSpawnTime = Time.time;
                DirectorDebug.LogAction($"Spawn {spawned}x {type}{(alerted ? " (alerted)" : "")} @ point {point.Id} [zone {point.Zone}]");
            }
            return spawned;
        }

        /// <summary>Instantly removes an enemy (no kill is counted). Useful to clean up enemies left far behind.</summary>
        public bool DespawnEnemy(EnemyInfo enemy)
        {
            if (enemy == null || !enemy.IsAlive || enemy.zombie == null) return false;
            enemy.zombie.Despawn();
            DirectorDebug.LogAction($"Despawn {enemy.Type} #{enemy.Id}");
            return true;
        }

        /// <summary>Makes an enemy start chasing the player.</summary>
        public bool AlertEnemy(EnemyInfo enemy)
        {
            if (enemy == null || !enemy.IsAlive || enemy.zombie == null || enemy.IsAlerted) return false;
            enemy.zombie.Alert();
            DirectorDebug.LogAction($"Alert {enemy.Type} #{enemy.Id}");
            return true;
        }

        /// <summary>Makes every living enemy chase the player. Returns how many were newly alerted.</summary>
        public int AlertAllEnemies()
        {
            int count = 0;
            foreach (Zombie zombie in Zombie.All)
            {
                if (zombie.IsAlerted) continue;
                zombie.Alert();
                count++;
            }
            if (count > 0) DirectorDebug.LogAction($"Alert all ({count})");
            return count;
        }

        /// <summary>Multiplies the damage dealt by every enemy. Clamped to [0.1, 5]. Default 1.</summary>
        public void SetEnemyDamageMultiplier(float multiplier)
        {
            multiplier = Mathf.Clamp(multiplier, 0.1f, 5f);
            if (Mathf.Approximately(multiplier, gm.EnemyDamageMultiplier)) return;
            gm.EnemyDamageMultiplier = multiplier;
            DirectorDebug.LogAction($"Enemy damage x{multiplier:0.##}");
        }

        /// <summary>Multiplies the movement speed of every enemy. Clamped to [0.5, 2]. Default 1.</summary>
        public void SetEnemySpeedMultiplier(float multiplier)
        {
            multiplier = Mathf.Clamp(multiplier, 0.5f, 2f);
            if (Mathf.Approximately(multiplier, gm.EnemySpeedMultiplier)) return;
            gm.EnemySpeedMultiplier = multiplier;
            DirectorDebug.LogAction($"Enemy speed x{multiplier:0.##}");
        }

        // ---------------------------------------------------------------- Pickups

        /// <summary>
        /// Spawns a pickup on a pickup spawn point.
        /// Refused if the point is not a Pickup point, is already occupied, or if MaxPickups is reached.
        /// </summary>
        public bool SpawnPickup(PickupType type, SpawnPointInfo point)
        {
            if (!IsRunning() || point == null || point.Kind != SpawnPointKind.Pickup || point.IsOccupied)
                return false;

            Pickup pickup = gm.spawner.SpawnPickup(type, point.Position);
            if (pickup == null) return false;

            pickup.origin = point.point;
            point.point.currentPickup = pickup;
            point.point.lastSpawnTime = Time.time;
            point.IsOccupied = true;
            DirectorDebug.LogAction($"Pickup {type} @ point {point.Id} [zone {point.Zone}]");
            return true;
        }

        /// <summary>Removes a pickup from the level.</summary>
        public bool RemovePickup(PickupInfo pickup)
        {
            if (pickup == null || !pickup.IsAvailable || pickup.pickup == null) return false;
            pickup.pickup.Remove();
            DirectorDebug.LogAction($"Remove pickup {pickup.Type} #{pickup.Id}");
            return true;
        }

        // ---------------------------------------------------------------- World events

        /// <summary>
        /// Negative event. Spawns <paramref name="size"/> alerted zombies (mostly Common, some Runners)
        /// over a few seconds from enemy spawn points hidden from the player.
        /// Refused while another horde is still spawning.
        /// </summary>
        public bool TriggerHorde(int size)
        {
            if (!IsRunning() || !gm.worldEvents.TriggerHorde(size)) return false;
            DirectorDebug.LogAction($"HORDE x{size}");
            return true;
        }

        /// <summary>Positive event. Drops health, ammo and a grenade a few meters ahead of the player.</summary>
        public bool SupplyDrop()
        {
            if (!IsRunning() || !gm.worldEvents.SupplyDrop()) return false;
            DirectorDebug.LogAction("Supply drop");
            return true;
        }

        /// <summary>Negative event. Thick fog for <paramref name="duration"/> seconds (re-triggering extends it).</summary>
        public bool StartFog(float duration)
        {
            return StartTimedEvent(WorldEventType.Fog, duration);
        }

        /// <summary>Negative event. Lights out for <paramref name="duration"/> seconds (re-triggering extends it).</summary>
        public bool StartBlackout(float duration)
        {
            return StartTimedEvent(WorldEventType.Blackout, duration);
        }

        /// <summary>Positive event. Faster movement, more damage, faster reloads for <paramref name="duration"/> seconds.</summary>
        public bool GiveAdrenaline(float duration)
        {
            return StartTimedEvent(WorldEventType.Adrenaline, duration);
        }

        // ---------------------------------------------------------------- Feedback

        /// <summary>Displays a message at the top of the player's screen.</summary>
        public void ShowMessage(string text, float duration = 3f)
        {
            Hud.ShowMessage(text, duration);
            DirectorDebug.LogAction($"Message \"{text}\"");
        }

        // ---------------------------------------------------------------- Internals

        bool StartTimedEvent(WorldEventType type, float duration)
        {
            if (!IsRunning() || !gm.worldEvents.StartTimedEvent(type, duration)) return false;
            DirectorDebug.LogAction($"{type} {duration:0.#}s");
            return true;
        }

        bool IsRunning() => gm.State == GameState.Playing;
    }
}
