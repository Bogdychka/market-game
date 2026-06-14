using Market.Economy;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Editor helper for the E1 farming slice: one carrot seed, one carrot crop, one debug plot.
    /// </summary>
    public static class CropE1SceneBuilder
    {
        private const string MarketSceneName = "Market";
        private const string SeedPath = "Assets/_Project/Data/Items/Item_CarrotSeed.asset";
        private const string CropPath = "Assets/_Project/Data/Crops/Crop_Carrot.asset";
        private const string HarvestPath = "Assets/_Project/Data/Items/Item_Carrot.asset";
        private const string ItemDatabasePath = "Assets/_Project/Data/ItemDatabase.asset";
        private const string PlotName = "Debug_CropPlot_Carrot";

        [MenuItem("Market/Debug/Create E1 Crop Plot")]
        public static void CreateE1CropPlot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != MarketSceneName)
            {
                Debug.LogError("[CropE1SceneBuilder] Open the Market scene before creating the crop plot.");
                return;
            }

            ItemSO harvest = AssetDatabase.LoadAssetAtPath<ItemSO>(HarvestPath);
            if (harvest == null)
            {
                Debug.LogError("[CropE1SceneBuilder] Missing carrot harvest item.");
                return;
            }

            ItemSO seed = EnsureSeedItem(harvest);
            CropSO crop = EnsureCrop(seed, harvest);
            AddItemToDatabase(seed);
            AddSeedToSupplier(seed);
            CreatePlot(crop);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CropE1SceneBuilder] Created E1 carrot seed, crop, supplier stock, and plot.");
        }

        private static ItemSO EnsureSeedItem(ItemSO harvest)
        {
            ItemSO seed = AssetDatabase.LoadAssetAtPath<ItemSO>(SeedPath);
            if (seed == null)
            {
                seed = ScriptableObject.CreateInstance<ItemSO>();
                AssetDatabase.CreateAsset(seed, SeedPath);
            }

            var serialized = new SerializedObject(seed);
            serialized.FindProperty("id").stringValue = "Item_CarrotSeed";
            serialized.FindProperty("displayName").stringValue = "Семена моркови";
            serialized.FindProperty("description").stringValue = "Семена для первой грядки.";
            serialized.FindProperty("icon").objectReferenceValue = harvest.Icon;
            serialized.FindProperty("category").enumValueIndex = (int)ItemCategory.Ingredient;
            serialized.FindProperty("baseBuyPrice").floatValue = 3f;
            serialized.FindProperty("baseSellPrice").floatValue = 1f;

            SerializedProperty seasons = serialized.FindProperty("availableInSeasons");
            seasons.arraySize = 1;
            seasons.GetArrayElementAtIndex(0).enumValueIndex = (int)Season.Spring;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seed);
            return seed;
        }

        private static CropSO EnsureCrop(ItemSO seed, ItemSO harvest)
        {
            CropSO crop = AssetDatabase.LoadAssetAtPath<CropSO>(CropPath);
            if (crop == null)
            {
                crop = ScriptableObject.CreateInstance<CropSO>();
                AssetDatabase.CreateAsset(crop, CropPath);
            }

            var serialized = new SerializedObject(crop);
            serialized.FindProperty("displayName").stringValue = "Морковь";
            serialized.FindProperty("seedItem").objectReferenceValue = seed;
            serialized.FindProperty("harvestItem").objectReferenceValue = harvest;
            serialized.FindProperty("growthHours").floatValue = 6f;
            serialized.FindProperty("yieldAmount").intValue = 2;

            SerializedProperty seasons = serialized.FindProperty("plantSeasons");
            seasons.arraySize = 1;
            seasons.GetArrayElementAtIndex(0).enumValueIndex = (int)Season.Spring;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(crop);
            return crop;
        }

        private static void AddItemToDatabase(ItemSO seed)
        {
            ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            if (database == null)
                return;

            var serialized = new SerializedObject(database);
            SerializedProperty items = serialized.FindProperty("items");
            if (Contains(items, seed))
                return;

            items.InsertArrayElementAtIndex(items.arraySize);
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void AddSeedToSupplier(ItemSO seed)
        {
            SupplierShop supplier = Object.FindFirstObjectByType<SupplierShop>();
            if (supplier == null)
                return;

            var serialized = new SerializedObject(supplier);
            SerializedProperty stock = serialized.FindProperty("stock");
            if (Contains(stock, seed))
                return;

            stock.InsertArrayElementAtIndex(stock.arraySize);
            stock.GetArrayElementAtIndex(stock.arraySize - 1).objectReferenceValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(supplier);
        }

        private static void CreatePlot(CropSO crop)
        {
            GameObject existing = FindRootObject(PlotName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject plot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plot.name = PlotName;
            plot.transform.SetPositionAndRotation(new Vector3(-4.2f, 0.1f, -1.2f), Quaternion.identity);
            plot.transform.localScale = new Vector3(1.8f, 0.2f, 1.8f);

            GameObject growth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            growth.name = "GrowthStub";
            growth.transform.SetParent(plot.transform, false);
            growth.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            growth.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);
            if (growth.TryGetComponent(out Collider growthCollider))
                Object.DestroyImmediate(growthCollider);

            CropPlot cropPlot = plot.AddComponent<CropPlot>();
            Inventory inventory = Object.FindFirstObjectByType<Inventory>();

            var serialized = new SerializedObject(cropPlot);
            serialized.FindProperty("crop").objectReferenceValue = crop;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("growthVisual").objectReferenceValue = growth.transform;
            serialized.FindProperty("debugInstantGrowOnInteract").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(plot, "Create E1 crop plot");
        }

        private static bool Contains(SerializedProperty array, Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    return true;

            return false;
        }

        private static GameObject FindRootObject(string objectName)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == objectName)
                    return roots[i];

            return null;
        }
    }
}
