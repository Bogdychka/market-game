using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Populates a settings panel RectTransform with sliders, toggles, and rebind buttons.
    /// Instantiate once; call <see cref="CancelActiveRebind"/> before the panel is hidden.
    /// </summary>
    public class SettingsPanelRenderer
    {
        private readonly RectTransform  _panel;
        private readonly int            _layer;
        private readonly SettingsService _service;
        private readonly SettingsSO     _defaults;
        private readonly PlayerInput    _playerInput;  // null in MainMenu

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        public SettingsPanelRenderer(
            RectTransform panel,
            int layer,
            SettingsService service,
            SettingsSO defaults,
            PlayerInput playerInput)
        {
            _panel       = panel;
            _layer       = layer;
            _service     = service;
            _defaults    = defaults;
            _playerInput = playerInput;

            Build();
        }

        /// <summary>Cancels any in-progress interactive rebind (call when panel closes).</summary>
        public void CancelActiveRebind()
        {
            _activeRebind?.Cancel();
            _activeRebind = null;
        }

        // -- Build ----------------------------------------------------------

        private void Build()
        {
            AddSectionHeader("Controls");

            TMP_Text sensValueLabel = null;
            Slider sensSlider = AddSliderRow(
                "Mouse sensitivity",
                _defaults.mouseSensitivityMin,
                _defaults.mouseSensitivityMax,
                _service.MouseSensitivity,
                ref sensValueLabel);
            sensSlider.onValueChanged.AddListener(v =>
            {
                _service.SetMouseSensitivity(v);
                if (sensValueLabel != null) sensValueLabel.text = v.ToString("F2");
            });

            AddToggleRow("Invert Y", _service.InvertY, v => _service.SetInvertY(v));

            AddSectionHeader("Audio");

            TMP_Text masterLabel = null;
            Slider masterSlider = AddSliderRow("Master", 0f, 1f, _service.MasterVolume, ref masterLabel);
            masterSlider.onValueChanged.AddListener(v =>
            {
                _service.SetMasterVolume(v);
                if (masterLabel != null) masterLabel.text = Mathf.RoundToInt(v * 100f) + "%";
            });

            TMP_Text musicLabel = null;
            Slider musicSlider = AddSliderRow("Music", 0f, 1f, _service.MusicVolume, ref musicLabel);
            musicSlider.onValueChanged.AddListener(v =>
            {
                _service.SetMusicVolume(v);
                if (musicLabel != null) musicLabel.text = Mathf.RoundToInt(v * 100f) + "%";
            });

            TMP_Text sfxLabel = null;
            Slider sfxSlider = AddSliderRow("SFX", 0f, 1f, _service.SfxVolume, ref sfxLabel);
            sfxSlider.onValueChanged.AddListener(v =>
            {
                _service.SetSfxVolume(v);
                if (sfxLabel != null) sfxLabel.text = Mathf.RoundToInt(v * 100f) + "%";
            });

            if (_playerInput != null)
            {
                AddSectionHeader("Key bindings");
                TryAddRebindRow("Interact", "Interact");
                TryAddRebindRow("Jump",     "Jump");
                TryAddRebindRow("Sprint",   "Sprint");
            }
        }

        // -- Row builders ---------------------------------------------------

        private void AddSectionHeader(string text)
        {
            TMP_Text label = UiFactory.CreateText("SectionHeader_" + text, _panel, _layer,
                13f, FontStyles.Bold, TextAlignmentOptions.Left);
            label.text  = text.ToUpper();
            label.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(label.gameObject, 28f);
        }

        private Slider AddSliderRow(string labelText, float min, float max, float value,
                                    ref TMP_Text valueLabelOut)
        {
            RectTransform row = CreateRow("Row_" + labelText);

            TMP_Text nameLabel = UiFactory.CreateText("Label", row, _layer, 15f, FontStyles.Normal,
                TextAlignmentOptions.Left);
            nameLabel.text = labelText;
            LayoutElement nameFlex = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameFlex.flexibleWidth = 1.2f;

            Slider slider = BuildSlider(row, min, max, value);
            LayoutElement sliderFlex = slider.gameObject.AddComponent<LayoutElement>();
            sliderFlex.flexibleWidth = 2f;
            sliderFlex.minHeight     = 24f;

            bool isPercent = (max == 1f && min == 0f);
            string initText = isPercent
                ? Mathf.RoundToInt(value * 100f) + "%"
                : value.ToString("F2");

            TMP_Text valueLabel = UiFactory.CreateText("Value", row, _layer, 14f, FontStyles.Normal,
                TextAlignmentOptions.Right);
            valueLabel.text  = initText;
            valueLabel.color = UiFactory.MutedText;
            LayoutElement valueFix = valueLabel.gameObject.AddComponent<LayoutElement>();
            valueFix.minWidth = 48f;
            valueFix.preferredWidth = 48f;

            valueLabelOut = valueLabel;
            UiFactory.AddLayoutHeight(row.gameObject, 36f);
            return slider;
        }

        private void AddToggleRow(string labelText, bool initialValue, Action<bool> onChange)
        {
            RectTransform row = CreateRow("Row_" + labelText);

            TMP_Text nameLabel = UiFactory.CreateText("Label", row, _layer, 15f, FontStyles.Normal,
                TextAlignmentOptions.Left);
            nameLabel.text = labelText;
            LayoutElement nameFlex = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameFlex.flexibleWidth = 1f;

            bool state = initialValue;
            Button btn = UiFactory.CreateButton("ToggleBtn", row, _layer,
                state ? "On" : "Off", null, 15f);
            LayoutElement btnLayout = btn.gameObject.AddComponent<LayoutElement>();
            btnLayout.minWidth = 80f;
            btnLayout.preferredWidth = 80f;

            btn.onClick.AddListener(() =>
            {
                state = !state;
                btn.GetComponentInChildren<TMP_Text>().text = state ? "On" : "Off";
                onChange(state);
            });

            UiFactory.AddLayoutHeight(row.gameObject, 36f);
        }

        private void TryAddRebindRow(string labelText, string actionName)
        {
            InputAction action = _playerInput.actions.FindAction(actionName);
            if (action == null) return;

            int bindingIdx = GetFirstKbBindingIndex(action);
            if (bindingIdx < 0) return;

            RectTransform row = CreateRow("Row_" + actionName);

            TMP_Text nameLabel = UiFactory.CreateText("Label", row, _layer, 15f, FontStyles.Normal,
                TextAlignmentOptions.Left);
            nameLabel.text = labelText;
            LayoutElement nameFlex = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameFlex.flexibleWidth = 1f;

            string initialDisplay = action.GetBindingDisplayString(bindingIdx);
            Button btn = UiFactory.CreateButton("RebindBtn", row, _layer,
                initialDisplay, null, 14f);
            LayoutElement btnLayout = btn.gameObject.AddComponent<LayoutElement>();
            btnLayout.minWidth = 100f;
            btnLayout.preferredWidth = 100f;

            TMP_Text btnLabel = btn.GetComponentInChildren<TMP_Text>();

            btn.onClick.AddListener(() => StartRebind(action, bindingIdx, btn, btnLabel));

            UiFactory.AddLayoutHeight(row.gameObject, 36f);
        }

        // -- Rebinding ------------------------------------------------------

        private void StartRebind(InputAction action, int bindingIdx, Button btn, TMP_Text label)
        {
            CancelActiveRebind();

            btn.interactable = false;
            label.text       = "...";

            _activeRebind = action.PerformInteractiveRebinding(bindingIdx)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    _service.SaveRebindsJson(_playerInput.actions.SaveBindingOverridesAsJson());
                    label.text       = action.GetBindingDisplayString(bindingIdx);
                    btn.interactable = true;
                    op.Dispose();
                    _activeRebind = null;
                })
                .OnCancel(op =>
                {
                    label.text       = action.GetBindingDisplayString(bindingIdx);
                    btn.interactable = true;
                    op.Dispose();
                    _activeRebind = null;
                })
                .Start();
        }

        // -- uGUI helpers ---------------------------------------------------

        private RectTransform CreateRow(string name)
        {
            RectTransform row = UiFactory.CreateRect(name, _panel, _layer);
            HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 8f;
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childControlHeight    = true;
            hlg.childControlWidth     = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            return row;
        }

        private Slider BuildSlider(RectTransform parent, float min, float max, float value)
        {
            RectTransform sliderRT = UiFactory.CreateRect("Slider", parent, _layer);

            RectTransform bg = UiFactory.CreateRect("Background", sliderRT, _layer);
            bg.anchorMin = new Vector2(0f, 0.25f);
            bg.anchorMax = new Vector2(1f, 0.75f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            UiFactory.AddImage(bg.gameObject, new Color(0.2f, 0.22f, 0.24f));

            RectTransform fillArea = UiFactory.CreateRect("Fill Area", sliderRT, _layer);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(5f,   0f);
            fillArea.offsetMax = new Vector2(-15f, 0f);

            RectTransform fill = UiFactory.CreateRect("Fill", fillArea, _layer);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            UiFactory.AddImage(fill.gameObject, UiFactory.ButtonBackground);

            RectTransform handleArea = UiFactory.CreateRect("Handle Slide Area", sliderRT, _layer);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f,  0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            RectTransform handle = UiFactory.CreateRect("Handle", handleArea, _layer);
            handle.sizeDelta = new Vector2(20f, 0f);
            Image handleImg = UiFactory.AddImage(handle.gameObject, new Color(0.88f, 0.92f, 0.96f));

            Slider slider = sliderRT.gameObject.AddComponent<Slider>();
            slider.fillRect    = fill;
            slider.handleRect  = handle;
            slider.targetGraphic = handleImg;
            slider.minValue    = min;
            slider.maxValue    = max;
            slider.value       = value;

            return slider;
        }

        private static int GetFirstKbBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite &&
                    b.groups.Contains("Keyboard&Mouse"))
                    return i;
            }
            return -1;
        }
    }
}
