using Market.World;
using UnityEditor;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Validates the explicit data contract currently owned by CropSO.</summary>
    public sealed class CropDataCheck : IProjectHealthCheck
    {
        public string Name => "Crop data";
        public ProjectHealthCategory Category => ProjectHealthCategory.ScriptableObjects;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            foreach (string path in context.FindAssetPaths("t:CropSO"))
            {
                CropSO crop = context.Load<CropSO>(path);
                if (crop == null)
                    continue;

                Validate(crop, path, report);
            }
        }

        private static void Validate(CropSO crop, string path, ProjectHealthReport report)
        {
            var serialized = new SerializedObject(crop);
            if (string.IsNullOrWhiteSpace(crop.DisplayName))
                Add(report, "Crop display name is empty", "Assign a player-visible English name.", path);
            if (crop.SeedItem == null)
                Add(report, "Crop seed item is missing", "Assign the inventory item consumed when planting.", path);
            if (crop.HarvestItem == null)
                Add(report, "Crop harvest item is missing", "Assign the inventory item granted at harvest.", path);
            if (serialized.FindProperty("growthHours").floatValue <= 0f)
                Add(report, "Crop growth time is invalid", "Growth hours must be greater than zero.", path);
            if (serialized.FindProperty("yieldAmount").intValue <= 0)
                Add(report, "Crop yield is invalid", "Yield must be greater than zero.", path);
        }

        private static void Add(ProjectHealthReport report, string title, string description, string path)
        {
            report.Add(new ProjectHealthIssue(
                ProjectHealthSeverity.Error,
                ProjectHealthCategory.ScriptableObjects,
                title,
                description,
                path));
        }
    }
}
