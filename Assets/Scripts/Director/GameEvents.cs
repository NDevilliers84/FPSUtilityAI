using System;

namespace ZombieFPS
{
    /// <summary>
    /// Gameplay events, raised the moment they happen (between Director ticks).
    /// Subscribe in IAIDirector.OnGameStart, e.g. <c>context.Events.PlayerDamaged += amount => ...;</c>
    /// </summary>
    public sealed class GameEvents
    {
        /// <summary>Player lost health. Argument: damage amount.</summary>
        public event Action<float> PlayerDamaged;
        /// <summary>Player regained health. Argument: amount actually healed.</summary>
        public event Action<float> PlayerHealed;
        /// <summary>An enemy appeared (spawned by the Director or by a horde).</summary>
        public event Action<EnemyInfo> EnemySpawned;
        /// <summary>An enemy died. See EnemyInfo.KilledByPlayer / KilledByHeadshot.</summary>
        public event Action<EnemyInfo> EnemyKilled;
        /// <summary>The player picked up an item.</summary>
        public event Action<PickupInfo> PickupCollected;
        /// <summary>The player fired a weapon.</summary>
        public event Action<WeaponType> ShotFired;
        /// <summary>The player entered a new zone. Argument: zone index.</summary>
        public event Action<int> ZoneEntered;
        public event Action<WorldEventType> WorldEventStarted;
        public event Action<WorldEventType> WorldEventEnded;
        /// <summary>The run ended. Argument: true on victory, false on defeat.</summary>
        public event Action<bool> GameOver;

        internal void RaisePlayerDamaged(float amount) => PlayerDamaged?.Invoke(amount);
        internal void RaisePlayerHealed(float amount) => PlayerHealed?.Invoke(amount);
        internal void RaiseEnemySpawned(EnemyInfo info) => EnemySpawned?.Invoke(info);
        internal void RaiseEnemyKilled(EnemyInfo info) => EnemyKilled?.Invoke(info);
        internal void RaisePickupCollected(PickupInfo info) => PickupCollected?.Invoke(info);
        internal void RaiseShotFired(WeaponType weapon) => ShotFired?.Invoke(weapon);
        internal void RaiseZoneEntered(int zone) => ZoneEntered?.Invoke(zone);
        internal void RaiseWorldEventStarted(WorldEventType type) => WorldEventStarted?.Invoke(type);
        internal void RaiseWorldEventEnded(WorldEventType type) => WorldEventEnded?.Invoke(type);
        internal void RaiseGameOver(bool victory) => GameOver?.Invoke(victory);
    }
}
