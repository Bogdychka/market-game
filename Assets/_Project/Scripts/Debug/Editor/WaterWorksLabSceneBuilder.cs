using System;
using Market.Interaction;
using Market.Player;
using Market.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds a standalone lab scene for the imported GapperGames "WaterWorks" package: one pool
    /// with a stepped seabed (dry beach down to a deep trench) and four feature stations - depth
    /// fade, refraction, screen-space reflection and wave displacement - so each shader feature can
    /// be judged in isolation. The underwater volumetric pass runs on its own URP renderer
    /// (<c>WaterWorksLab_Renderer</c>), so the full-screen blit never costs anything in Market.
    /// In play mode press F6 for the feature panel and F4 to fly.
    /// </summary>
    public static class WaterWorksLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/WaterWorksLab.unity";
        private const string GeneratedFolder = "Assets/_Project/Art/WaterWorksLab";
        private const string PlayerPrefabPath = "Assets/_Project/Art/Prefabs/Player/Player.prefab";
        private const string WaterMeshPath = "Assets/_Project/Art/Meshes/Water/RealisticWaterGrid.asset";

        private const string PackageWaterMaterialPath = "Assets/WaterWorks/Materials/SSR_Water.mat";
        private const string PackageBrightMaterialPath = "Assets/WaterWorks/Materials/SSR_Water_Bright.mat";
        private const string PackageVolumeMaterialPath = "Assets/WaterWorks/Resources/Water_Volume.mat";

        private const string LabWaterMaterialPath = GeneratedFolder + "/M_WaterWorksLab.mat";
        private const string LabBrightMaterialPath = GeneratedFolder + "/M_WaterWorksLabBright.mat";
        private const string LabVolumeMaterialPath = GeneratedFolder + "/M_WaterWorksLabVolume.mat";

        private const string SourceRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string LabRendererPath = "Assets/Settings/WaterWorksLab_Renderer.asset";
        private const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";

        private const string WaterPlanePrefabPath = "Assets/WaterWorks/Water_Plane.prefab";
        private const string DemoPostProfilePath = "Assets/WaterWorks/Demo/Demo_Postprocessing.asset";

        /// <summary>
        /// Reproduce the author's demo conditions instead of the project look, so the asset can be
        /// judged the way its store page shows it: the package ocean plane, untouched author
        /// material values, the demo sun (intensity 5) and the demo post profile (ACES + heavy
        /// bloom + lens dirt). Set this to false to get the project-lit variant on a dense water
        /// grid, where vertex displacement is actually visible - the package plane is an 11x11 quad
        /// scaled to 10000 units, so it cannot render waves at all, which is why the package ships
        /// with _Displacement_Amount at 0.
        /// </summary>
        /// Not a const: constant folding would turn every project-look branch into a CS0162 warning.
        private static readonly bool UseAuthorDemoLook = true;

        /// <summary>Water surface height. Everything below is seabed, everything above is shore.</summary>
        private const float WaterLevel = 0f;

        /// <summary>Half width of the pool along X - the seabed blocks span the full width.</summary>
        private const float PoolHalfWidth = 110f;

        private readonly struct Terrace
        {
            public Terrace(string name, float topY, float zStart, float zEnd, Color color)
            {
                Name = name;
                TopY = topY;
                ZStart = zStart;
                ZEnd = zEnd;
                Color = color;
            }

            public string Name { get; }
            public float TopY { get; }
            public float ZStart { get; }
            public float ZEnd { get; }
            public Color Color { get; }
        }

        /// <summary>
        /// Seabed steps from the spawn beach (south) into the trench (north). The depth range is
        /// what makes the depth fade, the shoreline foam and the underwater volume readable.
        /// </summary>
        private static readonly Terrace[] Terraces =
        {
            new Terrace("Beach", 1.6f, -120f, -80f, new Color(0.78f, 0.71f, 0.52f)),
            new Terrace("Shallows", -0.6f, -80f, -55f, new Color(0.70f, 0.64f, 0.46f)),
            new Terrace("Shallow Shelf", -2.5f, -55f, -25f, new Color(0.50f, 0.55f, 0.44f)),
            new Terrace("Mid Shelf", -7f, -25f, 10f, new Color(0.32f, 0.38f, 0.35f)),
            new Terrace("Deep Shelf", -16f, 10f, 45f, new Color(0.20f, 0.25f, 0.27f)),
            new Terrace("Trench", -34f, 45f, 120f, new Color(0.11f, 0.14f, 0.16f)),
        };

        /// <summary>Rebuilds and opens the standalone WaterWorksLab scene.</summary>
        [MenuItem("Market/Debug/Water/Build WaterWorks Lab")]
        public static void BuildWaterWorksLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EnsureGeneratedFolder();
                Material water = CopyMaterial(PackageWaterMaterialPath, LabWaterMaterialPath);
                Material bright = CopyMaterial(PackageBrightMaterialPath, LabBrightMaterialPath);
                Material volume = CopyMaterial(PackageVolumeMaterialPath, LabVolumeMaterialPath);
                TuneLabMaterials(water, bright, volume);

                ScriptableRendererData rendererData = EnsureLabRenderer(volume);
                int rendererIndex = EnsureRendererRegistered(rendererData);

                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "WaterWorksLab";

                BuildSeabed();
                BuildDepthStation();
                BuildRefractionStation();
                BuildReflectionStation();
                BuildWaveStation();
                GameObject waterObject = BuildWater(water, volume);
                BuildLighting();
                BuildLabels();
                BuildPlayer(waterObject, new[] { water, bright }, volume, rendererIndex);

                // Give the scene a path first: PostProcessingSetup saves the open scene, and on an
                // untitled scene that opens the modal "Save Scene" file dialog.
                EditorSceneManager.SaveScene(scene, ScenePath);
                BuildPostProcessing();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[WaterWorksLabSceneBuilder] Built {ScenePath} on renderer index {rendererIndex} " +
                    $"(WaterWorksLab_Renderer), look = {(UseAuthorDemoLook ? "author demo" : "project")}. " +
                    "Enter play mode: F6 feature panel, F4 fly, walk north past the Deep Shelf to " +
                    "enter the underwater volume.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WaterWorksLabSceneBuilder] Build failed: {exception.Message}");
                throw;
            }
        }

        // ---- Assets -------------------------------------------------------------------------

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "WaterWorksLab");
        }

        /// <summary>
        /// Copies a package material into the project once. Package materials are never tuned in
        /// place, so re-importing WaterWorks cannot wipe lab settings (AGENTS.md).
        /// </summary>
        private static Material CopyMaterial(string sourcePath, string targetPath)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (existing != null)
                return existing;

            RequireAsset<Material>(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new InvalidOperationException($"Could not copy {sourcePath} to {targetPath}.");

            AssetDatabase.ImportAsset(targetPath);
            return RequireAsset<Material>(targetPath);
        }

        private static void TuneLabMaterials(Material water, Material bright, Material volume)
        {
            if (UseAuthorDemoLook)
            {
                // Judging the asset means judging its own values - and the copies may still hold
                // overrides from an earlier build, so restore them from the package originals.
                RestoreFromPackage(water, PackageWaterMaterialPath, WaveProperties);
                RestoreFromPackage(bright, PackageBrightMaterialPath, WaveProperties);

                var packageVolume = RequireAsset<Material>(PackageVolumeMaterialPath);
                volume.SetVector("bounds", packageVolume.GetVector("bounds"));
                EditorUtility.SetDirty(volume);
                return;
            }

            // Only for the project-lit variant: the dense grid can actually render vertex waves.
            foreach (Material material in new[] { water, bright })
            {
                SetIfPresent(material, "_Displacement_Amount", 0.35f);
                SetIfPresent(material, "_Displacement_Scale", 1.4f);
                SetIfPresent(material, "_Displacement_Speed", 0.15f);
                SetIfPresent(material, "_MaxWaveDist", 160f);
                EditorUtility.SetDirty(material);
            }

            // A 400x120x400 box is enough for the finite pool; the shipped 10000x500x10000 makes
            // the ray march walk far more empty space than it needs to.
            volume.SetVector("bounds", new Vector4(400f, 120f, 400f, 0f));
            EditorUtility.SetDirty(volume);
        }

        private static void SetIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        /// <summary>Shader-graph names, not the dead duplicates the shipped material still carries.</summary>
        private static readonly string[] WaveProperties =
        {
            "_Displacement_Amount",
            "_Displacement_Scale",
            "_Displacement_Speed",
            "_MaxWaveDist",
        };

        private static void RestoreFromPackage(
            Material target, string packagePath, string[] properties)
        {
            var source = RequireAsset<Material>(packagePath);
            foreach (string property in properties)
            {
                if (source.HasProperty(property) && target.HasProperty(property))
                    target.SetFloat(property, source.GetFloat(property));
            }

            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Creates a renderer that is a copy of the PC renderer plus the WaterWorks underwater
        /// feature. Keeping it separate means the full-screen volumetric blit only runs for cameras
        /// that opt into it - Market keeps using renderer 0 and pays nothing.
        /// </summary>
        private static ScriptableRendererData EnsureLabRenderer(Material volumeMaterial)
        {
            ScriptableRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(LabRendererPath);
            if (rendererData == null)
            {
                RequireAsset<ScriptableRendererData>(SourceRendererPath);
                if (!AssetDatabase.CopyAsset(SourceRendererPath, LabRendererPath))
                    throw new InvalidOperationException(
                        $"Could not copy {SourceRendererPath} to {LabRendererPath}.");

                AssetDatabase.ImportAsset(LabRendererPath);
                rendererData = RequireAsset<ScriptableRendererData>(LabRendererPath);
            }

            // The volumetric pass reads the colour it writes, so it can never run on the backbuffer.
            var rendererObject = new SerializedObject(rendererData);
            SerializedProperty intermediate =
                rendererObject.FindProperty("m_IntermediateTextureMode");
            if (intermediate != null)
                intermediate.enumValueIndex = (int)IntermediateTextureMode.Always;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();

            AddWaterVolumeFeature(rendererData, volumeMaterial);
            return rendererData;
        }

        private static void AddWaterVolumeFeature(
            ScriptableRendererData rendererData, Material volumeMaterial)
        {
            Water_Volume feature = null;
            foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
            {
                if (existing is Water_Volume found)
                {
                    feature = found;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<Water_Volume>();
                feature.name = "Water_Volume";
                feature.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.SaveAssets();
                AppendFeatureToSerializedLists(rendererData, feature);
            }

            feature.settings.material = volumeMaterial;
            feature.settings.renderPass = RenderPassEvent.BeforeRenderingPostProcessing;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>
        /// Appends to both serialized lists at once: URP keeps a parallel local-id map and only
        /// repairs it when the feature list contains nulls, so writing just the list would leave a
        /// stale map behind.
        /// </summary>
        private static void AppendFeatureToSerializedLists(
            ScriptableRendererData rendererData, ScriptableRendererFeature feature)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature, out string _, out long localId))
                throw new InvalidOperationException(
                    "The Water_Volume feature was not persisted into the renderer asset.");

            var rendererObject = new SerializedObject(rendererData);
            SerializedProperty features = rendererObject.FindProperty("m_RendererFeatures");
            SerializedProperty map = rendererObject.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            map.arraySize = features.arraySize;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;

            rendererObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Adds the lab renderer to the PC pipeline asset and returns its camera index.</summary>
        private static int EnsureRendererRegistered(ScriptableRendererData rendererData)
        {
            var pipeline = RequireAsset<RenderPipelineAsset>(PipelineAssetPath);
            var pipelineObject = new SerializedObject(pipeline);
            SerializedProperty list = pipelineObject.FindProperty("m_RendererDataList");

            for (int index = 0; index < list.arraySize; index++)
            {
                if (list.GetArrayElementAtIndex(index).objectReferenceValue == rendererData)
                    return index;
            }

            list.arraySize++;
            int added = list.arraySize - 1;
            list.GetArrayElementAtIndex(added).objectReferenceValue = rendererData;
            pipelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return added;
        }

        // ---- Scene geometry -----------------------------------------------------------------

        private static void BuildSeabed()
        {
            var seabed = new GameObject("Seabed");
            foreach (Terrace terrace in Terraces)
            {
                float depth = terrace.ZEnd - terrace.ZStart;
                float thickness = 8f;
                CreateBlock(
                    seabed.transform,
                    terrace.Name,
                    new Vector3(
                        0f,
                        terrace.TopY - thickness * 0.5f,
                        (terrace.ZStart + terrace.ZEnd) * 0.5f),
                    new Vector3(PoolHalfWidth * 2f, thickness, depth),
                    GetOrCreateMaterial($"Seabed_{terrace.Name}", terrace.Color));
            }

            // Side walls keep the player inside the pool instead of walking off the seabed slab.
            Material wall = GetOrCreateMaterial("Pool_Wall", new Color(0.24f, 0.24f, 0.26f));
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBlock(
                    seabed.transform,
                    side < 0 ? "West Wall" : "East Wall",
                    new Vector3(side * PoolHalfWidth, 0f, 0f),
                    new Vector3(4f, 60f, 240f),
                    wall);
            }
        }

        /// <summary>Submerged staircase: each step sits at a known depth, so the depth gradient is measurable.</summary>
        private static void BuildDepthStation()
        {
            var station = new GameObject("Station A - Depth Fade");
            Material material = GetOrCreateMaterial("Station_Depth", new Color(0.86f, 0.86f, 0.84f));

            for (int step = 0; step < 8; step++)
            {
                float top = WaterLevel - 0.25f - step * 1.1f;
                CreateBlock(
                    station.transform,
                    $"Step {step} ({top:0.0}m)",
                    new Vector3(-75f, top - 1f, -62f + step * 4f),
                    new Vector3(14f, 2f, 4f),
                    material);
            }
        }

        /// <summary>
        /// Striped poles crossing the waterline: the offset between the dry and the submerged half
        /// of each stripe is exactly the refraction the shader applies.
        /// </summary>
        private static void BuildRefractionStation()
        {
            var station = new GameObject("Station B - Refraction");
            Material light = GetOrCreateMaterial("Station_Stripe_Light", new Color(0.92f, 0.90f, 0.86f));
            Material dark = GetOrCreateMaterial("Station_Stripe_Dark", new Color(0.09f, 0.10f, 0.12f));

            for (int pole = 0; pole < 6; pole++)
            {
                float x = -34f + pole * 5f;
                for (int band = 0; band < 10; band++)
                {
                    float y = -4.5f + band;
                    CreateBlock(
                        station.transform,
                        $"Pole {pole} Band {band}",
                        new Vector3(x, y, -40f),
                        new Vector3(2.4f, 1f, 2.4f),
                        band % 2 == 0 ? light : dark);
                }
            }
        }

        /// <summary>Tall pillars and emissive blocks next to the water: nothing else shows screen-space reflections as clearly.</summary>
        private static void BuildReflectionStation()
        {
            var station = new GameObject("Station C - Reflection");
            Material pillar = GetOrCreateMaterial("Station_Pillar", new Color(0.88f, 0.87f, 0.82f));
            Material emissive = GetOrCreateEmissiveMaterial(
                "Station_Beacon", new Color(1f, 0.45f, 0.12f), 4f);

            for (int index = 0; index < 4; index++)
            {
                float x = 22f + index * 9f;
                float height = 14f + index * 4f;
                CreateBlock(
                    station.transform,
                    $"Pillar {index}",
                    new Vector3(x, WaterLevel + height * 0.5f - 2f, -30f),
                    new Vector3(3f, height, 3f),
                    pillar);
            }

            for (int index = 0; index < 3; index++)
            {
                CreateBlock(
                    station.transform,
                    $"Beacon {index}",
                    new Vector3(26f + index * 12f, WaterLevel + 3f, -46f),
                    new Vector3(2.5f, 2.5f, 2.5f),
                    emissive);
            }
        }

        /// <summary>
        /// Fixed-height gauges marching away from the shore: the wave amplitude visibly dies out at
        /// the material's Max Wave Dist, which is the main gotcha of this shader.
        /// </summary>
        private static void BuildWaveStation()
        {
            var station = new GameObject("Station D - Waves");
            Material material = GetOrCreateMaterial("Station_Gauge", new Color(0.85f, 0.24f, 0.28f));

            float[] distances = { -50f, -20f, 10f, 40f, 70f, 100f };
            for (int index = 0; index < distances.Length; index++)
            {
                CreateBlock(
                    station.transform,
                    $"Gauge {distances[index]:0}m",
                    new Vector3(72f, WaterLevel, distances[index]),
                    new Vector3(2f, 6f, 2f),
                    material);
            }
        }

        private static GameObject BuildWater(Material material, Material volumeMaterial)
        {
            GameObject water = UseAuthorDemoLook
                ? BuildAuthorOceanPlane()
                : BuildDenseWaterGrid();

            MeshRenderer renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Aligns the underwater volume box with this surface. It must be handed the same
            // project copy the renderer feature uses, or it keeps writing into the package
            // material in Resources and the box never moves to the water line.
            Water_Settings settings = water.GetComponent<Water_Settings>();
            if (settings == null)
                settings = water.AddComponent<Water_Settings>();
            settings.SetVolumeMaterial(volumeMaterial);
            PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
            EditorUtility.SetDirty(settings);

            // Write the box position here too instead of trusting the [ExecuteAlways] tick: until
            // it runs, the material keeps the author's -245, which puts the top of the volume at
            // +5 and drowns the above-water view in fog.
            AlignVolumeBox(volumeMaterial, water.transform.position.y);
            return water;
        }

        /// <summary>
        /// Puts the top face of the underwater volume exactly on the water line, using the same
        /// formula <see cref="Water_Settings"/> applies at runtime.
        /// </summary>
        private static void AlignVolumeBox(Material volumeMaterial, float waterY)
        {
            float height = volumeMaterial.GetVector("bounds").y;
            volumeMaterial.SetVector("pos", new Vector4(0f, (height / -2f) + waterY, 0f, 0f));
            EditorUtility.SetDirty(volumeMaterial);
        }

        /// <summary>The package's own ocean plane, at the scale its demo scene uses - no visible horizon edge.</summary>
        private static GameObject BuildAuthorOceanPlane()
        {
            var prefab = RequireAsset<GameObject>(WaterPlanePrefabPath);
            var water = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (water == null)
                throw new InvalidOperationException($"Could not instantiate {WaterPlanePrefabPath}.");

            water.name = "WaterWorks Ocean";
            water.transform.position = new Vector3(0f, WaterLevel, 0f);
            water.transform.localScale = new Vector3(1000f, 1f, 1000f);
            return water;
        }

        /// <summary>A 200x200 vertex grid: the only way vertex displacement is visible at all.</summary>
        private static GameObject BuildDenseWaterGrid()
        {
            var mesh = RequireAsset<Mesh>(WaterMeshPath);

            var water = new GameObject("WaterWorks Surface");
            water.transform.position = new Vector3(0f, WaterLevel, 0f);
            // The 100x100 unit grid covers the whole pool at 2.4x, one vertex per world unit.
            water.transform.localScale = new Vector3(2.4f, 1f, 2.4f);

            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            water.AddComponent<MeshRenderer>();
            return water;
        }

        /// <summary>
        /// Demo-scene lighting when reproducing the author's look. Their sun is intensity 5 against
        /// a flat bluish ambient - four times the project sun, and most of what makes this water
        /// sparkle instead of shimmer.
        /// </summary>
        private static void BuildLighting()
        {
            var lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            if (UseAuthorDemoLook)
            {
                sun.intensity = 5f;
                sun.color = new Color(1f, 0.95686275f, 0.8392157f);
                lightObject.transform.rotation = Quaternion.Euler(70f, -30f, 0f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.56078434f, 0.59607846f, 0.67058825f);
            }
            else
            {
                sun.intensity = 1.25f;
                sun.color = new Color(1f, 0.96f, 0.89f);
                lightObject.transform.rotation = Quaternion.Euler(46f, -28f, 0f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.44f, 0.51f, 0.56f);
            }

            RenderSettings.sun = sun;
        }

        /// <summary>
        /// The author's post profile (ACES, bloom 1.0 with lens dirt at 5, vignette, motion blur) or
        /// the project one. The trailer look is largely this profile, not the water shader.
        /// </summary>
        private static void BuildPostProcessing()
        {
            if (!UseAuthorDemoLook)
            {
                PostProcessingSetup.SetupOpenScene();
                return;
            }

            var profile = RequireAsset<VolumeProfile>(DemoPostProfilePath);
            var volumeObject = new GameObject("Global Post Processing - WaterWorks Demo Profile");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static void BuildLabels()
        {
            CreateLabel("Label - Lab", new Vector3(0f, 6f, -118f), 0.13f,
                "WATERWORKS LAB",
                "F6 feature panel  |  F4 fly (Space / Left Ctrl)",
                "Walk north to go under - the trench is the volumetric test");

            CreateLabel("Label - Station A", new Vector3(-75f, 4f, -66f), 0.07f,
                "A - DEPTH FADE",
                "Steps at known depths");
            CreateLabel("Label - Station B", new Vector3(-21f, 5f, -40f), 0.07f,
                "B - REFRACTION",
                "Stripe offset at the waterline");
            CreateLabel("Label - Station C", new Vector3(40f, 6f, -46f), 0.07f,
                "C - REFLECTION",
                "Pillars and beacons for SSR");
            CreateLabel("Label - Station D", new Vector3(72f, 5f, -56f), 0.07f,
                "D - WAVES",
                UseAuthorDemoLook
                    ? "Off: the author ocean plane is an 11x11 quad"
                    : "Amplitude dies at Max Wave Dist");
        }

        private static void CreateLabel(
            string name, Vector3 position, float characterSize, params string[] lines)
        {
            var label = new GameObject(name);
            label.transform.position = position;

            for (int index = 0; index < lines.Length; index++)
            {
                var line = new GameObject($"Line {index}");
                line.transform.SetParent(label.transform, false);
                line.transform.localPosition = new Vector3(0f, -index * characterSize * 9f, 0f);

                TextMesh text = line.AddComponent<TextMesh>();
                text.text = lines[index];
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = index == 0 ? characterSize : characterSize * 0.6f;
                text.color = index == 0 ? Color.white : new Color(0.78f, 0.88f, 0.94f);
            }
        }

        private static void BuildPlayer(
            GameObject water, Material[] waterMaterials, Material volumeMaterial, int rendererIndex)
        {
            var prefab = RequireAsset<GameObject>(PlayerPrefabPath);
            var player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (player == null)
                throw new InvalidOperationException($"Could not instantiate {PlayerPrefabPath}.");

            player.name = "Player";
            // Stand in the shallows facing the stations, not up on the dry beach: the demo camera
            // sits ~1 unit above the surface, and this water only reads at that height. Seen from
            // the beach it is all grazing angle and the ripples vanish.
            player.transform.position = new Vector3(6f, Terraces[1].TopY + 1.2f, -62f);
            player.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            InteractionSystem interaction = player.GetComponent<InteractionSystem>();

            if (player.GetComponent<DebugFlyMode>() == null)
                player.AddComponent<DebugFlyMode>();

            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The player prefab has no camera.");

            // Matches the demo camera: at 600 the 10000-unit ocean plane gets clipped into a false horizon.
            camera.farClipPlane = 1000f;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(rendererIndex);
            cameraData.requiresColorTexture = true;
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = true;

            var uiModeObject = new GameObject("UI Mode Service");
            UIModeService uiMode = uiModeObject.AddComponent<UIModeService>();
            SetObjectReference(uiMode, "playerController", controller);
            SetObjectReference(uiMode, "interactionSystem", interaction);

            WaterWorksLabController lab = player.AddComponent<WaterWorksLabController>();
            lab.Configure(
                water.GetComponent<Renderer>(), waterMaterials, volumeMaterial, controller);
            EditorUtility.SetDirty(lab);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static void CreateBlock(
            Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateEmissiveMaterial(string name, Color color, float intensity)
        {
            Material material = GetOrCreateMaterial(name, color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetObjectReference(
            UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T RequireAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
                throw new InvalidOperationException($"Required asset is missing: {assetPath}");
            return asset;
        }
    }
}
