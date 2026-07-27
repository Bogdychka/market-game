using Market.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug fly mode for the player.
    /// F4        -- toggle fly
    /// Space     -- ascend, Left Ctrl -- descend (while flying)
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class DebugFlyMode : MonoBehaviour
    {
        private FirstPersonController _controller;
        private bool _flying;

        private void Awake()
        {
            _controller = GetComponent<FirstPersonController>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f4Key.wasPressedThisFrame)
                ToggleFly();

            if (!_flying) return;

            float vertical = 0f;
            if (kb.spaceKey.isPressed) vertical += 1f;
            if (kb.leftCtrlKey.isPressed) vertical -= 1f;
            _controller.SetFlyVerticalInput(vertical);
        }

        private void ToggleFly()
        {
            _flying = !_flying;
            _controller.SetFlyMode(_flying);
            Debug.Log($"[Fly] {(_flying ? "Enabled" : "Disabled")}");
        }
    }
}
