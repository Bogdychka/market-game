using System;
using System.Collections.Generic;
using Market.Core;
using Market.Core.Events;

namespace Market.Economy
{
    /// <summary>
    /// Tracks the current market day's visible business results for the evening report.
    /// </summary>
    public class DailySummarySystem : IDisposable
    {
        private readonly EventBus _eventBus;
        private readonly TimeSystem _timeSystem;
        private readonly Dictionary<ItemSO, ItemSalesSummary> _itemSales = new();

        private float _revenue;
        private float _expenses;
        private int _itemsSold;
        private int _ordersCompleted;
        private bool _marketOpenedToday;

        public DailySummarySystem(EventBus eventBus, TimeSystem timeSystem = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _timeSystem = timeSystem;

            _eventBus.Subscribe<ItemSoldEvent>(HandleItemSold);
            _eventBus.Subscribe<ItemPurchasedEvent>(HandleItemPurchased);
            _eventBus.Subscribe<MarketOpenChangedEvent>(HandleMarketOpenChanged);

            if (_timeSystem != null)
                _timeSystem.OnDayChanged += HandleDayChanged;
        }

        public bool HasMarketOpenedToday => _marketOpenedToday;

        public DailySummarySnapshot CreateSnapshot(int day)
        {
            ItemSalesSummary bestSeller = GetBestSeller();
            return new DailySummarySnapshot(
                day,
                _revenue,
                _expenses,
                _revenue - _expenses,
                _itemsSold,
                _ordersCompleted,
                bestSeller.Item,
                bestSeller.Quantity,
                bestSeller.Revenue);
        }

        public void RecordOrderCompleted()
        {
            _ordersCompleted++;
        }

        public void Reset()
        {
            _revenue = 0f;
            _expenses = 0f;
            _itemsSold = 0;
            _ordersCompleted = 0;
            _marketOpenedToday = false;
            _itemSales.Clear();
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<ItemSoldEvent>(HandleItemSold);
            _eventBus.Unsubscribe<ItemPurchasedEvent>(HandleItemPurchased);
            _eventBus.Unsubscribe<MarketOpenChangedEvent>(HandleMarketOpenChanged);

            if (_timeSystem != null)
                _timeSystem.OnDayChanged -= HandleDayChanged;
        }

        private void HandleItemSold(ItemSoldEvent evt)
        {
            if (evt.Item == null || evt.Price <= 0f)
                return;

            _revenue += evt.Price;
            _itemsSold++;

            if (!_itemSales.TryGetValue(evt.Item, out ItemSalesSummary summary))
                summary = new ItemSalesSummary(evt.Item);

            summary.AddSale(evt.Price);
            _itemSales[evt.Item] = summary;
        }

        private void HandleItemPurchased(ItemPurchasedEvent evt)
        {
            if (evt.Price > 0f)
                _expenses += evt.Price;
        }

        private void HandleMarketOpenChanged(MarketOpenChangedEvent evt)
        {
            if (evt.IsOpen)
                _marketOpenedToday = true;
        }

        private void HandleDayChanged(int day)
        {
            Reset();
        }

        private ItemSalesSummary GetBestSeller()
        {
            ItemSalesSummary best = default;

            foreach (ItemSalesSummary candidate in _itemSales.Values)
            {
                if (best.Item == null
                    || candidate.Quantity > best.Quantity
                    || (candidate.Quantity == best.Quantity && candidate.Revenue > best.Revenue))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private struct ItemSalesSummary
        {
            public readonly ItemSO Item;
            public int Quantity { get; private set; }
            public float Revenue { get; private set; }

            public ItemSalesSummary(ItemSO item)
            {
                Item = item;
                Quantity = 0;
                Revenue = 0f;
            }

            public void AddSale(float price)
            {
                Quantity++;
                Revenue += price;
            }
        }
    }
}
