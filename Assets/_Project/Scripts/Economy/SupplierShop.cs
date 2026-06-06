using System;
using Market.Core;
using Market.World;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Магазин-поставщик. Цена покупки берётся из прозрачной точки цен.
    /// Ассортимент фильтруется по текущему сезону (SeasonManager).
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
            if (moneySystem == null) Debug.LogError("[SupplierShop] moneySystem не назначен", this);
            if (inventory   == null) Debug.LogError("[SupplierShop] inventory не назначен",   this);
        }

        private void Start()
        {
            // Сервисы регистрируются в своих Awake — берём в Start, чтобы успели
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
            ServiceLocator.TryGet<PriceCalculator>(out _priceCalculator);
        }

        /// <summary>Товар из ассортимента по индексу (null если индекс невалиден).</summary>
        public ItemSO GetStockItem(int index)
        {
            if (stock == null || index < 0 || index >= stock.Length) return null;
            return stock[index];
        }

        /// <summary>Цена покупки товара (фолбэк на BaseBuyPrice).</summary>
        public float GetBuyPrice(ItemSO item)
        {
            if (item == null) return 0f;
            return _priceCalculator != null
                ? _priceCalculator.GetBuyPrice(item)
                : item.BaseBuyPrice;
        }

        /// <summary>Возвращает true если товар сейчас доступен у поставщика.</summary>
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
        /// Покупка по индексу в массиве stock. Возвращает false если товар вне сезона или денег не хватает.
        /// </summary>
        public bool Buy(int index)
        {
            if (moneySystem == null || inventory == null)
            {
                Debug.LogError("[SupplierShop] moneySystem/inventory не назначены — покупка невозможна.", this);
                return false;
            }
            if (!IsValidIndex(index)) return false;

            ItemSO item = stock[index];

            if (!IsInSeason(item))
            {
                Debug.Log($"[SupplierShop] {item.DisplayName} недоступен: " +
                          $"не сезон ({CurrentSeasonName()})");
                return false;
            }

            float price = GetBuyPrice(item);

            if (!moneySystem.TrySpend(price))
            {
                Debug.Log($"[SupplierShop] Не хватает денег: нужно {price:0.##}, " +
                          $"есть {moneySystem.Amount}");
                return false;
            }

            inventory.Add(item);
            Debug.Log($"[SupplierShop] Куплено: {item.DisplayName} за {price:0.##}. " +
                      $"Деньги: {moneySystem.Amount}. В инвентаре: {inventory.GetCount(item)}");
            return true;
        }

        // ── Helpers ────────────────────────────────────────────────────
        private bool IsValidIndex(int index)
        {
            if (stock == null || index < 0 || index >= stock.Length || stock[index] == null)
            {
                Debug.LogWarning($"[SupplierShop] Неверный индекс товара: {index}");
                return false;
            }
            return true;
        }

        private bool IsInSeason(ItemSO item)
        {
            if (_seasonManager == null) return true; // нет SeasonManager — всё доступно
            return item.IsAvailableIn(_seasonManager.CurrentSeason);
        }

        private string CurrentSeasonName() =>
            _seasonManager != null ? SeasonManager.GetName(_seasonManager.CurrentSeason) : "?";

        private void PrintStock()
        {
            if (stock == null || stock.Length == 0)
            {
                Debug.Log("=== Поставщик пуст (stock не назначен) ===");
                return;
            }

            string season = CurrentSeasonName();
            Debug.Log($"=== Поставщик ({season}) ===");

            for (int i = 0; i < stock.Length; i++)
            {
                if (stock[i] == null) continue;

                string available = IsInSeason(stock[i]) ? "" : " [не сезон]";
                Debug.Log($"[{i}] {stock[i].DisplayName} — {GetBuyPrice(stock[i]):0.##} монет{available}");
            }

            Debug.Log("Купить: DebugSupplierBuy → клавиши 1–5");
        }
    }
}
