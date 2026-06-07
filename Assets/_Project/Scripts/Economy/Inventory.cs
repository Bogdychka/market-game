using System;
using System.Collections.Generic;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Player inventory. Stores a Dictionary of ItemSO → count. Fires OnChanged on every change.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public event Action OnChanged;

        private readonly Dictionary<ItemSO, int> _items = new();

        public IReadOnlyDictionary<ItemSO, int> Items => _items;

        /// <summary>Add N units of an item (default 1).</summary>
        public void Add(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return;

            _items.TryGetValue(item, out int current);
            _items[item] = current + amount;
            OnChanged?.Invoke();
        }

        /// <summary>Attempt to remove N units. Returns false if insufficient stock.</summary>
        public bool TryRemove(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            if (!_items.TryGetValue(item, out int current) || current < amount) return false;

            current -= amount;
            if (current == 0) _items.Remove(item);
            else              _items[item] = current;

            OnChanged?.Invoke();
            return true;
        }

        public int  GetCount(ItemSO item) => item != null && _items.TryGetValue(item, out int n) ? n : 0;
        public bool Has(ItemSO item, int amount = 1) => GetCount(item) >= amount;

        /// <summary>Clear inventory completely (SaveSystem only).</summary>
        public void Clear()
        {
            _items.Clear();
            OnChanged?.Invoke();
        }
    }
}
