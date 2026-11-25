using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieFPS
{
    /// <summary>Implementation of the world events (horde, supply drop, fog, blackout, adrenaline).</summary>
    public class WorldEvents : MonoBehaviour
    {
        [Header("Horde")]
        public float hordeSpawnInterval = 0.15f;
        [Range(0f, 1f)] public float hordeRunnerRatio = 0.15f;
        public float hordeMinDistance = 18f;
        public float hordeMaxDistance = 70f;
        public int hordeSpawnPointCount = 3;

        [Header("Supply drop")]
        public PickupType[] supplyDropContents = { PickupType.Health, PickupType.Ammo, PickupType.Grenade };
        public float supplyDropDistanceAhead = 12f;

        [Header("Fog")]
        public float fogDensity = 0.09f;
        public Color fogColor = new Color(0.45f, 0.48f, 0.5f);

        [Header("Blackout")]
        public Light sun;
        public float blackoutSunIntensity = 0.02f;
        public Color blackoutAmbient = new Color(0.02f, 0.02f, 0.03f);

        [Header("Adrenaline")]
        public float adrenalineSpeedMultiplier = 1.3f;
        public float adrenalineDamageMultiplier = 1.5f;
        public float adrenalineReloadMultiplier = 2f;

        const int EventCount = 5;
        readonly float[] endTimes = new float[EventCount];
        readonly float[] lastTriggerTimes = new float[EventCount];
        readonly int[] triggerCounts = new int[EventCount];
        readonly bool[] active = new bool[EventCount];

        readonly List<SpawnPoint> hordePoints = new List<SpawnPoint>();
        int hordePending;
        float nextHordeSpawn;
        int hordeIndex;

        float baseSunIntensity;
        Color baseAmbient;
        Color baseFogColor;
        float baseFogDensity;
        bool baseFog;
        float fogBlend;
        float blackoutBlend;

        internal int PendingHordeEnemies => hordePending;

        void Awake()
        {
            for (int i = 0; i < EventCount; i++) lastTriggerTimes[i] = float.NegativeInfinity;
            if (sun != null) baseSunIntensity = sun.intensity;
            baseAmbient = RenderSettings.ambientLight;
            baseFog = RenderSettings.fog;
            baseFogColor = RenderSettings.fogColor;
            baseFogDensity = RenderSettings.fogDensity;
        }

        float Now => GameManager.Instance.GameTime;

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            UpdateTimedEvent(WorldEventType.Fog);
            UpdateTimedEvent(WorldEventType.Blackout);
            UpdateTimedEvent(WorldEventType.Adrenaline);
            UpdateHorde(gm);
            UpdateAtmosphere(gm);
        }

        // ---------------------------------------------------------------- Queries

        internal bool IsActive(WorldEventType type) => active[(int)type];

        internal float GetRemainingTime(WorldEventType type)
        {
            return active[(int)type] ? Mathf.Max(0f, endTimes[(int)type] - Now) : 0f;
        }

        internal float TimeSinceLastTrigger(WorldEventType type) => Now - lastTriggerTimes[(int)type];

        internal int TriggerCount(WorldEventType type) => triggerCounts[(int)type];

        // ---------------------------------------------------------------- Timed events

        internal bool StartTimedEvent(WorldEventType type, float duration)
        {
            if (duration <= 0f || type == WorldEventType.Horde || type == WorldEventType.SupplyDrop) return false;

            int index = (int)type;
            endTimes[index] = Mathf.Max(endTimes[index], Now + duration);
            MarkTriggered(type);

            if (!active[index])
            {
                active[index] = true;
                OnTimedEventChanged(type, true);
                GameManager.Instance.Events.RaiseWorldEventStarted(type);
            }
            return true;
        }

        void UpdateTimedEvent(WorldEventType type)
        {
            int index = (int)type;
            if (!active[index] || Now < endTimes[index]) return;

            active[index] = false;
            OnTimedEventChanged(type, false);
            GameManager.Instance.Events.RaiseWorldEventEnded(type);
        }

        void OnTimedEventChanged(WorldEventType type, bool on)
        {
            PlayerController player = GameManager.Instance.player;
            switch (type)
            {
                case WorldEventType.Blackout:
                    if (on && player.flashlight != null) player.flashlight.enabled = true;
                    if (on) Hud.ShowMessage("The lights went out...", 3f);
                    break;

                case WorldEventType.Fog:
                    if (on) Hud.ShowMessage("A thick fog rolls in...", 3f);
                    break;

                case WorldEventType.Adrenaline:
                    player.speedMultiplier = on ? adrenalineSpeedMultiplier : 1f;
                    player.Weapons.damageMultiplier = on ? adrenalineDamageMultiplier : 1f;
                    player.Weapons.reloadSpeedMultiplier = on ? adrenalineReloadMultiplier : 1f;
                    if (on) Hud.ShowMessage("ADRENALINE RUSH", 2.5f);
                    break;
            }
        }

        void UpdateAtmosphere(GameManager gm)
        {
            float dt = Time.deltaTime;
            fogBlend = Mathf.MoveTowards(fogBlend, active[(int)WorldEventType.Fog] ? 1f : 0f, dt * 0.4f);
            blackoutBlend = Mathf.MoveTowards(blackoutBlend, active[(int)WorldEventType.Blackout] ? 1f : 0f, dt * 1.5f);

            RenderSettings.fog = baseFog || fogBlend > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(baseFog ? baseFogDensity : 0f, fogDensity, fogBlend);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(baseFogColor, fogColor, fogBlend), Color.black, blackoutBlend);
            RenderSettings.ambientLight = Color.Lerp(baseAmbient, blackoutAmbient, blackoutBlend);
            if (sun != null) sun.intensity = Mathf.Lerp(baseSunIntensity, blackoutSunIntensity, blackoutBlend);
        }

        // ---------------------------------------------------------------- Horde

        internal bool TriggerHorde(int size)
        {
            if (size <= 0 || hordePending > 0) return false;

            GameManager gm = GameManager.Instance;
            PlayerController player = gm.player;
            Vector3 playerPos = player.transform.position;

            hordePoints.Clear();
            var candidates = new List<(SpawnPoint point, float score)>();
            foreach (SpawnPoint point in gm.level.SpawnPoints)
            {
                if (point.kind != SpawnPointKind.Enemy) continue;
                float distance = Vector3.Distance(point.transform.position, playerPos);
                if (player.CanSee(point.transform.position + Vector3.up)) continue;
                bool inRange = distance >= hordeMinDistance && distance <= hordeMaxDistance;
                candidates.Add((point, (inRange ? 0f : 1000f) + Mathf.Abs(distance - hordeMinDistance * 1.5f)));
            }
            if (candidates.Count == 0) return false;

            candidates.Sort((a, b) => a.score.CompareTo(b.score));
            for (int i = 0; i < candidates.Count && hordePoints.Count < hordeSpawnPointCount; i++)
                hordePoints.Add(candidates[i].point);

            hordePending = size;
            hordeIndex = 0;
            nextHordeSpawn = Time.time;
            MarkTriggered(WorldEventType.Horde);
            active[(int)WorldEventType.Horde] = true;
            gm.Events.RaiseWorldEventStarted(WorldEventType.Horde);
            Hud.ShowMessage("HERE THEY COME!", 3f);
            return true;
        }

        void UpdateHorde(GameManager gm)
        {
            if (hordePending <= 0 || gm.State != GameState.Playing || Time.time < nextHordeSpawn) return;
            nextHordeSpawn = Time.time + hordeSpawnInterval;

            SpawnPoint point = hordePoints[hordeIndex++ % hordePoints.Count];
            EnemyType type = Random.value < hordeRunnerRatio ? EnemyType.Runner : EnemyType.Common;
            Zombie zombie = gm.spawner.SpawnEnemy(type, point.transform.position, true, 2.5f);
            if (zombie != null) point.lastSpawnTime = Time.time;

            hordePending--;
            if (hordePending <= 0)
            {
                hordePending = 0;
                active[(int)WorldEventType.Horde] = false;
                gm.Events.RaiseWorldEventEnded(WorldEventType.Horde);
            }
        }

        // ---------------------------------------------------------------- Supply drop

        internal bool SupplyDrop()
        {
            GameManager gm = GameManager.Instance;
            Level level = gm.level;
            float along = level.GetDistanceAlongPath(gm.player.transform.position);
            Vector3 center = level.GetPointAtDistance(along + supplyDropDistanceAhead);
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 6f, NavMesh.AllAreas)) return false;

            int spawned = 0;
            for (int i = 0; i < supplyDropContents.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / supplyDropContents.Length;
                Vector3 position = hit.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;
                if (NavMesh.SamplePosition(position, out NavMeshHit itemHit, 2f, NavMesh.AllAreas)) position = itemHit.position;
                if (gm.spawner.SpawnPickup(supplyDropContents[i], position) != null) spawned++;
            }
            if (spawned == 0) return false;

            MarkTriggered(WorldEventType.SupplyDrop);
            gm.Events.RaiseWorldEventStarted(WorldEventType.SupplyDrop);
            gm.Events.RaiseWorldEventEnded(WorldEventType.SupplyDrop);
            Hud.ShowMessage("Supplies spotted ahead", 3f);
            return true;
        }

        void MarkTriggered(WorldEventType type)
        {
            lastTriggerTimes[(int)type] = Now;
            triggerCounts[(int)type]++;
        }
    }
}
