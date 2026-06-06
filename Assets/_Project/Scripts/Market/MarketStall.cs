using System;
using Market.Core;
using Market.Economy;
using UnityEngine;

namespace Market.Market
{
    /// <summary>
    /// Прилавок игрока с массивом слотов. На каждый слот можно положить ItemSO с ценой,
    /// NPC покупает через TakeSale(). Эмитит OnStockChanged при любом изменении стока.
    /// </summary>
    public class MarketStall : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private string prompt = "Управлять прилавком";

        [Header("Slots")]
        [SerializeField] private StallSlot[] slots;

        [Header("References")]
        [SerializeField] private Inventory playerInventory;

        [Header("Debug Starting Stock")]
        [Tooltip("ВКЛ выкладку тестового стока при старте. По умолчанию выключено — иначе New Game стартует с товаром.")]
        [SerializeField] private bool     enableDebugStartStock = false;
        [Tooltip("Товары, которые автоматически выкладываются при старте сцены. Только для тестирования.")]
        [SerializeField] private ItemSO[] debugStartItems;
        [SerializeField] private float    debugStartSellPrice = 20f;

        public event Action OnStockChanged;
        public event Action<MarketStall, GameObject> OpenRequested;

        public string PromptText => prompt;
        public bool   CanInteract => true;
        public StallSlot[] Slots  => slots;

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

        /// <summary>Рекомендованная цена продажи товара. Фолбэк на BaseSellPrice.</summary>
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
        /// Кладёт товар из инвентаря в слот по рекомендованной цене.
        /// </summary>
        public bool PlaceItem(int slotIndex, ItemSO item)
        {
            return PlaceItem(slotIndex, item, SuggestedSellPrice(item));
        }

        /// <summary>
        /// Кладёт товар из инвентаря игрока в указанный слот за указанную цену.
        /// </summary>
        public bool PlaceItem(int slotIndex, ItemSO item, float sellPrice)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;
            if (slots[slotIndex].IsOccupied)  return false;
            if (!playerInventory.Has(item))   return false;

            playerInventory.TryRemove(item);
            slots[slotIndex].Place(item, sellPrice);
            OnStockChanged?.Invoke();

            Debug.Log($"[MarketStall] Выложено: {item.DisplayName} в слот {slotIndex} за {sellPrice}. Stock={TotalStock}");
            return true;
        }

        /// <summary>Снимает товар со слота и возвращает его в инвентарь игрока.</summary>
        public bool RemoveItem(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;
            if (!slots[slotIndex].IsOccupied) return false;
            if (playerInventory == null) return false;

            ItemSO item = slots[slotIndex].Item;
            slots[slotIndex].Clear();
            playerInventory.Add(item);
            OnStockChanged?.Invoke();

            Debug.Log($"[MarketStall] Снято с прилавка: {item.DisplayName} из слота {slotIndex}. Stock={TotalStock}");
            return true;
        }

        /// <summary>
        /// Забирает товар из слота (NPC покупает). Возвращает price и item.
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
            Debug.Log("=== Прилавок ===");
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                Debug.Log(s.IsOccupied
                    ? $"[{i}] {s.Item.DisplayName} — цена {s.SellPrice}"
                    : $"[{i}] пусто");
            }
            Debug.Log($"Инвентарь: {InventoryContents()}");
        }

        private string InventoryContents()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in playerInventory.Items)
                sb.Append($"{kv.Key.DisplayName}×{kv.Value} ");
            return sb.Length > 0 ? sb.ToString() : "пусто";
        }
    }
}
