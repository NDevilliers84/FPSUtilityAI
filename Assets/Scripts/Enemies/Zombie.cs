using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieFPS
{
    /// <summary>
    /// Zombie behaviour: wanders around its spawn until it sees or hears the player,
    /// then chases and attacks. Stats are tuned per prefab.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Zombie : MonoBehaviour, IDamageable
    {
        /// <summary>All living zombies.</summary>
        public static readonly List<Zombie> All = new List<Zombie>();
        static int nextId;

        public EnemyType type;

        [Header("Stats")]
        public float maxHealth = 50f;
        public float wanderSpeed = 1.2f;
        public float chaseSpeed = 4.5f;
        public float attackDamage = 8f;
        public float attackRange = 1.6f;
        public float attackInterval = 1.1f;

        [Header("Senses")]
        public float sightRadius = 18f;
        [Tooltip("The player is always noticed below this distance, even out of sight.")]
        public float proximityRadius = 5f;
        public float wanderRadius = 6f;

        [Header("Explosion (Bloater)")]
        public bool explodes;
        public float explosionRadius = 4.5f;
        public float explosionDamage = 35f;

        public bool IsAlerted { get; private set; }
        public bool IsDead { get; private set; }
        public float Health { get; private set; }

        internal EnemyInfo Info { get; private set; }

        NavMeshAgent agent;
        Renderer[] renderers;
        MaterialPropertyBlock flashBlock;
        Vector3 home;
        float spawnTime;
        float nextRepath;
        float nextWander;
        float nextSense;
        float nextAttack;
        float flashEnd = -1f;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            renderers = GetComponentsInChildren<Renderer>();
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor("_BaseColor", Color.white);
            Health = maxHealth;
            spawnTime = Time.time;
            home = transform.position;
            nextWander = Time.time + Random.Range(0.5f, 3f);
            nextAttack = Time.time + 0.5f;

            Info = new EnemyInfo
            {
                Id = nextId++,
                Type = type,
                MaxHealth = maxHealth,
                Health = maxHealth,
                IsAlive = true,
                Position = transform.position,
                zombie = this,
            };
        }

        void OnEnable()
        {
            if (!IsDead) All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void Update()
        {
            if (IsDead) return;

            if (flashEnd > 0f && Time.time > flashEnd)
            {
                flashEnd = -1f;
                foreach (Renderer r in renderers) r.SetPropertyBlock(null);
            }

            GameManager gm = GameManager.Instance;
            if (gm == null || !agent.isOnNavMesh) return;

            PlayerController player = gm.player;
            if (gm.State != GameState.Playing || !player.Health.IsAlive)
            {
                if (!agent.isStopped) agent.isStopped = true;
                return;
            }
            if (agent.isStopped) agent.isStopped = false;

            Vector3 playerPos = player.transform.position;
            Vector3 toPlayer = playerPos - transform.position;
            float verticalGap = Mathf.Abs(toPlayer.y);
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (!IsAlerted)
            {
                Sense(player, distance);
                Wander(gm);
                return;
            }

            agent.speed = chaseSpeed * gm.EnemySpeedMultiplier;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.2f;
                agent.SetDestination(playerPos);
            }

            if (distance <= attackRange && verticalGap < 2.2f)
            {
                if (distance > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * 10f);

                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + attackInterval;
                    if (explodes)
                    {
                        Die(false, false);
                        return;
                    }

                    player.Health.TakeDamage(new DamageInfo
                    {
                        amount = attackDamage * gm.EnemyDamageMultiplier,
                        point = playerPos + Vector3.up,
                        direction = toPlayer.normalized,
                    });
                }
            }
        }

        void Sense(PlayerController player, float distance)
        {
            if (Time.time < nextSense) return;
            nextSense = Time.time + Random.Range(0.25f, 0.45f);

            if (distance <= proximityRadius)
            {
                Alert();
                return;
            }

            if (distance <= sightRadius)
            {
                Vector3 eyes = transform.position + Vector3.up * 1.6f;
                if (!Physics.Linecast(eyes, player.EyePosition, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                    Alert();
            }
        }

        void Wander(GameManager gm)
        {
            agent.speed = wanderSpeed * gm.EnemySpeedMultiplier;
            if (Time.time < nextWander) return;
            nextWander = Time.time + Random.Range(3f, 7f);

            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 target = home + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        /// <summary>Starts chasing the player.</summary>
        public void Alert()
        {
            if (IsAlerted || IsDead) return;
            IsAlerted = true;
            nextRepath = 0f;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || info.amount <= 0f) return;

            float amount = Mathf.Min(info.amount, Health);
            Health -= amount;
            if (info.fromPlayer && GameManager.Instance != null)
                GameManager.Instance.Stats.RecordDamageDealt(amount);

            Alert();
            foreach (Renderer r in renderers) r.SetPropertyBlock(flashBlock);
            flashEnd = Time.time + 0.06f;

            if (Health <= 0f) Die(info.fromPlayer, info.isHeadshot);
        }

        void Die(bool byPlayer, bool headshot)
        {
            if (IsDead) return;
            IsDead = true;
            Health = 0f;
            All.Remove(this);

            Info.IsAlive = false;
            Info.Health = 0f;
            Info.KilledByPlayer = byPlayer;
            Info.KilledByHeadshot = headshot;

            agent.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (Renderer r in renderers) r.SetPropertyBlock(null);

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (byPlayer) gm.Stats.RecordKill(headshot);
                gm.Events.RaiseEnemyKilled(Info);
            }

            if (explodes)
            {
                Damage.Explode(transform.position + Vector3.up, explosionRadius, explosionDamage, byPlayer, 1f);
                Destroy(gameObject);
                return;
            }

            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }

            yield return new WaitForSeconds(2f);
            Vector3 from = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
            {
                transform.position = from + Vector3.down * t;
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>Removes the zombie without counting a kill.</summary>
        internal void Despawn()
        {
            if (IsDead) return;
            IsDead = true;
            All.Remove(this);
            Info.IsAlive = false;
            Destroy(gameObject);
        }

        internal static void AlertAround(Vector3 position, float radius)
        {
            float sqrRadius = radius * radius;
            foreach (Zombie zombie in All)
            {
                if ((zombie.transform.position - position).sqrMagnitude <= sqrRadius)
                    zombie.Alert();
            }
        }

        internal void RefreshInfo(Vector3 playerPos, Level level, PlayerController player)
        {
            Vector3 position = transform.position;
            Info.Position = position;
            Info.DistanceToPlayer = Vector3.Distance(position, playerPos);
            Info.Health = Health;
            Info.IsAlerted = IsAlerted;
            Info.IsVisibleToPlayer = player.CanSee(position + Vector3.up * 1.2f);
            Info.Progress = level.GetProgress(position);
            Info.Age = Time.time - spawnTime;
        }
    }
}
