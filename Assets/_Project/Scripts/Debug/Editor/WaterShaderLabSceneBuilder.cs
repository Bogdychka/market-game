using System;
using System.Collections.Generic;
using Market.DebugTools;
using Market.Interaction;
using Market.Player;
using Market.UI;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds a small standalone scene for iterating on the experimental realistic water shader in
    /// isolation from the full Island terrain: a shoreline of stepped terraces (dry beach down to
    /// deep water) plus a few partially-submerged rocks, under <c>M_RealisticWaterLab</c>. Shader/
    /// material edits are visible immediately.
    /// </summary>
    public static class WaterShaderLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/WaterShaderLab.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Art/Prefabs/Player/Player.prefab";
        private const string RealisticWaterMeshPath = "Assets/_Project/Art/Meshes/Water/RealisticWaterGrid.asset";
        private const string RealisticWaterMaterialPath = "Assets/_Project/Art/Materials/Water/M_RealisticWaterLab.mat";
        private const string TemporalFoamComputePath =
            "Assets/_Project/Art/Shaders/RealisticWaterFoamUpdate.compute";
        private const string GeneratedFolder = "Assets/_Project/Art/WaterShaderLab";
        private const string PostProcessingProfilePath =
            "Assets/_Project/Art/PostProcessing/MarketPostFX.asset";
        private const string CausticReceiverRootName =
            "Caustic Projection Receivers";
        private const string FeatureReceiverRootName =
            "Underwater Feature Receivers";
        private const string UnderwaterSurfaceName = "Underwater Surface";

        private const float TerraceWidth = 100f;
        private const float TerraceDepth = 20f;
        private const float TerraceThickness = 4f;

        private readonly struct Terrace
        {
            public Terrace(string name, float topY, Color color)
            {
                Name = name;
                TopY = topY;
                Color = color;
            }

            public string Name { get; }
            public float TopY { get; }
            public Color Color { get; }
        }

        private static readonly Terrace[] Terraces =
        {
            new("Beach", 0.4f, new Color(0.80f, 0.72f, 0.50f)),
            new("Shallows", -0.6f, new Color(0.70f, 0.64f, 0.46f)),
            new("Shallow Shelf", -2.5f, new Color(0.45f, 0.52f, 0.42f)),
            new("Mid Shelf", -6f, new Color(0.30f, 0.36f, 0.34f)),
            new("Deep Trench", -14f, new Color(0.14f, 0.18f, 0.20f)),
        };

        /// <summary>
        /// Rebuilds and opens the standalone WaterShaderLab scene.
        /// </summary>
        [MenuItem("Market/Debug/Build Water Shader Lab")]
        public static void BuildWaterShaderLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureGeneratedFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WaterShaderLab";

            BuildSeabed();
            BuildLabArchitecture();
            BuildFoamTestRocks();
            BuildFeatureStations();
            RealisticWaterMaterialInstaller.CreateMaterial();
            GameObject water = BuildRealisticWater();
            BuildProjectedCaustics(scene, water);
            BuildUnderwaterSurface(water);
            BuildQualityController(water);
            BuildStandaloneCapture(water);
            BuildUnderwaterFog(water);
            BuildLighting();
            TextMesh weatherStatus = BuildLabel();
            BuildWeatherController(water, weatherStatus);
            BuildPostProcessing();
            BuildPlayer();
            // After the player: the wall is hung relative to the spawn and turned to face it.
            WaterSettingsWallBuilder.Build();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WaterShaderLabSceneBuilder] Built {ScenePath}. Edit RealisticWater.shader / M_RealisticWaterLab.mat and use Scene view or Play to iterate.");
        }

        private static void BuildSeabed()
        {
            GameObject seabed = new("Seabed");
            float z = -Terraces.Length * TerraceDepth * 0.5f;
            foreach (Terrace terrace in Terraces)
            {
                BuildTerrace(seabed.transform, terrace, z + TerraceDepth * 0.5f);
                z += TerraceDepth;
            }

            BuildBeachSlope(seabed.transform);
        }

        /// <summary>
        /// A gentle ramp laid over the Beach/Shallows step. The terraces are useful for reading
        /// depth-based effects at known depths, but their risers are vertical: the waterline ends up
        /// tucked behind a 1 m lip where no camera can see it, and there is nowhere for a surf band
        /// to sit. The ramp gives the shoreline somewhere to actually happen.
        /// </summary>
        private static void BuildBeachSlope(Transform parent)
        {
            const float RampLength = 26f;
            const float RampDrop = 2.6f;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Beach Slope";
            ramp.transform.SetParent(parent, false);
            // Centred on the Beach/Shallows boundary so the ramp crosses the water line.
            ramp.transform.localPosition = new Vector3(0f, -1.6f, -30f);
            ramp.transform.localRotation = Quaternion.Euler(
                -Mathf.Atan2(RampDrop, RampLength) * Mathf.Rad2Deg, 0f, 0f);
            ramp.transform.localScale = new Vector3(
                TerraceWidth, 3.2f, Mathf.Sqrt(RampLength * RampLength + RampDrop * RampDrop));
            ramp.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("Terrace_Beach", Terraces[0].Color);
        }

        private static void BuildTerrace(Transform parent, Terrace terrace, float centerZ)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = terrace.Name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = new Vector3(0f, terrace.TopY - TerraceThickness * 0.5f, centerZ);
            block.transform.localScale = new Vector3(TerraceWidth, TerraceThickness, TerraceDepth);
            block.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"Terrace_{terrace.Name}", terrace.Color);
        }

        private static void BuildFoamTestRocks()
        {
            GameObject rocks = new("Foam Test Rocks");
            Material material = GetOrCreateMaterial(
                "FoamTestRock", new Color(0.24f, 0.27f, 0.29f), 0.22f);
            Vector3[] positions =
            {
                new(-15f, -0.15f, -19f),
                new(-8f, -0.35f, -15f),
                new(-3f, -0.7f, -3f),
                new(8f, -1.1f, 7f),
            };
            Vector3[] scales =
            {
                new(3.8f, 2.1f, 3.1f),
                new(2.7f, 1.6f, 3.4f),
                new(3.1f, 2.4f, 2.6f),
                new(4.2f, 2.8f, 3.2f),
            };
            float[] rotations = { 18f, 52f, 81f, 127f };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject rock = CreatePrimitive(
                    PrimitiveType.Sphere,
                    rocks.transform,
                    $"Contact Rock {i + 1}",
                    positions[i],
                    scales[i],
                    material);
                rock.transform.localRotation =
                    Quaternion.Euler(8f, rotations[i], 5f);
            }
        }

        private static void BuildLabArchitecture()
        {
            GameObject root = new("Lab Observation Deck");
            Material wood = GetOrCreateMaterial(
                "LabDeckWood", new Color(0.23f, 0.13f, 0.07f), 0.16f);
            Material metal = GetOrCreateMaterial(
                "LabDeckMetal", new Color(0.16f, 0.21f, 0.24f), 0.55f, 0.35f);

            CreateBox(
                root.transform, "Observation Platform",
                new Vector3(0f, 2.25f, -43f),
                new Vector3(18f, 0.5f, 8f), wood);
            BuildDeckSupports(root.transform, metal);
            BuildDeckRails(root.transform, metal);
            BuildDeckStairs(root.transform, wood);
            BuildEntryFrame(root.transform, metal);
        }

        private static void BuildDeckSupports(Transform parent, Material material)
        {
            Vector3[] positions =
            {
                new(-8f, 0.9f, -46f),
                new(8f, 0.9f, -46f),
                new(-8f, 0.9f, -40f),
                new(8f, 0.9f, -40f),
            };

            foreach (Vector3 position in positions)
            {
                CreateBox(
                    parent, "Deck Support", position,
                    new Vector3(0.45f, 3.2f, 0.45f), material);
            }
        }

        private static void BuildDeckRails(Transform parent, Material material)
        {
            CreateBox(
                parent, "Left Rail", new Vector3(-8.55f, 3.35f, -43f),
                new Vector3(0.18f, 1.7f, 7.6f), material);
            CreateBox(
                parent, "Right Rail", new Vector3(8.55f, 3.35f, -43f),
                new Vector3(0.18f, 1.7f, 7.6f), material);
            CreateBox(
                parent, "Rear Rail", new Vector3(0f, 3.35f, -46.85f),
                new Vector3(17.2f, 1.7f, 0.18f), material);
        }

        private static void BuildDeckStairs(Transform parent, Material material)
        {
            const int StepCount = 6;
            for (int i = 0; i < StepCount; i++)
            {
                float top = 2.25f - i * 0.34f;
                CreateBox(
                    parent,
                    $"Deck Step {i + 1}",
                    new Vector3(0f, top - 0.16f, -38.3f + i * 1.15f),
                    new Vector3(6f, 0.32f, 1.35f),
                    material);
            }
        }

        private static void BuildEntryFrame(Transform parent, Material material)
        {
            CreateBox(
                parent, "Entry Frame Left",
                new Vector3(-7.2f, 3.4f, -35.6f),
                new Vector3(0.35f, 5.8f, 0.35f), material);
            CreateBox(
                parent, "Entry Frame Right",
                new Vector3(7.2f, 3.4f, -35.6f),
                new Vector3(0.35f, 5.8f, 0.35f), material);
            CreateBox(
                parent, "Entry Frame Header",
                new Vector3(0f, 6.05f, -35.6f),
                new Vector3(14.75f, 0.5f, 0.35f), material);
        }

        private static void BuildFeatureStations()
        {
            GameObject root = new("Shader Feature Stations");
            GameObject receivers = new(FeatureReceiverRootName);
            receivers.transform.SetParent(root.transform, false);

            BuildDepthStation(root.transform, receivers.transform);
            BuildRefractionStation(root.transform);
            BuildReflectionStation(root.transform);
            BuildWaveStation(root.transform);
            BuildUnderwaterStation(root.transform, receivers.transform);
            BuildFeatureSigns(root.transform);
        }

        private static void BuildDepthStation(
            Transform parent, Transform receiverParent)
        {
            Material tile = GetOrCreateMaterial(
                "LabDepthTile", new Color(0.78f, 0.82f, 0.80f), 0.42f);
            Vector3[] positions =
            {
                new(-25f, -0.48f, -17f),
                new(-25f, -2.38f, -2f),
                new(-25f, -5.88f, 17f),
                new(-25f, -13.88f, 37f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                CreateBox(
                    receiverParent, $"Depth Tile {i + 1}", positions[i],
                    new Vector3(10f, 0.18f, 6f), tile);
            }

            BuildDepthBuoys(parent);
        }

        private static void BuildDepthBuoys(Transform parent)
        {
            Material light = GetOrCreateMaterial(
                "LabStripeLight", new Color(0.88f, 0.9f, 0.87f), 0.28f);
            Material dark = GetOrCreateMaterial(
                "LabStripeDark", new Color(0.04f, 0.08f, 0.11f), 0.35f);
            float[] zPositions = { -17f, -2f, 17f, 37f };
            for (int i = 0; i < zPositions.Length; i++)
            {
                CreateStripedColumn(
                    parent, $"Depth Marker {i + 1}",
                    new Vector3(-31f, -1.2f, zPositions[i]),
                    3.8f, 8, 0.42f, light, dark, 0f);
            }
        }

        private static void BuildRefractionStation(Transform parent)
        {
            Material light = GetOrCreateMaterial(
                "LabRefractionLight", new Color(0.92f, 0.93f, 0.86f), 0.35f);
            Material dark = GetOrCreateMaterial(
                "LabRefractionDark", new Color(0.025f, 0.06f, 0.1f), 0.4f);
            float[] xPositions = { 15f, 20f, 25f, 30f };
            float[] tilts = { -8f, 5f, -4f, 9f };

            for (int i = 0; i < xPositions.Length; i++)
            {
                CreateStripedColumn(
                    parent, $"Refraction Column {i + 1}",
                    new Vector3(xPositions[i], -2.35f, -2f),
                    6.2f, 10, 0.62f, light, dark, tilts[i]);
            }
        }

        private static void BuildReflectionStation(Transform parent)
        {
            GameObject root = new("Reflection Beacons");
            root.transform.SetParent(parent, false);
            Material metal = GetOrCreateMaterial(
                "LabBeaconMetal", new Color(0.08f, 0.12f, 0.15f), 0.72f, 0.65f);
            Material cyan = GetOrCreateEmissiveMaterial(
                "LabBeaconCyan", new Color(0.03f, 0.42f, 0.58f),
                new Color(0.1f, 1.6f, 2.4f));
            Material coral = GetOrCreateEmissiveMaterial(
                "LabBeaconCoral", new Color(0.72f, 0.16f, 0.08f),
                new Color(2.6f, 0.35f, 0.12f));

            BuildReflectionBeacon(root.transform, new Vector3(16f, -1.5f, 14f), metal, cyan);
            BuildReflectionBeacon(root.transform, new Vector3(24f, -1.5f, 14f), metal, coral);
            BuildReflectionBeacon(root.transform, new Vector3(32f, -1.5f, 14f), metal, cyan);
            CreateBox(
                root.transform, "Beacon Crossbar", new Vector3(24f, 3f, 14f),
                new Vector3(16.5f, 0.35f, 0.45f), metal);
        }

        private static void BuildReflectionBeacon(
            Transform parent, Vector3 position, Material stem, Material lamp)
        {
            CreateCylinder(
                parent, "Beacon Stem", position,
                new Vector3(0.42f, 4.5f, 0.42f), stem);
            CreatePrimitive(
                PrimitiveType.Sphere, parent, "Beacon Lamp",
                position + Vector3.up * 4.5f,
                new Vector3(1.25f, 1.25f, 1.25f), lamp);
        }

        private static void BuildWaveStation(Transform parent)
        {
            GameObject root = new("Wave and Contact Gauges");
            root.transform.SetParent(parent, false);
            Material light = GetOrCreateMaterial(
                "LabWaveGaugeLight", new Color(0.88f, 0.9f, 0.86f), 0.28f);
            Material dark = GetOrCreateMaterial(
                "LabWaveGaugeDark", new Color(0.08f, 0.12f, 0.14f), 0.45f);
            float[] xPositions = { -12f, -4f, 4f, 12f };

            for (int i = 0; i < xPositions.Length; i++)
            {
                CreateStripedColumn(
                    root.transform, $"Wave Gauge {i + 1}",
                    new Vector3(xPositions[i], -1.35f, 26f),
                    4.4f, 8, 0.5f, light, dark, 0f);
            }
        }

        private static void BuildUnderwaterStation(
            Transform parent, Transform receiverParent)
        {
            Material stone = GetOrCreateMaterial(
                "LabUnderwaterStone", new Color(0.18f, 0.28f, 0.31f), 0.3f);
            Material path = GetOrCreateMaterial(
                "LabUnderwaterPath", new Color(0.72f, 0.76f, 0.7f), 0.38f);
            Material lamp = GetOrCreateEmissiveMaterial(
                "LabUnderwaterLamp", new Color(0.02f, 0.35f, 0.4f),
                new Color(0.06f, 1.8f, 2.2f));

            CreateBox(
                receiverParent, "Underwater Gallery Floor",
                new Vector3(0f, -13.86f, 40f),
                new Vector3(14f, 0.2f, 28f), path);
            float[] zPositions = { 33f, 40f, 47f };
            foreach (float z in zPositions)
                BuildUnderwaterArch(parent, z, stone, lamp);
        }

        private static void BuildUnderwaterArch(
            Transform parent, float z, Material stone, Material lamp)
        {
            CreateBox(
                parent, "Underwater Arch Left",
                new Vector3(-5.5f, -8.45f, z),
                new Vector3(0.7f, 10.8f, 0.9f), stone);
            CreateBox(
                parent, "Underwater Arch Right",
                new Vector3(5.5f, -8.45f, z),
                new Vector3(0.7f, 10.8f, 0.9f), stone);
            CreateBox(
                parent, "Underwater Arch Top",
                new Vector3(0f, -3.1f, z),
                new Vector3(11.7f, 0.7f, 0.9f), stone);
            CreatePrimitive(
                PrimitiveType.Sphere, parent, "Underwater Lamp",
                new Vector3(0f, -2.6f, z),
                new Vector3(0.75f, 0.75f, 0.75f), lamp);
        }

        private static void BuildFeatureSigns(Transform parent)
        {
            CreateFeatureSign(
                parent, "Shore Sign", new Vector3(-22f, 3.4f, -24f), 15f,
                "SHORE + CONTACT FOAM", "Slope, rocks and wave shoaling");
            CreateFeatureSign(
                parent, "Depth Sign", new Vector3(-25f, 3.4f, -9f), 13f,
                "DEPTH + CAUSTICS", "Four calibrated receiver shelves");
            CreateFeatureSign(
                parent, "Refraction Sign", new Vector3(23f, 4.2f, -8f), 13f,
                "REFRACTION", "Striped columns cross the surface");
            CreateFeatureSign(
                parent, "Reflection Sign", new Vector3(24f, 5.2f, 9f), 13f,
                "PLANAR REFLECTION", "Bright beacons expose stability");
            CreateFeatureSign(
                parent, "Wave Sign", new Vector3(0f, 4.2f, 20f), 12f,
                "WAVES + WHITECAPS", "Gauges expose displacement and foam");
            CreateFeatureSign(
                parent, "Underwater Sign", new Vector3(0f, 3.2f, 31f), 12f,
                "UNDERWATER GALLERY", "F4 fly, Left Ctrl descend");
        }

        private static GameObject BuildRealisticWater()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RealisticWaterMeshPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(RealisticWaterMaterialPath);
            if (mesh == null || material == null)
            {
                Debug.LogWarning(
                    "[WaterShaderLabSceneBuilder] Realistic water mesh/material missing - run " +
                    "'Market/Debug/Water/Generate Realistic Water Mesh' and " +
                    "'Market/Debug/Water/Create Realistic Water Material' first. Skipping the water plane.");
                return null;
            }

            GameObject water = new("Water");
            water.transform.position = Vector3.zero;
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureWaterRenderer(renderer);
            RealisticWaterTemporalFoam foam =
                water.AddComponent<RealisticWaterTemporalFoam>();
            ConfigureTemporalFoam(foam);
            water.AddComponent<RealisticWaterPlanarReflection>();
            return water;
        }

        /// <summary>
        /// Adds the R5 local planar reflection to the currently open WaterShaderLab scene.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install R5 Planar Reflection")]
        public static void InstallR5PlanarReflection()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] Open WaterShaderLab before installing R5.");
                return;
            }

            GameObject water = FindRoot(scene, "Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] The Water root is missing.");
                return;
            }

            if (water.GetComponent<RealisticWaterPlanarReflection>() == null)
                water.AddComponent<RealisticWaterPlanarReflection>();
            ConfigureWaterRenderer(water.GetComponent<MeshRenderer>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "[WaterShaderLabSceneBuilder] Installed the R5 half-resolution planar reflection.");
        }

        /// <summary>
        /// Adds the R6 temporal foam history to the currently open WaterShaderLab scene.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install R6 Temporal Foam")]
        public static void InstallR6TemporalFoam()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] Open WaterShaderLab before installing R6.");
                return;
            }

            GameObject water = FindRoot(scene, "Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] The Water root is missing.");
                return;
            }

            RealisticWaterTemporalFoam foam =
                water.GetComponent<RealisticWaterTemporalFoam>();
            if (foam == null)
                foam = water.AddComponent<RealisticWaterTemporalFoam>();
            ConfigureTemporalFoam(foam);
            ConfigureWaterRenderer(water.GetComponent<MeshRenderer>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "[WaterShaderLabSceneBuilder] Installed the R6 256x256 temporal foam history.");
        }

        /// <summary>
        /// Adds the R7 bounded world-space caustic receiver overlays to WaterShaderLab.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install R7 World Space Caustics")]
        public static void InstallR7WorldSpaceCaustics()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] Open WaterShaderLab before installing R7.");
                return;
            }

            GameObject water = FindRoot(scene, "Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] The Water root is missing.");
                return;
            }

            BuildProjectedCaustics(scene, water);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[WaterShaderLabSceneBuilder] Installed the R7 bounded projected caustics.");
        }

        /// <summary>
        /// Adds the R8 optional underside renderer and blended fog transition.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install R8 Underwater Surface")]
        public static void InstallR8UnderwaterSurface()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] Open WaterShaderLab before installing R8.");
                return;
            }

            GameObject water = FindRoot(scene, "Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] The Water root is missing.");
                return;
            }

            BuildUnderwaterSurface(water);
            GameObject fogObject = FindRoot(scene, "Underwater Fog Controller");
            UnderwaterFogController fog = fogObject != null
                ? fogObject.GetComponent<UnderwaterFogController>()
                : null;
            if (fog == null)
            {
                fogObject = new GameObject("Underwater Fog Controller");
                fog = fogObject.AddComponent<UnderwaterFogController>();
            }

            ConfigureUnderwaterFog(fog, water);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[WaterShaderLabSceneBuilder] Installed the R8 underwater surface and transition.");
        }

        /// <summary>
        /// Adds the coordinated R9 High/Low tiers and standalone capture hook.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install R9 Quality Tiers")]
        public static void InstallR9QualityTiers()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] Open WaterShaderLab before installing R9.");
                return;
            }

            GameObject water = FindRoot(scene, "Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterShaderLabSceneBuilder] The Water root is missing.");
                return;
            }

            RealisticWaterMaterialInstaller.CreateMaterial();
            BuildQualityController(water);
            BuildStandaloneCapture(water);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[WaterShaderLabSceneBuilder] Installed the R9 coordinated quality tiers.");
        }

        private static void ConfigureTemporalFoam(
            RealisticWaterTemporalFoam foam)
        {
            var serializedObject = new SerializedObject(foam);
            ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                TemporalFoamComputePath);
            if (compute == null)
            {
                Debug.LogError(
                    $"[WaterShaderLabSceneBuilder] Missing {TemporalFoamComputePath}.");
                return;
            }

            serializedObject.FindProperty("foamUpdateCompute").objectReferenceValue =
                compute;
            serializedObject.FindProperty("quality").enumValueIndex =
                (int)WaterFoamHistoryQuality.History256;
            serializedObject.FindProperty("whitecapDecayRate").floatValue = 1f;
            serializedObject.FindProperty("whitecapInjectionStrength").floatValue = 1f;
            serializedObject.FindProperty("shorelineDecayRate").floatValue = 0.9f;
            serializedObject.FindProperty("shorelineInjectionStrength").floatValue = 1f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWaterRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void BuildProjectedCaustics(Scene scene, GameObject water)
        {
            if (water == null)
                return;

            Material material =
                RealisticWaterMaterialInstaller.EnsureProjectedCausticMaterial();
            if (material == null)
                return;

            GameObject existingRoot = FindRoot(scene, CausticReceiverRootName);
            if (existingRoot != null)
                UnityEngine.Object.DestroyImmediate(existingRoot);

            GameObject receiverRoot = new(CausticReceiverRootName);
            List<MeshRenderer> sourceRenderers = CollectCausticSources(scene);
            var overlays = new List<Renderer>(sourceRenderers.Count);
            foreach (MeshRenderer sourceRenderer in sourceRenderers)
            {
                MeshRenderer overlay = BuildCausticOverlay(
                    receiverRoot.transform, sourceRenderer, material);
                if (overlay != null)
                    overlays.Add(overlay);
            }

            RealisticWaterCausticProjection projection =
                water.GetComponent<RealisticWaterCausticProjection>();
            if (projection == null)
                projection = water.AddComponent<RealisticWaterCausticProjection>();
            ConfigureCausticProjection(projection, receiverRoot, overlays);
        }

        private static List<MeshRenderer> CollectCausticSources(Scene scene)
        {
            var renderers = new List<MeshRenderer>();
            AddRenderers(FindRoot(scene, "Seabed"), renderers);
            AddRenderers(FindRoot(scene, "Foam Test Rocks"), renderers);
            GameObject stations = FindRoot(scene, "Shader Feature Stations");
            if (stations != null)
            {
                Transform featureReceivers =
                    stations.transform.Find(FeatureReceiverRootName);
                if (featureReceivers != null)
                {
                    renderers.AddRange(
                        featureReceivers.GetComponentsInChildren<MeshRenderer>(true));
                }
            }

            return renderers;
        }

        private static void AddRenderers(
            GameObject root, List<MeshRenderer> renderers)
        {
            if (root == null)
                return;

            renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
        }

        private static MeshRenderer BuildCausticOverlay(
            Transform parent, MeshRenderer source, Material material)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                return null;

            GameObject overlay = new($"{source.name} Caustics");
            overlay.transform.SetParent(parent, false);
            overlay.transform.SetPositionAndRotation(
                source.transform.position, source.transform.rotation);
            overlay.transform.localScale = source.transform.lossyScale;
            overlay.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureCausticRenderer(renderer);
            return renderer;
        }

        private static void ConfigureCausticRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void ConfigureCausticProjection(
            RealisticWaterCausticProjection projection,
            GameObject receiverRoot,
            List<Renderer> receivers)
        {
            var serializedObject = new SerializedObject(projection);
            serializedObject.FindProperty("receiverRoot").objectReferenceValue =
                receiverRoot;
            SerializedProperty receiverProperty =
                serializedObject.FindProperty("receiverRenderers");
            receiverProperty.arraySize = receivers.Count;
            for (int i = 0; i < receivers.Count; i++)
            {
                receiverProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                    receivers[i];
            }

            serializedObject.FindProperty("quality").enumValueIndex =
                (int)WaterCausticQuality.ProjectedReceivers;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            projection.RefreshProjection();
        }

        private static void BuildUnderwaterSurface(GameObject water)
        {
            if (water == null)
                return;

            Material material =
                RealisticWaterMaterialInstaller.EnsureUnderwaterSurfaceMaterial();
            MeshFilter sourceFilter = water.GetComponent<MeshFilter>();
            if (material == null ||
                sourceFilter == null ||
                sourceFilter.sharedMesh == null)
            {
                return;
            }

            Transform existing = water.transform.Find(UnderwaterSurfaceName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject surface = new(UnderwaterSurfaceName);
            surface.transform.SetParent(water.transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureWaterRenderer(renderer);

            RealisticWaterUnderwaterSurface controller =
                water.GetComponent<RealisticWaterUnderwaterSurface>();
            if (controller == null)
                controller = water.AddComponent<RealisticWaterUnderwaterSurface>();
            ConfigureUnderwaterSurface(controller, renderer);
        }

        private static void ConfigureUnderwaterSurface(
            RealisticWaterUnderwaterSurface controller,
            MeshRenderer renderer)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("underwaterRenderer").objectReferenceValue =
                renderer;
            serializedObject.FindProperty("quality").enumValueIndex =
                (int)WaterUnderwaterSurfaceQuality.UnderwaterSurface;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            controller.RefreshSurface();
        }

        private static void BuildUnderwaterFog(GameObject water)
        {
            if (water == null) return;

            GameObject fogObject = new("Underwater Fog Controller");
            UnderwaterFogController fog = fogObject.AddComponent<UnderwaterFogController>();
            ConfigureUnderwaterFog(fog, water);
        }

        private static void BuildQualityController(GameObject water)
        {
            if (water == null)
                return;

            RealisticWaterQualityController controller =
                water.GetComponent<RealisticWaterQualityController>() ??
                water.AddComponent<RealisticWaterQualityController>();
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("planarReflection").objectReferenceValue =
                water.GetComponent<RealisticWaterPlanarReflection>();
            serializedObject.FindProperty("temporalFoam").objectReferenceValue =
                water.GetComponent<RealisticWaterTemporalFoam>();
            serializedObject.FindProperty("causticProjection").objectReferenceValue =
                water.GetComponent<RealisticWaterCausticProjection>();
            serializedObject.FindProperty("underwaterSurface").objectReferenceValue =
                water.GetComponent<RealisticWaterUnderwaterSurface>();
            serializedObject.FindProperty("qualityTier").enumValueIndex =
                (int)RealisticWaterQualityTier.High;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            controller.RefreshQuality();
        }

        private const string OceanWaveProfilePath =
            "Assets/_Project/Art/Materials/Water/Profiles/WP_OceanSwell.asset";

        private static WaveProfileBinder BuildWaveProfileBinder(GameObject water)
        {
            if (water == null)
                return null;

            WaveProfileBinder binder =
                water.GetComponent<WaveProfileBinder>() ??
                water.AddComponent<WaveProfileBinder>();

            var serializedObject = new SerializedObject(binder);
            // Missing profile is not an error: the shaders fall back to the material's legacy four
            // waves, so a rebuilt lab still renders water.
            serializedObject.FindProperty("_profile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WaveProfile>(OceanWaveProfilePath);
            serializedObject.FindProperty("_uploadEveryFrame").boolValue = true;
            serializedObject.FindProperty("_useTransformAsWaterLevel").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return binder;
        }

        private static void BuildWeatherController(
            GameObject water, TextMesh statusLabel)
        {
            if (water == null)
                return;

            WaveProfileBinder waveProfileBinder = BuildWaveProfileBinder(water);
            RealisticWaterWeatherController controller =
                water.GetComponent<RealisticWaterWeatherController>() ??
                water.AddComponent<RealisticWaterWeatherController>();
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("waveProfileBinder").objectReferenceValue =
                waveProfileBinder;
            serializedObject.FindProperty("waterRenderer").objectReferenceValue =
                water.GetComponent<Renderer>();
            serializedObject.FindProperty("causticProjection").objectReferenceValue =
                water.GetComponent<RealisticWaterCausticProjection>();
            serializedObject.FindProperty("underwaterSurface").objectReferenceValue =
                water.GetComponent<RealisticWaterUnderwaterSurface>();
            serializedObject.FindProperty("statusLabel").objectReferenceValue =
                statusLabel;
            serializedObject.FindProperty("weather").enumValueIndex =
                (int)RealisticWaterWeather.Breeze;
            serializedObject.FindProperty("transitionDuration").floatValue = 3f;
            serializedObject.FindProperty("enableLabHotkeys").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildStandaloneCapture(GameObject water)
        {
            if (water != null &&
                water.GetComponent<RealisticWaterStandaloneCapture>() == null)
            {
                water.AddComponent<RealisticWaterStandaloneCapture>();
            }
        }

        private static void ConfigureUnderwaterFog(
            UnderwaterFogController fog, GameObject water)
        {
            var serializedObject = new SerializedObject(fog);
            serializedObject.FindProperty("waterSurface").objectReferenceValue =
                water.transform;
            serializedObject.FindProperty("underwaterSurface").objectReferenceValue =
                water.GetComponent<RealisticWaterUnderwaterSurface>();
            serializedObject.FindProperty("underwaterFogColor").colorValue =
                new Color(0.015f, 0.18f, 0.32f, 1f);
            serializedObject.FindProperty("underwaterFogDensity").floatValue = 0.08f;
            serializedObject.FindProperty("transitionHalfHeight").floatValue = 0.4f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLighting()
        {
            GameObject lightObject = new("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.42f, 0.46f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.61f, 0.71f, 0.77f);
            RenderSettings.fogStartDistance = 65f;
            RenderSettings.fogEndDistance = 155f;
        }

        private static TextMesh BuildLabel()
        {
            GameObject label = new("Label - Water Shader Lab");
            label.transform.position = new Vector3(0f, 5.45f, -35.75f);
            label.transform.rotation = Quaternion.identity;
            Material board = GetOrCreateMaterial(
                "LabSignBoard", new Color(0.035f, 0.075f, 0.095f), 0.4f, 0.2f);
            CreateBox(
                label.transform, "Title Board", Vector3.zero,
                new Vector3(13.5f, 1.35f, 0.22f), board);
            CreateTextLine(
                label.transform, "REALISTIC WATER LAB", 0.18f, 0.062f,
                Color.white, -0.13f);
            TextMesh weatherStatus = CreateTextLine(
                label.transform, "FOLLOW THE STATIONS  |  F4 TO FLY", -0.32f, 0.032f,
                new Color(0.48f, 0.9f, 0.94f), -0.13f);
            weatherStatus.text =
                "WEATHER: BREEZE  |  BRACKET KEYS TO CHANGE";
            return weatherStatus;
        }

        private static void BuildPostProcessing()
        {
            VolumeProfile profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingProfilePath);
            if (profile == null)
            {
                Debug.LogWarning(
                    $"[WaterShaderLabSceneBuilder] Missing {PostProcessingProfilePath}.");
                return;
            }

            GameObject volumeObject = new("Global Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static TextMesh CreateTextLine(
            Transform parent,
            string value,
            float localY,
            float characterSize,
            Color color,
            float localZ = 0f)
        {
            GameObject line = new($"Line - {value}");
            line.transform.SetParent(parent, false);
            line.transform.localPosition = new Vector3(0f, localY, localZ);
            TextMesh text = line.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
            return text;
        }

        private static void BuildPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (player == null)
                throw new InvalidOperationException($"Player prefab is missing at {PlayerPrefabPath}.");

            player.name = "Player";
            player.transform.position = new Vector3(0f, 2.7f, -43f);
            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            InteractionSystem interaction = player.GetComponent<InteractionSystem>();
            GameObject uiModeObject = new("UI Mode Service");
            UIModeService uiMode = uiModeObject.AddComponent<UIModeService>();
            SetObjectReference(uiMode, "playerController", controller);
            SetObjectReference(uiMode, "interactionSystem", interaction);
        }

        private static void CreateFeatureSign(
            Transform parent,
            string name,
            Vector3 position,
            float width,
            string title,
            string detail)
        {
            GameObject sign = new(name);
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = position;
            Material board = GetOrCreateMaterial(
                "LabStationSign", new Color(0.025f, 0.13f, 0.16f), 0.34f, 0.12f);
            CreateBox(
                sign.transform, "Board", Vector3.zero,
                new Vector3(width, 1.25f, 0.18f), board);
            CreateTextLine(
                sign.transform, title, 0.18f, 0.052f,
                Color.white, -0.11f);
            CreateTextLine(
                sign.transform, detail, -0.28f, 0.031f,
                new Color(0.5f, 0.88f, 0.92f), -0.11f);
        }

        private static void CreateStripedColumn(
            Transform parent,
            string name,
            Vector3 position,
            float height,
            int segmentCount,
            float width,
            Material first,
            Material second,
            float tilt)
        {
            GameObject column = new(name);
            column.transform.SetParent(parent, false);
            column.transform.localPosition = position;
            column.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            float segmentHeight = height / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                CreateBox(
                    column.transform, $"Band {i + 1}",
                    new Vector3(0f, segmentHeight * (i + 0.5f), 0f),
                    new Vector3(width, segmentHeight * 0.96f, width),
                    i % 2 == 0 ? first : second);
            }
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            return CreatePrimitive(
                PrimitiveType.Cube, parent, name, position, scale, material);
        }

        private static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            return CreatePrimitive(
                PrimitiveType.Cylinder, parent, name, position, scale, material);
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;
            }

            return null;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "WaterShaderLab");
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            float smoothness = 0.1f,
            float metallic = 0f)
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
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateEmissiveMaterial(
            string name, Color baseColor, Color emissionColor)
        {
            Material material = GetOrCreateMaterial(
                name, baseColor, 0.58f, 0.08f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
