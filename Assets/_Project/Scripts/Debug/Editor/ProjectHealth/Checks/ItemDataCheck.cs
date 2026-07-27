using System.Collections.Generic;
using System.Linq;
using Market.Economy;
using UnityEditor;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Validates stable item identity, prices, and database membership.</summary>
    public sealed class ItemDataCheck : IProjectHealthCheck
    {
        public string Name => "Item data";
        public ProjectHealthCategory Category => ProjectHealthCategory.ScriptableObjects;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            string[] paths = context.FindAssetPaths("t:ItemSO");
            var items = new List<ItemSO>(paths.Length);
            var itemPaths = new Dictionary<ItemSO, string>();

            foreach (string path in paths)
            {
                ItemSO item = context.Load<ItemSO>(path);
                if (item == null)
                    continue;

                items.Add(item);
                itemPaths[item] = path;
                ValidateItem(item, path, report);
            }

            AddDuplicateIdIssues(items, itemPaths, report);
            ValidateDatabases(context, items, report);
        }

        private static void ValidateItem(ItemSO item, string path, ProjectHealthReport report)
        {
            var serialized = new SerializedObject(item);
            string rawId = serialized.FindProperty("id").stringValue;
            if (ProjectHealthRules.IsMissingStableId(rawId))
                Add(report, ProjectHealthSeverity.Error, "Item ID is empty", "Assign a stable ID before this item is saved.", path);
            else if (!ProjectHealthRules.IsLowerSnakeCase(rawId))
                Add(report, ProjectHealthSeverity.Info, "Item ID uses a legacy format", $"ID '{rawId}' is stable but new IDs should use lowercase_snake_case.", path);

            if (string.IsNullOrWhiteSpace(item.DisplayName))
                Add(report, ProjectHealthSeverity.Error, "Item display name is empty", "Assign a player-visible English name.", path);
            if (!ProjectHealthRules.IsNonNegative(item.BaseBuyPrice))
                Add(report, ProjectHealthSeverity.Error, "Item buy price is negative", "Buy prices must be zero or greater.", path);
            if (!ProjectHealthRules.IsNonNegative(item.BaseSellPrice))
                Add(report, ProjectHealthSeverity.Error, "Item sell price is negative", "Sell prices must be zero or greater.", path);
        }

        private static void AddDuplicateIdIssues(
            IReadOnlyList<ItemSO> items,
            IReadOnlyDictionary<ItemSO, string> paths,
            ProjectHealthReport report)
        {
            HashSet<string> duplicates = ProjectHealthRules.FindDuplicateKeys(items.Select(item => item.Id));
            foreach (ItemSO item in items)
                if (duplicates.Contains(item.Id))
                    Add(report, ProjectHealthSeverity.Error, "Duplicate ItemSO ID", $"ID '{item.Id}' is used by more than one item.", paths[item]);
        }

        private static void ValidateDatabases(
            ProjectHealthContext context,
            IReadOnlyCollection<ItemSO> allItems,
            ProjectHealthReport report)
        {
            foreach (string path in context.FindAssetPaths("t:ItemDatabase"))
            {
                ItemDatabase database = context.Load<ItemDatabase>(path);
                if (database == null)
                    continue;

                SerializedProperty items = new SerializedObject(database).FindProperty("items");
                ValidateDatabaseEntries(items, allItems, path, report);
            }
        }

        private static void ValidateDatabaseEntries(
            SerializedProperty entries,
            IReadOnlyCollection<ItemSO> allItems,
            string path,
            ProjectHealthReport report)
        {
            var registered = new HashSet<ItemSO>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                ItemSO item = entries.GetArrayElementAtIndex(i).objectReferenceValue as ItemSO;
                if (item == null)
                    Add(report, ProjectHealthSeverity.Error, "ItemDatabase contains an empty entry", $"Remove or replace entry {i}.", path);
                else if (!registered.Add(item))
                    Add(report, ProjectHealthSeverity.Warning, "ItemDatabase contains a duplicate entry", $"'{item.name}' is registered more than once.", path);
            }

            foreach (ItemSO item in allItems)
                if (!registered.Contains(item))
                    Add(report, ProjectHealthSeverity.Warning, "Item is missing from ItemDatabase", $"'{item.name}' cannot be resolved from saved data.", path);
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
