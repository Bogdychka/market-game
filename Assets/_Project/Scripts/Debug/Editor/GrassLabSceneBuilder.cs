using System.Collections.Generic;
using Market.Interaction;
using Market.Player;
using Market.UI;
using Market.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds GrassLab.unity: a small terrain to paint on with <see cref="GrassScatterBrush"/>, a
    /// reference row holding one of every built card (single and X-cross side by side), a banded
    /// 1.8 m post to judge clump height against, and a dirt path - grass reads very differently
    /// where it meets bare ground, and that edge is what the brush is usually asked to sell.
    /// Re-runnable: the scene is rebuilt from scratch, the terrain asset is reused in place.
    /// </summary>
    public static class GrassLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/GrassLab.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Art/Prefabs/Player/Player.prefab";
        private const string PostProcessingProfilePath =
            "Assets/_Project/Art/PostProcessing/MarketPostFX.asset";
        private const string TerrainDataPath =
            "Assets/_Project/Art/Terrain/GrassLab_TerrainData.asset";
        private const string GrassLayerPath = "Assets/_Project/Art/Terrain/Layers/Grass.asset";
        private const string DirtLayerPath = "Assets/_Project/Art/Terrain/Layers/Dirt.asset";
        private const string GeneratedFolder = "Assets/_Project/Art/GrassLab";

        private const float TerrainSize = 100f;
        private const float TerrainHeight = 12f;
        private const int HeightmapResolution = 257;
        private const int AlphamapResolution = 256;

        // The reference row sits north of the spawn, on the flat middle, in the player's first view.
        private const float RowSpacing = 3f;
        private const float SingleRowZ = 14f;
        private const float CrossRowZ = 17f;
        private const float ScaleReferenceHeight = 1.8f;

        [MenuItem("Market/Debug/Build Grass Lab")]
        public static void BuildGrassLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(GeneratedFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GrassLab";

            Terrain terrain = BuildTerrain();
            BuildLighting();
            BuildWind();
            BuildPostProcessing();
            BuildScaleReference(terrain);
            int cards = BuildReferenceRow(terrain);
            BuildLabel(terrain);
            BuildPlayer(terrain);
            GrassLabVisualUpgrade.Build(terrain);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GrassLabSceneBuilder] Built {ScenePath} with {cards} reference card(s). " +
                      "Open Market/Debug/Grass Scatter Brush, press 'Reload grass cards', tick " +
                      "'Enable Painting' and drag over the terrain in the Scene view.");
        }

        private static Terrain BuildTerrain()
        {
            var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
            var dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DirtLayerPath);
            if (grass == null || dirt == null)
                Debug.LogWarning("[GrassLabSceneBuilder] Terrain layers are missing; the ground will render untextured.");

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightmapResolution;
            data.alphamapResolution = AlphamapResolution;
            data.baseMapResolution = 512;
            data.SetDetailResolution(512, 32);
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            data.terrainLayers = grass != null && dirt != null
                ? new[] { grass, dirt }
                : data.terrainLayers;
            SculptGround(data);
            if (grass != null && dirt != null)
                PaintPath(data);
            EditorUtility.SetDirty(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "GrassLab Terrain";
            terrainObject.transform.position =
                new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);

            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            // The lab is small and always viewed up close, so trade LOD savings for an accurate
            // surface: painted clumps sit on the collider, and a popping heightmap floats them.
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 200f;
            terrain.allowAutoConnect = false;
            return terrain;
        }

        /// <summary>
        /// A flat plateau in the middle so the first strokes land on level ground, gently rolling
        /// ground around it, and one clean hillside on +X to check Align To Slope and the random
        /// lean against - grass that looks right on the flat often skates on a slope.
        /// </summary>
        private static void SculptGround(TerrainData data)
        {
            int resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];
            float step = TerrainSize / (resolution - 1);

            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                float z = zIndex * step - TerrainSize * 0.5f;
                for (int xIndex = 0; xIndex < resolution; xIndex++)
                {
                    float x = xIndex * step - TerrainSize * 0.5f;
                    float openGround = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 20f) / 10f);
                    float rolling = Mathf.Sin(x * 0.09f) * Mathf.Cos(z * 0.11f) * 0.5f +
                                    Mathf.Sin(x * 0.21f + 1.3f) * Mathf.Sin(z * 0.17f) * 0.2f;
                    float hillside = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - 22f) / 20f));
                    heights[zIndex, xIndex] = 0.25f + openGround * rolling * 0.08f + hillside * 0.5f;
                }
            }

            data.SetHeights(0, 0, heights);
        }

        /// <summary>Solid grass with one winding dirt path, kept off to the west of the work area.</summary>
        private static void PaintPath(TerrainData data)
        {
            int resolution = data.alphamapResolution;
            var weights = new float[resolution, resolution, 2];
            float step = TerrainSize / resolution;

            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                float z = zIndex * step - TerrainSize * 0.5f;
                float pathCenterX = -15f + Mathf.Sin(z * 0.06f) * 6f;
                for (int xIndex = 0; xIndex < resolution; xIndex++)
                {
                    float x = xIndex * step - TerrainSize * 0.5f;
                    float dirt = 1f - Mathf.SmoothStep(1.6f, 3.4f, Mathf.Abs(x - pathCenterX));
                    weights[zIndex, xIndex, 0] = 1f - dirt;
                    weights[zIndex, xIndex, 1] = dirt;
                }
            }

            data.SetAlphamaps(0, 0, weights);
        }

        /// <summary>
        /// One of every built card, singles in front and their X-cross twins behind, so the two
        /// flavours can be compared from the same angle before deciding on a Cross Chance.
        /// </summary>
        private static int BuildReferenceRow(Terrain terrain)
        {
            List<GameObject> singles = GrassCardBuilder.LoadPalettePrefabs(false);
            List<GameObject> crosses = GrassCardBuilder.LoadPalettePrefabs(true);
            if (singles.Count == 0)
            {
                Debug.LogWarning("[GrassLabSceneBuilder] No grass cards built yet - run " +
                                 "Market/Debug/Grass Card/2. Build Material + Clump Prefab, then rebuild the lab.");
                return 0;
            }

            var root = new GameObject("Card Reference Row");
            float startX = -(singles.Count - 1) * RowSpacing * 0.5f;
            int placed = 0;

            for (int index = 0; index < singles.Count; index++)
            {
                float x = startX + index * RowSpacing;
                placed += PlaceCard(root.transform, terrain, singles[index], x, SingleRowZ);
                if (index < crosses.Count)
                    placed += PlaceCard(root.transform, terrain, crosses[index], x, CrossRowZ);

                CreateLabel(
                    root.transform,
                    VariantLabel(singles[index]),
                    Ground(terrain, x, SingleRowZ - 1.6f) + Vector3.up * 0.12f,
                    0.045f,
                    new Color(0.95f, 0.95f, 0.8f));
            }

            CreateLabel(root.transform, "SINGLE CARD",
                Ground(terrain, startX - RowSpacing, SingleRowZ) + Vector3.up * 0.35f, 0.05f, Color.white);
            CreateLabel(root.transform, "X-CROSS",
                Ground(terrain, startX - RowSpacing, CrossRowZ) + Vector3.up * 0.35f, 0.05f, Color.white);
            return placed;
        }

        private static int PlaceCard(Transform parent, Terrain terrain, GameObject prefab, float x, float z)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
                return 0;

            instance.transform.position = Ground(terrain, x, z);
            return 1;
        }

        /// <summary>"GrassCard_6_Clump" -> "6.1", matching the source PNG the card was painted from.</summary>
        private static string VariantLabel(GameObject prefab)
        {
            string id = prefab.name.Replace("GrassCard", string.Empty)
                .Replace("_Clump", string.Empty)
                .Trim('_');
            return string.IsNullOrEmpty(id) ? "3.1" : id + ".1";
        }

        /// <summary>
        /// A post banded every 30 cm up to eye height. Grass height is the setting that goes wrong
        /// most easily, and it can only be judged against something of a known size.
        /// </summary>
        private static void BuildScaleReference(Terrain terrain)
        {
            var root = new GameObject("Scale Reference 1.8m");
            root.transform.position = Ground(terrain, 12f, SingleRowZ);

            Material light = GetOrCreateMaterial("LabPostLight", new Color(0.86f, 0.86f, 0.82f));
            Material dark = GetOrCreateMaterial("LabPostDark", new Color(0.16f, 0.17f, 0.19f));

            const float bandHeight = 0.3f;
            int bands = Mathf.RoundToInt(ScaleReferenceHeight / bandHeight);
            for (int index = 0; index < bands; index++)
            {
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = $"Band {index * bandHeight:0.0}m";
                band.transform.SetParent(root.transform, false);
                band.transform.localPosition = new Vector3(0f, bandHeight * (index + 0.5f), 0f);
                band.transform.localScale = new Vector3(0.12f, bandHeight, 0.12f);
                band.GetComponent<MeshRenderer>().sharedMaterial = index % 2 == 0 ? dark : light;
                Object.DestroyImmediate(band.GetComponent<BoxCollider>());
            }

            CreateLabel(root.transform, "1.8 m", new Vector3(0f, ScaleReferenceHeight + 0.25f, 0f),
                0.05f, Color.white, local: true);
        }

        private static void BuildLabel(Terrain terrain)
        {
            var root = new GameObject("Label - Grass Lab");
            root.transform.position = Ground(terrain, 0f, 6f) + Vector3.up * 1.6f;
            CreateLabel(root.transform, "GRASS LAB", Vector3.zero, 0.09f, Color.white, local: true);
            CreateLabel(root.transform, "MARKET / DEBUG / GRASS SCATTER BRUSH", new Vector3(0f, -0.28f, 0f),
                0.035f, new Color(0.55f, 0.9f, 0.6f), local: true);
            CreateLabel(root.transform, "DRAG = PAINT   SHIFT+DRAG = ERASE", new Vector3(0f, -0.48f, 0f),
                0.035f, new Color(0.55f, 0.9f, 0.6f), local: true);
        }

        private static void BuildLighting()
        {
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            ApplyDaylight(sun);
        }

        /// <summary>
        /// Re-applies the lab's neutral daylight to the scene that is already open, without
        /// rebuilding it - the lab fills up with thousands of painted clumps, and those must not be
        /// thrown away to change a light.
        /// </summary>
        [MenuItem("Market/Debug/Grass Lab/Reset Lighting To Daylight")]
        public static void ResetLightingToDaylight()
        {
            Scene scene = SceneManager.GetActiveScene();
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light candidate in Object.FindObjectsByType<Light>())
                {
                    if (candidate.type != LightType.Directional)
                        continue;

                    sun = candidate;
                    break;
                }
            }

            if (sun == null)
            {
                Debug.LogError("[GrassLabSceneBuilder] No directional light in the open scene.");
                return;
            }

            ApplyDaylight(sun);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[GrassLabSceneBuilder] Reset '{scene.name}' to neutral daylight.");
        }

        /// <summary>
        /// Plain midday sun under Unity's procedural sky. Deliberately NOT the project's
        /// M_SkyboxLab: that material is tuned live by the skybox lab and is usually parked on some
        /// mood - dusk, night - and grass colour judged under a coloured sky is judged wrong. Mood
        /// belongs in the real scenes; a lab needs a neutral reference light.
        /// </summary>
        private static void ApplyDaylight(Light sun)
        {
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            // Low enough that cards throw a readable shadow: grass with no contact shadow floats.
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.26f, 0.2f);
            RenderSettings.ambientIntensity = 1f;
        }

        /// <summary>
        /// The scene's single wind, plus the trample feed. Both write shader globals, so one of
        /// each is all the lab needs no matter how much grass gets painted into it.
        /// </summary>
        private static void BuildWind()
        {
            var windObject = new GameObject("Grass Wind");
            windObject.AddComponent<GrassWindController>();
            windObject.AddComponent<GrassInteractionSystem>();
        }

        private static void BuildPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[GrassLabSceneBuilder] Missing {PostProcessingProfilePath}; the lab will render untonemapped.");
                return;
            }

            var volumeObject = new GameObject("Global Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static void BuildPlayer(Terrain terrain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GrassLabSceneBuilder] Player prefab is missing at {PlayerPrefabPath}; " +
                                 "the lab is Scene-view only.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
            // The prefab root sits at the character's feet. Adding half a controller height here
            // raises the camera to nearly 3 m and makes every grass clump look toy-sized.
            player.transform.position = Ground(terrain, 0f, -6f) + Vector3.up * 0.03f;

            var uiModeObject = new GameObject("UI Mode Service");
            UIModeService uiMode = uiModeObject.AddComponent<UIModeService>();
            SetObjectReference(uiMode, "playerController", player.GetComponent<FirstPersonController>());
            SetObjectReference(uiMode, "interactionSystem", player.GetComponent<InteractionSystem>());
        }

        private static Vector3 Ground(Terrain terrain, float x, float z)
        {
            var position = new Vector3(x, 0f, z);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static void CreateLabel(
            Transform parent,
            string value,
            Vector3 position,
            float characterSize,
            Color color,
            bool local = false)
        {
            var line = new GameObject($"Label - {value}");
            line.transform.SetParent(parent, false);
            if (local)
                line.transform.localPosition = position;
            else
                line.transform.position = position;

            TextMesh text = line.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
