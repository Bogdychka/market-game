using Market.Economy;
using UnityEngine;

namespace Market.Market
{
    /// <summary>
    /// One slot on the market stall. Stores an ItemSO + price and spawns the item's 3D model
    /// (from ItemSO.WorldPrefab) as a child of this object.
    /// </summary>
    public class StallSlot : MonoBehaviour
    {
        public ItemSO Item     { get; private set; }
        public float  SellPrice { get; private set; }
        public bool   IsOccupied => Item != null;

        private GameObject _visual;

        /// <summary>Place an item in the slot and spawn its 3D model.</summary>
        public void Place(ItemSO item, float price)
        {
            Item      = item;
            SellPrice = price;
            RefreshVisual();
        }

        /// <summary>Clear the slot and destroy the 3D model.</summary>
        public void Clear()
        {
            Item      = null;
            SellPrice = 0f;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_visual != null)
            {
                Destroy(_visual);
                _visual = null;
            }

            if (Item?.WorldPrefab != null)
                _visual = Instantiate(Item.WorldPrefab, transform.position, Quaternion.identity, transform);
        }
    }
}
