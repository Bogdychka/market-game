using System;
using System.Collections.Generic;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Инвентарь игрока. Хранит словарь ItemSO → количество. Эмитит OnChanged при любом изменении.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public event Action OnChanged;

        private readonly Dictionary<ItemSO, int> _items = new();

        public IReadOnlyDictionary<ItemSO, int> Items => _items;

        /// <summary>Добавляет N штук товара (по умолчанию 1).</summary>
        public void Add(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return;

            _items.TryGetValue(item, out int current);
            _items[item] = current + amount;
            OnChanged?.Invoke();
        }

        /// <summary>Пытается снять N штук. Возвращает false если не хватает.</summary>
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

        /// <summary>Очищает инвентарь полностью (только для SaveSystem).</summary>
        public void Clear()
        {
            _items.Clear();
            OnChanged?.Invoke();
        }
    }
}
