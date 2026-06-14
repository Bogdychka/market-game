using Market.Economy;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Data for one plantable crop: seed item, growth time, yield, and allowed seasons.
    /// </summary>
    [CreateAssetMenu(menuName = "Market/Crop", fileName = "Crop_New")]
    public class CropSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Crop";

        [Header("Items")]
        [SerializeField] private ItemSO seedItem;
        [SerializeField] private ItemSO harvestItem;

        [Header("Growth")]
        [Tooltip("Game hours from planting to ready-to-harvest.")]
        [SerializeField] private float growthHours = 12f;
        [Tooltip("How many harvest items are added to the inventory.")]
        [SerializeField] private int yieldAmount = 1;

        [Header("Seasons")]
        [Tooltip("Allowed planting seasons. Empty list = year-round.")]
        [SerializeField] private Season[] plantSeasons = new Season[0];

        public string DisplayName => displayName;
        public ItemSO SeedItem => seedItem;
        public ItemSO HarvestItem => harvestItem;
        public float GrowthHours => Mathf.Max(0.01f, growthHours);
        public int YieldAmount => Mathf.Max(1, yieldAmount);
        public Season[] PlantSeasons => plantSeasons;

        public bool CanPlantIn(Season season)
        {
            if (plantSeasons == null || plantSeasons.Length == 0)
                return true;

            for (int i = 0; i < plantSeasons.Length; i++)
                if (plantSeasons[i] == season)
                    return true;

            return false;
        }
    }
}
