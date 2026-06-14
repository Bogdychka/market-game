using System;
using System.Collections.Generic;
using Market.Core;
using Market.Interaction;
using Market.Player;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Central coordinator for switching between gameplay and UI modes.
    /// It owns cursor lock/visibility and suppresses player input while panels are open.
    /// </summary>
    public class UIModeService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private InteractionSystem interactionSystem;

        [Header("Settings")]
        [Tooltip("Use for menu-only scenes where the cursor should start visible.")]
        [SerializeField] private bool startInMenuMode;
        [Tooltip("When false, the service only toggles gameplay input and leaves cursor state unchanged.")]
        [SerializeField] private bool controlCursor = true;
        [Tooltip("Lock and hide the cursor whenever no UI panel is active.")]
        [SerializeField] private bool lockCursorInGameMode = true;

        private readonly HashSet<object> _menuOwners = new();
        private bool _persistentMenuMode;
        private bool _lastAppliedMenuMode;
        private int _lastCloseRequestFrame = -1;

        public event Action<bool> ModeChanged;
        public event Action CloseRequested;

        /// <summary>True while any UI owner has requested menu mode.</summary>
        public bool IsMenuMode => _persistentMenuMode || _menuOwners.Count > 0;

        /// <summary>True if Escape was already consumed by UI this frame.</summary>
        public bool WasCloseRequestConsumedThisFrame => _lastCloseRequestFrame == Time.frameCount;

        private void Awake()
        {
            _persistentMenuMode = startInMenuMode;
            ServiceLocator.Register(this);
            ApplyMode(force: true);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UIModeService>(out UIModeService current) && ReferenceEquals(current, this))
                ServiceLocator.Unregister<UIModeService>();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyMode(force: true);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
                ApplyMode(force: true);
        }

        /// <summary>For scene-level menus that should keep UI mode active without a panel owner.</summary>
        public void SetPersistentMenuMode(bool enabled)
        {
            if (_persistentMenuMode == enabled) return;

            _persistentMenuMode = enabled;
            ApplyMode(force: false);
        }

        /// <summary>Requests UI mode for a panel owner. Duplicate requests by the same owner are ignored.</summary>
        public void EnterMenuMode(object owner)
        {
            if (owner == null)
            {
                Debug.LogWarning("[UIModeService] EnterMenuMode called with null owner.", this);
                return;
            }

            if (_menuOwners.Add(owner))
                ApplyMode(force: false);
        }

        /// <summary>Releases UI mode for a panel owner.</summary>
        public void ExitMenuMode(object owner)
        {
            if (owner == null) return;

            if (_menuOwners.Remove(owner))
                ApplyMode(force: false);
        }

        /// <summary>Consumes an Escape/back request for the active UI panel, if one exists.</summary>
        public bool TryConsumeCloseRequest()
        {
            if (!IsMenuMode) return false;

            _lastCloseRequestFrame = Time.frameCount;
            CloseRequested?.Invoke();
            return true;
        }

        private void ApplyMode(bool force)
        {
            bool menuMode = IsMenuMode;
            if (!force && menuMode == _lastAppliedMenuMode) return;

            SetGameplayInputEnabled(!menuMode);
            ApplyCursor(menuMode);
            _lastAppliedMenuMode = menuMode;
            ModeChanged?.Invoke(menuMode);
        }

        private void SetGameplayInputEnabled(bool enabled)
        {
            if (playerController != null)
                playerController.enabled = enabled;

            if (interactionSystem != null)
                interactionSystem.enabled = enabled;
        }

        private void ApplyCursor(bool menuMode)
        {
            if (!controlCursor) return;

            if (menuMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = lockCursorInGameMode ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursorInGameMode;
        }
    }
}
