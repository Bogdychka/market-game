using System;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Деньги игрока. Эмитит OnChanged(newAmount) при любом изменении.
    /// </summary>
    public class MoneySystem : MonoBehaviour
    {
        [SerializeField] private float startAmount = 200f;

        public event Action<float> OnChanged;

        private float _amount;
        public float Amount => _amount;

        private void Awake() => _amount = startAmount;

        /// <summary>Добавить деньги. Игнорирует отрицательные значения.</summary>
        public void Add(float value)
        {
            if (value <= 0f) return;
            _amount += value;
            OnChanged?.Invoke(_amount);
        }

        /// <summary>Пытается снять деньги. Возвращает false если не хватает.</summary>
        public bool TrySpend(float value)
        {
            if (value <= 0f || _amount < value) return false;
            _amount -= value;
            OnChanged?.Invoke(_amount);
            return true;
        }

        public bool CanAfford(float value) => _amount >= value;

        /// <summary>Напрямую устанавливает сумму (только для SaveSystem). Кламп >= 0.</summary>
        public void SetAmount(float value)
        {
            _amount = Mathf.Max(0f, value);
            OnChanged?.Invoke(_amount);
        }
    }
}
