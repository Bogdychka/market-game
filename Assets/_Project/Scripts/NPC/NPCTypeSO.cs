using Market.Economy;
using UnityEngine;

namespace Market.NPC
{
    /// <summary>
    /// Тип NPC: префаб, бюджет, скорость, предпочтения категорий.
    /// Используется NPCSpawner'ом для разнообразия посетителей.
    /// </summary>
    [CreateAssetMenu(menuName = "Market/NPC Type", fileName = "NPCType_New")]
    public class NPCTypeSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string typeName = "Обычный покупатель";
        [Tooltip("Префаб NPC. Должен содержать компоненты NavMeshAgent + NPCVisitor.")]
        [SerializeField] private GameObject npcPrefab;

        [Header("Behaviour")]
        [Tooltip("Максимальная сумма, которую готов потратить за один товар.")]
        [SerializeField] private float budget = 50f;
        [SerializeField] private float walkSpeed  = 3.5f;
        [Tooltip("Время раздумий у прилавка перед покупкой (в секундах).")]
        [SerializeField] private float browseTime = 1.5f;

        [Header("Preferences")]
        [Tooltip("Предпочитаемые категории товаров. Пусто = покупает любой товар.")]
        [SerializeField] private ItemCategory[] preferredCategories;

        public string         TypeName            => typeName;
        public GameObject     NpcPrefab           => npcPrefab;
        public float          Budget              => budget;
        public float          WalkSpeed           => walkSpeed;
        public float          BrowseTime          => browseTime;
        public ItemCategory[] PreferredCategories => preferredCategories;
    }
}
