using System.Collections.Generic;
using UnityEngine;

namespace ZombieFPS
{
    /// <summary>Item collected by walking over it.</summary>
    public class Pickup : MonoBehaviour
    {
        /// <summary>All pickups lying in the level.</summary>
        public static readonly List<Pickup> All = new List<Pickup>();
        static int nextId;

        public PickupType type;
        [Tooltip("Health: hit points. Ammo: fraction of max reserve (0..1). Grenade: count. Weapons: unused.")]
        public float amount = 1f;
        public float collectRadius = 1.3f;
        public Transform visual;

        internal PickupInfo Info { get; private set; }
        internal SpawnPoint origin;

        float spawnTime;
        float bobOffset;

        void Awake()
        {
            spawnTime = Time.time;
            bobOffset = Random.value * 10f;
            Info = new PickupInfo
            {
                Id = nextId++,
                Type = type,
                Position = transform.position,
                IsAvailable = true,
                pickup = this,
            };
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (visual != null)
            {
                visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                visual.localPosition = new Vector3(0f, 0.45f + Mathf.Sin((Time.time + bobOffset) * 2.5f) * 0.08f, 0f);
            }

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            Vector3 delta = gm.player.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 2f) return;
            delta.y = 0f;
            if (delta.sqrMagnitude > collectRadius * collectRadius) return;

            if (TryApply(gm.player)) Collect(gm);
        }

        bool TryApply(PlayerController player)
        {
            switch (type)
            {
                case PickupType.Health:
                    return player.Health.Heal(amount) > 0f;
                case PickupType.Ammo:
                    return player.Weapons.AddAmmo(amount);
                case PickupType.Grenade:
                    return player.Weapons.AddGrenades(Mathf.Max(1, Mathf.RoundToInt(amount)));
                case PickupType.Shotgun:
                    return player.Weapons.GiveWeapon(WeaponType.Shotgun);
                case PickupType.Rifle:
                    return player.Weapons.GiveWeapon(WeaponType.Rifle);
                default:
                    return false;
            }
        }

        void Collect(GameManager gm)
        {
            gm.Stats.RecordPickup();
            Hud.ShowPickup(Label());
            Remove();
            gm.Events.RaisePickupCollected(Info);
        }

        string Label()
        {
            switch (type)
            {
                case PickupType.Health: return $"+{amount:0} Health";
                case PickupType.Ammo: return "+Ammo";
                case PickupType.Grenade: return "+Grenade";
                default: return type.ToString();
            }
        }

        internal void Remove()
        {
            Info.IsAvailable = false;
            if (origin != null && origin.currentPickup == this) origin.currentPickup = null;
            Destroy(gameObject);
            enabled = false;
            All.Remove(this);
        }

        internal void RefreshInfo(Vector3 playerPos, Level level)
        {
            Vector3 position = transform.position;
            Info.Position = position;
            Info.DistanceToPlayer = Vector3.Distance(position, playerPos);
            Info.Progress = level.GetProgress(position);
            Info.Age = Time.time - spawnTime;
        }
    }
}
