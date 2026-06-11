using Market.Economy;
using Market.World;
using UnityEditor;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Builds throwaway ItemSO/ItemDatabase instances for EditMode tests.
    /// Serialized private fields are set through SerializedObject so the assets
    /// behave exactly like Inspector-authored ones.
    /// </summary>
    internal static class TestItems
    {
        public static ItemSO CreateItem(
            string id,
            string displayName,
            float buyPrice = 10f,
            float sellPrice = 15f,
            Season[] seasons = null)
        {
            ItemSO item = ScriptableObject.CreateInstance<ItemSO>();
            // Property names must match the private serialized field names of ItemSO /
            // ItemDatabase exactly; update them together when those classes are refactored.
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("baseBuyPrice").floatValue = buyPrice;
            serialized.FindProperty("baseSellPrice").floatValue = sellPrice;

            if (seasons != null)
            {
                SerializedProperty seasonsProp = serialized.FindProperty("availableInSeasons");
                seasonsProp.arraySize = seasons.Length;
                for (int i = 0; i < seasons.Length; i++)
                    seasonsProp.GetArrayElementAtIndex(i).enumValueIndex = (int)seasons[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        public static ItemDatabase CreateDatabase(params ItemSO[] items)
        {
            ItemDatabase database = ScriptableObject.CreateInstance<ItemDatabase>();
            var serialized = new SerializedObject(database);
            SerializedProperty itemsProp = serialized.FindProperty("items");
            itemsProp.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return database;
        }
    }
}
