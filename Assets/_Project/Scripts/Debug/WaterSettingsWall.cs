using System.Collections.Generic;
using Market.UI;
using Market.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Market.DebugTools
{
    /// <summary>
    /// A physical wall of water controls the player walks up to and operates with the crosshair:
    /// one slider per shader property, arrow buttons for a precise step, and steppers for the
    /// things that are a list rather than a number (weather, wave profile, quality tier).
    /// <para>
    /// The panel is built in code from <see cref="WaterWallFields"/> - adding a property is one
    /// row in that table, not a hand-wired widget.
    /// </para>
    /// Values are written to the renderer's current material. In Play Mode the weather controller
    /// has already swapped in its own runtime copy, so the project material asset is never
    /// touched; with no weather controller present this component makes that copy itself.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WaterSettingsWall : MonoBehaviour
    {
        private const string RuntimeMaterialSuffix = "(Lab Runtime)";
        private const float RowHeight = 34f;
        private const float GroupHeadingHeight = 26f;
        private const float ArrowWidth = 26f;
        // Wide enough for the longest label in the table ("Caustic Intensity") at RowFontSize;
        // a narrower column let the slider crowd the text on the right-hand side.
        private const float LabelWidth = 178f;
        private const float ValueWidth = 62f;
        private const float RowFontSize = 18f;

        [Header("References")]
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private WaveProfileBinder waveProfileBinder;
        [SerializeField] private RealisticWaterWeatherController weatherController;
        [SerializeField] private RealisticWaterQualityController qualityController;

        [Tooltip("Wave profiles the profile stepper cycles through. The empty entry falls back " +
            "to the material's legacy four waves.")]
        [SerializeField] private WaveProfile[] waveProfiles;

        [Header("Layout")]
        [Tooltip("Panel size in metres.")]
        [SerializeField] private Vector2 panelSize = new(3.0f, 2.0f);

        [Tooltip("Pixels per metre used by the world-space canvas.")]
        [SerializeField] private float pixelsPerMetre = 340f;

        [Tooltip("How many columns the rows are split across.")]
        [SerializeField] [Range(1, 3)] private int columns = 2;

        private sealed class Row
        {
            public WaterWallField Field;
            public int PropertyId;
            public Slider Slider;
            public TMP_Text Value;
            public float DefaultValue;
        }

        private readonly List<Row> _rows = new();
        private readonly List<ResolvedWaveLayer> _scratchLayers = new(WaveProfile.MaxLayers);

        private Canvas _canvas;
        private Material _material;
        private TMP_Text _weatherValue;
        private TMP_Text _profileValue;
        private TMP_Text _qualityValue;
        private TMP_Text _statusText;
        private Vector3 _defaultBankScale = Vector3.one;
        private int _profileIndex;
        private bool _applying;

        /// <summary>Rebuilds the panel from the field table. Safe to call again at runtime.</summary>
        public void Rebuild()
        {
            ClearPanel();
            BuildPanel();
            CaptureDefaults();
            RefreshAll();
        }

        private void Awake()
        {
            ResolveReferences();
            Rebuild();
        }

        private void OnEnable()
        {
            AttachCanvasCamera();
            RefreshAll();
        }

        private void Update()
        {
            // The weather controller writes the same properties on a transition, and the wave
            // bank is re-uploaded every frame; without this the sliders would drift out of sync
            // with the water they are supposed to describe.
            if (!_applying)
                RefreshAll();
        }

        private void ResolveReferences()
        {
            if (waterRenderer == null)
                waterRenderer = GetComponentInParent<Renderer>();

            AttachCanvasCamera();
            _material = ResolveMaterial();
        }

        private void AttachCanvasCamera()
        {
            if (_canvas == null)
                return;

            // A world-space canvas with no event camera cannot be raycast, so the crosshair
            // would look at the panel and hit nothing.
            if (_canvas.worldCamera == null)
                _canvas.worldCamera = Camera.main;
        }

        private Material ResolveMaterial()
        {
            if (waterRenderer == null)
                return null;

            Material current = waterRenderer.sharedMaterial;
            if (current == null)
                return null;

            if (!Application.isPlaying || weatherController != null)
                return current;

            if (current.name.EndsWith(RuntimeMaterialSuffix))
                return current;

            Material runtimeCopy = new(current)
            {
                name = $"{current.name} {RuntimeMaterialSuffix}",
            };
            waterRenderer.sharedMaterial = runtimeCopy;
            return runtimeCopy;
        }

        private void CaptureDefaults()
        {
            _material = ResolveMaterial();
            _defaultBankScale = waveProfileBinder != null
                ? waveProfileBinder.BankScale
                : Vector3.one;

            for (int i = 0; i < _rows.Count; i++)
                _rows[i].DefaultValue = ReadValue(_rows[i]);
        }

        private void BuildPanel()
        {
            int layer = LayerMask.NameToLayer("UI");
            if (layer < 0)
                layer = gameObject.layer;

            GameObject canvasObject = new("Wall Canvas", typeof(RectTransform))
            {
                layer = layer,
                // Built from the field table on every load, in the editor as well as in Play
                // Mode, so it is never serialized into the scene - otherwise every rebuild would
                // leave another copy of a hundred widgets in the scene file.
                hideFlags = HideFlags.DontSave,
            };
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(
                panelSize.x * pixelsPerMetre, panelSize.y * pixelsPerMetre);
            canvasRect.localScale = Vector3.one / pixelsPerMetre;
            canvasRect.anchoredPosition3D = Vector3.zero;
            AttachCanvasCamera();

            RectTransform background = UiFactory.CreateRect("Background", canvasRect, layer);
            UiFactory.StretchToParent(background);
            UiFactory.AddImage(background.gameObject, UiFactory.PanelBackground);

            float width = canvasRect.sizeDelta.x;
            float height = canvasRect.sizeDelta.y;

            BuildTitle(background, layer, width);
            BuildRows(background, layer, width, height);
            BuildFooter(background, layer, width);
        }

        private void BuildTitle(RectTransform parent, int layer, float width)
        {
            TMP_Text title = UiFactory.CreateText(
                "Title", parent, layer, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "WATER SETTINGS";
            Place(title.rectTransform, 0f, 14f, width, 40f);

            _statusText = UiFactory.CreateText(
                "Status", parent, layer, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            _statusText.color = UiFactory.MutedText;
            _statusText.text = "Aim with the dot, hold LMB on a slider and look along it";
            Place(_statusText.rectTransform, 0f, 54f, width, 24f);
        }

        private void BuildRows(RectTransform parent, int layer, float width, float height)
        {
            int columnCount = Mathf.Max(1, columns);
            float columnWidth = width / columnCount;
            const float TopInset = 88f;
            const float BottomInset = 76f;
            float usableHeight = height - TopInset - BottomInset;

            int rowsPerColumn = Mathf.CeilToInt(
                CountLayoutSlots() / (float)columnCount);
            float slotHeight = Mathf.Min(
                RowHeight, usableHeight / Mathf.Max(1, rowsPerColumn));

            string currentGroup = null;
            int slot = 0;

            for (int i = 0; i < WaterWallFields.All.Length; i++)
            {
                WaterWallField field = WaterWallFields.All[i];
                if (field.Group != currentGroup)
                {
                    currentGroup = field.Group;
                    int headingColumn = Mathf.Min(slot / rowsPerColumn, columnCount - 1);
                    float headingTop = TopInset + (slot % rowsPerColumn) * slotHeight;
                    BuildGroupHeading(
                        parent, layer, currentGroup,
                        headingColumn * columnWidth, headingTop, columnWidth);
                    slot++;
                }

                int column = Mathf.Min(slot / rowsPerColumn, columnCount - 1);
                float top = TopInset + (slot % rowsPerColumn) * slotHeight;
                BuildFieldRow(
                    parent, layer, field, column * columnWidth, top, columnWidth, slotHeight);
                slot++;
            }
        }

        private static int CountLayoutSlots()
        {
            int slots = 0;
            string currentGroup = null;
            for (int i = 0; i < WaterWallFields.All.Length; i++)
            {
                if (WaterWallFields.All[i].Group != currentGroup)
                {
                    currentGroup = WaterWallFields.All[i].Group;
                    slots++;
                }

                slots++;
            }

            return slots;
        }

        private void BuildGroupHeading(
            RectTransform parent, int layer, string group,
            float left, float top, float columnWidth)
        {
            TMP_Text heading = UiFactory.CreateText(
                $"Group {group}", parent, layer, 20f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            heading.color = new Color(0.55f, 0.85f, 1f);
            heading.text = group;
            Place(
                heading.rectTransform,
                left + 16f, top + 4f, columnWidth - 32f, GroupHeadingHeight);
        }

        private void BuildFieldRow(
            RectTransform parent, int layer, WaterWallField field,
            float left, float top, float columnWidth, float slotHeight)
        {
            var row = new Row
            {
                Field = field,
                PropertyId = string.IsNullOrEmpty(field.Property) ? 0 : field.PropertyId,
            };

            TMP_Text label = UiFactory.CreateText(
                $"Label {field.Label}", parent, layer, RowFontSize,
                FontStyles.Normal, TextAlignmentOptions.Left);
            label.text = field.Label;
            Place(label.rectTransform, left + 16f, top, LabelWidth, slotHeight);

            row.Value = UiFactory.CreateText(
                $"Value {field.Label}", parent, layer, RowFontSize,
                FontStyles.Bold, TextAlignmentOptions.Right);
            Place(
                row.Value.rectTransform,
                left + columnWidth - ValueWidth - 16f, top, ValueWidth, slotHeight);

            float sliderLeft = left + 16f + LabelWidth + ArrowWidth + 6f;
            float sliderWidth =
                columnWidth - LabelWidth - ValueWidth - 2f * ArrowWidth - 62f;

            RectTransform downArrow = UiFactory.CreateButton(
                $"Down {field.Label}", parent, layer, "<",
                () => StepRow(row, -1), 20f).GetComponent<RectTransform>();
            Place(downArrow, left + 16f + LabelWidth, top + 4f, ArrowWidth, slotHeight - 8f);

            row.Slider = BuildSlider(parent, layer, field, row);
            Place(
                row.Slider.GetComponent<RectTransform>(),
                sliderLeft, top + 8f, Mathf.Max(40f, sliderWidth), slotHeight - 16f);

            RectTransform upArrow = UiFactory.CreateButton(
                $"Up {field.Label}", parent, layer, ">",
                () => StepRow(row, 1), 20f).GetComponent<RectTransform>();
            Place(
                upArrow, sliderLeft + Mathf.Max(40f, sliderWidth) + 6f, top + 4f,
                ArrowWidth, slotHeight - 8f);

            _rows.Add(row);
        }

        private Slider BuildSlider(
            RectTransform parent, int layer, WaterWallField field, Row row)
        {
            RectTransform sliderRect = UiFactory.CreateRect(
                $"Slider {field.Label}", parent, layer);

            RectTransform background = UiFactory.CreateRect("Background", sliderRect, layer);
            UiFactory.StretchToParent(background);
            UiFactory.AddImage(background.gameObject, new Color(0.18f, 0.20f, 0.23f, 1f));

            RectTransform fillArea = UiFactory.CreateRect("Fill Area", sliderRect, layer);
            UiFactory.StretchToParent(fillArea);
            RectTransform fill = UiFactory.CreateRect("Fill", fillArea, layer);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = UiFactory.AddImage(fill.gameObject, UiFactory.ButtonBackground);
            fillImage.raycastTarget = false;

            RectTransform handleArea = UiFactory.CreateRect("Handle Slide Area", sliderRect, layer);
            UiFactory.StretchToParent(handleArea);
            RectTransform handle = UiFactory.CreateRect("Handle", handleArea, layer);
            handle.sizeDelta = new Vector2(14f, 0f);
            Image handleImage = UiFactory.AddImage(
                handle.gameObject, new Color(0.85f, 0.93f, 1f, 1f));

            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = field.Minimum;
            slider.maxValue = field.Maximum;
            slider.onValueChanged.AddListener(value => OnSliderChanged(row, value));
            return slider;
        }

        private void BuildFooter(RectTransform parent, int layer, float width)
        {
            float top = panelSize.y * pixelsPerMetre - 64f;
            float stepperWidth = (width - 64f) / 4f;

            _weatherValue = BuildStepper(
                parent, layer, "WEATHER", 16f, top, stepperWidth, StepWeather);
            _profileValue = BuildStepper(
                parent, layer, "WAVE PROFILE", 16f + stepperWidth, top, stepperWidth,
                StepWaveProfile);
            _qualityValue = BuildStepper(
                parent, layer, "QUALITY", 16f + 2f * stepperWidth, top, stepperWidth,
                StepQuality);

            RectTransform resetButton = UiFactory.CreateButton(
                "Reset", parent, layer, "RESET ALL", ResetAll, 20f)
                .GetComponent<RectTransform>();
            Place(resetButton, 16f + 3f * stepperWidth + 8f, top + 18f, stepperWidth - 24f, 34f);
        }

        private TMP_Text BuildStepper(
            RectTransform parent, int layer, string label,
            float left, float top, float widthAvailable,
            System.Action<int> onStep)
        {
            TMP_Text caption = UiFactory.CreateText(
                $"Caption {label}", parent, layer, 16f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            caption.color = UiFactory.MutedText;
            caption.text = label;
            Place(caption.rectTransform, left, top, widthAvailable - 12f, 18f);

            RectTransform downArrow = UiFactory
                .CreateButton($"{label} Down", parent, layer, "<", () => onStep(-1), 20f)
                .GetComponent<RectTransform>();
            Place(downArrow, left, top + 20f, ArrowWidth, 30f);

            TMP_Text value = UiFactory.CreateText(
                $"Value {label}", parent, layer, 19f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Place(
                value.rectTransform, left + ArrowWidth + 4f, top + 22f,
                widthAvailable - 2f * ArrowWidth - 20f, 26f);

            RectTransform upArrow = UiFactory
                .CreateButton($"{label} Up", parent, layer, ">", () => onStep(1), 20f)
                .GetComponent<RectTransform>();
            Place(upArrow, left + widthAvailable - ArrowWidth - 12f, top + 20f, ArrowWidth, 30f);

            return value;
        }

        private void ClearPanel()
        {
            _rows.Clear();
            if (_canvas == null)
                return;

            if (Application.isPlaying)
                Destroy(_canvas.gameObject);
            else
                DestroyImmediate(_canvas.gameObject);

            _canvas = null;
        }

        private void StepRow(Row row, int direction)
        {
            float range = row.Field.Maximum - row.Field.Minimum;
            float stepped = Mathf.Clamp(
                ReadValue(row) + direction * range * 0.05f,
                row.Field.Minimum,
                row.Field.Maximum);
            WriteValue(row, stepped);
            RefreshRow(row);
        }

        private void OnSliderChanged(Row row, float value)
        {
            if (_applying)
                return;

            WriteValue(row, value);
            RefreshRowValueText(row);
        }

        private float ReadValue(Row row)
        {
            switch (row.Field.Target)
            {
                case WaterWallTarget.WaveLengthScale:
                    return waveProfileBinder != null ? waveProfileBinder.BankScale.x : 1f;
                case WaterWallTarget.WaveAmplitudeScale:
                    return waveProfileBinder != null ? waveProfileBinder.BankScale.y : 1f;
                case WaterWallTarget.WaveSteepnessScale:
                    return waveProfileBinder != null ? waveProfileBinder.BankScale.z : 1f;
                default:
                    _material = ResolveMaterial();
                    return _material != null && _material.HasProperty(row.PropertyId)
                        ? _material.GetFloat(row.PropertyId)
                        : row.Field.Minimum;
            }
        }

        private void WriteValue(Row row, float value)
        {
            if (row.Field.Target == WaterWallTarget.MaterialFloat)
            {
                _material = ResolveMaterial();
                if (_material != null && _material.HasProperty(row.PropertyId))
                    _material.SetFloat(row.PropertyId, value);

                return;
            }

            if (waveProfileBinder == null)
                return;

            Vector3 scale = waveProfileBinder.BankScale;
            switch (row.Field.Target)
            {
                case WaterWallTarget.WaveLengthScale:
                    scale.x = value;
                    break;
                case WaterWallTarget.WaveAmplitudeScale:
                    scale.y = value;
                    break;
                case WaterWallTarget.WaveSteepnessScale:
                    scale.z = value;
                    break;
            }

            waveProfileBinder.BankScale = scale;
        }

        private void StepWeather(int direction)
        {
            if (weatherController == null)
                return;

            const int StateCount = (int)RealisticWaterWeather.Storm + 1;
            int next = ((int)weatherController.Weather + direction + StateCount) % StateCount;
            weatherController.SetWeather((RealisticWaterWeather)next);
            RefreshAll();
        }

        private void StepWaveProfile(int direction)
        {
            if (waveProfileBinder == null || waveProfiles == null || waveProfiles.Length == 0)
                return;

            _profileIndex =
                (_profileIndex + direction + waveProfiles.Length) % waveProfiles.Length;
            waveProfileBinder.Profile = waveProfiles[_profileIndex];
            RefreshAll();
        }

        private void StepQuality(int direction)
        {
            if (qualityController == null)
                return;

            const int TierCount = (int)RealisticWaterQualityTier.High + 1;
            int next = ((int)qualityController.QualityTier + direction + TierCount) % TierCount;
            qualityController.SetQuality((RealisticWaterQualityTier)next);
            RefreshAll();
        }

        private void ResetAll()
        {
            _applying = true;

            if (weatherController != null)
            {
                // Re-apply the selected weather rather than the values captured at load: those
                // are the pre-weather material defaults, so restoring them would leave the wall
                // reading BREEZE while the water showed something else.
                // This also restores the bank scale, since the weather step owns it.
                weatherController.SetWeatherImmediate(weatherController.Weather);
            }
            else
            {
                for (int i = 0; i < _rows.Count; i++)
                    WriteValue(_rows[i], _rows[i].DefaultValue);

                if (waveProfileBinder != null)
                    waveProfileBinder.BankScale = _defaultBankScale;
            }

            _applying = false;
            RefreshAll();
        }

        private void RefreshAll()
        {
            _applying = true;

            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(_rows[i]);

            if (_weatherValue != null)
            {
                _weatherValue.text = weatherController != null
                    ? weatherController.Weather.ToString().ToUpperInvariant()
                    : "-";
            }

            if (_profileValue != null)
                _profileValue.text = DescribeProfile();

            if (_qualityValue != null)
            {
                _qualityValue.text = qualityController != null
                    ? qualityController.QualityTier.ToString().ToUpperInvariant()
                    : "-";
            }

            _applying = false;
        }

        private string DescribeProfile()
        {
            if (waveProfileBinder == null)
                return "-";

            WaveProfile profile = waveProfileBinder.Profile;
            if (profile == null)
                return "LEGACY";

            profile.ResolveLayers(_scratchLayers);
            return $"{profile.name.ToUpperInvariant()} ({_scratchLayers.Count})";
        }

        private void RefreshRow(Row row)
        {
            float value = ReadValue(row);
            if (row.Slider != null)
                row.Slider.SetValueWithoutNotify(value);

            RefreshRowValueText(row, value);
        }

        private void RefreshRowValueText(Row row)
        {
            RefreshRowValueText(row, ReadValue(row));
        }

        private void RefreshRowValueText(Row row, float value)
        {
            if (row.Value == null)
                return;

            row.Value.text = Mathf.Abs(value) >= 10f
                ? value.ToString("0.0")
                : value.ToString("0.00");
        }

        private static void Place(
            RectTransform rect, float left, float top, float width, float height)
        {
            UiFactory.PlaceTopLeft(rect, left, top, width, height);
        }
    }
}
