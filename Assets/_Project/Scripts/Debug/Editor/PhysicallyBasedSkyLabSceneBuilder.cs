using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds the open-sea lab for three vendored packages: jiaozi158's Physically Based Sky
    /// (Packages/com.jiaozi158.unity-physically-based-sky-urp), jiaozi158's Volumetric Clouds
    /// (Assets/VolumetricCloudsURP), and gasgiant's Ocean-URP water (Assets/OceanURP), so the
    /// atmosphere can be judged against the same water OceanURPLab uses.
    ///
    /// All of the renderer, pipeline, sky-profile and water wiring lives in <see cref="SkyOceanLabRig"/>
    /// and is shared with BeachLab; this builder only places this scene's content.
    /// </summary>
    public static class PhysicallyBasedSkyLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/PhysicallyBasedSkyLab.unity";

        private static readonly Vector3 CameraPosition = new(0f, 9f, -45f);
        private static readonly Vector3 CameraEuler = new(6f, 0f, 0f);

        // Low sun ahead of the default camera, same placement OceanUrpLabSceneBuilder uses.
        private static readonly Vector3 SunEuler = new(26f, 165f, 0f);

        /// <summary>
        /// Rebuilds and opens the Physically Based Sky lab scene, creating the renderer and
        /// pipeline wiring it needs on the way.
        /// </summary>
        [MenuItem("Market/Debug/Build Physically Based Sky Lab")]
        public static void BuildPhysicallyBasedSkyLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            int rendererIndex = SkyOceanLabRig.EnsureRig();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PhysicallyBasedSkyLab";

            Camera camera = SkyOceanLabRig.BuildFlyCamera(rendererIndex, CameraPosition, CameraEuler);
            SkyOceanLabRig.BuildSun(SunEuler);
            SkyOceanLabRig.BuildSkyVolume();
            SkyOceanLabRig.BuildOcean(camera.transform);
            SkyOceanLabRig.ConfigureEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PhysicallyBasedSkyLabSceneBuilder] Built {ScenePath}. Enter Play Mode to see " +
                "the sky, clouds and water together.");
        }
    }
}
