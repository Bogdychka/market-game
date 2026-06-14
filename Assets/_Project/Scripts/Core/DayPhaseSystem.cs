using System;
using Market.Core.Events;

namespace Market.Core
{
    /// <summary>
    /// Derives the current day phase from game time and notifies dependents when it changes.
    /// </summary>
    public class DayPhaseSystem : IDisposable
    {
        public const int MorningPrepStartHour = 8;
        public const int MarketOpenStartHour = 9;
        public const int EveningSummaryStartHour = 18;
        public const int NightStartHour = 21;

        private readonly TimeSystem _timeSystem;
        private readonly EventBus _eventBus;

        public event Action<DayPhase> OnPhaseChanged;

        public DayPhase Phase { get; private set; }

        public DayPhaseSystem(TimeSystem timeSystem, EventBus eventBus = null)
        {
            _timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            _eventBus = eventBus;
            Phase = GetPhaseForHour(_timeSystem.Hour);
            _timeSystem.OnHourChanged += HandleHourChanged;
            _timeSystem.OnDayChanged += HandleDayChanged;
        }

        public static DayPhase GetPhaseForHour(int hour)
        {
            int wrappedHour = ((hour % 24) + 24) % 24;

            if (wrappedHour >= NightStartHour || wrappedHour < MorningPrepStartHour)
                return DayPhase.NightNextDay;

            if (wrappedHour >= EveningSummaryStartHour)
                return DayPhase.EveningSummary;

            if (wrappedHour >= MarketOpenStartHour)
                return DayPhase.MarketOpen;

            return DayPhase.MorningPrep;
        }

        public static string GetDisplayName(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.MorningPrep => "Morning Prep",
                DayPhase.MarketOpen => "Market Open",
                DayPhase.EveningSummary => "Evening Summary",
                DayPhase.NightNextDay => "Night / Next Day",
                _ => phase.ToString()
            };
        }

        public void Refresh()
        {
            SetPhase(GetPhaseForHour(_timeSystem.Hour));
        }

        public void Dispose()
        {
            _timeSystem.OnHourChanged -= HandleHourChanged;
            _timeSystem.OnDayChanged -= HandleDayChanged;
        }

        private void HandleHourChanged(int hour)
        {
            SetPhase(GetPhaseForHour(hour));
        }

        private void HandleDayChanged(int day)
        {
            Refresh();
        }

        private void SetPhase(DayPhase phase)
        {
            if (Phase == phase) return;

            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
            _eventBus?.Publish(new DayPhaseChangedEvent(phase));
        }
    }
}
