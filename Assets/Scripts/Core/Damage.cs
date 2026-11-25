using System.Collections.Generic;
using UnityEngine;

namespace ZombieFPS
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public bool fromPlayer;
        public bool isHeadshot;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }

    internal static class Damage
    {
        /// <summary>Radial damage with linear falloff. Alerts zombies in a wide radius.</summary>
        public static void Explode(Vector3 center, float radius, float damage, bool fromPlayer, float playerDamageScale)
        {
            Fx.Explosion(center, radius);
            Zombie.AlertAround(center, radius * 5f);

            var damaged = new HashSet<IDamageable>();
            Collider[] hits = Physics.OverlapSphere(center, radius, Layers.DamageMask, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !damaged.Add(target)) continue;

                Vector3 targetCenter = hit.bounds.center;
                float distance = Vector3.Distance(center, targetCenter);
                float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(distance / radius));
                float amount = damage * falloff;
                if (target is PlayerHealth) amount *= playerDamageScale;

                target.TakeDamage(new DamageInfo
                {
                    amount = amount,
                    point = targetCenter,
                    direction = (targetCenter - center).normalized,
                    fromPlayer = fromPlayer,
                });
            }
        }
    }
}
