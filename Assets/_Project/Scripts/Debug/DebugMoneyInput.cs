using Market.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug-управление деньгами.
    /// По умолчанию: F1 — добавить, F2 — потратить.
    /// </summary>
    public class DebugMoneyInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MoneySystem moneySystem;

        [Header("Settings")]
        [SerializeField] private float amount = 100f;
        [SerializeField] private Key addKey   = Key.F1;
        [SerializeField] private Key spendKey = Key.F2;

        private void Update()
        {
            if (moneySystem == null) return;
            var kb = Keyboard.current;

            if (kb[addKey].wasPressedThisFrame)   moneySystem.Add(amount);
            if (kb[spendKey].wasPressedThisFrame) moneySystem.TrySpend(amount);
        }
    }
}
