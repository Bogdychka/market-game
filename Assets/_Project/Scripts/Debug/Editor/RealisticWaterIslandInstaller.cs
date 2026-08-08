using System;
using System.Collections.Generic;
using System.IO;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Promotes the reference-matched realistic water stack to the generated Island terrain.
    /// </summary>
    public static class RealisticWaterIslandInstaller
    {
        private const string ScenePath = "Assets/_Project/Scenes/Island.unity";
        private const string StylizedWaterName = "Island Stylized Water";
        private const string RealisticWaterName = "Island Realistic Water";
        private const string TerrainName = "Island_Terrain";
        private const string ShoreOverlayName = "Island Wet Sand Shore";
        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/Water";
        private const string LabWaterMaterialPath =
            MaterialFolder + "/M_RealisticWaterLab.mat";
        private const string IslandWaterMaterialPath =
            MaterialFolder + "/M_RealisticWaterIsland.mat";
        private const string IslandWetSandMaterialPath =
            MaterialFolder + "/M_RealisticWetSandIsland.mat";
        private const string IslandWaterMeshPath =
            "Assets/_Project/Art/Meshes/Water/IslandRealisticWaterGrid.asset";
        private const string IslandShoreMeshPath =
            "Assets/_Project/Art/Meshes/Water/IslandWetSandShore.asset";
        private const string TemporalFoamComputePath =
            "Assets/_Project/Art/Shaders/RealisticWaterFoamUpdate.compute";
        private const string OceanWaveProfilePath =
            "Assets/_Project/Art/Materials/Water/Profiles/WP_OceanSwell.asset";
        private const string WetSandShaderName = "Market/World/RealisticWetSand";
        private const int WaterGridResolution = 193;
        private const int ShoreGridResolution = 513;
        private const float WaterSize = 950f;
        private const float HistoryMargin = 8f;
        private const float ShoreBelowWater = 0.25f;
        private const float ShoreAboveWater = 0.9f;
        private const float ShoreSurfaceOffset = 0.035f;

        private readonly struct ShoreVertex
        {
            public ShoreVertex(Vector3 position, Vector3 normal)
            {
                Position = position;
                Normal = normal;
            }

            public Vector3 Position { get; }
            public Vector3 Normal { get; }
        }

        /// <summary>
        /// Installs realistic surface, temporal foam, signed shore map, and wet-sand overlay.
        /// </summary>
        [MenuItem("Market/Debug/Water/Install Realistic Water on Island")]
        public static void Install()
        {
            try
            {
                Scene scene = RequireIslandScene();
                Terrain terrain = RequireTerrain(scene);
                IslandSceneBuilder.RemoveBrokenLegacyTerrainData();
                GameObject water = RequireWater(scene);
                ConfigureWaterObject(water);
                Material waterMaterial = EnsureIslandWaterMaterial();
                Material wetSandMaterial = EnsureIslandWetSandMaterial(
                    water.transform.position.y);
                ConfigureWaterRenderer(water, waterMaterial);
                RealisticWaterTemporalFoam foam = ConfigureWaterStack(
                    water, terrain);
                Renderer shore = BuildShoreOverlay(
                    scene, terrain, water.transform.position.y, wetSandMaterial);
                ConfigureWetSandBinding(water, foam, shore);
                ShoreDepthBaker.Bake();
                Save(scene);
                Debug.Log(
                    "[RealisticWaterIslandInstaller] Installed realistic Island water, " +
                    "curved shore field, focused foam history, and wet-sand overlay.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[RealisticWaterIslandInstaller] Install failed: {exception.Message}");
                throw;
            }
        }

        private static Scene RequireIslandScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException(
                    $"Open '{ScenePath}' before installing Island water.");
            }

            return scene;
        }

        private static Terrain RequireTerrain(Scene scene)
        {
            GameObject terrainObject = FindRoot(scene, TerrainName);
            Terrain terrain = terrainObject != null
                ? terrainObject.GetComponent<Terrain>()
                : null;
            if (terrain == null)
            {
                throw new InvalidOperationException(
                    $"The scene needs a '{TerrainName}' Terrain.");
            }

            if (terrain.terrainData == null)
                IslandSceneBuilder.RestoreTerrainData(terrain);
            if (terrain.terrainData == null)
                throw new InvalidOperationException("Island TerrainData could not be restored.");

            return terrain;
        }

        private static GameObject RequireWater(Scene scene)
        {
            GameObject water = FindRoot(scene, RealisticWaterName) ??
                FindRoot(scene, StylizedWaterName);
            if (water == null)
            {
                throw new InvalidOperationException(
                    "The Island water root is missing.");
            }

            return water;
        }

        private static void ConfigureWaterObject(GameObject water)
        {
            water.name = RealisticWaterName;
            WaterMaterialSwitcher switcher =
                water.GetComponent<WaterMaterialSwitcher>();
            if (switcher != null)
                UnityEngine.Object.DestroyImmediate(switcher);

            Mesh mesh = RealisticWaterMeshGenerator.EnsureGridMesh(
                IslandWaterMeshPath,
                WaterGridResolution,
                WaterSize,
                "IslandRealisticWaterGrid");
            water.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private static Material EnsureIslandWaterMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                IslandWaterMaterialPath);
            if (material == null && !AssetDatabase.CopyAsset(
                    LabWaterMaterialPath, IslandWaterMaterialPath))
            {
                throw new IOException(
                    $"Could not copy '{LabWaterMaterialPath}'.");
            }

            if (material == null)
            {
                AssetDatabase.ImportAsset(IslandWaterMaterialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(
                    IslandWaterMaterialPath);
            }
            if (material == null)
                throw new IOException("The Island water material copy did not import.");
            material.name = "M_RealisticWaterIsland";
            material.SetFloat("_DepthFadeDistance", 6f);
            material.SetColor(
                "_ScatteringColor", new Color(0.04f, 0.28f, 0.42f, 1f));
            material.SetFloat("_ScatteringStrength", 0.9f);
            material.SetFloat("_Roughness", 0.16f);
            material.SetFloat("_PlanarReflectionStrength", 0.75f);
            material.SetFloat("_ShoreBandWidth", 6f);
            material.SetFloat("_FoamShoreStrength", 1.5f);
            material.SetFloat("_FoamResidualStrength", 0.85f);
            material.SetFloat("_FoamBreakup", 0.68f);
            material.SetFloat("_ShoreLineWidth", 0.55f);
            material.SetFloat("_ShoreLineStrength", 0.8f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureIslandWetSandMaterial(float waterLevel)
        {
            Shader shader = Shader.Find(WetSandShaderName);
            if (shader == null)
                throw new InvalidOperationException(
                    $"Shader '{WetSandShaderName}' is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                IslandWetSandMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "M_RealisticWetSandIsland",
                };
                AssetDatabase.CreateAsset(material, IslandWetSandMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            ConfigureWetSandMaterial(material, waterLevel);
            return material;
        }

        private static void ConfigureWetSandMaterial(
            Material material, float waterLevel)
        {
            material.SetColor("_DryColor", new Color(0.95f, 0.84f, 0.56f, 1f));
            material.SetColor("_WetColor", new Color(0.35f, 0.25f, 0.14f, 1f));
            material.SetColor("_SwashColor", new Color(0.8f, 0.84f, 0.78f, 1f));
            material.SetFloat("_WaterLevel", waterLevel);
            material.SetFloat("_RunupHeight", ShoreAboveWater);
            material.SetFloat("_RunupDistance", 7.5f);
            material.SetFloat("_RetreatWidth", 1.35f);
            material.SetFloat("_HistoryProbeOffset", 2.5f);
            material.SetFloat("_EventGain", 2.2f);
            material.SetFloat("_BreakupScale", 0.14f);
            material.SetFloat("_BreakupStrength", 0.35f);
            material.SetFloat("_UseShoreDistanceField", 1f);
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureWaterRenderer(
            GameObject water, Material material)
        {
            MeshRenderer renderer = water.GetComponent<MeshRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("Island water needs a MeshRenderer.");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static RealisticWaterTemporalFoam ConfigureWaterStack(
            GameObject water, Terrain terrain)
        {
            RealisticWaterTemporalFoam foam =
                GetOrAdd<RealisticWaterTemporalFoam>(water);
            ConfigureTemporalFoam(foam, terrain);
            RealisticWaterPlanarReflection reflection =
                GetOrAdd<RealisticWaterPlanarReflection>(water);
            reflection.SetQuality(WaterPlanarReflectionQuality.SkyOnly);
            foam.SetQuality(WaterFoamHistoryQuality.History256);
            ConfigureWaveProfile(GetOrAdd<WaveProfileBinder>(water));
            RealisticWaterQualityController quality =
                water.GetComponent<RealisticWaterQualityController>();
            if (quality != null)
                UnityEngine.Object.DestroyImmediate(quality);
            return foam;
        }

        private static void ConfigureTemporalFoam(
            RealisticWaterTemporalFoam foam, Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            var coverage = new Vector4(
                origin.x - HistoryMargin,
                origin.z - HistoryMargin,
                size.x + HistoryMargin * 2f,
                size.z + HistoryMargin * 2f);
            var serialized = new SerializedObject(foam);
            serialized.FindProperty("foamUpdateCompute").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ComputeShader>(TemporalFoamComputePath);
            serialized.FindProperty("quality").enumValueIndex =
                (int)WaterFoamHistoryQuality.History256;
            serialized.FindProperty("useCustomWorldRect").boolValue = true;
            serialized.FindProperty("customWorldRect").vector4Value = coverage;
            serialized.FindProperty("shorelineWidth").floatValue = 6f;
            serialized.FindProperty("freshFoamDecayRate").floatValue = 1.2f;
            serialized.FindProperty("residualFoamDecayRate").floatValue = 0.22f;
            serialized.FindProperty("shorelineInjectionStrength").floatValue = 1.35f;
            serialized.FindProperty("residualTransfer").floatValue = 0.55f;
            serialized.FindProperty("residualAdvectionScale").floatValue = -0.15f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWaveProfile(WaveProfileBinder binder)
        {
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("_profile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WaveProfile>(OceanWaveProfilePath);
            serialized.FindProperty("_uploadEveryFrame").boolValue = true;
            serialized.FindProperty("_useTransformAsWaterLevel").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            binder.UploadProfile();
        }

        private static Renderer BuildShoreOverlay(
            Scene scene,
            Terrain terrain,
            float waterLevel,
            Material material)
        {
            DestroyRoot(scene, ShoreOverlayName);
            Mesh generated = BuildShoreMesh(terrain, waterLevel);
            Mesh mesh = SaveMesh(generated, IslandShoreMeshPath);
            var shore = new GameObject(ShoreOverlayName);
            shore.transform.SetPositionAndRotation(
                terrain.transform.position, terrain.transform.rotation);
            shore.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = shore.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static Mesh BuildShoreMesh(Terrain terrain, float waterLevel)
        {
            TerrainData data = terrain.terrainData;
            int row = ShoreGridResolution;
            var sourceVertices = new Vector3[row * row];
            var sourceNormals = new Vector3[sourceVertices.Length];
            FillShoreVertices(data, sourceVertices, sourceNormals, row);
            BuildClippedShoreGeometry(
                sourceVertices,
                sourceNormals,
                row,
                waterLevel - terrain.transform.position.y,
                out List<Vector3> vertices,
                out List<Vector3> normals,
                out List<int> triangles);
            var mesh = new Mesh
            {
                name = "IslandWetSandShore",
                indexFormat = IndexFormat.UInt32,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildClippedShoreGeometry(
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            int row,
            float localWaterLevel,
            out List<Vector3> vertices,
            out List<Vector3> normals,
            out List<int> triangles)
        {
            vertices = new List<Vector3>(row * 16);
            normals = new List<Vector3>(row * 16);
            triangles = new List<int>(row * 24);
            var bufferA = new ShoreVertex[8];
            var bufferB = new ShoreVertex[8];
            float low = localWaterLevel - ShoreBelowWater;
            float high = localWaterLevel + ShoreAboveWater;
            for (int z = 0; z < row - 1; z++)
            {
                for (int x = 0; x < row - 1; x++)
                {
                    int bottomLeft = z * row + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + row;
                    int topRight = topLeft + 1;
                    AddClippedTriangle(
                        bottomLeft, topLeft, bottomRight, sourceVertices,
                        sourceNormals, low, high, bufferA, bufferB,
                        vertices, normals, triangles);
                    AddClippedTriangle(
                        bottomRight, topLeft, topRight, sourceVertices,
                        sourceNormals, low, high, bufferA, bufferB,
                        vertices, normals, triangles);
                }
            }
        }

        private static void AddClippedTriangle(
            int a,
            int b,
            int c,
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            float low,
            float high,
            ShoreVertex[] bufferA,
            ShoreVertex[] bufferB,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            bufferA[0] = new ShoreVertex(sourceVertices[a], sourceNormals[a]);
            bufferA[1] = new ShoreVertex(sourceVertices[b], sourceNormals[b]);
            bufferA[2] = new ShoreVertex(sourceVertices[c], sourceNormals[c]);
            int count = ClipAtHeight(bufferA, 3, bufferB, low, true);
            if (count < 3)
                return;
            count = ClipAtHeight(bufferB, count, bufferA, high, false);
            if (count < 3)
                return;

            AddClippedPolygon(bufferA, count, vertices, normals, triangles);
        }

        private static int ClipAtHeight(
            ShoreVertex[] input,
            int count,
            ShoreVertex[] output,
            float height,
            bool keepAbove)
        {
            int outputCount = 0;
            ShoreVertex previous = input[count - 1];
            bool previousInside = IsInside(previous.Position.y, height, keepAbove);
            for (int i = 0; i < count; i++)
            {
                ShoreVertex current = input[i];
                bool currentInside = IsInside(
                    current.Position.y, height, keepAbove);
                if (currentInside != previousInside)
                {
                    output[outputCount++] = IntersectAtHeight(
                        previous, current, height);
                }
                if (currentInside)
                    output[outputCount++] = current;
                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static bool IsInside(
            float value, float boundary, bool keepAbove)
        {
            return keepAbove ? value >= boundary : value <= boundary;
        }

        private static ShoreVertex IntersectAtHeight(
            ShoreVertex from, ShoreVertex to, float height)
        {
            float denominator = to.Position.y - from.Position.y;
            float amount = Mathf.Abs(denominator) > 0.000001f
                ? (height - from.Position.y) / denominator
                : 0f;
            return new ShoreVertex(
                Vector3.LerpUnclamped(from.Position, to.Position, amount),
                Vector3.LerpUnclamped(from.Normal, to.Normal, amount).normalized);
        }

        private static void AddClippedPolygon(
            ShoreVertex[] polygon,
            int count,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            int first = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                vertices.Add(polygon[i].Position);
                normals.Add(polygon[i].Normal);
            }
            for (int i = 1; i < count - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + i);
                triangles.Add(first + i + 1);
            }
        }

        private static void FillShoreVertices(
            TerrainData data,
            Vector3[] vertices,
            Vector3[] normals,
            int row)
        {
            Vector3 size = data.size;
            for (int z = 0; z < row; z++)
            {
                float v = z / (float)(row - 1);
                for (int x = 0; x < row; x++)
                {
                    float u = x / (float)(row - 1);
                    int index = z * row + x;
                    vertices[index] = new Vector3(
                        u * size.x,
                        data.GetInterpolatedHeight(u, v) + ShoreSurfaceOffset,
                        v * size.z);
                    normals[index] = data.GetInterpolatedNormal(u, v);
                }
            }
        }

        private static Mesh SaveMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void ConfigureWetSandBinding(
            GameObject water,
            RealisticWaterTemporalFoam foam,
            Renderer shore)
        {
            RealisticWaterWetSand binding =
                GetOrAdd<RealisticWaterWetSand>(water);
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("foamSource").objectReferenceValue = foam;
            serialized.FindProperty("waterRenderer").objectReferenceValue =
                water.GetComponent<Renderer>();
            SerializedProperty targets =
                serialized.FindProperty("targetRenderers");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = shore;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }
    }
}
