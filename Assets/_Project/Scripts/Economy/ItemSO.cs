using Market.World;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Item descriptor. Data separated from logic — ScriptableObject only.
    /// </summary>
    [CreateAssetMenu(menuName = "Market/Item", fileName = "Item_New")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable save ID. Do NOT rename after release! " +
                 "Auto-filled from asset name if empty.")]
        [SerializeField] private string id;

        [Header("Info")]
        [SerializeField] private string      displayName  = "Товар";
        [TextArea]
        [SerializeField] private string      description;
        [SerializeField] private Sprite      icon;
        [SerializeField] private GameObject  worldPrefab;
        [SerializeField] private ItemCategory category;

        [Header("Pricing")]
        [SerializeField] private float baseBuyPrice  = 10f;
        [SerializeField] private float baseSellPrice = 15f;

        [Header("Season Availability")]
        [Tooltip("Available from the supplier in these seasons. Empty list = year-round.")]
        [SerializeField] private Season[] availableInSeasons = new Season[0];

        // ── Properties ─────────────────────────────────────────────────
        /// <summary>Stable save ID. Falls back to asset name if the field is empty.</summary>
        public string      Id           => string.IsNullOrEmpty(id) ? name : id;
        public string      DisplayName  => displayName;
        public string      Description  => description;
        public Sprite      Icon         => icon;
        public GameObject  WorldPrefab  => worldPrefab;
        public ItemCategory Category    => category;
        public float       BaseBuyPrice  => baseBuyPrice;
        public float       BaseSellPrice => baseSellPrice;
        public Season[]    AvailableInSeasons => availableInSeasons;

        /// <summary>
        /// Returns true if the item is available in the given season.
        /// An empty <see cref="availableInSeasons"/> list means year-round availability.
        /// </summary>
        public bool IsAvailableIn(Season season)
        {
            if (availableInSeasons == null || availableInSeasons.Length == 0) return true;
            foreach (var s in availableInSeasons)
                if (s == season) return true;
            return false;
        }

#if UNITY_EDITOR
        // Auto-fill Id from asset name on create/change in the Editor
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                id = name;
        }
#endif
    }
}
