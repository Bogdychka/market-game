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
    /// Builds a standalone scene combining three vendored packages: jiaozi158's Physically Based
    /// Sky (Packages/com.jiaozi158.unity-physically-based-sky-urp), jiaozi158's Volumetric Clouds
    /// (Assets/VolumetricCloudsURP), and gasgiant's Ocean-URP water (Assets/OceanURP), so the
    /// atmosphere can be judged against the same water OceanURPLab uses.
    ///
    /// Needs its own URP renderer because all three vendored packages inject renderer features
    /// that no other scene wants; the renderer is appended to the existing pipeline assets and the
    /// lab camera selects it by index, so gameplay scenes and OceanURPLab keep rendering exactly
    /// as before (OceanURPLab shares the water code but not this renderer).
    /// </summary>
    public static class PhysicallyBasedSkyLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/PhysicallyBasedSkyLab.unity";
        private const string RendererPath = "Assets/Settings/SkyOcean_Renderer.asset";
        private const string GeneratedFolder = "Assets/_Project/Art/PhysicallyBasedSkyLab";
        private const string OceanMaterialPath = "Assets/OceanURP/Presets/Ocean.mat";
        private const string ColorsPresetPath = "Assets/OceanURP/Presets/Colors.asset";
        private const string SimulationSettingsPath = "Assets/OceanURP/Presets/SimulationSettings.asset";
        private const string InputsProviderPath = "Assets/OceanURP/Presets/Beaufort 128x4 700m.asset";
        private const string PbSkyShaderName = "Hidden/Skybox/PhysicallyBasedSky";
        private const string PbSkyLutShaderName = "Hidden/Sky/PhysicallyBasedSkyPrecomputation";
        private const string PbSkyFallbackMaterialPath =
            "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/Procedural Sky.mat";
        private const string CloudsMaterialPath = "Assets/VolumetricCloudsURP/VolumetricClouds.mat";

        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset"
        };

        /// <summary>
        /// Rebuilds and opens the Physically Based Sky lab scene, creating the renderer and
        /// pipeline wiring it needs on the way.
        /// </summary>
        [MenuItem("Market/Debug/Build Physically Based Sky Lab")]
        public static void BuildPhysicallyBasedSkyLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ScriptableRendererData rendererData = EnsureSkyOceanRenderer();
            int rendererIndex = ConfigurePipelineAssets(rendererData);
            EnsureGeneratedFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PhysicallyBasedSkyLab";

            Camera camera = BuildCamera(rendererIndex);
            BuildSun();
            BuildSkyVolume();
            BuildOcean(camera.transform);
            ConfigureEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PhysicallyBasedSkyLabSceneBuilder] Built {ScenePath}. Enter Play Mode to see " +
                "the sky, clouds and water together.");
        }

        // --- pipeline wiring -------------------------------------------------------------------

        private static ScriptableRendererData EnsureSkyOceanRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.name = "SkyOcean_Renderer";
                AssetDatabase.CreateAsset(data, RendererPath);
            }

            EnsureFeature<OceanRendererFeature>(data, "Ocean");
            EnsureFeature<PhysicallyBasedSkyURP>(data, "Physically Based Sky URP");
            EnsureFeature<VolumetricCloudsURP>(data, "Volumetric Clouds URP");

            ConfigureOceanFeature(data);
            ConfigureSkyFeature(data);
            ConfigureCloudsFeature(data);
            ConfigureRendererData(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath);
            return data;
        }

        // Idempotent so re-running the builder on an already-built renderer (e.g. after adding a
        // new vendored feature) adds only what's missing instead of duplicating features.
        private static void EnsureFeature<T>(ScriptableRendererData data, string displayName)
            where T : ScriptableRendererFeature
        {
            foreach (ScriptableRendererFeature existingFeature in data.rendererFeatures)
            {
                if (existingFeature is T) return;
            }

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = displayName;
            AssetDatabase.AddObjectToAsset(feature, data);
            AddRendererFeature(data, feature);
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

        private static void ConfigureSkyFeature(ScriptableRendererData data)
        {
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature is not PhysicallyBasedSkyURP) continue;

                var serializedFeature = new SerializedObject(feature);
                serializedFeature.FindProperty("m_Shader").objectReferenceValue = Shader.Find(PbSkyShaderName);
                serializedFeature.FindProperty("m_LutShader").objectReferenceValue = Shader.Find(PbSkyLutShaderName);
                serializedFeature.FindProperty("m_FallbackSkyMaterial").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>(PbSkyFallbackMaterialPath);
                serializedFeature.ApplyModifiedProperties();
                return;
            }
        }

        private static void ConfigureCloudsFeature(ScriptableRendererData data)
        {
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature is not VolumetricCloudsURP) continue;

                var serializedFeature = new SerializedObject(feature);
                serializedFeature.FindProperty("material").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>(CloudsMaterialPath);
                // Full-resolution clouds, as upstream's setup screenshot shows. The code default is
                // 0.5 (half-res + bilateral upscale) for performance; this is a look-dev lab, so it
                // takes the cost to avoid judging upscale artefacts as cloud shape.
                serializedFeature.FindProperty("resolutionScale").floatValue = 1f;
                serializedFeature.ApplyModifiedProperties();
                return;
            }
        }

        /// <summary>
        /// A freshly created renderer has no post-process resources and copies depth after
        /// transparents. Both the ocean (BeforeRenderingTransparents) and the sky's atmospheric
        /// scattering pass (AfterRenderingSkybox) sample the depth copy, so it has to happen right
        /// after the opaques instead.
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
        /// Appends the sky+ocean renderer to every pipeline asset and turns on what refraction
        /// needs. Returns the renderer index the lab camera should select.
        /// </summary>
        private static int ConfigurePipelineAssets(ScriptableRendererData rendererData)
        {
            int index = -1;
            foreach (string path in PipelineAssetPaths)
            {
                var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipelineAsset == null)
                {
                    Debug.LogWarning($"[PhysicallyBasedSkyLabSceneBuilder] Pipeline asset not found: {path}");
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
                    Debug.LogWarning("[PhysicallyBasedSkyLabSceneBuilder] SkyOcean renderer sits at " +
                        $"different indices across pipeline assets ({index} vs {found}); the lab camera uses {index}.");
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
            camera.farClipPlane = 100000f;
            camera.allowHDR = true;

            var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
            cameraData.SetRenderer(rendererIndex);
            cameraData.renderPostProcessing = true;

            go.AddComponent<OceanLabFlyCamera>();
            go.AddComponent<AudioListener>();
            return camera;
        }

        /// <summary>
        /// Low sun ahead of the default camera, same placement OceanUrpLabSceneBuilder uses: this
        /// is where sun glitter and specular breakup on the water read, and it also puts the
        /// atmosphere's horizon glow in view instead of a flat overhead sun.
        /// </summary>
        private static void BuildSun()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(26f, 165f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            // Upstream README's suggested starting point.
            light.intensity = 3.030782f;
            light.shadows = LightShadows.Soft;

            RenderSettings.sun = light;
        }

        /// <summary>
        /// The four pieces the upstream README calls out: the renderer feature is already on
        /// SkyOcean_Renderer, so this adds the three required Volume overrides. Exposure and the
        /// intensity mode already default to the README's recommended values (Exposure 0); only
        /// the sky type has to be turned on explicitly.
        /// </summary>
        private static void BuildSkyVolume()
        {
            var go = new GameObject("Sky Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsureSkyProfile();
        }

        private static VolumeProfile EnsureSkyProfile()
        {
            string path = $"{GeneratedFolder}/PhysicallyBasedSkyLabProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var visualEnvironment = profile.Add<VisualEnvironment>(true);
            visualEnvironment.skyType.overrideState = true;
            visualEnvironment.skyType.value = (int)VisualEnvironment.SkyType.PhysicallyBased;
            visualEnvironment.skyAmbientMode.overrideState = true;
            visualEnvironment.skyAmbientMode.value = VisualEnvironment.SkyAmbientMode.Dynamic;
            AssetDatabase.AddObjectToAsset(visualEnvironment, profile);

            var physicallyBasedSky = profile.Add<PhysicallyBasedSky>(true);
            physicallyBasedSky.atmosphericScattering.overrideState = true;
            physicallyBasedSky.atmosphericScattering.value = true;
            physicallyBasedSky.skyIntensityMode.overrideState = true;
            physicallyBasedSky.skyIntensityMode.value = PhysicallyBasedSky.SkyIntensityMode.Exposure;
            physicallyBasedSky.exposure.overrideState = true;
            physicallyBasedSky.exposure.value = 0f;
            AssetDatabase.AddObjectToAsset(physicallyBasedSky, profile);

            var fog = profile.Add<Fog>(true);
            fog.enabled.overrideState = true;
            fog.enabled.value = true;
            AssetDatabase.AddObjectToAsset(fog, profile);

            var clouds = profile.Add<VolumetricClouds>(true);
            clouds.state.value = true;
            // Custom exposes the shape properties instead of driving them from a weather preset,
            // which is what Add<T>(true) already implies - every parameter here is overridden.
            // The Custom case is a no-op in ApplyCurrentCloudPreset, so no values are clobbered.
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Custom;
            AssetDatabase.AddObjectToAsset(clouds, profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // Identical wiring to OceanUrpLabSceneBuilder.BuildOcean - this is deliberately the same
        // water, not a re-tuned copy, so the sky is judged against a known reference.
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
        /// The sky renderer feature drives RenderSettings.skybox itself once active; this only
        /// sets the ambient/reflection modes it expects and turns off the legacy scene fog (the
        /// Fog volume override owns atmospheric fog instead).
        /// </summary>
        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
        }

        private static void EnsureGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;
            AssetDatabase.CreateFolder("Assets/_Project/Art", "PhysicallyBasedSkyLab");
        }
    }
}
