using Market.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug money control.
    /// Default: F1 -- add money, F2 -- spend money.
    /// </summary>
    public class DebugMoneyInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MoneySystem moneySystem;

        [Header("Settings")]
        [SerializeField] private int amount = 100;
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
