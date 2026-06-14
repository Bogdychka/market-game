using System.Collections.Generic;
using Market.Core;
using UnityEngine;

namespace Market.Market
{
    /// <summary>
    /// Scene coordinator that owns the market stalls available to NPCs, UI, and save/load.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class MarketStallRegistry : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional explicit stall list. Child MarketStall components are also collected in Awake.")]
        [SerializeField] private MarketStall[] stalls;

        private readonly List<MarketStall> _stalls = new();

        /// <summary>Registered stalls in deterministic hierarchy order.</summary>
        public IReadOnlyList<MarketStall> Stalls => _stalls;

        /// <summary>Number of valid stalls registered in this scene.</summary>
        public int Count => _stalls.Count;

        private void Awake()
        {
            ResolveStalls();
            ServiceLocator.Register(this);
            ValidateReferences();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<MarketStallRegistry>(out MarketStallRegistry current) &&
                ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<MarketStallRegistry>();
            }
        }

        /// <summary>Returns a random registered stall, or null when none are available.</summary>
        public MarketStall GetRandomStall()
        {
            if (_stalls.Count == 0)
                return null;

            return _stalls[Random.Range(0, _stalls.Count)];
        }

        /// <summary>Returns the first registered stall, or null when none are available.</summary>
        public MarketStall GetFirstStall()
        {
            return _stalls.Count > 0 ? _stalls[0] : null;
        }

        /// <summary>Finds a stall by stable id.</summary>
        public bool TryGetStall(string stallId, out MarketStall stall)
        {
            stall = null;
            if (string.IsNullOrWhiteSpace(stallId))
                return false;

            foreach (MarketStall candidate in _stalls)
            {
                if (candidate == null) continue;
                if (candidate.StallId == stallId)
                {
                    stall = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ResolveStalls()
        {
            _stalls.Clear();

            if (stalls != null)
            {
                foreach (MarketStall stall in stalls)
                    AddIfValid(stall);
            }

            MarketStall[] childStalls = GetComponentsInChildren<MarketStall>(includeInactive: true);
            foreach (MarketStall stall in childStalls)
                AddIfValid(stall);
        }

        private void AddIfValid(MarketStall stall)
        {
            if (stall == null || _stalls.Contains(stall))
                return;

            _stalls.Add(stall);
        }

        private void ValidateReferences()
        {
            if (_stalls.Count == 0)
            {
                Debug.LogError("[MarketStallRegistry] No MarketStall components registered.", this);
                return;
            }

            for (int i = 0; i < _stalls.Count; i++)
            {
                MarketStall stall = _stalls[i];
                if (stall == null) continue;

                for (int j = i + 1; j < _stalls.Count; j++)
                {
                    MarketStall other = _stalls[j];
                    if (other != null && stall.StallId == other.StallId)
                        Debug.LogError($"[MarketStallRegistry] Duplicate stall id: {stall.StallId}", this);
                }
            }
        }
    }
}
