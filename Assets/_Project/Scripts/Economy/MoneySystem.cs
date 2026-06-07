using System;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Player money. Fires OnChanged(newAmount) on every change.
    /// </summary>
    public class MoneySystem : MonoBehaviour
    {
        [SerializeField] private float startAmount = 200f;

        public event Action<float> OnChanged;

        private float _amount;
        public float Amount => _amount;

        private void Awake() => _amount = startAmount;

        /// <summary>Add money. Ignores non-positive values.</summary>
        public void Add(float value)
        {
            if (value <= 0f) return;
            _amount += value;
            OnChanged?.Invoke(_amount);
        }

        /// <summary>Attempt to spend money. Returns false if insufficient funds.</summary>
        public bool TrySpend(float value)
        {
            if (value <= 0f || _amount < value) return false;
            _amount -= value;
            OnChanged?.Invoke(_amount);
            return true;
        }

        public bool CanAfford(float value) => _amount >= value;

        /// <summary>Set amount directly (SaveSystem only). Clamped to >= 0.</summary>
        public void SetAmount(float value)
        {
            _amount = Mathf.Max(0f, value);
            OnChanged?.Invoke(_amount);
        }
    }
}
