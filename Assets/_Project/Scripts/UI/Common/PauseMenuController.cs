using Market.Core;
using Market.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        private RectTransform _root;
        private RectTransform _mainPanel;
        private RectTransform _settingsPanel;
        private TMP_Text _statusLabel;
        private SceneLoader _sceneLoader;
        private bool _isPaused;

        /// <summary>True while the pause menu is open.</summary>
        public bool IsPaused => _isPaused;

        private void Awake()
        {
            ResolveUIModeService();
            ResolveSceneLoader();
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
            _root = CreateRect("PauseMenuRoot", transform);
            StretchToParent(_root);

            Image backdrop = AddImage(_root.gameObject, new Color(0f, 0f, 0f, 0.56f));
            backdrop.raycastTarget = true;

            _mainPanel = CreatePanel("PausePanel");
            TMP_Text title = CreateText("Title", _mainPanel, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "Пауза";
            AddLayout(title.gameObject, 52f);

            CreateButton("ResumeButton", _mainPanel, "Продолжить", Resume);
            CreateButton("SaveButton", _mainPanel, "Сохранить", OnSave);
            CreateButton("SettingsButton", _mainPanel, "Настройки", OnSettings);
            CreateButton("MainMenuButton", _mainPanel, "В главное меню", OnMainMenu);

            _statusLabel = CreateText("Status", _mainPanel, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            _statusLabel.color = new Color(0.72f, 0.78f, 0.82f);
            AddLayout(_statusLabel.gameObject, 28f);

            _settingsPanel = CreatePanel("PauseSettingsPanel");
            TMP_Text settingsTitle = CreateText("Title", _settingsPanel, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            settingsTitle.text = "Настройки";
            AddLayout(settingsTitle.gameObject, 48f);

            TMP_Text placeholder = CreateText("Placeholder", _settingsPanel, 17f, FontStyles.Normal, TextAlignmentOptions.Center);
            placeholder.text = "Настройки появятся в следующем обновлении.";
            placeholder.color = new Color(0.72f, 0.78f, 0.82f);
            AddLayout(placeholder.gameObject, 80f);

            CreateButton("BackButton", _settingsPanel, "Назад", OnBackToMain);
            _settingsPanel.gameObject.SetActive(false);
        }

        private RectTransform CreatePanel(string name)
        {
            RectTransform panel = CreateRect(name, _root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, 0f);

            AddImage(panel.gameObject, new Color(0.08f, 0.09f, 0.10f, 0.96f));

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
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect.gameObject, new Color(0.22f, 0.45f, 0.55f, 1f));

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", rect, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            StretchToParent(text.rectTransform);

            AddLayout(rect.gameObject, ButtonHeight);
            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.layer = gameObject.layer;
            obj.transform.SetParent(parent, false);

            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.layer = gameObject.layer;
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        private Image AddImage(GameObject obj, Color color)
        {
            Image image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void AddLayout(GameObject obj, float preferredHeight)
        {
            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
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

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
