using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieFPS
{
    /// <summary>Game flow: run state, pause, victory / defeat, restart.</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerController player;
        public Level level;
        public Spawner spawner;
        public WorldEvents worldEvents;

        public GameState State { get; private set; } = GameState.Playing;
        /// <summary>Seconds of play since the run started (pauses excluded).</summary>
        public float GameTime { get; private set; }
        public int CurrentZone { get; private set; }

        internal GameStats Stats { get; private set; }
        internal GameEvents Events { get; private set; }
        internal float EnemyDamageMultiplier { get; set; } = 1f;
        internal float EnemySpeedMultiplier { get; set; } = 1f;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Stats = new GameStats(() => GameTime);
            Events = new GameEvents();
            DirectorDebug.Reset();

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (level == null) level = FindAnyObjectByType<Level>();
            if (spawner == null) spawner = FindAnyObjectByType<Spawner>();
            if (worldEvents == null) worldEvents = FindAnyObjectByType<WorldEvents>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            CurrentZone = Mathf.Max(0, level.GetZoneIndex(player.transform.position));
            SetCursorLocked(true);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool restart = keyboard != null && keyboard.rKey.wasPressedThisFrame;

            switch (State)
            {
                case GameState.Playing:
                    GameTime += Time.deltaTime;
                    UpdateZone();
                    if (escape) SetPaused(true);
                    break;

                case GameState.Paused:
                    if (escape) SetPaused(false);
                    else if (restart) Restart();
                    break;

                default:
                    if (restart) Restart();
                    break;
            }
        }

        void UpdateZone()
        {
            int zone = level.GetZoneIndex(player.transform.position);
            if (zone < 0 || zone == CurrentZone) return;

            CurrentZone = zone;
            Events.RaiseZoneEntered(zone);
            if (zone == level.LastZone) EndRun(true);
        }

        internal void OnPlayerDied()
        {
            EndRun(false);
        }

        void EndRun(bool victory)
        {
            if (State == GameState.Victory || State == GameState.Defeat) return;
            State = victory ? GameState.Victory : GameState.Defeat;
            SetCursorLocked(false);
            Events.RaiseGameOver(victory);
        }

        void SetPaused(bool paused)
        {
            State = paused ? GameState.Paused : GameState.Playing;
            Time.timeScale = paused ? 0f : 1f;
            SetCursorLocked(!paused);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
