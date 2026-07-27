using System.Collections.Generic;
using Market.DebugTools;
using Market.Economy;
using Market.Persistence;
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
        private const string CarrotPlantPath = "Assets/Cartoon_Farm_Crops/Prefabs/Standard/Carrot_Plant.prefab";
        private const string DirtPilePath = "Assets/Cartoon_Farm_Crops/Prefabs/Standard/Dirt_Pile.prefab";
        private const string GrassTexturePath = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/Grass/Grass_normal/Grass_normal_up.png";
        private const string SoilMaterialPath = "Assets/_Project/Art/Farming/Materials/FarmCell_Grass.mat";
        private const string FarmBedName = "FarmBed_Center";
        private const string LegacyPlotName = "Debug_CropPlot_Carrot";
        private const int GridSize = 3;
        private const float CellSpacing = 2f;
        private static readonly Vector2 FarmCenter = new(58f, 10f);

        [MenuItem("Market/Debug/Build E2 Farm Bed")]
        public static void BuildFarmBed()
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
            EnsureDebugTimeControl();
            List<CropPlot> plots = CreateFarmBed(crop);
            RegisterPlotsInSaver(plots);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CropE1SceneBuilder] Built a 3x3 farm bed with soil preparation and carrot growth stages.");
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
            serialized.FindProperty("displayName").stringValue = "Carrot Seeds";
            serialized.FindProperty("description").stringValue = "Seeds for the first crop plot.";
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
            serialized.FindProperty("displayName").stringValue = "Carrot";
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
            SupplierShop supplier = Object.FindAnyObjectByType<SupplierShop>();
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

        private static List<CropPlot> CreateFarmBed(CropSO crop)
        {
            DestroyRootObject(FarmBedName);
            DestroyRootObject(LegacyPlotName);

            Material grassMaterial = EnsureGrassMaterial();
            Inventory inventory = Object.FindAnyObjectByType<Inventory>();
            var plots = new List<CropPlot>(GridSize * GridSize);
            GameObject farmBed = new(FarmBedName);
            farmBed.transform.position = GetFarmPosition();

            for (int z = 0; z < GridSize; z++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int index = z * GridSize + x;
                    float offset = (GridSize - 1) * CellSpacing * 0.5f;
                    Vector3 localPosition = new(x * CellSpacing - offset, 0f, z * CellSpacing - offset);
                    plots.Add(CreatePlotCell(farmBed.transform, localPosition, index, crop, inventory, grassMaterial));
                }
            }

            Undo.RegisterCreatedObjectUndo(farmBed, "Build E2 farm bed");
            return plots;
        }

        private static CropPlot CreatePlotCell(
            Transform parent,
            Vector3 localPosition,
            int index,
            CropSO crop,
            Inventory inventory,
            Material grassMaterial)
        {
            GameObject plot = new($"FarmCell_{index + 1:00}");
            plot.transform.SetParent(parent, false);
            plot.transform.localPosition = localPosition;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
                plot.layer = interactableLayer;

            var collider = plot.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.1f, 0f);
            collider.size = new Vector3(1.8f, 0.2f, 1.8f);
            CreateGrassBase(plot.transform, grassMaterial);

            GameObject tilled = CreateTilledVisual(plot.transform);
            Transform sprout = CreateStageVisual(plot.transform, "Sprout", 0.35f);
            Transform ready = CreateStageVisual(plot.transform, "Ready", 1f);
            Renderer[] tilledRenderers = tilled != null
                ? tilled.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];

            CropPlot cropPlot = plot.AddComponent<CropPlot>();
            var serialized = new SerializedObject(cropPlot);
            serialized.FindProperty("plotId").stringValue = $"FarmCell_{index:00}";
            serialized.FindProperty("crop").objectReferenceValue = crop;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("sproutVisual").objectReferenceValue = sprout;
            serialized.FindProperty("readyVisual").objectReferenceValue = ready;
            serialized.FindProperty("tilledVisual").objectReferenceValue = tilled;
            SerializedProperty renderers = serialized.FindProperty("tilledRenderers");
            renderers.arraySize = tilledRenderers.Length;
            for (int i = 0; i < tilledRenderers.Length; i++)
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = tilledRenderers[i];
            serialized.FindProperty("debugInstantGrowOnInteract").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return cropPlot;
        }

        private static void CreateGrassBase(Transform parent, Material grassMaterial)
        {
            GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grass.name = "GrassSoil";
            grass.transform.SetParent(parent, false);
            grass.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            grass.transform.localScale = new Vector3(1.8f, 0.2f, 1.8f);
            Object.DestroyImmediate(grass.GetComponent<Collider>());
            if (grassMaterial != null)
                grass.GetComponent<Renderer>().sharedMaterial = grassMaterial;
        }

        private static GameObject CreateTilledVisual(Transform parent)
        {
            GameObject dirtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirtPilePath);
            if (dirtPrefab == null)
            {
                Debug.LogError($"[CropE1SceneBuilder] Missing tilled-soil prefab: {DirtPilePath}");
                return null;
            }

            GameObject tilled = (GameObject)PrefabUtility.InstantiatePrefab(dirtPrefab);
            tilled.name = "TilledSoil";
            tilled.transform.SetParent(parent, false);
            tilled.transform.localPosition = new Vector3(0f, 0.29f, 0f);
            tilled.transform.localScale = Vector3.one * 1.55f;
            RemoveColliders(tilled);
            tilled.SetActive(false);
            return tilled;
        }

        private static Transform CreateStageVisual(Transform parent, string stageName, float scale)
        {
            GameObject plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarrotPlantPath);
            if (plantPrefab == null)
            {
                Debug.LogError($"[CropE1SceneBuilder] Missing crop prefab: {CarrotPlantPath}");
                return null;
            }

            GameObject stage = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
            stage.name = $"Carrot_{stageName}";
            stage.transform.SetParent(parent, false);
            stage.transform.localPosition = new Vector3(0f, 0.21f, 0f);
            stage.transform.localScale = Vector3.one * scale;
            RemoveColliders(stage);
            stage.SetActive(false);
            return stage.transform;
        }

        private static void RemoveColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
                Object.DestroyImmediate(collider);
        }

        /// <summary>Registers all farm cells in GameSaver so soil and crop states persist.</summary>
        private static void RegisterPlotsInSaver(List<CropPlot> plots)
        {
            if (plots == null)
                return;

            GameSaver saver = Object.FindAnyObjectByType<GameSaver>();
            if (saver == null)
            {
                Debug.LogWarning("[CropE1SceneBuilder] No GameSaver in scene; crop plot will not be saved.");
                return;
            }

            var serialized = new SerializedObject(saver);
            SerializedProperty plotReferences = serialized.FindProperty("cropPlots");
            plotReferences.arraySize = plots.Count;
            for (int i = 0; i < plots.Count; i++)
                plotReferences.GetArrayElementAtIndex(i).objectReferenceValue = plots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saver);
        }

        private static Material EnsureGrassMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SoilMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
            if (shader == null || texture == null)
            {
                Debug.LogError("[CropE1SceneBuilder] Missing URP/Lit shader or grass texture for farm soil.");
                return null;
            }

            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Farming");
            EnsureFolder("Assets/_Project/Art/Farming/Materials");
            material = new Material(shader) { name = "FarmCell_Grass" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, SoilMaterialPath);
            return material;
        }

        private static void EnsureDebugTimeControl()
        {
            if (Object.FindAnyObjectByType<DebugTimeControl>() != null)
                return;

            GameObject debugRoot = new("DebugTimeControl");
            debugRoot.AddComponent<DebugTimeControl>();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        private static Vector3 GetFarmPosition()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return new Vector3(FarmCenter.x, 0f, FarmCenter.y);

            Vector3 position = new(FarmCenter.x, 0f, FarmCenter.y);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static void DestroyRootObject(string objectName)
        {
            GameObject existing = FindRootObject(objectName);
            if (existing != null)
                Object.DestroyImmediate(existing);
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
