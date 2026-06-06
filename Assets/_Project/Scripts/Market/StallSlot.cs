using Market.Economy;
using UnityEngine;

namespace Market.Market
{
    /// <summary>
    /// Один слот на прилавке. Хранит ItemSO + цену + спавнит 3D-модель товара
    /// (из ItemSO.WorldPrefab) как child этого объекта.
    /// </summary>
    public class StallSlot : MonoBehaviour
    {
        public ItemSO Item     { get; private set; }
        public float  SellPrice { get; private set; }
        public bool   IsOccupied => Item != null;

        private GameObject _visual;

        /// <summary>Кладёт товар в слот и спавнит его 3D-модель.</summary>
        public void Place(ItemSO item, float price)
        {
            Item      = item;
            SellPrice = price;
            RefreshVisual();
        }

        /// <summary>Очищает слот и удаляет 3D-модель.</summary>
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
