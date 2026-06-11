using Market.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Inventory model: add/remove rules and OnChanged notifications the UI relies on.
    /// </summary>
    public class InventoryTests
    {
        private GameObject _go;
        private Inventory _inventory;
        private ItemSO _apple;
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Inventory");
            _inventory = _go.AddComponent<Inventory>();
            _apple = TestItems.CreateItem("item_apple", "Apple");
            _changedCount = 0;
            _inventory.OnChanged += CountChange;
        }

        [TearDown]
        public void TearDown()
        {
            _inventory.OnChanged -= CountChange;
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_apple);
        }

        private void CountChange() => _changedCount++;

        [Test]
        public void Add_IncreasesCount_AndFiresOnChanged()
        {
            _inventory.Add(_apple, 3);

            Assert.AreEqual(3, _inventory.GetCount(_apple));
            Assert.AreEqual(1, _changedCount);
        }

        [Test]
        public void Add_NullOrNonPositive_IsIgnored()
        {
            _inventory.Add(null);
            _inventory.Add(_apple, 0);
            _inventory.Add(_apple, -2);

            Assert.AreEqual(0, _inventory.GetCount(_apple));
            Assert.AreEqual(0, _changedCount, "Rejected adds must not fire OnChanged.");
        }

        [Test]
        public void TryRemove_InsufficientStock_ReturnsFalse_WithoutChanges()
        {
            _inventory.Add(_apple, 2);
            _changedCount = 0;

            Assert.IsFalse(_inventory.TryRemove(_apple, 3));
            Assert.AreEqual(2, _inventory.GetCount(_apple));
            Assert.AreEqual(0, _changedCount);
        }

        [Test]
        public void TryRemove_LastUnit_RemovesItemEntry()
        {
            _inventory.Add(_apple, 2);

            Assert.IsTrue(_inventory.TryRemove(_apple, 2));
            Assert.AreEqual(0, _inventory.GetCount(_apple));
            Assert.AreEqual(0, _inventory.Items.Count, "Item entry must disappear at zero count.");
        }

        [Test]
        public void Has_ChecksRequestedAmount()
        {
            _inventory.Add(_apple, 2);

            Assert.IsTrue(_inventory.Has(_apple));
            Assert.IsTrue(_inventory.Has(_apple, 2));
            Assert.IsFalse(_inventory.Has(_apple, 3));
        }

        [Test]
        public void Clear_EmptiesInventory_AndFiresOnChanged()
        {
            _inventory.Add(_apple, 5);
            _changedCount = 0;

            _inventory.Clear();

            Assert.AreEqual(0, _inventory.Items.Count);
            Assert.AreEqual(1, _changedCount);
        }
    }
}
