using System;
using Market.Economy;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Fills the shared market panel with the supplier assortment: a buy row per stock
    /// item, muted and non-interactable when out of season or unaffordable.
    /// </summary>
    public class SupplierPanelRenderer
    {
        private readonly MarketPanelView _view;
        private readonly Action _requestRefresh;

        public SupplierPanelRenderer(MarketPanelView view, Action requestRefresh)
        {
            _view = view;
            _requestRefresh = requestRefresh;
        }

        /// <summary>Render the supplier stock into the cleared panel.</summary>
        public void Render(SupplierShop shop, MoneySystem moneySystem, string subtitle)
        {
            _view.SetHeader("Поставщик", subtitle);

            if (shop == null || shop.StockCount == 0)
            {
                _view.CreateEmptyText("Ассортимент пуст.");
                return;
            }

            for (int i = 0; i < shop.StockCount; i++)
            {
                int index = i;
                ItemSO item = shop.GetStockItem(index);
                if (item == null) continue;

                float price = shop.GetBuyPrice(item);
                bool available = shop.IsAvailable(item);
                Button button = _view.CreateActionRow(
                    item,
                    item.DisplayName,
                    available ? $"{price:0.##} монет" : "Не сезон",
                    "Купить",
                    () =>
                    {
                        shop.Buy(index);
                        _requestRefresh?.Invoke();
                    },
                    !available);

                button.interactable = available;
                if (moneySystem != null)
                    button.interactable = button.interactable && moneySystem.Amount >= price;
            }
        }
    }
}
