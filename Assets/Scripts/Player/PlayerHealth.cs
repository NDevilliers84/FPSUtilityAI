using UnityEngine;

namespace ZombieFPS
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;

        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            GameManager gm = GameManager.Instance;
            if (!IsAlive || gm == null || gm.State != GameState.Playing) return;

            float amount = Mathf.Min(info.amount, Current);
            if (amount <= 0f) return;

            Current -= amount;
            gm.Stats.RecordDamageTaken(amount);
            gm.Events.RaisePlayerDamaged(amount);
            Hud.OnPlayerDamaged(amount);

            if (Current <= 0f)
            {
                Current = 0f;
                gm.OnPlayerDied();
            }
        }

        /// <summary>Returns the amount actually healed.</summary>
        public float Heal(float amount)
        {
            float healed = Mathf.Min(amount, maxHealth - Current);
            if (healed <= 0f || !IsAlive) return 0f;

            Current += healed;
            GameManager gm = GameManager.Instance;
            gm.Stats.RecordHealing(healed);
            gm.Events.RaisePlayerHealed(healed);
            return healed;
        }
    }
}
