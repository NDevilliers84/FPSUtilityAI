using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieFPS
{
    /// <summary>First person movement and mouse look.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cameraPivot;
        public Camera playerCamera;
        public Light flashlight;

        public float walkSpeed = 5f;
        public float sprintSpeed = 8f;
        public float jumpHeight = 1.1f;
        public float gravity = -20f;
        public float mouseSensitivity = 0.08f;

        public PlayerHealth Health { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public bool IsSprinting { get; private set; }
        public Vector3 EyePosition => playerCamera.transform.position;

        internal float speedMultiplier = 1f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;
        float recoilOffset;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Health = GetComponent<PlayerHealth>();
            Weapons = GetComponent<PlayerWeapons>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (cameraPivot == null) cameraPivot = playerCamera.transform;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            Look(mouse);
            Move(keyboard);

            if (keyboard.fKey.wasPressedThisFrame && flashlight != null)
                flashlight.enabled = !flashlight.enabled;
        }

        void Look(Mouse mouse)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            recoilOffset = Mathf.Lerp(recoilOffset, 0f, Time.deltaTime * 10f);
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoilOffset, 0f, 0f);
        }

        void Move(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.zKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.qKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            IsSprinting = keyboard.leftShiftKey.isPressed && input.y > 0.1f;
            float speed = (IsSprinting ? sprintSpeed : walkSpeed) * speedMultiplier;
            Vector3 horizontal = (transform.forward * input.y + transform.right * input.x) * speed;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        internal void AddRecoil(float degrees)
        {
            recoilOffset += degrees;
        }

        /// <summary>True if the world point is in the camera frustum and not hidden by level geometry.</summary>
        internal bool CanSee(Vector3 worldPoint)
        {
            Vector3 viewport = playerCamera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f)
                return false;
            return !Physics.Linecast(EyePosition, worldPoint, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        internal void RefreshInfo(PlayerInfo info, Level level)
        {
            Vector3 position = transform.position;
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;

            info.Position = position;
            info.Forward = transform.forward;
            info.Speed = velocity.magnitude;
            info.IsSprinting = IsSprinting;

            info.Health = Health.Current;
            info.MaxHealth = Health.maxHealth;
            info.IsAlive = Health.IsAlive;

            WeaponSlot weapon = Weapons.Current;
            info.CurrentWeapon = weapon.type;
            info.AmmoInMagazine = weapon.magazine;
            info.ReserveAmmo = weapon.infiniteReserve ? -1 : weapon.reserve;
            info.AmmoRatio = Weapons.AmmoRatio;
            info.Grenades = Weapons.Grenades;
            info.MaxGrenades = Weapons.maxGrenades;
            foreach (WeaponSlot slot in Weapons.weapons)
                info.ownedWeapons[(int)slot.type] = slot.owned;

            float along = level.GetDistanceAlongPath(position);
            info.DistanceAlongPath = along;
            info.Progress = level.PathLength > 0f ? along / level.PathLength : 0f;
            info.CurrentZone = level.GetZoneIndex(position);
            info.IsInSafeRoom = info.CurrentZone == 0 || info.CurrentZone == level.LastZone;
        }
    }
}
