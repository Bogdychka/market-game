using Market.Core;
using Market.World;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Отображает игровое время и сезон в HUD.
    /// Обновляется только при смене минуты — экономит на string allocation.
    /// </summary>
    public class TimeHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeLabel;

        private TimeSystem    _timeSystem;
        private SeasonManager _seasonManager;

        private int    _lastMinute = -1;
        private int    _lastHour   = -1;
        private int    _lastDay    = -1;
        private Season _lastSeason = (Season)(-1);

        private void Awake()
        {
            ValidateReferences();
            ResolveTimeSystem();
        }

        private void Start()
        {
            // SeasonManager может не существовать (опционален)
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
            Refresh();
        }

        private void Update()
        {
            if (_timeSystem == null) return;

            Season curSeason = _seasonManager?.CurrentSeason ?? (Season)(-1);

            if (_timeSystem.Minute == _lastMinute
                && _timeSystem.Hour == _lastHour
                && _timeSystem.Day  == _lastDay
                && curSeason        == _lastSeason) return;

            Refresh();
        }

        private void Refresh()
        {
            if (_timeSystem == null || timeLabel == null) return;

            _lastMinute = _timeSystem.Minute;
            _lastHour   = _timeSystem.Hour;
            _lastDay    = _timeSystem.Day;
            _lastSeason = _seasonManager?.CurrentSeason ?? (Season)(-1);

            string seasonStr = _seasonManager != null
                ? $"  {SeasonManager.GetName(_seasonManager.CurrentSeason)}"
                : "";

            timeLabel.text = _timeSystem.FormatTime() + seasonStr;
        }

        private void ResolveTimeSystem()
        {
            if (ServiceLocator.TryGet<TimeSystem>(out _timeSystem)) return;

            Debug.LogWarning("[TimeHUD] TimeSystem не найден — HUD времени будет пустым.", this);
        }

        private void ValidateReferences()
        {
            if (timeLabel == null)
                Debug.LogError("[TimeHUD] timeLabel не назначен", this);
        }
    }
}
