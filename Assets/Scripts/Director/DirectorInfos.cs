using UnityEngine;

namespace ZombieFPS
{
    /// <summary>Read-only snapshot of the player, refreshed before each Director tick.</summary>
    public sealed class PlayerInfo
    {
        public Vector3 Position { get; internal set; }
        /// <summary>Horizontal look direction.</summary>
        public Vector3 Forward { get; internal set; }
        /// <summary>Horizontal speed in m/s.</summary>
        public float Speed { get; internal set; }
        public bool IsSprinting { get; internal set; }

        public float Health { get; internal set; }
        public float MaxHealth { get; internal set; }
        /// <summary>Health / MaxHealth (0..1).</summary>
        public float HealthRatio => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public bool IsAlive { get; internal set; }

        public WeaponType CurrentWeapon { get; internal set; }
        /// <summary>Rounds in the current weapon's magazine.</summary>
        public int AmmoInMagazine { get; internal set; }
        /// <summary>Reserve rounds of the current weapon. -1 means infinite (pistol).</summary>
        public int ReserveAmmo { get; internal set; }
        /// <summary>
        /// Remaining ammo of all owned limited weapons (shotgun, rifle) relative to their capacity (0..1).
        /// 1 when only the pistol is owned.
        /// </summary>
        public float AmmoRatio { get; internal set; }
        public int Grenades { get; internal set; }
        public int MaxGrenades { get; internal set; }

        /// <summary>Progress along the level path, 0 = start safe room, 1 = end safe room.</summary>
        public float Progress { get; internal set; }
        /// <summary>Distance travelled along the level path in meters.</summary>
        public float DistanceAlongPath { get; internal set; }
        /// <summary>Index of the zone the player is in (see DirectorContext.GetZoneName). -1 if between zones.</summary>
        public int CurrentZone { get; internal set; }
        public bool IsInSafeRoom { get; internal set; }

        internal readonly bool[] ownedWeapons = new bool[3];

        public bool HasWeapon(WeaponType weapon) => ownedWeapons[(int)weapon];
    }

    /// <summary>Read-only data about one enemy. The same instance is kept for the enemy's whole life.</summary>
    public sealed class EnemyInfo
    {
        /// <summary>Unique id for the whole run.</summary>
        public int Id { get; internal set; }
        public EnemyType Type { get; internal set; }
        public Vector3 Position { get; internal set; }
        public float DistanceToPlayer { get; internal set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; internal set; }
        public float HealthRatio => MaxHealth > 0f ? Health / MaxHealth : 0f;
        /// <summary>False once killed or despawned.</summary>
        public bool IsAlive { get; internal set; }
        /// <summary>True when the enemy is chasing the player.</summary>
        public bool IsAlerted { get; internal set; }
        /// <summary>True when the enemy is inside the player's view and not hidden behind level geometry.</summary>
        public bool IsVisibleToPlayer { get; internal set; }
        /// <summary>Position along the level path (0..1).</summary>
        public float Progress { get; internal set; }
        /// <summary>Seconds since this enemy spawned.</summary>
        public float Age { get; internal set; }
        /// <summary>Set when the enemy dies: true if the killing blow came from the player.</summary>
        public bool KilledByPlayer { get; internal set; }
        /// <summary>Set when the enemy dies: true if the killing blow was a headshot.</summary>
        public bool KilledByHeadshot { get; internal set; }

        internal Zombie zombie;
    }

    /// <summary>Read-only data about one pickup present in the level.</summary>
    public sealed class PickupInfo
    {
        public int Id { get; internal set; }
        public PickupType Type { get; internal set; }
        public Vector3 Position { get; internal set; }
        public float DistanceToPlayer { get; internal set; }
        /// <summary>Position along the level path (0..1).</summary>
        public float Progress { get; internal set; }
        /// <summary>Seconds since this pickup appeared.</summary>
        public float Age { get; internal set; }
        /// <summary>False once collected or removed.</summary>
        public bool IsAvailable { get; internal set; }

        internal Pickup pickup;
    }

    /// <summary>
    /// A location authored in the level where the Director is allowed to spawn things.
    /// Enemy points accept enemies, Pickup points accept pickups.
    /// </summary>
    public sealed class SpawnPointInfo
    {
        public int Id { get; internal set; }
        public SpawnPointKind Kind { get; internal set; }
        public Vector3 Position { get; internal set; }
        /// <summary>Zone index this point belongs to.</summary>
        public int Zone { get; internal set; }
        /// <summary>Position along the level path (0..1).</summary>
        public float Progress { get; internal set; }
        public float DistanceToPlayer { get; internal set; }
        /// <summary>True when the point is inside the player's view and not hidden behind level geometry.</summary>
        public bool IsVisibleToPlayer { get; internal set; }
        /// <summary>True when the point is further along the path than the player.</summary>
        public bool IsAheadOfPlayer { get; internal set; }
        /// <summary>Pickup points only: true while a pickup spawned here has not been collected.</summary>
        public bool IsOccupied { get; internal set; }
        /// <summary>Seconds since something was last spawned here (infinity if never).</summary>
        public float TimeSinceLastSpawn { get; internal set; }

        internal SpawnPoint point;
    }
}
