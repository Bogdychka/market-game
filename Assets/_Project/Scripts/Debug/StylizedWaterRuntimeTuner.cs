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
    /// In-game panel that tunes the Bitgem stylized water while playing: every catalogued shader
    /// property gets a labelled slider with a short explanation, and presets can be cycled, loaded
    /// and saved without leaving play mode. Toggled with F7. Changes are written to the shared
    /// material, so inside the Editor they survive leaving play mode.
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    [DisallowMultipleComponent]
    public class StylizedWaterRuntimeTuner : MonoBehaviour
    {
        private const float RowSpacing = 4f;
        private const float SliderHeight = 14f;
        private const float LabelHeight = 18f;
        private const float DescriptionHeight = 32f;
        private const string NewPresetPrefix = "water-";

        [Header("References")]
        [Tooltip("Renderer whose shared material is tuned.")]
        [SerializeField] private Renderer _waterRenderer;

        [Tooltip("Disabled while the panel is open when no UIModeService is present.")]
        [SerializeField] private FirstPersonController _playerController;

        [Header("Settings")]
        [Tooltip("Open the panel as soon as play mode starts.")]
        [SerializeField] private bool _startOpen;

        private readonly List<FieldRow> _rows = new List<FieldRow>();
        private readonly List<StylizedWaterField> _leftFields = new List<StylizedWaterField>();

        private GameObject _root;
        private TMP_Text _presetLabel;
        private TMP_Text _headerLabel;
        private string[] _presetNames = System.Array.Empty<string>();
        private int _presetIndex;
        private bool _isOpen;
        private int _uiLayer;

        private sealed class FieldRow
        {
            public StylizedWaterField Field;
            public TMP_Text Label;
            public Slider[] Sliders;
        }

        private Material Material =>
            _waterRenderer != null ? _waterRenderer.sharedMaterial : null;

        /// <summary>Wires the tuner without manual Inspector setup.</summary>
        public void Configure(Renderer waterRenderer, FirstPersonController playerController)
        {
            _waterRenderer = waterRenderer;
            _playerController = playerController;
        }

        private void Awake()
        {
            _uiLayer = LayerMask.NameToLayer("UI");
            if (_uiLayer < 0)
                _uiLayer = 0;

            if (_waterRenderer == null)
                Debug.LogError($"{nameof(StylizedWaterRuntimeTuner)}: water renderer is not assigned.", this);
        }

        private void Start()
        {
            if (_startOpen)
                SetOpen(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame)
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
                        $"{nameof(StylizedWaterRuntimeTuner)}: no water material to tune.",
                        this);
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
            if (!open)
                return;

            RefreshPresetList();
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
            SplitFieldsIntoColumns();

            RectTransform left = CreateColumn("Left Column", true);
            RectTransform right = CreateColumn("Right Column", false);

            _headerLabel = CreateHeader(left);
            foreach (StylizedWaterGroup group in StylizedWaterShaderCatalog.Groups)
                CreateGroup(group, left, right);

            CreatePresetControls(right);
            _root.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("Water Tuner EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("Water Tuner Canvas");
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
            column.anchorMin = new Vector2(leftSide ? 0f : 1f, 0f);
            column.anchorMax = new Vector2(leftSide ? 0f : 1f, 1f);
            column.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
            column.sizeDelta = new Vector2(470f, -48f);
            column.anchoredPosition = new Vector2(leftSide ? 24f : -24f, 0f);

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

        private void SplitFieldsIntoColumns()
        {
            // Colour, foam and lighting stay on the left; ripples and waves go right.
            _leftFields.Clear();
            foreach (StylizedWaterGroup group in StylizedWaterShaderCatalog.Groups)
            {
                if (group.Title != "Colour and depth" &&
                    group.Title != "Shore foam" &&
                    group.Title != "Lighting response")
                {
                    continue;
                }

                foreach (StylizedWaterField field in group.Fields)
                    _leftFields.Add(field);
            }
        }

        private void CreateGroup(StylizedWaterGroup group, RectTransform left, RectTransform right)
        {
            RectTransform column = _leftFields.Contains(group.Fields[0]) ? left : right;
            CreateGroupTitle(group.Title, column);
            foreach (StylizedWaterField field in group.Fields)
            {
                if (field.Kind == StylizedWaterFieldKind.Texture)
                    continue;
                if (Material == null || !Material.HasProperty(field.Property))
                    continue;

                CreateFieldRow(field, column);
            }
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
            UiFactory.AddLayoutHeight(label.gameObject, 24f);
        }

        private void CreateFieldRow(StylizedWaterField field, RectTransform column)
        {
            int channels = ChannelCount(field);
            float height = LabelHeight + channels * (SliderHeight + 2f) + DescriptionHeight;

            RectTransform row = UiFactory.CreateRect($"Row {field.Label}", column, _uiLayer);
            UiFactory.AddLayoutHeight(row.gameObject, height);

            TMP_Text label = CreateRowText(row, 13f, FontStyles.Normal, 0f, LabelHeight);
            var sliders = new Slider[channels];
            for (int channel = 0; channel < channels; channel++)
            {
                float top = LabelHeight + channel * (SliderHeight + 2f);
                sliders[channel] = CreateSlider(row, field, channel, top);
            }

            TMP_Text description = CreateRowText(
                row,
                11f,
                FontStyles.Italic,
                LabelHeight + channels * (SliderHeight + 2f),
                DescriptionHeight);
            description.text = field.Description;
            description.color = UiFactory.MutedText;

            var fieldRow = new FieldRow { Field = field, Label = label, Sliders = sliders };
            _rows.Add(fieldRow);
            RefreshRow(fieldRow);
        }

        private TMP_Text CreateRowText(
            RectTransform row,
            float fontSize,
            FontStyles style,
            float top,
            float height)
        {
            TMP_Text text = UiFactory.CreateText(
                "Text",
                row,
                _uiLayer,
                fontSize,
                style,
                TextAlignmentOptions.TopLeft);
            PlaceInRow(text.rectTransform, top, height);
            return text;
        }

        private static void PlaceInRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private Slider CreateSlider(
            RectTransform row,
            StylizedWaterField field,
            int channel,
            float top)
        {
            RectTransform rect = UiFactory.CreateRect($"Slider {channel}", row, _uiLayer);
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
            slider.minValue = ChannelMin(field);
            slider.maxValue = ChannelMax(field);
            slider.onValueChanged.AddListener(value => OnSliderChanged(field, channel, value));
            return slider;
        }

        // ---- Presets ------------------------------------------------------------------------

        private void CreatePresetControls(RectTransform column)
        {
            CreateGroupTitle("Presets", column);

            RectTransform selector = UiFactory.CreateRect("Preset Selector", column, _uiLayer);
            UiFactory.AddLayoutHeight(selector.gameObject, 28f);
            CreateInlineButton(selector, "<", 0f, 40f, () => StepPreset(-1));
            _presetLabel = UiFactory.CreateText(
                "Preset Name",
                selector,
                _uiLayer,
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            PlaceInRow(_presetLabel.rectTransform, 4f, 22f);

            CreateInlineButton(selector, ">", 400f, 40f, () => StepPreset(1));

            RectTransform actions = UiFactory.CreateRect("Preset Actions", column, _uiLayer);
            UiFactory.AddLayoutHeight(actions.gameObject, 30f);
            CreateInlineButton(actions, "Load", 0f, 132f, LoadSelectedPreset);
            CreateInlineButton(actions, "Overwrite", 148f, 132f, OverwriteSelectedPreset);
            CreateInlineButton(actions, "Save new", 296f, 132f, SaveNewPreset);

            TMP_Text hint = UiFactory.CreateText(
                "Preset Hint",
                column,
                _uiLayer,
                11f,
                FontStyles.Italic,
                TextAlignmentOptions.TopLeft);
            hint.text = "Presets are JSON files shared with the editor tuner window. " +
                        "F7 closes this panel.";
            hint.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(hint.gameObject, 34f);
        }

        private void CreateInlineButton(
            RectTransform parent,
            string label,
            float left,
            float width,
            UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(
                $"Button {label}",
                parent,
                _uiLayer,
                label,
                onClick,
                13f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(left, 0f);
        }

        private void RefreshPresetList()
        {
            _presetNames = StylizedWaterPresets.List();
            _presetIndex = _presetNames.Length == 0
                ? 0
                : Mathf.Clamp(_presetIndex, 0, _presetNames.Length - 1);
            RefreshPresetLabel();
        }

        private void RefreshPresetLabel()
        {
            if (_presetLabel == null)
                return;

            _presetLabel.text = _presetNames.Length == 0
                ? "no presets saved"
                : _presetNames[_presetIndex];
        }

        private void StepPreset(int step)
        {
            if (_presetNames.Length == 0)
                return;

            _presetIndex = (_presetIndex + step + _presetNames.Length) % _presetNames.Length;
            RefreshPresetLabel();
        }

        private void LoadSelectedPreset()
        {
            if (_presetNames.Length == 0 || Material == null)
                return;

            StylizedWaterPreset preset = StylizedWaterPresets.Load(_presetNames[_presetIndex]);
            if (preset == null)
                return;

            StylizedWaterPresets.Apply(preset, Material);
            RefreshRows();
        }

        private void OverwriteSelectedPreset()
        {
            if (_presetNames.Length == 0)
            {
                SaveNewPreset();
                return;
            }

            StylizedWaterPresets.Save(_presetNames[_presetIndex], Material);
            RefreshPresetList();
        }

        private void SaveNewPreset()
        {
            string name = NextPresetName();
            if (!StylizedWaterPresets.Save(name, Material))
                return;

            RefreshPresetList();
            for (int index = 0; index < _presetNames.Length; index++)
            {
                if (_presetNames[index] != name)
                    continue;

                _presetIndex = index;
                break;
            }

            RefreshPresetLabel();
        }

        private string NextPresetName()
        {
            for (int number = 1; number < 100; number++)
            {
                string candidate = $"{NewPresetPrefix}{number:00}";
                if (System.Array.IndexOf(_presetNames, candidate) < 0)
                    return candidate;
            }

            return $"{NewPresetPrefix}99";
        }

        // ---- Values -------------------------------------------------------------------------

        private void OnSliderChanged(StylizedWaterField field, int channel, float value)
        {
            Material material = Material;
            if (material == null || !material.HasProperty(field.Property))
                return;

            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    Color color = material.GetColor(field.Property);
                    color[channel] = value;
                    material.SetColor(field.Property, color);
                    break;
                case StylizedWaterFieldKind.Tiling:
                    Vector4 vector = material.GetVector(field.Property);
                    vector[channel] = value;
                    material.SetVector(field.Property, vector);
                    break;
                default:
                    material.SetFloat(field.Property, value);
                    break;
            }

            RefreshLabelOf(field);
        }

        private void RefreshRows()
        {
            if (_headerLabel != null && Material != null)
                _headerLabel.text = $"Water tuner (F7)\n{Material.name}";

            for (int index = 0; index < _rows.Count; index++)
                RefreshRow(_rows[index]);
        }

        private void RefreshRow(FieldRow row)
        {
            for (int channel = 0; channel < row.Sliders.Length; channel++)
                row.Sliders[channel].SetValueWithoutNotify(ReadChannel(row.Field, channel));
            RefreshLabel(row);
        }

        private void RefreshLabelOf(StylizedWaterField field)
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                if (_rows[index].Field != field)
                    continue;

                RefreshLabel(_rows[index]);
                return;
            }
        }

        private void RefreshLabel(FieldRow row)
        {
            Material material = Material;
            if (material == null)
                return;

            switch (row.Field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    Color color = material.GetColor(row.Field.Property);
                    row.Label.text = $"{row.Field.Label}   R {color.r:0.00}  G {color.g:0.00}  " +
                                     $"B {color.b:0.00}  A {color.a:0.00}";
                    break;
                case StylizedWaterFieldKind.Tiling:
                    Vector4 vector = material.GetVector(row.Field.Property);
                    row.Label.text = $"{row.Field.Label}   X {vector.x:0.00}  Y {vector.y:0.00}";
                    break;
                default:
                    row.Label.text =
                        $"{row.Field.Label}   {material.GetFloat(row.Field.Property):0.###}";
                    break;
            }
        }

        private float ReadChannel(StylizedWaterField field, int channel)
        {
            Material material = Material;
            if (material == null || !material.HasProperty(field.Property))
                return 0f;

            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    return material.GetColor(field.Property)[channel];
                case StylizedWaterFieldKind.Tiling:
                    return material.GetVector(field.Property)[channel];
                default:
                    return material.GetFloat(field.Property);
            }
        }

        private static int ChannelCount(StylizedWaterField field)
        {
            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    return 4;
                case StylizedWaterFieldKind.Tiling:
                    return 2;
                default:
                    return 1;
            }
        }

        private static float ChannelMin(StylizedWaterField field) =>
            field.Kind == StylizedWaterFieldKind.Color ? 0f : field.Min;

        private static float ChannelMax(StylizedWaterField field) =>
            field.Kind == StylizedWaterFieldKind.Color ? 1f : field.Max;
    }
}
