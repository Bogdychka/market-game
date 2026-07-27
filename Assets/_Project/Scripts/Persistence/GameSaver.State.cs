using System.Collections.Generic;
using Market.Economy;
using Market.Market;
using Market.World;
using UnityEngine;

namespace Market.Persistence
{
    public partial class GameSaver
    {
        // -- Save: collect ----------------------------------------------
        private SaveData CollectSaveData()
        {
            var data = new SaveData();

            CollectMoney(data);
            CollectInventory(data);
            CollectStallSlots(data);
            CollectNpcVisitors(data);
            CollectCropPlots(data);
            CollectPlayerTransform(data);
            CollectTime(data);

            return data;
        }

        private void CollectMoney(SaveData data)
        {
            data.moneyCoins = moneySystem.Amount;
            data.money = data.moneyCoins;
        }

        private void CollectInventory(SaveData data)
        {
            foreach (var kv in inventory.Items)
                data.inventory.Add(new InventoryItemData
                {
                    itemId = kv.Key.Id,
                    itemName = kv.Key.DisplayName,
                    count = kv.Value
                });
        }

        private void CollectStallSlots(SaveData data)
        {
            foreach (MarketStall stall in stallRegistry.Stalls)
            {
                if (stall == null || stall.Slots == null) continue;

                StallSlot[] slots = stall.Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    StallSlot slot = slots[i];
                    if (slot == null || !slot.IsOccupied) continue;

                    data.stallSlots.Add(new StallSlotData
                    {
                        stallId = stall.StallId,
                        slotIndex = i,
                        itemId = slot.Item.Id,
                        itemName = slot.Item.DisplayName,
                        sellPrice = slot.SellPrice
                    });
                }
            }
        }

        private void CollectNpcVisitors(SaveData data)
        {
            npcSpawner?.CollectActiveVisitors(data.npcVisitors);
        }

        private void CollectCropPlots(SaveData data)
        {
            if (cropPlots == null) return;

            foreach (CropPlot plot in cropPlots)
            {
                if (plot == null || string.IsNullOrWhiteSpace(plot.PlotId)) continue;

                data.cropPlots.Add(new CropPlotData
                {
                    plotId = plot.PlotId,
                    planted = plot.IsPlanted,
                    plantedAtMinutes = plot.PlantedAtMinutes,
                    soilState = (int)plot.SoilState
                });
            }
        }

        private void CollectTime(SaveData data)
        {
            if (_timeSystem == null) return;
            data.day = _timeSystem.Day;
            data.hour = _timeSystem.Hour;
            data.minute = _timeSystem.Minute;
        }

        private void CollectPlayerTransform(SaveData data)
        {
            var pos = playerTransform.position;
            data.playerX = pos.x;
            data.playerY = pos.y;
            data.playerZ = pos.z;
            data.playerRotationY = playerTransform.eulerAngles.y;
        }

        // -- Load: apply ------------------------------------------------
        private void ApplySaveData(SaveData data)
        {
            ApplyMoney(data);
            ApplyInventory(data);
            ApplyStallSlots(data);
            ApplyPlayerTransform(data);
            ApplyTime(data);
            ApplyCropPlots(data);
            ApplyNpcVisitors(data);
        }

        private void ApplyMoney(SaveData data)
        {
            moneySystem.SetAmount(GetSavedMoneyCoins(data));
        }

        private static int GetSavedMoneyCoins(SaveData data)
        {
            if (data == null) return 0;

            return data.version >= 6 || data.moneyCoins > 0
                ? Mathf.Max(0, data.moneyCoins)
                : MoneySystem.ToCoins(data.money);
        }

        private void ApplyInventory(SaveData data)
        {
            inventory.Clear();
            if (data.inventory == null) return;

            foreach (var itemData in data.inventory)
            {
                var so = itemDatabase.Resolve(itemData.itemId, itemData.itemName);
                if (so != null) inventory.Add(so, itemData.count);
            }
        }

        private void ApplyStallSlots(SaveData data)
        {
            foreach (MarketStall stall in stallRegistry.Stalls)
            {
                if (stall == null || stall.Slots == null) continue;

                foreach (StallSlot slot in stall.Slots)
                {
                    if (slot != null && slot.IsOccupied)
                        slot.Clear();
                }
            }

            if (data.stallSlots == null) return;

            foreach (var slotData in data.stallSlots)
            {
                MarketStall stall = ResolveSavedStall(slotData);
                if (stall == null || stall.Slots == null) continue;
                if (slotData.slotIndex < 0 || slotData.slotIndex >= stall.Slots.Length) continue;

                var so = itemDatabase.Resolve(slotData.itemId, slotData.itemName);
                if (so != null)
                    stall.Slots[slotData.slotIndex].Place(so, slotData.sellPrice);
            }
        }

        private void ApplyTime(SaveData data)
        {
            if (_timeSystem == null) return;
            _timeSystem.SetTime(data.day, data.hour, data.minute);

            // Season is derived from day, so refresh after setting time.
            _seasonManager?.RefreshSeason();
        }

        private void ApplyPlayerTransform(SaveData data)
        {
            // CharacterController must be disabled to teleport the position.
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
            playerTransform.eulerAngles = new Vector3(0f, data.playerRotationY, 0f);

            if (cc != null) cc.enabled = true;
        }

        private void ApplyCropPlots(SaveData data)
        {
            if (cropPlots == null) return;

            // Empty crop state is the correct restore for pre-v5 saves: every plot
            // resets to empty, matching the old no-persistence behavior.
            foreach (CropPlot plot in cropPlots)
            {
                if (plot == null) continue;

                CropPlotData saved = FindCropData(data.cropPlots, plot.PlotId);
                if (saved != null)
                    plot.RestoreState(saved.planted, saved.plantedAtMinutes, (CropSoilState)saved.soilState);
                else
                    plot.RestoreState(false, 0f, CropSoilState.Untilled);
            }
        }

        private static CropPlotData FindCropData(List<CropPlotData> saved, string plotId)
        {
            if (saved == null || string.IsNullOrWhiteSpace(plotId)) return null;

            foreach (CropPlotData data in saved)
                if (data != null && data.plotId == plotId)
                    return data;

            return null;
        }

        private void ApplyNpcVisitors(SaveData data)
        {
            npcSpawner?.RestoreActiveVisitors(data.npcVisitors);
        }
    }
}
