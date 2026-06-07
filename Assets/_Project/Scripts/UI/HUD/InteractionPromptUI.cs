using Market.Core;
using Market.Interaction;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Interaction prompt HUD. Subscribes to InteractionSystem.CurrentChanged
    /// and shows/hides text of the form "[E] action".
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionSystem source;
        [SerializeField] private TMP_Text label;

        [Header("Display")]
        [SerializeField] private string keyHint = "E";
        [SerializeField] private string fallbackText = "Взаимодействовать";

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            SetVisible(false);

            if (source == null) Debug.LogError("[InteractionPromptUI] source not assigned", this);
            if (label  == null) Debug.LogError("[InteractionPromptUI] label not assigned",  this);
        }

        private void OnEnable()
        {
            if (source != null) source.CurrentChanged += OnCurrentChanged;
        }

        private void OnDisable()
        {
            if (source != null) source.CurrentChanged -= OnCurrentChanged;
        }

        private void OnCurrentChanged(IInteractable target)
        {
            if (target == null)
            {
                SetVisible(false);
                return;
            }

            string text = string.IsNullOrEmpty(target.PromptText) ? fallbackText : target.PromptText;
            label.text = $"[{keyHint}] {text}";
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
            // Prompt is purely visual — never blocks raycasts
            _group.blocksRaycasts = false;
            _group.interactable   = false;
        }
    }
}
