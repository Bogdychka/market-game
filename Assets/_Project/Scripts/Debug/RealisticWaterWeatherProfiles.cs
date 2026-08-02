using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Describes the supported wind-driven water states.
    /// </summary>
    public enum RealisticWaterWeather
    {
        Calm = 0,
        Breeze = 1,
        Windy = 2,
        Storm = 3,
    }

    /// <summary>
    /// Stores the coordinated shader values for one realistic-water weather state.
    /// </summary>
    public readonly struct RealisticWaterWeatherProfile
    {
        public readonly float WindSpread;
        public readonly Vector4 Wave1Params;
        public readonly Vector4 Wave2Params;
        public readonly Vector4 Wave3Params;
        public readonly Vector4 Wave4Params;
        public readonly Vector4 WaveSteepness;
        public readonly Vector2 NormalSpeeds;
        public readonly float MicroWaveStrength;
        public readonly float RefractionStrength;
        public readonly float Roughness;
        public readonly float FoamCrestGain;
        public readonly float FoamCrestBias;
        public readonly float FoamCrestStrength;
        public readonly float FoamNoiseSpeed;
        public readonly float SurfaceCausticIntensity;
        public readonly float SurfaceCausticSpeed;
        public readonly float ProjectedCausticIntensity;
        public readonly Vector2 ProjectedCausticSpeeds;

        public RealisticWaterWeatherProfile(
            float windSpread,
            Vector4 wave1Params,
            Vector4 wave2Params,
            Vector4 wave3Params,
            Vector4 wave4Params,
            Vector4 waveSteepness,
            Vector2 normalSpeeds,
            float microWaveStrength,
            float refractionStrength,
            float roughness,
            float foamCrestGain,
            float foamCrestBias,
            float foamCrestStrength,
            float foamNoiseSpeed,
            float surfaceCausticIntensity,
            float surfaceCausticSpeed,
            float projectedCausticIntensity,
            Vector2 projectedCausticSpeeds)
        {
            WindSpread = windSpread;
            Wave1Params = wave1Params;
            Wave2Params = wave2Params;
            Wave3Params = wave3Params;
            Wave4Params = wave4Params;
            WaveSteepness = waveSteepness;
            NormalSpeeds = normalSpeeds;
            MicroWaveStrength = microWaveStrength;
            RefractionStrength = refractionStrength;
            Roughness = roughness;
            FoamCrestGain = foamCrestGain;
            FoamCrestBias = foamCrestBias;
            FoamCrestStrength = foamCrestStrength;
            FoamNoiseSpeed = foamNoiseSpeed;
            SurfaceCausticIntensity = surfaceCausticIntensity;
            SurfaceCausticSpeed = surfaceCausticSpeed;
            ProjectedCausticIntensity = projectedCausticIntensity;
            ProjectedCausticSpeeds = projectedCausticSpeeds;
        }

        /// <summary>
        /// Blends every coordinated water property between two weather profiles.
        /// </summary>
        public static RealisticWaterWeatherProfile Lerp(
            RealisticWaterWeatherProfile from,
            RealisticWaterWeatherProfile to,
            float blend)
        {
            float t = Mathf.Clamp01(blend);
            return new RealisticWaterWeatherProfile(
                Mathf.Lerp(from.WindSpread, to.WindSpread, t),
                Vector4.Lerp(from.Wave1Params, to.Wave1Params, t),
                Vector4.Lerp(from.Wave2Params, to.Wave2Params, t),
                Vector4.Lerp(from.Wave3Params, to.Wave3Params, t),
                Vector4.Lerp(from.Wave4Params, to.Wave4Params, t),
                Vector4.Lerp(from.WaveSteepness, to.WaveSteepness, t),
                Vector2.Lerp(from.NormalSpeeds, to.NormalSpeeds, t),
                Mathf.Lerp(from.MicroWaveStrength, to.MicroWaveStrength, t),
                Mathf.Lerp(from.RefractionStrength, to.RefractionStrength, t),
                Mathf.Lerp(from.Roughness, to.Roughness, t),
                Mathf.Lerp(from.FoamCrestGain, to.FoamCrestGain, t),
                Mathf.Lerp(from.FoamCrestBias, to.FoamCrestBias, t),
                Mathf.Lerp(from.FoamCrestStrength, to.FoamCrestStrength, t),
                Mathf.Lerp(from.FoamNoiseSpeed, to.FoamNoiseSpeed, t),
                Mathf.Lerp(
                    from.SurfaceCausticIntensity,
                    to.SurfaceCausticIntensity,
                    t),
                Mathf.Lerp(from.SurfaceCausticSpeed, to.SurfaceCausticSpeed, t),
                Mathf.Lerp(
                    from.ProjectedCausticIntensity,
                    to.ProjectedCausticIntensity,
                    t),
                Vector2.Lerp(
                    from.ProjectedCausticSpeeds,
                    to.ProjectedCausticSpeeds,
                    t));
        }
    }

    /// <summary>
    /// Provides the authored calm-to-storm realistic-water profile ladder.
    /// </summary>
    public static class RealisticWaterWeatherProfiles
    {
        private static readonly RealisticWaterWeatherProfile Calm = new(
            0.12f,
            new Vector4(25f, 18f, 0.08f, 0.45f),
            new Vector4(95f, 10f, 0.045f, 0.65f),
            new Vector4(200f, 5f, 0.02f, 0.8f),
            new Vector4(320f, 2.8f, 0.01f, 1f),
            new Vector4(0.16f, 0.12f, 0.08f, 0.05f),
            new Vector2(0.006f, 0.012f),
            0.08f,
            0.012f,
            0.08f,
            1.2f,
            0.55f,
            0.08f,
            0.12f,
            0.95f,
            0.18f,
            0.95f,
            new Vector2(0.18f, 0.25f));

        private static readonly RealisticWaterWeatherProfile Breeze = new(
            0.35f,
            new Vector4(25f, 15f, 0.2f, 0.8f),
            new Vector4(95f, 8.5f, 0.11f, 1.05f),
            new Vector4(200f, 4.5f, 0.055f, 1.35f),
            new Vector4(320f, 2.3f, 0.025f, 1.75f),
            new Vector4(0.34f, 0.28f, 0.2f, 0.14f),
            new Vector2(0.016f, 0.03f),
            0.16f,
            0.02f,
            0.1f,
            2.8f,
            0.28f,
            0.45f,
            0.3f,
            0.85f,
            0.28f,
            0.85f,
            new Vector2(0.28f, 0.4f));

        private static readonly RealisticWaterWeatherProfile Windy = new(
            0.55f,
            new Vector4(25f, 14f, 0.35f, 1f),
            new Vector4(95f, 8f, 0.2f, 1.4f),
            new Vector4(200f, 4.5f, 0.1f, 1.8f),
            new Vector4(320f, 2.2f, 0.05f, 2.4f),
            new Vector4(0.5f, 0.4f, 0.3f, 0.25f),
            new Vector2(0.025f, 0.045f),
            0.2f,
            0.025f,
            0.13f,
            4f,
            0.12f,
            1f,
            0.4f,
            0.6f,
            0.38f,
            0.6f,
            new Vector2(0.38f, 0.6f));

        private static readonly RealisticWaterWeatherProfile Storm = new(
            0.75f,
            new Vector4(25f, 18f, 0.68f, 1.3f),
            new Vector4(95f, 10f, 0.38f, 1.65f),
            new Vector4(200f, 5.5f, 0.19f, 2.1f),
            new Vector4(320f, 2.7f, 0.09f, 2.7f),
            new Vector4(0.72f, 0.6f, 0.48f, 0.38f),
            new Vector2(0.06f, 0.095f),
            0.44f,
            0.04f,
            0.21f,
            7.5f,
            0.045f,
            1.45f,
            0.68f,
            0.12f,
            0.52f,
            0.12f,
            new Vector2(0.52f, 0.9f));

        /// <summary>
        /// Returns the (wavelength, amplitude, steepness) scale this weather state represents,
        /// measured against the Windy state the wave profile assets are authored at. A bound
        /// WaveProfile replaces the four legacy waves, so this is what keeps calm-to-storm
        /// changing the sea instead of only the foam and the micro normals.
        /// </summary>
        public static Vector3 GetBankScale(RealisticWaterWeatherProfile profile)
        {
            return new Vector3(
                Ratio(MeanWavelength(profile), MeanWavelength(Windy)),
                Ratio(TotalAmplitude(profile), TotalAmplitude(Windy)),
                Ratio(MeanSteepness(profile), MeanSteepness(Windy)));
        }

        private static float MeanWavelength(RealisticWaterWeatherProfile profile)
        {
            return (profile.Wave1Params.y + profile.Wave2Params.y +
                profile.Wave3Params.y + profile.Wave4Params.y) * 0.25f;
        }

        private static float TotalAmplitude(RealisticWaterWeatherProfile profile)
        {
            return profile.Wave1Params.z + profile.Wave2Params.z +
                profile.Wave3Params.z + profile.Wave4Params.z;
        }

        private static float MeanSteepness(RealisticWaterWeatherProfile profile)
        {
            return (profile.WaveSteepness.x + profile.WaveSteepness.y +
                profile.WaveSteepness.z + profile.WaveSteepness.w) * 0.25f;
        }

        private static float Ratio(float value, float reference)
        {
            return reference > 0.0001f ? value / reference : 1f;
        }

        /// <summary>
        /// Returns the coordinated shader profile for a supported weather state.
        /// </summary>
        public static RealisticWaterWeatherProfile Get(
            RealisticWaterWeather weather)
        {
            return weather switch
            {
                RealisticWaterWeather.Calm => Calm,
                RealisticWaterWeather.Breeze => Breeze,
                RealisticWaterWeather.Windy => Windy,
                RealisticWaterWeather.Storm => Storm,
                _ => Breeze,
            };
        }
    }
}
