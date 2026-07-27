using System.Collections.Generic;
using Market.DebugTools;
using Market.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds a standalone lab scene for the BOXOPHOBIC "Skybox Cubemap Extended" pack: the water
    /// setup from WaterShaderLab (copied, so both labs stay in sync with the same water material)
    /// under a blended day/night cubemap sky, plus the in-game <see cref="SkyboxRuntimeTuner"/>
    /// panel on F8. Rebuilding overwrites the scene, so keep manual tweaks in the sky material.
    /// </summary>
    public static class SkyboxLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/SkyboxLab.unity";
        private const string WaterLabScenePath = "Assets/_Project/Scenes/WaterShaderLab.unity";
        private const string SkyboxFolder = "Assets/_Project/Art/Materials/Skybox";
        private const string SkyboxMaterialPath = SkyboxFolder + "/M_SkyboxLab.mat";
        private const string SkyboxBackupMaterialPath =
            SkyboxFolder + "/M_SkyboxLab_AuthorDefaults.mat";
        private const string BlendShaderName = "Skybox/Cubemap Blend";
        private const string DaySkyPath =
            "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/Polyverse Skies - Blue Sky.png";
        private const string NightSkyPath =
            "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/Polyverse Skies - Night Sky.exr";
        private const string HdSkyPath = "Assets/BOXOPHOBIC/Utils/Settings/HD Sky.exr";
        private const string TunerObjectName = "Skybox Lab Tuner";
        private const string WaterLabelName = "Label - Water Shader Lab";
        private const string LabelName = "Label - Skybox Lab";

        /// <summary>Rebuilds and opens the standalone SkyboxLab scene.</summary>
        [MenuItem("Market/Debug/Build Skybox Lab")]
        public static void BuildSkyboxLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WaterLabScenePath) == null)
            {
                Debug.LogError(
                    $"[SkyboxLabSceneBuilder] {WaterLabScenePath} is missing - run " +
                    "'Market/Debug/Build Water Shader Lab' first.");
                return;
            }

            Material sky = EnsureSkyboxMaterial();
            if (sky == null)
                return;

            // Deleting the scene asset while it is open confuses the Editor, so park on the source.
            if (SceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.OpenScene(WaterLabScenePath, OpenSceneMode.Single);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(WaterLabScenePath, ScenePath))
            {
                Debug.LogError($"[SkyboxLabSceneBuilder] Could not copy {WaterLabScenePath}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Light sun = ApplyEnvironment(scene, sky);
            BuildLabel(scene);
            BuildTuner(scene, sky, sun);
            PostProcessingSetup.SetupOpenScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[SkyboxLabSceneBuilder] Built {ScenePath}. Enter play mode and press F8 for the " +
                "sky panel, F4 to fly.");
        }

        /// <summary>
        /// Creates or refreshes the lab sky material. Kept as a project asset so the runtime panel
        /// writes tuned values straight into version control instead of into the imported package.
        /// </summary>
        [MenuItem("Market/Debug/Rendering/Create Skybox Lab Material")]
        public static Material EnsureSkyboxMaterial()
        {
            Shader shader = Shader.Find(BlendShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[SkyboxLabSceneBuilder] Shader '{BlendShaderName}' not found - is the " +
                    "BOXOPHOBIC 'Skybox Cubemap Extended' package imported?");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(SkyboxFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Skybox");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_SkyboxLab" };
                AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
                ApplyMaterialDefaults(material);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                ApplyMaterialDefaults(material);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Overwrites the lab sky material with the tuned late-afternoon look. The untouched
        /// as-imported values live in <see cref="SkyboxBackupMaterialPath"/>.
        /// </summary>
        [MenuItem("Market/Debug/Rendering/Reset Skybox Lab Material")]
        public static void ResetSkyboxMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (material == null)
            {
                Debug.LogError($"[SkyboxLabSceneBuilder] {SkyboxMaterialPath} is missing.");
                return;
            }

            ApplyMaterialDefaults(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyboxLabSceneBuilder] Reset the sky material to the tuned defaults.");
        }

        /// <summary>
        /// Restores the sky material from the backup taken before the look was tuned.
        /// </summary>
        [MenuItem("Market/Debug/Rendering/Restore Skybox Lab Author Material")]
        public static void RestoreAuthorSkyboxMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            Material backup = AssetDatabase.LoadAssetAtPath<Material>(SkyboxBackupMaterialPath);
            if (material == null || backup == null)
            {
                Debug.LogError(
                    $"[SkyboxLabSceneBuilder] Need both {SkyboxMaterialPath} and " +
                    $"{SkyboxBackupMaterialPath} to restore.");
                return;
            }

            material.CopyPropertiesFromMaterial(backup);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[SkyboxLabSceneBuilder] Restored the sky material from " +
                $"{SkyboxBackupMaterialPath}.");
        }

        /// <summary>
        /// Tuned look: neutral-warm stylized day sky, slow cloud drift, and a soft haze band that
        /// hides the hard horizon line of the cubemap where it meets the water plane.
        /// </summary>
        private static void ApplyMaterialDefaults(Material material)
        {
            material.SetTexture("_Tex", LoadSky(DaySkyPath));
            material.SetTexture("_Tex_Blend", LoadSky(NightSkyPath));
            material.SetFloat("_CubemapTransition", 0f);
            // 1.1 clipped the already bright horizon band under Neutral tonemapping.
            material.SetFloat("_Exposure", 0.95f);
            // 0.5 per channel is neutral for this [Gamma] tint - this is a slight warm lift.
            material.SetColor("_TintColor", new Color(0.53f, 0.51f, 0.47f, 1f));
            material.SetFloat("_Rotation", 35f);
            // Stylized clouds are sparse; a slow drift reads as wind, 0.4 read as a spinning sky.
            material.SetFloat("_RotationSpeed", 0.12f);
            material.SetFloat("_EnableRotation", 1f);
            material.EnableKeyword("_ENABLEROTATION_ON");
            material.SetFloat("_EnableFog", 1f);
            material.EnableKeyword("_ENABLEFOG_ON");
            material.SetFloat("_FogIntensity", 0.85f);
            material.SetFloat("_FogHeight", 0.22f);
            material.SetFloat("_FogSmoothness", 0.4f);
            material.SetFloat("_FogFill", 0.3f);
        }

        private static Cubemap LoadSky(string path)
        {
            Cubemap cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cubemap == null)
                Debug.LogWarning($"[SkyboxLabSceneBuilder] Cubemap missing at {path}.");
            return cubemap;
        }

        private static Cubemap[] LoadSkies()
        {
            var skies = new List<Cubemap>(3);
            foreach (string path in new[] { DaySkyPath, NightSkyPath, HdSkyPath })
            {
                Cubemap cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (cubemap != null)
                    skies.Add(cubemap);
            }

            return skies.ToArray();
        }

        private static Light ApplyEnvironment(Scene scene, Material sky)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            // A bright stylized sky at full ambient flattens everything; 0.85 keeps shape.
            RenderSettings.ambientIntensity = 0.85f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            // Scene fog stays off: UnderwaterFogController owns RenderSettings fog when submerged.
            // This colour only feeds the sky shader's own height fog.
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.78f, 1f);

            GameObject sunObject = FindRoot(scene, "Sun");
            Light sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
            if (sun != null)
            {
                RenderSettings.sun = sun;
                // Low sun in front of the player start (z = -42, facing +Z): the specular path
                // runs towards the camera, which is what makes the water read as water.
                sun.transform.rotation = Quaternion.Euler(22f, 150f, 0f);
                // Skybox ambient now carries part of the fill that the flat ambient used to add.
                sun.intensity = 1.15f;
            }

            DynamicGI.UpdateEnvironment();
            return sun;
        }

        private static void BuildLabel(Scene scene)
        {
            GameObject label = FindRoot(scene, WaterLabelName) ?? FindRoot(scene, LabelName);
            if (label == null)
                return;

            label.name = LabelName;
            for (int index = label.transform.childCount - 1; index >= 0; index--)
                Object.DestroyImmediate(label.transform.GetChild(index).gameObject);

            CreateTextLine(label.transform, "SKYBOX LAB", 0f, 0.10f, Color.white);
            CreateTextLine(
                label.transform,
                "Skybox Cubemap Blend over the WaterShaderLab water",
                -0.6f,
                0.045f,
                new Color(0.85f, 0.9f, 0.95f));
            CreateTextLine(
                label.transform,
                "F8 sky panel  |  F4 fly  |  Space / Left Ctrl up-down while flying",
                -1.2f,
                0.045f,
                new Color(0.85f, 0.9f, 0.95f));
        }

        private static void CreateTextLine(
            Transform parent, string value, float localY, float characterSize, Color color)
        {
            GameObject line = new($"Line - {value}");
            line.transform.SetParent(parent, false);
            line.transform.localPosition = new Vector3(0f, localY, 0f);
            TextMesh text = line.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static void BuildTuner(Scene scene, Material sky, Light sun)
        {
            GameObject tunerObject = FindRoot(scene, TunerObjectName) ?? new GameObject(TunerObjectName);
            SkyboxRuntimeTuner tuner = tunerObject.GetComponent<SkyboxRuntimeTuner>() ??
                                       tunerObject.AddComponent<SkyboxRuntimeTuner>();

            FirstPersonController controller = FindPlayerController(scene);
            var serializedObject = new SerializedObject(tuner);
            serializedObject.FindProperty("_skyboxMaterial").objectReferenceValue = sky;
            serializedObject.FindProperty("_sun").objectReferenceValue = sun;
            serializedObject.FindProperty("_playerController").objectReferenceValue = controller;
            serializedObject.FindProperty("_startOpen").boolValue = true;

            Cubemap[] skies = LoadSkies();
            SerializedProperty skyList = serializedObject.FindProperty("_skies");
            skyList.arraySize = skies.Length;
            for (int index = 0; index < skies.Length; index++)
                skyList.GetArrayElementAtIndex(index).objectReferenceValue = skies[index];

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FirstPersonController FindPlayerController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                FirstPersonController controller =
                    root.GetComponentInChildren<FirstPersonController>(true);
                if (controller != null)
                    return controller;
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;
            }

            return null;
        }
    }
}
