using Market.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug-управление временем.
    /// Page Up   — ускорить время (×2)
    /// Page Down — замедлить время (÷2)
    /// H         — пропустить 1 час
    /// </summary>
    public class DebugTimeControl : MonoBehaviour
    {
        private TimeSystem _timeSystem;

        private void Awake()
        {
            ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
        }

        private void Update()
        {
            if (_timeSystem == null) return;

            var kb = Keyboard.current;

            if (kb.pageUpKey.wasPressedThisFrame)
            {
                _timeSystem.TimeScale *= 2f;
                Debug.Log($"[Time] Скорость ×{_timeSystem.TimeScale}");
            }

            if (kb.pageDownKey.wasPressedThisFrame)
            {
                _timeSystem.TimeScale = Mathf.Max(0.25f, _timeSystem.TimeScale / 2f);
                Debug.Log($"[Time] Скорость ×{_timeSystem.TimeScale}");
            }

            if (kb.hKey.wasPressedThisFrame)
            {
                _timeSystem.SkipHours(1);
                Debug.Log($"[Time] {_timeSystem.FormatTime()}");
            }
        }
    }
}
