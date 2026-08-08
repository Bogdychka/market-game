using System.Collections.Generic;
using Market.DebugTools;
using OceanSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds a standalone scene for the vendored gasgiant Ocean-URP water (Assets/OceanURP):
    /// FFT simulation with four cascades, geoclipmap surface, and the underwater volume effect.
    /// It is deliberately separate from WaterShaderLab, which drives our own RealisticWater shader.
    ///
    /// The ocean needs its own URP renderer because OceanRendererFeature injects passes that no
    /// other scene wants; the renderer is appended to the existing pipeline assets and the lab
    /// camera selects it by index, so gameplay scenes keep rendering exactly as before.
    /// </summary>
    public static class OceanUrpLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/OceanURPLab.unity";
        private const string RendererPath = "Assets/Settings/Ocean_Renderer.asset";
        private const string GeneratedFolder = "Assets/_Project/Art/OceanURPLab";
        private const string OceanMaterialPath = "Assets/OceanURP/Presets/Ocean.mat";
        private const string ColorsPresetPath = "Assets/OceanURP/Presets/Colors.asset";
        private const string SimulationSettingsPath = "Assets/OceanURP/Presets/SimulationSettings.asset";
        private const string InputsProviderPath = "Assets/OceanURP/Presets/Beaufort 128x4 700m.asset";

        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset"
        };

        /// <summary>
        /// Rebuilds and opens the Ocean URP lab scene, creating the renderer and pipeline wiring
        /// it needs on the way.
        /// </summary>
        [MenuItem("Market/Debug/Build Ocean URP Lab")]
        public static void BuildOceanUrpLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ScriptableRendererData rendererData = EnsureOceanRenderer();
            int rendererIndex = ConfigurePipelineAssets(rendererData);
            EnsureGeneratedFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "OceanURPLab";

            Camera camera = BuildCamera(rendererIndex);
            BuildLighting();
            BuildPostProcessing();
            BuildOcean(camera.transform);
            BuildReferenceProps();
            ConfigureEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OceanUrpLabSceneBuilder] Built {ScenePath}. Enter Play Mode, or tick " +
                "'Render In Edit Mode' on the Ocean feature of Ocean_Renderer to see it in the Scene view.");
        }

        // --- pipeline wiring -------------------------------------------------------------------

        private static ScriptableRendererData EnsureOceanRenderer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (existing != null)
            {
                ConfigureOceanFeature(existing);
                ConfigureRendererData(existing);
                return existing;
            }

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.name = "Ocean_Renderer";
            AssetDatabase.CreateAsset(data, RendererPath);

            var feature = ScriptableObject.CreateInstance<OceanRendererFeature>();
            feature.name = "Ocean";
            AssetDatabase.AddObjectToAsset(feature, data);
            AddRendererFeature(data, feature);
            ConfigureOceanFeature(data);
            ConfigureRendererData(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath);
            return data;
        }

        // URP keeps a parallel list of local file ids next to the feature list; writing both through
        // SerializedObject is what its own inspector does, and skipping the map breaks feature lookup.
        private static void AddRendererFeature(ScriptableRendererData data, ScriptableRendererFeature feature)
        {
            var serializedData = new SerializedObject(data);
            SerializedProperty features = serializedData.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedData.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            if (featureMap != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            }

            serializedData.ApplyModifiedProperties();
        }

        private static void ConfigureOceanFeature(ScriptableRendererData data)
        {
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature is not OceanRendererFeature) continue;

                var serializedFeature = new SerializedObject(feature);
                SerializedProperty settings = serializedFeature.FindProperty("_settings");
                settings.FindPropertyRelative("skyMapResolution").intValue = 256;
                settings.FindPropertyRelative("updateSkyMap").boolValue = true;
                settings.FindPropertyRelative("transparency").boolValue = true;
                settings.FindPropertyRelative("underwaterEffect").boolValue = true;
                serializedFeature.ApplyModifiedProperties();
                return;
            }
        }

        /// <summary>
        /// A freshly created renderer has no post-process resources and copies depth after
        /// transparents. The ocean is drawn before transparents and samples the depth copy, so the
        /// copy has to happen right after the opaques instead.
        /// </summary>
        private static void ConfigureRendererData(ScriptableRendererData data)
        {
            var serializedData = new SerializedObject(data);
            serializedData.FindProperty("m_CopyDepthMode").enumValueIndex = (int)CopyDepthMode.AfterOpaques;

            SerializedProperty postProcessData = serializedData.FindProperty("postProcessData");
            if (postProcessData != null && postProcessData.objectReferenceValue == null)
                postProcessData.objectReferenceValue = LoadDefaultPostProcessData();

            serializedData.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }

        // Taken from the renderer the game already uses, so the lab does not hardcode a package path.
        private static Object LoadDefaultPostProcessData()
        {
            var reference = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (reference == null) return null;
            return new SerializedObject(reference).FindProperty("postProcessData").objectReferenceValue;
        }

        /// <summary>
        /// Appends the ocean renderer to every pipeline asset and turns on what refraction needs.
        /// Returns the renderer index the lab camera should select.
        /// </summary>
        private static int ConfigurePipelineAssets(ScriptableRendererData rendererData)
        {
            int index = -1;
            foreach (string path in PipelineAssetPaths)
            {
                var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipelineAsset == null)
                {
                    Debug.LogWarning($"[OceanUrpLabSceneBuilder] Pipeline asset not found: {path}");
                    continue;
                }

                var serializedAsset = new SerializedObject(pipelineAsset);
                SerializedProperty list = serializedAsset.FindProperty("m_RendererDataList");

                int found = -1;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == rendererData)
                    {
                        found = i;
                        break;
                    }
                }

                if (found < 0)
                {
                    list.arraySize++;
                    found = list.arraySize - 1;
                    list.GetArrayElementAtIndex(found).objectReferenceValue = rendererData;
                }

                // Refraction samples the opaque copy and the depth copy; downsampling the opaque
                // copy is what produces coloured fringes along the edges of objects in the water.
                if (path.Contains("PC_RPAsset"))
                {
                    serializedAsset.FindProperty("m_RequireDepthTexture").boolValue = true;
                    serializedAsset.FindProperty("m_RequireOpaqueTexture").boolValue = true;
                    serializedAsset.FindProperty("m_OpaqueDownsampling").intValue = 0;
                }

                serializedAsset.ApplyModifiedProperties();
                EditorUtility.SetDirty(pipelineAsset);

                if (index < 0)
                    index = found;
                else if (index != found)
                    Debug.LogWarning("[OceanUrpLabSceneBuilder] Ocean renderer sits at different " +
                        $"indices across pipeline assets ({index} vs {found}); the lab camera uses {index}.");
            }

            return Mathf.Max(index, 0);
        }

        // --- scene content ---------------------------------------------------------------------

        private static Camera BuildCamera(int rendererIndex)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(new Vector3(0f, 9f, -45f), Quaternion.Euler(6f, 0f, 0f));

            Camera camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000f;
            camera.allowHDR = true;

            var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
            cameraData.SetRenderer(rendererIndex);
            cameraData.renderPostProcessing = true;

            go.AddComponent<OceanLabFlyCamera>();
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void BuildLighting()
        {
            var go = new GameObject("Directional Light");
            // Low sun placed ahead of the default camera: that is where the glitter path, the
            // specular breakup on wave faces and the subsurface scatter through crests all read.
            // Raising it or putting it behind the camera flattens exactly what this lab exists for.
            go.transform.rotation = Quaternion.Euler(26f, 165f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 2.2f;
            light.shadows = LightShadows.Soft;

            RenderSettings.sun = light;
        }

        /// <summary>
        /// The ocean writes a wide HDR range - sun glitter against deep scatter - so the lab needs
        /// a tonemapper of its own. It deliberately does not reuse the game's graded profile, which
        /// would colour every judgement made about the water here.
        /// </summary>
        private static void BuildPostProcessing()
        {
            var go = new GameObject("Post Processing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsureVolumeProfile();
        }

        private static VolumeProfile EnsureVolumeProfile()
        {
            string path = $"{GeneratedFolder}/OceanURPLabProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;
            AssetDatabase.AddObjectToAsset(tonemapping, profile);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            AssetDatabase.AddObjectToAsset(bloom, profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void BuildOcean(Transform viewer)
        {
            var go = new GameObject("Ocean");

            var simulation = go.AddComponent<OceanSimulation>();
            var serializedSimulation = new SerializedObject(simulation);
            serializedSimulation.RequireProperty("_simulationSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<OceanSimulationSettings>(SimulationSettingsPath);
            serializedSimulation.RequireProperty("_inputsProvider").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<OceanSimulationInputsProvider>(InputsProviderPath);
            serializedSimulation.RequireProperty("_localWindDirection").floatValue = 0f;
            serializedSimulation.RequireProperty("_swellDirection").floatValue = 45f;
            serializedSimulation.RequireProperty("_windForce01").floatValue = 0.45f;
            serializedSimulation.ApplyModifiedProperties();

            var renderer = go.AddComponent<OceanRenderer>();
            var serializedRenderer = new SerializedObject(renderer);
            serializedRenderer.RequireProperty("_material").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
            serializedRenderer.RequireProperty("_colorsPreset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<OceanColorsPreset>(ColorsPresetPath);
            serializedRenderer.RequireProperty("_reflectionsMode").enumValueIndex = 0;
            serializedRenderer.RequireProperty("_viewer").objectReferenceValue = viewer;
            serializedRenderer.RequireProperty("_minMeshScale").floatValue = 15f;
            serializedRenderer.RequireProperty("_clipMapLevels").intValue = 7;
            serializedRenderer.RequireProperty("_vertexDensity").intValue = 25;
            serializedRenderer.ApplyModifiedProperties();

            go.AddComponent<OceanLabController>();
        }

        /// <summary>
        /// Half-submerged reference geometry. Without it there is nothing to judge refraction,
        /// the shoreline-less transparency falloff, or the underwater transition against.
        /// </summary>
        private static void BuildReferenceProps()
        {
            Material material = EnsurePropMaterial();
            var root = new GameObject("Reference Props");

            var props = new List<(string name, PrimitiveType type, Vector3 position, Vector3 scale)>
            {
                ("Platform", PrimitiveType.Cube, new Vector3(0f, -0.5f, -12f), new Vector3(14f, 1f, 14f)),
                ("Pillar A", PrimitiveType.Cube, new Vector3(-9f, 1f, -4f), new Vector3(1.5f, 12f, 1.5f)),
                ("Pillar B", PrimitiveType.Cube, new Vector3(9f, 1f, -4f), new Vector3(1.5f, 12f, 1.5f)),
                ("Buoy", PrimitiveType.Sphere, new Vector3(4f, 0f, 6f), Vector3.one * 3f),
                ("Reef", PrimitiveType.Sphere, new Vector3(-6f, -2.5f, 9f), Vector3.one * 7f),
                ("Depth Post", PrimitiveType.Cylinder, new Vector3(0f, -4f, 14f), new Vector3(1f, 8f, 1f))
            };

            foreach ((string name, PrimitiveType type, Vector3 position, Vector3 scale) in props)
            {
                GameObject go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(root.transform);
                go.transform.position = position;
                go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static Material EnsurePropMaterial()
        {
            string path = $"{GeneratedFolder}/M_OceanLabProp.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.62f, 0.58f, 0.52f));
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
        }

        private static void EnsureGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;
            AssetDatabase.CreateFolder("Assets/_Project/Art", "OceanURPLab");
        }
    }

    internal static class OceanLabSerializedObjectExtensions
    {
        /// <summary>
        /// FindProperty that fails loudly: a silently missing field would produce a scene whose
        /// ocean components look wired up but are not.
        /// </summary>
        public static SerializedProperty RequireProperty(this SerializedObject serializedObject, string name)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
                throw new System.InvalidOperationException(
                    $"Serialized field '{name}' not found on {serializedObject.targetObject.GetType().Name}.");
            return property;
        }
    }
}
