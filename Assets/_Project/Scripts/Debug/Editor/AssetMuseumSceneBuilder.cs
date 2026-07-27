using System;
using System.Collections.Generic;
using Market.Interaction;
using Market.Player;
using Market.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds a walkable asset museum from the project's imported model packs.
    /// </summary>
    public static class AssetMuseumSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/AssetMuseum.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Art/Prefabs/Player/Player.prefab";
        private const string GeneratedFolder = "Assets/_Project/Art/AssetMuseum";
        private const string FoodFolder = "Assets/kenney_food-kit/Models/FBX format";
        private const string CropFolder = "Assets/Cartoon_Farm_Crops/Prefabs/Standard";
        private const string BuildingFolder = "Assets/Farm Buildings by Quaternius/FBX";
        private const string AnimalFolder = "Assets/Farm Animals Animated  by Quaternius/FBX";
        private const string FishFolder = "Assets/Fish Pack Animated by Quaternius/FBX";
        private const string TreeFolder = "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/FBX";
        private const string TreeTextureFolder = "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/Textures";

        private const float ZoneWidth = 72f;
        private const float ZoneGap = 8f;
        private const float ColumnGap = 10f;
        private const int ZoneColumns = 3;

        private sealed class ExhibitGroup
        {
            public string Name { get; }
            public List<GameObject> Assets { get; }
            public float CellSize { get; }
            public float DisplaySize { get; }
            public float DisplayHeight { get; }
            public Color Color { get; }

            public ExhibitGroup(
                string name,
                List<GameObject> assets,
                float cellSize,
                float displaySize,
                float displayHeight,
                Color color)
            {
                Name = name;
                Assets = assets;
                CellSize = cellSize;
                DisplaySize = displaySize;
                DisplayHeight = displayHeight;
                Color = color;
            }
        }

        /// <summary>
        /// Rebuilds and opens the standalone AssetMuseum scene.
        /// </summary>
        [MenuItem("Market/Debug/Build Asset Museum")]
        public static void BuildAssetMuseum()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureGeneratedFolder();
            List<ExhibitGroup> groups = CollectGroups();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AssetMuseum";

            Material pedestalMaterial = GetOrCreateMaterial("Pedestal", new Color(0.22f, 0.24f, 0.25f));
            BuildEnvironment(groups, pedestalMaterial);
            BuildPlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AssetMuseumSceneBuilder] Built {ScenePath} with {CountAssets(groups)} categorized exhibits.");
        }

        private static List<ExhibitGroup> CollectGroups()
        {
            var groups = new List<ExhibitGroup>();
            AddGroup(groups, "Farm Buildings", LoadAssets(BuildingFolder, ".fbx"), 18f, 13f, 15f, new Color(0.35f, 0.25f, 0.16f));
            AddGroup(groups, "Farm Animals", LoadAssets(AnimalFolder, ".fbx"), 10f, 6f, 6f, new Color(0.34f, 0.45f, 0.24f));
            AddGroup(groups, "Aquatic Animals", LoadAssets(FishFolder, ".fbx"), 12f, 8f, 5f, new Color(0.16f, 0.38f, 0.55f));
            AddTreeGroups(groups);
            AddCropGroups(groups);
            AddFoodGroups(groups);
            return groups;
        }

        private static void AddTreeGroups(List<ExhibitGroup> groups)
        {
            List<GameObject> trees = LoadAssets(TreeFolder, ".fbx");
            AddGroup(groups, "Living Trees", TakeByName(trees, IsLivingTree), 10f, 7f, 12f, new Color(0.18f, 0.42f, 0.20f));
            AddGroup(groups, "Dead Trees", TakeByName(trees, name => name.Contains("dead")), 10f, 7f, 12f, new Color(0.35f, 0.30f, 0.25f));
            AddGroup(groups, "Pine Trees", TakeByName(trees, name => name.Contains("pine")), 10f, 7f, 12f, new Color(0.12f, 0.34f, 0.22f));
        }

        private static bool IsLivingTree(string name)
        {
            return !name.Contains("dead") && !name.Contains("pine");
        }

        private static void AddCropGroups(List<ExhibitGroup> groups)
        {
            List<GameObject> crops = LoadAssets(CropFolder, ".prefab");
            AddGroup(groups, "Crop Plants", TakeByName(crops, name => name.Contains("plant")), 7f, 4f, 5f, new Color(0.35f, 0.50f, 0.18f));
            AddGroup(groups, "Crop Harvests", TakeByName(crops, name => name.Contains("fruit")), 6f, 3f, 3f, new Color(0.65f, 0.42f, 0.12f));
            AddGroup(groups, "Crop Props", TakeByName(crops, name => !name.Contains("plant") && !name.Contains("fruit")), 6f, 3f, 3f, new Color(0.42f, 0.31f, 0.20f));
        }

        private static void AddFoodGroups(List<ExhibitGroup> groups)
        {
            List<GameObject> food = LoadAssets(FoodFolder, ".fbx");
            var buckets = CreateFoodBuckets();
            foreach (GameObject asset in food)
                buckets[ClassifyFood(asset.name)].Add(asset);

            Color produce = new(0.55f, 0.38f, 0.16f);
            AddGroup(groups, "Fruit", buckets["Fruit"], 4f, 2.2f, 2.5f, new Color(0.60f, 0.22f, 0.18f));
            AddGroup(groups, "Vegetables", buckets["Vegetables"], 4f, 2.2f, 2.5f, produce);
            AddGroup(groups, "Bakery and Sweets", buckets["Bakery and Sweets"], 4f, 2.2f, 2.5f, new Color(0.62f, 0.40f, 0.22f));
            AddGroup(groups, "Meat Fish and Eggs", buckets["Meat Fish and Eggs"], 4f, 2.2f, 2.5f, new Color(0.52f, 0.20f, 0.20f));
            AddGroup(groups, "Drinks", buckets["Drinks"], 4f, 2.2f, 2.5f, new Color(0.18f, 0.42f, 0.58f));
            AddGroup(groups, "Prepared Food", buckets["Prepared Food"], 4f, 2.2f, 2.5f, new Color(0.55f, 0.30f, 0.16f));
            AddGroup(groups, "Kitchenware", buckets["Kitchenware"], 4f, 2.2f, 2.5f, new Color(0.34f, 0.36f, 0.38f));
            AddGroup(groups, "Pantry and Other Food", buckets["Pantry and Other Food"], 4f, 2.2f, 2.5f, new Color(0.45f, 0.36f, 0.22f));
        }

        private static Dictionary<string, List<GameObject>> CreateFoodBuckets()
        {
            return new Dictionary<string, List<GameObject>>
            {
                ["Fruit"] = new(),
                ["Vegetables"] = new(),
                ["Bakery and Sweets"] = new(),
                ["Meat Fish and Eggs"] = new(),
                ["Drinks"] = new(),
                ["Prepared Food"] = new(),
                ["Kitchenware"] = new(),
                ["Pantry and Other Food"] = new()
            };
        }

        private static string ClassifyFood(string assetName)
        {
            string name = assetName.ToLowerInvariant();
            if (ContainsAny(name, "apple", "avocado", "advocado", "banana", "cherries", "coconut", "grapes", "lemon", "orange", "pear", "pineapple", "strawberry", "watermelon"))
                return "Fruit";
            if (ContainsAny(name, "beet", "broccoli", "cabbage", "carrot", "cauliflower", "celery", "corn", "eggplant", "leek", "mushroom", "onion", "paprika", "pumpkin", "radish", "tomato"))
                return "Vegetables";
            if (ContainsAny(name, "bread", "cake", "candy", "chocolate", "cookie", "croissant", "cupcake", "donut", "ginger-bread", "ice-cream", "loaf", "lollypop", "muffin", "pancake", "pie", "popsicle", "pudding", "sundae", "waffle", "whipped-cream"))
                return "Bakery and Sweets";
            if (ContainsAny(name, "bacon", "egg", "fish", "ham", "meat", "mussel", "salmon", "sausage", "turkey"))
                return "Meat Fish and Eggs";
            if (ContainsAny(name, "bottle", "cocktail", "coffee", "cup-tea", "frappe", "glass-wine", "soda", "wine-"))
                return "Drinks";
            if (ContainsAny(name, "bowl-broth", "bowl-cereal", "bowl-soup", "burger", "chinese", "corn-dog", "dim-sum", "fries", "frikandel", "hot-dog", "maki", "pizza", "rice-ball", "salad", "sandwich", "skewer", "stew", "styrofoam-dinner", "sub", "sushi", "taco"))
                return "Prepared Food";
            if (ContainsAny(name, "bowl", "can", "carton", "chopst", "cooking-", "cup", "cutting-board", "frying-pan", "glass", "knife", "mortar", "mug", "pan", "plate", "pot", "rollingpin", "shaker", "steamer", "tajine", "utensil", "whisk"))
                return "Kitchenware";
            return "Pantry and Other Food";
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.Contains(term))
                    return true;
            }

            return false;
        }

        private static void BuildEnvironment(List<ExhibitGroup> groups, Material pedestalMaterial)
        {
            var columnDepths = new float[ZoneColumns];
            float columnSpacing = ZoneWidth + ColumnGap;
            for (int index = 0; index < groups.Count; index++)
            {
                int column = index % ZoneColumns;
                float zoneDepth = GetZoneDepth(groups[index]);
                float x = (column - 1) * columnSpacing;
                float startZ = 14f + columnDepths[column];
                BuildZone(groups[index], new Vector3(x, 0f, startZ), zoneDepth, pedestalMaterial);
                columnDepths[column] += zoneDepth + ZoneGap;
            }

            float museumDepth = Mathf.Max(columnDepths) + 28f;
            BuildGround(museumDepth);
            BuildEntrance();
            BuildLighting();
        }

        private static float GetZoneDepth(ExhibitGroup group)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((ZoneWidth - 8f) / group.CellSize));
            int rows = Mathf.CeilToInt(group.Assets.Count / (float)columns);
            return Mathf.Max(18f, rows * group.CellSize + 12f);
        }

        private static void BuildZone(
            ExhibitGroup group,
            Vector3 origin,
            float zoneDepth,
            Material pedestalMaterial)
        {
            GameObject zone = new(group.Name);
            zone.transform.position = origin;
            CreateZoneFloor(zone.transform, zoneDepth, group.Color);
            CreateText(group.Name, zone.transform, new Vector3(0f, 2.7f, 2.5f), 0.12f, Color.white);

            int columns = Mathf.Max(1, Mathf.FloorToInt((ZoneWidth - 8f) / group.CellSize));
            for (int index = 0; index < group.Assets.Count; index++)
                CreateExhibit(group, zone.transform, index, columns, pedestalMaterial);
        }

        private static void CreateZoneFloor(Transform parent, float depth, Color color)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Zone Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, 0.03f, depth * 0.5f);
            floor.transform.localScale = new Vector3(ZoneWidth, 0.06f, depth);
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"Zone_{ColorUtility.ToHtmlStringRGB(color)}", color);
        }

        private static void CreateExhibit(
            ExhibitGroup group,
            Transform parent,
            int index,
            int columns,
            Material pedestalMaterial)
        {
            int row = index / columns;
            int column = index % columns;
            float usedWidth = (columns - 1) * group.CellSize;
            Vector3 position = new(-usedWidth * 0.5f + column * group.CellSize, 0f, 8f + row * group.CellSize);

            GameObject exhibit = new(group.Assets[index].name);
            exhibit.transform.SetParent(parent, false);
            exhibit.transform.localPosition = position;
            CreatePedestal(exhibit.transform, group.CellSize, pedestalMaterial);
            InstantiateDisplay(group.Assets[index], exhibit.transform, group.DisplaySize, group.DisplayHeight);
            CreateText(group.Assets[index].name, exhibit.transform, new Vector3(0f, 0.7f, -group.CellSize * 0.38f), 0.035f, Color.white);
        }

        private static void CreatePedestal(Transform parent, float cellSize, Material material)
        {
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(parent, false);
            float size = Mathf.Max(2.2f, cellSize * 0.72f);
            pedestal.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            pedestal.transform.localScale = new Vector3(size, 0.3f, size);
            pedestal.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void InstantiateDisplay(GameObject asset, Transform parent, float maxSize, float maxHeight)
        {
            GameObject display = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (display == null)
                return;

            display.name = "Model";
            display.transform.SetParent(parent, false);
            ApplyTreeMaterials(display, asset);
            Bounds bounds = CalculateBounds(display);
            float factor = CalculateScaleFactor(bounds, maxSize, maxHeight);
            display.transform.localScale *= factor;
            bounds = CalculateBounds(display);
            display.transform.position += Vector3.up * (0.34f - bounds.min.y);
        }

        private static void ApplyTreeMaterials(GameObject display, GameObject asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!assetPath.StartsWith(TreeFolder, StringComparison.OrdinalIgnoreCase))
                return;

            Renderer[] renderers = display.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                ApplyTreeRendererMaterials(renderer, asset.name);
        }

        private static void ApplyTreeRendererMaterials(Renderer renderer, string assetName)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                string materialName = materials[index] != null ? materials[index].name : string.Empty;
                bool isBark = materialName.ToLowerInvariant().Contains("bark") || index == 0;
                materials[index] = GetTreeMaterial(assetName, isBark);
            }

            renderer.sharedMaterials = materials;
        }

        private static Material GetTreeMaterial(string assetName, bool isBark)
        {
            string name = assetName.ToLowerInvariant();
            if (name.Contains("deadbirch"))
                return GetOrCreateTexturedMaterial("Tree_DeadBirchBark", "Color Variations/Birch_Bark_Dead.png", false);
            if (name.Contains("deadtree"))
                return GetOrCreateTexturedMaterial("Tree_DeadBark", "Color Variations/Bark_Dead.png", false);
            if (name.Contains("birch"))
                return isBark
                    ? GetOrCreateTexturedMaterial("Tree_BirchBark", "Birch_Bark.png", false)
                    : GetOrCreateTexturedMaterial("Tree_BirchLeaves", "Birch_Leaves_Green.png", true);
            if (name.Contains("pine"))
                return isBark
                    ? GetOrCreateTexturedMaterial("Tree_Bark", "Tree_Bark.jpg", false)
                    : GetOrCreateTexturedMaterial("Tree_PineLeaves", "Pine_Leaves.png", true);
            return isBark
                ? GetOrCreateTexturedMaterial("Tree_Bark", "Tree_Bark.jpg", false)
                : GetOrCreateTexturedMaterial("Tree_Leaves", "Tree_Leaves.png", true);
        }

        private static Material GetOrCreateTexturedMaterial(string name, string textureName, bool alphaClipped)
        {
            Material material = GetOrCreateMaterial(name, Color.white);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TreeTextureFolder}/{textureName}");
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_AlphaClip", alphaClipped ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.4f);
            material.SetFloat("_Cull", alphaClipped ? 0f : 2f);
            if (alphaClipped)
                material.EnableKeyword("_ALPHATEST_ON");
            else
                material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float CalculateScaleFactor(Bounds bounds, float maxSize, float maxHeight)
        {
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            float horizontalFactor = width > 0.001f ? maxSize / width : 1f;
            float verticalFactor = bounds.size.y > 0.001f ? maxHeight / bounds.size.y : 1f;
            return Mathf.Clamp(Mathf.Min(horizontalFactor, verticalFactor), 0.02f, 25f);
        }

        private static void BuildGround(float museumDepth)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Museum Ground";
            ground.transform.position = new Vector3(0f, -0.25f, museumDepth * 0.5f - 6f);
            ground.transform.localScale = new Vector3(260f, 0.5f, museumDepth + 20f);
            ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Ground", new Color(0.11f, 0.13f, 0.14f));
        }

        private static void BuildEntrance()
        {
            GameObject entrance = new("Entrance");
            CreateText("ASSET MUSEUM", entrance.transform, new Vector3(0f, 3.4f, 2f), 0.18f, Color.white);
            CreateText("WASD move  |  Mouse look  |  Shift sprint  |  Space jump", entrance.transform, new Vector3(0f, 2.2f, 2f), 0.055f, new Color(0.75f, 0.85f, 0.90f));
        }

        private static void BuildLighting()
        {
            GameObject lightObject = new("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.48f);
        }

        private static void BuildPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (player == null)
                throw new InvalidOperationException($"Player prefab is missing at {PlayerPrefabPath}.");

            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.1f, -5f);
            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            InteractionSystem interaction = player.GetComponent<InteractionSystem>();
            GameObject uiModeObject = new("UI Mode Service");
            UIModeService uiMode = uiModeObject.AddComponent<UIModeService>();
            SetObjectReference(uiMode, "playerController", controller);
            SetObjectReference(uiMode, "interactionSystem", interaction);
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateText(
            string value,
            Transform parent,
            Vector3 localPosition,
            float characterSize,
            Color color)
        {
            GameObject label = new($"Label - {value}");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static List<GameObject> LoadAssets(string folder, string extension)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            var assets = new List<GameObject>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || !seenPaths.Add(path))
                    continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            assets.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            return assets;
        }

        private static List<GameObject> TakeByName(List<GameObject> assets, Func<string, bool> predicate)
        {
            var result = new List<GameObject>();
            foreach (GameObject asset in assets)
            {
                if (predicate(asset.name.ToLowerInvariant()))
                    result.Add(asset);
            }

            return result;
        }

        private static void AddGroup(
            List<ExhibitGroup> groups,
            string name,
            List<GameObject> assets,
            float cellSize,
            float displaySize,
            float displayHeight,
            Color color)
        {
            if (assets.Count > 0)
                groups.Add(new ExhibitGroup(name, assets, cellSize, displaySize, displayHeight, color));
        }

        private static int CountAssets(List<ExhibitGroup> groups)
        {
            int count = 0;
            foreach (ExhibitGroup group in groups)
                count += group.Assets.Count;
            return count;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "AssetMuseum");
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
