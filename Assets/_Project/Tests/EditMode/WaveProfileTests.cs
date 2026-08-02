using System.Collections.Generic;
using Market.World;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    /// <summary>
    /// Covers the wave bank the water shaders render from: seeded generation has to be
    /// reproducible, multipliers and curves have to resolve the way the shader upload reads them,
    /// and the C# sampler has to land on the same surface the vertex shader displaces.
    /// </summary>
    public sealed class WaveProfileTests
    {
        private readonly List<WaveProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i] != null)
                    Object.DestroyImmediate(_profiles[i]);
            }

            _profiles.Clear();
        }

        [Test]
        public void Generate_IsReproducibleForTheSameSeed()
        {
            WaveProfile first = CreateProfile(seed: 12345, layerCount: 6);
            WaveProfile second = CreateProfile(seed: 12345, layerCount: 6);

            Assert.AreEqual(6, first.Layers.Count);
            for (int i = 0; i < first.Layers.Count; i++)
            {
                Assert.AreEqual(
                    first.Layers[i].Wavelength, second.Layers[i].Wavelength, 0.00001f,
                    $"Wavelength of layer {i} is not reproducible.");
                Assert.AreEqual(
                    first.Layers[i].Amplitude, second.Layers[i].Amplitude, 0.00001f,
                    $"Amplitude of layer {i} is not reproducible.");
                Assert.AreEqual(
                    first.Layers[i].DirectionAngle, second.Layers[i].DirectionAngle, 0.00001f,
                    $"Direction of layer {i} is not reproducible.");
            }
        }

        [Test]
        public void Generate_DifferentSeedsProduceDifferentBanks()
        {
            WaveProfile first = CreateProfile(seed: 1, layerCount: 4);
            WaveProfile second = CreateProfile(seed: 2, layerCount: 4);

            bool anyDifference = false;
            for (int i = 0; i < first.Layers.Count; i++)
            {
                if (!Mathf.Approximately(
                        first.Layers[i].Wavelength, second.Layers[i].Wavelength) ||
                    !Mathf.Approximately(
                        first.Layers[i].DirectionAngle, second.Layers[i].DirectionAngle))
                {
                    anyDifference = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifference, "Two seeds produced an identical bank.");
        }

        [Test]
        public void Generate_LayerCountIsCappedAtTheShaderLimit()
        {
            WaveProfile profile = CreateProfile(seed: 7, layerCount: 32);

            Assert.AreEqual(WaveProfile.MaxLayers, profile.Layers.Count);
        }

        [Test]
        public void ResolveLayers_AppliesMultipliers()
        {
            WaveProfile profile = CreateProfile(seed: 3, layerCount: 3);
            float authoredWavelength = profile.Layers[0].Wavelength;
            float authoredAmplitude = profile.Layers[0].Amplitude;
            profile.WavelengthMultiplier = 2f;
            profile.AmplitudeMultiplier = 0.5f;

            List<ResolvedWaveLayer> resolved = new();
            profile.ResolveLayers(resolved);

            Assert.AreEqual(3, resolved.Count);
            Assert.AreEqual(authoredWavelength * 2f, resolved[0].Wavelength, 0.0001f);
            Assert.AreEqual(authoredAmplitude * 0.5f, resolved[0].Amplitude, 0.0001f);
        }

        [Test]
        public void ResolveLayers_SkipsDisabledLayers()
        {
            WaveProfile profile = CreateProfile(seed: 4, layerCount: 4);
            profile.Layers[1].Enabled = false;

            List<ResolvedWaveLayer> resolved = new();
            profile.ResolveLayers(resolved);

            Assert.AreEqual(3, resolved.Count);
            Assert.AreEqual(3, profile.ActiveLayerCount);
        }

        [Test]
        public void ApplyMultipliers_BakesTheValueAndResetsTheKnob()
        {
            WaveProfile profile = CreateProfile(seed: 5, layerCount: 2);
            float authoredAmplitude = profile.Layers[0].Amplitude;
            profile.AmplitudeMultiplier = 3f;

            profile.ApplyMultipliers(false, true, false);

            Assert.AreEqual(authoredAmplitude * 3f, profile.Layers[0].Amplitude, 0.0001f);
            Assert.AreEqual(1f, profile.AmplitudeMultiplier, 0.0001f);
        }

        [Test]
        public void SampleHeight_StaysWithinTheSumOfAmplitudes()
        {
            WaveProfile profile = CreateProfile(seed: 11, layerCount: 4);
            List<ResolvedWaveLayer> layers = new();
            profile.ResolveLayers(layers);

            float amplitudeSum = 0f;
            for (int i = 0; i < layers.Count; i++)
                amplitudeSum += layers[i].Amplitude;

            WaveWindSettings wind = new(new Vector2(1f, 0f), 0.5f);
            for (int step = 0; step < 32; step++)
            {
                Vector2 point = new(step * 3.7f, step * -2.3f);
                float height = WaveSampler.SampleHeight(
                    layers, wind, point, step * 0.25f, stillWaterY: 5f);

                Assert.LessOrEqual(Mathf.Abs(height - 5f), amplitudeSum + 0.0001f);
            }
        }

        [Test]
        public void SampleHeight_InverseSolveLandsOnTheDisplacedSurface()
        {
            WaveProfile profile = CreateProfile(seed: 13, layerCount: 4);
            List<ResolvedWaveLayer> layers = new();
            profile.ResolveLayers(layers);
            WaveWindSettings wind = new(new Vector2(0.4f, -0.9f), 0.55f);

            // A vertex starting at basePoint is drawn at basePoint + displacement.xz. Asking the
            // sampler for the height at that drawn position must return the drawn height, or a
            // floating object sits beside the wave instead of on it.
            Vector2 basePoint = new(12.5f, -4.25f);
            const float time = 3.5f;
            Vector3 displacement = WaveSampler.EvaluateDisplacement(
                layers, wind, basePoint, time, WaveSampler.DefaultFoldLimit, out _);
            Vector2 drawnPoint = basePoint + new Vector2(displacement.x, displacement.z);

            float sampled = WaveSampler.SampleHeight(
                layers, wind, drawnPoint, time, stillWaterY: 0f, iterations: 6);

            Assert.AreEqual(displacement.y, sampled, 0.01f);
        }

        [Test]
        public void FoldSafeSteepness_ClampsSharpShortWaves()
        {
            // A short, tall wave asks for a steepness the surface cannot hold without folding.
            float clamped = WaveSampler.FoldSafeSteepness(
                steepness: 1f, wavelength: 1f, amplitude: 0.5f,
                foldLimit: WaveSampler.DefaultFoldLimit);

            Assert.Less(clamped, 1f);
            Assert.Greater(clamped, 0f);
        }

        [Test]
        public void CircularLayer_RadiatesFromItsOrigin()
        {
            // Two points at the same distance from the origin must sit at the same height, and a
            // point a half wavelength further out must not.
            List<ResolvedWaveLayer> layers = new()
            {
                new ResolvedWaveLayer(
                    wavelength: 4f,
                    amplitude: 0.3f,
                    steepness: 0.3f,
                    directionAngle: 0f,
                    speedMultiplier: 1f,
                    mode: WaveLayerMode.Circular,
                    origin: Vector2.zero),
            };

            // Radius 9 is a crest for this 4 m wavelength; radius 10 is a quarter period past it.
            // Sampling a zero crossing would pass the equality check for the wrong reason.
            WaveWindSettings wind = WaveWindSettings.Default;
            float north = WaveSampler.SampleHeight(
                layers, wind, new Vector2(0f, 9f), 0f, stillWaterY: 0f);
            float east = WaveSampler.SampleHeight(
                layers, wind, new Vector2(9f, 0f), 0f, stillWaterY: 0f);
            float further = WaveSampler.SampleHeight(
                layers, wind, new Vector2(0f, 10f), 0f, stillWaterY: 0f);

            Assert.Greater(Mathf.Abs(north), 0.01f, "Sampled a zero crossing, not a crest.");

            Assert.AreEqual(north, east, 0.001f);
            Assert.Greater(Mathf.Abs(north - further), 0.001f);
        }

        private WaveProfile CreateProfile(int seed, int layerCount)
        {
            WaveProfile profile = ScriptableObject.CreateInstance<WaveProfile>();
            _profiles.Add(profile);
            profile.Generation.Seed = seed;
            profile.Generation.LayerCount = layerCount;
            profile.RegenerateLayers();
            return profile;
        }
    }
}
