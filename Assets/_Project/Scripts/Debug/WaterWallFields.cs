using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// What a settings-wall row actually writes to.
    /// </summary>
    public enum WaterWallTarget
    {
        /// <summary>A float property on the water material.</summary>
        MaterialFloat = 0,

        /// <summary>Wave length multiplier on the bound wave profile.</summary>
        WaveLengthScale = 1,

        /// <summary>Wave amplitude multiplier on the bound wave profile.</summary>
        WaveAmplitudeScale = 2,

        /// <summary>Wave steepness multiplier on the bound wave profile.</summary>
        WaveSteepnessScale = 3,
    }

    /// <summary>
    /// One labelled slider row of the water settings wall.
    /// </summary>
    public readonly struct WaterWallField
    {
        /// <summary>Group heading this row is filed under.</summary>
        public readonly string Group;

        /// <summary>Row label shown on the wall.</summary>
        public readonly string Label;

        /// <summary>Shader property name; empty for the wave-profile rows.</summary>
        public readonly string Property;

        /// <summary>What the row writes to.</summary>
        public readonly WaterWallTarget Target;

        /// <summary>Slider minimum.</summary>
        public readonly float Minimum;

        /// <summary>Slider maximum.</summary>
        public readonly float Maximum;

        /// <summary>Creates a settings-wall row.</summary>
        public WaterWallField(
            string group,
            string label,
            string property,
            WaterWallTarget target,
            float minimum,
            float maximum)
        {
            Group = group;
            Label = label;
            Property = property;
            Target = target;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>Shader property id, cached by the wall at build time.</summary>
        public int PropertyId => Shader.PropertyToID(Property);
    }

    /// <summary>
    /// The properties the water settings wall exposes. Kept as one table so the wall stays a
    /// layout that reads a list, not a hand-wired panel that has to be edited per property.
    /// Ranges match the shader's own Range() declarations, so a slider cannot push the material
    /// somewhere the shader was never tuned for.
    /// </summary>
    public static class WaterWallFields
    {
        /// <summary>Every row on the wall, in display order.</summary>
        public static readonly WaterWallField[] All =
        {
            new("WAVES", "Wave Height", string.Empty,
                WaterWallTarget.WaveAmplitudeScale, 0f, 3f),
            new("WAVES", "Wave Length", string.Empty,
                WaterWallTarget.WaveLengthScale, 0.25f, 3f),
            new("WAVES", "Wave Steepness", string.Empty,
                WaterWallTarget.WaveSteepnessScale, 0f, 2f),
            new("WAVES", "Wind Spread", "_WindSpread",
                WaterWallTarget.MaterialFloat, 0f, 1f),

            new("SURFACE", "Micro Ripples", "_MicroWaveStrength",
                WaterWallTarget.MaterialFloat, 0f, 1f),
            new("SURFACE", "Roughness", "_Roughness",
                WaterWallTarget.MaterialFloat, 0.02f, 0.4f),
            new("SURFACE", "Refraction", "_RefractionStrength",
                WaterWallTarget.MaterialFloat, 0f, 0.2f),
            new("SURFACE", "Reflection", "_ReflectionStrength",
                WaterWallTarget.MaterialFloat, 0f, 1f),

            new("DEPTH", "Optical Path", "_DepthFadeDistance",
                WaterWallTarget.MaterialFloat, 0.5f, 60f),
            new("DEPTH", "In-Scattering", "_ScatteringStrength",
                WaterWallTarget.MaterialFloat, 0f, 1f),
            new("DEPTH", "Crest Subsurface", "_SubsurfaceStrength",
                WaterWallTarget.MaterialFloat, 0f, 4f),

            new("FOAM", "Whitecap Gain", "_FoamCrestGain",
                WaterWallTarget.MaterialFloat, 0f, 12f),
            new("FOAM", "Whitecap Height", "_FoamCrestHeight",
                WaterWallTarget.MaterialFloat, 0f, 3f),
            new("FOAM", "Shore Foam Width", "_FoamShoreWidth",
                WaterWallTarget.MaterialFloat, 0.1f, 10f),
            new("FOAM", "Edge Breakup", "_FoamBreakup",
                WaterWallTarget.MaterialFloat, 0f, 1f),

            new("CAUSTICS", "Caustic Intensity", "_CausticIntensity",
                WaterWallTarget.MaterialFloat, 0f, 3f),
        };
    }
}
