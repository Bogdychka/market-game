using Market.Core;
using Market.Interaction;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Подсказка взаимодействия. Подписывается на InteractionSystem.CurrentChanged
    /// и показывает/скрывает текст вида "[E] действие".
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

            if (source == null) Debug.LogError("[InteractionPromptUI] source не назначен", this);
            if (label  == null) Debug.LogError("[InteractionPromptUI] label не назначен",  this);
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
            // Подсказка — чисто визуальная, рейкасты не блокирует никогда
            _group.blocksRaycasts = false;
            _group.interactable   = false;
        }
    }
}
