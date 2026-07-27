using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds the compact old-market valley landscape around the existing Market gameplay area.
    /// </summary>
    public static class MarketTerrainSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Market.unity";
        private const string GeneratedFolder = "Assets/_Project/Art/MarketLandscape";
        private const string LayerFolder = GeneratedFolder + "/Layers";
        private const string MaterialFolder = GeneratedFolder + "/Materials";
        private const string TerrainDataPath = GeneratedFolder + "/MarketTerrainData.asset";
        private const string NavMeshDataPath = "Assets/_Project/Scenes/Market/NavMesh-Ground.asset";
        private const string OwnedRootName = "MarketLandscapeLayout";
        private const string TerrainName = "MarketTerrain";
        private const int HeightResolution = 513;
        private const int AlphaResolution = 512;
        private const float TerrainHeight = 36f;

        private static readonly Vector3 TerrainPosition = new(-140f, -12f, -130f);
        private static readonly Vector3 TerrainSize = new(280f, TerrainHeight, 260f);
        private static Component _pendingNavMeshSurface;
        private static TerrainData _pendingTerrainData;
        private static Scene _pendingScene;
        private static object _navMeshAssetManager;
        private static MethodInfo _isSurfaceBakingMethod;

        private static readonly Zone[] Zones =
        {
            new("CentralMarket", new Vector2(0f, 0f), new Vector2(37f, 33f), 0f, -0.05f),
            new("GrandFair", new Vector2(8f, 34f), new Vector2(29f, 21f), -5f, 0.35f),
            new("TownEdge", new Vector2(-18f, -88f), new Vector2(47f, 25f), 7f, 0.35f),
            new("MainFarm", new Vector2(58f, 10f), new Vector2(47f, 32f), 8f, 0.35f),
            new("SecondFarm", new Vector2(80f, 72f), new Vector2(36f, 26f), -12f, 1.7f),
            new("Animals", new Vector2(66f, -66f), new Vector2(47f, 32f), -7f, 0.45f),
            new("FishingShore", new Vector2(-75f, 70f), new Vector2(31f, 19f), -7f, -2.05f),
            new("ShipyardFerry", new Vector2(-105f, 72f), new Vector2(25f, 16f), 4f, -2.35f),
            new("Crafting", new Vector2(-58f, -3f), new Vector2(35f, 25f), -8f, 0.55f),
            new("NorthExpansion", new Vector2(18f, 65f), new Vector2(32f, 23f), 6f, 0.9f),
            new("WestExpansion", new Vector2(-35f, 39f), new Vector2(24f, 17f), -15f, 0.5f)
        };

        private static readonly Route[] Routes =
        {
            new("TownToMarketA", new Vector2(-18f, -108f), new Vector2(-12f, -30f), 0.35f, -0.05f, 7f),
            new("TownToMarketB", new Vector2(-12f, -30f), Vector2.zero, -0.05f, -0.05f, 7f),
            new("MarketToFarm", Vector2.zero, new Vector2(58f, 10f), -0.05f, 0.35f, 7f),
            new("FarmToSecondA", new Vector2(58f, 10f), new Vector2(80f, 42f), 0.35f, 1.2f, 7f),
            new("FarmToSecondB", new Vector2(80f, 42f), new Vector2(80f, 72f), 1.2f, 1.7f, 7f),
            new("SecondToNorth", new Vector2(80f, 72f), new Vector2(18f, 65f), 1.7f, 0.9f, 7f),
            new("NorthToFishing", new Vector2(18f, 65f), new Vector2(-75f, 70f), 0.9f, -2.05f, 7f),
            new("FishingToCrafting", new Vector2(-75f, 70f), new Vector2(-58f, -3f), -2.05f, 0.55f, 7f),
            new("CraftingToMarket", new Vector2(-58f, -3f), Vector2.zero, 0.55f, -0.05f, 7f),
            new("MarketToAnimals", new Vector2(0f, -5f), new Vector2(66f, -66f), -0.05f, 0.45f, 7f),
            new("AnimalsToTown", new Vector2(66f, -66f), new Vector2(-18f, -108f), 0.45f, 0.35f, 7f),
            new("RaceLoopEastA", new Vector2(66f, -66f), new Vector2(112f, -58f), 0.45f, 1.6f, 6f),
            new("RaceLoopEastB", new Vector2(112f, -58f), new Vector2(116f, 5f), 1.6f, 3.7f, 6f),
            new("RaceLoopEastC", new Vector2(116f, 5f), new Vector2(105f, 52f), 3.7f, 2.6f, 6f),
            new("RaceLoopReturn", new Vector2(105f, 52f), new Vector2(80f, 72f), 2.6f, 1.7f, 6f),
            new("MarketToExpansion", Vector2.zero, new Vector2(18f, 65f), -0.05f, 0.9f, 4f),
            new("FishingToFerry", new Vector2(-75f, 70f), new Vector2(-110f, 76f), -2.05f, -2.35f, 4f)
        };

        private readonly struct Zone
        {
            public string Name { get; }
            public Vector2 Center { get; }
            public Vector2 Radius { get; }
            public float Rotation { get; }
            public float Height { get; }

            public Zone(string name, Vector2 center, Vector2 radius, float rotation, float height)
            {
                Name = name;
                Center = center;
                Radius = radius;
                Rotation = rotation;
                Height = height;
            }
        }

        private readonly struct Route
        {
            public string Name { get; }
            public Vector2 Start { get; }
            public Vector2 End { get; }
            public float StartHeight { get; }
            public float EndHeight { get; }
            public float Width { get; }

            public Route(
                string name,
                Vector2 start,
                Vector2 end,
                float startHeight,
                float endHeight,
                float width)
            {
                Name = name;
                Start = start;
                End = end;
                StartHeight = startHeight;
                EndHeight = endHeight;
                Width = width;
            }
        }

        /// <summary>
        /// Creates or updates the complete Market terrain foundation and its owned layout objects.
        /// </summary>
        [MenuItem("Market/Debug/Build Market Landscape")]
        public static void BuildMarketLandscape()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!ValidateScene(scene))
                return;

            EnsureFolders();
            TerrainLayer[] layers = GetOrCreateLayers();
            TerrainData terrainData = GetOrCreateTerrainData(layers);
            Terrain terrain = GetOrCreateTerrain(scene, terrainData);
            if (terrain == null)
                return;

            BuildTerrainData(terrainData);
            ConfigureTerrain(terrain, terrainData);
            ConfigureExistingMarketGround();
            RebuildLayout(scene, terrain);
            if (!StartNavMeshBake(scene, terrainData))
                Save(scene, terrainData);
            Debug.Log("[MarketTerrainSceneBuilder] Built a 280x260 old-market valley with eleven reserved zones and a connected route loop; NavMesh asset update queued.");
        }

        /// <summary>
        /// Captures five review views of the current Market landscape to the project Artifacts folder.
        /// </summary>
        [MenuItem("Market/Debug/Capture Market Landscape Views")]
        public static void CaptureLandscapeViews()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!ValidateScene(scene))
                return;

            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "Artifacts", "MarketLandscapeViews");
            try
            {
                Directory.CreateDirectory(folder);
                CaptureReviewViews(folder);
                Debug.Log($"[MarketTerrainSceneBuilder] Captured landscape review views in {folder}.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MarketTerrainSceneBuilder] Failed to capture landscape views: {exception.Message}");
            }
        }

        /// <summary>
        /// Reports compact terrain, route, zone, spawn, vegetation, and NavMesh validation metrics.
        /// </summary>
        [MenuItem("Market/Debug/Validate Market Landscape")]
        public static void ValidateLandscape()
        {
            Scene scene = SceneManager.GetActiveScene();
            Terrain terrain = FindTerrain(scene, TerrainName);
            if (!ValidateScene(scene) || terrain == null)
            {
                Debug.LogError("[MarketTerrainSceneBuilder] Market terrain is not available for validation.");
                return;
            }

            string zoneMetrics = BuildZoneMetrics(terrain);
            string routeMetrics = BuildRouteMetrics(terrain);
            string sceneMetrics = BuildSceneMetrics(terrain);
            Debug.Log($"[MarketTerrainSceneBuilder] VALIDATION | terrain=280x260x36 origin=(-140,-12,-130) | {zoneMetrics} | {routeMetrics} | {sceneMetrics}");
        }

        private static bool ValidateScene(Scene scene)
        {
            if (scene.path == ScenePath)
                return true;

            Debug.LogError($"[MarketTerrainSceneBuilder] Load {ScenePath} before building or capturing the landscape.");
            return false;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Art", "MarketLandscape");
            EnsureFolder(GeneratedFolder, "Layers");
            EnsureFolder(GeneratedFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static TerrainLayer[] GetOrCreateLayers()
        {
            return new[]
            {
                GetOrCreateLayer("Grass", "Grass/Grass_normal/Grass_normal_up.png", 13f),
                GetOrCreateLayer("WornGround", "Dirt/dirt_lighted_rocks/dirt_lighted_rocks_up.png", 9f),
                GetOrCreateLayer("DirtPath", "Dirt/dirt_normal/dirt_normal_up.png", 8f),
                GetOrCreateLayer("CultivatedSoil", "Dirt/dirt_claydarked/dirt_claydarked_up.png", 7f),
                GetOrCreateLayer("OldSoil", "Dirt/dirt_clay/dirt_clay_up.png", 7f),
                GetOrCreateLayer("MoistSoil", "Grass/Grass_swamp_dark/Grass_swamp_dark_up.png", 10f),
                GetOrCreateLayer("Rock", "Dirt/dirt_desatured_rocks/dirt_desatured_rocks_up.png", 11f),
                GetOrCreateLayer("WorkshopGround", "Dirt/dirt_lighted/dirt_lighted_up.png", 8f)
            };
        }

        private static TerrainLayer GetOrCreateLayer(string name, string relativeTexturePath, float tileSize)
        {
            string assetPath = $"{LayerFolder}/{name}.asset";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = name };
                AssetDatabase.CreateAsset(layer, assetPath);
            }

            const string textureRoot = "Assets/Handpainted_Grass_and_Ground_Textures/Textures";
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureRoot}/{relativeTexturePath}");
            layer.tileSize = Vector2.one * tileSize;
            layer.metallic = 0f;
            layer.smoothness = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static TerrainData GetOrCreateTerrainData(TerrainLayer[] layers)
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData { name = "MarketTerrainData" };
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightResolution;
            data.alphamapResolution = AlphaResolution;
            data.baseMapResolution = 1024;
            data.SetDetailResolution(512, 32);
            data.size = TerrainSize;
            data.terrainLayers = layers;
            return data;
        }

        private static Terrain GetOrCreateTerrain(Scene scene, TerrainData data)
        {
            Terrain owned = FindTerrain(scene, TerrainName);
            if (owned != null)
                return AssignTerrainData(owned, data);

            Terrain existing = FindTerrain(scene, null);
            if (existing != null)
            {
                Debug.LogError($"[MarketTerrainSceneBuilder] Existing terrain '{existing.name}' is not owned by this builder; refusing to create a conflicting terrain.");
                return null;
            }

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = TerrainName;
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            return terrainObject.GetComponent<Terrain>();
        }

        private static Terrain FindTerrain(Scene scene, string requiredName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.TryGetComponent(out Terrain terrain))
                    continue;
                if (requiredName == null || root.name == requiredName)
                    return terrain;
            }

            return null;
        }

        private static Terrain AssignTerrainData(Terrain terrain, TerrainData data)
        {
            terrain.terrainData = data;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null)
                collider = terrain.gameObject.AddComponent<TerrainCollider>();
            collider.terrainData = data;
            return terrain;
        }

        private static void BuildTerrainData(TerrainData data)
        {
            float[,] heights = BuildHeights();
            data.SetHeights(0, 0, heights);
            float[,,] weights = BuildAlphamaps(data);
            data.SetAlphamaps(0, 0, weights);
            data.treeInstances = Array.Empty<TreeInstance>();
            data.treePrototypes = Array.Empty<TreePrototype>();
            data.detailPrototypes = Array.Empty<DetailPrototype>();
        }

        private static float[,] BuildHeights()
        {
            var heights = new float[HeightResolution, HeightResolution];
            for (int z = 0; z < HeightResolution; z++)
            {
                float worldZ = TerrainPosition.z + z / (HeightResolution - 1f) * TerrainSize.z;
                for (int x = 0; x < HeightResolution; x++)
                {
                    float worldX = TerrainPosition.x + x / (HeightResolution - 1f) * TerrainSize.x;
                    float elevation = EvaluateElevation(new Vector2(worldX, worldZ));
                    heights[z, x] = Mathf.Clamp01((elevation - TerrainPosition.y) / TerrainHeight);
                }
            }

            return heights;
        }

        private static float EvaluateElevation(Vector2 point)
        {
            float elevation = EvaluateLargeForms(point);
            foreach (Route route in Routes)
                elevation = ApplyRouteHeight(elevation, point, route);
            foreach (Zone zone in Zones)
                elevation = Mathf.Lerp(elevation, zone.Height, ZoneWeight(point, zone, 0.72f, 1.18f));
            return elevation;
        }

        private static float EvaluateLargeForms(Vector2 point)
        {
            float elevation = -0.05f + Mathf.Sin(point.x * 0.031f) * Mathf.Cos(point.y * 0.027f) * 0.28f;
            elevation += Gaussian(point, new Vector2(118f, 82f), 8.5f, 64f, 55f);
            elevation += Gaussian(point, new Vector2(116f, -82f), 6.2f, 58f, 64f);
            elevation += Gaussian(point, new Vector2(-118f, -55f), 5.4f, 48f, 68f);
            elevation += Gaussian(point, new Vector2(-25f, 124f), 4.6f, 92f, 28f);
            elevation -= Gaussian(point, new Vector2(-91f, 103f), 5.8f, 57f, 31f);
            elevation += Mathf.SmoothStep(0f, 5.5f, Mathf.InverseLerp(96f, 140f, point.x));
            elevation += Mathf.SmoothStep(0f, 2.2f, Mathf.InverseLerp(112f, 140f, -point.y));
            return elevation;
        }

        private static float Gaussian(Vector2 point, Vector2 center, float height, float radiusX, float radiusZ)
        {
            float x = (point.x - center.x) / radiusX;
            float z = (point.y - center.y) / radiusZ;
            return height * Mathf.Exp(-(x * x + z * z));
        }

        private static float ZoneWeight(Vector2 point, Zone zone, float inner, float outer)
        {
            Vector2 local = Rotate(point - zone.Center, -zone.Rotation * Mathf.Deg2Rad);
            float radius = Mathf.Sqrt(
                local.x * local.x / (zone.Radius.x * zone.Radius.x) +
                local.y * local.y / (zone.Radius.y * zone.Radius.y));
            return FalloffWeight(inner, outer, radius);
        }

        private static float FalloffWeight(float inner, float outer, float value)
        {
            float progress = Mathf.InverseLerp(inner, outer, value);
            return 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        private static Vector2 Rotate(Vector2 value, float radians)
        {
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);
        }

        private static float ApplyRouteHeight(float elevation, Vector2 point, Route route)
        {
            float distance = DistanceToSegment(point, route.Start, route.End, out float progress);
            float halfWidth = route.Width * 0.5f;
            float weight = FalloffWeight(halfWidth, halfWidth + 3f, distance);
            float routeHeight = Mathf.Lerp(route.StartHeight, route.EndHeight, progress);
            return Mathf.Lerp(elevation, routeHeight, weight);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, out float progress)
        {
            Vector2 line = end - start;
            float lengthSquared = line.sqrMagnitude;
            progress = lengthSquared > 0.001f ? Mathf.Clamp01(Vector2.Dot(point - start, line) / lengthSquared) : 0f;
            return Vector2.Distance(point, start + line * progress);
        }

        private static float[,,] BuildAlphamaps(TerrainData data)
        {
            int layerCount = data.terrainLayers.Length;
            var weights = new float[AlphaResolution, AlphaResolution, layerCount];
            for (int z = 0; z < AlphaResolution; z++)
            {
                float nz = z / (AlphaResolution - 1f);
                float worldZ = TerrainPosition.z + nz * TerrainSize.z;
                for (int x = 0; x < AlphaResolution; x++)
                    PaintSample(data, weights, x, z, x / (AlphaResolution - 1f), nz, worldZ);
            }

            return weights;
        }

        private static void PaintSample(
            TerrainData data,
            float[,,] weights,
            int x,
            int z,
            float nx,
            float nz,
            float worldZ)
        {
            Vector2 point = new(TerrainPosition.x + nx * TerrainSize.x, worldZ);
            var sample = new float[8];
            sample[0] = 1f;
            sample[1] += ZoneWeight(point, Zones[0], 0.55f, 1.22f) * 4f;
            sample[1] += ZoneWeight(point, Zones[1], 0.62f, 1.12f) * 1.8f;
            sample[3] += ZoneWeight(point, Zones[3], 0.55f, 1.08f) * 5f;
            sample[4] += ZoneWeight(point, Zones[4], 0.52f, 1.08f) * 5f;
            sample[5] += FishingSoilWeight(point) * 4.5f;
            sample[7] += ZoneWeight(point, Zones[8], 0.55f, 1.08f) * 4f;
            sample[2] += RouteTextureWeight(point) * 6f;
            sample[6] += RockWeight(data.GetSteepness(nx, nz), point) * 7f;
            NormalizeSample(weights, sample, x, z);
        }

        private static float FishingSoilWeight(Vector2 point)
        {
            float shore = Mathf.Max(
                ZoneWeight(point, Zones[6], 0.45f, 1.35f),
                ZoneWeight(point, Zones[7], 0.45f, 1.35f));
            float basin = Gaussian(point, new Vector2(-91f, 103f), 1f, 62f, 36f);
            return Mathf.Clamp01(Mathf.Max(shore, basin));
        }

        private static float RouteTextureWeight(Vector2 point)
        {
            float weight = 0f;
            foreach (Route route in Routes)
            {
                float distance = DistanceToSegment(point, route.Start, route.End, out _);
                weight = Mathf.Max(weight, FalloffWeight(route.Width * 0.45f, route.Width * 0.75f + 1f, distance));
            }

            return weight;
        }

        private static float RockWeight(float slope, Vector2 point)
        {
            float slopeWeight = Mathf.InverseLerp(22f, 38f, slope);
            float eastRidge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(105f, 136f, point.x));
            float southWest = Gaussian(point, new Vector2(-125f, -55f), 1f, 28f, 58f);
            return Mathf.Clamp01(Mathf.Max(slopeWeight, Mathf.Max(eastRidge * 0.75f, southWest * 0.55f)));
        }

        private static void NormalizeSample(float[,,] target, float[] sample, int x, int z)
        {
            float total = 0f;
            foreach (float value in sample)
                total += value;
            float inverse = total > 0.001f ? 1f / total : 1f;
            for (int layer = 0; layer < sample.Length; layer++)
                target[z, x, layer] = sample[layer] * inverse;
        }

        private static void ConfigureTerrain(Terrain terrain, TerrainData data)
        {
            terrain.name = TerrainName;
            terrain.transform.position = TerrainPosition;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 700f;
            terrain.detailObjectDistance = 90f;
            terrain.treeDistance = 550f;
            terrain.allowAutoConnect = false;
            AssignTerrainData(terrain, data);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.GetComponent<TerrainCollider>());
        }

        private static void ConfigureExistingMarketGround()
        {
            GameObject ground = GameObject.Find("Ground");
            MeshRenderer renderer = ground != null ? ground.GetComponent<MeshRenderer>() : null;
            if (renderer == null)
                return;

            string texturePath = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/Dirt/dirt_lighted_rocks/dirt_lighted_rocks_up.png";
            Material material = GetOrCreateGroundMaterial(texturePath);
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        private static Material GetOrCreateGroundMaterial(string texturePath)
        {
            string assetPath = $"{MaterialFolder}/MarketGround.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MarketGround" };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_BaseMap", new Vector2(5.5f, 5.5f));
            material.SetTextureScale("_MainTex", new Vector2(5.5f, 5.5f));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RebuildLayout(Scene scene, Terrain terrain)
        {
            GameObject existing = GameObject.Find(OwnedRootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject root = new(OwnedRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            CreateZoneMarkers(root.transform, terrain);
            CreateRouteMarkers(root.transform, terrain);
            CreateBoundaryVegetation(root.transform, terrain);
        }

        private static void CreateZoneMarkers(Transform parent, Terrain terrain)
        {
            GameObject group = new("ReservedZones");
            group.transform.SetParent(parent, false);
            foreach (Zone zone in Zones)
            {
                GameObject marker = new($"Zone_{zone.Name}");
                marker.transform.SetParent(group.transform, false);
                marker.transform.position = SurfacePosition(terrain, zone.Center);
                marker.transform.rotation = Quaternion.Euler(0f, zone.Rotation, 0f);
                marker.transform.localScale = new Vector3(zone.Radius.x * 2f, 1f, zone.Radius.y * 2f);
            }

            AddZoneMarker(group.transform, terrain, "HorseStables", new Vector2(94f, -70f), new Vector2(30f, 20f));
            AddZoneMarker(group.transform, terrain, "RaceEvent", new Vector2(112f, -5f), new Vector2(22f, 58f));
        }

        private static void AddZoneMarker(
            Transform parent,
            Terrain terrain,
            string name,
            Vector2 center,
            Vector2 size)
        {
            GameObject marker = new($"Zone_{name}");
            marker.transform.SetParent(parent, false);
            marker.transform.position = SurfacePosition(terrain, center);
            marker.transform.localScale = new Vector3(size.x, 1f, size.y);
        }

        private static void CreateRouteMarkers(Transform parent, Terrain terrain)
        {
            GameObject group = new("ReservedRoutes");
            group.transform.SetParent(parent, false);
            foreach (Route route in Routes)
            {
                GameObject marker = new($"Route_{route.Name}_{route.Width:0.#}m");
                marker.transform.SetParent(group.transform, false);
                marker.transform.position = SurfacePosition(terrain, (route.Start + route.End) * 0.5f);
            }
        }

        private static void CreateBoundaryVegetation(Transform parent, Terrain terrain)
        {
            GameObject group = new("BoundaryVegetation");
            group.transform.SetParent(parent, false);
            var random = new System.Random(7419);
            CreateTreeClusters(group.transform, terrain, random);
            CreateBushClusters(group.transform, terrain, random);
            CreateBoundaryProps(group.transform, terrain);
        }

        private static void CreateTreeClusters(Transform parent, Terrain terrain, System.Random random)
        {
            string[] lowPolyTrees =
            {
                "Assets/Low-Poly Medieval Market/Prefabs/Environment/tree_01.prefab",
                "Assets/Low-Poly Medieval Market/Prefabs/Environment/tree_02.prefab",
                "Assets/Low-Poly Medieval Market/Prefabs/Environment/tree_03.prefab"
            };
            string[] stylizedTrees =
            {
                "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/FBX/Tree_1.fbx",
                "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/FBX/Birch_3.fbx",
                "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/FBX/Pine_2.fbx"
            };

            Vector2[] centers =
            {
                new(120f, 85f), new(121f, -53f), new(-118f, -57f),
                new(-82f, -111f), new(25f, 116f), new(-126f, 8f)
            };
            for (int index = 0; index < centers.Length; index++)
            {
                string[] assets = index % 2 == 0 ? stylizedTrees : lowPolyTrees;
                PlaceCluster(parent, terrain, random, centers[index], 24f, 10, assets, 7f, 12f, "BoundaryTree");
            }
        }

        private static void CreateBushClusters(Transform parent, Terrain terrain, System.Random random)
        {
            string[] bushes =
            {
                "Assets/Low-Poly Medieval Market/Prefabs/Environment/bush_01.prefab",
                "Assets/Low-Poly Medieval Market/Prefabs/Environment/bush_02.prefab"
            };
            PlaceCluster(parent, terrain, random, new Vector2(-104f, -42f), 25f, 12, bushes, 1.2f, 2.2f, "BoundaryBush");
            PlaceCluster(parent, terrain, random, new Vector2(101f, 92f), 24f, 12, bushes, 1.2f, 2.2f, "BoundaryBush");
        }

        private static void PlaceCluster(
            Transform parent,
            Terrain terrain,
            System.Random random,
            Vector2 center,
            float radius,
            int count,
            string[] assetPaths,
            float minHeight,
            float maxHeight,
            string namePrefix)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 8 && placed < count; attempt++)
            {
                Vector2 point = RandomPoint(random, center, radius);
                if (!CanPlaceVegetation(point))
                    continue;
                string assetPath = assetPaths[random.Next(assetPaths.Length)];
                float targetHeight = Mathf.Lerp(minHeight, maxHeight, (float)random.NextDouble());
                if (PlaceAsset(parent, terrain, point, assetPath, targetHeight, random, $"{namePrefix}_{placed:00}"))
                    placed++;
            }
        }

        private static Vector2 RandomPoint(System.Random random, Vector2 center, float radius)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private static bool CanPlaceVegetation(Vector2 point)
        {
            if (Mathf.Abs(point.x) > 136f || Mathf.Abs(point.y) > 126f)
                return false;
            foreach (Zone zone in Zones)
            {
                Vector2 paddedRadius = zone.Radius + Vector2.one * 8f;
                Zone padded = new(zone.Name, zone.Center, paddedRadius, zone.Rotation, zone.Height);
                if (ZoneWeight(point, padded, 0.72f, 1.02f) > 0.02f)
                    return false;
            }

            foreach (Route route in Routes)
            {
                float distance = DistanceToSegment(point, route.Start, route.End, out _);
                if (distance < route.Width * 0.5f + 5f)
                    return false;
            }

            return true;
        }

        private static bool PlaceAsset(
            Transform parent,
            Terrain terrain,
            Vector2 point,
            string assetPath,
            float targetHeight,
            System.Random random,
            string instanceName)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject instance = asset != null ? PrefabUtility.InstantiatePrefab(asset, parent) as GameObject : null;
            if (instance == null)
                return false;

            instance.name = instanceName;
            Quaternion importedRotation = instance.transform.localRotation;
            instance.transform.position = new Vector3(point.x, 0f, point.y);
            instance.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * importedRotation;
            ApplyStylizedTreeMaterials(instance, assetPath);
            ScaleAndGround(instance, terrain, point, targetHeight);
            return true;
        }

        private static void ApplyStylizedTreeMaterials(GameObject instance, string assetPath)
        {
            if (!assetPath.Contains("Textured Stylized Trees", StringComparison.OrdinalIgnoreCase))
                return;

            string lower = assetPath.ToLowerInvariant();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = GetTreeMaterial(lower, index == 0);
                renderer.sharedMaterials = materials;
            }
        }

        private static Material GetTreeMaterial(string assetPath, bool bark)
        {
            const string textureRoot = "Assets/Textured Stylized Trees - May 2020/Textured Stylized Trees - May 2020/Textures";
            if (assetPath.Contains("birch"))
                return GetOrCreateTreeMaterial(bark ? "BirchBark" : "BirchLeaves", $"{textureRoot}/{(bark ? "Birch_Bark.png" : "Birch_Leaves_Green.png")}", !bark);
            if (assetPath.Contains("pine"))
                return GetOrCreateTreeMaterial(bark ? "TreeBark" : "PineLeaves", $"{textureRoot}/{(bark ? "Tree_Bark.jpg" : "Pine_Leaves.png")}", !bark);
            return GetOrCreateTreeMaterial(bark ? "TreeBark" : "TreeLeaves", $"{textureRoot}/{(bark ? "Tree_Bark.jpg" : "Tree_Leaves.png")}", !bark);
        }

        private static Material GetOrCreateTreeMaterial(string name, string texturePath, bool alphaClip)
        {
            string assetPath = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.4f);
            material.SetFloat("_Cull", alphaClip ? 0f : 2f);
            if (alphaClip)
                material.EnableKeyword("_ALPHATEST_ON");
            else
                material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ScaleAndGround(GameObject instance, Terrain terrain, Vector2 point, float targetHeight)
        {
            Bounds bounds = CalculateBounds(instance);
            float factor = bounds.size.y > 0.01f ? targetHeight / bounds.size.y : 1f;
            instance.transform.localScale *= Mathf.Clamp(factor, 0.05f, 12f);
            bounds = CalculateBounds(instance);
            float surface = SurfaceHeight(terrain, point);
            instance.transform.position += Vector3.up * (surface - bounds.min.y);
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

        private static void CreateBoundaryProps(Transform parent, Terrain terrain)
        {
            const string stoneFence = "Assets/Low-Poly Medieval Market/Prefabs/Environment/stone_fence_01.prefab";
            Vector2[] points = { new(-126f, 32f), new(-127f, 18f), new(125f, 38f), new(126f, 20f) };
            for (int index = 0; index < points.Length; index++)
            {
                var random = new System.Random(920 + index);
                PlaceAsset(parent, terrain, points[index], stoneFence, 1.8f, random, $"BoundaryStone_{index:00}");
            }
        }

        private static Vector3 SurfacePosition(Terrain terrain, Vector2 point)
        {
            return new Vector3(point.x, SurfaceHeight(terrain, point), point.y);
        }

        private static float SurfaceHeight(Terrain terrain, Vector2 point)
        {
            return terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.transform.position.y;
        }

        private static bool StartNavMeshBake(Scene scene, TerrainData terrainData)
        {
            GameObject ground = GameObject.Find("Ground");
            Component surface = ground != null ? ground.GetComponent("NavMeshSurface") : null;
            Type managerType = Type.GetType("Unity.AI.Navigation.Editor.NavMeshAssetManager, Unity.AI.Navigation.Editor");
            MethodInfo startMethod = managerType?.GetMethod("StartBakingSurfaces", BindingFlags.Instance | BindingFlags.Public);
            _isSurfaceBakingMethod = managerType?.GetMethod("IsSurfaceBaking", BindingFlags.Instance | BindingFlags.Public);
            _navMeshAssetManager = managerType?.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (surface == null || startMethod == null || _isSurfaceBakingMethod == null || _navMeshAssetManager == null)
            {
                Debug.LogWarning("[MarketTerrainSceneBuilder] NavMeshSurface was not available; terrain was built without a NavMesh bake.");
                return false;
            }

            RestorePersistentNavMeshData(surface);
            _pendingNavMeshSurface = surface;
            _pendingTerrainData = terrainData;
            _pendingScene = scene;
            startMethod.Invoke(_navMeshAssetManager, new object[] { new UnityEngine.Object[] { surface } });
            EditorApplication.update -= CompleteNavMeshBake;
            EditorApplication.update += CompleteNavMeshBake;
            return true;
        }

        private static void RestorePersistentNavMeshData(Component surface)
        {
            var serializedSurface = new SerializedObject(surface);
            SerializedProperty dataProperty = serializedSurface.FindProperty("m_NavMeshData");
            UnityEngine.Object currentData = dataProperty?.objectReferenceValue;
            if (currentData == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(currentData)))
                return;

            UnityEngine.Object persistentData = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NavMeshDataPath);
            surface.GetType().GetMethod("RemoveData", BindingFlags.Instance | BindingFlags.Public)?.Invoke(surface, null);
            dataProperty.objectReferenceValue = persistentData;
            serializedSurface.ApplyModifiedPropertiesWithoutUndo();
            surface.GetType().GetMethod("AddData", BindingFlags.Instance | BindingFlags.Public)?.Invoke(surface, null);
            UnityEngine.Object.DestroyImmediate(currentData);
        }

        private static void CompleteNavMeshBake()
        {
            if (_pendingNavMeshSurface == null || _navMeshAssetManager == null)
            {
                ClearPendingBake();
                return;
            }

            bool isBaking = (bool)_isSurfaceBakingMethod.Invoke(
                _navMeshAssetManager,
                new object[] { _pendingNavMeshSurface });
            if (isBaking)
                return;

            Save(_pendingScene, _pendingTerrainData);
            Debug.Log("[MarketTerrainSceneBuilder] NavMesh asset update completed and the Market scene was saved.");
            ClearPendingBake();
        }

        private static void ClearPendingBake()
        {
            EditorApplication.update -= CompleteNavMeshBake;
            _pendingNavMeshSurface = null;
            _pendingTerrainData = null;
            _navMeshAssetManager = null;
            _isSurfaceBakingMethod = null;
        }

        private static void Save(Scene scene, TerrainData terrainData)
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorUtility.SetDirty(terrainData);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.ForceReserializeAssets(
                new[] { ScenePath },
                ForceReserializeAssetsOptions.ReserializeAssets);
            Selection.activeGameObject = GameObject.Find(TerrainName);
        }

        private static string BuildZoneMetrics(Terrain terrain)
        {
            float maximumVariation = 0f;
            float maximumSlope = 0f;
            string leastFlatZone = string.Empty;
            foreach (Zone zone in Zones)
            {
                float center = SurfaceHeight(terrain, zone.Center);
                Vector2 offsetX = new(zone.Radius.x * 0.45f, 0f);
                Vector2 offsetZ = new(0f, zone.Radius.y * 0.45f);
                float variation = ZoneVariation(terrain, zone.Center, center, offsetX, offsetZ);
                if (variation > maximumVariation)
                {
                    maximumVariation = variation;
                    leastFlatZone = zone.Name;
                }
                maximumSlope = Mathf.Max(maximumSlope, SampleSlope(terrain, zone.Center));
            }

            return $"zones={Zones.Length + 2} maxZoneVariation={maximumVariation:0.00}m({leastFlatZone}) maxCenterSlope={maximumSlope:0.0}deg";
        }

        private static float ZoneVariation(
            Terrain terrain,
            Vector2 center,
            float centerHeight,
            Vector2 offsetX,
            Vector2 offsetZ)
        {
            float variation = Mathf.Abs(SurfaceHeight(terrain, center + offsetX) - centerHeight);
            variation = Mathf.Max(variation, Mathf.Abs(SurfaceHeight(terrain, center - offsetX) - centerHeight));
            variation = Mathf.Max(variation, Mathf.Abs(SurfaceHeight(terrain, center + offsetZ) - centerHeight));
            return Mathf.Max(variation, Mathf.Abs(SurfaceHeight(terrain, center - offsetZ) - centerHeight));
        }

        private static float SampleSlope(Terrain terrain, Vector2 point)
        {
            float nx = Mathf.InverseLerp(TerrainPosition.x, TerrainPosition.x + TerrainSize.x, point.x);
            float nz = Mathf.InverseLerp(TerrainPosition.z, TerrainPosition.z + TerrainSize.z, point.y);
            return terrain.terrainData.GetSteepness(nx, nz);
        }

        private static string BuildRouteMetrics(Terrain terrain)
        {
            float maximumGrade = 0f;
            foreach (Route route in Routes)
            {
                float distance = Vector2.Distance(route.Start, route.End);
                float grade = distance > 0.01f ? Mathf.Abs(route.EndHeight - route.StartHeight) / distance * 100f : 0f;
                maximumGrade = Mathf.Max(maximumGrade, grade);
            }

            string startLayer = DominantLayerAt(terrain, new Vector2(-18f, -100f));
            string farmLayer = DominantLayerAt(terrain, Zones[3].Center);
            float townRouteWeight = RouteTextureWeight(new Vector2(-18f, -100f));
            return $"routes={Routes.Length} widths=4-7m maxPlannedGrade={maximumGrade:0.0}% townRouteWeight={townRouteWeight:0.00} townLayer={startLayer} farmLayer={farmLayer}";
        }

        private static string DominantLayerAt(Terrain terrain, Vector2 point)
        {
            int x = Mathf.RoundToInt(Mathf.InverseLerp(TerrainPosition.x, TerrainPosition.x + TerrainSize.x, point.x) * (AlphaResolution - 1));
            int z = Mathf.RoundToInt(Mathf.InverseLerp(TerrainPosition.z, TerrainPosition.z + TerrainSize.z, point.y) * (AlphaResolution - 1));
            float[,,] values = terrain.terrainData.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int index = 1; index < values.GetLength(2); index++)
            {
                if (values[0, 0, index] > values[0, 0, best])
                    best = index;
            }

            return terrain.terrainData.terrainLayers[best].name;
        }

        private static string BuildSceneMetrics(Terrain terrain)
        {
            Transform vegetation = GameObject.Find($"{OwnedRootName}/BoundaryVegetation")?.transform;
            int vegetationCount = vegetation != null ? vegetation.childCount : 0;
            float spawnDelta = HeightDelta("SpawnPoint", terrain);
            float exitDelta = HeightDelta("ExitPoint", terrain);
            GameObject ground = GameObject.Find("Ground");
            Component surface = ground != null ? ground.GetComponent("NavMeshSurface") : null;
            return $"vegetation={vegetationCount} spawnDelta={spawnDelta:0.00}m exitDelta={exitDelta:0.00}m navMesh={(surface != null ? "present" : "missing")}";
        }

        private static float HeightDelta(string objectName, Terrain terrain)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
                return float.PositiveInfinity;
            Vector2 point = new(target.transform.position.x, target.transform.position.z);
            float surface = Mathf.Abs(point.x) <= 25f && Mathf.Abs(point.y) <= 25f ? 0f : SurfaceHeight(terrain, point);
            return target.transform.position.y - surface;
        }

        private static void CaptureReviewViews(string folder)
        {
            CaptureView(folder, "01_Map_Top", new Vector3(0f, 230f, 0f), Vector3.zero, true, 145f);
            CaptureView(folder, "02_Town_To_Market", new Vector3(-18f, 4.5f, -102f), new Vector3(0f, 1f, 0f));
            CaptureView(folder, "03_Market_To_Farm", new Vector3(8f, 5f, -5f), new Vector3(58f, 0.5f, 10f));
            CaptureView(folder, "04_Fishing_Shore", new Vector3(-73f, 5f, 50f), new Vector3(-92f, -3f, 104f));
            CaptureView(folder, "05_Animals_Reserve", new Vector3(28f, 5f, -44f), new Vector3(68f, 0f, -66f));
        }

        private static void CaptureView(
            string folder,
            string fileName,
            Vector3 position,
            Vector3 target,
            bool orthographic = false,
            float orthographicSize = 20f)
        {
            GameObject cameraObject = new("LandscapeReviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 58f;
            camera.farClipPlane = 900f;
            camera.orthographic = orthographic;
            camera.orthographicSize = orthographicSize;
            RenderCamera(camera, Path.Combine(folder, fileName + ".png"));
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        private static void RenderCamera(Camera camera, string outputPath)
        {
            const int width = 1600;
            const int height = 900;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
