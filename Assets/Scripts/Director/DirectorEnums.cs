namespace ZombieFPS
{
    /// <summary>Enemy archetypes the Director can spawn.</summary>
    public enum EnemyType
    {
        /// <summary>Average zombie. Wanders until it sees or hears the player.</summary>
        Common,
        /// <summary>Fast and fragile.</summary>
        Runner,
        /// <summary>Huge, slow to kill, hits very hard.</summary>
        Tank,
        /// <summary>Slow. Explodes on contact or on death, hurting everything around (zombies included).</summary>
        Bloater,
    }

    /// <summary>Items the Director can spawn on pickup spawn points.</summary>
    public enum PickupType
    {
        /// <summary>Restores health. Not collected if the player is at full health.</summary>
        Health,
        /// <summary>Refills part of the reserve ammo of every owned weapon (pistol is infinite).</summary>
        Ammo,
        /// <summary>+1 grenade.</summary>
        Grenade,
        /// <summary>Gives the shotgun (or ammo for it if already owned).</summary>
        Shotgun,
        /// <summary>Gives the rifle (or ammo for it if already owned).</summary>
        Rifle,
    }

    public enum WeaponType
    {
        Pistol,
        Shotgun,
        Rifle,
    }

    /// <summary>Global events the Director can trigger.</summary>
    public enum WorldEventType
    {
        /// <summary>Negative. A wave of alerted zombies rushes the player from out-of-sight spawn points.</summary>
        Horde,
        /// <summary>Positive. A cluster of resources appears a few meters ahead of the player.</summary>
        SupplyDrop,
        /// <summary>Negative. Thick fog reduces visibility for a duration.</summary>
        Fog,
        /// <summary>Negative. Lights go out for a duration (the flashlight turns on).</summary>
        Blackout,
        /// <summary>Positive. Player moves faster, deals more damage and reloads faster for a duration.</summary>
        Adrenaline,
    }

    public enum SpawnPointKind
    {
        Enemy,
        Pickup,
    }

    public enum GameState
    {
        Playing,
        Paused,
        Victory,
        Defeat,
    }
}
