using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Bakes a top-down map of how deep the water is at every world XZ, so both the vertex and the
    /// fragment stage of <c>RealisticWater.shader</c> can reason about the shore without the camera
    /// depth texture (which only knows what is currently on screen, and is unavailable in a vertex
    /// shader).
    ///
    /// Baking is done with downward raycasts rather than a depth render: it needs no render-pass
    /// plumbing, it reads the actual colliders that objects are standing on, and it runs once in
    /// the Editor where speed does not matter. Same world-rect convention as the foam history
    /// (<c>(worldXZ - rect.xy) * rect.zw</c>).
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public static class ShoreDepthBaker
    {
        /// <summary>
        /// Re-bake after the seabed moves. The map is geometry, not a look setting: a stale bake
        /// silently puts the shoreline in the wrong place rather than failing loudly.
        /// </summary>
        private const string TextureFolder = "Assets/_Project/Art/Textures/Water";
        private const string TextureNamePrefix = "T_ShoreDepth_";

        private const int Resolution = 512;

        /// <summary>Deepest value the map stores. Anything deeper is clamped and reads as open water.</summary>
        private const float MaximumDepth = 60f;

        /// <summary>Raycasts start this far above the surface so props standing in the water are hit from above.</summary>
        private const float RayHeight = 200f;

        /// <summary>The bake covers the water bounds plus this margin, so the shore band never runs off the map.</summary>
        private const float Margin = 8f;

        private static readonly int ShoreDepthTextureId = Shader.PropertyToID("_ShoreDepthTexture");
        private static readonly int ShoreDepthWorldRectId = Shader.PropertyToID("_ShoreDepthWorldRect");
        private static readonly int ShoreDepthAvailableId = Shader.PropertyToID("_ShoreDepthAvailable");
        private static readonly int ShoreDepthTexelSizeId = Shader.PropertyToID("_ShoreDepthTexelWorldSize");
        private static readonly int ShoreDepthMaximumId = Shader.PropertyToID("_ShoreDepthMaximum");

        /// <summary>
        /// Bakes the shore map for every water renderer in the open scene and wires it into their
        /// materials. Re-run after moving the seabed or adding props that sit in the water.
        /// </summary>
        [MenuItem("Market/Debug/Water/Bake Shore Depth Map")]
        public static void Bake()
        {
            try
            {
                Renderer water = FindWaterRenderer();
                Material material = water.sharedMaterial;
                if (material == null)
                    throw new InvalidOperationException(
                        $"'{water.name}' has no material to write the shore map into.");

                float waterY = water.transform.position.y;
                Bounds bounds = water.bounds;
                Vector2 min = new Vector2(bounds.min.x - Margin, bounds.min.z - Margin);
                Vector2 size = new Vector2(
                    bounds.size.x + Margin * 2f, bounds.size.z + Margin * 2f);

                Texture2D map = BakeDepthTexture(water, waterY, min, size);
                string texturePath = GetTexturePath();
                Texture2D saved = SaveTexture(map, texturePath);

                material.SetTexture(ShoreDepthTextureId, saved);
                material.SetVector(
                    ShoreDepthWorldRectId,
                    new Vector4(min.x, min.y, 1f / size.x, 1f / size.y));
                material.SetVector(
                    ShoreDepthTexelSizeId,
                    new Vector4(size.x / Resolution, size.y / Resolution, 0f, 0f));
                material.SetFloat(ShoreDepthMaximumId, MaximumDepth);
                material.SetFloat(ShoreDepthAvailableId, 1f);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[ShoreDepthBaker] Baked {Resolution}x{Resolution} shore map for '{water.name}' " +
                    $"over {size.x:0.#}x{size.y:0.#} world units " +
                    $"({size.x / Resolution:0.##} m/texel) at water level {waterY:0.##} -> {texturePath}.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ShoreDepthBaker] Bake failed: {exception.Message}");
                throw;
            }
        }

        private static Texture2D BakeDepthTexture(
            Renderer water, float waterY, Vector2 min, Vector2 size)
        {
            // R = water column depth, G = signed horizontal distance to the waterline.
            // Positive G is water and negative G is dry land. The sign lets shore consumers
            // recover both inland distance and the local direction into the water.
            // The distance is baked rather than derived at runtime from the depth gradient:
            // depth/slope is only valid on a monotonic slope, and on a terraced seabed every
            // vertical riser has an near-infinite slope, which puts a false shoreline on each step.
            var map = new Texture2D(Resolution, Resolution, TextureFormat.RGFloat, false, true)
            {
                name = "T_ShoreDepth",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Collider waterCollider = water.GetComponent<Collider>();
            bool waterColliderWasEnabled = waterCollider != null && waterCollider.enabled;
            if (waterColliderWasEnabled)
                waterCollider.enabled = false;

            var pixels = new Color[Resolution * Resolution];
            try
            {
                for (int y = 0; y < Resolution; y++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Baking shore depth",
                            $"Row {y + 1} / {Resolution}",
                            (y + 1) / (float)Resolution))
                        throw new OperationCanceledException("Cancelled by the user.");

                    for (int x = 0; x < Resolution; x++)
                    {
                        // Sample texel centres so the map lines up with bilinear filtering.
                        float worldX = min.x + (x + 0.5f) / Resolution * size.x;
                        float worldZ = min.y + (y + 0.5f) / Resolution * size.y;
                        pixels[y * Resolution + x] =
                            new Color(SampleDepth(worldX, worldZ, waterY), 0f, 0f, 1f);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (waterColliderWasEnabled)
                    waterCollider.enabled = true;
            }

            WriteSignedShoreDistance(
                pixels, size.x / Resolution, size.y / Resolution);
            map.SetPixels(pixels);
            map.Apply(false, false);
            return map;
        }

        /// <summary>
        /// Fills the green channel with signed horizontal distance in metres. Water is positive
        /// distance to dry land and land is negative distance to water. The existing water shader
        /// only samples the positive half, while wet-sand receivers use the negative half and its
        /// gradient to follow curved shorelines.
        /// </summary>
        private static void WriteSignedShoreDistance(
            Color[] pixels, float texelX, float texelZ)
        {
            float[] distanceToDry = BuildDistanceField(
                pixels, false, texelX, texelZ);
            float[] distanceToWater = BuildDistanceField(
                pixels, true, texelX, texelZ);
            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];
                bool isWater = pixel.r > 0.0001f;
                float distance = isWater
                    ? distanceToDry[index]
                    : -distanceToWater[index];
                pixel.g = Mathf.Clamp(
                    distance, -MaximumDepth, MaximumDepth);
                pixels[index] = pixel;
            }
        }

        private static float[] BuildDistanceField(
            Color[] pixels, bool seedWater, float texelX, float texelZ)
        {
            const float Far = 1e9f;
            float diagonalStep = Mathf.Sqrt(texelX * texelX + texelZ * texelZ);
            var distance = new float[pixels.Length];
            for (int index = 0; index < pixels.Length; index++)
            {
                bool isWater = pixels[index].r > 0.0001f;
                distance[index] = isWater == seedWater ? 0f : Far;
            }

            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int index = y * Resolution + x;
                    float best = distance[index];
                    if (x > 0)
                        best = Mathf.Min(best, distance[index - 1] + texelX);
                    if (y > 0)
                        best = Mathf.Min(best, distance[index - Resolution] + texelZ);
                    if (x > 0 && y > 0)
                        best = Mathf.Min(best, distance[index - Resolution - 1] + diagonalStep);
                    if (x < Resolution - 1 && y > 0)
                        best = Mathf.Min(best, distance[index - Resolution + 1] + diagonalStep);
                    distance[index] = best;
                }
            }

            for (int y = Resolution - 1; y >= 0; y--)
            {
                for (int x = Resolution - 1; x >= 0; x--)
                {
                    int index = y * Resolution + x;
                    float best = distance[index];
                    if (x < Resolution - 1)
                        best = Mathf.Min(best, distance[index + 1] + texelX);
                    if (y < Resolution - 1)
                        best = Mathf.Min(best, distance[index + Resolution] + texelZ);
                    if (x < Resolution - 1 && y < Resolution - 1)
                        best = Mathf.Min(best, distance[index + Resolution + 1] + diagonalStep);
                    if (x > 0 && y < Resolution - 1)
                        best = Mathf.Min(best, distance[index + Resolution - 1] + diagonalStep);
                    distance[index] = best;
                }
            }

            return distance;
        }

        /// <summary>
        /// Depth of the water column above whatever solid surface is at this world XZ. Nothing hit
        /// means open water, which reads as the maximum depth rather than as dry land.
        /// </summary>
        private static float SampleDepth(float worldX, float worldZ, float waterY)
        {
            var origin = new Vector3(worldX, waterY + RayHeight, worldZ);
            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    RayHeight + MaximumDepth,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return MaximumDepth;

            return Mathf.Clamp(waterY - hit.point.y, 0f, MaximumDepth);
        }

        private static Texture2D SaveTexture(Texture2D map, string texturePath)
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder))
                Directory.CreateDirectory(TextureFolder);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(map, texturePath);
                return map;
            }

            // Overwrite in place so every material already pointing at this asset keeps working.
            existing.Reinitialize(map.width, map.height, map.format, false);
            existing.SetPixels(map.GetPixels());
            existing.Apply(false, false);
            existing.wrapMode = TextureWrapMode.Clamp;
            existing.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(map);
            return existing;
        }

        private static string GetTexturePath()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            var safeName = new StringBuilder(sceneName.Length);
            foreach (char character in sceneName)
            {
                safeName.Append(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '-'
                    ? character
                    : '_');
            }

            return $"{TextureFolder}/{TextureNamePrefix}{safeName}.asset";
        }

        /// <summary>
        /// Exact name, not a prefix match: the caustic projector and the underwater surface are
        /// also called "RealisticWater*" and a substring match picks the projector instead.
        /// </summary>
        private const string WaterShaderName = "Market/World/RealisticWater";

        private static Renderer FindWaterRenderer()
        {
            Renderer best = null;
            foreach (Renderer renderer in
                     UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null)
                    continue;
                if (material.shader.name != WaterShaderName)
                    continue;
                if (best == null || renderer.bounds.size.x > best.bounds.size.x)
                    best = renderer;
            }

            if (best == null)
                throw new InvalidOperationException(
                    $"No renderer in the open scene uses the '{WaterShaderName}' shader.");
            return best;
        }
    }
}
