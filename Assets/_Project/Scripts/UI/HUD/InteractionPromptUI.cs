using Market.Core;
using Market.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.UI
{
    /// <summary>
    /// Interaction prompt HUD. Shows the current Interact binding for the active control scheme.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractionPromptUI : MonoBehaviour
    {
        private const string DefaultActionName = "Interact";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";
        private const string GamepadGroup = "Gamepad";

        [Header("References")]
        [SerializeField] private InteractionSystem source;
        [SerializeField] private TMP_Text label;
        [SerializeField] private PlayerInput playerInput;

        [Header("Display")]
        [Tooltip("Input action shown in the prompt. Defaults to Interact when empty.")]
        [SerializeField] private string actionName = DefaultActionName;
        [SerializeField] private string fallbackKeyHint = "E";
        [SerializeField] private string fallbackText = "Interact";
        [Tooltip("How often (seconds) to re-read the current target's prompt text, so a target whose "
               + "PromptText changes over time (e.g. a crop growing to Ready) updates the label.")]
        [SerializeField] private float refreshInterval = 0.25f;

        private CanvasGroup _group;
        private IInteractable _currentTarget;
        private PlayerInput _subscribedPlayerInput;
        private InputAction _action;
        private float _nextRefreshTime;
        private string _lastLabelText;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            ResolveReferences();
            ResolveAction();
            SetVisible(false);

            if (source == null) Debug.LogError("[InteractionPromptUI] source not assigned.", this);
            if (label == null) Debug.LogError("[InteractionPromptUI] label not assigned.", this);
        }

        private void OnEnable()
        {
            if (source != null) source.CurrentChanged += OnCurrentChanged;
            SubscribePlayerInput();
            Refresh();
        }

        private void OnDisable()
        {
            if (source != null) source.CurrentChanged -= OnCurrentChanged;
            UnsubscribePlayerInput();
        }

        private void Update()
        {
            // The target can change its own prompt text over time (e.g. a crop plot going
            // Growing -> Ready), so poll it at a low rate while a target is set.
            if (_currentTarget == null) return;
            if (Time.unscaledTime < _nextRefreshTime) return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            Refresh();
        }

        private void OnCurrentChanged(IInteractable target)
        {
            _currentTarget = target;
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            Refresh();
        }

        private void OnControlsChanged(PlayerInput input)
        {
            ResolveAction();
            Refresh();
        }

        private void Refresh()
        {
            if (label == null)
                return;

            if (_currentTarget == null)
            {
                _lastLabelText = null;
                SetVisible(false);
                return;
            }

            string promptText = string.IsNullOrEmpty(_currentTarget.PromptText) ? fallbackText : _currentTarget.PromptText;
            string newText = $"[{GetKeyHint()}] {promptText}";
            if (newText != _lastLabelText)
            {
                label.text = _lastLabelText = newText;
            }
            SetVisible(true);
        }

        private void ResolveReferences()
        {
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);

            if (playerInput == null && source != null)
                playerInput = source.PlayerInput;
        }

        private void ResolveAction()
        {
            if (source != null && source.InteractAction != null)
            {
                _action = source.InteractAction;
                return;
            }

            if (playerInput == null || playerInput.actions == null)
            {
                _action = null;
                return;
            }

            string resolvedActionName = string.IsNullOrEmpty(actionName) ? DefaultActionName : actionName;
            _action = playerInput.actions.FindAction(resolvedActionName, throwIfNotFound: false);
        }

        private void SubscribePlayerInput()
        {
            if (playerInput == null || ReferenceEquals(_subscribedPlayerInput, playerInput))
                return;

            UnsubscribePlayerInput();
            _subscribedPlayerInput = playerInput;
            _subscribedPlayerInput.onControlsChanged += OnControlsChanged;
        }

        private void UnsubscribePlayerInput()
        {
            if (_subscribedPlayerInput == null)
                return;

            _subscribedPlayerInput.onControlsChanged -= OnControlsChanged;
            _subscribedPlayerInput = null;
        }

        private string GetKeyHint()
        {
            if (_action == null)
                return fallbackKeyHint;

            string controlScheme = playerInput != null ? playerInput.currentControlScheme : null;
            int bindingIndex = FindBindingIndex(_action, controlScheme);
            if (bindingIndex < 0)
                return fallbackKeyHint;

            string display = _action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);

            return string.IsNullOrEmpty(display) ? fallbackKeyHint : display;
        }

        private static int FindBindingIndex(InputAction action, string controlScheme)
        {
            if (!string.IsNullOrEmpty(controlScheme))
            {
                int schemeMatch = FindBindingIndexForGroup(action, controlScheme);
                if (schemeMatch >= 0)
                    return schemeMatch;
            }

            int keyboardMatch = FindBindingIndexForGroup(action, KeyboardMouseGroup);
            if (keyboardMatch >= 0)
                return keyboardMatch;

            int gamepadMatch = FindBindingIndexForGroup(action, GamepadGroup);
            return gamepadMatch >= 0 ? gamepadMatch : FindFirstButtonBinding(action);
        }

        private static int FindBindingIndexForGroup(InputAction action, string group)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (IsPromptBinding(binding) && BindingMatchesGroup(binding, group))
                    return i;
            }

            return -1;
        }

        private static int FindFirstButtonBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (IsPromptBinding(action.bindings[i]))
                    return i;
            }

            return -1;
        }

        private static bool IsPromptBinding(InputBinding binding)
        {
            return !binding.isComposite && !binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath);
        }

        private static bool BindingMatchesGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups))
                return false;

            return binding.groups.Contains(group);
        }

        private void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }
}
