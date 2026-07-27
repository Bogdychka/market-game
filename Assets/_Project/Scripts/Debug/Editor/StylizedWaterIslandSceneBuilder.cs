using System.Collections.Generic;
using System.IO;
using Market.Interaction;
using Market.Player;
using Market.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Builds a compact Unity Terrain island ringed by Bitgem stylized water. Every water and
    /// environment setting is copied from StylizedWaterProto.unity (materials, lighting, fog,
    /// skybox, ambient, post-processing profile, reflection probe and camera render options), so
    /// the water reads exactly like the package showcase. The scene is kept at showcase scale
    /// because the copied linear fog (10..30) is authored for a lagoon-sized view.
    /// Temporary debug tooling (see AGENTS.md); the produced scene and assets are the content.
    /// </summary>
    public static class StylizedWaterIslandSceneBuilder
    {
        private const int Seed = 20260724;

        private const string ScenePath =
            "Assets/_Project/Scenes/StylizedWaterIsland.unity";
        private const string TerrainDir =
            "Assets/_Project/Art/Terrain/StylizedWaterIsland";
        private const string TerrainDataPath =
            TerrainDir + "/StylizedWaterIsland_TerrainData.asset";
        private const string WaterMeshDir = "Assets/_Project/Art/Meshes/Water";
        private const string WaterMeshPath =
            WaterMeshDir + "/StylizedWaterIslandGrid.asset";
        private const string PlayerPrefabPath =
            "Assets/_Project/Art/Prefabs/Player/Player.prefab";
        private const string WaterMaterialCopyDir = "Assets/_Project/Art/Materials/Water";

        // Environment assets referenced by StylizedWaterProto.unity.
        private const string SkyboxPath =
            "Assets/Low-Poly Medieval Market/Materials/Skybox_Mat.mat";
        private const string VolumeProfilePath =
            "Assets/Settings/SampleSceneProfile.asset";

        private static readonly string[] WaterMaterialPaths =
        {
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-01.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-02.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-03.mat",
        };

        // World units. The island stays small so the showcase fog range still reads correctly.
        private const float TerrainSize = 48f;
        private const float TerrainHeight = 16f;
        private const int HeightmapRes = 129;   // must be 2^n + 1
        private const int AlphamapRes = 256;

        // Normalized (0..1 of TerrainHeight) sea level. World Y = WaterLevel01 * TerrainHeight.
        // The sea floor sits far enough below it that the water reaches its deepest shade before
        // the terrain ends, so the terrain border does not show up as a shallow square.
        private const float WaterLevel01 = 0.45f;

        // Water is one mesh built from concentric zones: the shore keeps the Bitgem tile size
        // (0.5) so the foam band and wave displacement match the package, while the open water
        // coarsens with distance. The outer zone reaches past the fog end distance, so the mesh
        // border always dissolves into fog instead of showing an edge. Every extent must be a
        // whole multiple of the next zone's cell size.
        private static readonly float[] WaterZoneExtents = { 30f, 110f, 220f };
        private static readonly float[] WaterZoneCells = { 0.5f, 2.5f, 10f };

        // Shore band (in world units below the waterline) that receives Bitgem foam.
        private const float FoamDepth = 0.3f;

        private const float CameraFarClip = 250f;

        private static float WaterY => WaterLevel01 * TerrainHeight;

        /// <summary>
        /// Rebuilds and opens the stylized-water island scene from scratch.
        /// </summary>
        [MenuItem("Market/Debug/Water/Build Stylized Water Island Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(TerrainDir);
            Directory.CreateDirectory(WaterMeshDir);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            SetupLighting();
            CreateEnvironmentVolume();
            Terrain terrain = CreateTerrain();
            CreateReflectionProbe();
            Renderer water = CreateWater(terrain);
            CreateCamera(water);
            GameObject player = CreatePlayer(terrain);
            CreateTuningRig(water, player);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log(
                $"StylizedWaterIslandSceneBuilder: built '{ScenePath}' " +
                $"(terrain {TerrainSize}x{TerrainSize}, water reach " +
                $"{WaterZoneExtents[WaterZoneExtents.Length - 1]}, sea level Y={WaterY:0.##}).");
        }

        // ---- Lighting and environment ---------------------------------------------------

        private static void SetupLighting()
        {
            var root = new GameObject("Bitgem Lighting");
            root.transform.rotation = Quaternion.Euler(0f, 14f, 0f);

            Light sun = CreateDirectionalLight(
                root.transform,
                "Sun",
                new Color(1f, 0.88502806f, 0.6273585f),
                4.01f,
                new Vector3(50.43f, 0f, 0f),
                LightShadows.Soft);
            CreateDirectionalLight(
                root.transform,
                "Backlight",
                new Color(0.3272072f, 0.37035647f, 0.4056604f),
                0.5f,
                new Vector3(13.96f, 180f, 0f),
                LightShadows.None);

            ConfigureEnvironmentRenderSettings(sun);
        }

        private static Light CreateDirectionalLight(
            Transform parent,
            string name,
            Color color,
            float intensity,
            Vector3 localRotation,
            LightShadows shadows)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(localRotation);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.bounceIntensity = 1.5f;
            light.shadows = shadows;
            return light;
        }

        private static void ConfigureEnvironmentRenderSettings(Light sun)
        {
            RenderSettings.skybox = LoadRequiredAsset<Material>(SkyboxPath);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);
            RenderSettings.ambientIntensity = 0.88f;
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f);
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.4009434f, 0.80695605f, 1f);
            // Fog colour and mode are the proto values. Only the distances are scaled up by the
            // island/lagoon size ratio - the proto range (10..30) is authored for a view a few
            // metres wide and would bury a 48 unit island in haze.
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 120f;
            DynamicGI.UpdateEnvironment();
        }

        private static void CreateEnvironmentVolume()
        {
            var volumeObject = new GameObject("Stylized Water Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = LoadRequiredAsset<VolumeProfile>(VolumeProfilePath);
        }

        private static void CreateReflectionProbe()
        {
            var probeObject = new GameObject("Bitgem Reflection Probe");
            probeObject.transform.position = new Vector3(0f, WaterY + 6f, 0f);

            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(220f, 60f, 220f);
            probe.farClipPlane = CameraFarClip;
            probe.shadowDistance = 100f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.4666667f, 0.7372549f, 0.77647066f, 0f);
            probe.hdr = true;

            int waterLayer = LayerMask.NameToLayer("Water");
            probe.cullingMask = waterLayer >= 0 ? ~(1 << waterLayer) : ~0;
        }

        // ---- Terrain ---------------------------------------------------------------------

        private static Terrain CreateTerrain()
        {
            TerrainData data = BuildTerrainData();
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Stylized Water Island Terrain";
            terrainObject.isStatic = true;
            terrainObject.transform.position =
                new Vector3(TerrainSize * -0.5f, 0f, TerrainSize * -0.5f);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            ConfigureTerrain(terrain);
            return terrain;
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 15f;
            terrain.basemapDistance = 120f;
            terrain.treeDistance = 120f;
            terrain.treeBillboardDistance = 30f;
            terrain.treeMaximumFullLODCount = 20;
            terrain.detailObjectDistance = 40f;
            terrain.detailObjectDensity = 0.7f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.enableHeightmapRayTracing = false;
            terrain.enableHeightmapLODFrustumCulling = false;
            EditorUtility.SetDirty(terrain);
        }

        private static TerrainData BuildTerrainData()
        {
            AssetDatabase.DeleteAsset(TerrainDataPath);

            var data = new TerrainData
            {
                heightmapResolution = HeightmapRes,
                alphamapResolution = AlphamapRes,
                baseMapResolution = 256,
                size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
            };
            data.SetDetailResolution(128, 16);

            float[,] heights = GenerateHeights();
            data.SetHeights(0, 0, heights);
            data.terrainLayers = BuildTerrainLayers();
            data.SetAlphamaps(0, 0, GenerateSplat(heights));

            AssetDatabase.CreateAsset(data, TerrainDataPath);
            return data;
        }

        private static float[,] GenerateHeights()
        {
            var heights = new float[HeightmapRes, HeightmapRes];
            var rng = new System.Random(Seed);
            float coastOffset = (float)rng.NextDouble() * 100f;
            float rollOffset = (float)rng.NextDouble() * 100f;
            float hillOffset = (float)rng.NextDouble() * 100f;

            for (int y = 0; y < HeightmapRes; y++)
            {
                float v = y / (float)(HeightmapRes - 1);
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float u = x / (float)(HeightmapRes - 1);
                    heights[y, x] = SampleIslandHeight(
                        u,
                        v,
                        coastOffset,
                        rollOffset,
                        hillOffset);
                }
            }

            return heights;
        }

        private static float SampleIslandHeight(
            float u,
            float v,
            float coastOffset,
            float rollOffset,
            float hillOffset)
        {
            float dx = (u - 0.5f) * 2f;
            float dy = (v - 0.5f) * 2f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            // Wobble the coastline so the island is not a perfect disc.
            float warp =
                (Mathf.PerlinNoise(u * 2.2f + coastOffset, v * 2.2f + coastOffset) - 0.5f) * 0.3f;

            float d = dist + warp;

            // The profile is built around the coastline instead of one island mask: outside it
            // the sea floor dives fast (so the water reaches its deepest shade well before the
            // terrain rim), inside it a flat beach turns into a gentle inland rise.
            float toDeep = SStep(0.58f, 0.74f, d);
            float toLand = 1f - SStep(0.18f, 0.5f, d);
            float shoreLine = WaterLevel01 + 0.012f;
            float baseLand = shoreLine - toDeep * (shoreLine - 0.03f) + toLand * 0.17f;

            float bigHills = Mathf.PerlinNoise(u * 1.6f + hillOffset, v * 1.6f + hillOffset);
            float hill = SStep(0.35f, 0.95f, bigHills) * 0.16f * toLand;
            float rolling =
                ((Mathf.PerlinNoise(u * 4.5f + rollOffset, v * 4.5f + rollOffset) - 0.5f) * 0.06f
                 + (Mathf.PerlinNoise(u * 9f + rollOffset, v * 9f + rollOffset) - 0.5f) * 0.025f)
                * toLand;

            return Mathf.Clamp01(baseLand + hill + rolling);
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

                    float sand = 1f - SStep(WaterLevel01 - 0.01f, WaterLevel01 + 0.05f, height01);
                    float rock = SStep(0.6f, 1.1f, slope) + SStep(0.86f, 0.96f, height01);
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
            TerrainLayer grass = MakeLayer("StylizedIsland_Grass",
                new Color(0.36f, 0.54f, 0.27f), new Color(0.32f, 0.48f, 0.24f), 6f);
            TerrainLayer sand = MakeLayer("StylizedIsland_Sand",
                new Color(0.86f, 0.79f, 0.56f), new Color(0.82f, 0.74f, 0.5f), 4f);
            TerrainLayer rock = MakeLayer("StylizedIsland_Rock",
                new Color(0.46f, 0.45f, 0.43f), new Color(0.38f, 0.37f, 0.36f), 5f);
            return new[] { grass, sand, rock };
        }

        private static TerrainLayer MakeLayer(string name, Color a, Color b, float tile)
        {
            const int size = 256;
            string texturePath = $"{TerrainDir}/{name}_Tex.asset";
            string layerPath = $"{TerrainDir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(texturePath);
            AssetDatabase.DeleteAsset(layerPath);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name + "_Tex",
                wrapMode = TextureWrapMode.Repeat
            };
            var rng = new System.Random(name.GetHashCode());
            var pixels = new Color32[size * size];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = Color.Lerp(a, b, (float)rng.NextDouble() * 0.85f);
            texture.SetPixels32(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, texturePath);

            var layer = new TerrainLayer
            {
                diffuseTexture = texture,
                tileSize = new Vector2(tile, tile),
                name = name
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        // ---- Water ------------------------------------------------------------------------

        private static Renderer CreateWater(Terrain terrain)
        {
            Material[] materials = LoadWaterMaterials();

            var waterObject = new GameObject("Stylized Water Volume");
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0)
                waterObject.layer = waterLayer;
            waterObject.transform.position = new Vector3(0f, WaterY, 0f);

            MeshFilter filter = waterObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildWaterMesh(terrain);

            MeshRenderer renderer = waterObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[0];
            ConfigureWaterRenderer(renderer);

            // Keeps material switching (F6, Shift+F6 reverses) available while playing as the
            // player, where the showcase camera and its 1-3 keys are disabled.
            WaterMaterialSwitcher switcher = waterObject.AddComponent<WaterMaterialSwitcher>();
            switcher.Configure(renderer, materials);
            return renderer;
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
        /// (the editor window or the in-game panel) then always edits project assets and can never
        /// overwrite the imported package materials. An existing copy is kept untouched, so tuned
        /// values survive a scene rebuild.
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
                throw new IOException(
                    $"StylizedWaterIslandSceneBuilder: could not copy '{packagePath}'.");

            AssetDatabase.ImportAsset(copyPath);
            Debug.Log($"StylizedWaterIslandSceneBuilder: created '{copyPath}'.");
            return LoadRequiredAsset<Material>(copyPath);
        }

        private static Mesh BuildWaterMesh(Terrain terrain)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "StylizedWaterIslandGrid" };
            else
                mesh.Clear();

            PopulateWaterMesh(mesh, terrain);
            if (isNew)
                AssetDatabase.CreateAsset(mesh, WaterMeshPath);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void PopulateWaterMesh(Mesh mesh, Terrain terrain)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int zone = 0; zone < WaterZoneExtents.Length; zone++)
            {
                float hole = zone == 0 ? 0f : WaterZoneExtents[zone - 1];
                AppendWaterZone(
                    terrain,
                    vertices,
                    colors,
                    uvs,
                    triangles,
                    WaterZoneExtents[zone],
                    WaterZoneCells[zone],
                    hole);
            }

            float outer = WaterZoneExtents[WaterZoneExtents.Length - 1] * 2f;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(outer, 4f, outer));
        }

        /// <summary>
        /// Appends one square water zone, skipping the quads already covered by a finer zone.
        /// </summary>
        private static void AppendWaterZone(
            Terrain terrain,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uvs,
            List<int> triangles,
            float extent,
            float cell,
            float hole)
        {
            int cells = Mathf.RoundToInt(extent * 2f / cell);
            int row = cells + 1;
            int first = vertices.Count;
            float terrainY = terrain.transform.position.y;

            for (int z = 0; z < row; z++)
            {
                float worldZ = -extent + z * cell;
                for (int x = 0; x < row; x++)
                {
                    float worldX = -extent + x * cell;
                    vertices.Add(new Vector3(worldX, 0f, worldZ));

                    // The Bitgem water mesh uses world XZ as UV, so tiling matches the package.
                    uvs.Add(new Vector2(worldX, worldZ));

                    float ground =
                        terrainY + terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
                    // Red vertex colour is the package foam mask, here along the shore band.
                    colors.Add(ground > WaterY - FoamDepth ? Color.red : Color.black);
                }
            }

            AppendWaterZoneTriangles(triangles, first, cells, row, extent, cell, hole);
        }

        private static void AppendWaterZoneTriangles(
            List<int> triangles,
            int first,
            int cells,
            int row,
            float extent,
            float cell,
            float hole)
        {
            for (int z = 0; z < cells; z++)
            {
                float centerZ = -extent + (z + 0.5f) * cell;
                for (int x = 0; x < cells; x++)
                {
                    float centerX = -extent + (x + 0.5f) * cell;
                    if (Mathf.Abs(centerX) < hole && Mathf.Abs(centerZ) < hole)
                        continue;

                    int bottomLeft = first + z * row + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + row;
                    int topRight = topLeft + 1;
                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(bottomRight);
                    triangles.Add(bottomRight);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                }
            }
        }

        // ---- Camera -----------------------------------------------------------------------

        private static void CreateCamera(Renderer water)
        {
            var focus = new GameObject("Stylized Water Camera Focus");
            focus.transform.position = new Vector3(0f, WaterY + 1f, 0f);

            // The player prefab brings its own camera, so the showcase orbit rig ships disabled:
            // enable it in the Hierarchy (and disable the player) for package-style fly-around
            // shots of the water.
            var cameraObject = new GameObject("Showcase Camera");
            cameraObject.transform.position = new Vector3(-34f, 16f, -34f);
            cameraObject.transform.LookAt(focus.transform.position);

            Camera camera = cameraObject.AddComponent<Camera>();
            ConfigureCamera(camera);
            cameraObject.AddComponent<AudioListener>();

            StylizedWaterShowcaseController controller =
                cameraObject.AddComponent<StylizedWaterShowcaseController>();
            controller.Configure(focus.transform, water, LoadWaterMaterials());
            EditorUtility.SetDirty(controller);
            cameraObject.SetActive(false);
        }

        /// <summary>
        /// Drops the shared first-person player on top of the island and gives its camera the
        /// render options the water needs (depth plus opaque textures, post processing).
        /// </summary>
        private static GameObject CreatePlayer(Terrain terrain)
        {
            var prefab = LoadRequiredAsset<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";

            var spawn = new Vector3(4f, 0f, 4f);
            spawn.y = terrain.transform.position.y + terrain.SampleHeight(spawn) + 1.2f;
            player.transform.position = spawn;
            player.transform.rotation = Quaternion.Euler(0f, 215f, 0f);

            foreach (Camera camera in player.GetComponentsInChildren<Camera>(true))
                ConfigureCamera(camera);
            EditorUtility.SetDirty(player);
            return player;
        }

        /// <summary>
        /// Adds the UI-mode service and the in-game water tuner (F7), both wired to the player, so
        /// the shader can be tuned with sliders while playing.
        /// </summary>
        private static void CreateTuningRig(Renderer water, GameObject player)
        {
            var controller = player.GetComponent<FirstPersonController>();
            var interaction = player.GetComponent<InteractionSystem>();

            var uiModeObject = new GameObject("UI Mode Service");
            UIModeService uiMode = uiModeObject.AddComponent<UIModeService>();
            var serialized = new SerializedObject(uiMode);
            serialized.FindProperty("playerController").objectReferenceValue = controller;
            serialized.FindProperty("interactionSystem").objectReferenceValue = interaction;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var tunerObject = new GameObject("Water Tuner");
            StylizedWaterRuntimeTuner tuner =
                tunerObject.AddComponent<StylizedWaterRuntimeTuner>();
            tuner.Configure(water, controller);
            EditorUtility.SetDirty(tuner);
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.41229087f, 0.6332952f, 0.8018868f, 0f);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = CameraFarClip;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.volumeLayerMask = 1;
            data.antialiasing = AntialiasingMode.None;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
        }

        // ---- Helpers ----------------------------------------------------------------------

        private static T LoadRequiredAsset<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException(
                    $"StylizedWaterIslandSceneBuilder: required asset is missing: {path}");
            return asset;
        }

        // GLSL-style edge smoothstep: 0 below e0, 1 above e1, smooth between.
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
            float cell = TerrainSize / n;
            float gx = (hR - hL) * TerrainHeight / (2f * cell);
            float gz = (hU - hD) * TerrainHeight / (2f * cell);
            return Mathf.Sqrt(gx * gx + gz * gz);
        }
    }
}
