using System;
using Market.Core;
using Market.World;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Supplier shop. Buy prices come from the transparent price point.
    /// Assortment is filtered by the current season (SeasonManager).
    /// </summary>
    public class SupplierShop : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private string prompt = "Открыть магазин";

        [Header("Stock")]
        [SerializeField] private ItemSO[] stock;

        [Header("References")]
        [SerializeField] private MoneySystem moneySystem;
        [SerializeField] private Inventory   inventory;

        public string PromptText => prompt;
        public bool   CanInteract => true;
        public int    StockCount  => stock?.Length ?? 0;

        public event Action<SupplierShop, GameObject> OpenRequested;

        private SeasonManager   _seasonManager;
        private PriceCalculator _priceCalculator;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (moneySystem == null) Debug.LogError("[SupplierShop] moneySystem not assigned", this);
            if (inventory   == null) Debug.LogError("[SupplierShop] inventory not assigned",   this);
        }

        private void Start()
        {
            // Services register in their own Awake — resolve here in Start to be safe
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
            ServiceLocator.TryGet<PriceCalculator>(out _priceCalculator);
        }

        /// <summary>Stock item by index (null if index is invalid).</summary>
        public ItemSO GetStockItem(int index)
        {
            if (stock == null || index < 0 || index >= stock.Length) return null;
            return stock[index];
        }

        /// <summary>Purchase price for an item (falls back to BaseBuyPrice).</summary>
        public float GetBuyPrice(ItemSO item)
        {
            if (item == null) return 0f;
            return _priceCalculator != null
                ? _priceCalculator.GetBuyPrice(item)
                : item.BaseBuyPrice;
        }

        /// <summary>Returns true if the item is currently available from this supplier.</summary>
        public bool IsAvailable(ItemSO item)
        {
            return IsInSeason(item);
        }

        public void Interact(GameObject actor)
        {
            if (OpenRequested != null)
            {
                OpenRequested.Invoke(this, actor);
                return;
            }

            PrintStock();
        }

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Buy by stock array index. Returns false if the item is out of season or funds are insufficient.
        /// </summary>
        public bool Buy(int index)
        {
            if (moneySystem == null || inventory == null)
            {
                Debug.LogError("[SupplierShop] moneySystem/inventory not assigned — purchase impossible.", this);
                return false;
            }
            if (!IsValidIndex(index)) return false;

            ItemSO item = stock[index];

            if (!IsInSeason(item))
            {
                Debug.Log($"[SupplierShop] {item.DisplayName} unavailable: " +
                          $"out of season ({CurrentSeasonName()})");
                return false;
            }

            float price = GetBuyPrice(item);

            if (!moneySystem.TrySpend(price))
            {
                Debug.Log($"[SupplierShop] Insufficient funds: need {price:0.##}, " +
                          $"have {moneySystem.Amount}");
                return false;
            }

            inventory.Add(item);
            Debug.Log($"[SupplierShop] Bought: {item.DisplayName} for {price:0.##}. " +
                      $"Funds: {moneySystem.Amount}. In inventory: {inventory.GetCount(item)}");
            return true;
        }

        // ── Helpers ────────────────────────────────────────────────────
        private bool IsValidIndex(int index)
        {
            if (stock == null || index < 0 || index >= stock.Length || stock[index] == null)
            {
                Debug.LogWarning($"[SupplierShop] Invalid item index: {index}");
                return false;
            }
            return true;
        }

        private bool IsInSeason(ItemSO item)
        {
            if (_seasonManager == null) return true; // no SeasonManager — all available
            return item.IsAvailableIn(_seasonManager.CurrentSeason);
        }

        private string CurrentSeasonName() =>
            _seasonManager != null ? SeasonManager.GetName(_seasonManager.CurrentSeason) : "?";

        private void PrintStock()
        {
            if (stock == null || stock.Length == 0)
            {
                Debug.Log("=== Supplier is empty (stock not assigned) ===");
                return;
            }

            string season = CurrentSeasonName();
            Debug.Log($"=== Supplier ({season}) ===");

            for (int i = 0; i < stock.Length; i++)
            {
                if (stock[i] == null) continue;

                string available = IsInSeason(stock[i]) ? "" : " [out of season]";
                Debug.Log($"[{i}] {stock[i].DisplayName} — {GetBuyPrice(stock[i]):0.##} coins{available}");
            }

            Debug.Log("Buy: DebugSupplierBuy -> keys 1-5");
        }
    }
}
