using Market.Economy;
using Market.World;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Pure economy logic: the single transparent price-read point, item seasonal
    /// availability, and money spend rules. Guards the "no hidden coefficients" contract.
    /// </summary>
    public class EconomyTests
    {
        private ItemSO _apple;

        [SetUp]
        public void SetUp()
        {
            _apple = TestItems.CreateItem("item_apple", "Apple", buyPrice: 10f, sellPrice: 15f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_apple);
        }

        [Test]
        public void PriceCalculator_ReadsBasePricesFromItem()
        {
            var calculator = new PriceCalculator();

            Assert.AreEqual(10f, calculator.GetBuyPrice(_apple));
            Assert.AreEqual(15f, calculator.GetSuggestedSellPrice(_apple));
        }

        [Test]
        public void PriceCalculator_NullItem_ReturnsZero()
        {
            var calculator = new PriceCalculator();

            Assert.AreEqual(0f, calculator.GetBuyPrice(null));
            Assert.AreEqual(0f, calculator.GetSuggestedSellPrice(null));
        }

        [Test]
        public void ItemSO_EmptySeasonList_IsAvailableYearRound()
        {
            Assert.IsTrue(_apple.IsAvailableIn(Season.Spring));
            Assert.IsTrue(_apple.IsAvailableIn(Season.Winter));
        }

        [Test]
        public void ItemSO_SeasonList_LimitsAvailability()
        {
            ItemSO summerOnly = TestItems.CreateItem(
                "item_melon", "Melon", seasons: new[] { Season.Summer });

            Assert.IsTrue(summerOnly.IsAvailableIn(Season.Summer));
            Assert.IsFalse(summerOnly.IsAvailableIn(Season.Winter));

            Object.DestroyImmediate(summerOnly);
        }

        [Test]
        public void MoneySystem_TrySpend_RespectsBalance()
        {
            var go = new GameObject("MoneySystem");
            try
            {
                var money = go.AddComponent<MoneySystem>();
                money.SetAmount(100);

                Assert.IsTrue(money.TrySpend(40));
                Assert.AreEqual(60, money.Amount);

                Assert.IsFalse(money.TrySpend(100), "Spending above balance must fail.");
                Assert.IsFalse(money.TrySpend(0), "Zero spend must be rejected.");
                Assert.IsFalse(money.TrySpend(-5), "Negative spend must be rejected.");
                Assert.AreEqual(60, money.Amount, "Failed spends must not change the balance.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MoneySystem_SetAmount_ClampsToZero()
        {
            var go = new GameObject("MoneySystem");
            try
            {
                var money = go.AddComponent<MoneySystem>();
                money.SetAmount(-50);
                Assert.AreEqual(0, money.Amount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MoneySystem_FloatInputs_RoundToWholeCoins()
        {
            var go = new GameObject("MoneySystem");
            try
            {
                var money = go.AddComponent<MoneySystem>();
                money.SetAmount(100.4f);

                Assert.AreEqual(100, money.Amount);

                money.Add(10.6f);
                Assert.AreEqual(111, money.Amount);

                Assert.IsTrue(money.TrySpend(20.5f));
                Assert.AreEqual(90, money.Amount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
