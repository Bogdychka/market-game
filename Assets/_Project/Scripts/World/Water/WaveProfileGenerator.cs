using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Builds a wave bank from seeded settings. The whole point is reproducibility: the same
    /// settings rebuild byte-identical layers on any machine and any Unity version, so it uses its
    /// own integer hash instead of UnityEngine.Random, whose stream is shared global state.
    /// </summary>
    public static class WaveProfileGenerator
    {
        /// <summary>
        /// Regenerates <paramref name="destination"/> in place from <paramref name="settings"/>.
        /// Layers descend from the longest swell to the shortest chop; amplitude and steepness
        /// follow their curves over the normalized wavelength, so one curve edit reshapes the
        /// whole bank without touching a single layer.
        /// </summary>
        public static void Generate(
            WaveGenerationSettings settings,
            List<WaveLayer> destination)
        {
            if (settings == null || destination == null)
                return;

            int layerCount = Mathf.Clamp(settings.LayerCount, 1, WaveProfile.MaxLayers);
            float minWavelength = settings.MinWavelength;
            float maxWavelength = settings.MaxWavelength;

            while (destination.Count > layerCount)
                destination.RemoveAt(destination.Count - 1);
            while (destination.Count < layerCount)
                destination.Add(new WaveLayer());

            for (int i = 0; i < layerCount; i++)
            {
                float slot = layerCount > 1 ? i / (float)(layerCount - 1) : 0f;

                // Even ladder from long to short, then nudged off its slot so the bank does not
                // beat in a visible pattern.
                float jitter = settings.WavelengthJitter *
                    (Hash01(settings.Seed, i, 0) * 2f - 1f);
                float wavelength = Mathf.Clamp(
                    Mathf.Lerp(maxWavelength, minWavelength, Mathf.Clamp01(slot + jitter)),
                    minWavelength,
                    maxWavelength);

                float lengthFraction = maxWavelength - minWavelength > 0.0001f
                    ? Mathf.InverseLerp(minWavelength, maxWavelength, wavelength)
                    : 1f;

                WaveLayer layer = destination[i] ?? new WaveLayer();
                destination[i] = layer;

                layer.Enabled = true;
                layer.Mode = settings.Mode;
                layer.Origin = settings.Origin;
                layer.Wavelength = wavelength;
                layer.Amplitude = Mathf.Lerp(
                    settings.MinAmplitude,
                    settings.MaxAmplitude,
                    EvaluateShare(settings.AmplitudeByLength, lengthFraction));
                layer.Steepness = Mathf.Lerp(
                    settings.MinSteepness,
                    settings.MaxSteepness,
                    EvaluateShare(settings.SteepnessByLength, lengthFraction));

                // Fan the directions deterministically around the base angle instead of drawing
                // them at random: a random fan clumps, and a clumped bank reads as one big wave.
                float fan = layerCount > 1
                    ? slot - 0.5f
                    : 0f;
                float wobble = (Hash01(settings.Seed, i, 1) * 2f - 1f) * 0.5f /
                    Mathf.Max(1, layerCount);
                layer.DirectionAngle = settings.BaseDirectionAngle +
                    (fan + wobble) * settings.DirectionAngleVariation;

                // Dispersion already gives short waves their higher frequency; this only adds a
                // small per-layer drift so no two layers stay phase locked.
                layer.SpeedMultiplier = Mathf.Lerp(
                    0.9f, 1.1f, Hash01(settings.Seed, i, 2));
            }
        }

        private static float EvaluateShare(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0)
                return t;

            return Mathf.Clamp01(curve.Evaluate(t));
        }

        /// <summary>
        /// Deterministic 0-1 hash of a seed and two indices (Wang-style integer mix).
        /// </summary>
        private static float Hash01(int seed, int index, int channel)
        {
            unchecked
            {
                uint value = (uint)seed * 747796405u +
                    (uint)index * 2891336453u +
                    (uint)channel * 2246822519u + 374761393u;
                value = (value ^ (value >> 15)) * 2246822519u;
                value = (value ^ (value >> 13)) * 3266489917u;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / (float)0x01000000u;
            }
        }
    }
}
