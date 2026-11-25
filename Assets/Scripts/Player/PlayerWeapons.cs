using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace ZombieFPS
{
    [Serializable]
    public class WeaponSlot
    {
        public WeaponType type;
        public string displayName;
        public bool startOwned;

        [Header("Shooting")]
        public float damage = 20f;
        public int pellets = 1;
        [Tooltip("Cone half-angle in degrees.")]
        public float spread = 1f;
        public float fireInterval = 0.25f;
        public bool automatic;
        public float range = 150f;
        [Tooltip("Camera kick in degrees.")]
        public float recoil = 1f;

        [Header("Ammo")]
        public int magazineSize = 12;
        public int maxReserve;
        public bool infiniteReserve;
        public float reloadTime = 1.2f;

        [Header("View model")]
        public Color color = Color.gray;
        public Vector3 viewScale = new Vector3(0.08f, 0.12f, 0.35f);

        [NonSerialized] public bool owned;
        [NonSerialized] public int magazine;
        [NonSerialized] public int reserve;
    }

    /// <summary>Hitscan weapons, reloads, weapon switching and grenades.</summary>
    public class PlayerWeapons : MonoBehaviour
    {
        public WeaponSlot[] weapons = CreateDefaultWeapons();

        [Header("Grenades")]
        public Grenade grenadePrefab;
        public int startGrenades = 1;
        public int maxGrenades = 3;
        public float throwSpeed = 16f;

        [Header("View model")]
        public Transform viewModel;
        public Renderer viewModelRenderer;
        public Transform muzzle;
        public Light muzzleLight;

        [Tooltip("Zombies within this radius hear gunshots.")]
        public float gunshotHearingRadius = 28f;

        public int Grenades { get; private set; }
        public WeaponSlot Current => weapons[currentIndex];
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.InverseLerp(reloadStart, reloadEnd, Time.time) : 0f;

        internal float damageMultiplier = 1f;
        internal float reloadSpeedMultiplier = 1f;

        int currentIndex;
        float reloadStart;
        float reloadEnd;
        float nextFireTime;
        float muzzleOffTime;
        Vector3 viewRest;
        Camera cam;
        PlayerController controller;
        MaterialPropertyBlock block;

        /// <summary>Remaining ammo of owned limited weapons relative to their capacity (1 if none owned).</summary>
        public float AmmoRatio
        {
            get
            {
                float ammo = 0f, capacity = 0f;
                foreach (WeaponSlot w in weapons)
                {
                    if (!w.owned || w.infiniteReserve) continue;
                    ammo += w.magazine + w.reserve;
                    capacity += w.magazineSize + w.maxReserve;
                }
                return capacity > 0f ? ammo / capacity : 1f;
            }
        }

        static WeaponSlot[] CreateDefaultWeapons()
        {
            return new[]
            {
                new WeaponSlot
                {
                    type = WeaponType.Pistol, displayName = "Pistol", startOwned = true,
                    damage = 22f, pellets = 1, spread = 0.6f, fireInterval = 0.2f, automatic = false, recoil = 1.2f,
                    magazineSize = 12, maxReserve = 0, infiniteReserve = true, reloadTime = 1.1f,
                    color = new Color(0.22f, 0.22f, 0.25f), viewScale = new Vector3(0.045f, 0.08f, 0.2f),
                },
                new WeaponSlot
                {
                    type = WeaponType.Shotgun, displayName = "Shotgun",
                    damage = 14f, pellets = 9, spread = 6f, fireInterval = 0.85f, automatic = false, recoil = 4f, range = 60f,
                    magazineSize = 8, maxReserve = 40, reloadTime = 2.2f,
                    color = new Color(0.45f, 0.28f, 0.15f), viewScale = new Vector3(0.06f, 0.07f, 0.5f),
                },
                new WeaponSlot
                {
                    type = WeaponType.Rifle, displayName = "Rifle",
                    damage = 20f, pellets = 1, spread = 1.4f, fireInterval = 0.1f, automatic = true, recoil = 0.8f,
                    magazineSize = 30, maxReserve = 180, reloadTime = 1.8f,
                    color = new Color(0.18f, 0.24f, 0.18f), viewScale = new Vector3(0.055f, 0.085f, 0.45f),
                },
            };
        }

        void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            controller = GetComponent<PlayerController>();
            block = new MaterialPropertyBlock();
            if (viewModel != null) viewRest = viewModel.localPosition;

            foreach (WeaponSlot w in weapons)
            {
                w.owned = w.startOwned;
                w.magazine = w.owned ? w.magazineSize : 0;
                w.reserve = w.owned ? w.maxReserve : 0;
            }
            Grenades = startGrenades;
            currentIndex = Mathf.Max(0, Array.FindIndex(weapons, w => w.owned));
            ApplyViewModel();
        }

        void Update()
        {
            if (muzzleLight != null && Time.time > muzzleOffTime) muzzleLight.enabled = false;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.1f) Cycle(-1);
            else if (scroll < -0.1f) Cycle(1);

            if (IsReloading && Time.time >= reloadEnd) FinishReload();
            if (keyboard.rKey.wasPressedThisFrame) StartReload();

            WeaponSlot weapon = Current;
            bool trigger = weapon.automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (trigger && !IsReloading && Time.time >= nextFireTime)
            {
                if (weapon.magazine > 0) Fire(weapon);
                else StartReload();
            }

            if (keyboard.gKey.wasPressedThisFrame) ThrowGrenade();

            if (viewModel != null)
            {
                Vector3 target = IsReloading ? viewRest + new Vector3(0f, -0.12f, -0.05f) : viewRest;
                viewModel.localPosition = Vector3.Lerp(viewModel.localPosition, target, Time.deltaTime * 12f);
            }
        }

        void Fire(WeaponSlot weapon)
        {
            GameManager gm = GameManager.Instance;
            weapon.magazine--;
            nextFireTime = Time.time + weapon.fireInterval;

            Transform camTransform = cam.transform;
            Vector3 origin = camTransform.position;
            bool anyHit = false;
            bool anyHeadshot = false;

            for (int i = 0; i < weapon.pellets; i++)
            {
                Vector2 offset = Random.insideUnitCircle * weapon.spread;
                Vector3 direction = camTransform.rotation * Quaternion.Euler(offset.y, offset.x, 0f) * Vector3.forward;
                Vector3 end = origin + direction * weapon.range;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, weapon.range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && !(target is PlayerHealth))
                    {
                        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
                        float multiplier = hitbox != null ? hitbox.damageMultiplier : 1f;
                        bool headshot = hitbox != null && hitbox.isHead;
                        target.TakeDamage(new DamageInfo
                        {
                            amount = weapon.damage * multiplier * damageMultiplier,
                            point = hit.point,
                            direction = direction,
                            fromPlayer = true,
                            isHeadshot = headshot,
                        });
                        anyHit = true;
                        anyHeadshot |= headshot;
                        Fx.Impact(hit.point, hit.normal, true);
                    }
                    else
                    {
                        Fx.Impact(hit.point, hit.normal, false);
                    }
                }

                if (i < 3 && muzzle != null) Fx.Tracer(muzzle.position, end);
            }

            gm.Stats.RecordShot(anyHit);
            gm.Events.RaiseShotFired(weapon.type);
            if (anyHit) Hud.ShowHitMarker(anyHeadshot);
            Zombie.AlertAround(transform.position, gunshotHearingRadius);

            controller.AddRecoil(weapon.recoil);
            if (viewModel != null) viewModel.localPosition -= new Vector3(0f, 0f, 0.04f + weapon.recoil * 0.01f);
            if (muzzleLight != null)
            {
                muzzleLight.enabled = true;
                muzzleOffTime = Time.time + 0.04f;
            }
        }

        void StartReload()
        {
            WeaponSlot weapon = Current;
            if (IsReloading || weapon.magazine >= weapon.magazineSize) return;
            if (!weapon.infiniteReserve && weapon.reserve <= 0) return;

            IsReloading = true;
            reloadStart = Time.time;
            reloadEnd = Time.time + weapon.reloadTime / Mathf.Max(0.1f, reloadSpeedMultiplier);
        }

        void FinishReload()
        {
            IsReloading = false;
            WeaponSlot weapon = Current;
            int needed = weapon.magazineSize - weapon.magazine;
            int taken = weapon.infiniteReserve ? needed : Mathf.Min(needed, weapon.reserve);
            weapon.magazine += taken;
            if (!weapon.infiniteReserve) weapon.reserve -= taken;
        }

        void Select(int index)
        {
            if (index < 0 || index >= weapons.Length || index == currentIndex || !weapons[index].owned) return;
            IsReloading = false;
            currentIndex = index;
            nextFireTime = Time.time + 0.25f;
            ApplyViewModel();
            if (viewModel != null) viewModel.localPosition = viewRest + Vector3.down * 0.25f;
        }

        void Cycle(int direction)
        {
            for (int step = 1; step < weapons.Length; step++)
            {
                int index = (currentIndex + direction * step + weapons.Length) % weapons.Length;
                if (weapons[index].owned)
                {
                    Select(index);
                    return;
                }
            }
        }

        void ApplyViewModel()
        {
            if (viewModel == null) return;
            WeaponSlot weapon = Current;
            viewModel.localScale = weapon.viewScale;
            if (muzzle != null) muzzle.localPosition = new Vector3(0f, 0f, 0.55f);
            if (viewModelRenderer != null)
            {
                block.SetColor("_BaseColor", weapon.color);
                viewModelRenderer.SetPropertyBlock(block);
            }
        }

        void ThrowGrenade()
        {
            if (Grenades <= 0 || grenadePrefab == null) return;
            Grenades--;

            Transform camTransform = cam.transform;
            Grenade grenade = Instantiate(grenadePrefab, camTransform.position + camTransform.forward * 0.6f, Random.rotation);
            var body = grenade.GetComponent<Rigidbody>();
            body.linearVelocity = camTransform.forward * throwSpeed + Vector3.up * 2.5f;
            body.angularVelocity = Random.insideUnitSphere * 10f;

            var playerCollider = GetComponent<Collider>();
            var grenadeCollider = grenade.GetComponent<Collider>();
            if (playerCollider != null && grenadeCollider != null)
                Physics.IgnoreCollision(playerCollider, grenadeCollider);
        }

        // ---------------------------------------------------------------- Pickups

        int IndexOf(WeaponType type) => Array.FindIndex(weapons, w => w.type == type);

        internal bool GiveWeapon(WeaponType type)
        {
            int index = IndexOf(type);
            if (index < 0) return false;
            WeaponSlot weapon = weapons[index];

            if (!weapon.owned)
            {
                weapon.owned = true;
                weapon.magazine = weapon.magazineSize;
                weapon.reserve = weapon.maxReserve / 2;
                Select(index);
                return true;
            }

            if (weapon.infiniteReserve || weapon.reserve >= weapon.maxReserve) return false;
            weapon.reserve = Mathf.Min(weapon.maxReserve, weapon.reserve + weapon.magazineSize * 2);
            return true;
        }

        /// <summary>Adds a fraction of max reserve to every owned limited weapon.</summary>
        internal bool AddAmmo(float fraction)
        {
            bool added = false;
            foreach (WeaponSlot weapon in weapons)
            {
                if (!weapon.owned || weapon.infiniteReserve || weapon.reserve >= weapon.maxReserve) continue;
                weapon.reserve = Mathf.Min(weapon.maxReserve, weapon.reserve + Mathf.CeilToInt(weapon.maxReserve * fraction));
                added = true;
            }
            return added;
        }

        internal bool AddGrenades(int count)
        {
            if (Grenades >= maxGrenades) return false;
            Grenades = Mathf.Min(maxGrenades, Grenades + count);
            return true;
        }
    }
}
