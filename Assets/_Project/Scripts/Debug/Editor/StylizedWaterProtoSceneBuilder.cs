using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Creates a project-local showcase scene from the imported Bitgem URP water example.
    /// </summary>
    public static class StylizedWaterProtoSceneBuilder
    {
        private const string SourceScenePath =
            "Assets/Bitgem/StylisedWater/URP/Examples/Example-Scene-01.unity";
        private const string ScenePath =
            "Assets/_Project/Scenes/StylizedWaterProto.unity";
        private const string MaterialFolder =
            "Assets/Bitgem/StylisedWater/URP/Materials";

        private static readonly string[] WaterMaterialPaths =
        {
            MaterialFolder + "/example-water-01.mat",
            MaterialFolder + "/example-water-02.mat",
            MaterialFolder + "/example-water-03.mat",
        };

        /// <summary>
        /// Rebuilds and opens the standalone stylized-water scene.
        /// </summary>
        [MenuItem("Market/Debug/Water/Build Stylized Water Proto Scene")]
        public static void Build()
        {
            try
            {
                ValidatePackageAssets();
                Scene scene = EditorSceneManager.OpenScene(
                    SourceScenePath,
                    OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
                    throw new InvalidOperationException(
                        "Unity could not create the stylized-water scene.");

                RenameShowcaseObjects();
                ClearImportedLayerAssumptions(scene);
                ConfigureWaterRenderer();
                ConfigureCamera();
                AddShowcaseController();
                ConfigureEnvironment();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException(
                        "Unity could not save the stylized-water scene.");

                AssetDatabase.SaveAssets();
                Selection.activeGameObject = Camera.main != null
                    ? Camera.main.gameObject
                    : null;
                Debug.Log(
                    "StylizedWaterProtoSceneBuilder: built StylizedWaterProto.unity.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"StylizedWaterProtoSceneBuilder: build failed: {exception.Message}");
                throw;
            }
        }

        private static void ValidatePackageAssets()
        {
            RequireAsset<SceneAsset>(SourceScenePath);
            foreach (string materialPath in WaterMaterialPaths)
                RequireAsset<Material>(materialPath);
        }

        private static void RenameShowcaseObjects()
        {
            Rename("scene-01", "Bitgem Lagoon Environment");
            Rename("Water", "Stylized Water Volume");
            Rename("Cube", "Floating Reference Cube");
            Rename("PostProcessingVolume", "Stylized Water Post Processing");
        }

        private static void Rename(string currentName, string newName)
        {
            GameObject gameObject = GameObject.Find(currentName);
            if (gameObject != null)
                gameObject.name = newName;
        }

        private static void ClearImportedLayerAssumptions(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                SetLayerRecursively(root.transform, 0);
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int index = 0; index < transform.childCount; index++)
                SetLayerRecursively(transform.GetChild(index), layer);
        }

        private static void ConfigureWaterRenderer()
        {
            GameObject water = GameObject.Find("Stylized Water Volume");
            if (water == null || !water.TryGetComponent(out Renderer renderer))
                throw new InvalidOperationException(
                    "The Bitgem example water renderer was not found.");

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException(
                    "The Bitgem example scene has no Main Camera.");

            camera.gameObject.layer = 0;
            camera.farClipPlane = 250f;
            camera.allowDynamicResolution = false;

            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();
            cameraData.requiresColorTexture = true;
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;
        }

        private static void AddShowcaseController()
        {
            Camera camera = Camera.main;
            GameObject water = GameObject.Find("Stylized Water Volume");
            Renderer waterRenderer = water.GetComponent<Renderer>();

            GameObject focus = new("Stylized Water Camera Focus");
            focus.transform.position = new Vector3(4f, 1.5f, 6f);

            StylizedWaterShowcaseController controller =
                camera.gameObject.GetComponent<StylizedWaterShowcaseController>();
            if (controller == null)
                controller =
                    camera.gameObject.AddComponent<StylizedWaterShowcaseController>();

            Material[] materials = new Material[WaterMaterialPaths.Length];
            for (int index = 0; index < WaterMaterialPaths.Length; index++)
                materials[index] = RequireAsset<Material>(WaterMaterialPaths[index]);

            controller.Configure(focus.transform, waterRenderer, materials);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureEnvironment()
        {
            Light sun = FindNamedComponent<Light>("Sun");
            if (sun != null)
            {
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.7f);

            ReflectionProbe probe = UnityEngine.Object.FindAnyObjectByType<
                ReflectionProbe>();
            if (probe != null)
            {
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            }
        }

        private static T FindNamedComponent<T>(string name)
            where T : Component
        {
            GameObject gameObject = GameObject.Find(name);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static T RequireAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
            return asset;
        }
    }
}
