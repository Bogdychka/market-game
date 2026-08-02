using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Wind steering shared by every layer of a bank, mirroring _WindDirection and _WindSpread on
    /// the water material.
    /// </summary>
    public readonly struct WaveWindSettings
    {
        /// <summary>Direction the wind blows toward, on the world XZ plane.</summary>
        public readonly Vector2 Direction;

        /// <summary>0 aligns every layer with the wind, 1 keeps the authored fan.</summary>
        public readonly float Spread;

        /// <summary>Creates wind settings from a world XZ direction and a 0-1 spread.</summary>
        public WaveWindSettings(Vector2 direction, float spread)
        {
            Direction = direction;
            Spread = Mathf.Clamp01(spread);
        }

        /// <summary>Wind that leaves the authored directions untouched.</summary>
        public static WaveWindSettings Default => new(new Vector2(1f, 0f), 1f);

        /// <summary>Normalized wind direction, falling back to +X like the shader does.</summary>
        public Vector2 NormalizedDirection
        {
            get
            {
                float lengthSq = Direction.sqrMagnitude;
                return lengthSq > 0.0001f
                    ? Direction / Mathf.Sqrt(lengthSq)
                    : new Vector2(1f, 0f);
            }
        }
    }

    /// <summary>
    /// CPU twin of RealisticWaterWaves.hlsl. Every formula here is the same one the vertex shader
    /// runs, so anything that has to agree with what the player sees - buoyancy, splashes, a boat,
    /// a float bobbing on the surface - can read the wave height instead of guessing it.
    /// Keep the two files edited together.
    /// </summary>
    public static class WaveSampler
    {
        private const float TwoPi = 6.28318530718f;
        private const float Gravity = 9.81f;

        /// <summary>Default fold limit, matching WaveProfile's Steepness Clamping default.</summary>
        public const float DefaultFoldLimit = 0.95f;

        /// <summary>
        /// Accumulates the displacement of the whole bank at one world XZ point, plus the surface
        /// normal built from the exact Gerstner derivatives.
        /// </summary>
        public static Vector3 EvaluateDisplacement(
            IReadOnlyList<ResolvedWaveLayer> layers,
            in WaveWindSettings wind,
            Vector2 worldXZ,
            float time,
            float foldLimit,
            out Vector3 normal)
        {
            Vector3 offset = Vector3.zero;
            Vector3 tangentX = new(1f, 0f, 0f);
            Vector3 tangentZ = new(0f, 0f, 1f);

            if (layers != null)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    EvaluateLayer(
                        layers[i], wind, worldXZ, time, foldLimit,
                        ref offset, ref tangentX, ref tangentZ);
                }
            }

            Vector3 macroNormal = Vector3.Cross(tangentZ, tangentX);
            normal = macroNormal.sqrMagnitude < 0.000001f
                ? Vector3.up
                : macroNormal.normalized;
            return offset;
        }

        /// <summary>
        /// Returns the water height above <paramref name="stillWaterY"/> at a world XZ position.
        /// Gerstner waves move the surface sideways as well as up, so the point that lands on
        /// <paramref name="worldXZ"/> started somewhere else; a few fixed-point iterations invert
        /// that horizontal displacement. Four is plenty at normal steepness.
        /// </summary>
        public static float SampleHeight(
            IReadOnlyList<ResolvedWaveLayer> layers,
            in WaveWindSettings wind,
            Vector2 worldXZ,
            float time,
            float stillWaterY,
            float foldLimit = DefaultFoldLimit,
            int iterations = 4)
        {
            Vector2 samplePoint = worldXZ;
            Vector3 displacement = Vector3.zero;

            int steps = Mathf.Max(1, iterations);
            for (int i = 0; i < steps; i++)
            {
                displacement = EvaluateDisplacement(
                    layers, wind, samplePoint, time, foldLimit, out _);
                samplePoint = worldXZ - new Vector2(displacement.x, displacement.z);
            }

            displacement = EvaluateDisplacement(
                layers, wind, samplePoint, time, foldLimit, out _);
            return stillWaterY + displacement.y;
        }

        /// <summary>
        /// Returns the surface normal at a world XZ position, using the same inverse solve as
        /// <see cref="SampleHeight"/>.
        /// </summary>
        public static Vector3 SampleNormal(
            IReadOnlyList<ResolvedWaveLayer> layers,
            in WaveWindSettings wind,
            Vector2 worldXZ,
            float time,
            float foldLimit = DefaultFoldLimit,
            int iterations = 4)
        {
            Vector2 samplePoint = worldXZ;
            Vector3 normal = Vector3.up;

            int steps = Mathf.Max(1, iterations);
            for (int i = 0; i < steps; i++)
            {
                Vector3 displacement = EvaluateDisplacement(
                    layers, wind, samplePoint, time, foldLimit, out normal);
                samplePoint = worldXZ - new Vector2(displacement.x, displacement.z);
            }

            EvaluateDisplacement(layers, wind, samplePoint, time, foldLimit, out normal);
            return normal;
        }

        /// <summary>
        /// Bends an authored angle toward the wind exactly as RealisticWaterWindAlignedAngle does.
        /// </summary>
        public static float WindAlignedAngle(
            float authoredAngleDegrees, in WaveWindSettings wind)
        {
            Vector2 windDirection = wind.NormalizedDirection;
            float windAngle = Mathf.Atan2(windDirection.y, windDirection.x);
            float authoredAngle = authoredAngleDegrees * Mathf.Deg2Rad;
            float angleDelta = authoredAngle - windAngle;
            float shortestDelta = Mathf.Atan2(Mathf.Sin(angleDelta), Mathf.Cos(angleDelta));
            return windAngle + shortestDelta * wind.Spread;
        }

        /// <summary>
        /// Clamps steepness below the fold point, matching the shader's fold-safe limit.
        /// </summary>
        public static float FoldSafeSteepness(
            float steepness, float wavelength, float amplitude, float foldLimit)
        {
            float waveNumberAmplitude =
                TwoPi * Mathf.Max(0f, amplitude) / Mathf.Max(0.05f, wavelength);
            float limit = foldLimit > 0.0001f ? foldLimit : DefaultFoldLimit;
            float foldSafe = limit / Mathf.Max(4f * waveNumberAmplitude, 0.0001f);
            return Mathf.Min(Mathf.Clamp01(steepness), foldSafe);
        }

        private static void EvaluateLayer(
            in ResolvedWaveLayer layer,
            in WaveWindSettings wind,
            Vector2 worldXZ,
            float time,
            float foldLimit,
            ref Vector3 offset,
            ref Vector3 tangentX,
            ref Vector3 tangentZ)
        {
            float waveAngle = WindAlignedAngle(layer.DirectionAngle, wind);
            Vector2 direction = new(Mathf.Cos(waveAngle), Mathf.Sin(waveAngle));
            float wavelength = Mathf.Max(0.05f, layer.Wavelength);
            float amplitude = Mathf.Max(0f, layer.Amplitude);
            float steepness = FoldSafeSteepness(
                layer.Steepness, wavelength, amplitude, foldLimit);

            float phaseDistance = Vector2.Dot(direction, worldXZ);
            if (layer.Mode == WaveLayerMode.Circular)
            {
                Vector2 toPoint = worldXZ - layer.Origin;
                float distance = toPoint.magnitude;
                if (distance > 0.0001f)
                    direction = toPoint / distance;
                phaseDistance = distance;
            }

            float waveNumber = TwoPi / wavelength;
            float waveNumberAmplitude = waveNumber * amplitude;
            float angularFrequency = Mathf.Sqrt(Gravity * waveNumber);
            float phase = waveNumber * phaseDistance -
                time * angularFrequency * layer.SpeedMultiplier;
            float sine = Mathf.Sin(phase);
            float cosine = Mathf.Cos(phase);

            offset.x += steepness * amplitude * direction.x * cosine;
            offset.z += steepness * amplitude * direction.y * cosine;
            offset.y += amplitude * sine;

            float horizontalDerivative = steepness * waveNumberAmplitude * sine;
            tangentX.x += -horizontalDerivative * direction.x * direction.x;
            tangentX.y += waveNumberAmplitude * direction.x * cosine;
            tangentX.z += -horizontalDerivative * direction.x * direction.y;
            tangentZ.x += -horizontalDerivative * direction.x * direction.y;
            tangentZ.y += waveNumberAmplitude * direction.y * cosine;
            tangentZ.z += -horizontalDerivative * direction.y * direction.y;
        }
    }
}
