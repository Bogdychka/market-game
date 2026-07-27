using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>Explicit, confirmation-gated mutations for a selected imported model.</summary>
    public static class AssetPipelineActions
    {
        private const string WrapperFolder = "Assets/_Project/Art/Prefabs/Imported";

        public static bool ApplyStaticImporterPreset(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return false;

            if (!EditorUtility.DisplayDialog(
                    "Apply Static Import Preset",
                    $"Change only the importer for '{path}'? This disables cameras, lights, rig, animation, blend shapes, and Read/Write.",
                    "Apply and Reimport",
                    "Cancel"))
            {
                return false;
            }

            Undo.RecordObject(importer, "Apply static model import preset");
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.SaveAndReimport();
            return true;
        }

        public static GameObject CreateWrapperPrefab(GameObject model, bool addBoxCollider)
        {
            EnsureFolder(WrapperFolder);
            string prefabName = SafeFileName(model.name);
            string prefabPath = $"{WrapperFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("Wrapper Exists", $"The existing prefab was selected:\n{prefabPath}", "OK");
                return existing;
            }

            if (!EditorUtility.DisplayDialog(
                    "Create Wrapper Prefab",
                    $"Create '{prefabPath}'{(addBoxCollider ? " with a BoxCollider" : string.Empty)}? The source model will not be changed.",
                    "Create",
                    "Cancel"))
            {
                return null;
            }

            return BuildWrapper(model, prefabName, prefabPath, addBoxCollider);
        }

        public static void Reimport(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                importer.SaveAndReimport();
        }

        private static GameObject BuildWrapper(
            GameObject model,
            string prefabName,
            string prefabPath,
            bool addBoxCollider)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(prefabName);
                SceneManager.MoveGameObjectToScene(root, preview);
                GameObject child = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
                child.transform.SetParent(root.transform, false);
                if (addBoxCollider)
                    AddBoundsCollider(root, child);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                return prefab;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void AddBoundsCollider(GameObject root, GameObject child)
        {
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SafeFileName(string value)
        {
            string result = value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(result) ? "ImportedModel" : result;
        }
    }
}
