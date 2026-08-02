using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// One wave layer after the profile multipliers and curves have been applied - the exact set
    /// of numbers the shader and the C# sampler both evaluate.
    /// </summary>
    public readonly struct ResolvedWaveLayer
    {
        /// <summary>Crest-to-crest distance in metres.</summary>
        public readonly float Wavelength;

        /// <summary>Vertical half-height in metres.</summary>
        public readonly float Amplitude;

        /// <summary>Crest sharpness in the 0-1 range, before the fold-safe clamp.</summary>
        public readonly float Steepness;

        /// <summary>Authored travel direction in degrees, before the wind bend.</summary>
        public readonly float DirectionAngle;

        /// <summary>Art-directed multiplier on the dispersion speed.</summary>
        public readonly float SpeedMultiplier;

        /// <summary>Directional or circular travel.</summary>
        public readonly WaveLayerMode Mode;

        /// <summary>World XZ origin used by circular layers.</summary>
        public readonly Vector2 Origin;

        /// <summary>Creates a resolved layer from already-scaled values.</summary>
        public ResolvedWaveLayer(
            float wavelength,
            float amplitude,
            float steepness,
            float directionAngle,
            float speedMultiplier,
            WaveLayerMode mode,
            Vector2 origin)
        {
            Wavelength = Mathf.Max(0.05f, wavelength);
            Amplitude = Mathf.Max(0f, amplitude);
            Steepness = Mathf.Clamp01(steepness);
            DirectionAngle = directionAngle;
            SpeedMultiplier = Mathf.Max(0f, speedMultiplier);
            Mode = mode;
            Origin = origin;
        }

        /// <summary>
        /// Returns the layer with its wavelength, amplitude and steepness scaled. Used to drive a
        /// whole bank from outside the asset - weather, a quality tier - without editing it.
        /// </summary>
        public ResolvedWaveLayer Scaled(Vector3 bankScale)
        {
            return new ResolvedWaveLayer(
                Wavelength * bankScale.x,
                Amplitude * bankScale.y,
                Steepness * bankScale.z,
                DirectionAngle,
                SpeedMultiplier,
                Mode,
                Origin);
        }

        /// <summary>
        /// Packs the layer into the two float4 rows the shader arrays expect:
        /// A = (angle, wavelength, amplitude, speed), B = (steepness, mode, originX, originZ).
        /// </summary>
        public void Pack(out Vector4 rowA, out Vector4 rowB)
        {
            rowA = new Vector4(
                DirectionAngle, Wavelength, Amplitude, SpeedMultiplier);
            rowB = new Vector4(
                Steepness,
                Mode == WaveLayerMode.Circular ? 1f : 0f,
                Origin.x,
                Origin.y);
        }
    }
}
