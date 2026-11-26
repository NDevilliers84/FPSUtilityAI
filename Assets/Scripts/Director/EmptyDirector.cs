using System.Collections.Generic;
using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Director that does nothing. Replace it with your own IAIDirector implementation.
    /// </summary>
    public class EmptyDirector : MonoBehaviour, IAIDirector
    {
        [Header("Intensité (stress du joueur)")] [SerializeField]
        private float _intensityDecay = 0.04f;

        [SerializeField] private float _damageToIntensity = 0.012f;
        [SerializeField] private float _closeEnemyIntensity = 0.02f;
        [SerializeField] private float _peakThreshold = 0.85f;
        [SerializeField] private float _relaxDuration = 12f;

        [Header("Décision")] [SerializeField] private float _decisionInterval = 0.5f;
        [SerializeField] private float _momentum = 0.1f;

        private float _intensity;
        private float _relaxTimer;
        private float _decisionTimer;
        private UtilityAction _lastAction;
        private readonly List<UtilityAction> _utilityActions = new List<UtilityAction>();

        public float Intensity => _intensity;
        public bool IsRelaxing => _relaxTimer > 0f;

        public void OnGameStart(DirectorContext context, DirectorActions actions)
        {
            context.Events.PlayerDamaged += amount => _intensity += amount * _damageToIntensity;

            _utilityActions.Add(new IdleAction());
            _utilityActions.Add(new SpawnAmbientAction());
            _utilityActions.Add(new HordeAction());
            _utilityActions.Add(new SpawnHealthAction());
            _utilityActions.Add(new SpawnAmmoAction());
            _utilityActions.Add(new SupplyDropAction());
            _utilityActions.Add(new AdrenalineAction());
            _utilityActions.Add(new FogAction());
        }

        public void Tick(DirectorContext context, DirectorActions actions, float deltaTime)
        {
            UpdateIntensity(context, deltaTime);

            foreach (var a in _utilityActions) a.TickCooldown(deltaTime);

            _decisionTimer -= deltaTime;
            if (_decisionTimer > 0f) return;
            _decisionTimer = _decisionInterval;

            UtilityAction best = null;
            float bestScore = -1f;
            foreach (var a in _utilityActions)
            {
                float score = a.Evaluate(context, this);
                if (a == _lastAction && score > 0f) score += _momentum;
                DirectorDebug.Watch(a.Name, score.ToString("0.00"));

                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            if (best != null && best.Execute(context, actions, this))
            {
                best.StartCooldown();
                _lastAction = best;
                if (!(best is IdleAction)) DirectorDebug.Log($"{best.Name} ({bestScore:0.00})");
            }
        }

        private void UpdateIntensity(DirectorContext ctx, float dt)
        {
            _intensity += ctx.CountEnemiesWithin(8f) * _closeEnemyIntensity * dt;
            _intensity -= _intensityDecay * dt;
            _intensity = Mathf.Clamp01(_intensity);

            if (!IsRelaxing && _intensity >= _peakThreshold)
            {
                _relaxTimer = _relaxDuration;
                DirectorDebug.Log("Pic atteint -> phase de répit");
            }

            if (_relaxTimer > 0f) _relaxTimer -= dt;

            DirectorDebug.Plot("Intensité", _intensity, 0f, 1f);
            DirectorDebug.Watch("Phase", IsRelaxing ? "Répit" : "Build-up");
        }

        public static bool TryFindEnemyPoint(DirectorContext ctx, float minDist, float maxDist,
            out SpawnPointInfo point)
        {
            point = default;
            bool found = false;
            float bestScore = -1f;
            float ideal = (minDist + maxDist) * 0.5f;

            for (int i = 0; i < ctx.EnemySpawnPoints.Count; i++)
            {
                var p = ctx.EnemySpawnPoints[i];
                if (p.IsOccupied || p.IsVisibleToPlayer) continue;
                if (p.DistanceToPlayer < minDist || p.DistanceToPlayer > maxDist) continue;

                float score = 1f - Mathf.Abs(p.DistanceToPlayer - ideal) / ideal;
                if (p.IsAheadOfPlayer) score += 0.5f;
                score += Random.value * 0.2f;

                if (score > bestScore)
                {
                    bestScore = score;
                    point = p;
                    found = true;
                }
            }

            return found;
        }

        public static bool TryFindPickupPoint(DirectorContext ctx, out SpawnPointInfo point)
        {
            point = default;
            bool found = false;
            float bestDist = float.MaxValue;

            for (int i = 0; i < ctx.PickupSpawnPoints.Count; i++)
            {
                var p = ctx.PickupSpawnPoints[i];
                if (p.IsOccupied || !p.IsAheadOfPlayer) continue;
                if (p.DistanceToPlayer < 8f || p.DistanceToPlayer > 40f) continue;

                if (p.DistanceToPlayer < bestDist)
                {
                    bestDist = p.DistanceToPlayer;
                    point = p;
                    found = true;
                }
            }

            return found;
        }
    }

    public static class Curves
    {
        public static float Linear(float x) => Mathf.Clamp01(x);
        public static float Inverse(float x) => 1f - Mathf.Clamp01(x);

        public static float Quadratic(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x;
        }

        public static float InverseQuadratic(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return x * x;
        }

        public static float Logistic(float x, float mid = 0.5f, float steep = 10f)
            => 1f / (1f + Mathf.Exp(-steep * (Mathf.Clamp01(x) - mid)));

        public static float Combine(params float[] considerations)
        {
            float score = 1f;
            foreach (float c in considerations)
            {
                score *= c;
                if (score <= 0f) return 0f;
            }

            int n = considerations.Length;
            float mod = 1f - 1f / n;
            float makeUp = (1f - score) * mod;
            return score + makeUp * score;
        }
    }

    public abstract class UtilityAction
    {
        public abstract string Name { get; }
        protected virtual float Cooldown => 0f;

        private float _cooldownLeft;
        public void TickCooldown(float dt) => _cooldownLeft -= dt;
        public void StartCooldown() => _cooldownLeft = Cooldown;

        public float Evaluate(DirectorContext ctx, EmptyDirector dir)
            => _cooldownLeft > 0f ? 0f : Score(ctx, dir);

        protected abstract float Score(DirectorContext ctx, EmptyDirector dir);
        public abstract bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir);
    }

    public class IdleAction : UtilityAction
    {
        public override string Name => "Idle";
        protected override float Score(DirectorContext ctx, EmptyDirector dir) => 0.15f;
        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir) => true;
    }

    public class SpawnAmbientAction : UtilityAction
    {
        public override string Name => "Spawn ambiant";
        protected override float Cooldown => 2f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            if (dir.IsRelaxing || ctx.Player.IsInSafeRoom) return 0f;

            float fill = (float)ctx.CountEnemies() / ctx.MaxAliveEnemies;
            return Curves.Combine(
                Curves.Inverse(dir.Intensity),
                Curves.InverseQuadratic(fill),
                Curves.Linear(ctx.Stats.TimeSinceLastCombat / 8f)
            );
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
        {
            if (!EmptyDirector.TryFindEnemyPoint(ctx, 18f, 45f, out var point)) return false;

            float progress = ctx.Player.Progress;
            float r = Random.value;

            if (progress > 0.5f && r < 0.06f && ctx.CountEnemies(EnemyType.Tank) == 0)
                return act.SpawnEnemy(EnemyType.Tank, point, false);
            if (r < 0.15f && ctx.CountEnemies(EnemyType.Bloater) < 2)
                return act.SpawnEnemy(EnemyType.Bloater, point, false);
            if (r < 0.15f + 0.3f * progress)
           
                return act.SpawnEnemies(EnemyType.Runner, Random.Range(1, 3), point, false) > 0;

            return act.SpawnEnemies(EnemyType.Common, Random.Range(2, 5), point, false) > 0;
        }
    }

    public class HordeAction : UtilityAction
    {
        public override string Name => "Horde";
        protected override float Cooldown => 60f;
        private float _lastHordeTime;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            if (dir.IsRelaxing || ctx.Player.IsInSafeRoom) return 0f;

            float sinceLast = (ctx.GameTime - _lastHordeTime) / 120f;
            return Curves.Combine(
                Curves.Logistic(sinceLast, 0.6f, 8f),
                Curves.Linear(ctx.Player.HealthRatio),
                Curves.Inverse(dir.Intensity)
            );
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
        {
            int size = Mathf.RoundToInt(6 + ctx.Player.Progress * 10);
            if (!act.TriggerHorde(size)) return false;
            _lastHordeTime = ctx.GameTime;
            act.ShowMessage("Vous entendez des cris au loin...", 3f);
            return true;
        }
    }

    public class SpawnHealthAction : UtilityAction
    {
        public override string Name => "Spawn soin";
        protected override float Cooldown => 10f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            float score = Curves.Combine(
                Curves.InverseQuadratic(ctx.Player.HealthRatio),
                Curves.Inverse(ctx.CountPickups(PickupType.Health) / 2f)
            );
            return dir.IsRelaxing ? score * 1.2f : score;
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
            => EmptyDirector.TryFindPickupPoint(ctx, out var p) && act.SpawnPickup(PickupType.Health, p);
    }

    public class SpawnAmmoAction : UtilityAction
    {
        public override string Name => "Spawn munitions";
        protected override float Cooldown => 10f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            if (ctx.Player.ReserveAmmo == -1) return 0f;
            return Curves.Combine(
                Curves.InverseQuadratic(ctx.Player.AmmoRatio),
                Curves.Inverse(ctx.CountPickups(PickupType.Ammo) / 2f)
            );
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
            => EmptyDirector.TryFindPickupPoint(ctx, out var p) && act.SpawnPickup(PickupType.Ammo, p);
    }

    public class SupplyDropAction : UtilityAction
    {
        public override string Name => "Supply drop";
        protected override float Cooldown => 90f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            float ammo = ctx.Player.ReserveAmmo == -1 ? 0.5f : ctx.Player.AmmoRatio;
            return Curves.Combine(
                Curves.InverseQuadratic(ctx.Player.HealthRatio),
                Curves.Inverse(ammo),
                Curves.Linear(ctx.CountEnemiesWithin(15f) / 5f)
            );
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
        {
            if (!act.SupplyDrop()) return false;
            act.ShowMessage("Ravitaillement largué !", 2.5f);
            return true;
        }
    }

    public class AdrenalineAction : UtilityAction
    {
        public override string Name => "Adrénaline";
        protected override float Cooldown => 75f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
            => Curves.Combine(
                Curves.Quadratic(ctx.CountEnemiesWithin(10f) / 6f),
                Curves.Inverse(ctx.Player.HealthRatio)
            );

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
            => act.GiveAdrenaline(8f);
    }

    public class FogAction : UtilityAction
    {
        public override string Name => "Brouillard";
        protected override float Cooldown => 120f;

        protected override float Score(DirectorContext ctx, EmptyDirector dir)
        {
            if (dir.IsRelaxing || ctx.Player.IsInSafeRoom) return 0f;
            return Curves.Combine(
                Curves.Quadratic(ctx.Player.HealthRatio),
                Curves.Linear(ctx.Stats.AccuracyInLast(30f)),
                Curves.Inverse(dir.Intensity)
            ) * 0.8f;
        }

        public override bool Execute(DirectorContext ctx, DirectorActions act, EmptyDirector dir)
            => act.StartFog(20f);
    }
}
