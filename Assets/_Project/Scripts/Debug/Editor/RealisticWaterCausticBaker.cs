using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Bakes the seamless looping caustic flipbook used by the realistic water shaders.
    /// Sunlight is traced through a synthetic wind-wave surface onto a flat seabed and the
    /// arriving photons are accumulated, so the result is a real refraction caustic network
    /// (thin bright filaments over darker cells) instead of a hand-painted cell pattern.
    /// </summary>
    public static class RealisticWaterCausticBaker
    {
        // Each atlas cell is a power-of-two block that holds one traced frame plus a wrap
        // border, so tiling stays seamless once bilinear and mip filtering kick in.
        private const int FrameSize = 496;
        private const int FrameBorder = 8;
        private const int CellSize = FrameSize + 2 * FrameBorder;
        private const int FrameCount = 32;
        private const int AtlasColumns = 8;
        private const int AtlasRows = 4;
        private const int AtlasWidth = AtlasColumns * CellSize;
        private const int AtlasHeight = AtlasRows * CellSize;
        private const int SlopeGridSize = 1024;
        private const int PhotonGridSize = 2048;
        private const int WaveCount = 32;
        private const int MinHarmonic = 5;
        private const int MaxHarmonic = 22;

        // World-space framing of a single tile. The shader maps one tile onto
        // _CausticScale metres, so these two values only set the internal proportions.
        private const float TileWorldSize = 8f;
        private const float BakeDepth = 1.6f;
        private const float DominantWavelength = 0.85f;
        private const float SpectrumBandWidth = 0.70f;
        private const float DirectionalFloor = 0.5f;
        private const float TargetSlopeRms = 0.125f;
        private const float LoopSeconds = 1.6f;
        private const float Gravity = 9.81f;

        // Stored 1.0 equals this multiple of the average seabed irradiance; the shader
        // multiplies the sample back by the same constant before shaping it.
        private const float EncodeRange = 8f;

        // Water is dispersive: red bends slightly less than blue, which tints the razor-thin
        // caustic ridges. The offsets are scaled from the green trace instead of retraced.
        private const float RedDispersion = 0.988f;
        private const float BlueDispersion = 1.016f;

        private const int Seed = 20260730;
        private const string TextureDirectory = "Assets/_Project/Art/Textures/Water";
        private const string FlipbookPath =
            TextureDirectory + "/T_WaterCausticFlipbook.png";

        /// <summary>
        /// Path of the baked flipbook so material setup code can reference it.
        /// </summary>
        public static string FlipbookAssetPath => FlipbookPath;

        /// <summary>
        /// Irradiance multiple that a stored 1.0 represents, needed to decode the flipbook.
        /// </summary>
        public static float EncodeRangeValue => EncodeRange;

        /// <summary>
        /// Atlas layout the shaders need: columns, rows, frame count and loop length.
        /// </summary>
        public static Vector4 AtlasLayout =>
            new(AtlasColumns, AtlasRows, FrameCount, LoopSeconds);

        /// <summary>
        /// Normalised size of one frame inside its cell plus the wrap border offset,
        /// which the shaders use to fold tiling UVs into the right atlas cell.
        /// </summary>
        public static Vector4 AtlasFrameRect =>
            new(
                FrameSize / (float)AtlasWidth,
                FrameSize / (float)AtlasHeight,
                FrameBorder / (float)AtlasWidth,
                FrameBorder / (float)AtlasHeight);

        /// <summary>
        /// Traces the flipbook, writes it next to the other water textures and imports it.
        /// </summary>
        [MenuItem("Market/Debug/Water/Bake Caustic Flipbook")]
        public static void Bake()
        {
            try
            {
                Directory.CreateDirectory(TextureDirectory);
                WaveHarmonic[] spectrum = BuildSpectrum();
                Color32[] atlas = TraceAtlas(spectrum);
                WriteAtlas(atlas);
                AssetDatabase.Refresh();
                ConfigureImporter();
                Debug.Log(
                    "[RealisticWaterCausticBaker] Baked " + FrameCount +
                    " caustic frames into '" + FlipbookPath + "'.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[RealisticWaterCausticBaker] Bake failed: " + exception.Message);
            }
        }

        private readonly struct WaveHarmonic
        {
            public readonly float Kx;
            public readonly float Kz;
            public readonly float Amplitude;
            public readonly float Omega;
            public readonly float Phase;

            public WaveHarmonic(
                float kx, float kz, float amplitude, float omega, float phase)
            {
                Kx = kx;
                Kz = kz;
                Amplitude = amplitude;
                Omega = omega;
                Phase = phase;
            }
        }

        /// <summary>
        /// Picks wave vectors off the tile's integer lattice so the field is seamless, and
        /// quantises every frequency to the loop so the flipbook wraps without a pop.
        /// </summary>
        private static WaveHarmonic[] BuildSpectrum()
        {
            System.Random random = new(Seed);
            Vector2 wind = new Vector2(0.94f, 0.34f).normalized;
            float fundamental = 2f * Mathf.PI / TileWorldSize;
            float peakWaveNumber = 2f * Mathf.PI / DominantWavelength;
            float loopStep = 2f * Mathf.PI / LoopSeconds;
            List<WaveHarmonic> waves = new(WaveCount);

            for (int attempt = 0; attempt < 200000 && waves.Count < WaveCount; attempt++)
            {
                int nx = random.Next(-MaxHarmonic, MaxHarmonic + 1);
                int nz = random.Next(-MaxHarmonic, MaxHarmonic + 1);
                int harmonicSquared = nx * nx + nz * nz;
                if (harmonicSquared < MinHarmonic * MinHarmonic ||
                    harmonicSquared > MaxHarmonic * MaxHarmonic)
                {
                    continue;
                }

                float kx = nx * fundamental;
                float kz = nz * fundamental;
                float waveNumber = Mathf.Sqrt(kx * kx + kz * kz);
                float band = Mathf.Log(waveNumber / peakWaveNumber) / SpectrumBandWidth;
                float alignment = Mathf.Abs(
                    (kx * wind.x + kz * wind.y) / waveNumber);
                float weight = Mathf.Exp(-0.5f * band * band) *
                    Mathf.Lerp(DirectionalFloor, 1f, alignment * alignment);
                if (random.NextDouble() > weight)
                    continue;

                float periods = Mathf.Max(
                    1f, Mathf.Round(Mathf.Sqrt(Gravity * waveNumber) / loopStep));
                waves.Add(new WaveHarmonic(
                    kx,
                    kz,
                    1f / waveNumber,
                    periods * loopStep,
                    (float)random.NextDouble() * 2f * Mathf.PI));
            }

            return NormalizeSlope(waves);
        }

        private static WaveHarmonic[] NormalizeSlope(List<WaveHarmonic> waves)
        {
            if (waves.Count == 0)
                throw new InvalidOperationException("Spectrum sampling produced no waves.");

            double slopeVariance = 0.0;
            foreach (WaveHarmonic wave in waves)
            {
                float waveNumber = Mathf.Sqrt(wave.Kx * wave.Kx + wave.Kz * wave.Kz);
                double slopeAmplitude = wave.Amplitude * waveNumber;
                slopeVariance += slopeAmplitude * slopeAmplitude * 0.5;
            }

            float scale = TargetSlopeRms / Mathf.Sqrt((float)slopeVariance);
            WaveHarmonic[] normalized = new WaveHarmonic[waves.Count];
            for (int index = 0; index < waves.Count; index++)
            {
                WaveHarmonic wave = waves[index];
                normalized[index] = new WaveHarmonic(
                    wave.Kx, wave.Kz, wave.Amplitude * scale, wave.Omega, wave.Phase);
            }

            return normalized;
        }

        private static Color32[] TraceAtlas(WaveHarmonic[] spectrum)
        {
            Color32[] atlas = new Color32[AtlasWidth * AtlasHeight];
            float[] slopeX = new float[SlopeGridSize * SlopeGridSize];
            float[] slopeZ = new float[SlopeGridSize * SlopeGridSize];
            float[] height = new float[SlopeGridSize * SlopeGridSize];

            for (int frame = 0; frame < FrameCount; frame++)
            {
                float time = frame * (LoopSeconds / FrameCount);
                SampleSurface(spectrum, time, slopeX, slopeZ, height);
                float[] irradiance = TraceFrame(slopeX, slopeZ, height);
                Normalize(irradiance);
                Blur(irradiance);
                CopyFrameToAtlas(irradiance, atlas, frame);
            }

            return atlas;
        }

        private static void SampleSurface(
            WaveHarmonic[] spectrum,
            float time,
            float[] slopeX,
            float[] slopeZ,
            float[] height)
        {
            float step = TileWorldSize / SlopeGridSize;
            Parallel.For(0, SlopeGridSize, row =>
            {
                float z = row * step;
                int rowOffset = row * SlopeGridSize;
                for (int column = 0; column < SlopeGridSize; column++)
                {
                    float x = column * step;
                    float gradientX = 0f;
                    float gradientZ = 0f;
                    float surface = 0f;
                    for (int index = 0; index < spectrum.Length; index++)
                    {
                        WaveHarmonic wave = spectrum[index];
                        float phase = wave.Kx * x + wave.Kz * z -
                            wave.Omega * time + wave.Phase;
                        float cosine = Mathf.Cos(phase) * wave.Amplitude;
                        surface += Mathf.Sin(phase) * wave.Amplitude;
                        gradientX += wave.Kx * cosine;
                        gradientZ += wave.Kz * cosine;
                    }

                    slopeX[rowOffset + column] = gradientX;
                    slopeZ[rowOffset + column] = gradientZ;
                    height[rowOffset + column] = surface;
                }
            });
        }

        /// <summary>
        /// Refracts one downward photon per surface sample and accumulates where it lands.
        /// </summary>
        private static float[] TraceFrame(float[] slopeX, float[] slopeZ, float[] height)
        {
            float[] irradiance = new float[FrameSize * FrameSize * 3];
            object gate = new();
            float photonStep = TileWorldSize / PhotonGridSize;
            float refractionIndex = 1.3335f;
            float eta = 1f / refractionIndex;

            Parallel.For(
                0,
                PhotonGridSize,
                () => new float[FrameSize * FrameSize * 3],
                (row, _, local) =>
                {
                    float z = (row + 0.5f) * photonStep;
                    float gridZ = (row + 0.5f) * SlopeGridSize / PhotonGridSize - 0.5f;
                    for (int column = 0; column < PhotonGridSize; column++)
                    {
                        float x = (column + 0.5f) * photonStep;
                        float gridX =
                            (column + 0.5f) * SlopeGridSize / PhotonGridSize - 0.5f;
                        float gradientX = SampleGrid(slopeX, gridX, gridZ);
                        float gradientZ = SampleGrid(slopeZ, gridX, gridZ);
                        float surface = SampleGrid(height, gridX, gridZ);

                        float normalScale = 1f / Mathf.Sqrt(
                            gradientX * gradientX + gradientZ * gradientZ + 1f);
                        float normalX = -gradientX * normalScale;
                        float normalY = normalScale;
                        float normalZ = -gradientZ * normalScale;

                        float cosIncident = normalY;
                        float radicand =
                            1f - eta * eta * (1f - cosIncident * cosIncident);
                        if (radicand <= 0f)
                            continue;

                        float bend = eta * cosIncident - Mathf.Sqrt(radicand);
                        float directionX = bend * normalX;
                        float directionY = -eta + bend * normalY;
                        float directionZ = bend * normalZ;
                        if (directionY >= -1e-4f)
                            continue;

                        float travel = (surface + BakeDepth) / -directionY;
                        float offsetX = directionX * travel;
                        float offsetZ = directionZ * travel;
                        Splat(local, 0, x + offsetX * RedDispersion,
                            z + offsetZ * RedDispersion);
                        Splat(local, 1, x + offsetX, z + offsetZ);
                        Splat(local, 2, x + offsetX * BlueDispersion,
                            z + offsetZ * BlueDispersion);
                    }

                    return local;
                },
                local =>
                {
                    lock (gate)
                    {
                        for (int index = 0; index < irradiance.Length; index++)
                            irradiance[index] += local[index];
                    }
                });

            return irradiance;
        }

        private static float SampleGrid(float[] grid, float x, float z)
        {
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            float fractionX = x - x0;
            float fractionZ = z - z0;
            int left = Wrap(x0, SlopeGridSize);
            int right = Wrap(x0 + 1, SlopeGridSize);
            int bottom = Wrap(z0, SlopeGridSize) * SlopeGridSize;
            int top = Wrap(z0 + 1, SlopeGridSize) * SlopeGridSize;
            float lower = Mathf.Lerp(grid[bottom + left], grid[bottom + right], fractionX);
            float upper = Mathf.Lerp(grid[top + left], grid[top + right], fractionX);
            return Mathf.Lerp(lower, upper, fractionZ);
        }

        private static void Splat(float[] target, int channel, float worldX, float worldZ)
        {
            float pixelX = worldX / TileWorldSize * FrameSize - 0.5f;
            float pixelZ = worldZ / TileWorldSize * FrameSize - 0.5f;
            int x0 = Mathf.FloorToInt(pixelX);
            int z0 = Mathf.FloorToInt(pixelZ);
            float fractionX = pixelX - x0;
            float fractionZ = pixelZ - z0;
            int left = Wrap(x0, FrameSize);
            int right = Wrap(x0 + 1, FrameSize);
            int bottom = Wrap(z0, FrameSize) * FrameSize;
            int top = Wrap(z0 + 1, FrameSize) * FrameSize;
            target[(bottom + left) * 3 + channel] +=
                (1f - fractionX) * (1f - fractionZ);
            target[(bottom + right) * 3 + channel] += fractionX * (1f - fractionZ);
            target[(top + left) * 3 + channel] += (1f - fractionX) * fractionZ;
            target[(top + right) * 3 + channel] += fractionX * fractionZ;
        }

        private static int Wrap(int value, int size)
        {
            int wrapped = value % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        /// <summary>
        /// Rescales so an unlit flat surface would read 1.0, which makes the shader's
        /// pedestal and gain independent of the photon count.
        /// </summary>
        private static void Normalize(float[] irradiance)
        {
            float expected =
                (float)PhotonGridSize * PhotonGridSize / (FrameSize * FrameSize);
            float inverse = 1f / expected;
            for (int index = 0; index < irradiance.Length; index++)
                irradiance[index] *= inverse;
        }

        /// <summary>
        /// Softens by roughly the angular size of the sun's disc at the baked depth, which
        /// also removes the photon noise without rounding off the filaments.
        /// </summary>
        private static void Blur(float[] irradiance)
        {
            float[] scratch = new float[irradiance.Length];
            BlurAxis(irradiance, scratch, true);
            BlurAxis(scratch, irradiance, false);
        }

        private static void BlurAxis(float[] source, float[] target, bool horizontal)
        {
            const float SideWeight = 0.11f;
            const float CenterWeight = 1f - 2f * SideWeight;
            Parallel.For(0, FrameSize, row =>
            {
                for (int column = 0; column < FrameSize; column++)
                {
                    int previous = horizontal
                        ? row * FrameSize + Wrap(column - 1, FrameSize)
                        : Wrap(row - 1, FrameSize) * FrameSize + column;
                    int next = horizontal
                        ? row * FrameSize + Wrap(column + 1, FrameSize)
                        : Wrap(row + 1, FrameSize) * FrameSize + column;
                    int center = row * FrameSize + column;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        target[center * 3 + channel] =
                            source[previous * 3 + channel] * SideWeight +
                            source[center * 3 + channel] * CenterWeight +
                            source[next * 3 + channel] * SideWeight;
                    }
                }
            });
        }

        /// <summary>
        /// Writes one traced frame into its atlas cell, repeating the wrapped opposite edge
        /// into the border so bilinear and mip taps never bleed across neighbouring frames.
        /// </summary>
        private static void CopyFrameToAtlas(float[] irradiance, Color32[] atlas, int frame)
        {
            int originX = frame % AtlasColumns * CellSize;
            int originY = frame / AtlasColumns * CellSize;
            for (int y = 0; y < CellSize; y++)
            {
                int sourceY = Wrap(y - FrameBorder, FrameSize) * FrameSize;
                int targetRow = (originY + y) * AtlasWidth + originX;
                for (int x = 0; x < CellSize; x++)
                {
                    int source = (sourceY + Wrap(x - FrameBorder, FrameSize)) * 3;
                    atlas[targetRow + x] = new Color32(
                        EncodeChannel(irradiance[source]),
                        EncodeChannel(irradiance[source + 1]),
                        EncodeChannel(irradiance[source + 2]),
                        255);
                }
            }
        }

        private static byte EncodeChannel(float irradiance)
        {
            float normalized = Mathf.Clamp01(irradiance / EncodeRange);
            float encoded = normalized <= 0.0031308f
                ? normalized * 12.92f
                : 1.055f * Mathf.Pow(normalized, 1f / 2.4f) - 0.055f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(encoded * 255f), 0, 255);
        }

        private static void WriteAtlas(Color32[] atlas)
        {
            Texture2D texture = new(
                AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false, false);
            try
            {
                texture.SetPixels32(atlas);
                texture.Apply(false, false);
                File.WriteAllBytes(FlipbookPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ConfigureImporter()
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(FlipbookPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "No texture importer for '" + FlipbookPath + "'.");

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = AtlasWidth;
            importer.SaveAndReimport();
        }
    }
}
