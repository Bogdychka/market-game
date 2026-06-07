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
    /// Точка входа игры. Висит в сцене Bootstrap.
    /// Поднимает базовые сервисы (EventBus, SceneLoader, SaveSystem, TimeSystem) и грузит первую сцену.
    /// Помечен DontDestroyOnLoad — живёт всю сессию.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Boot")]
        [SerializeField] private string firstScene = SceneNames.MainMenu;
        [SerializeField] private bool   skipMenuInEditor = false;

        [Header("Time")]
        [Tooltip("Сколько игровых минут проходит за одну реальную секунду (2 = 12 минут реал = 1 игровой день).")]
        [SerializeField] private float minutesPerRealSecond = 2f;

        [Header("Controls")]
        [Tooltip("Сколько секунд после загрузки сцены игнорировать Escape, чтобы старый ввод не выбрасывал игрока обратно в меню.")]
        [SerializeField] private float escapeSceneLoadCooldown = 1.5f;

        private static bool _initialized;
        private bool _isPrimaryInstance;
        private SceneLoader _sceneLoader;
        private TimeSystem _timeSystem;  // кешируем чтобы не делать ServiceLocator.TryGet каждый кадр
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

            Debug.Log("[GameBootstrap] Сервисы подняты");
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
                Debug.Log("[GameBootstrap] Escape проигнорирован сразу после загрузки сцены.");
                return;
            }

            if (_sceneLoader != null && _sceneLoader.IsLoading) return;

            string current = SceneManager.GetActiveScene().name;

            if (current != SceneNames.MainMenu && TryConsumeUiEscape())
                return;

            if (current == SceneNames.MainMenu)
                QuitApplication();
            else
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
            Debug.Log("[GameBootstrap] Escape: возврат в MainMenu");
            ServiceLocator.Get<SceneLoader>().Load(SceneNames.MainMenu);
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
