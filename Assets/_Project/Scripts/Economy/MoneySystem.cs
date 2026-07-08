using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Market.Economy
{
    /// <summary>
    /// Player money. Fires OnChanged(newAmount) on every change.
    /// </summary>
    public class MoneySystem : MonoBehaviour
    {
        [FormerlySerializedAs("startAmount")]
        [SerializeField] private int startCoins = 200;

        public event Action<int> OnChanged;

        private int _coins;
        public int Amount => _coins;
        public int Coins => _coins;

        private void Awake() => _coins = Mathf.Max(0, startCoins);

        /// <summary>Add money. Ignores non-positive values.</summary>
        public void Add(int coins)
        {
            if (coins <= 0) return;
            _coins += coins;
            OnChanged?.Invoke(_coins);
        }

        /// <summary>Add money from a legacy/price value, rounded to whole coins.</summary>
        public void Add(float value) => Add(ToCoins(value));

        /// <summary>Attempt to spend money. Returns false if insufficient funds.</summary>
        public bool TrySpend(int coins)
        {
            if (coins <= 0 || _coins < coins) return false;
            _coins -= coins;
            OnChanged?.Invoke(_coins);
            return true;
        }

        /// <summary>Attempt to spend a legacy/price value, rounded to whole coins.</summary>
        public bool TrySpend(float value) => TrySpend(ToCoins(value));

        public bool CanAfford(int coins) => _coins >= coins;
        public bool CanAfford(float value) => CanAfford(ToCoins(value));

        /// <summary>Set amount directly (SaveSystem only). Clamped to >= 0.</summary>
        public void SetAmount(int coins)
        {
            _coins = Mathf.Max(0, coins);
            OnChanged?.Invoke(_coins);
        }

        /// <summary>Set amount from a legacy save value, rounded to whole coins.</summary>
        public void SetAmount(float value) => SetAmount(ToCoins(value));

        public static int ToCoins(float value)
        {
            return value > 0f ? Mathf.FloorToInt(value + 0.5f) : 0;
        }
    }
}
