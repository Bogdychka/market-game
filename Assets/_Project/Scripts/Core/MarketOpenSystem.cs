using System;
using Market.Core.Events;

namespace Market.Core
{
    /// <summary>
    /// Tracks whether the player has opened the market for customer purchases.
    /// </summary>
    public class MarketOpenSystem : IDisposable
    {
        private readonly DayPhaseSystem _dayPhaseSystem;
        private readonly EventBus _eventBus;

        public event Action<bool> OnOpenChanged;

        public bool IsOpen { get; private set; }

        public MarketOpenSystem(DayPhaseSystem dayPhaseSystem = null, EventBus eventBus = null)
        {
            _dayPhaseSystem = dayPhaseSystem;
            _eventBus = eventBus;

            if (_dayPhaseSystem != null)
                _dayPhaseSystem.OnPhaseChanged += HandlePhaseChanged;
        }

        public bool CanOpen => !IsOpen
                               && _dayPhaseSystem != null
                               && (_dayPhaseSystem.Phase == DayPhase.MorningPrep
                                   || _dayPhaseSystem.Phase == DayPhase.MarketOpen);

        public bool CanClose => IsOpen;

        public bool TryOpen()
        {
            if (!CanOpen)
                return false;

            SetOpen(true);
            return true;
        }

        public bool TryClose()
        {
            if (!CanClose)
                return false;

            SetOpen(false);
            return true;
        }

        public void Dispose()
        {
            if (_dayPhaseSystem != null)
                _dayPhaseSystem.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.MorningPrep || phase == DayPhase.NightNextDay)
                SetOpen(false);
        }

        private void SetOpen(bool isOpen)
        {
            if (IsOpen == isOpen)
                return;

            IsOpen = isOpen;
            OnOpenChanged?.Invoke(IsOpen);
            _eventBus?.Publish(new MarketOpenChangedEvent(IsOpen));
        }
    }
}
