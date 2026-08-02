using System.IO;
using Market.World;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Creates the shipped wave profiles so a water surface has something sensible to start from
    /// instead of an empty bank. Each preset is only a seed plus its procedural parameters, so the
    /// assets can be rebuilt at any time - re-running the menu item regenerates them in place.
    /// Menu: Market/Debug/Water/Create Preset Wave Profiles.
    /// </summary>
    public static class WaveProfilePresetBuilder
    {
        private const string ProfileFolder = "Assets/_Project/Art/Materials/Water/Profiles";

        /// <summary>Creates or refreshes every preset wave profile.</summary>
        [MenuItem("Market/Debug/Water/Create Preset Wave Profiles")]
        public static void CreatePresets()
        {
            EnsureFolder();

            // Open water: a long swell carrying progressively shorter chop, fanned wide enough
            // that the surface never reads as one repeating ridge.
            BuildProfile(
                "WP_OceanSwell",
                seed: 24601,
                layerCount: 6,
                minMaxWavelength: new Vector2(2.5f, 22f),
                minMaxAmplitude: new Vector2(0.03f, 0.42f),
                minMaxSteepness: new Vector2(0.22f, 0.58f),
                baseDirectionAngle: 25f,
                directionVariation: 130f,
                mode: WaveLayerMode.Directional,
                origin: Vector2.zero,
                steepnessClamping: 0.95f);

            // Sheltered water: short, low, closely aligned chop with no swell under it.
            BuildProfile(
                "WP_LakeChop",
                seed: 8801,
                layerCount: 5,
                minMaxWavelength: new Vector2(0.9f, 6f),
                minMaxAmplitude: new Vector2(0.008f, 0.075f),
                minMaxSteepness: new Vector2(0.18f, 0.42f),
                baseDirectionAngle: 70f,
                directionVariation: 70f,
                mode: WaveLayerMode.Directional,
                origin: Vector2.zero,
                steepnessClamping: 0.8f);

            // Rings radiating from a point - a spring, a fountain, a small enclosed pond.
            BuildProfile(
                "WP_PondRings",
                seed: 1207,
                layerCount: 3,
                minMaxWavelength: new Vector2(1.2f, 4.5f),
                minMaxAmplitude: new Vector2(0.01f, 0.05f),
                minMaxSteepness: new Vector2(0.15f, 0.35f),
                baseDirectionAngle: 0f,
                directionVariation: 0f,
                mode: WaveLayerMode.Circular,
                origin: Vector2.zero,
                steepnessClamping: 0.7f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WaveProfilePresetBuilder] Preset wave profiles written to {ProfileFolder}");
        }

        private static void BuildProfile(
            string assetName,
            int seed,
            int layerCount,
            Vector2 minMaxWavelength,
            Vector2 minMaxAmplitude,
            Vector2 minMaxSteepness,
            float baseDirectionAngle,
            float directionVariation,
            WaveLayerMode mode,
            Vector2 origin,
            float steepnessClamping)
        {
            string path = $"{ProfileFolder}/{assetName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<WaveProfile>(path);
            bool isNew = profile == null;
            if (isNew)
                profile = ScriptableObject.CreateInstance<WaveProfile>();

            WaveGenerationSettings generation = profile.Generation;
            generation.Seed = seed;
            generation.LayerCount = layerCount;
            generation.MinMaxWavelength = minMaxWavelength;
            generation.MinMaxAmplitude = minMaxAmplitude;
            generation.MinMaxSteepness = minMaxSteepness;
            generation.BaseDirectionAngle = baseDirectionAngle;
            generation.DirectionAngleVariation = directionVariation;
            generation.Mode = mode;
            generation.Origin = origin;
            generation.AmplitudeByLength = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            generation.SteepnessByLength = AnimationCurve.Linear(0f, 0.35f, 1f, 1f);

            profile.SteepnessClamping = steepnessClamping;
            profile.WavelengthMultiplier = 1f;
            profile.AmplitudeMultiplier = 1f;
            profile.SteepnessMultiplier = 1f;
            profile.RegenerateLayers();

            if (isNew)
                AssetDatabase.CreateAsset(profile, path);
            else
                EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(ProfileFolder))
                return;

            string parent = Path.GetDirectoryName(ProfileFolder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(ProfileFolder);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
