using System.Collections.Generic;
using Market.Core;
using Market.Player;
using Market.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Market.DebugTools
{
    /// <summary>
    /// In-game panel for the WaterWorks lab scene, toggled with F6. Every feature of the
    /// GapperGames water shader gets an on/off button so it can be A/B compared against the same
    /// view, plus a switch between the two shipped water materials. Changes are written to the
    /// shared material, so inside the Editor they survive leaving play mode.
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterWorksLabController : MonoBehaviour
    {
        private const string VolumeMaterialResource = "Water_Volume";
        private const string VolumeDensityProperty = "density";
        private const float RowHeight = 46f;
        private const float PanelWidth = 460f;

        private readonly struct FeatureToggle
        {
            public FeatureToggle(string label, string property, float off, float on, string hint)
            {
                Label = label;
                Property = property;
                Off = off;
                On = on;
                Hint = hint;
            }

            public string Label { get; }
            public string Property { get; }
            public float Off { get; }
            public float On { get; }
            public string Hint { get; }
        }

        /// <summary>
        /// Water shader features. "On" values match the values the package ships with, so turning
        /// everything on returns the material to its authored look.
        /// Property names come from the shader graph, not from the material: SSR_Water.mat still
        /// carries dead duplicates from an older revision of the shader (_Use_Foam, _Normal_Strength,
        /// _Edge_Color, _MaxDist ...) which look real in the Inspector but drive nothing.
        /// </summary>
        private static readonly FeatureToggle[] SurfaceToggles =
        {
            new FeatureToggle("Screen-space reflections", "_ScreenSpaceReflections", 0f, 1f,
                "Reflects the pillars and sky in the surface"),
            new FeatureToggle("Shoreline foam", "_UseFoam", 0f, 1f,
                "Depth-based foam where the water meets the seabed"),
            new FeatureToggle("Wave displacement", "_Displacement_Amount", 0f, 0.35f,
                "Vertex waves, fades out past Max Wave Dist"),
            new FeatureToggle("Caustics", "_Caustic_Strength", 0f, 2f,
                "Light patterns projected on the seabed"),
            new FeatureToggle("Surface detail", "_NormalStrength", 0f, 0.1f,
                "Normal map ripples - the source of the distant shimmer"),
            new FeatureToggle("Refraction", "_Transparency", 1f, 0.95f,
                "Opaque-texture distortion of what is under the surface"),
        };

        [Header("References")]
        [Tooltip("Renderer whose shared material is toggled.")]
        [SerializeField] private Renderer _waterRenderer;

        [Tooltip("Materials cycled by the Water Material button.")]
        [SerializeField] private Material[] _waterMaterials = System.Array.Empty<Material>();

        [Tooltip("Volume material driving the underwater renderer feature. Project copy, not the package one.")]
        [SerializeField] private Material _volumeMaterial;

        [Tooltip("Disabled while the panel is open when no UIModeService is present.")]
        [SerializeField] private FirstPersonController _playerController;

        private readonly List<TMP_Text> _toggleLabels = new List<TMP_Text>();

        private GameObject _root;
        private TMP_Text _materialLabel;
        private TMP_Text _volumeLabel;
        private bool _isOpen;
        private int _uiLayer;

        private Material Material =>
            _waterRenderer != null ? _waterRenderer.sharedMaterial : null;

        /// <summary>Wires the controller without manual Inspector setup.</summary>
        public void Configure(
            Renderer waterRenderer,
            Material[] waterMaterials,
            Material volumeMaterial,
            FirstPersonController playerController)
        {
            _waterRenderer = waterRenderer;
            _waterMaterials = waterMaterials;
            _volumeMaterial = volumeMaterial;
            _playerController = playerController;
        }

        private void Awake()
        {
            _uiLayer = LayerMask.NameToLayer("UI");
            if (_uiLayer < 0)
                _uiLayer = 0;

            if (_volumeMaterial == null)
                _volumeMaterial = Resources.Load<Material>(VolumeMaterialResource);
            if (_waterRenderer == null)
                Debug.LogError($"{nameof(WaterWorksLabController)}: water renderer is not assigned.", this);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame)
                SetOpen(!_isOpen);
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
                if (Material == null)
                {
                    Debug.LogError(
                        $"{nameof(WaterWorksLabController)}: no water material to toggle.", this);
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
                RefreshLabels();
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

            RectTransform column = CreateColumn();
            CreateHeader(column);

            for (int index = 0; index < SurfaceToggles.Length; index++)
                CreateToggleRow(column, SurfaceToggles[index], index);

            _volumeLabel = CreateActionRow(column, ToggleUnderwaterVolume);
            _materialLabel = CreateActionRow(column, CycleWaterMaterial);

            CreateHint(column,
                "Underwater volume needs the WaterWorksLab renderer on the camera.");

            _root.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("WaterWorks Lab EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("WaterWorks Lab Canvas");
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

        private RectTransform CreateColumn()
        {
            RectTransform column = UiFactory.CreateRect("Panel", _root.transform, _uiLayer);
            column.anchorMin = new Vector2(0f, 1f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(24f, -24f);
            column.sizeDelta = new Vector2(PanelWidth, 0f);

            UiFactory.AddImage(column.gameObject, UiFactory.PanelBackground);

            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        private void CreateHeader(RectTransform column)
        {
            TMP_Text header = UiFactory.CreateText(
                "Header", column, _uiLayer, 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            header.text = "WATERWORKS LAB  -  F6";
            UiFactory.AddLayoutHeight(header.gameObject, 32f);

            CreateHint(column, "Toggle one feature at a time and compare the same view.");
        }

        private void CreateHint(RectTransform column, string value)
        {
            TMP_Text hint = UiFactory.CreateText(
                "Hint", column, _uiLayer, 14f, FontStyles.Italic, TextAlignmentOptions.Left);
            hint.text = value;
            hint.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(hint.gameObject, 22f);
        }

        private void CreateToggleRow(RectTransform column, FeatureToggle toggle, int index)
        {
            Button button = UiFactory.CreateButton(
                $"Toggle {toggle.Label}", column, _uiLayer, toggle.Label,
                () => ToggleFeature(index), 16f);
            UiFactory.AddLayoutHeight(button.gameObject, RowHeight);
            _toggleLabels.Add(button.GetComponentInChildren<TMP_Text>());

            CreateHint(column, toggle.Hint);
        }

        private TMP_Text CreateActionRow(RectTransform column, UnityEngine.Events.UnityAction action)
        {
            Button button = UiFactory.CreateButton(
                "Action", column, _uiLayer, string.Empty, action, 16f);
            UiFactory.AddLayoutHeight(button.gameObject, RowHeight);
            return button.GetComponentInChildren<TMP_Text>();
        }

        // ---- Actions ------------------------------------------------------------------------

        private void ToggleFeature(int index)
        {
            Material material = Material;
            FeatureToggle toggle = SurfaceToggles[index];
            if (material == null || !material.HasProperty(toggle.Property))
                return;

            bool isOn = IsOn(material, toggle);
            material.SetFloat(toggle.Property, isOn ? toggle.Off : toggle.On);
            RefreshLabels();
        }

        private void ToggleUnderwaterVolume()
        {
            if (_volumeMaterial == null || !_volumeMaterial.HasProperty(VolumeDensityProperty))
                return;

            float density = _volumeMaterial.GetFloat(VolumeDensityProperty);
            _volumeMaterial.SetFloat(VolumeDensityProperty, density > 0.01f ? 0f : 1f);
            RefreshLabels();
        }

        private void CycleWaterMaterial()
        {
            if (_waterRenderer == null || _waterMaterials.Length == 0)
                return;

            int next = 0;
            for (int index = 0; index < _waterMaterials.Length; index++)
            {
                if (_waterMaterials[index] != _waterRenderer.sharedMaterial)
                    continue;

                next = (index + 1) % _waterMaterials.Length;
                break;
            }

            _waterRenderer.sharedMaterial = _waterMaterials[next];
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            Material material = Material;
            for (int index = 0; index < _toggleLabels.Count; index++)
            {
                FeatureToggle toggle = SurfaceToggles[index];
                bool supported = material != null && material.HasProperty(toggle.Property);
                string state = !supported ? "n/a" : IsOn(material, toggle) ? "ON" : "OFF";
                _toggleLabels[index].text = $"{toggle.Label}   [{state}]";
            }

            if (_volumeLabel != null)
            {
                bool volumeOn = _volumeMaterial != null
                    && _volumeMaterial.HasProperty(VolumeDensityProperty)
                    && _volumeMaterial.GetFloat(VolumeDensityProperty) > 0.01f;
                _volumeLabel.text = $"Underwater volume   [{(volumeOn ? "ON" : "OFF")}]";
            }

            if (_materialLabel != null)
            {
                string name = material != null ? material.name : "none";
                _materialLabel.text = $"Water material   [{name}]";
            }
        }

        private static bool IsOn(Material material, FeatureToggle toggle)
        {
            float value = material.GetFloat(toggle.Property);
            return Mathf.Abs(value - toggle.On) < Mathf.Abs(value - toggle.Off);
        }
    }
}
