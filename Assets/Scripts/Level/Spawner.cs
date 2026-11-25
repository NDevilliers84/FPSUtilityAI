using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace ZombieFPS
{
    /// <summary>Instantiates enemies and pickups from their prefabs, within global limits.</summary>
    public class Spawner : MonoBehaviour
    {
        [Serializable]
        public struct EnemyEntry
        {
            public EnemyType type;
            public Zombie prefab;
        }

        [Serializable]
        public struct PickupEntry
        {
            public PickupType type;
            public Pickup prefab;
        }

        public EnemyEntry[] enemies = Array.Empty<EnemyEntry>();
        public PickupEntry[] pickups = Array.Empty<PickupEntry>();
        public int maxAliveEnemies = 60;
        public int maxPickups = 25;

        Transform enemyRoot;
        Transform pickupRoot;

        void Awake()
        {
            enemyRoot = new GameObject("Enemies").transform;
            pickupRoot = new GameObject("Pickups").transform;
        }

        internal Zombie SpawnEnemy(EnemyType type, Vector3 position, bool alerted, float scatter)
        {
            if (Zombie.All.Count >= maxAliveEnemies) return null;

            Zombie prefab = null;
            foreach (EnemyEntry entry in enemies)
                if (entry.type == type) prefab = entry.prefab;
            if (prefab == null) return null;

            Vector2 offset = Random.insideUnitCircle * scatter;
            Vector3 target = position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas) &&
                !NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas))
                return null;

            Zombie zombie = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), enemyRoot);
            if (alerted) zombie.Alert();
            GameManager.Instance.Events.RaiseEnemySpawned(zombie.Info);
            return zombie;
        }

        internal Pickup SpawnPickup(PickupType type, Vector3 position)
        {
            if (Pickup.All.Count >= maxPickups) return null;

            Pickup prefab = null;
            foreach (PickupEntry entry in pickups)
                if (entry.type == type) prefab = entry.prefab;
            if (prefab == null) return null;

            return Instantiate(prefab, position, Quaternion.identity, pickupRoot);
        }
    }
}
