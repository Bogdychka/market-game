using Market.Core;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Temporary interaction target for opening and closing the market while D2 uses stub props.
    /// </summary>
    public class DebugMarketOpenInteractable : MonoBehaviour, IInteractable
    {
        private MarketOpenSystem _marketOpenSystem;

        public string PromptText
        {
            get
            {
                if (_marketOpenSystem == null)
                    return "Market controls unavailable";

                return _marketOpenSystem.IsOpen ? "Close market" : "Open market";
            }
        }

        public bool CanInteract => _marketOpenSystem != null
                                   && (_marketOpenSystem.CanOpen || _marketOpenSystem.CanClose);

        private void Awake()
        {
            ResolveMarketOpenSystem();
        }

        public void Interact(GameObject actor)
        {
            ResolveMarketOpenSystem();
            if (_marketOpenSystem == null)
                return;

            bool changed = _marketOpenSystem.IsOpen
                ? _marketOpenSystem.TryClose()
                : _marketOpenSystem.TryOpen();

            if (!changed)
                Debug.Log("[DebugMarketOpen] Market state did not change.", this);
        }

        private void ResolveMarketOpenSystem()
        {
            ServiceLocator.TryGet<MarketOpenSystem>(out _marketOpenSystem);
        }
    }
}
