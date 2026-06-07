using Market.Economy;
using UnityEngine;

namespace Market.NPC
{
    /// <summary>
    /// NPC type descriptor: prefab, budget, walk speed, and category preferences.
    /// Used by NPCSpawner to generate varied visitors.
    /// </summary>
    [CreateAssetMenu(menuName = "Market/NPC Type", fileName = "NPCType_New")]
    public class NPCTypeSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string typeName = "Обычный покупатель";
        [Tooltip("NPC prefab. Must contain NavMeshAgent + NPCVisitor components.")]
        [SerializeField] private GameObject npcPrefab;

        [Header("Behaviour")]
        [Tooltip("Maximum amount the NPC is willing to spend on a single item.")]
        [SerializeField] private float budget = 50f;
        [SerializeField] private float walkSpeed  = 3.5f;
        [Tooltip("Time spent browsing at the stall before making a purchase decision (seconds).")]
        [SerializeField] private float browseTime = 1.5f;

        [Header("Preferences")]
        [Tooltip("Preferred item categories. Empty = buys any category.")]
        [SerializeField] private ItemCategory[] preferredCategories;

        public string         TypeName            => typeName;
        public GameObject     NpcPrefab           => npcPrefab;
        public float          Budget              => budget;
        public float          WalkSpeed           => walkSpeed;
        public float          BrowseTime          => browseTime;
        public ItemCategory[] PreferredCategories => preferredCategories;
    }
}
