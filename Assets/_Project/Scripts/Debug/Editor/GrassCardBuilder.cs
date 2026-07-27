using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Turns the artist's single alpha-cutout quad (Grass_3.fbx + Grass_3.1.png) into usable grass:
    /// fixes the texture import, builds a GrassWind material set up for cards, bakes the clump mesh
    /// (see <see cref="CardsPerClump"/>), and scatters a visible patch onto the island terrain.
    /// Re-runnable and idempotent.
    /// </summary>
    public static class GrassCardBuilder
    {
        private const string CardModelPath = "Assets/blender/Grass_3.fbx";
        private const string OutputFolder = "Assets/_Project/Art/Nature/Grass";
        private const string LegacyCrossMeshPath = OutputFolder + "/GrassCard_Cross.asset";
        private const string ShaderName = "Market/Nature/GrassWind";
        private const string ContainerName = "GrassCards";

        /// <summary>One painted grass texture and the assets built from it.</summary>
        private sealed class CardVariant
        {
            public string TexturePath;
            public string MeshPath;
            public string MaterialPath;
            public string PrefabPath;
        }

        // All variants share the Grass_3 quad as geometry; each texture is cropped to its own
        // artwork, so cards keep the proportions of the painting they came from.
        private static readonly CardVariant[] Variants =
        {
            new CardVariant
            {
                TexturePath = "Assets/blender/Grass_3.1.png",
                MeshPath = OutputFolder + "/GrassCard_Mesh.asset",
                MaterialPath = OutputFolder + "/GrassCard.mat",
                PrefabPath = OutputFolder + "/GrassCard_Clump.prefab"
            },
            new CardVariant
            {
                TexturePath = "Assets/blender/Grass_4.1.png",
                MeshPath = OutputFolder + "/GrassCard_4_Mesh.asset",
                MaterialPath = OutputFolder + "/GrassCard_4.mat",
                PrefabPath = OutputFolder + "/GrassCard_4_Clump.prefab"
            },
            new CardVariant
            {
                TexturePath = "Assets/blender/Grass_5.1.png",
                MeshPath = OutputFolder + "/GrassCard_5_Mesh.asset",
                MaterialPath = OutputFolder + "/GrassCard_5.mat",
                PrefabPath = OutputFolder + "/GrassCard_5_Clump.prefab"
            },
        };

        private const float AlphaCutoff = 0.35f;
        // Cards per clump, spread evenly over 180 deg (the shader is Cull Off, so a half turn already
        // covers every orientation). 1 = a single quad: cheapest, and it goes edge-on invisible from
        // some angles - fine here, density from the scatter brush hides it. Raise to 2 for the usual
        // X-cross if a clump ever has to read solid on its own.
        private const int CardsPerClump = 1;
        private const int PatchInstances = 400;
        private const float PatchRadius = 6f;

        [MenuItem("Market/Debug/Grass Card/1. Inspect Source Card")]
        public static void InspectSourceCard()
        {
            Mesh mesh = LoadSourceMesh();
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Color[] colors = mesh.colors;

            var report = new System.Text.StringBuilder();
            report.AppendLine($"[GrassCardBuilder] '{mesh.name}': {vertices.Length} verts, " +
                              $"{mesh.triangles.Length / 3} tris, bounds center {mesh.bounds.center} size {mesh.bounds.size}");
            report.AppendLine($"  uv set: {(uvs.Length == vertices.Length ? "present" : "MISSING")}, " +
                              $"vertex colors: {(colors.Length == vertices.Length ? "present" : "none")}");

            for (int i = 0; i < vertices.Length; i++)
            {
                string uv = uvs.Length == vertices.Length ? uvs[i].ToString("F3") : "-";
                string color = colors.Length == vertices.Length ? colors[i].ToString("F2") : "-";
                report.AppendLine($"  v{i}: pos {vertices[i].ToString("F4")}  uv {uv}  color {color}");
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Market/Debug/Grass Card/2. Build Material + Clump Prefab")]
        public static void BuildCardAssets()
        {
            Mesh sourceMesh = LoadSourceMesh();
            if (sourceMesh == null)
                return;

            EnsureFolder(OutputFolder);
            AssetDatabase.DeleteAsset(LegacyCrossMeshPath);

            int built = 0;
            foreach (CardVariant variant in Variants)
            {
                if (BuildVariant(sourceMesh, variant))
                    built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[GrassCardBuilder] Built {built} grass card variant(s) in {OutputFolder} " +
                      $"({CardsPerClump} card(s) per clump).");
        }

        private static bool BuildVariant(Mesh sourceMesh, CardVariant variant)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(variant.TexturePath) == null)
            {
                Debug.LogWarning($"[GrassCardBuilder] {variant.TexturePath} is missing; skipped.");
                return false;
            }

            ConfigureTextureImport(variant.TexturePath);
            Rect artBounds = MeasureAlphaBounds(variant.TexturePath);
            Mesh clumpMesh = BuildClumpMesh(sourceMesh, artBounds, variant.MeshPath);
            Material material = BuildMaterial(artBounds, variant);
            BuildClumpPrefab(clumpMesh, material, variant.PrefabPath);
            return material != null;
        }

        [MenuItem("Market/Debug/Grass Card/3. Scatter Patch In Scene")]
        public static void ScatterPatch()
        {
            List<GameObject> prefabs = LoadVariantPrefabs();
            if (prefabs.Count == 0)
            {
                Debug.LogError($"[GrassCardBuilder] No clump prefabs in {OutputFolder} - run step 2 first.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[GrassCardBuilder] No active Terrain in the open scene.");
                return;
            }

            Transform container = GetOrCreateContainer();
            for (int i = container.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);

            Vector3 center = PickPatchCenter(terrain);
            int placed = 0;
            for (int i = 0; i < PatchInstances; i++)
            {
                Vector2 offset = Random.insideUnitCircle * PatchRadius;
                var samplePos = new Vector3(center.x + offset.x, 0f, center.z + offset.y);
                samplePos.y = terrain.SampleHeight(samplePos) + terrain.transform.position.y;

                GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                instance.transform.SetPositionAndRotation(
                    samplePos,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                instance.transform.localScale = Vector3.one * Random.Range(0.7f, 1.25f);
                Undo.RegisterCreatedObjectUndo(instance, "Scatter Grass Cards");
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
            FrameSceneView(center);
            Debug.Log($"[GrassCardBuilder] Scattered {placed} clump(s) around {center} under '{ContainerName}'.");
        }

        /// <summary>
        /// Renders the patch from eye height with a throwaway camera, so the result can be inspected
        /// without entering Play Mode (the island scene has no camera of its own).
        /// </summary>
        [MenuItem("Market/Debug/Grass Card/4. Capture Patch Preview")]
        public static void CapturePatchPreview()
        {
            Terrain terrain = Terrain.activeTerrain;
            GameObject container = GameObject.Find(ContainerName);
            if (terrain == null || container == null || container.transform.childCount == 0)
            {
                Debug.LogError("[GrassCardBuilder] No scattered patch to capture - run step 3 first.");
                return;
            }

            Vector3 center = PickPatchCenter(terrain);
            var camHolder = new GameObject("GrassCardPreviewCam");
            var cam = camHolder.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;

            Vector3 eye = center + new Vector3(0f, 0.45f, -PatchRadius * 0.8f);
            camHolder.transform.position = eye;
            camHolder.transform.LookAt(center + new Vector3(0f, 0.2f, 0f));

            const int width = 1280;
            const int height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                shot.Apply(false);

                string dir = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "Artifacts", "Capture");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "grass_card_patch.png");
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log($"[GrassCardBuilder] Wrote patch preview to {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(camHolder);
            }
        }

        /// <summary>Clump prefabs of every variant that has been built.</summary>
        private static List<GameObject> LoadVariantPrefabs()
        {
            var prefabs = new List<GameObject>();
            foreach (CardVariant variant in Variants)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);
                if (prefab != null)
                    prefabs.Add(prefab);
            }

            return prefabs;
        }

        private static Mesh LoadSourceMesh()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CardModelPath);
            if (model == null)
            {
                Debug.LogError($"[GrassCardBuilder] {CardModelPath} not found.");
                return null;
            }

            var filter = model.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError($"[GrassCardBuilder] {CardModelPath} has no mesh.");
                return null;
            }

            return filter.sharedMesh;
        }

        /// <summary>
        /// Bakes <see cref="CardsPerClump"/> copies of the card, spread evenly around Y, into one
        /// mesh - so a multi-card clump still costs a single renderer instead of one per quad.
        /// The source quad is authored lying flat (root at the origin, tip along +Z, Y flat) like
        /// Grass_1/Grass_2, so each copy is stood upright first: -90 deg around X maps +Z to +Y.
        /// </summary>
        private static Mesh BuildClumpMesh(Mesh sourceMesh, Rect artBounds, string meshPath)
        {
            Mesh quad = BuildCroppedQuad(sourceMesh, artBounds);
            Quaternion standUpright = Quaternion.Euler(-90f, 0f, 0f);
            var combine = new CombineInstance[CardsPerClump];
            for (int i = 0; i < CardsPerClump; i++)
            {
                combine[i].mesh = quad;
                combine[i].transform = Matrix4x4.TRS(
                    Vector3.zero,
                    Quaternion.Euler(0f, i * (180f / CardsPerClump), 0f) * standUpright,
                    Vector3.one);
            }

            var baked = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(meshPath) };
            baked.CombineMeshes(combine, true, true);
            baked.RecalculateBounds();
            Object.DestroyImmediate(quad);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                existing.Clear();
                existing.vertices = baked.vertices;
                existing.normals = baked.normals;
                existing.uv = baked.uv;
                existing.triangles = baked.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(baked, meshPath);
            return baked;
        }

        /// <summary>
        /// Finds the sub-rect of the texture the artwork actually covers, in UV space. The source
        /// PNG only fills part of its square, and the empty remainder would otherwise be shaded as
        /// transparent pixels on every card.
        /// </summary>
        private static Rect MeasureAlphaBounds(string texturePath)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
                return new Rect(0f, 0f, 1f, 1f);

            bool wasReadable = importer.isReadable;
            importer.isReadable = true;
            importer.SaveAndReimport();

            Rect bounds;
            try
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                Color32[] pixels = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;
                byte threshold = (byte)Mathf.RoundToInt(AlphaCutoff * 255f);

                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[row + x].a <= threshold)
                            continue;

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX < 0)
                {
                    Debug.LogWarning($"[GrassCardBuilder] {texturePath} is fully below the cutoff; using the whole texture.");
                    return new Rect(0f, 0f, 1f, 1f);
                }

                // One texel of padding so bilinear filtering never samples across the crop edge.
                float uMin = Mathf.Max(0f, (minX - 1f) / width);
                float vMin = Mathf.Max(0f, (minY - 1f) / height);
                float uMax = Mathf.Min(1f, (maxX + 2f) / width);
                float vMax = Mathf.Min(1f, (maxY + 2f) / height);
                bounds = new Rect(uMin, vMin, uMax - uMin, vMax - vMin);
                Debug.Log($"[GrassCardBuilder] Artwork covers UV {bounds} " +
                          $"({bounds.width * bounds.height * 100f:F0}% of the texture square).");
            }
            finally
            {
                importer.isReadable = wasReadable;
                importer.SaveAndReimport();
            }

            return bounds;
        }

        /// <summary>
        /// Rebuilds the source quad so it spans only <paramref name="artBounds"/> in space while its
        /// own UVs still run a full 0..1 -- the crop rides in _BaseMap_ST instead. That keeps the
        /// UV.y wind mask running root-to-tip across the visible blades rather than across a card
        /// that is half empty.
        /// </summary>
        private static Mesh BuildCroppedQuad(Mesh sourceMesh, Rect artBounds)
        {
            Vector3[] sourceVerts = sourceMesh.vertices;
            Vector2[] sourceUvs = sourceMesh.uv;

            var corners = new Vector3[4];
            var found = new bool[4];
            for (int i = 0; i < sourceVerts.Length; i++)
            {
                int index = (sourceUvs[i].x > 0.5f ? 1 : 0) + (sourceUvs[i].y > 0.5f ? 2 : 0);
                corners[index] = sourceVerts[i];
                found[index] = true;
            }

            foreach (bool corner in found)
            {
                if (corner)
                    continue;

                Debug.LogWarning("[GrassCardBuilder] Source card is not a 0..1 UV quad; using it uncropped.");
                return Object.Instantiate(sourceMesh);
            }

            Vector3 PositionAt(float u, float v) => Vector3.Lerp(
                Vector3.Lerp(corners[0], corners[1], u),
                Vector3.Lerp(corners[2], corners[3], u),
                v);

            var quad = new Mesh { name = "GrassCard_Quad" };
            quad.vertices = new[]
            {
                PositionAt(artBounds.xMin, artBounds.yMin),
                PositionAt(artBounds.xMin, artBounds.yMax),
                PositionAt(artBounds.xMax, artBounds.yMax),
                PositionAt(artBounds.xMax, artBounds.yMin),
            };
            quad.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }

        private static Material BuildMaterial(Rect artBounds, CardVariant variant)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[GrassCardBuilder] Shader '{ShaderName}' not found.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(variant.MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, variant.MaterialPath);
            }

            material.shader = shader;
            material.SetTexture(
                "_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(variant.TexturePath));
            // The mesh was cropped to the artwork; point its full 0..1 UVs at that sub-rect.
            material.SetTextureScale("_BaseMap", artBounds.size);
            material.SetTextureOffset("_BaseMap", artBounds.position);
            material.SetFloat("_Cutoff", AlphaCutoff);
            // The card's own object-space Z extent is 0, so the legacy tip-height mask would freeze it.
            material.SetFloat("_WindMaskFromUV", 1f);
            material.EnableKeyword("_WINDMASK_UV");
            // The quad carries no authored vertex-color tint; let the texture and the tint colors rule.
            material.SetFloat("_VertexColorTint", 0f);
            // The texture already carries the blade colors, so the tints only shade it.
            material.SetColor("_BaseColor", new Color(0.72f, 0.86f, 0.62f, 1f));
            material.SetColor("_TipColor", new Color(1.0f, 1.0f, 0.86f, 1f));
            material.SetFloat("_NormalSoftness", 0.85f);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Translucency", 1.1f);
            material.SetFloat("_RimStrength", 0.35f);
            material.SetFloat("_WindStrength", 0.05f);
            material.SetFloat("_SquashAmount", 0.15f);
            material.SetFloat("_WobbleAmount", 0.03f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildClumpPrefab(Mesh clumpMesh, Material material, string prefabPath)
        {
            if (clumpMesh == null || material == null)
                return;

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = clumpMesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Alpha-cutout foliage needs coverage-preserving mips (otherwise distant grass thins out and
        /// vanishes) and clamped wrap (otherwise the card's edge pixels bleed across the seam).
        /// </summary>
        private static void ConfigureTextureImport(string texturePath)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            {
                Debug.LogError($"[GrassCardBuilder] {texturePath} is not a texture.");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = AlphaCutoff;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        private static Vector3 PickPatchCenter(Terrain terrain)
        {
            GameObject anchor = GameObject.Find("Zone_FarmFields");
            Vector3 center = anchor != null
                ? anchor.transform.position
                : terrain.transform.position + terrain.terrainData.size * 0.5f;

            center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
            return center;
        }

        private static Transform GetOrCreateContainer()
        {
            GameObject existing = GameObject.Find(ContainerName);
            if (existing != null)
                return existing.transform;

            var go = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Create Grass Card Container");
            return go.transform;
        }

        private static void FrameSceneView(Vector3 center)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;

            view.Frame(new Bounds(center, Vector3.one * (PatchRadius * 2.5f)), false);
            view.Repaint();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var segments = new List<string>(folder.Split('/'));
            string current = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
