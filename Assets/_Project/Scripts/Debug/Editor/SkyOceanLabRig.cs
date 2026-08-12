using OceanSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// The shared "physically based sky + volumetric clouds + Ocean-URP water" rig, used by every
    /// lab scene that wants that exact atmosphere: PhysicallyBasedSkyLab (open sea) and BeachLab
    /// (the same sky and water against a Terrain shore).
    ///
    /// The renderer, the pipeline wiring and the sky Volume profile are deliberately *one* set of
    /// assets shared by those scenes - the point of a second lab is to change the scene, not the
    /// atmosphere, so tuning the sky in one lab has to show up in the other. Only the scene content
    /// (camera, sun angle, terrain, wave direction) is per-lab.
    ///
    /// Every Ensure* method is idempotent: re-running a builder on already-built assets adds what
    /// is missing and re-applies the values this rig owns, so a rebuild is how a lab gets back to a
    /// known state. Hand-tuning done in the Inspector is deliberately overwritten.
    /// </summary>
    internal static class SkyOceanLabRig
    {
        private const string RendererPath = "Assets/Settings/SkyOcean_Renderer.asset";

        // Kept under the PhysicallyBasedSkyLab folder now that the profile is shared: renaming it
        // would orphan the profile the already-built PhysicallyBasedSkyLab.unity references.
        private const string GeneratedFolder = "Assets/_Project/Art/PhysicallyBasedSkyLab";
        private const string SkyProfilePath = GeneratedFolder + "/PhysicallyBasedSkyLabProfile.asset";

        private const string OceanMaterialPath = "Assets/OceanURP/Presets/Ocean.mat";
        private const string ColorsPresetPath = "Assets/OceanURP/Presets/Colors.asset";
        private const string SimulationSettingsPath = "Assets/OceanURP/Presets/SimulationSettings.asset";
        private const string InputsProviderPath = "Assets/OceanURP/Presets/Beaufort 128x4 700m.asset";
        private const string PbSkyShaderName = "Hidden/Skybox/PhysicallyBasedSky";
        private const string PbSkyLutShaderName = "Hidden/Sky/PhysicallyBasedSkyPrecomputation";
        private const string PbSkyFallbackMaterialPath =
            "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/Procedural Sky.mat";
        private const string CloudsMaterialPath = "Assets/VolumetricCloudsURP/VolumetricClouds.mat";

        // The lab sea state, owned here rather than per scene: "the water from the sky lab" has to
        // mean the same waves as well as the same material. Degrees are measured from +X.
        private const float WindForce01 = 0.45f;
        private const float LocalWindDirection = 0f;
        private const float SwellDirection = 45f;

        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset"
        };

        /// <summary>
        /// Creates/updates the sky+ocean renderer, appends it to the pipeline assets and makes sure
        /// the generated-asset folder exists. Returns the renderer index a lab camera must select.
        /// </summary>
        internal static int EnsureRig()
        {
            ScriptableRendererData rendererData = EnsureSkyOceanRenderer();
            int rendererIndex = ConfigurePipelineAssets(rendererData);
            EnsureGeneratedFolder();
            return rendererIndex;
        }

        // --- pipeline wiring -------------------------------------------------------------------

        /// <summary>
        /// The rig needs its own URP renderer because all three vendored packages inject renderer
        /// features that no other scene wants; it is appended to the existing pipeline assets and
        /// the lab cameras select it by index, so gameplay scenes keep rendering as before.
        /// </summary>
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

        // Idempotent so re-running a builder on an already-built renderer (e.g. after adding a
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
                // Half-resolution clouds, matching the package default and what HDRP and most
                // shipping games do - raymarching them at full resolution was 0.93 ms of a 2.11 ms
                // frame. If half-res ever reads as noisy rather than soft, the paired fix is
                // upscaleMode = Bilateral, which is what enables the shader's low-res clouds path.
                serializedFeature.FindProperty("resolutionScale").floatValue = 0.5f;
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
                    Debug.LogWarning($"[SkyOceanLabRig] Pipeline asset not found: {path}");
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
                    Debug.LogWarning("[SkyOceanLabRig] SkyOcean renderer sits at different indices " +
                        $"across pipeline assets ({index} vs {found}); the lab cameras use {index}.");
            }

            return Mathf.Max(index, 0);
        }

        // --- scene content ---------------------------------------------------------------------

        /// <summary>
        /// Free-fly lab camera on the sky+ocean renderer. The far plane is large because the
        /// physically based sky and the ocean clipmap both extend to the horizon.
        /// </summary>
        internal static Camera BuildFlyCamera(int rendererIndex, Vector3 position, Vector3 eulerAngles)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));

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
        /// Directional sun at the upstream README's suggested intensity. Labs keep it low in the
        /// sky: that is where sun glitter and specular breakup on the water read, and it puts the
        /// atmosphere's horizon glow in view instead of a flat overhead sun.
        /// </summary>
        internal static Light BuildSun(Vector3 eulerAngles)
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(eulerAngles);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 3.030782f;
            light.shadows = LightShadows.Soft;

            RenderSettings.sun = light;
            return light;
        }

        /// <summary>
        /// The four pieces the upstream README calls out: the renderer feature is already on
        /// SkyOcean_Renderer, so this adds the Volume overrides. Exposure and the intensity mode
        /// already default to the README's recommended values (Exposure 0); only the sky type has
        /// to be turned on explicitly.
        /// </summary>
        internal static void BuildSkyVolume()
        {
            var go = new GameObject("Sky Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsureSkyProfile();
        }

        private static VolumeProfile EnsureSkyProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SkyProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, SkyProfilePath);
            }

            VisualEnvironment visualEnvironment = EnsureOverride<VisualEnvironment>(profile);
            visualEnvironment.skyType.value = (int)VisualEnvironment.SkyType.PhysicallyBased;
            visualEnvironment.skyAmbientMode.value = VisualEnvironment.SkyAmbientMode.Dynamic;

            PhysicallyBasedSky physicallyBasedSky = EnsureOverride<PhysicallyBasedSky>(profile);
            physicallyBasedSky.atmosphericScattering.value = true;
            physicallyBasedSky.skyIntensityMode.value = PhysicallyBasedSky.SkyIntensityMode.Exposure;
            physicallyBasedSky.exposure.value = 0f;

            Fog fog = EnsureOverride<Fog>(profile);
            fog.enabled.value = true;

            VolumetricClouds clouds = EnsureOverride<VolumetricClouds>(profile);
            clouds.state.value = true;
            // Custom exposes the shape properties instead of driving them from a weather preset,
            // which is what EnsureOverride's Add<T>(true) already implies - every parameter here is
            // overridden. The Custom case is a no-op in ApplyCurrentCloudPreset, so nothing is
            // clobbered by setting it.
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Custom;
            // Both the package default and upstream's own sample profile leave this at 0, which
            // means the wind vector never advances and the clouds are completely static.
            //
            // Units are km/h, but a realistic value reads as no motion at all. The shader samples
            // the shape noise at windVector / NOISE_TEXTURE_NORMALIZATION_FACTOR * shapeScale, so
            // with the 100000 factor and shapeScale 5 one full noise tile spans 20 km: at an
            // ordinary 50 km/h the cloud pattern needs ~24 minutes to cross it. That is physically
            // correct and useless for look-dev, so this is deliberately exaggerated to about 6x
            // real - roughly 1 km of cloud field per 12 s, which reads as a moving sky.
            clouds.globalSpeed.value = 300f;
            // Degrees from +X, matching the ocean's LocalWindDirection so sky and sea drift the
            // same way instead of visibly disagreeing.
            clouds.globalOrientation.value = 0f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // Mirrors EnsureFeature<T> on the renderer side: Add<T> logs an error if the component is
        // already on the profile, so an existing one has to be reused rather than re-added.
        private static T EnsureOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;

            T component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>
        /// gasgiant's Ocean-URP water, wired exactly as OceanUrpLabSceneBuilder does it - the same
        /// water in every lab, not a re-tuned copy, so the sky is judged against a known reference.
        /// There is deliberately no per-scene parameter: a lab that wants a different sea state
        /// changes it at runtime with OceanLabController ([ ] wind force, , . local wind direction,
        /// ; ' swell direction), which leaves the reference asset alone.
        /// </summary>
        internal static GameObject BuildOcean(Transform viewer)
        {
            var go = new GameObject("Ocean");

            var simulation = go.AddComponent<OceanSimulation>();
            var serializedSimulation = new SerializedObject(simulation);
            serializedSimulation.RequireProperty("_simulationSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<OceanSimulationSettings>(SimulationSettingsPath);
            serializedSimulation.RequireProperty("_inputsProvider").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<OceanSimulationInputsProvider>(InputsProviderPath);
            serializedSimulation.RequireProperty("_localWindDirection").floatValue = LocalWindDirection;
            serializedSimulation.RequireProperty("_swellDirection").floatValue = SwellDirection;
            serializedSimulation.RequireProperty("_windForce01").floatValue = WindForce01;
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

            // The controller pushes its own fields into the simulation every Update, so leaving it
            // at its component defaults would quietly replace the preset above the moment Play Mode
            // starts (its swell direction defaults to 0, not 45).
            OceanLabController controller = go.AddComponent<OceanLabController>();
            var serializedController = new SerializedObject(controller);
            serializedController.RequireProperty("_windForce01").floatValue = WindForce01;
            serializedController.RequireProperty("_localWindDirection").floatValue = LocalWindDirection;
            serializedController.RequireProperty("_swellDirection").floatValue = SwellDirection;
            serializedController.ApplyModifiedProperties();
            return go;
        }

        /// <summary>
        /// The sky renderer feature drives RenderSettings.skybox itself once active; this only
        /// sets the ambient/reflection modes it expects and turns off the legacy scene fog (the
        /// Fog volume override owns atmospheric fog instead).
        /// </summary>
        internal static void ConfigureEnvironment()
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
