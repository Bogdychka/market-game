using Market.Core;
using Market.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Standalone settings panel builder. Placed on the SettingsPanel GameObject in the MainMenu
    /// scene (and anywhere else that needs a self-contained settings view).
    /// Builds a centered content panel with <see cref="SettingsPanelRenderer"/> on Awake.
    /// Optional player refs allow the rebind section; leave them null in MainMenu.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SettingsMenuController : MonoBehaviour
    {
        private const float PanelWidth    = 420f;
        private const float ButtonHeight  = 48f;
        private const float ButtonSpacing = 8f;

        [Header("Settings")]
        [SerializeField] private SettingsSO settingsSO;

        [Header("Player (optional -- leave null in MainMenu)")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private PlayerInput           playerInput;

        [Header("Back callback (optional -- wire in Inspector or leave null to omit the button)")]
        [SerializeField] private UnityEvent onBack;

        private SettingsPanelRenderer _renderer;

        private void Awake()
        {
            if (settingsSO == null)
            {
                Debug.LogError("[SettingsMenuController] settingsSO not assigned.", this);
                return;
            }

            SettingsService svc = ResolveSettingsService();
            if (svc == null) return;

            AddBackdrop();
            RectTransform panel = BuildContentPanel();
            AddTitle(panel);
            _renderer = new SettingsPanelRenderer(panel, gameObject.layer, svc, settingsSO, playerInput);
            if (onBack != null && onBack.GetPersistentEventCount() > 0)
                AddBackButton(panel);
        }

        private void OnDisable()
        {
            _renderer?.CancelActiveRebind();
        }

        // -- Construction ---------------------------------------------------

        private void AddBackdrop()
        {
            Image img = gameObject.GetComponent<Image>();
            if (img == null)
                img = UiFactory.AddImage(gameObject, new Color(0f, 0f, 0f, 0.55f));
            img.raycastTarget = true;
        }

        private RectTransform BuildContentPanel()
        {
            RectTransform panel = UiFactory.CreateRect("SettingsContent", transform, gameObject.layer);
            panel.anchorMin      = new Vector2(0.5f, 0.5f);
            panel.anchorMax      = new Vector2(0.5f, 0.5f);
            panel.pivot          = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta      = new Vector2(PanelWidth, 0f);

            UiFactory.AddImage(panel.gameObject, UiFactory.PanelBackground);

            VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding              = new RectOffset(24, 24, 24, 24);
            vlg.spacing              = ButtonSpacing;
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlHeight   = true;
            vlg.childControlWidth    = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;

            ContentSizeFitter csf = panel.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panel;
        }

        private void AddTitle(RectTransform parent)
        {
            TMP_Text title = UiFactory.CreateText("Title", parent, gameObject.layer,
                26f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "Settings";
            UiFactory.AddLayoutHeight(title.gameObject, 48f);
        }

        private void AddBackButton(RectTransform parent)
        {
            Button btn = UiFactory.CreateButton("BackButton", parent, gameObject.layer,
                "Back", () => onBack.Invoke(), 17f);
            UiFactory.AddLayoutHeight(btn.gameObject, ButtonHeight);
        }

        // -- Service --------------------------------------------------------

        private SettingsService ResolveSettingsService()
        {
            if (ServiceLocator.TryGet<SettingsService>(out SettingsService svc)) return svc;

            svc = new SettingsService(settingsSO);
            ServiceLocator.Register(svc);
            Debug.LogWarning("[SettingsMenuController] SettingsService not found. Created local instance.", this);
            return svc;
        }
    }
}
