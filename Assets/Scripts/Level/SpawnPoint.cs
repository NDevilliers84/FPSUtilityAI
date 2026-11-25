using UnityEngine;

namespace ZombieFPS
{
    /// <summary>Location where the Director is allowed to spawn enemies or pickups.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public SpawnPointKind kind;
        public int zone;

        internal SpawnPointInfo Info { get; private set; }
        internal Pickup currentPickup;
        internal float lastSpawnTime = float.NegativeInfinity;

        internal void Initialize(int id, Level level)
        {
            Info = new SpawnPointInfo
            {
                Id = id,
                Kind = kind,
                Position = transform.position,
                Zone = zone,
                Progress = level.GetProgress(transform.position),
                TimeSinceLastSpawn = float.PositiveInfinity,
                point = this,
            };
        }

        internal void RefreshInfo(Vector3 playerPos, float playerAlong, PlayerController player, float now)
        {
            Vector3 position = transform.position;
            Info.DistanceToPlayer = Vector3.Distance(position, playerPos);
            Info.IsVisibleToPlayer = player.CanSee(position + Vector3.up);
            Info.IsAheadOfPlayer = Info.Progress * GameManager.Instance.level.PathLength > playerAlong;
            Info.IsOccupied = currentPickup != null;
            Info.TimeSinceLastSpawn = now - lastSpawnTime;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == SpawnPointKind.Enemy ? new Color(1f, 0.25f, 0.2f) : new Color(0.3f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
    }
}
