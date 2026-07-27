using System.Collections.Generic;
using Market.Core;
using Market.DebugTools;
using Market.Interaction;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot editor cleanup for development-speed friction that lives in serialized project state.
    /// </summary>
    public static class DevelopmentFrictionCleanupTool
    {
        private const string InteractableLayerName = "Interactable";
        private const int InteractableLayer = 6;

        [MenuItem("Market/Debug/Apply Development Friction Cleanup")]
        public static void Apply()
        {
            EnsureInteractableLayer();
            CleanOpenScenes();
            CleanPrefab("Assets/_Project/Art/Prefabs/Player/Player.prefab");
            ApplyAsciiPlayerSettings();
            ForceReserializeCoreAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DevelopmentFrictionCleanup] Applied scene, prefab, layer, and PlayerSettings cleanup.");
        }

        private static void EnsureInteractableLayer()
        {
            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0)
            {
                Debug.LogWarning("[DevelopmentFrictionCleanup] TagManager.asset not found.");
                return;
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(InteractableLayer);
            layer.stringValue = InteractableLayerName;
            tagManager.ApplyModifiedProperties();
        }

        private static void CleanOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                    CleanHierarchy(root);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void CleanPrefab(string prefabPath)
        {
            if (!System.IO.File.Exists(prefabPath))
            {
                Debug.LogWarning($"[DevelopmentFrictionCleanup] Prefab not found: {prefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                CleanHierarchy(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CleanHierarchy(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = transform.gameObject;

                if (go.GetComponentInParent<IInteractable>(true) != null)
                    go.layer = InteractableLayer;

                DisableDebugInput(go);
                ConfigureInteractionMask(go);
                DisableCropInstantGrow(go);
            }
        }

        private static void DisableDebugInput(GameObject go)
        {
            SetEnabled(go.GetComponent<DebugMoneyInput>(), false);
            SetEnabled(go.GetComponent<DebugSupplierBuy>(), false);
            SetEnabled(go.GetComponent<DebugStallPlace>(), false);
            SetEnabled(go.GetComponent<DebugTimeControl>(), false);
            SetEnabled(go.GetComponent<MarketAutoDebugger>(), false);
        }

        private static void ConfigureInteractionMask(GameObject go)
        {
            InteractionSystem interaction = go.GetComponent<InteractionSystem>();
            if (interaction == null) return;

            SerializedObject serialized = new SerializedObject(interaction);
            serialized.FindProperty("layerMask").intValue = 1 << InteractableLayer;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(interaction);
        }

        private static void DisableCropInstantGrow(GameObject go)
        {
            CropPlot cropPlot = go.GetComponent<CropPlot>();
            if (cropPlot == null) return;

            SerializedObject serialized = new SerializedObject(cropPlot);
            serialized.FindProperty("debugInstantGrowOnInteract").boolValue = false;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(cropPlot);
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour == null || behaviour.enabled == enabled) return;

            behaviour.enabled = enabled;
            EditorUtility.SetDirty(behaviour);
        }

        private static void ApplyAsciiPlayerSettings()
        {
            PlayerSettings.productName = "Market Game";
            PlayerSettings.companyName = "BoRoda";
        }

        private static void ForceReserializeCoreAssets()
        {
            AssetDatabase.ForceReserializeAssets(new List<string>
            {
                "ProjectSettings/TagManager.asset",
                "ProjectSettings/ProjectSettings.asset",
                "Assets/_Project/Scenes/Bootstrap.unity",
                "Assets/_Project/Scenes/MainMenu.unity",
                "Assets/_Project/Scenes/Market.unity",
                "Assets/_Project/Art/Prefabs/Player/Player.prefab",
                "Assets/_Project/Art/Prefabs/NPC/NPC_Visitor.prefab"
            }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
        }
    }
}
