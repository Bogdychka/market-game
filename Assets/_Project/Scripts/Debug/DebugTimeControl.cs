using Market.Core;
using Market.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug-управление временем.
    /// Page Up   — ускорить время (×2)
    /// Page Down — замедлить время (÷2)
    /// H         — пропустить 1 час
    /// N         — пропустить до следующего сезона
    /// </summary>
    public class DebugTimeControl : MonoBehaviour
    {
        private TimeSystem _timeSystem;
        private SeasonManager _seasonManager;

        private void Awake()
        {
            ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
        }

        private void Start()
        {
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
        }

        private void Update()
        {
            if (_timeSystem == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

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

            if (kb.nKey.wasPressedThisFrame)
            {
                SkipToNextSeason();
            }
        }

        private void SkipToNextSeason()
        {
            if (_seasonManager == null)
                ServiceLocator.TryGet<SeasonManager>(out _seasonManager);

            if (_seasonManager == null)
            {
                Debug.LogWarning("[Time] SeasonManager не найден — пропуск сезона невозможен.", this);
                return;
            }

            int hours = Mathf.Max(1, _seasonManager.DaysUntilNextSeason) * 24;
            _timeSystem.SkipHours(hours);
            Debug.Log($"[Time] Пропущено до сезона: {SeasonManager.GetName(_seasonManager.CurrentSeason)}");
        }
    }
}
