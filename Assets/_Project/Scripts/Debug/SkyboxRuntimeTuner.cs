using System;
using System.Collections.Generic;
using Market.Core;
using Market.Player;
using Market.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Market.DebugTools
{
    /// <summary>
    /// In-game panel that tunes the BOXOPHOBIC "Skybox/Cubemap Blend" sky while playing: the two
    /// cubemap slots, the day-to-night transition, exposure, tint, rotation, the sky height fog and
    /// the matching sun/ambient settings all get a labelled slider or button. Toggled with F8.
    /// Changes are written to the shared skybox material, so inside the Editor they survive leaving
    /// play mode. Temporary debug tooling (see AGENTS.md).
    /// </summary>
    [DisallowMultipleComponent]
    public class SkyboxRuntimeTuner : MonoBehaviour
    {
        private const float RowSpacing = 4f;
        private const float LabelHeight = 17f;
        private const float SliderHeight = 14f;
        private const float RowHeight = LabelHeight + SliderHeight;
        private const float ButtonHeight = 26f;
        private const float TitleHeight = 24f;
        private const float EnvironmentRefreshInterval = 0.25f;

        private const string TexProperty = "_Tex";
        private const string BlendTexProperty = "_Tex_Blend";
        private const string TransitionProperty = "_CubemapTransition";
        private const string ExposureProperty = "_Exposure";
        private const string TintProperty = "_TintColor";
        private const string RotationProperty = "_Rotation";
        private const string RotationSpeedProperty = "_RotationSpeed";
        private const string EnableRotationProperty = "_EnableRotation";
        private const string EnableFogProperty = "_EnableFog";
        private const string FogIntensityProperty = "_FogIntensity";
        private const string FogHeightProperty = "_FogHeight";
        private const string FogSmoothnessProperty = "_FogSmoothness";
        private const string FogFillProperty = "_FogFill";
        private const string RotationKeyword = "_ENABLEROTATION_ON";
        private const string FogKeyword = "_ENABLEFOG_ON";

        [Header("References")]
        [Tooltip("Sky material that is tuned and pushed to RenderSettings.skybox.")]
        [SerializeField] private Material _skyboxMaterial;

        [Tooltip("Directional light steered by the sun rows.")]
        [SerializeField] private Light _sun;

        [Tooltip("Cubemaps the two sky slots cycle through.")]
        [SerializeField] private Cubemap[] _skies = Array.Empty<Cubemap>();

        [Tooltip("Disabled while the panel is open when no UIModeService is present.")]
        [SerializeField] private FirstPersonController _playerController;

        [Header("Settings")]
        [Tooltip("Open the panel as soon as play mode starts.")]
        [SerializeField] private bool _startOpen = true;

        private readonly List<SliderRow> _sliders = new List<SliderRow>();
        private readonly List<CaptionRow> _captions = new List<CaptionRow>();

        private GameObject _root;
        private TMP_Text _headerLabel;
        private bool _isOpen;
        private bool _suppressCallbacks;
        private bool _environmentDirty;
        private float _lastEnvironmentUpdate;
        private float _sunYaw;
        private float _sunPitch;
        private int _uiLayer;

        private sealed class SliderRow
        {
            public string Title;
            public TMP_Text Label;
            public Slider Slider;
            public Func<float> Read;
            public Action<float> Write;
        }

        private sealed class CaptionRow
        {
            public TMP_Text Label;
            public Func<string> Caption;
        }

        /// <summary>Wires the tuner without manual Inspector setup.</summary>
        public void Configure(
            Material skyboxMaterial,
            Light sun,
            Cubemap[] skies,
            FirstPersonController playerController)
        {
            _skyboxMaterial = skyboxMaterial;
            _sun = sun;
            _skies = skies ?? Array.Empty<Cubemap>();
            _playerController = playerController;
        }

        private void Awake()
        {
            _uiLayer = LayerMask.NameToLayer("UI");
            if (_uiLayer < 0)
                _uiLayer = 0;

            if (_skyboxMaterial == null)
            {
                Debug.LogError($"{nameof(SkyboxRuntimeTuner)}: skybox material is not assigned.", this);
            }
            else
            {
                RenderSettings.skybox = _skyboxMaterial;
                _environmentDirty = true;
            }

            if (_sun != null)
            {
                Vector3 angles = _sun.transform.eulerAngles;
                _sunPitch = NormalizeAngle(angles.x);
                _sunYaw = angles.y;
            }
        }

        private void Start()
        {
            if (_startOpen)
                SetOpen(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                SetOpen(!_isOpen);
        }

        private void LateUpdate()
        {
            if (!_environmentDirty ||
                Time.unscaledTime - _lastEnvironmentUpdate < EnvironmentRefreshInterval)
            {
                return;
            }

            _environmentDirty = false;
            _lastEnvironmentUpdate = Time.unscaledTime;
            DynamicGI.UpdateEnvironment();
        }

        private void OnDisable()
        {
            if (_isOpen)
                SetOpen(false);
        }

        // ---- Open / close -------------------------------------------------------------------

        private void SetOpen(bool open)
        {
            if (open)
            {
                if (_skyboxMaterial == null)
                {
                    Debug.LogError($"{nameof(SkyboxRuntimeTuner)}: no sky material to tune.", this);
                    return;
                }

                if (_root == null)
                    BuildUi();
            }

            // Closing during scene teardown must stay silent: the panel may never have been built.
            if (_root == null)
            {
                _isOpen = false;
                return;
            }

            _isOpen = open;
            _root.SetActive(open);
            ApplyInputMode(open);
            if (open)
                RefreshRows();
        }

        private void ApplyInputMode(bool menuMode)
        {
            if (ServiceLocator.TryGet(out UIModeService uiMode))
            {
                if (menuMode)
                    uiMode.EnterMenuMode(this);
                else
                    uiMode.ExitMenuMode(this);
                return;
            }

            if (_playerController != null)
                _playerController.enabled = !menuMode;
            Cursor.lockState = menuMode ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menuMode;
        }

        // ---- Construction -------------------------------------------------------------------

        private void BuildUi()
        {
            EnsureEventSystem();
            _root = CreateCanvas();

            RectTransform left = CreateColumn("Left Column", true);
            RectTransform right = CreateColumn("Right Column", false);

            _headerLabel = CreateHeader(left);
            BuildSkyGroup(left);
            BuildRotationGroup(left);
            BuildFogGroup(right);
            BuildSunGroup(right);
            BuildHint(right);

            _root.SetActive(false);
        }

        private void BuildSkyGroup(RectTransform column)
        {
            CreateGroupTitle("Sky", column);
            AddCaptionButton(
                column,
                "Sky A",
                () => $"Sky A: {SkyName(TexProperty)}",
                () => CycleSky(TexProperty, 1));
            AddCaptionButton(
                column,
                "Sky B",
                () => $"Sky B: {SkyName(BlendTexProperty)}",
                () => CycleSky(BlendTexProperty, 1));

            AddMaterialSlider(column, "Sky A -> Sky B blend", TransitionProperty, 0f, 1f);
            AddMaterialSlider(column, "Exposure", ExposureProperty, 0f, 8f);
            AddColorSliders(column, "Tint", TintProperty);
        }

        private void BuildRotationGroup(RectTransform column)
        {
            CreateGroupTitle("Rotation", column);
            AddKeywordToggle(column, "Rotation", EnableRotationProperty, RotationKeyword);
            AddMaterialSlider(column, "Rotation angle", RotationProperty, 0f, 360f);
            AddMaterialSlider(column, "Rotation speed", RotationSpeedProperty, -5f, 5f);
        }

        private void BuildFogGroup(RectTransform column)
        {
            CreateGroupTitle("Sky height fog", column);
            AddKeywordToggle(column, "Sky fog", EnableFogProperty, FogKeyword);
            AddMaterialSlider(column, "Fog intensity", FogIntensityProperty, 0f, 1f);
            AddMaterialSlider(column, "Fog height", FogHeightProperty, 0f, 1f);
            AddMaterialSlider(column, "Fog smoothness", FogSmoothnessProperty, 0.01f, 1f);
            AddMaterialSlider(column, "Fog fill", FogFillProperty, 0f, 1f);

            for (int channel = 0; channel < 3; channel++)
            {
                int index = channel;
                AddSlider(
                    column,
                    $"Fog color {ChannelName(index)}",
                    0f,
                    1f,
                    () => RenderSettings.fogColor[index],
                    value =>
                    {
                        Color color = RenderSettings.fogColor;
                        color[index] = value;
                        RenderSettings.fogColor = color;
                    });
            }
        }

        private void BuildSunGroup(RectTransform column)
        {
            CreateGroupTitle("Sun and ambient", column);
            if (_sun != null)
            {
                AddSlider(column, "Sun yaw", 0f, 360f, () => _sunYaw, value =>
                {
                    _sunYaw = value;
                    ApplySunRotation();
                });
                AddSlider(column, "Sun pitch", -20f, 90f, () => _sunPitch, value =>
                {
                    _sunPitch = value;
                    ApplySunRotation();
                });
                AddSlider(column, "Sun intensity", 0f, 3f,
                    () => _sun.intensity,
                    value => _sun.intensity = value);
            }

            AddSlider(column, "Ambient intensity", 0f, 2f,
                () => RenderSettings.ambientIntensity,
                value => RenderSettings.ambientIntensity = value);
            AddSlider(column, "Reflection intensity", 0f, 1f,
                () => RenderSettings.reflectionIntensity,
                value => RenderSettings.reflectionIntensity = value);
        }

        private void BuildHint(RectTransform column)
        {
            TMP_Text hint = UiFactory.CreateText(
                "Hint",
                column,
                _uiLayer,
                11f,
                FontStyles.Italic,
                TextAlignmentOptions.TopLeft);
            hint.text = "F8 closes this panel. Values are written to the shared sky material and " +
                        "survive leaving play mode in the Editor. F4 toggles fly mode.";
            hint.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(hint.gameObject, 48f);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("Skybox Tuner EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("Skybox Tuner Canvas");
            canvasObject.layer = _uiLayer;
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject;
        }

        private RectTransform CreateColumn(string name, bool leftSide)
        {
            RectTransform column = UiFactory.CreateRect(name, _root.transform, _uiLayer);
            column.anchorMin = new Vector2(leftSide ? 0f : 1f, 1f);
            column.anchorMax = new Vector2(leftSide ? 0f : 1f, 1f);
            column.pivot = new Vector2(leftSide ? 0f : 1f, 1f);
            column.sizeDelta = new Vector2(430f, 0f);
            column.anchoredPosition = new Vector2(leftSide ? 24f : -24f, -24f);

            Image background = UiFactory.AddImage(column.gameObject, UiFactory.PanelBackground);
            background.color = new Color(
                background.color.r,
                background.color.g,
                background.color.b,
                0.82f);

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = RowSpacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        private TMP_Text CreateHeader(RectTransform column)
        {
            TMP_Text header = UiFactory.CreateText(
                "Header",
                column,
                _uiLayer,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            UiFactory.AddLayoutHeight(header.gameObject, 44f);
            return header;
        }

        private void CreateGroupTitle(string title, RectTransform column)
        {
            TMP_Text label = UiFactory.CreateText(
                $"Title {title}",
                column,
                _uiLayer,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            label.text = title;
            label.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(label.gameObject, TitleHeight);
        }

        // ---- Rows ---------------------------------------------------------------------------

        private void AddMaterialSlider(
            RectTransform column, string title, string property, float min, float max)
        {
            if (_skyboxMaterial == null || !_skyboxMaterial.HasProperty(property))
                return;

            AddSlider(
                column,
                title,
                min,
                max,
                () => _skyboxMaterial.GetFloat(property),
                value => _skyboxMaterial.SetFloat(property, value));
        }

        private void AddColorSliders(RectTransform column, string title, string property)
        {
            if (_skyboxMaterial == null || !_skyboxMaterial.HasProperty(property))
                return;

            for (int channel = 0; channel < 3; channel++)
            {
                int index = channel;
                AddSlider(
                    column,
                    $"{title} {ChannelName(index)}",
                    0f,
                    1f,
                    () => _skyboxMaterial.GetColor(property)[index],
                    value =>
                    {
                        Color color = _skyboxMaterial.GetColor(property);
                        color[index] = value;
                        _skyboxMaterial.SetColor(property, color);
                    });
            }
        }

        private void AddSlider(
            RectTransform column,
            string title,
            float min,
            float max,
            Func<float> read,
            Action<float> write)
        {
            RectTransform row = UiFactory.CreateRect($"Row {title}", column, _uiLayer);
            UiFactory.AddLayoutHeight(row.gameObject, RowHeight);

            TMP_Text label = UiFactory.CreateText(
                "Label", row, _uiLayer, 13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            PlaceInRow(label.rectTransform, 0f, LabelHeight);

            var entry = new SliderRow { Title = title, Label = label, Read = read, Write = write };
            entry.Slider = CreateSlider(row, LabelHeight, min, max, entry);
            _sliders.Add(entry);
            RefreshSlider(entry);
        }

        private Slider CreateSlider(RectTransform row, float top, float min, float max, SliderRow entry)
        {
            RectTransform rect = UiFactory.CreateRect("Slider", row, _uiLayer);
            PlaceInRow(rect, top, SliderHeight);
            Image background = UiFactory.AddImage(rect.gameObject, new Color(1f, 1f, 1f, 0.14f));

            RectTransform fillArea = UiFactory.CreateRect("Fill Area", rect, _uiLayer);
            UiFactory.StretchToParent(fillArea);
            RectTransform fill = UiFactory.CreateRect("Fill", fillArea, _uiLayer);
            UiFactory.StretchToParent(fill);
            UiFactory.AddImage(fill.gameObject, UiFactory.ButtonBackground);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = background;
            slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(value => OnSliderChanged(entry, value));
            return slider;
        }

        private void AddKeywordToggle(
            RectTransform column, string title, string property, string keyword)
        {
            if (_skyboxMaterial == null || !_skyboxMaterial.HasProperty(property))
                return;

            AddCaptionButton(
                column,
                title,
                () => $"{title}: {(_skyboxMaterial.GetFloat(property) > 0.5f ? "ON" : "OFF")}",
                () => SetKeyword(property, keyword, _skyboxMaterial.GetFloat(property) <= 0.5f));
        }

        private void AddCaptionButton(
            RectTransform column, string name, Func<string> caption, Action onClick)
        {
            Button button = UiFactory.CreateButton(
                $"Button {name}", column, _uiLayer, caption(), () => { }, 13f);
            UiFactory.AddLayoutHeight(button.gameObject, ButtonHeight);

            var entry = new CaptionRow
            {
                Label = button.GetComponentInChildren<TMP_Text>(),
                Caption = caption,
            };
            _captions.Add(entry);

            button.onClick.AddListener(() =>
            {
                onClick();
                _environmentDirty = true;
                RefreshRows();
            });
        }

        private static void PlaceInRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        // ---- Values -------------------------------------------------------------------------

        private void OnSliderChanged(SliderRow row, float value)
        {
            if (_suppressCallbacks)
                return;

            row.Write(value);
            RefreshSliderLabel(row);
            _environmentDirty = true;
        }

        private void RefreshRows()
        {
            if (_headerLabel != null)
                _headerLabel.text = $"Skybox tuner (F8)\n{_skyboxMaterial.name}";

            for (int index = 0; index < _sliders.Count; index++)
                RefreshSlider(_sliders[index]);
            for (int index = 0; index < _captions.Count; index++)
                _captions[index].Label.text = _captions[index].Caption();
        }

        private void RefreshSlider(SliderRow row)
        {
            _suppressCallbacks = true;
            row.Slider.SetValueWithoutNotify(row.Read());
            _suppressCallbacks = false;
            RefreshSliderLabel(row);
        }

        private void RefreshSliderLabel(SliderRow row)
        {
            row.Label.text = $"{row.Title}   {row.Read():0.###}";
        }

        private void SetKeyword(string property, string keyword, bool enabled)
        {
            _skyboxMaterial.SetFloat(property, enabled ? 1f : 0f);
            if (enabled)
                _skyboxMaterial.EnableKeyword(keyword);
            else
                _skyboxMaterial.DisableKeyword(keyword);
        }

        private void CycleSky(string property, int step)
        {
            if (_skies.Length == 0 || !_skyboxMaterial.HasProperty(property))
                return;

            int index = IndexOfSky(_skyboxMaterial.GetTexture(property));
            index = (index + step + _skies.Length) % _skies.Length;
            _skyboxMaterial.SetTexture(property, _skies[index]);
        }

        private int IndexOfSky(Texture texture)
        {
            for (int index = 0; index < _skies.Length; index++)
            {
                if (_skies[index] == texture)
                    return index;
            }

            return 0;
        }

        private string SkyName(string property)
        {
            if (_skyboxMaterial == null || !_skyboxMaterial.HasProperty(property))
                return "none";

            Texture texture = _skyboxMaterial.GetTexture(property);
            return texture != null ? texture.name : "none";
        }

        private void ApplySunRotation()
        {
            if (_sun != null)
                _sun.transform.rotation = Quaternion.Euler(_sunPitch, _sunYaw, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static string ChannelName(int channel)
        {
            switch (channel)
            {
                case 0: return "R";
                case 1: return "G";
                default: return "B";
            }
        }
    }
}
