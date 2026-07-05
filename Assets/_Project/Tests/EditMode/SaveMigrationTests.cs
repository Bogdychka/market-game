using Market.Economy;
using Market.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Save-format compatibility: ItemDatabase id/name resolution (the v1-to-v2 item
    /// migration path) and SaveData JSON defaults for fields missing in old saves.
    /// Extend these tests whenever SaveData.version is bumped.
    /// </summary>
    public class SaveMigrationTests
    {
        private ItemSO _apple;
        private ItemSO _fish;
        private ItemDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _apple = TestItems.CreateItem("item_apple", "Apple");
            _fish = TestItems.CreateItem("item_fish", "Fish");
            _database = TestItems.CreateDatabase(_apple, _fish);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_database);
            Object.DestroyImmediate(_apple);
            Object.DestroyImmediate(_fish);
        }

        [Test]
        public void Resolve_ById_IgnoresDisplayName()
        {
            // Id is the primary key: a stale display name must not break resolution.
            Assert.AreSame(_apple, _database.Resolve("item_apple", "Renamed Apple"));
        }

        [Test]
        public void Resolve_EmptyId_FallsBackToName()
        {
            // v1 saves stored only itemName; itemId arrived in save version 2.
            Assert.AreSame(_fish, _database.Resolve(string.Empty, "Fish"));
        }

        [Test]
        public void Resolve_UnknownIdAndName_ReturnsNull()
        {
            Assert.IsNull(_database.Resolve("item_missing", "Unknown Item"));
        }

        [Test]
        public void SaveData_V1Json_FillsTimeDefaults()
        {
            // A v1 save has no day/hour/minute fields: JsonUtility must keep the
            // SaveData field initializers (Day 1, 08:00) instead of zeroing them.
            const string v1Json = "{\"version\":1,\"money\":120.5," +
                "\"inventory\":[{\"itemId\":\"\",\"itemName\":\"Apple\",\"count\":2}]," +
                "\"playerX\":1.0,\"playerY\":0.0,\"playerZ\":3.0,\"playerRotationY\":90.0}";

            SaveData data = JsonUtility.FromJson<SaveData>(v1Json);

            Assert.AreEqual(1, data.version);
            Assert.AreEqual(120.5f, data.money);
            Assert.AreEqual(1, data.day);
            Assert.AreEqual(8, data.hour);
            Assert.AreEqual(0, data.minute);
            Assert.AreEqual(1, data.inventory.Count);
            Assert.AreSame(_apple, _database.Resolve(data.inventory[0].itemId, data.inventory[0].itemName));
        }

        [Test]
        public void SaveData_RoundTrip_PreservesFields()
        {
            var original = new SaveData
            {
                money = 333.25f,
                day = 12,
                hour = 19,
                minute = 45,
                playerX = 4.5f,
                playerRotationY = 180f
            };
            original.inventory.Add(new InventoryItemData { itemId = "item_fish", itemName = "Fish", count = 7 });
            original.stallSlots.Add(new StallSlotData
            {
                stallId = "MarketStall_1",
                slotIndex = 1,
                itemId = "item_apple",
                itemName = "Apple",
                sellPrice = 25f
            });

            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original));

            Assert.AreEqual(original.version, restored.version);
            Assert.AreEqual(333.25f, restored.money);
            Assert.AreEqual(12, restored.day);
            Assert.AreEqual(19, restored.hour);
            Assert.AreEqual(45, restored.minute);
            Assert.AreEqual(4.5f, restored.playerX);
            Assert.AreEqual(180f, restored.playerRotationY);
            Assert.AreEqual(1, restored.inventory.Count);
            Assert.AreEqual("item_fish", restored.inventory[0].itemId);
            Assert.AreEqual(7, restored.inventory[0].count);
            Assert.AreEqual(1, restored.stallSlots.Count);
            Assert.AreEqual("MarketStall_1", restored.stallSlots[0].stallId);
            Assert.AreEqual(25f, restored.stallSlots[0].sellPrice);
        }

        [Test]
        public void SaveData_V3Json_StallSlotsAllowMissingStallId()
        {
            const string v3Json = "{\"version\":3,\"money\":50," +
                "\"stallSlots\":[{\"slotIndex\":2,\"itemId\":\"item_apple\",\"itemName\":\"Apple\",\"sellPrice\":18.5}]}";

            SaveData data = JsonUtility.FromJson<SaveData>(v3Json);

            Assert.AreEqual(3, data.version);
            Assert.AreEqual(1, data.stallSlots.Count);
            Assert.IsTrue(string.IsNullOrEmpty(data.stallSlots[0].stallId));
            Assert.AreEqual(2, data.stallSlots[0].slotIndex);
            Assert.AreEqual(18.5f, data.stallSlots[0].sellPrice);
        }

        [Test]
        public void SaveData_V4Json_HasEmptyCropPlots()
        {
            // Crop plots arrived in version 5: a v4 save has no cropPlots field, so the list
            // initializer must survive (empty, not null) and every plot restores to empty.
            const string v4Json = "{\"version\":4,\"money\":80,\"npcVisitors\":[]}";

            SaveData data = JsonUtility.FromJson<SaveData>(v4Json);

            Assert.AreEqual(4, data.version);
            Assert.IsNotNull(data.cropPlots);
            Assert.AreEqual(0, data.cropPlots.Count);
        }

        [Test]
        public void SaveData_V5CropPlots_RoundTrip()
        {
            var original = new SaveData();
            original.cropPlots.Add(new CropPlotData
            {
                plotId = "CropPlot_0",
                planted = true,
                plantedAtMinutes = 1234.5f
            });

            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original));

            Assert.AreEqual(5, restored.version);
            Assert.AreEqual(1, restored.cropPlots.Count);
            Assert.AreEqual("CropPlot_0", restored.cropPlots[0].plotId);
            Assert.IsTrue(restored.cropPlots[0].planted);
            Assert.AreEqual(1234.5f, restored.cropPlots[0].plantedAtMinutes);
        }
    }
}
