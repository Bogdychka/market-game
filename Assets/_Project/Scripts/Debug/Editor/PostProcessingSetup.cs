using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Creates the project-owned post-processing profile and drops a global Volume into the open
    /// scene. The game camera renders post processing (Player.prefab), but a camera with no Volume
    /// in range still tonemaps nothing - so every playable scene needs one of these.
    /// Values target the cozy market look: Neutral tonemapping (ACES would desaturate the cartoon
    /// palette), restrained bloom, a warm white balance, and a slight contrast/saturation lift.
    /// Re-runnable: an existing profile is reused untouched so hand-tuned values survive.
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public static class PostProcessingSetup
    {
        private const string ProfileDir = "Assets/_Project/Art/PostProcessing";
        private const string ProfilePath = ProfileDir + "/MarketPostFX.asset";
        private const string VolumeObjectName = "Global Post Processing";

        [MenuItem("Market/Debug/Rendering/Setup Post Processing In Open Scene")]
        public static void SetupOpenScene()
        {
            VolumeProfile profile = EnsureProfile();
            Scene scene = SceneManager.GetActiveScene();

            GameObject volumeObject = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == VolumeObjectName)
                {
                    volumeObject = root;
                    break;
                }
            }

            if (volumeObject == null)
            {
                volumeObject = new GameObject(VolumeObjectName);
                Undo.RegisterCreatedObjectUndo(volumeObject, "Add global post processing");
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
                volume = volumeObject.AddComponent<Volume>();

            volume.isGlobal = true;
            // Above the default 0 so this profile deterministically wins over any stray global
            // volume shipped inside an imported package (Island still carries the Bitgem demo one).
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"PostProcessingSetup: '{VolumeObjectName}' in '{scene.name}' now uses " +
                $"'{ProfilePath}'.");
        }

        /// <summary>
        /// Returns the project post-processing profile, creating it with the cozy defaults on
        /// first use. An existing asset is returned as-is so tuning is never overwritten.
        /// </summary>
        private static VolumeProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(ProfileDir);
            AssetDatabase.Refresh();

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            AddTonemapping(profile);
            AddColorAdjustments(profile);
            AddWhiteBalance(profile);
            AddBloom(profile);
            AddVignette(profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath);
            Debug.Log($"PostProcessingSetup: created '{ProfilePath}'.");
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        /// <summary>
        /// Adds one override and persists it as a sub-asset. <see cref="VolumeProfile.Add{T}"/>
        /// only fills the in-memory component list; without
        /// <see cref="AssetDatabase.AddObjectToAsset(Object, Object)"/> the saved profile keeps
        /// five null entries and the volume does nothing.
        /// </summary>
        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInHierarchy;
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // Neutral, not ACES: ACES crushes saturation and reads filmic/desaturated, which fights a
        // bright cartoon palette. Comparable cozy games (Stardew-likes, Animal-Crossing-likes) keep
        // a neutral-to-lifted curve.
        private static void AddTonemapping(VolumeProfile profile)
        {
            var tonemapping = AddOverride<Tonemapping>(profile);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        private static void AddColorAdjustments(VolumeProfile profile)
        {
            var color = AddOverride<ColorAdjustments>(profile);
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.1f;
            color.contrast.overrideState = true;
            color.contrast.value = 8f;
            color.saturation.overrideState = true;
            color.saturation.value = 8f;
        }

        private static void AddWhiteBalance(VolumeProfile profile)
        {
            var whiteBalance = AddOverride<WhiteBalance>(profile);
            whiteBalance.temperature.overrideState = true;
            whiteBalance.temperature.value = 8f;
            whiteBalance.tint.overrideState = true;
            whiteBalance.tint.value = 2f;
        }

        // Threshold above 1 so only genuinely bright things bloom (sun glint, foam, lamps) instead
        // of washing every lit surface.
        private static void AddBloom(VolumeProfile profile)
        {
            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.1f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = true;
        }

        private static void AddVignette(VolumeProfile profile)
        {
            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;
        }
    }
}
