using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Generates deterministic seamless normal maps for the R4 water detail layers.
    /// </summary>
    public static class RealisticWaterNormalTextureGenerator
    {
        private const int TextureSize = 256;
        private const string TextureDirectory = "Assets/_Project/Art/Textures/Water";
        private const string NormalMapAPath =
            TextureDirectory + "/T_RealisticWater_NormalA.png";
        private const string NormalMapBPath =
            TextureDirectory + "/T_RealisticWater_NormalB.png";
        private const string MaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterLab.mat";

        /// <summary>
        /// Generates both normal maps, configures their import settings, and assigns them.
        /// </summary>
        [MenuItem("Market/Debug/Water/Generate R4 Normal Maps")]
        public static void Generate()
        {
            try
            {
                Directory.CreateDirectory(TextureDirectory);
                WriteNormalMap(NormalMapAPath, 1847, 0.025f);
                WriteNormalMap(NormalMapBPath, 7919, 0.018f);
                AssetDatabase.Refresh();
                ConfigureImporter(NormalMapAPath);
                ConfigureImporter(NormalMapBPath);
                AssignToMaterial();
                Debug.Log("[RealisticWaterNormalTextureGenerator] Generated and assigned R4 maps.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[RealisticWaterNormalTextureGenerator] Generation failed: {exception.Message}");
            }
        }

        private static void WriteNormalMap(string assetPath, int seed, float slopeScale)
        {
            float[] heights = GenerateHeightField(seed);
            Color32[] pixels = GenerateNormalPixels(heights, slopeScale);
            Texture2D texture = new(
                TextureSize, TextureSize, TextureFormat.RGBA32, false, true);

            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float[] GenerateHeightField(int seed)
        {
            float[] heights = new float[TextureSize * TextureSize];
            int[] periods = { 4, 8, 16, 32, 64 };
            float[] weights = { 0.42f, 0.27f, 0.16f, 0.10f, 0.05f };

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize;
                    float v = (y + 0.5f) / TextureSize;
                    heights[y * TextureSize + x] =
                        AccumulateNoise(u, v, seed, periods, weights);
                }
            }

            return heights;
        }

        private static float AccumulateNoise(
            float u, float v, int seed, int[] periods, float[] weights)
        {
            float height = 0f;
            for (int octave = 0; octave < periods.Length; octave++)
            {
                float noise = PeriodicValueNoise(
                    u, v, periods[octave], seed + octave * 1013);
                height += noise * weights[octave];
            }

            return height;
        }

        private static float PeriodicValueNoise(
            float u, float v, int period, int seed)
        {
            float px = u * period;
            float py = v * period;
            int floorX = Mathf.FloorToInt(px);
            int floorY = Mathf.FloorToInt(py);
            int x0 = floorX % period;
            int y0 = floorY % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;
            float tx = Smooth(px - floorX);
            float ty = Smooth(py - floorY);
            float bottom = Mathf.Lerp(Hash01(x0, y0, seed), Hash01(x1, y0, seed), tx);
            float top = Mathf.Lerp(Hash01(x0, y1, seed), Hash01(x1, y1, seed), tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                return (hash ^ (hash >> 16)) / (float)uint.MaxValue;
            }
        }

        private static Color32[] GenerateNormalPixels(float[] heights, float slopeScale)
        {
            Color32[] pixels = new Color32[heights.Length];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] =
                        BuildNormalPixel(heights, x, y, slopeScale);
                }
            }

            return pixels;
        }

        private static Color32 BuildNormalPixel(
            float[] heights, int x, int y, float slopeScale)
        {
            float dx = SampleHeight(heights, x + 1, y) -
                SampleHeight(heights, x - 1, y);
            float dy = SampleHeight(heights, x, y + 1) -
                SampleHeight(heights, x, y - 1);
            Vector3 normal = new(
                -dx * TextureSize * slopeScale,
                -dy * TextureSize * slopeScale,
                1f);
            normal.Normalize();
            return new Color(
                normal.x * 0.5f + 0.5f,
                normal.y * 0.5f + 0.5f,
                normal.z * 0.5f + 0.5f,
                1f);
        }

        private static float SampleHeight(float[] heights, int x, int y)
        {
            int wrappedX = (x + TextureSize) % TextureSize;
            int wrappedY = (y + TextureSize) % TextureSize;
            return heights[wrappedY * TextureSize + wrappedX];
        }

        private static void ConfigureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"No texture importer for '{assetPath}'.");
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
        }

        private static void AssignToMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                throw new InvalidOperationException($"Material not found at '{MaterialPath}'.");
            }

            material.SetTexture(
                "_NormalMapA", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapAPath));
            material.SetTexture(
                "_NormalMapB", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapBPath));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}
