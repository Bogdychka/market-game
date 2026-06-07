using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Registry of all ItemSO assets for resolving saves. Looks up by stable Id (primary)
    /// and by DisplayName (fallback for old saves).
    /// </summary>
    [CreateAssetMenu(menuName = "Market/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private ItemSO[] items;

        /// <summary>Find by stable Id. Primary method for resolving saves.</summary>
        public ItemSO FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (var item in items)
                if (item != null && item.Id == id)
                    return item;

            Debug.LogWarning($"[ItemDatabase] Item not found by Id: '{id}'");
            return null;
        }

        /// <summary>Find by display name. Fallback for saves predating the Id field.</summary>
        public ItemSO FindByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;

            foreach (var item in items)
                if (item != null && item.DisplayName == displayName)
                    return item;

            Debug.LogWarning($"[ItemDatabase] Item not found by name: '{displayName}'");
            return null;
        }

        /// <summary>
        /// Resolve a saved item: try Id first, then fall back to name (old-save migration).
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
