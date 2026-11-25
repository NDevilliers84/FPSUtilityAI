using UnityEngine;

namespace ZombieFPS
{
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : MonoBehaviour
    {
        public float fuseTime = 2.2f;
        public float radius = 6f;
        public float damage = 180f;
        [Tooltip("Fraction of the damage applied to the player when caught in the blast.")]
        public float selfDamageScale = 0.35f;

        float explodeTime;

        void Start()
        {
            explodeTime = Time.time + fuseTime;
        }

        void Update()
        {
            if (Time.time < explodeTime) return;
            Damage.Explode(transform.position, radius, damage, true, selfDamageScale);
            Destroy(gameObject);
        }
    }
}
