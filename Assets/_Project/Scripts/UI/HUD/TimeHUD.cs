using Market.Core;
using Market.World;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Displays game time and season in the HUD.
    /// Updates only when the minute changes to save string allocations.
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
            // SeasonManager is optional — may not exist
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

            Debug.LogWarning("[TimeHUD] TimeSystem not found — time HUD will be blank.", this);
        }

        private void ValidateReferences()
        {
            if (timeLabel == null)
                Debug.LogError("[TimeHUD] timeLabel not assigned", this);
        }
    }
}
