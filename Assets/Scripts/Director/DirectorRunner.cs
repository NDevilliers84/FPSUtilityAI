using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Drives the AI Director: finds the IAIDirector component on this GameObject,
    /// refreshes the DirectorContext and calls Tick at a fixed interval while the game is running.
    /// </summary>
    public class DirectorRunner : MonoBehaviour
    {
        [Tooltip("Seconds between two Director ticks.")]
        [SerializeField, Min(0.02f)] float tickInterval = 0.25f;

        public float TickInterval => tickInterval;
        public IAIDirector Director { get; private set; }
        public DirectorContext Context { get; private set; }
        public DirectorActions Actions { get; private set; }

        float accumulator;
        bool started;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[Director] No GameManager in the scene.");
                enabled = false;
                return;
            }

            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is IAIDirector director && behaviour.enabled)
                {
                    Director = director;
                    break;
                }
            }

            if (Director == null)
            {
                Debug.LogWarning("[Director] No enabled IAIDirector component found on " + name + ".");
                enabled = false;
                return;
            }

            Context = new DirectorContext(gm);
            Actions = new DirectorActions(gm);
            DirectorDebug.DirectorName = Director.GetType().Name;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;

            if (!started)
            {
                started = true;
                Context.Refresh();
                Director.OnGameStart(Context, Actions);
                return;
            }

            accumulator += Time.deltaTime;
            if (accumulator < tickInterval) return;

            float deltaTime = accumulator;
            accumulator = 0f;
            Context.Refresh();
            Director.Tick(Context, Actions, deltaTime);
        }
    }
}
