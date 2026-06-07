using Market.Economy;
using Market.Market;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Debug: F3 places the first inventory item into the first free stall slot
    /// at the default debug price (quick stall test without opening the UI).
    /// </summary>
    public class DebugStallPlace : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarketStall stall;
        [SerializeField] private Inventory   inventory;

        [Header("Settings")]
        [SerializeField] private float debugSellPrice = 20f;
        [SerializeField] private Key   placeKey       = Key.F3;

        private void Update()
        {
            if (stall == null || inventory == null) return;
            if (!Keyboard.current[placeKey].wasPressedThisFrame) return;

            var item = FindFirstItemInInventory();
            if (item == null)
            {
                Debug.Log("[DebugStallPlace] Inventory is empty.");
                return;
            }

            if (!TryPlaceInFirstFreeSlot(item))
                Debug.Log("[DebugStallPlace] No free stall slots.");
        }

        private ItemSO FindFirstItemInInventory()
        {
            foreach (var kv in inventory.Items)
                return kv.Key;
            return null;
        }

        private bool TryPlaceInFirstFreeSlot(ItemSO item)
        {
            var slots = stall.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsOccupied) continue;
                stall.PlaceItem(i, item, debugSellPrice);
                return true;
            }
            return false;
        }
    }
}
