using Market.NPC;
using UnityEngine.AI;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Validates NPC type values and the prefab components promised by NPCTypeSO.</summary>
    public sealed class NpcTypeDataCheck : IProjectHealthCheck
    {
        public string Name => "NPC type data";
        public ProjectHealthCategory Category => ProjectHealthCategory.ScriptableObjects;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            foreach (string path in context.FindAssetPaths("t:NPCTypeSO"))
            {
                NPCTypeSO npc = context.Load<NPCTypeSO>(path);
                if (npc == null)
                    continue;

                Validate(npc, path, report);
            }
        }

        private static void Validate(NPCTypeSO npc, string path, ProjectHealthReport report)
        {
            if (ProjectHealthRules.IsMissingStableId(npc.Id))
                Add(report, ProjectHealthSeverity.Error, "NPC type ID is empty", "Assign a stable save key.", path);
            else if (!ProjectHealthRules.IsLowerSnakeCase(npc.Id))
                Add(report, ProjectHealthSeverity.Info, "NPC type ID uses a legacy format", "New IDs should use lowercase_snake_case.", path);

            if (string.IsNullOrWhiteSpace(npc.TypeName))
                Add(report, ProjectHealthSeverity.Error, "NPC type name is empty", "Assign a readable English type name.", path);
            if (npc.NpcPrefab == null)
                Add(report, ProjectHealthSeverity.Error, "NPC prefab is missing", "Assign the visitor prefab.", path);
            else
                ValidatePrefab(npc, path, report);

            if (npc.Budget < 0f)
                Add(report, ProjectHealthSeverity.Error, "NPC budget is negative", "Budget must be zero or greater.", path);
            if (npc.WalkSpeed <= 0f)
                Add(report, ProjectHealthSeverity.Error, "NPC walk speed is invalid", "Walk speed must be greater than zero.", path);
            if (npc.BrowseTime < 0f)
                Add(report, ProjectHealthSeverity.Error, "NPC browse time is negative", "Browse time must be zero or greater.", path);
        }

        private static void ValidatePrefab(NPCTypeSO npc, string path, ProjectHealthReport report)
        {
            if (npc.NpcPrefab.GetComponentInChildren<NPCVisitor>(true) == null)
                Add(report, ProjectHealthSeverity.Error, "NPCVisitor component is missing", "The assigned prefab must contain NPCVisitor.", path);
            if (npc.NpcPrefab.GetComponentInChildren<NavMeshAgent>(true) == null)
                Add(report, ProjectHealthSeverity.Error, "NavMeshAgent component is missing", "The assigned prefab must contain NavMeshAgent.", path);
        }

        private static void Add(
            ProjectHealthReport report,
            ProjectHealthSeverity severity,
            string title,
            string description,
            string path)
        {
            report.Add(new ProjectHealthIssue(severity, ProjectHealthCategory.ScriptableObjects, title, description, path));
        }
    }
}
