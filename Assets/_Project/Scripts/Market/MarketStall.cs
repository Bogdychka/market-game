using System;
using Market.Core;
using Market.Economy;
using UnityEngine;

namespace Market.Market
{
    /// <summary>
    /// Player stall with a slot array. Each slot holds an ItemSO with a price.
    /// NPCs purchase via TakeSale(). Fires OnStockChanged on any stock change.
    /// </summary>
    public class MarketStall : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [Tooltip("Stable save key. Falls back to the GameObject name when empty.")]
        [SerializeField] private string stallId;
        [SerializeField] private string prompt = "Управлять прилавком";

        [Header("Slots")]
        [SerializeField] private StallSlot[] slots;

        [Header("References")]
        [SerializeField] private Inventory playerInventory;

        [Header("Debug Starting Stock")]
        [Tooltip("Enable placing debug stock on start. Off by default — otherwise New Game starts with items on the stall.")]
        [SerializeField] private bool     enableDebugStartStock = false;
        [Tooltip("Items automatically placed on the stall at scene start. For testing only.")]
        [SerializeField] private ItemSO[] debugStartItems;
        [SerializeField] private float    debugStartSellPrice = 20f;

        public event Action OnStockChanged;
        public event Action<MarketStall, GameObject> OpenRequested;

        public string PromptText => prompt;
        public bool   CanInteract => true;
        public StallSlot[] Slots  => slots;
        public string StallId => string.IsNullOrWhiteSpace(stallId) ? gameObject.name : stallId;

        private PriceCalculator _priceCalculator;

        public int TotalStock
        {
            get
            {
                int count = 0;
                foreach (var s in slots)
                    if (s.IsOccupied) count++;
                return count;
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Start()
        {
            ServiceLocator.TryGet<PriceCalculator>(out _priceCalculator);

#if UNITY_EDITOR
            if (enableDebugStartStock) PrepopulateDebugStock();
#endif
        }

        /// <summary>Suggested sell price for an item. Falls back to BaseSellPrice.</summary>
        public float SuggestedSellPrice(ItemSO item)
        {
            if (item == null) return 0f;
            return _priceCalculator != null
                ? _priceCalculator.GetSuggestedSellPrice(item)
                : item.BaseSellPrice;
        }

        // ── IInteractable ──────────────────────────────────────────────
        public void Interact(GameObject actor)
        {
            if (OpenRequested != null)
            {
                OpenRequested.Invoke(this, actor);
                return;
            }

            PrintCurrentState();
        }

        // ── Public API ─────────────────────────────────────────────────
        /// <summary>
        /// Place an item from the player inventory into a slot at the suggested price.
        /// </summary>
        public bool PlaceItem(int slotIndex, ItemSO item)
        {
            return PlaceItem(slotIndex, item, SuggestedSellPrice(item));
        }

        /// <summary>
        /// Place an item from the player inventory into the specified slot at the specified price.
        /// </summary>
        public bool PlaceItem(int slotIndex, ItemSO item, float sellPrice)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;
            if (slots[slotIndex].IsOccupied)  return false;
            if (!playerInventory.Has(item))   return false;

            playerInventory.TryRemove(item);
            slots[slotIndex].Place(item, sellPrice);
            OnStockChanged?.Invoke();

            Debug.Log($"[MarketStall] Placed: {item.DisplayName} in slot {slotIndex} for {sellPrice}. Stock={TotalStock}");
            return true;
        }

        /// <summary>Remove an item from a slot and return it to the player inventory.</summary>
        public bool RemoveItem(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;
            if (!slots[slotIndex].IsOccupied) return false;
            if (playerInventory == null) return false;

            ItemSO item = slots[slotIndex].Item;
            slots[slotIndex].Clear();
            playerInventory.Add(item);
            OnStockChanged?.Invoke();

            Debug.Log($"[MarketStall] Removed from stall: {item.DisplayName} from slot {slotIndex}. Stock={TotalStock}");
            return true;
        }

        /// <summary>
        /// Take an item from a slot (NPC purchase). Returns price and item.
        /// </summary>
        public bool TakeSale(int slotIndex, out ItemSO item, out float price)
        {
            item = null; price = 0f;

            if (!IsValidSlotIndex(slotIndex))   return false;
            if (!slots[slotIndex].IsOccupied)   return false;

            item  = slots[slotIndex].Item;
            price = slots[slotIndex].SellPrice;
            slots[slotIndex].Clear();
            OnStockChanged?.Invoke();
            return true;
        }

        // ── Internals ──────────────────────────────────────────────────
        private bool IsValidSlotIndex(int i) => i >= 0 && i < slots.Length;

        private void PrepopulateDebugStock()
        {
            if (debugStartItems == null) return;

            int max = Mathf.Min(debugStartItems.Length, slots.Length);
            for (int i = 0; i < max; i++)
            {
                if (debugStartItems[i] != null)
                    slots[i].Place(debugStartItems[i], debugStartSellPrice);
            }
        }

        private void PrintCurrentState()
        {
            Debug.Log("=== Stall ===");
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                Debug.Log(s.IsOccupied
                    ? $"[{i}] {s.Item.DisplayName} — price {s.SellPrice}"
                    : $"[{i}] empty");
            }
            Debug.Log($"Inventory: {InventoryContents()}");
        }

        private string InventoryContents()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in playerInventory.Items)
                sb.Append($"{kv.Key.DisplayName}x{kv.Value} ");
            return sb.Length > 0 ? sb.ToString() : "empty";
        }
    }
}
