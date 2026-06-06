using Market.World;
using UnityEngine;

namespace Market.Economy
{
    /// <summary>
    /// Описание товара. Данные отделены от логики — только ScriptableObject.
    /// </summary>
    [CreateAssetMenu(menuName = "Market/Item", fileName = "Item_New")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Стабильный ID для сейвов. Не переименовывать после релиза! " +
                 "Заполняется автоматически из имени ассета, если пуст.")]
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
        [Tooltip("Доступен у поставщика в эти сезоны. Пустой список = круглый год.")]
        [SerializeField] private Season[] availableInSeasons = new Season[0];

        // ── Properties ─────────────────────────────────────────────────
        /// <summary>Стабильный ID для сейвов. Фолбэк на имя ассета, если поле пусто.</summary>
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
        /// Возвращает true если товар доступен в данный сезон.
        /// Пустой список <see cref="availableInSeasons"/> означает «круглый год».
        /// </summary>
        public bool IsAvailableIn(Season season)
        {
            if (availableInSeasons == null || availableInSeasons.Length == 0) return true;
            foreach (var s in availableInSeasons)
                if (s == season) return true;
            return false;
        }

#if UNITY_EDITOR
        // Авто-заполнение Id именем ассета при создании/изменении в редакторе
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                id = name;
        }
#endif
    }
}
