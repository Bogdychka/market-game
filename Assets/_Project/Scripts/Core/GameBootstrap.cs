using Market.Core.Events;
using Market.DebugTools;
using Market.Economy;
using Market.Persistence;
using Market.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Market.Core
{
    /// <summary>
    /// Game entry point. Lives in the Bootstrap scene.
    /// Brings up core services (EventBus, SceneLoader, SaveSystem, TimeSystem) and loads the first scene.
    /// Marked DontDestroyOnLoad — persists for the entire session.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Boot")]
        [SerializeField] private string firstScene = SceneNames.MainMenu;
        [SerializeField] private bool   skipMenuInEditor = false;

        [Header("Settings")]
        [Tooltip("Assign the SettingsSO asset so settings are available before any scene loads.")]
        [SerializeField] private SettingsSO settingsSO;

        [Header("Time")]
        [Tooltip("How many game minutes pass per real second (2 = 12 real minutes per game day).")]
        [SerializeField] private float minutesPerRealSecond = 2f;

        [Header("Controls")]
        [Tooltip("How many seconds after a scene load to ignore Escape, so stale input doesn't immediately exit back to the menu.")]
        [SerializeField] private float escapeSceneLoadCooldown = 1.5f;

        private static bool _initialized;
        private bool _isPrimaryInstance;
        private SceneLoader _sceneLoader;
        private TimeSystem _timeSystem;  // cached to avoid ServiceLocator.TryGet every frame
        private DayPhaseSystem _dayPhaseSystem;
        private MarketOpenSystem _marketOpenSystem;
        private float _ignoreEscapeUntil;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (_initialized)
            {
                Destroy(gameObject);
                return;
            }
            _initialized = true;
            _isPrimaryInstance = true;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FileLogger.Initialize();
#endif
            InitializeServices();
            LoadFirstScene();
        }

        private void Update()
        {
            TickTime();
            HandleEscape();
        }

        private void OnDestroy()
        {
            if (!_isPrimaryInstance) return;

            if (_sceneLoader != null)
                _sceneLoader.OnSceneLoadCompleted -= OnSceneLoadCompleted;

            _dayPhaseSystem?.Dispose();
            _marketOpenSystem?.Dispose();
            ServiceLocator.Clear();
            _initialized = false;
            _isPrimaryInstance = false;
        }

        // ── Setup ──────────────────────────────────────────────────────
        private void InitializeServices()
        {
            ServiceLocator.Clear();

            ServiceLocator.Register(new EventBus());

            _sceneLoader = new SceneLoader(this);
            _sceneLoader.OnSceneLoadCompleted += OnSceneLoadCompleted;
            ServiceLocator.Register(_sceneLoader);

            ServiceLocator.Register(new SaveSystem());
            ServiceLocator.Register(new PriceCalculator());

            _timeSystem = new TimeSystem(minutesPerRealSecond);
            ServiceLocator.Register(_timeSystem);

            _dayPhaseSystem = new DayPhaseSystem(_timeSystem, ServiceLocator.Get<EventBus>());
            ServiceLocator.Register(_dayPhaseSystem);

            _marketOpenSystem = new MarketOpenSystem(_dayPhaseSystem, ServiceLocator.Get<EventBus>());
            ServiceLocator.Register(_marketOpenSystem);

            if (settingsSO != null)
                ServiceLocator.Register(new SettingsService(settingsSO));
            else
                Debug.LogWarning("[GameBootstrap] settingsSO not assigned; SettingsService not registered.");

            Debug.Log("[GameBootstrap] Services initialized.");
        }

        private void LoadFirstScene()
        {
            string target = firstScene;
#if UNITY_EDITOR
            if (skipMenuInEditor) target = SceneNames.Market;
#endif
            ServiceLocator.Get<SceneLoader>().Load(target);
        }

        // ── Per-frame ──────────────────────────────────────────────────
        private void TickTime()
        {
            _timeSystem?.Tick(Time.deltaTime);
        }

        private void HandleEscape()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (Time.unscaledTime < _ignoreEscapeUntil)
            {
                Debug.Log("[GameBootstrap] Escape ignored immediately after scene load.");
                return;
            }

            if (_sceneLoader != null && _sceneLoader.IsLoading) return;

            string current = SceneManager.GetActiveScene().name;

            if (current != SceneNames.MainMenu && TryConsumeUiEscape())
                return;

            if (current == SceneNames.MainMenu)
            {
                QuitApplication();
                return;
            }

            if (current == SceneNames.Market && TryOpenPauseMenu())
                return;

            ReturnToMainMenu();
        }

        private static bool TryConsumeUiEscape()
        {
            if (!ServiceLocator.TryGet<UIModeService>(out UIModeService uiModeService))
                return false;

            if (uiModeService.WasCloseRequestConsumedThisFrame)
                return true;

            return uiModeService.IsMenuMode && uiModeService.TryConsumeCloseRequest();
        }

        private void ReturnToMainMenu()
        {
            Debug.Log("[GameBootstrap] Escape: returning to MainMenu.");
            ServiceLocator.Get<SceneLoader>().Load(SceneNames.MainMenu);
        }

        private static bool TryOpenPauseMenu()
        {
            if (!ServiceLocator.TryGet<PauseMenuController>(out PauseMenuController pauseMenu))
            {
                Debug.LogWarning("[GameBootstrap] PauseMenuController not registered; Escape falls back to MainMenu.");
                return false;
            }

            pauseMenu.Open();
            return true;
        }

        private void OnSceneLoadCompleted(string sceneName)
        {
            _ignoreEscapeUntil = Time.unscaledTime + escapeSceneLoadCooldown;
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
