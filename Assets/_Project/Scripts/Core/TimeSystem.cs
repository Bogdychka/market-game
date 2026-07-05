using System;
using UnityEngine;

namespace Market.Core
{
    /// <summary>
    /// Game clock. Registered in ServiceLocator, ticked from GameBootstrap.Update().
    /// 1 real second = minutesPerRealSecond game minutes (default 2 ~ 12 min/day).
    /// </summary>
    public class TimeSystem
    {
        private const float MinutesPerHour = 60f;
        private const float HoursPerDay    = 24f;

        public event Action<int> OnHourChanged;
        public event Action<int> OnDayChanged;

        private readonly float _minutesPerRealSecond;
        private float _accumulatedMinutes;
        private bool  _paused;
        private bool  _waitingForSleep;

        public int   Hour    { get; private set; } = 8;
        public int   Day     { get; private set; } = 1;
        public int   Minute  => Mathf.FloorToInt(_accumulatedMinutes);
        public float TimeScale { get; set; } = 1f;
        public bool  IsWaitingForSleep => _waitingForSleep;

        public TimeSystem(float minutesPerRealSecond = 2f)
        {
            _minutesPerRealSecond = minutesPerRealSecond;
        }

        public void Tick(float deltaTime)
        {
            if (_paused || _waitingForSleep) return;

            _accumulatedMinutes += deltaTime * _minutesPerRealSecond * TimeScale;

            while (_accumulatedMinutes >= MinutesPerHour)
            {
                _accumulatedMinutes -= MinutesPerHour;
                AdvanceHour();
            }
        }

        public void Pause()  => _paused = true;
        public void Resume() => _paused = false;
        public bool IsPaused => _paused;

        /// <summary>Reset to initial state (called on New Game).</summary>
        public void Reset()
        {
            Hour = 8;
            Day  = 1;
            _accumulatedMinutes = 0f;
            TimeScale = 1f;
            _paused   = false;
            _waitingForSleep = false;
            OnDayChanged?.Invoke(Day);
            OnHourChanged?.Invoke(Hour);
        }

        /// <summary>
        /// Set time directly (used when loading a save).
        /// Fires OnDayChanged and OnHourChanged so dependent systems (season, lighting) re-sync.
        /// </summary>
        public void SetTime(int day, int hour, int minute)
        {
            Day    = Mathf.Max(1, day);
            Hour   = Mathf.Clamp(hour, 0, 23);
            _accumulatedMinutes = Mathf.Clamp(minute, 0, 59);
            _waitingForSleep = Hour == 0 && Minute == 0;

            OnDayChanged?.Invoke(Day);
            OnHourChanged?.Invoke(Hour);
        }

        /// <summary>Advance from midnight to the next morning. Used by bed/sleep interactions.</summary>
        public bool SleepToNextDay()
        {
            if (!_waitingForSleep)
                return false;

            Day++;
            Hour = 8;
            _accumulatedMinutes = 0f;
            _waitingForSleep = false;
            _paused = false;

            OnDayChanged?.Invoke(Day);
            OnHourChanged?.Invoke(Hour);
            Debug.Log($"[TimeSystem] Slept until day: {Day}");
            return true;
        }

        /// <summary>Instantly skip N game hours (debug use).</summary>
        public void SkipHours(int hours)
        {
            for (int i = 0; i < hours && !_waitingForSleep; i++)
                AdvanceHour();
        }

        public string FormatTime() => $"Day {Day}  {Hour:00}:{Minute:00}";

        private void AdvanceHour()
        {
            if (Hour >= (int)HoursPerDay - 1)
            {
                Hour = 0;
                _accumulatedMinutes = 0f;
                _waitingForSleep = true;
                OnHourChanged?.Invoke(Hour);
                Debug.Log("[TimeSystem] Midnight reached. Waiting for sleep.");
                return;
            }

            Hour++;
            OnHourChanged?.Invoke(Hour);
        }
    }
}
