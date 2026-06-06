using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Реестр всех ItemSO для резолва сейвов. Поиск по стабильному Id (основной)
    /// и по DisplayName (фолбэк для старых сейвов).
    /// </summary>
    [CreateAssetMenu(menuName = "Market/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private ItemSO[] items;

        /// <summary>Поиск по стабильному Id. Основной способ резолва сейвов.</summary>
        public ItemSO FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (var item in items)
                if (item != null && item.Id == id)
                    return item;

            Debug.LogWarning($"[ItemDatabase] Товар не найден по Id: '{id}'");
            return null;
        }

        /// <summary>Поиск по отображаемому имени. Фолбэк для сейвов до введения Id.</summary>
        public ItemSO FindByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;

            foreach (var item in items)
                if (item != null && item.DisplayName == displayName)
                    return item;

            Debug.LogWarning($"[ItemDatabase] Товар не найден по имени: '{displayName}'");
            return null;
        }

        /// <summary>
        /// Резолвит товар сейва: сначала по Id, затем фолбэк по имени (миграция старых сейвов).
        /// </summary>
        public ItemSO Resolve(string id, string displayName)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (var item in items)
                    if (item != null && item.Id == id)
                        return item;
            }
            return FindByName(displayName);
        }
    }
}
