using Market.Core;
using Market.Economy;
using Market.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Guards the E1 seed, growth, and harvest loop.
    /// </summary>
    public class CropPlotTests
    {
        private ItemSO _seed;
        private ItemSO _harvest;
        private CropSO _crop;
        private GameObject _root;
        private Inventory _inventory;
        private CropPlot _plot;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register(new TimeSystem());

            _seed = TestItems.CreateItem("item_carrot_seed", "Carrot Seeds");
            _harvest = TestItems.CreateItem("item_carrot", "Carrot");
            _crop = CreateCrop(_seed, _harvest, growthHours: 2f);

            _root = new GameObject("CropPlotTest");
            _root.SetActive(false);
            _inventory = _root.AddComponent<Inventory>();
            _plot = _root.AddComponent<CropPlot>();

            var serialized = new SerializedObject(_plot);
            serialized.FindProperty("crop").objectReferenceValue = _crop;
            serialized.FindProperty("inventory").objectReferenceValue = _inventory;
            serialized.FindProperty("debugInstantGrowOnInteract").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_seed);
            Object.DestroyImmediate(_harvest);
            Object.DestroyImmediate(_crop);
            ServiceLocator.Clear();
        }

        [Test]
        public void Plant_ConsumesSeed_AndHarvestAddsYield()
        {
            _inventory.Add(_seed);

            Assert.IsTrue(_plot.TryPlant());
            Assert.AreEqual(0, _inventory.GetCount(_seed));
            Assert.AreEqual(CropState.Planted, _plot.State);

            Assert.IsTrue(_plot.DebugGrowNow());
            Assert.AreEqual(CropState.Ready, _plot.State);

            Assert.IsTrue(_plot.TryHarvest());
            Assert.AreEqual(2, _inventory.GetCount(_harvest));
            Assert.AreEqual(CropState.Empty, _plot.State);
        }

        [Test]
        public void Plant_FailsWithoutSeed()
        {
            Assert.IsFalse(_plot.TryPlant());
            Assert.AreEqual(CropState.Empty, _plot.State);
        }

        private static CropSO CreateCrop(ItemSO seed, ItemSO harvest, float growthHours)
        {
            CropSO crop = ScriptableObject.CreateInstance<CropSO>();
            var serialized = new SerializedObject(crop);
            serialized.FindProperty("displayName").stringValue = "Carrot";
            serialized.FindProperty("seedItem").objectReferenceValue = seed;
            serialized.FindProperty("harvestItem").objectReferenceValue = harvest;
            serialized.FindProperty("growthHours").floatValue = growthHours;
            serialized.FindProperty("yieldAmount").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return crop;
        }
    }
}
