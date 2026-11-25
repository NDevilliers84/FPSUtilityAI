using System;
using System.Collections.Generic;

namespace ZombieFPS
{
    /// <summary>
    /// Raw performance data of the player since the start of the run.
    /// "InLast(seconds)" methods return values over a sliding time window (up to 120 s).
    /// "TimeSince..." properties return infinity when the event never happened.
    /// </summary>
    public sealed class GameStats
    {
        const float MaxWindow = 120f;

        sealed class Track
        {
            readonly List<(float time, float value)> entries = new List<(float, float)>();
            public float Total;
            public float LastTime = float.NegativeInfinity;

            public void Add(float time, float value)
            {
                entries.Add((time, value));
                Total += value;
                LastTime = time;
                if (entries.Count > 512 && time - entries[0].time > MaxWindow)
                    entries.RemoveAll(e => time - e.time > MaxWindow);
            }

            public float Sum(float now, float window)
            {
                float sum = 0f;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    if (now - entries[i].time > window) break;
                    sum += entries[i].value;
                }
                return sum;
            }
        }

        readonly Func<float> clock;
        readonly Track damageTaken = new Track();
        readonly Track damageDealt = new Track();
        readonly Track healing = new Track();
        readonly Track kills = new Track();
        readonly Track headshots = new Track();
        readonly Track shotsFired = new Track();
        readonly Track shotsHit = new Track();
        readonly Track pickups = new Track();

        internal GameStats(Func<float> clock)
        {
            this.clock = clock;
        }

        float Now => clock();

        public int TotalKills => (int)kills.Total;
        public int TotalHeadshots => (int)headshots.Total;
        public int TotalShotsFired => (int)shotsFired.Total;
        public int TotalShotsHit => (int)shotsHit.Total;
        public int TotalPickupsCollected => (int)pickups.Total;
        public float TotalDamageTaken => damageTaken.Total;
        public float TotalDamageDealt => damageDealt.Total;
        public float TotalHealing => healing.Total;
        /// <summary>Shots that hit an enemy / shots fired (0..1). A shotgun blast counts as one shot.</summary>
        public float Accuracy => shotsFired.Total > 0f ? shotsHit.Total / shotsFired.Total : 0f;

        public float DamageTakenInLast(float seconds) => damageTaken.Sum(Now, seconds);
        public float DamageDealtInLast(float seconds) => damageDealt.Sum(Now, seconds);
        public float HealingInLast(float seconds) => healing.Sum(Now, seconds);
        public int KillsInLast(float seconds) => (int)kills.Sum(Now, seconds);
        public int HeadshotsInLast(float seconds) => (int)headshots.Sum(Now, seconds);
        public int ShotsFiredInLast(float seconds) => (int)shotsFired.Sum(Now, seconds);
        public int ShotsHitInLast(float seconds) => (int)shotsHit.Sum(Now, seconds);
        public int PickupsCollectedInLast(float seconds) => (int)pickups.Sum(Now, seconds);

        /// <summary>Accuracy over the last seconds. Returns -1 if no shot was fired in that window.</summary>
        public float AccuracyInLast(float seconds)
        {
            float fired = shotsFired.Sum(Now, seconds);
            return fired > 0f ? shotsHit.Sum(Now, seconds) / fired : -1f;
        }

        public float TimeSinceDamageTaken => Now - damageTaken.LastTime;
        public float TimeSinceDamageDealt => Now - damageDealt.LastTime;
        public float TimeSinceKill => Now - kills.LastTime;
        public float TimeSinceShotFired => Now - shotsFired.LastTime;
        public float TimeSincePickup => Now - pickups.LastTime;
        /// <summary>Seconds since the player last took or dealt damage.</summary>
        public float TimeSinceLastCombat => Math.Min(TimeSinceDamageTaken, TimeSinceDamageDealt);

        internal void RecordDamageTaken(float amount) => damageTaken.Add(Now, amount);
        internal void RecordDamageDealt(float amount) => damageDealt.Add(Now, amount);
        internal void RecordHealing(float amount) => healing.Add(Now, amount);
        internal void RecordKill(bool headshot)
        {
            kills.Add(Now, 1f);
            if (headshot) headshots.Add(Now, 1f);
        }
        internal void RecordShot(bool hit)
        {
            shotsFired.Add(Now, 1f);
            if (hit) shotsHit.Add(Now, 1f);
        }
        internal void RecordPickup() => pickups.Add(Now, 1f);
    }
}
