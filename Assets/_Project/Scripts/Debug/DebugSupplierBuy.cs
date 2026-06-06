using Market.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug: клавиши 1-5 покупают товар по индексу у поставщика.
    /// </summary>
    public class DebugSupplierBuy : MonoBehaviour
    {
        private const int DigitKeyCount = 9;

        [Header("References")]
        [SerializeField] private SupplierShop shop;

        [Header("Settings")]
        [Tooltip("Сколько цифровых клавиш слушать. Ограничено клавишами 1-9.")]
        [SerializeField] private int maxIndex = 5;

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
                    Debug.Log($"[DebugSupplierBuy] Покупаю товар [{i}]");
                    shop.Buy(i);
                }
            }
        }
    }
}
