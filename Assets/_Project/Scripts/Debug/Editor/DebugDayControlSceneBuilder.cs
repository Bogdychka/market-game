using Market.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Editor helper for placing temporary D2/D5 debug interaction cubes in the Market scene.
    /// </summary>
    public static class DebugDayControlSceneBuilder
    {
        private const string MarketSceneName = "Market";
        private const string OpenCloseName = "Debug_OpenCloseMarket_Cube";
        private const string SleepName = "Debug_SleepNextDay_Cube";
        private const string OpenCloseMaterialPath = "Assets/_Project/Art/Materials/DebugMarketOpenControl.mat";
        private const string SleepMaterialPath = "Assets/_Project/Art/Materials/DebugSleepControl.mat";

        [MenuItem("Market/Debug/Create Day Control Cubes")]
        public static void CreateDayControlCubes()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != MarketSceneName)
            {
                Debug.LogError("[DebugDayControlSceneBuilder] Open the Market scene before creating controls.");
                return;
            }

            CreateControl<DebugMarketOpenInteractable>(
                OpenCloseName,
                new Vector3(3f, 0.6f, -1.5f),
                OpenCloseMaterialPath);

            CreateControl<DebugSleepInteractable>(
                SleepName,
                new Vector3(-1.4f, 0.6f, -1.5f),
                SleepMaterialPath);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[DebugDayControlSceneBuilder] Created day control debug cubes.");
        }

        private static void CreateControl<T>(string objectName, Vector3 position, string materialPath)
            where T : Component
        {
            GameObject existing = FindRootObject(objectName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            go.AddComponent<T>();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null && go.TryGetComponent(out MeshRenderer renderer))
                renderer.sharedMaterial = material;

            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
        }

        private static GameObject FindRootObject(string objectName)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == objectName)
                    return roots[i];
            }

            return null;
        }
    }
}
