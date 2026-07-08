using System;
using System.Collections.Generic;

namespace Market.Persistence
{
    [Serializable]
    public class SaveData
    {
        public int version = 6;
        public int moneyCoins;
        // Legacy balance field (version 5 and earlier). Kept for old-save compatibility.
        public float money;
        public List<InventoryItemData> inventory = new();
        public List<StallSlotData> stallSlots    = new();
        public List<NPCVisitorData> npcVisitors   = new();
        public List<CropPlotData> cropPlots       = new();
        public float playerX, playerY, playerZ;
        public float playerRotationY;

        // Time fields (introduced in version 2)
        public int day    = 1;
        public int hour   = 8;
        public int minute = 0;
    }

    [Serializable]
    public class InventoryItemData
    {
        public string itemId;   // primary key (version 2+)
        public string itemName; // fallback for old saves
        public int    count;
    }

    [Serializable]
    public class StallSlotData
    {
        public string stallId;
        public int    slotIndex;
        public string itemId;
        public string itemName;
        public float  sellPrice;
    }

    [Serializable]
    public class CropPlotData
    {
        // The crop type is inherent to the plot (its serialized CropSO), so only the
        // plant state and the absolute game-minute timestamp need persisting (version 5+).
        public string plotId;
        public bool   planted;
        public float  plantedAtMinutes;
    }

    [Serializable]
    public class NPCVisitorData
    {
        // Intent only (schedule-style restore): the visitor is re-spawned at an entrance and walks in,
        // so no transform/timer/state is stored. Avoids restoring an agent mid-stride off the navmesh.
        public string npcTypeKey;
        public string targetStallId;                  // stall the visitor still wants to reach
        public List<string> visitedStallIds = new();  // already-browsed stalls, so it won't revisit
    }
}
