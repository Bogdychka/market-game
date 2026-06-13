using Market.Core;
using Market.Persistence;
using Market.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Runtime pause menu for the Market scene.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PauseMenuController : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 12f;

        [Header("References")]
        [Tooltip("Market scene save coordinator used by the Save button.")]
        [SerializeField] private GameSaver gameSaver;
        [Tooltip("Manages cursor lock and player-input suppression; source of the CloseRequested event used to close the menu.")]
        [SerializeField] private UIModeService uiModeService;

        [Header("Settings")]
        [Tooltip("Default values asset for the settings panel.")]
        [SerializeField] private SettingsSO settingsSO;
        [Tooltip("Player controller; receives look settings changes at runtime.")]
        [SerializeField] private FirstPersonController playerController;
        [Tooltip("PlayerInput on the player GameObject; required for key rebinding.")]
        [SerializeField] private PlayerInput playerInput;

        private RectTransform _root;
        private RectTransform _mainPanel;
        private RectTransform _settingsPanel;
        private TMP_Text _statusLabel;
        private SceneLoader _sceneLoader;
        private SettingsService _settingsService;
        private SettingsPanelRenderer _settingsPanelRenderer;
        private bool _isPaused;

        /// <summary>True while the pause menu is open.</summary>
        public bool IsPaused => _isPaused;

        private void Awake()
        {
            ResolveUIModeService();
            ResolveSceneLoader();
            ResolveSettingsService();
            ValidateReferences();
            BuildUi();
            SetVisible(false);
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            if (uiModeService != null)
                uiModeService.CloseRequested += OnCloseRequested;
        }

        private void OnDisable()
        {
            if (uiModeService != null)
                uiModeService.CloseRequested -= OnCloseRequested;

            Resume();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<PauseMenuController>(out PauseMenuController current) && ReferenceEquals(current, this))
                ServiceLocator.Unregister<PauseMenuController>();
        }

        /// <summary>Opens the pause menu and stops scaled time.</summary>
        public void Open()
        {
            if (_isPaused) return;
            if (uiModeService != null && uiModeService.IsMenuMode) return;

            _isPaused = true;
            Time.timeScale = 0f;
            ShowMainPanel();
            SetVisible(true);
            uiModeService?.EnterMenuMode(this);
        }

        /// <summary>Closes the pause menu and restores scaled time.</summary>
        public void Resume()
        {
            if (!_isPaused) return;

            _isPaused = false;
            SetVisible(false);
            uiModeService?.ExitMenuMode(this);
            Time.timeScale = 1f;
        }

        private void OnCloseRequested()
        {
            if (!_isPaused) return;
            Resume();
        }

        private void OnSave()
        {
            if (gameSaver == null)
            {
                Debug.LogError("[PauseMenuController] gameSaver not assigned.", this);
                SetStatus("Сохранение недоступно");
                return;
            }

            gameSaver.Save();
            SetStatus("Сохранено");
        }

        private void OnSettings()
        {
            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(false);

            if (_settingsPanel != null)
                _settingsPanel.gameObject.SetActive(true);
        }

        private void OnBackToMain()
        {
            _settingsPanelRenderer?.CancelActiveRebind();
            ShowMainPanel();
        }

        private void OnMainMenu()
        {
            Resume();

            if (_sceneLoader == null)
                ResolveSceneLoader();

            if (_sceneLoader == null)
            {
                Debug.LogError("[PauseMenuController] SceneLoader unavailable; cannot load MainMenu.", this);
                return;
            }

            _sceneLoader.Load(SceneNames.MainMenu);
        }

        private void BuildUi()
        {
            _root = UiFactory.CreateRect("PauseMenuRoot", transform, gameObject.layer);
            UiFactory.StretchToParent(_root);

            Image backdrop = UiFactory.AddImage(_root.gameObject, new Color(0f, 0f, 0f, 0.56f));
            backdrop.raycastTarget = true;

            _mainPanel = CreatePanel("PausePanel");
            TMP_Text title = CreateText("Title", _mainPanel, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "Пауза";
            UiFactory.AddLayoutHeight(title.gameObject, 52f);

            CreateButton("ResumeButton", _mainPanel, "Продолжить", Resume);
            CreateButton("SaveButton", _mainPanel, "Сохранить", OnSave);
            CreateButton("SettingsButton", _mainPanel, "Настройки", OnSettings);
            CreateButton("MainMenuButton", _mainPanel, "В главное меню", OnMainMenu);

            _statusLabel = CreateText("Status", _mainPanel, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            _statusLabel.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(_statusLabel.gameObject, 28f);

            _settingsPanel = CreatePanel("PauseSettingsPanel");
            TMP_Text settingsTitle = CreateText("Title", _settingsPanel, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            settingsTitle.text = "Настройки";
            UiFactory.AddLayoutHeight(settingsTitle.gameObject, 48f);

            if (_settingsService != null && settingsSO != null)
                _settingsPanelRenderer = new SettingsPanelRenderer(
                    _settingsPanel, gameObject.layer, _settingsService, settingsSO, playerInput);

            CreateButton("BackButton", _settingsPanel, "Назад", OnBackToMain);
            _settingsPanel.gameObject.SetActive(false);
        }

        private RectTransform CreatePanel(string name)
        {
            RectTransform panel = UiFactory.CreateRect(name, _root, gameObject.layer);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, 0f);

            UiFactory.AddImage(panel.gameObject, UiFactory.PanelBackground);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        private Button CreateButton(string name, Transform parent, string label, UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(name, parent, gameObject.layer, label, onClick, 17f);
            UiFactory.AddLayoutHeight(button.gameObject, ButtonHeight);
            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            return UiFactory.CreateText(name, parent, gameObject.layer, fontSize, style, alignment);
        }

        private void ShowMainPanel()
        {
            SetStatus(string.Empty);

            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(true);

            if (_settingsPanel != null)
                _settingsPanel.gameObject.SetActive(false);
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void ResolveUIModeService()
        {
            if (uiModeService != null) return;
            uiModeService = GetComponent<UIModeService>();
        }

        private void ResolveSettingsService()
        {
            if (ServiceLocator.TryGet<SettingsService>(out _settingsService)) return;
            if (settingsSO == null)
            {
                Debug.LogWarning("[PauseMenuController] settingsSO not assigned; settings panel will be empty.", this);
                return;
            }
            _settingsService = new SettingsService(settingsSO);
            ServiceLocator.Register(_settingsService);
            Debug.LogWarning("[PauseMenuController] SettingsService not found. Created local instance for direct Market startup.", this);
        }

        private void ResolveSceneLoader()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out _sceneLoader)) return;

            _sceneLoader = new SceneLoader(this);
            Debug.LogWarning("[PauseMenuController] SceneLoader not found. " +
                             "Created a local SceneLoader for direct Market startup.", this);
        }

        private void ValidateReferences()
        {
            if (gameSaver == null) Debug.LogError("[PauseMenuController] gameSaver not assigned.", this);
            if (uiModeService == null) Debug.LogError("[PauseMenuController] uiModeService not assigned.", this);
        }
    }
}
