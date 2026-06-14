using Market.Core;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Temporary bed stub: interact at midnight to advance to the next morning.
    /// </summary>
    public class DebugSleepInteractable : MonoBehaviour, IInteractable
    {
        private TimeSystem _timeSystem;
        private MarketOpenSystem _marketOpenSystem;

        public string PromptText => "Sleep until morning";
        public bool CanInteract => _timeSystem != null && _timeSystem.IsWaitingForSleep;

        private void Awake()
        {
            ResolveServices();
        }

        public void Interact(GameObject actor)
        {
            ResolveServices();
            if (_timeSystem == null || !_timeSystem.IsWaitingForSleep)
                return;

            _marketOpenSystem?.TryClose();
            _timeSystem.SleepToNextDay();
        }

        private void ResolveServices()
        {
            ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
            ServiceLocator.TryGet<MarketOpenSystem>(out _marketOpenSystem);
        }
    }
}
