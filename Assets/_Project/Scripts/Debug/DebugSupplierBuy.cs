using Market.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug: digit keys buy an item by index from the supplier shop.
    /// </summary>
    public class DebugSupplierBuy : MonoBehaviour
    {
        private const int DigitKeyCount = 9;

        [Header("References")]
        [SerializeField] private SupplierShop shop;

        [Header("Settings")]
        [Tooltip("How many digit keys to listen on. Clamped to keys 1-9.")]
        [SerializeField] private int maxIndex = 6;

        private void Update()
        {
            if (shop == null) return;
            var kb = Keyboard.current;
            int keyCount = Mathf.Clamp(maxIndex, 0, DigitKeyCount);

            for (int i = 0; i < keyCount; i++)
            {
                if (shop.GetStockItem(i) == null) continue;

                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    Debug.Log($"[DebugSupplierBuy] Buying item [{i}]");
                    shop.Buy(i);
                }
            }
        }
    }
}
