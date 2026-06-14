using System;
using System.Collections.Generic;

namespace Market.Persistence
{
    [Serializable]
    public class SaveData
    {
        public int version = 4;
        public float money;
        public List<InventoryItemData> inventory = new();
        public List<StallSlotData> stallSlots    = new();
        public List<NPCVisitorData> npcVisitors   = new();
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
    public class NPCVisitorData
    {
        public string npcTypeKey;
        public int    state;
        public float  x;
        public float  y;
        public float  z;
        public float  rotationY;
        public float  browseTimer;
    }
}
