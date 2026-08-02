using System.Collections.Generic;
using Market.DebugTools;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Adds the authored meadow, habitat frame, physical lab sign, neutral post profile and
    /// eye-level camera setup that turn GrassLab from a scatter test into a useful beauty scene.
    /// Re-running replaces only its own root and preserves hand-painted GrassScatter content.
    /// </summary>
    public static class GrassLabVisualUpgrade
    {
        private const string SceneName = "GrassLab";
        private const string VisualRootName = "Grass Lab Visual Upgrade";
        private const string FloatingLabelName = "Label - Grass Lab";
        private const string PlayerName = "Player";
        private const string SunName = "Sun";
        private const string PostProcessingName = "Global Post Processing";
        private const string GeneratedFolder = "Assets/_Project/Art/GrassLab";
        private const string SourcePostProfilePath =
            "Assets/_Project/Art/PostProcessing/MarketPostFX.asset";
        private const string LabPostProfilePath =
            GeneratedFolder + "/GrassLabPostFX.asset";
        private const string GeometryGrassMaterialPath =
            GeneratedFolder + "/LabGeometryGrass.mat";
        private const string FineGrassMeshPath =
            GeneratedFolder + "/LabFineGrassTuft.asset";
        private const string FineGrassPrefabPath =
            GeneratedFolder + "/LabFineGrassTuft.prefab";
        private const int MeadowClumpCount = 960;
        private const int FineTuftCount = 260;
        private const int BladesPerFineTuft = 14;

        private static readonly string[] TreePrefabs =
        {
            "Assets/SimpleNaturePack/Prefabs/Tree_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_03.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_04.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_05.prefab"
        };

        private static readonly string[] BushPrefabs =
        {
            "Assets/SimpleNaturePack/Prefabs/Bush_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Bush_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Bush_03.prefab"
        };

        private static readonly string[] RockPrefabs =
        {
            "Assets/SimpleNaturePack/Prefabs/Rock_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_03.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_04.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_05.prefab"
        };

        private static readonly string[] FlowerPrefabs =
        {
            "Assets/SimpleNaturePack/Prefabs/Flowers_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Flowers_02.prefab"
        };

        private static readonly HabitatPlacement[] TreePlacements =
        {
            new(-36f, 13f, 1.35f, 18f, 0),
            new(-31f, 29f, 1.65f, 132f, 2),
            new(-17f, 40f, 1.45f, 248f, 4),
            new(-2f, 45f, 1.2f, 75f, 1),
            new(14f, 43f, 1.5f, 194f, 3),
            new(29f, 34f, 1.55f, 302f, 0),
            new(37f, 18f, 1.35f, 94f, 4),
            new(41f, -2f, 1.2f, 215f, 1)
        };

        private static readonly HabitatPlacement[] BushPlacements =
        {
            new(-22f, 5f, 1.35f, 34f, 1),
            new(-25f, 12f, 1.1f, 178f, 2),
            new(-19f, 23f, 1.4f, 291f, 0),
            new(-9f, 29f, 1.2f, 112f, 2),
            new(8f, 31f, 1.35f, 245f, 1),
            new(20f, 26f, 1.15f, 18f, 0),
            new(25f, 17f, 1.45f, 166f, 2),
            new(23f, 7f, 1.25f, 320f, 1),
            new(17f, -1f, 1.1f, 72f, 0)
        };

        private static readonly HabitatPlacement[] RockPlacements =
        {
            new(-18f, -7f, 1.1f, 24f, 2),
            new(-20f, -1f, 0.8f, 146f, 0),
            new(-21f, 9f, 1.25f, 278f, 4),
            new(-18f, 18f, 0.95f, 81f, 1),
            new(-14f, 27f, 1.35f, 205f, 3),
            new(16f, 25f, 1.05f, 319f, 0),
            new(21f, 15f, 1.4f, 107f, 4),
            new(20f, 5f, 0.85f, 252f, 1),
            new(16f, -6f, 1.2f, 43f, 2)
        };

        private readonly struct HabitatPlacement
        {
            public HabitatPlacement(float x, float z, float scale, float yaw, int variant)
            {
                X = x;
                Z = z;
                Scale = scale;
                Yaw = yaw;
                Variant = variant;
            }

            public float X { get; }
            public float Z { get; }
            public float Scale { get; }
            public float Yaw { get; }
            public int Variant { get; }
        }

        /// <summary>Applies and saves the visual upgrade in the currently open GrassLab scene.</summary>
        [MenuItem("Market/Debug/Grass Lab/Apply Visual Upgrade")]
        public static void ApplyToOpenLab()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName)
            {
                Debug.LogError("[GrassLabVisualUpgrade] Open GrassLab before applying the upgrade.");
                return;
            }

            Terrain terrain = FindSceneComponent<Terrain>(scene);
            if (terrain == null)
            {
                Debug.LogError("[GrassLabVisualUpgrade] GrassLab has no Terrain.");
                return;
            }

            Build(terrain);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[GrassLabVisualUpgrade] Applied the meadow beauty pass and saved GrassLab.");
        }

        /// <summary>Toggles the preserved scale and card-reference diagnostics in the open lab.</summary>
        [MenuItem("Market/Debug/Grass Lab/Toggle Diagnostics")]
        public static void ToggleDiagnostics()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject visualRoot = FindSceneRoot(scene, VisualRootName);
            GrassLabPresentationToggle toggle =
                visualRoot != null
                    ? visualRoot.GetComponent<GrassLabPresentationToggle>()
                    : null;
            if (toggle == null)
            {
                Debug.LogError("[GrassLabVisualUpgrade] Apply the visual upgrade before toggling diagnostics.");
                return;
            }

            toggle.ToggleDiagnostics();
            EditorUtility.SetDirty(toggle);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Builds the visual upgrade into the terrain's scene without saving it. Scene builders can
        /// call this once their core lab objects have been created.
        /// </summary>
        public static void Build(Terrain terrain)
        {
            Scene scene = terrain.gameObject.scene;
            EnsureFolder(GeneratedFolder);
            RemovePreviousVisualRoot(scene);
            DisableFloatingLabel(scene);
            ConfigureTerrain(terrain);
            ConfigureLighting(scene);
            ConfigurePostProcessing(scene);
            ConfigurePlayerView(scene, terrain);

            var root = new GameObject(VisualRootName);
            ConfigurePresentation(scene, root);
            BuildShowcaseMeadow(root.transform, terrain);
            BuildFineBladeLayer(root.transform, terrain);
            BuildHabitatFrame(root.transform, terrain);
            BuildFlowerPatches(root.transform, terrain);
            BuildPhysicalSign(root.transform, terrain);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void RemovePreviousVisualRoot(Scene scene)
        {
            GameObject existing = FindSceneRoot(scene, VisualRootName);
            if (existing != null)
                UObject.DestroyImmediate(existing);
        }

        private static void DisableFloatingLabel(Scene scene)
        {
            GameObject floatingLabel = FindSceneRoot(scene, FloatingLabelName);
            if (floatingLabel != null)
                floatingLabel.SetActive(false);
        }

        private static void ConfigurePresentation(Scene scene, GameObject visualRoot)
        {
            var diagnosticRoots = new List<GameObject>();
            AddDiagnosticRoot(scene, "Card Reference Row", diagnosticRoots);
            AddDiagnosticRoot(scene, "Scale Reference 1.8m", diagnosticRoots);

            GrassLabPresentationToggle toggle =
                visualRoot.AddComponent<GrassLabPresentationToggle>();
            toggle.Configure(diagnosticRoots.ToArray(), false);
        }

        private static void AddDiagnosticRoot(
            Scene scene,
            string rootName,
            List<GameObject> diagnosticRoots)
        {
            GameObject root = FindSceneRoot(scene, rootName);
            if (root != null)
                diagnosticRoots.Add(root);
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 140f;
            terrain.detailObjectDistance = 65f;
            terrain.treeDistance = 120f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
        }

        private static void ConfigureLighting(Scene scene)
        {
            Light sun = FindNamedSceneComponent<Light>(scene, SunName);
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.94f, 0.82f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(47f, -32f, 0f);
                RenderSettings.sun = sun;
                EditorUtility.SetDirty(sun);
            }

            RenderSettings.skybox =
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.79f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.59f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.34f, 0.24f);
            RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.63f, 0.75f, 0.82f);
            RenderSettings.fogDensity = 0.0055f;
            DynamicGI.UpdateEnvironment();
        }

        private static void ConfigurePostProcessing(Scene scene)
        {
            Volume volume = FindNamedSceneComponent<Volume>(scene, PostProcessingName);
            VolumeProfile profile = GetOrCreatePostProfile();
            if (volume != null && profile != null)
            {
                volume.sharedProfile = profile;
                EditorUtility.SetDirty(volume);
            }
        }

        private static VolumeProfile GetOrCreatePostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LabPostProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePostProfilePath, LabPostProfilePath))
                {
                    Debug.LogWarning("[GrassLabVisualUpgrade] Could not create the lab post profile.");
                    return null;
                }

                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LabPostProfilePath);
            }

            TunePostProfile(profile);
            return profile;
        }

        private static void TunePostProfile(VolumeProfile profile)
        {
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(0f);
                color.contrast.Override(10f);
                color.saturation.Override(2f);
                color.colorFilter.Override(new Color(1f, 0.99f, 0.96f));
                EditorUtility.SetDirty(color);
            }

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.intensity.Override(0.18f);
                bloom.threshold.Override(1.15f);
                EditorUtility.SetDirty(bloom);
            }

            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.11f);
                vignette.smoothness.Override(0.35f);
                EditorUtility.SetDirty(vignette);
            }

            EditorUtility.SetDirty(profile);
        }

        private static void ConfigurePlayerView(Scene scene, Terrain terrain)
        {
            GameObject player = FindSceneRoot(scene, PlayerName);
            if (player == null)
                return;

            player.transform.SetPositionAndRotation(
                Ground(terrain, 0f, -6f) + Vector3.up * 0.03f,
                Quaternion.identity);

            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.fieldOfView = 64f;
                camera.nearClipPlane = 0.08f;
                EditorUtility.SetDirty(camera);
            }
        }

        private static void BuildShowcaseMeadow(Transform parent, Terrain terrain)
        {
            List<GameObject> singles = GrassCardBuilder.LoadPalettePrefabs(false);
            List<GameObject> crosses = GrassCardBuilder.LoadPalettePrefabs(true);
            if (singles.Count == 0)
            {
                Debug.LogWarning("[GrassLabVisualUpgrade] Grass card prefabs are missing; meadow skipped.");
                return;
            }

            var meadow = new GameObject("Authored Meadow");
            meadow.transform.SetParent(parent, false);
            Random.State previousState = Random.state;
            Random.InitState(41873);

            try
            {
                ScatterMeadowClumps(meadow.transform, terrain, singles, crosses);
            }
            finally
            {
                Random.state = previousState;
            }
        }

        private static void ScatterMeadowClumps(
            Transform parent,
            Terrain terrain,
            List<GameObject> singles,
            List<GameObject> crosses)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < MeadowClumpCount && attempts < MeadowClumpCount * 4)
            {
                attempts++;
                float x = Random.Range(-22f, 22f);
                float z = Random.Range(-10f, 13f);
                if (!AcceptMeadowPoint(x, z))
                    continue;

                float distance = Mathf.InverseLerp(-10f, 13f, z);
                bool useCross = crosses.Count > 0 && Random.value < Mathf.Lerp(0.58f, 0.8f, distance);
                List<GameObject> palette = useCross ? crosses : singles;
                GameObject prefab = palette[Random.Range(0, palette.Count)];
                PlaceGrassClump(parent, terrain, prefab, x, z);
                placed++;
            }
        }

        private static bool AcceptMeadowPoint(float x, float z)
        {
            float pathCenter = -15f + Mathf.Sin(z * 0.06f) * 6f;
            if (Mathf.Abs(x - pathCenter) < 2.35f)
                return false;
            if (new Vector2(x, z + 6f).sqrMagnitude < 3.2f)
                return false;

            float density = Mathf.PerlinNoise((x + 31f) * 0.085f, (z + 19f) * 0.1f);
            float threshold = Mathf.Lerp(0.2f, 0.82f, density);
            return Random.value < threshold;
        }

        private static void PlaceGrassClump(
            Transform parent,
            Terrain terrain,
            GameObject prefab,
            float x,
            float z)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            float height = Random.Range(1.05f, 1.78f);
            float width = Random.Range(0.72f, 1.12f);
            Vector3 position = Ground(terrain, x, z) - Vector3.up * (0.025f * height);
            Vector3 normal = TerrainNormal(terrain, position);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion variation = Quaternion.Euler(
                Random.Range(-4f, 4f),
                Random.Range(0f, 360f),
                Random.Range(-4f, 4f));

            instance.transform.SetPositionAndRotation(position, slope * variation);
            instance.transform.localScale = new Vector3(width, height, width);
            ConfigureDecorRenderers(instance);
        }

        private static void BuildHabitatFrame(Transform parent, Terrain terrain)
        {
            var habitat = new GameObject("Habitat Frame");
            habitat.transform.SetParent(parent, false);
            PlaceSet(habitat.transform, terrain, TreePrefabs, TreePlacements, "Tree");
            PlaceSet(habitat.transform, terrain, BushPrefabs, BushPlacements, "Bush");
            PlaceSet(habitat.transform, terrain, RockPrefabs, RockPlacements, "Rock");
        }

        private static void BuildFineBladeLayer(Transform parent, Terrain terrain)
        {
            GameObject prefab = GetOrCreateFineGrassPrefab();
            if (prefab == null)
                return;

            var tufts = new GameObject("Fine Blade Layer");
            tufts.transform.SetParent(parent, false);
            Random.State previousState = Random.state;
            Random.InitState(7331);

            try
            {
                for (int index = 0; index < FineTuftCount; index++)
                    PlaceFineTuft(tufts.transform, terrain, prefab);
            }
            finally
            {
                Random.state = previousState;
            }
        }

        private static void PlaceFineTuft(
            Transform parent,
            Terrain terrain,
            GameObject prefab)
        {
            Vector2 point = FindFineTuftPoint();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Vector3 position = Ground(terrain, point.x, point.y);
            Vector3 normal = TerrainNormal(terrain, position);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion yaw = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
            instance.transform.SetPositionAndRotation(
                position - normal * 0.015f,
                yaw * slope);
            float height = Random.Range(0.78f, 1.18f);
            float width = Random.Range(0.85f, 1.2f);
            instance.transform.localScale = new Vector3(width, height, width);
        }

        private static Vector2 FindFineTuftPoint()
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float x = Random.Range(-20f, 20f);
                float z = Random.Range(-9f, 13f);
                float pathCenter = -15f + Mathf.Sin(z * 0.06f) * 6f;
                if (Mathf.Abs(x - pathCenter) > 2.5f &&
                    new Vector2(x, z + 6f).sqrMagnitude > 4f)
                    return new Vector2(x, z);
            }

            return new Vector2(0f, 8f);
        }

        private static void PlaceSet(
            Transform parent,
            Terrain terrain,
            string[] prefabPaths,
            HabitatPlacement[] placements,
            string label)
        {
            for (int index = 0; index < placements.Length; index++)
            {
                HabitatPlacement placement = placements[index];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPaths[placement.Variant % prefabPaths.Length]);
                if (prefab == null)
                    continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"{label} {index + 1:00}";
                instance.transform.SetPositionAndRotation(
                    Ground(terrain, placement.X, placement.Z),
                    Quaternion.Euler(0f, placement.Yaw, 0f));
                instance.transform.localScale = Vector3.one * placement.Scale;
                StripDecorColliders(instance);
                ConfigureDecorRenderers(instance);
            }
        }

        private static void BuildFlowerPatches(Transform parent, Terrain terrain)
        {
            var flowers = new GameObject("Wildflower Patches");
            flowers.transform.SetParent(parent, false);
            Vector2[] centers =
            {
                new(-7f, 1f),
                new(7f, 5f),
                new(-8f, 10f),
                new(11f, 11f)
            };

            Random.State previousState = Random.state;
            Random.InitState(90210);
            try
            {
                for (int patch = 0; patch < centers.Length; patch++)
                    BuildFlowerPatch(flowers.transform, terrain, centers[patch], patch);
            }
            finally
            {
                Random.state = previousState;
            }
        }

        private static void BuildFlowerPatch(
            Transform parent,
            Terrain terrain,
            Vector2 center,
            int patchIndex)
        {
            for (int index = 0; index < 9; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    FlowerPrefabs[(index + patchIndex) % FlowerPrefabs.Length]);
                if (prefab == null)
                    continue;

                Vector2 offset = Random.insideUnitCircle * Random.Range(0.8f, 2.6f);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"Wildflowers {patchIndex + 1}-{index + 1:00}";
                instance.transform.SetPositionAndRotation(
                    Ground(terrain, center.x + offset.x, center.y + offset.y),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                instance.transform.localScale = Vector3.one * Random.Range(0.8f, 1.25f);
                StripDecorColliders(instance);
                ConfigureDecorRenderers(instance);
            }
        }

        private static void BuildPhysicalSign(Transform parent, Terrain terrain)
        {
            var sign = new GameObject("Grass Lab Field Sign");
            sign.transform.SetParent(parent, false);
            sign.transform.position = Ground(terrain, -7.5f, 8.5f);
            sign.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            Material wood = GetOrCreateMaterial(
                "LabSignWood",
                new Color(0.23f, 0.12f, 0.055f),
                0.18f);
            Material trim = GetOrCreateMaterial(
                "LabSignTrim",
                new Color(0.73f, 0.46f, 0.16f),
                0.28f);

            CreatePrimitive(sign.transform, "Board", PrimitiveType.Cube,
                new Vector3(0f, 1.75f, 0f), new Vector3(4.4f, 1.35f, 0.14f), wood);
            CreatePrimitive(sign.transform, "Left Post", PrimitiveType.Cube,
                new Vector3(-1.7f, 0.85f, 0.04f), new Vector3(0.16f, 1.7f, 0.16f), trim);
            CreatePrimitive(sign.transform, "Right Post", PrimitiveType.Cube,
                new Vector3(1.7f, 0.85f, 0.04f), new Vector3(0.16f, 1.7f, 0.16f), trim);
            CreatePrimitive(sign.transform, "Top Trim", PrimitiveType.Cube,
                new Vector3(0f, 2.39f, -0.08f), new Vector3(4.55f, 0.1f, 0.08f), trim);
            CreateSignText(sign.transform, "GRASS LAB", 1.95f, 0.085f, Color.white);
            CreateSignText(sign.transform, "WIND  /  DENSITY  /  CONTACT",
                1.58f, 0.038f, new Color(0.76f, 0.94f, 0.66f));
            CreateSignText(sign.transform, "F6 = DIAGNOSTICS",
                1.38f, 0.025f, new Color(0.7f, 0.78f, 0.62f));
        }

        private static void CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject item = GameObject.CreatePrimitive(primitiveType);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            UObject.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static void CreateSignText(
            Transform parent,
            string value,
            float localY,
            float characterSize,
            Color color)
        {
            var item = new GameObject($"Label - {value}");
            item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(0f, localY, -0.081f);
            TextMesh text = item.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            float smoothness)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject GetOrCreateFineGrassPrefab()
        {
            Mesh mesh = GetOrCreateFineGrassMesh();
            Material material = GetOrCreateFineGrassMaterial();
            if (mesh == null || material == null)
                return null;

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FineGrassPrefabPath);
            if (existing != null)
                return existing;

            var root = new GameObject("LabFineGrassTuft");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FineGrassPrefabPath);
            UObject.DestroyImmediate(root);
            return prefab;
        }

        private static Mesh GetOrCreateFineGrassMesh()
        {
            var vertices = new List<Vector3>(BladesPerFineTuft * 6);
            var normals = new List<Vector3>(BladesPerFineTuft * 6);
            var uvs = new List<Vector2>(BladesPerFineTuft * 6);
            var colors = new List<Color>(BladesPerFineTuft * 6);
            var triangles = new List<int>(BladesPerFineTuft * 12);

            Random.State previousState = Random.state;
            Random.InitState(1447);
            try
            {
                for (int index = 0; index < BladesPerFineTuft; index++)
                    AddFineBlade(vertices, normals, uvs, colors, triangles);
            }
            finally
            {
                Random.state = previousState;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FineGrassMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "LabFineGrassTuft" };
                AssetDatabase.CreateAsset(mesh, FineGrassMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddFineBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Random.value) * 0.17f;
            var center = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float facing = Random.Range(0f, Mathf.PI * 2f);
            var right = new Vector3(Mathf.Cos(facing), 0f, -Mathf.Sin(facing));
            var normal = new Vector3(Mathf.Sin(facing), 0f, Mathf.Cos(facing));
            var lean = normal * Random.Range(-0.055f, 0.055f);
            float height = Random.Range(0.18f, 0.42f);
            float halfWidth = Random.Range(0.012f, 0.027f);
            int start = vertices.Count;

            vertices.Add(center - right * halfWidth);
            vertices.Add(center + right * halfWidth);
            vertices.Add(center + Vector3.up * (height * 0.58f) + lean * 0.35f - right * halfWidth * 0.7f);
            vertices.Add(center + Vector3.up * (height * 0.58f) + lean * 0.35f + right * halfWidth * 0.7f);
            vertices.Add(center + Vector3.up * height + lean - right * halfWidth * 0.1f);
            vertices.Add(center + Vector3.up * height + lean + right * halfWidth * 0.1f);
            for (int index = 0; index < 6; index++)
                normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 0.58f));
            uvs.Add(new Vector2(1f, 0.58f));
            uvs.Add(new Vector2(0.5f, 1f));
            uvs.Add(new Vector2(0.5f, 1f));
            AddFineBladeColors(colors);
            triangles.AddRange(new[]
            {
                start, start + 2, start + 1,
                start + 1, start + 2, start + 3,
                start + 2, start + 4, start + 3,
                start + 3, start + 4, start + 5
            });
        }

        private static void AddFineBladeColors(List<Color> colors)
        {
            float variation = Random.Range(-0.08f, 0.08f);
            Color root = new Color(0.82f + variation, 0.92f, 0.72f - variation, 1f);
            Color tip = new Color(0.96f, 1f, 0.82f + variation, 1f);
            colors.Add(root);
            colors.Add(root);
            colors.Add(Color.Lerp(root, tip, 0.58f));
            colors.Add(Color.Lerp(root, tip, 0.58f));
            colors.Add(tip);
            colors.Add(tip);
        }

        private static Material GetOrCreateFineGrassMaterial()
        {
            Shader shader = Shader.Find("Market/Nature/GrassWind");
            if (shader == null)
                return null;

            var material = AssetDatabase.LoadAssetAtPath<Material>(GeometryGrassMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GeometryGrassMaterialPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", new Color(0.38f, 0.62f, 0.22f, 1f));
            material.SetColor("_TipColor", new Color(0.68f, 0.84f, 0.32f, 1f));
            material.SetFloat("_Cutoff", 0.1f);
            material.SetFloat("_WindMaskFromUV", 1f);
            material.EnableKeyword("_WINDMASK_UV");
            material.SetFloat("_VertexColorTint", 0.55f);
            material.SetFloat("_ColorSaturation", 0.78f);
            material.SetFloat("_ColorVariation", 0.7f);
            material.SetFloat("_PatchVariation", 0.12f);
            material.SetFloat("_RootDarkening", 0.22f);
            material.SetFloat("_NormalSoftness", 0.38f);
            material.SetFloat("_ToonBands", 3f);
            material.SetFloat("_ToonSoftness", 0.3f);
            material.SetFloat("_WrapLighting", 0.2f);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Translucency", 0.9f);
            material.SetFloat("_RimStrength", 0.08f);
            material.SetFloat("_WindResponse", 0.9f);
            material.SetFloat("_BladeTipHeight", 0.002f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void StripDecorColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                UObject.DestroyImmediate(collider);
        }

        private static void ConfigureDecorRenderers(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private static Vector3 Ground(Terrain terrain, float x, float z)
        {
            var position = new Vector3(x, 0f, z);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static Vector3 TerrainNormal(Terrain terrain, Vector3 worldPosition)
        {
            Vector3 local = worldPosition - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return terrain.terrainData.GetInterpolatedNormal(
                Mathf.Clamp01(local.x / size.x),
                Mathf.Clamp01(local.z / size.z));
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static T FindNamedSceneComponent<T>(Scene scene, string rootName)
            where T : Component
        {
            GameObject root = FindSceneRoot(scene, rootName);
            return root != null ? root.GetComponent<T>() : null;
        }

        private static GameObject FindSceneRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
