using Market.Player;
using Market.UI;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Builds the water settings wall and the crosshair rig in the open lab scene: a physical
    /// panel the player walks up to and operates by aiming, instead of a screen-space window that
    /// has to steal the cursor.
    /// Menu: Market/Debug/Water/Build Water Settings Wall. Re-running updates the existing wall.
    /// </summary>
    public static class WaterSettingsWallBuilder
    {
        private const string WallRootName = "Water Settings Wall";
        private const string PointerRootName = "Player Gaze Pointer";
        private const string WallMaterialPath =
            "Assets/_Project/Art/WaterShaderLab/LabSettingsWall.mat";
        private const string ProfileFolder = "Assets/_Project/Art/Materials/Water/Profiles";

        // Sized and hung so the whole panel sits between the deck floor and comfortable eye
        // height: the first build put its footer below the boards, where the deck ate it.
        private static readonly Vector2 PanelSize = new(3.0f, 2.0f);
        private const float PixelsPerMetre = 340f;
        private const float PanelHeightAboveSpawn = 1.05f;
        private const float PanelDistanceFromSpawn = 3.1f;

        /// <summary>Creates or refreshes the settings wall in the open scene.</summary>
        [MenuItem("Market/Debug/Water/Build Water Settings Wall")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject water = GameObject.Find("Water");
            if (water == null)
            {
                Debug.LogError(
                    "[WaterSettingsWallBuilder] No 'Water' object in the open scene - " +
                    "open WaterShaderLab first.");
                return;
            }

            GameObject wall = BuildWall(water);
            BuildPointerRig();
            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = wall;
            Debug.Log(
                $"[WaterSettingsWallBuilder] Settings wall ready in '{scene.name}' at " +
                $"{wall.transform.position}.");
        }

        private static GameObject BuildWall(GameObject water)
        {
            GameObject root = GameObject.Find(WallRootName) ?? new GameObject(WallRootName);
            PlaceWall(root.transform);
            BuildBackboard(root.transform);
            ConfigureWallComponent(root, water);
            return root;
        }

        /// <summary>
        /// Puts the wall beside the player spawn, turned to face it: a settings panel the player
        /// has to hunt for is a panel nobody uses, and one placed in front of the spawn would
        /// stand between the camera and the water it is meant to tune.
        /// </summary>
        private static void PlaceWall(Transform wall)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null)
            {
                wall.position = new Vector3(-3.4f, 3.5f, -42f);
                wall.rotation = Quaternion.Euler(0f, 250f, 0f);
                return;
            }

            Transform spawn = player.transform;
            Vector3 anchor = spawn.position -
                spawn.right * PanelDistanceFromSpawn +
                Vector3.up * PanelHeightAboveSpawn;
            wall.position = anchor;

            Vector3 toPlayer = spawn.position + Vector3.up * PanelHeightAboveSpawn - anchor;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                // Away from the player, not toward: a world-space canvas is read from its -Z
                // side, so pointing its forward at the reader shows the text mirrored.
                wall.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            }
        }

        private static void BuildBackboard(Transform wall)
        {
            Transform existing = wall.Find("Backboard");
            GameObject backboard = existing != null
                ? existing.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            backboard.name = "Backboard";
            backboard.transform.SetParent(wall, false);
            // The reader stands on the canvas's -Z side (see PlaceWall), so the board goes at +Z
            // to sit behind the panel instead of in front of it.
            backboard.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            backboard.transform.localRotation = Quaternion.identity;
            backboard.transform.localScale = new Vector3(
                PanelSize.x + 0.2f, PanelSize.y + 0.2f, 0.1f);

            // The panel is the interactive surface; a collider on the board in front of it would
            // swallow the crosshair ray before it reaches a slider.
            Collider collider = backboard.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            var renderer = backboard.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = LoadOrCreateWallMaterial();
        }

        private static Material LoadOrCreateWallMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader)
            {
                name = "LabSettingsWall",
            };
            material.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.07f, 1f));
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, WallMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ConfigureWallComponent(GameObject root, GameObject water)
        {
            WaterSettingsWall wall = root.GetComponent<WaterSettingsWall>() ??
                root.AddComponent<WaterSettingsWall>();

            var serializedObject = new SerializedObject(wall);
            serializedObject.FindProperty("waterRenderer").objectReferenceValue =
                water.GetComponent<Renderer>();
            serializedObject.FindProperty("waveProfileBinder").objectReferenceValue =
                water.GetComponent<WaveProfileBinder>();
            serializedObject.FindProperty("weatherController").objectReferenceValue =
                water.GetComponent<RealisticWaterWeatherController>();
            serializedObject.FindProperty("qualityController").objectReferenceValue =
                water.GetComponent<RealisticWaterQualityController>();
            serializedObject.FindProperty("panelSize").vector2Value = PanelSize;
            serializedObject.FindProperty("pixelsPerMetre").floatValue = PixelsPerMetre;

            SerializedProperty profiles = serializedObject.FindProperty("waveProfiles");
            string[] profileNames = { "WP_OceanSwell", "WP_LakeChop", "WP_PondRings" };
            // The trailing empty slot is the legacy four-wave material path, so the wall can show
            // what the water looked like before profiles existed.
            profiles.arraySize = profileNames.Length + 1;
            for (int i = 0; i < profileNames.Length; i++)
            {
                profiles.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<WaveProfile>(
                        $"{ProfileFolder}/{profileNames[i]}.asset");
            }

            profiles.GetArrayElementAtIndex(profileNames.Length).objectReferenceValue = null;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPointerRig()
        {
            GameObject root = GameObject.Find(PointerRootName) ?? new GameObject(PointerRootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            CrosshairView crosshair = root.GetComponent<CrosshairView>() ??
                root.AddComponent<CrosshairView>();
            GazeUiPointer pointer = root.GetComponent<GazeUiPointer>() ??
                root.AddComponent<GazeUiPointer>();

            var serializedObject = new SerializedObject(pointer);
            serializedObject.FindProperty("crosshair").objectReferenceValue = crosshair;
            // sourceCamera stays empty on purpose: the lab camera is a runtime child of the player
            // prefab, so the pointer resolves Camera.main at Awake instead of storing a stale ref.
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
                return;

            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
