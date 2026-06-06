using System;
using UnityEngine;

namespace Market.Core
{
    /// <summary>
    /// Игровые часы. Регистрируется в ServiceLocator, тикается из GameBootstrap.Update().
    /// 1 реальная секунда = minutesPerRealSecond игровых минут (по умолчанию 2 = ~12 мин/день).
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

        public int   Hour    { get; private set; } = 8;
        public int   Day     { get; private set; } = 1;
        public int   Minute  => Mathf.FloorToInt(_accumulatedMinutes);
        public float TimeScale { get; set; } = 1f;

        public TimeSystem(float minutesPerRealSecond = 2f)
        {
            _minutesPerRealSecond = minutesPerRealSecond;
        }

        public void Tick(float deltaTime)
        {
            if (_paused) return;

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

        /// <summary>Сброс к начальному состоянию (вызывается при "Новая игра").</summary>
        public void Reset()
        {
            Hour = 8;
            Day  = 1;
            _accumulatedMinutes = 0f;
            TimeScale = 1f;
            _paused   = false;
        }

        /// <summary>
        /// Устанавливает время напрямую (используется при загрузке сейва).
        /// Эмитит OnDayChanged и OnHourChanged, чтобы зависимые системы (сезон, освещение) синхронизировались.
        /// </summary>
        public void SetTime(int day, int hour, int minute)
        {
            Day    = Mathf.Max(1, day);
            Hour   = Mathf.Clamp(hour, 0, 23);
            _accumulatedMinutes = Mathf.Clamp(minute, 0, 59);

            OnDayChanged?.Invoke(Day);
            OnHourChanged?.Invoke(Hour);
        }

        /// <summary>Мгновенно перемотать на N игровых часов (для дебага).</summary>
        public void SkipHours(int hours)
        {
            for (int i = 0; i < hours; i++)
                AdvanceHour();
        }

        public string FormatTime() => $"День {Day}  {Hour:00}:{Minute:00}";

        private void AdvanceHour()
        {
            Hour = (Hour + 1) % (int)HoursPerDay;
            if (Hour == 0)
            {
                Day++;
                OnDayChanged?.Invoke(Day);
                Debug.Log($"[TimeSystem] Новый день: {Day}");
            }
            OnHourChanged?.Invoke(Hour);
        }
    }
}
