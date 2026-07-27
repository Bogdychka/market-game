using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds the initial textured terrain foundation for the dedicated Map scene.
    /// </summary>
    public static class MapTerrainSceneBuilder
    {
        private const string MapScenePath = "Assets/_Project/Scenes/Map.unity";
        private const string TerrainFolder = "Assets/_Project/Art/Terrain";
        private const string LayersFolder = TerrainFolder + "/Layers";
        private const string TerrainDataPath = TerrainFolder + "/MapTerrainData.asset";
        private const string GrassLayerPath = LayersFolder + "/Grass.asset";
        private const string DirtLayerPath = LayersFolder + "/Dirt.asset";
        private const string GrassTexturePath = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/Grass/Grass_normal/Grass_normal_up.png";
        private const string DirtTexturePath = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/Dirt/dirt_normal/dirt_normal_up.png";

        /// <summary>
        /// Creates or updates the Map terrain and its initial texture layers.
        /// </summary>
        [MenuItem("Market/Debug/Build Map Terrain")]
        public static void BuildMapTerrain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MapScenePath)
            {
                Debug.LogError($"[MapTerrainSceneBuilder] Load {MapScenePath} before building terrain.");
                return;
            }

            EnsureFolders();
            TerrainLayer grassLayer = GetOrCreateLayer(GrassLayerPath, GrassTexturePath);
            TerrainLayer dirtLayer = GetOrCreateLayer(DirtLayerPath, DirtTexturePath);
            TerrainData terrainData = GetOrCreateTerrainData(grassLayer, dirtLayer, out bool created);
            Terrain terrain = GetOrCreateTerrain(scene, terrainData);
            RemoveDefaultDuplicate(scene, terrain);

            ConfigureTerrain(terrain);
            if (created)
                FillWithGrass(terrainData);

            EditorUtility.SetDirty(terrainData);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = terrain.gameObject;
            Debug.Log("[MapTerrainSceneBuilder] Map terrain is ready: 256x256, height 30, grass and dirt layers.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(TerrainFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Terrain");
            if (!AssetDatabase.IsValidFolder(LayersFolder))
                AssetDatabase.CreateFolder(TerrainFolder, "Layers");
        }

        private static TerrainLayer GetOrCreateLayer(string assetPath, string texturePath)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, assetPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.tileSize = new Vector2(10f, 10f);
            layer.metallic = 0f;
            layer.smoothness = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static TerrainData GetOrCreateTerrainData(
            TerrainLayer grassLayer,
            TerrainLayer dirtLayer,
            out bool created)
        {
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            created = terrainData == null;
            if (created)
            {
                terrainData = new TerrainData();
                AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
            }

            terrainData.heightmapResolution = 513;
            terrainData.alphamapResolution = 512;
            terrainData.baseMapResolution = 512;
            terrainData.SetDetailResolution(512, 32);
            terrainData.size = new Vector3(256f, 30f, 256f);
            terrainData.terrainLayers = new[] { grassLayer, dirtLayer };
            return terrainData;
        }

        private static Terrain GetOrCreateTerrain(Scene scene, TerrainData terrainData)
        {
            Terrain fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.TryGetComponent(out Terrain existing))
                    continue;
                if (root.name == "WorldTerrain")
                    return AssignTerrainData(existing, terrainData);
                fallback ??= existing;
            }

            if (fallback != null)
                return AssignTerrainData(fallback, terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "WorldTerrain";
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            return terrainObject.GetComponent<Terrain>();
        }

        private static Terrain AssignTerrainData(Terrain terrain, TerrainData terrainData)
        {
            terrain.name = "WorldTerrain";
            terrain.terrainData = terrainData;
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
                terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
            terrainCollider.terrainData = terrainData;
            return terrain;
        }

        private static void RemoveDefaultDuplicate(Scene scene, Terrain keep)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != keep.gameObject && root.name == "Terrain" && root.GetComponent<Terrain>() != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            terrain.transform.position = new Vector3(-128f, 0f, -128f);
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 15f;
            terrain.basemapDistance = 250f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            terrain.enableHeightmapRayTracing = false;
            terrain.enableHeightmapLODFrustumCulling = false;
            terrain.allowAutoConnect = false;
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.GetComponent<TerrainCollider>());
        }

        private static void FillWithGrass(TerrainData terrainData)
        {
            int resolution = terrainData.alphamapResolution;
            var weights = new float[resolution, resolution, 2];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                    weights[z, x, 0] = 1f;
            }

            terrainData.SetAlphamaps(0, 0, weights);
        }
    }
}
