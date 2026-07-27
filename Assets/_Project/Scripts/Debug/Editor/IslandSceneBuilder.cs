using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// One-shot Editor builder that generates the main gameplay location: a cozy temperate
    /// farm/trading island on a Unity Terrain, ringed by Bitgem stylized water. Layout is
    /// zoned (market hub, harbor, farm, pasture, fishing
    /// dock, crafting, town, forest hill) so every project subsystem has a home. Re-runnable:
    /// rebuilds the terrain data / layers deterministically from <see cref="Seed"/>.
    /// Temporary debug tooling (see AGENTS.md); the produced scene/assets are the real content.
    /// </summary>
    public static class IslandSceneBuilder
    {
        private const int Seed = 1337;

        private const string ScenePath = "Assets/_Project/Scenes/Island.unity";
        private const string TerrainDir = "Assets/_Project/Art/Terrain";
        private const string WaterMeshDir = "Assets/_Project/Art/Meshes/Water";
        private const string WaterMeshPath =
            WaterMeshDir + "/IslandStylizedWaterGrid.asset";
        private const string WaterObjectName = "Island Stylized Water";
        private const float WaterSize = 950f;
        private const int WaterGridResolution = 128;
        private const string LightingRootName = "Bitgem Lighting";
        private const string VolumeObjectName = "Bitgem Post Processing";
        private const string ReflectionProbeName = "Bitgem Reflection Probe";
        private const string SkyboxPath =
            "Assets/Bitgem/StylisedWater/URP/Materials/skybox.mat";
        private const string VolumeProfilePath =
            "Assets/Bitgem/StylisedWater/URP/Examples/StylisedWaterProfile.asset";
        private const string LightingSettingsPath =
            "Assets/Bitgem/StylisedWater/URP/Examples/Example-Scene-01Settings.lighting";

        private const string WaterMaterialCopyDir = "Assets/_Project/Art/Materials/Water";

        // Source materials only. The scene is always bound to the project copies below, never to
        // the imported package assets - see EnsureProjectCopy.
        private static readonly string[] WaterMaterialPaths =
        {
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-01.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-02.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-03.mat",
        };

        // World units.
        private const float TerrainWidth = 500f;
        private const float TerrainLength = 500f;
        private const float TerrainHeight = 60f;
        private const int HeightmapRes = 513;   // must be 2^n + 1
        private const int AlphamapRes = 512;
        private const float CameraFarClip = 750f;

        // Normalized (0..1 of TerrainHeight) sea level. World Y = WaterLevel01 * TerrainHeight.
        private const float WaterLevel01 = 0.20f;

        [MenuItem("Market/Debug/Build Island Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(TerrainDir);
            Directory.CreateDirectory(WaterMeshDir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            CreateEnvironmentVolume();
            CreateReflectionProbe();
            TerrainData terrainData = BuildTerrainData();
            GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = "Island_Terrain";
            terrainGo.isStatic = true;
            Terrain terrain = terrainGo.GetComponent<Terrain>();
            ConfigureTerrain(terrain);

            CreateWater();
            CreateZoneAnchors(terrain);
            CreateCamera(terrain);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"IslandSceneBuilder: built '{ScenePath}' " +
                      $"(terrain {TerrainWidth}x{TerrainLength}, sea level Y={WaterLevel01 * TerrainHeight:0.#}).");
        }

        /// <summary>
        /// Replaces only the water in the existing Island scene and preserves all other content.
        /// </summary>
        [MenuItem("Market/Debug/Water/Replace Island Water")]
        public static void ReplaceOpenIslandWater()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"IslandSceneBuilder: open '{ScenePath}' before replacing water.");
                return;
            }

            DestroyRoot(scene, "Ocean");
            DestroyRoot(scene, WaterObjectName);
            Directory.CreateDirectory(WaterMeshDir);
            CreateWater();

            foreach (Camera camera in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include))
            {
                ConfigureCamera(camera);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("IslandSceneBuilder: replaced Island water with Bitgem stylized water.");
        }

        /// <summary>
        /// Applies the complete Bitgem demo environment without rebuilding other Island content.
        /// </summary>
        [MenuItem("Market/Debug/Environment/Apply Bitgem Preset to Island")]
        public static void ApplyOpenIslandEnvironmentPreset()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError(
                    $"IslandSceneBuilder: open '{ScenePath}' before applying the environment.");
                return;
            }

            DestroyRoot(scene, "Directional Light");
            DestroyRoot(scene, LightingRootName);
            DestroyRoot(scene, VolumeObjectName);
            DestroyRoot(scene, ReflectionProbeName);
            SetupLighting();
            CreateEnvironmentVolume();
            CreateReflectionProbe();

            foreach (Camera camera in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include))
            {
                ConfigureCamera(camera);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("IslandSceneBuilder: applied the full Bitgem environment preset.");
        }

        [MenuItem("Market/Debug/Optimize Open Island Scene")]
        public static void OptimizeOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"IslandSceneBuilder: open '{ScenePath}' before optimizing.");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                ConfigureComponents(roots[i]);
            }

            RenderSettings.fogEndDistance = CameraFarClip;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("IslandSceneBuilder: applied Island performance settings and saved the scene.");
        }

        private static void ConfigureComponents(GameObject root)
        {
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                ConfigureTerrain(terrain);
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                ConfigureCamera(camera);
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
                ConfigureLight(light);
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.gameObject.name == WaterObjectName)
                    ConfigureWaterRenderer(renderer);
        }

        // ---- Lighting ---------------------------------------------------------------------

        private static void SetupLighting()
        {
            Lightmapping.lightingSettings =
                LoadRequiredAsset<LightingSettings>(LightingSettingsPath);
            var root = new GameObject(LightingRootName);
            Light sun = CreateDirectionalLight(
                root.transform,
                "Sun",
                new Color(1f, 0.88502806f, 0.6273585f),
                4.01f,
                new Vector3(50.43f, 14f, 0f),
                LightShadows.Soft);
            CreateDirectionalLight(
                root.transform,
                "Backlight",
                new Color(0.3272072f, 0.37035647f, 0.4056604f),
                0.5f,
                new Vector3(13.96f, 194f, 0f),
                LightShadows.None);
            ConfigureEnvironmentRenderSettings(sun);
        }

        private static Light CreateDirectionalLight(
            Transform parent,
            string name,
            Color color,
            float intensity,
            Vector3 rotation,
            LightShadows shadows)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(rotation);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            ConfigureLight(light);
            return light;
        }

        private static void ConfigureEnvironmentRenderSettings(Light sun)
        {
            RenderSettings.skybox = LoadRequiredAsset<Material>(SkyboxPath);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.7f);
            RenderSettings.ambientIntensity = 0.88f;
            RenderSettings.subtractiveShadowColor =
                new Color(0.42f, 0.478f, 0.627f);
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.4009434f, 0.80695605f, 1f);
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = CameraFarClip;
            DynamicGI.UpdateEnvironment();
        }

        private static void CreateEnvironmentVolume()
        {
            VolumeProfile profile = LoadRequiredAsset<VolumeProfile>(VolumeProfilePath);
            var volumeObject = new GameObject(VolumeObjectName);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static void CreateReflectionProbe()
        {
            var probeObject = new GameObject(ReflectionProbeName);
            probeObject.transform.position =
                new Vector3(TerrainWidth * 0.5f, 30f, TerrainLength * 0.5f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(TerrainWidth, 120f, TerrainLength);
            probe.farClipPlane = CameraFarClip;
            probe.shadowDistance = 100f;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            int waterLayer = LayerMask.NameToLayer("Water");
            probe.cullingMask = waterLayer >= 0
                ? ~(1 << waterLayer)
                : ~0;
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException(
                    $"IslandSceneBuilder: required asset is missing: {path}");
            return asset;
        }

        private static void ConfigureLight(Light light)
        {
            if (light.type != LightType.Directional)
                return;

            light.shadowResolution = LightShadowResolution.Medium;
            light.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.Low;
            EditorUtility.SetDirty(light);
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 15f;
            terrain.basemapDistance = 250f;
            terrain.treeDistance = 500f;
            terrain.treeBillboardDistance = 40f;
            terrain.treeMaximumFullLODCount = 20;
            terrain.detailObjectDistance = 40f;
            terrain.detailObjectDensity = 0.7f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.enableHeightmapRayTracing = false;
            terrain.enableHeightmapLODFrustumCulling = false;
            EditorUtility.SetDirty(terrain);
        }

        // ---- Terrain heightmap + splat ----------------------------------------------------

        private static TerrainData BuildTerrainData()
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightmapRes,
                alphamapResolution = AlphamapRes,
                baseMapResolution = 512,
                size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength)
            };
            data.SetDetailResolution(512, 16);

            float[,] heights = GenerateHeights();
            data.SetHeights(0, 0, heights);

            data.terrainLayers = BuildTerrainLayers();
            data.SetAlphamaps(0, 0, GenerateSplat(heights));

            AssetDatabase.CreateAsset(data, $"{TerrainDir}/Island_TerrainData.asset");
            return data;
        }

        private static float[,] GenerateHeights()
        {
            var h = new float[HeightmapRes, HeightmapRes];
            // Deterministic noise offsets from the seed.
            var rng = new System.Random(Seed);
            float o1 = (float)rng.NextDouble() * 100f;
            float o2 = (float)rng.NextDouble() * 100f;
            float o3 = (float)rng.NextDouble() * 100f;

            for (int y = 0; y < HeightmapRes; y++)
            {
                float v = y / (float)(HeightmapRes - 1);
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float u = x / (float)(HeightmapRes - 1);

                    // Radial distance from centre (0..~1.41).
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Wobble the coastline so it is not a perfect circle.
                    float warp = (Mathf.PerlinNoise(u * 2.5f + o1, v * 2.5f + o1) - 0.5f) * 0.28f;

                    // Island mask: 1 inland, 0 offshore, with a fairly steep shore so the
                    // shallows (and the foam ring) stay a tidy band instead of a wide frill.
                    float landMask = 1f - SStep(0.50f, 0.70f, dist + warp);
                    float plateau = SStep(0.02f, 0.5f, landMask);

                    // Seabed baseline below the waterline (0.20); the land bulge raises the
                    // broad buildable interior clearly above the water.
                    float baseLand = 0.06f + plateau * 0.24f;

                    // Broad inland hills for a real silhouette.
                    float bigHills = Mathf.PerlinNoise(u * 1.3f + o3, v * 1.3f + o3);
                    float hill = SStep(0.45f, 0.95f, bigHills) * 0.30f * plateau;

                    // Two-octave rolling so the interior is not a flat disc.
                    float rolling = ((Mathf.PerlinNoise(u * 3.5f + o2, v * 3.5f + o2) - 0.5f) * 0.06f
                                   + (Mathf.PerlinNoise(u * 7f + o2, v * 7f + o2) - 0.5f) * 0.03f) * plateau;

                    h[y, x] = Mathf.Clamp01(baseLand + hill + rolling);
                }
            }
            return h;
        }

        private static float[,,] GenerateSplat(float[,] heights)
        {
            var splat = new float[AlphamapRes, AlphamapRes, 3]; // 0 grass, 1 sand, 2 rock
            for (int y = 0; y < AlphamapRes; y++)
            {
                float fy = y / (float)(AlphamapRes - 1);
                for (int x = 0; x < AlphamapRes; x++)
                {
                    float fx = x / (float)(AlphamapRes - 1);
                    float height01 = SampleBilinear(heights, fx, fy);
                    float slope = SampleSlope(heights, fx, fy);

                    // Beach band just above the waterline (wide enough to read as sand).
                    float sand = 1f - SStep(WaterLevel01 - 0.01f, WaterLevel01 + 0.07f, height01);
                    // Rock on steep faces and high peaks.
                    float rock = SStep(0.16f, 0.34f, slope)
                                 + SStep(0.42f, 0.5f, height01);
                    rock = Mathf.Clamp01(rock) * (1f - sand);
                    float grass = Mathf.Max(0f, 1f - sand - rock);

                    float sum = sand + rock + grass + 1e-5f;
                    splat[y, x, 0] = grass / sum;
                    splat[y, x, 1] = sand / sum;
                    splat[y, x, 2] = rock / sum;
                }
            }
            return splat;
        }

        private static TerrainLayer[] BuildTerrainLayers()
        {
            TerrainLayer grass = MakeLayer("Island_Grass",
                new Color(0.36f, 0.54f, 0.27f), new Color(0.32f, 0.48f, 0.24f), 18f);
            TerrainLayer sand = MakeLayer("Island_Sand",
                new Color(0.86f, 0.79f, 0.56f), new Color(0.82f, 0.74f, 0.50f), 12f);
            TerrainLayer rock = MakeLayer("Island_Rock",
                new Color(0.46f, 0.45f, 0.43f), new Color(0.38f, 0.37f, 0.36f), 14f);
            return new[] { grass, sand, rock };
        }

        private static TerrainLayer MakeLayer(string name, Color a, Color b, float tile)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name + "_Tex" };
            var rng = new System.Random(name.GetHashCode());
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                // Low-frequency two-tone speckle so flat cartoon colour still has some grain.
                float n = (float)rng.NextDouble();
                pixels[i] = Color.Lerp(a, b, n * 0.85f);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            AssetDatabase.CreateAsset(tex, $"{TerrainDir}/{name}_Tex.asset");

            var layer = new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(tile, tile),
                name = name
            };
            AssetDatabase.CreateAsset(layer, $"{TerrainDir}/{name}.terrainlayer");
            return layer;
        }

        // ---- Water ------------------------------------------------------------------------

        private static void CreateWater()
        {
            Material[] materials = LoadWaterMaterials();
            var waterObject = new GameObject(WaterObjectName);
            int waterLayer = LayerMask.NameToLayer("Water");
            waterObject.layer = waterLayer >= 0 ? waterLayer : waterObject.layer;

            float y = WaterLevel01 * TerrainHeight;
            waterObject.transform.position =
                new Vector3(TerrainWidth * 0.5f, y, TerrainLength * 0.5f);

            MeshFilter filter = waterObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildWaterMesh();
            MeshRenderer renderer = waterObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[0];
            ConfigureWaterRenderer(renderer);

            WaterMaterialSwitcher switcher =
                waterObject.AddComponent<WaterMaterialSwitcher>();
            switcher.Configure(renderer, materials);
        }

        private static void ConfigureWaterRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorUtility.SetDirty(renderer);
        }

        private static Material[] LoadWaterMaterials()
        {
            var materials = new Material[WaterMaterialPaths.Length];
            for (int index = 0; index < WaterMaterialPaths.Length; index++)
            {
                materials[index] = EnsureProjectCopy(
                    WaterMaterialPaths[index],
                    $"StylizedWater_{index + 1:00}");
            }

            return materials;
        }

        /// <summary>
        /// Returns a project-owned copy of a package water material, created on first use. Tuning
        /// (the editor window or the in-game F7 panel) then always edits project assets and can
        /// never overwrite the imported package materials. An existing copy is kept untouched, so
        /// tuned values survive a scene rebuild.
        /// </summary>
        private static Material EnsureProjectCopy(string packagePath, string copyName)
        {
            string copyPath = $"{WaterMaterialCopyDir}/{copyName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(WaterMaterialCopyDir);
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(packagePath, copyPath))
                throw new IOException($"IslandSceneBuilder: could not copy '{packagePath}'.");

            AssetDatabase.ImportAsset(copyPath);
            Debug.Log($"IslandSceneBuilder: created '{copyPath}'.");
            return LoadRequiredAsset<Material>(copyPath);
        }

        private static Mesh BuildWaterMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "IslandStylizedWaterGrid" };
            else
                mesh.Clear();

            PopulateWaterMesh(mesh);
            if (isNew)
                AssetDatabase.CreateAsset(mesh, WaterMeshPath);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void PopulateWaterMesh(Mesh mesh)
        {
            int row = WaterGridResolution + 1;
            var vertices = new Vector3[row * row];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[WaterGridResolution * WaterGridResolution * 6];

            PopulateWaterVertices(vertices, normals, uvs, colors, row);
            PopulateWaterTriangles(triangles, row);
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateTangents();
            mesh.bounds = new Bounds(
                Vector3.zero,
                new Vector3(WaterSize, 10f, WaterSize));
        }

        private static void PopulateWaterVertices(
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            Color[] colors,
            int row)
        {
            for (int z = 0; z < row; z++)
            {
                float z01 = z / (float)WaterGridResolution;
                for (int x = 0; x < row; x++)
                {
                    float x01 = x / (float)WaterGridResolution;
                    int index = z * row + x;
                    vertices[index] = new Vector3(
                        (x01 - 0.5f) * WaterSize,
                        0f,
                        (z01 - 0.5f) * WaterSize);
                    normals[index] = Vector3.up;
                    uvs[index] = new Vector2(x01 * WaterSize, z01 * WaterSize);
                    colors[index] = Color.black;
                }
            }
        }

        private static void PopulateWaterTriangles(int[] triangles, int row)
        {
            int triangle = 0;
            for (int z = 0; z < WaterGridResolution; z++)
            {
                for (int x = 0; x < WaterGridResolution; x++)
                {
                    int bottomLeft = z * row + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + row;
                    int topRight = topLeft + 1;
                    triangles[triangle++] = bottomLeft;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = topRight;
                }
            }
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    Object.DestroyImmediate(root);
                    return;
                }
            }
        }

        // ---- Zone anchors -----------------------------------------------------------------

        private static void CreateZoneAnchors(Terrain terrain)
        {
            var root = new GameObject("ZoneAnchors");

            // Terrain-local XZ (0..500) planning slots for every subsystem home.
            AddAnchor(root, terrain, "Zone_MarketSquare", 250f, 250f);
            AddAnchor(root, terrain, "Zone_Harbor_Supplier", 250f, 130f);
            AddAnchor(root, terrain, "Zone_FishingDock", 360f, 175f);
            AddAnchor(root, terrain, "Zone_FarmFields", 345f, 320f);
            AddAnchor(root, terrain, "Zone_AnimalPasture", 155f, 320f);
            AddAnchor(root, terrain, "Zone_CraftingYard", 150f, 200f);
            AddAnchor(root, terrain, "Zone_TownCenter", 250f, 335f);
            AddAnchor(root, terrain, "Zone_ForestHill", 335f, 400f);
        }

        private static void AddAnchor(GameObject root, Terrain terrain, string name, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var pos = new Vector3(x, 0f, z);
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            go.transform.position = pos;
        }

        // ---- Camera -----------------------------------------------------------------------

        private static void CreateCamera(Terrain terrain)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            // Establishing view: elevated 3/4 aerial from the south looking north over the isle.
            camGo.transform.position = new Vector3(TerrainWidth * 0.5f, 130f, 40f);
            camGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            ConfigureCamera(cam);
            camGo.AddComponent<AudioListener>();
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.farClipPlane = CameraFarClip;
            camera.allowHDR = true;
            camera.allowMSAA = false;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1;
            data.antialiasing = AntialiasingMode.None;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
        }

        // GLSL-style edge smoothstep: 0 below e0, 1 above e1, smooth between.
        // (Unity's Mathf.SmoothStep is a smoothed lerp from a to b - not the same thing.)
        private static float SStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float SampleBilinear(float[,] map, float u, float v)
        {
            int n = map.GetLength(0) - 1;
            float fx = u * n;
            float fy = v * n;
            int x0 = Mathf.Clamp((int)fx, 0, n);
            int y0 = Mathf.Clamp((int)fy, 0, n);
            int x1 = Mathf.Min(x0 + 1, n);
            int y1 = Mathf.Min(y0 + 1, n);
            float tx = fx - x0;
            float ty = fy - y0;
            float a = Mathf.Lerp(map[y0, x0], map[y0, x1], tx);
            float b = Mathf.Lerp(map[y1, x0], map[y1, x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static float SampleSlope(float[,] map, float u, float v)
        {
            int n = map.GetLength(0) - 1;
            float e = 1f / n;
            float hL = SampleBilinear(map, Mathf.Clamp01(u - e), v);
            float hR = SampleBilinear(map, Mathf.Clamp01(u + e), v);
            float hD = SampleBilinear(map, u, Mathf.Clamp01(v - e));
            float hU = SampleBilinear(map, u, Mathf.Clamp01(v + e));
            // Convert normalized-height delta over one cell to a world gradient magnitude.
            float cell = TerrainWidth / n;
            float gx = (hR - hL) * TerrainHeight / (2f * cell);
            float gz = (hU - hD) * TerrainHeight / (2f * cell);
            return Mathf.Sqrt(gx * gx + gz * gz);
        }
    }
}
