using UnityEngine;

namespace ZombieFPS
{
    /// <summary>Damage multiplier for a collider of a damageable (e.g. head).</summary>
    public class Hitbox : MonoBehaviour
    {
        public float damageMultiplier = 2f;
        public bool isHead = true;
    }
}
