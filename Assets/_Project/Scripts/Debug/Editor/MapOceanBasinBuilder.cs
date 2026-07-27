using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Carves a recoverable ocean basin into the Map terrain heightmap.
    /// </summary>
    public static class MapOceanBasinBuilder
    {
        private const string MapScenePath = "Assets/_Project/Scenes/Map.unity";
        private const string BackupFolder = "Assets/_Project/Art/Terrain/Backups";
        private const string BackupPath = BackupFolder + "/NewTerrain3_BeforeOceanBasin.asset";
        private const float BasinFloorDepth = 45f;
        private const float InnerRadiusRatio = 0.38f;
        private const float OuterRadiusRatio = 0.48f;

        /// <summary>
        /// Creates a backup and carves a broad, feathered basin below the Ocean surface.
        /// </summary>
        [MenuItem("Market/Debug/Carve Map Ocean Basin")]
        public static void CarveOceanBasin()
        {
            if (!TryResolveSceneObjects(out Terrain terrain, out Transform ocean))
                return;

            TerrainData targetData = terrain.terrainData;
            TerrainData sourceData = GetOrCreateBackup(targetData);
            if (sourceData == null)
                return;

            Undo.RegisterCompleteObjectUndo(targetData, "Carve Map Ocean Basin");
            float[,] sourceHeights = sourceData.GetHeights(0, 0, sourceData.heightmapResolution, sourceData.heightmapResolution);
            BasinSettings settings = CreateSettings(terrain, ocean, targetData);
            float[,] basinHeights = BuildBasinHeights(sourceHeights, settings);
            targetData.SetHeights(0, 0, basinHeights);
            EditorUtility.SetDirty(targetData);
            AssetDatabase.SaveAssets();

            LogResult(targetData, settings);
        }

        /// <summary>
        /// Logs the live Terrain heights around the Ocean and Camera without changing assets.
        /// </summary>
        [MenuItem("Market/Debug/Analyze Map Ocean Heights")]
        public static void AnalyzeOceanHeights()
        {
            if (!TryResolveSceneObjects(out Terrain terrain, out Transform ocean))
                return;

            Transform cameraTransform = FindRootTransform("Camera");
            float centerHeight = GetWorldHeight(terrain, ocean.position);
            float cameraHeight = cameraTransform != null
                ? GetWorldHeight(terrain, cameraTransform.position)
                : float.NaN;
            GetHeightRange(terrain, out float minimumHeight, out float maximumHeight);
            Debug.Log(
                $"[MapOceanBasinBuilder] Water Y={ocean.position.y:F1}, Terrain min Y={minimumHeight:F1}, " +
                $"max Y={maximumHeight:F1}, center Y={centerHeight:F1}, camera-ground Y={cameraHeight:F1}.");
        }

        private static bool TryResolveSceneObjects(out Terrain terrain, out Transform ocean)
        {
            terrain = null;
            ocean = null;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MapScenePath)
            {
                Debug.LogError($"[MapOceanBasinBuilder] Load {MapScenePath} before carving the basin.");
                return false;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (terrain == null && root.TryGetComponent(out Terrain candidate))
                    terrain = candidate;
                if (root.name == "Ocean")
                    ocean = root.transform;
            }

            if (terrain != null && terrain.terrainData != null && ocean != null)
                return true;

            Debug.LogError("[MapOceanBasinBuilder] Map requires one Terrain with TerrainData and an Ocean root object.");
            return false;
        }

        private static TerrainData GetOrCreateBackup(TerrainData targetData)
        {
            EnsureBackupFolders();
            TerrainData backup = AssetDatabase.LoadAssetAtPath<TerrainData>(BackupPath);
            if (backup != null)
                return backup;

            string sourcePath = AssetDatabase.GetAssetPath(targetData);
            if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, BackupPath))
            {
                Debug.LogError("[MapOceanBasinBuilder] Could not create the TerrainData backup.");
                return null;
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TerrainData>(BackupPath);
        }

        private static Transform FindRootTransform(string rootName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == rootName)
                    return root.transform;
            }

            return null;
        }

        private static float GetWorldHeight(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        }

        private static void GetHeightRange(Terrain terrain, out float minimum, out float maximum)
        {
            TerrainData data = terrain.terrainData;
            float[,] heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
            minimum = float.MaxValue;
            maximum = float.MinValue;
            foreach (float height in heights)
            {
                float worldHeight = terrain.transform.position.y + height * data.size.y;
                minimum = Mathf.Min(minimum, worldHeight);
                maximum = Mathf.Max(maximum, worldHeight);
            }
        }

        private static void EnsureBackupFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Terrain"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Terrain");
            if (!AssetDatabase.IsValidFolder(BackupFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Terrain", "Backups");
        }

        private static BasinSettings CreateSettings(Terrain terrain, Transform ocean, TerrainData data)
        {
            Vector3 terrainPosition = terrain.transform.position;
            float centerX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + data.size.x, ocean.position.x);
            float centerZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + data.size.z, ocean.position.z);
            float terrainSpan = Mathf.Min(data.size.x, data.size.z);
            float floorWorldY = ocean.position.y - BasinFloorDepth;
            float floorNormalized = Mathf.Clamp01((floorWorldY - terrainPosition.y) / data.size.y);
            return new BasinSettings(centerX, centerZ, floorNormalized, terrainSpan, data.size);
        }

        private static float[,] BuildBasinHeights(float[,] source, BasinSettings settings)
        {
            int height = source.GetLength(0);
            int width = source.GetLength(1);
            var result = new float[height, width];
            for (int z = 0; z < height; z++)
            {
                float normalizedZ = z / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = x / (float)(width - 1);
                    result[z, x] = CarveHeight(source[z, x], normalizedX, normalizedZ, settings);
                }
            }

            return result;
        }

        private static float CarveHeight(
            float sourceHeight,
            float normalizedX,
            float normalizedZ,
            BasinSettings settings)
        {
            float deltaX = (normalizedX - settings.CenterX) * settings.TerrainSize.x;
            float deltaZ = (normalizedZ - settings.CenterZ) * settings.TerrainSize.z;
            float distance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            float innerRadius = settings.TerrainSpan * InnerRadiusRatio;
            float outerRadius = settings.TerrainSpan * OuterRadiusRatio;
            float edgeProgress = Mathf.InverseLerp(innerRadius, outerRadius, distance);
            float basinWeight = 1f - Mathf.SmoothStep(0f, 1f, edgeProgress);
            float bottomRise = Mathf.Clamp01(distance / innerRadius) * 5f / settings.TerrainSize.y;
            float targetHeight = Mathf.Min(sourceHeight, settings.FloorNormalized + bottomRise);
            return Mathf.Lerp(sourceHeight, targetHeight, basinWeight);
        }

        private static void LogResult(TerrainData data, BasinSettings settings)
        {
            float floorWorldY = settings.FloorNormalized * data.size.y;
            Debug.Log(
                $"[MapOceanBasinBuilder] Basin carved. Floor local Y={floorWorldY:F1}, " +
                $"inner radius={settings.TerrainSpan * InnerRadiusRatio:F1}, " +
                $"outer radius={settings.TerrainSpan * OuterRadiusRatio:F1}. " +
                $"Original TerrainData backup: {BackupPath}");
        }

        private readonly struct BasinSettings
        {
            public BasinSettings(
                float centerX,
                float centerZ,
                float floorNormalized,
                float terrainSpan,
                Vector3 terrainSize)
            {
                CenterX = centerX;
                CenterZ = centerZ;
                FloorNormalized = floorNormalized;
                TerrainSpan = terrainSpan;
                TerrainSize = terrainSize;
            }

            public float CenterX { get; }
            public float CenterZ { get; }
            public float FloorNormalized { get; }
            public float TerrainSpan { get; }
            public Vector3 TerrainSize { get; }
        }
    }
}
