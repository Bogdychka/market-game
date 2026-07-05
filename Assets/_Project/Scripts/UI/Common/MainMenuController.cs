using Market.Core;
using Market.DebugTools;
using Market.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Main menu controller. Lives in the MainMenu scene.
    /// Buttons are wired via the Inspector.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private UIModeService uiModeService;

        private SceneLoader _sceneLoader;
        private SaveSystem  _saveSystem;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FileLogger.Initialize();
#endif
            ResolveUIModeService();
            ValidateReferences();
            ResolveServices();
        }

        private void OnEnable()
        {
            WireButtons();
        }

        private void OnDisable()
        {
            UnwireButtons();
        }

        private void Start()
        {
            uiModeService?.SetPersistentMenuMode(true);
            RefreshContinueAvailability();
            ShowMainPanel();
        }

        // -- Button handlers --------------------------------------------
        private void OnNewGame()
        {
            Debug.Log("[MainMenu] New Game");

            if (ServiceLocator.TryGet<TimeSystem>(out var timeSystem))
                timeSystem.Reset();

            LoadMarketScene();
        }

        private void OnContinue()
        {
            Debug.Log("[MainMenu] Continue");

            _saveSystem.ShouldLoadOnStart = true;
            LoadMarketScene();
        }

        private void OnSettings()  => SetPanelsActive(mainPanel: false, settingsPanel: true);
        public  void CloseSettings() => SetPanelsActive(mainPanel: true,  settingsPanel: false);

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // -- Setup ------------------------------------------------------
        private void WireButtons()
        {
            UnwireButtons();
            if (newGameButton  != null) newGameButton.onClick.AddListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (quitButton     != null) quitButton.onClick.AddListener(OnQuit);
        }

        private void UnwireButtons()
        {
            if (newGameButton  != null) newGameButton.onClick.RemoveListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (quitButton     != null) quitButton.onClick.RemoveListener(OnQuit);
        }

        private void RefreshContinueAvailability()
        {
            if (continueButton == null) return;
            if (_saveSystem == null)
            {
                continueButton.interactable = false;
                return;
            }
            continueButton.interactable = _saveSystem.HasSave();
        }

        private void ShowMainPanel() => SetPanelsActive(mainPanel: true, settingsPanel: false);

        private void SetPanelsActive(bool mainPanel, bool settingsPanel)
        {
            if (this.mainPanel     != null) this.mainPanel.SetActive(mainPanel);
            if (this.settingsPanel != null) this.settingsPanel.SetActive(settingsPanel);
        }

        private void LoadMarketScene()
        {
            if (_sceneLoader == null)
                ResolveServices();

            if (_sceneLoader == null)
            {
                Debug.LogError("[MainMenu] SceneLoader unavailable -- cannot load Market.", this);
                return;
            }

            _sceneLoader.Load(SceneNames.Market);
        }

        private void ResolveServices()
        {
            if (!ServiceLocator.TryGet<SceneLoader>(out _sceneLoader))
            {
                _sceneLoader = new SceneLoader(this);
                Debug.LogWarning("[MainMenu] SceneLoader not found. " +
                                 "Created a local SceneLoader for direct MainMenu startup.", this);
            }

            if (!ServiceLocator.TryGet<SaveSystem>(out _saveSystem))
            {
                _saveSystem = new SaveSystem();
                ServiceLocator.Register(_saveSystem);
                Debug.LogWarning("[MainMenu] SaveSystem not found. " +
                                 "Created a local SaveSystem for direct MainMenu startup.", this);
            }
        }

        private void ResolveUIModeService()
        {
            if (uiModeService != null) return;
            uiModeService = GetComponent<UIModeService>();
        }

        private void ValidateReferences()
        {
            if (newGameButton  == null) Debug.LogError("[MainMenu] newGameButton not assigned",  this);
            if (continueButton == null) Debug.LogError("[MainMenu] continueButton not assigned", this);
            if (settingsButton == null) Debug.LogError("[MainMenu] settingsButton not assigned", this);
            if (quitButton     == null) Debug.LogError("[MainMenu] quitButton not assigned",     this);
            if (mainPanel      == null) Debug.LogError("[MainMenu] mainPanel not assigned",      this);
            if (settingsPanel  == null) Debug.LogError("[MainMenu] settingsPanel not assigned",  this);
            if (uiModeService  == null) Debug.LogError("[MainMenu] uiModeService not assigned",  this);
        }
    }
}
