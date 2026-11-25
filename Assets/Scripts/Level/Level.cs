using System;
using Unity.AI.Navigation;
using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Level layout: the main path (used to compute progress), the zones and the spawn points.
    /// </summary>
    public class Level : MonoBehaviour
    {
        [Serializable]
        public struct ZoneArea
        {
            public int zone;
            public Bounds bounds;
        }

        [Tooltip("Polyline going from the start safe room to the end safe room.")]
        public Vector3[] pathPoints = Array.Empty<Vector3>();
        public string[] zoneNames = Array.Empty<string>();
        public ZoneArea[] areas = Array.Empty<ZoneArea>();

        public int ZoneCount => zoneNames.Length;
        public int LastZone => ZoneCount - 1;
        public float PathLength { get; private set; }
        internal SpawnPoint[] SpawnPoints { get; private set; }

        float[] cumulative;

        void Awake()
        {
            ComputePath();

            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            if (surface != null && surface.navMeshData == null) surface.BuildNavMesh();

            SpawnPoints = GetComponentsInChildren<SpawnPoint>();
            for (int i = 0; i < SpawnPoints.Length; i++) SpawnPoints[i].Initialize(i, this);
        }

        void ComputePath()
        {
            cumulative = new float[pathPoints.Length];
            PathLength = 0f;
            for (int i = 1; i < pathPoints.Length; i++)
            {
                PathLength += Flat(pathPoints[i] - pathPoints[i - 1]).magnitude;
                cumulative[i] = PathLength;
            }
        }

        public string GetZoneName(int zone)
        {
            return zone >= 0 && zone < zoneNames.Length ? zoneNames[zone] : "-";
        }

        /// <summary>Zone containing the position, -1 if none.</summary>
        public int GetZoneIndex(Vector3 position)
        {
            foreach (ZoneArea area in areas)
            {
                Bounds b = area.bounds;
                if (position.x >= b.min.x && position.x <= b.max.x && position.z >= b.min.z && position.z <= b.max.z)
                    return area.zone;
            }
            return -1;
        }

        /// <summary>Distance in meters along the path of the closest point on the path.</summary>
        public float GetDistanceAlongPath(Vector3 position)
        {
            if (cumulative == null || cumulative.Length != pathPoints.Length) ComputePath();
            if (pathPoints.Length < 2) return 0f;

            Vector3 p = Flat(position);
            float bestSqr = float.MaxValue;
            float bestAlong = 0f;
            for (int i = 1; i < pathPoints.Length; i++)
            {
                Vector3 a = Flat(pathPoints[i - 1]);
                Vector3 segment = Flat(pathPoints[i]) - a;
                float length = segment.magnitude;
                float t = length > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, segment) / (length * length)) : 0f;
                float sqr = (a + segment * t - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestAlong = cumulative[i - 1] + t * length;
                }
            }
            return bestAlong;
        }

        public float GetProgress(Vector3 position)
        {
            float along = GetDistanceAlongPath(position);
            return PathLength > 0f ? along / PathLength : 0f;
        }

        public Vector3 GetPointAtDistance(float distance)
        {
            if (cumulative == null || cumulative.Length != pathPoints.Length) ComputePath();
            if (pathPoints.Length == 0) return transform.position;

            distance = Mathf.Clamp(distance, 0f, PathLength);
            for (int i = 1; i < pathPoints.Length; i++)
            {
                if (distance <= cumulative[i])
                {
                    float segmentLength = cumulative[i] - cumulative[i - 1];
                    float t = segmentLength > 0f ? (distance - cumulative[i - 1]) / segmentLength : 0f;
                    return Vector3.Lerp(pathPoints[i - 1], pathPoints[i], t);
                }
            }
            return pathPoints[pathPoints.Length - 1];
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 1; i < pathPoints.Length; i++)
                Gizmos.DrawLine(pathPoints[i - 1] + Vector3.up * 0.2f, pathPoints[i] + Vector3.up * 0.2f);

            foreach (ZoneArea area in areas)
            {
                Gizmos.color = Color.HSVToRGB(area.zone * 0.13f % 1f, 0.6f, 1f);
                Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
            }
        }
    }
}
