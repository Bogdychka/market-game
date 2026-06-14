using Market.Core;
using Market.Core.Events;
using Market.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Guards the end-of-day revenue, expense, and best-seller accounting.
    /// </summary>
    public class DailySummarySystemTests
    {
        private ItemSO _apple;
        private ItemSO _fish;

        [SetUp]
        public void SetUp()
        {
            _apple = TestItems.CreateItem("item_apple", "Apple");
            _fish = TestItems.CreateItem("item_fish", "Fish");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_apple);
            Object.DestroyImmediate(_fish);
        }

        [Test]
        public void Snapshot_TracksRevenueExpensesProfitAndBestSeller()
        {
            var eventBus = new EventBus();
            using var summarySystem = new DailySummarySystem(eventBus);

            eventBus.Publish(new MarketOpenChangedEvent(true));
            eventBus.Publish(new ItemPurchasedEvent(_apple, 10f));
            eventBus.Publish(new ItemSoldEvent(_apple, 15f));
            eventBus.Publish(new ItemSoldEvent(_fish, 30f));
            eventBus.Publish(new ItemSoldEvent(_fish, 20f));

            DailySummarySnapshot snapshot = summarySystem.CreateSnapshot(3);

            Assert.IsTrue(summarySystem.HasMarketOpenedToday);
            Assert.AreEqual(3, snapshot.Day);
            Assert.AreEqual(65f, snapshot.Revenue);
            Assert.AreEqual(10f, snapshot.Expenses);
            Assert.AreEqual(55f, snapshot.Profit);
            Assert.AreEqual(3, snapshot.ItemsSold);
            Assert.AreEqual(_fish, snapshot.BestSellingItem);
            Assert.AreEqual(2, snapshot.BestSellingQuantity);
            Assert.AreEqual(50f, snapshot.BestSellingRevenue);
        }

        [Test]
        public void DayChange_ResetsCurrentSummary()
        {
            var eventBus = new EventBus();
            var timeSystem = new TimeSystem();
            DailySummarySnapshot completedSummary = default;
            bool summaryReady = false;
            eventBus.Subscribe<DailySummaryReadyEvent>(evt =>
            {
                completedSummary = evt.Summary;
                summaryReady = true;
            });
            using var summarySystem = new DailySummarySystem(eventBus, timeSystem);

            eventBus.Publish(new MarketOpenChangedEvent(true));
            eventBus.Publish(new ItemSoldEvent(_apple, 15f));
            timeSystem.SetTime(2, 8, 0);

            DailySummarySnapshot snapshot = summarySystem.CreateSnapshot(2);

            Assert.IsTrue(summaryReady);
            Assert.AreEqual(1, completedSummary.Day);
            Assert.AreEqual(15f, completedSummary.Revenue);
            Assert.IsFalse(summarySystem.HasMarketOpenedToday);
            Assert.AreEqual(0f, snapshot.Revenue);
            Assert.AreEqual(0, snapshot.ItemsSold);
            Assert.IsNull(snapshot.BestSellingItem);
        }
    }
}
