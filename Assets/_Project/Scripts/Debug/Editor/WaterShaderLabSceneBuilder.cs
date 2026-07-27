using System;
using System.Collections.Generic;
using Market.DebugTools;
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
        private const string CausticReceiverRootName =
            "Caustic Projection Receivers";
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
            BuildFoamTestRocks();
            RealisticWaterMaterialInstaller.CreateMaterial();
            GameObject water = BuildRealisticWater();
            BuildProjectedCaustics(scene, water);
            BuildUnderwaterSurface(water);
            BuildQualityController(water);
            BuildStandaloneCapture(water);
            BuildUnderwaterFog(water);
            BuildLighting();
            BuildLabel();
            BuildPlayer();

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
            Material material = GetOrCreateMaterial("FoamTestRock", new Color(0.32f, 0.30f, 0.28f));
            Vector3[] positions =
            {
                new(-14f, -0.2f, -18f),
                new(6f, -0.4f, -8f),
                new(-4f, -0.6f, 2f),
                new(16f, -1.2f, 10f),
            };

            foreach (Vector3 position in positions)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock";
                rock.transform.SetParent(rocks.transform, false);
                rock.transform.localPosition = position;
                rock.transform.localScale = new Vector3(2.5f, 1.6f, 2.5f);
                rock.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 90f), 0f);
                rock.GetComponent<Renderer>().sharedMaterial = material;
            }
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
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.52f, 0.55f);
        }

        private static void BuildLabel()
        {
            GameObject label = new("Label - Water Shader Lab");
            label.transform.position = new Vector3(0f, 3.2f, -20f);
            label.transform.rotation = Quaternion.identity;
            CreateTextLine(label.transform, "WATER SHADER LAB", 0f, 0.10f, Color.white);
            CreateTextLine(label.transform, "Edit RealisticWater.shader / M_RealisticWaterLab.mat to iterate", -0.6f, 0.045f, new Color(0.8f, 0.9f, 0.95f));
            CreateTextLine(label.transform, "F4 fly  |  Space / Left Ctrl up-down while flying", -1.2f, 0.045f, new Color(0.8f, 0.9f, 0.95f));
        }

        private static void CreateTextLine(Transform parent, string value, float localY, float characterSize, Color color)
        {
            GameObject line = new($"Line - {value}");
            line.transform.SetParent(parent, false);
            line.transform.localPosition = new Vector3(0f, localY, 0f);
            TextMesh text = line.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static void BuildPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (player == null)
                throw new InvalidOperationException($"Player prefab is missing at {PlayerPrefabPath}.");

            player.name = "Player";
            player.transform.position = new Vector3(0f, Terraces[0].TopY + 0.6f, -42f);
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
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
