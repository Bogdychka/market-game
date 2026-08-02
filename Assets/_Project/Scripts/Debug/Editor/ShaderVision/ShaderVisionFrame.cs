using System;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One captured frame: the display-referred pixels plus the raw float readback used for
    /// measurements. Shader bugs are usually invisible in a thumbnail but obvious in the numbers
    /// (NaN pixels, magenta error shader, everything clipped to white), so we keep both.
    /// </summary>
    public sealed class ShaderVisionFrame
    {
        public readonly string Label;
        public readonly int Width;
        public readonly int Height;
        /// <summary>Row-major from the BOTTOM row up, matching Texture2D pixel order.</summary>
        public readonly Color32[] Pixels;

        private readonly Color[] _raw;

        public ShaderVisionFrame(string label, int width, int height, Color32[] pixels, Color[] raw)
        {
            Label = label;
            Width = width;
            Height = height;
            Pixels = pixels;
            _raw = raw;
        }

        /// <summary>
        /// Renders <paramref name="camera"/> into an off-screen HDR target and reads it back twice:
        /// RGB24 for what a human would see, RGBAFloat for what the shader actually wrote.
        /// No MSAA - the PC renderer is Deferred, where it does nothing (AGENTS.md).
        /// </summary>
        public static ShaderVisionFrame Capture(Camera camera, string label, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var display = new Texture2D(width, height, TextureFormat.RGB24, false);
            var raw = new Texture2D(width, height, TextureFormat.RGBAFloat, false);

            try
            {
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var full = new Rect(0f, 0f, width, height);
                display.ReadPixels(full, 0, 0);
                display.Apply(false);
                raw.ReadPixels(full, 0, 0);
                raw.Apply(false);

                return new ShaderVisionFrame(label, width, height, display.GetPixels32(), raw.GetPixels());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(display);
                UnityEngine.Object.DestroyImmediate(raw);
            }
        }

        /// <summary>Measures the frame. Percentiles come from a 256-bin luminance histogram.</summary>
        public ShaderVisionShotReport Analyze()
        {
            var report = new ShaderVisionShotReport { label = Label };

            var histogram = new int[256];
            double sum = 0d;
            double sumSquares = 0d;
            double rSum = 0d;
            double gSum = 0d;
            double bSum = 0d;
            float min = float.MaxValue;
            float max = float.MinValue;
            int black = 0;
            int clipped = 0;
            int nonFinite = 0;
            int magenta = 0;
            int finiteCount = 0;

            for (int i = 0; i < _raw.Length; i++)
            {
                Color c = _raw[i];
                if (!IsFinite(c.r) || !IsFinite(c.g) || !IsFinite(c.b))
                {
                    nonFinite++;
                    continue;
                }

                finiteCount++;
                float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += luminance;
                sumSquares += (double)luminance * luminance;
                rSum += c.r;
                gSum += c.g;
                bSum += c.b;

                if (luminance < min) min = luminance;
                if (luminance > max) max = luminance;
                if (luminance < 0.02f) black++;
                if (c.r > 0.99f && c.g > 0.99f && c.b > 0.99f) clipped++;
                // Unity's error/missing shader draws bright magenta - a dead giveaway in a capture.
                if (c.r > 0.85f && c.b > 0.85f && c.g < 0.25f) magenta++;

                histogram[Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(luminance) * 255f), 0, 255)]++;
            }

            int total = Mathf.Max(1, _raw.Length);
            int finite = Mathf.Max(1, finiteCount);
            double mean = sum / finite;

            report.luminanceMean = (float)mean;
            report.luminanceMin = finiteCount > 0 ? min : 0f;
            report.luminanceMax = finiteCount > 0 ? max : 0f;
            report.luminanceStdDev = (float)Math.Sqrt(Math.Max(0d, sumSquares / finite - mean * mean));
            report.luminanceP05 = Percentile(histogram, finiteCount, 0.05f);
            report.luminanceP50 = Percentile(histogram, finiteCount, 0.50f);
            report.luminanceP95 = Percentile(histogram, finiteCount, 0.95f);
            report.rgbMean = new[] { (float)(rSum / finite), (float)(gSum / finite), (float)(bSum / finite) };
            report.blackPct = 100f * black / total;
            report.clippedPct = 100f * clipped / total;
            report.nonFinitePct = 100f * nonFinite / total;
            report.magentaPct = 100f * magenta / total;
            report.detail = MeasureDetail();
            return report;
        }

        /// <summary>
        /// Mean absolute luminance step between horizontal neighbours - a blunt but reliable
        /// "is there still surface detail" number for normal maps, foam and micro-waves.
        /// </summary>
        private float MeasureDetail()
        {
            double sum = 0d;
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 1; x < Width; x++)
                {
                    float a = Luminance(Pixels[row + x]);
                    float b = Luminance(Pixels[row + x - 1]);
                    sum += Mathf.Abs(a - b);
                    count++;
                }
            }

            return count == 0 ? 0f : (float)(sum / count);
        }

        private static float Percentile(int[] histogram, int count, float fraction)
        {
            if (count <= 0)
                return 0f;

            int target = Mathf.Clamp(Mathf.RoundToInt(count * fraction), 1, count);
            int running = 0;
            for (int bin = 0; bin < histogram.Length; bin++)
            {
                running += histogram[bin];
                if (running >= target)
                    return bin / 255f;
            }

            return 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static float Luminance(Color32 c)
        {
            return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        }
    }
}
