using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Generates the dense, evenly-spaced grid mesh the realistic water shader needs for
    /// vertex-displaced Gerstner waves. A flat primitive Plane (10x10 verts) has nowhere near
    /// enough resolution for wave silhouettes; this re-runnable tool bakes a proper grid asset
    /// instead. Experimental water track only (see WaterShaderLabSceneBuilder) - MarketWater.shader
    /// / M_Ocean are untouched.
    /// </summary>
    public static class RealisticWaterMeshGenerator
    {
        private const string MeshPath = "Assets/_Project/Art/Meshes/Water/RealisticWaterGrid.asset";
        private const int Resolution = 200;
        private const float Size = 100f;

        [MenuItem("Market/Debug/Water/Generate Realistic Water Mesh")]
        public static void GenerateMesh()
        {
            EnsureFolders();
            Mesh mesh = BuildGridMesh(Resolution, Size);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                AssetDatabase.SaveAssets();
                LogResult(existing);
                return;
            }

            AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();
            LogResult(mesh);
        }

        private static Mesh BuildGridMesh(int resolution, float size)
        {
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            float half = size * 0.5f;
            float step = size / (resolution - 1);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * resolution + x;
                    vertices[i] = new Vector3(x * step - half, 0f, z * step - half);
                    uvs[i] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
                    normals[i] = Vector3.up;
                }
            }

            int quadsPerSide = resolution - 1;
            var triangles = new int[quadsPerSide * quadsPerSide * 6];
            int t = 0;
            for (int z = 0; z < quadsPerSide; z++)
            {
                for (int x = 0; x < quadsPerSide; x++)
                {
                    int i = z * resolution + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + resolution + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "RealisticWaterGrid",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Meshes");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Meshes/Water"))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Meshes", "Water");
        }

        private static void LogResult(Mesh mesh)
        {
            Debug.Log(
                $"[RealisticWaterMeshGenerator] {MeshPath}: {Resolution}x{Resolution} verts " +
                $"({mesh.vertexCount} total, {mesh.triangles.Length / 3} tris), {Size}x{Size} world units.");
        }
    }
}
