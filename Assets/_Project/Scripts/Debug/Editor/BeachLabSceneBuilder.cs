using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Builds the beach lab: the Physically Based Sky lab's exact atmosphere and Ocean-URP water
    /// (see <see cref="SkyOceanLabRig"/>), but with a Unity Terrain shore instead of open sea, so
    /// the water can be judged where it actually gets judged in game - against sand, in shallows,
    /// with a waterline.
    ///
    /// The terrain is generated, not hand-sculpted: the whole point is a shore profile whose
    /// numbers are readable and repeatable (seabed depth, approach slope, sandbar, berm, dunes),
    /// so a rebuild always produces the same beach. Sea level is world Y = 0, which is where the
    /// ocean sits, and the terrain is placed so that its normalized height <see cref="WaterLevel01"/>
    /// lands there.
    ///
    /// The water is the sky lab's water unchanged, sea state included - the shore is the variable
    /// here, not the sea. Two consequences worth knowing before reading anything into a capture:
    /// the shared wind blows along +X, which is along this beach rather than into it, so the swell
    /// meets the sand at 45 degrees (turn it with OceanLabController: , . for wind, ; ' for swell);
    /// and Ocean-URP is a deep-water spectrum, so waves never shoal, break or run up the sand.
    /// Depth-based transparency and the sandbar are what make the shallows read.
    /// </summary>
    public static class BeachLabSceneBuilder
    {
        private const int Seed = 20260810;

        private const string ScenePath = "Assets/_Project/Scenes/BeachLab.unity";
        private const string TerrainDir = "Assets/_Project/Art/Terrain/BeachLab";
        private const string TerrainDataPath = TerrainDir + "/BeachLab_TerrainData.asset";

        // World units. One square kilometre of shore: far enough offshore that the water reaches
        // its deep colour before the terrain ends, far enough inland that the dunes close the view.
        private const float TerrainSize = 1024f;
        private const float TerrainHeight = 96f;
        private const int HeightmapRes = 513;   // must be 2^n + 1; 2 m per sample
        private const int AlphamapRes = 512;

        // Ground palette, from the Handpainted Grass & Ground Textures pack. The pack ships every
        // tile in four pre-rotated copies (_up/_right/_down/_left = 0/90/180/270 degrees) because a
        // hand-painted tile shows its repeat badly; we take two of the four per material and let
        // the splat swap between them in patches, so no orientation covers a whole view.
        private const string PackTextures =
            "Assets/Handpainted_Grass_and_Ground_Textures/Textures";
        private const string RotationA = "up";      // 0 degrees
        private const string RotationB = "right";   // 90 degrees

        // Rotating the second copy hides the repeat's orientation; scaling it as well moves its
        // repeat period off the first one, so the two never line up into a visible grid. Irrational
        // enough that the periods do not resynchronise inside the 1 km terrain.
        private const float RotationBTileScale = 1.41f;

        /// <summary>One paintable ground material: a pack tile plus how it sits on the terrain.</summary>
        private readonly struct BeachMaterial
        {
            public readonly string Name;      // layer name, minus the BeachLab_ prefix and rotation
            public readonly string Tile;      // pack path, minus the rotation suffix and extension
            public readonly float TileSize;   // world metres per texture repeat
            public readonly float Smoothness;

            public BeachMaterial(string name, string tile, float tileSize, float smoothness)
            {
                Name = name;
                Tile = tile;
                TileSize = tileSize;
                Smoothness = smoothness;
            }
        }

        // A value ramp - dark seabed, damp mid brown, pale dry sand, muted dune green - all four
        // picked out of the same pack so the beach reads as painted by one hand. Order is the splat
        // order, and each entry becomes two layers (rotation A, then rotation B).
        private static readonly BeachMaterial[] Materials =
        {
            new("SeabedSand", "Dirt/dirt_claydarked/dirt_claydarked", 14f, 0.20f),
            new("WetSand", "Dirt/dirt_desatured/dirt_desatured", 12f, 0.60f),
            new("DrySand", "Dirt/dirt_lighted/dirt_lighted", 12f, 0.05f),
            new("DuneGrass", "Grass/Grass_desatured/Grass_desatured", 9f, 0.10f)
        };

        private const int LayerCount = 8;   // Materials.Length * 2 rotations

        // Written by the pre-rotation version of this lab; deleted on rebuild so the folder does not
        // keep four orphaned layers and their generated noise textures.
        private static readonly string[] LegacyAssetNames =
        {
            "BeachLab_SeabedSand", "BeachLab_WetSand", "BeachLab_DrySand", "BeachLab_DuneGrass"
        };

        // Normalized sea level, so world Y 0 (where the ocean is) sits here on the terrain.
        private const float WaterLevel01 = 0.4f;

        private static readonly Vector3 TerrainOrigin =
            new(-TerrainSize * 0.5f, -WaterLevel01 * TerrainHeight, -TerrainSize * 0.5f);

        // Shore profile, all in world metres relative to the waterline (Z grows inland).
        private const float SeabedDepth = -34f;
        private const float ApproachStart = -460f;   // where the flat deep seabed starts to climb
        private const float SandbarCentre = -120f;
        private const float SandbarWidth = 55f;
        private const float SandbarHeight = 2.4f;
        private const float BermEnd = 55f;           // flat wet-to-dry beach ends here
        private const float BermHeight = 3.2f;
        private const float DuneEnd = 200f;
        private const float DuneHeight = 13f;
        private const float InlandEnd = 520f;
        private const float InlandHeight = 12f;

        // Offshore in the shallows, looking shoreward: the sun sits ahead of this camera, so the
        // glitter path runs from the horizon into the shallows - the one view that shows sky,
        // water, waterline and sand at once. Close in (70 m, not 170) because the whole beach is
        // only ~15 m tall: from further out it collapses into a dark line under the horizon.
        // The sun being shoreward also means the sand is backlit from every seaward view; fly
        // round to the dunes and look along the beach for a lit one.
        private static readonly Vector3 CameraPosition = new(0f, 6f, -70f);
        private static readonly Vector3 CameraEuler = new(3f, 0f, 0f);
        private static readonly Vector3 SunEuler = new(26f, 165f, 0f);

        /// <summary>
        /// Rebuilds and opens the beach lab scene, regenerating its terrain data on the way.
        /// </summary>
        [MenuItem("Market/Debug/Build Beach Lab")]
        public static void BuildBeachLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Before the empty scene is opened, not after: a missing texture pack should leave the
            // editor on the scene it was on, not on a half-built lab.
            if (!PrepareGroundTextures())
                return;

            int rendererIndex = SkyOceanLabRig.EnsureRig();
            EnsureTerrainFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BeachLab";

            Camera camera = SkyOceanLabRig.BuildFlyCamera(rendererIndex, CameraPosition, CameraEuler);
            SkyOceanLabRig.BuildSun(SunEuler);
            SkyOceanLabRig.BuildSkyVolume();
            BuildTerrain();
            SkyOceanLabRig.BuildOcean(camera.transform);
            SkyOceanLabRig.ConfigureEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BeachLabSceneBuilder] Built {ScenePath}. Enter Play Mode and fly with RMB + " +
                "WASD; the waterline is at Z = 0, land is +Z. The eight ground layers are paint " +
                "brushes - select the terrain, Paint Texture, and the R0/R90 pairs are the same " +
                "tile at 0 and 90 degrees.");
        }

        // ---- Terrain -----------------------------------------------------------------------

        private static void BuildTerrain()
        {
            TerrainData data = BuildTerrainData();
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Beach Terrain";
            terrainObject.isStatic = true;
            terrainObject.transform.position = TerrainOrigin;

            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 600f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            // The sky feature drives ambient and reflections through RenderSettings, and the lab
            // has no reflection probes to blend against.
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.enableHeightmapRayTracing = false;
            EditorUtility.SetDirty(terrain);
        }

        private static TerrainData BuildTerrainData()
        {
            // Deleted rather than reused: the heights and splat are fully regenerated, and an
            // in-place rewrite of an open TerrainData leaves stale detail/tree data behind.
            AssetDatabase.DeleteAsset(TerrainDataPath);

            // Created as an asset before anything is written into it. A TerrainData filled in
            // memory and saved afterwards loses its alphamap - CreateAsset serialises the layer
            // list but the splat comes back as "all weight on layer 0", which paints the whole
            // shore in the first layer and is invisible if the layers are all much of a muchness.
            var data = new TerrainData { name = "BeachLab_TerrainData" };
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            data.heightmapResolution = HeightmapRes;
            data.alphamapResolution = AlphamapRes;
            data.baseMapResolution = 512;
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            data.SetDetailResolution(256, 16);

            data.SetHeights(0, 0, GenerateHeights());
            data.terrainLayers = BuildTerrainLayers();
            data.SetAlphamaps(0, 0, GenerateSplat());
            data.SetBaseMapDirty();   // the basemap is what the terrain shows past basemapDistance

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static float[,] GenerateHeights()
        {
            var heights = new float[HeightmapRes, HeightmapRes];
            for (int z = 0; z < HeightmapRes; z++)
            {
                float worldZ = TerrainOrigin.z + z / (float)(HeightmapRes - 1) * TerrainSize;
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float worldX = TerrainOrigin.x + x / (float)(HeightmapRes - 1) * TerrainSize;
                    heights[z, x] = Mathf.Clamp01(
                        (SampleWorldHeight(worldX, worldZ) - TerrainOrigin.y) / TerrainHeight);
                }
            }

            return heights;
        }

        /// <summary>
        /// The shore profile in world metres. Reading it top to bottom is reading the beach:
        /// deep seabed, approach slope, a sandbar, the waterline at Z = 0, a flat berm, dunes,
        /// then inland. Everything is a function of distance from the (meandering) waterline, so
        /// the beach is a real cross-section rather than noise that happens to cross Y = 0.
        /// </summary>
        private static float SampleWorldHeight(float worldX, float worldZ)
        {
            // Meander the waterline along X so it is not a drawn-with-a-ruler line.
            float z = worldZ - (Perlin(worldX * 0.0018f, 0f, 11) - 0.5f) * 70f;

            float height = Mathf.Lerp(SeabedDepth, 0f, SStep(ApproachStart, 0f, z));

            // A submerged bar: the shallow line offshore that makes the water read as having a
            // bottom instead of one flat gradient.
            float bar = (z - SandbarCentre) / SandbarWidth;
            height += SandbarHeight * Mathf.Exp(-bar * bar) * (1f - SStep(-40f, 0f, z));

            height += SStep(0f, BermEnd, z) * BermHeight;

            float duneNoise = Perlin(worldX * 0.006f, worldZ * 0.006f, 23);
            height += SStep(BermEnd, DuneEnd, z) * DuneHeight * (0.55f + 0.45f * duneNoise);
            height += SStep(DuneEnd, InlandEnd, z) * InlandHeight;

            // Fine relief, kept off the beach face so the waterline stays clean.
            float land = SStep(0f, 45f, z);
            height += (Perlin(worldX * 0.012f, worldZ * 0.012f, 37) - 0.5f) * 3f * land;
            height += (Perlin(worldX * 0.02f, worldZ * 0.02f, 53) - 0.5f) * 1.2f * (1f - land);

            return height;
        }

        private static float[,,] GenerateSplat()
        {
            var splat = new float[AlphamapRes, AlphamapRes, LayerCount];
            for (int z = 0; z < AlphamapRes; z++)
            {
                float worldZ = TerrainOrigin.z + z / (float)(AlphamapRes - 1) * TerrainSize;
                for (int x = 0; x < AlphamapRes; x++)
                {
                    float worldX = TerrainOrigin.x + x / (float)(AlphamapRes - 1) * TerrainSize;
                    float worldY = SampleWorldHeight(worldX, worldZ);

                    // Splat follows height above sea level, not distance along Z, so the sandbar
                    // and the meandering waterline get the right material for free.
                    float seabed = 1f - SStep(-7f, -2.5f, worldY);
                    float aboveBeach = SStep(0.5f, 2.2f, worldY);
                    // Ragged grass line: a straight one reads as a decal on the dunes.
                    float grassNoise = (Perlin(worldX * 0.02f, worldZ * 0.02f, 71) - 0.5f) * 5f;
                    float grass = SStep(6f, 11f, worldY + grassNoise) * aboveBeach;
                    float dry = aboveBeach * (1f - grass);
                    float wet = (1f - seabed) * (1f - aboveBeach);

                    float sum = seabed + wet + dry + grass + 1e-5f;
                    SplitByRotation(splat, z, x, 0, seabed / sum, worldX, worldZ);
                    SplitByRotation(splat, z, x, 1, wet / sum, worldX, worldZ);
                    SplitByRotation(splat, z, x, 2, dry / sum, worldX, worldZ);
                    SplitByRotation(splat, z, x, 3, grass / sum, worldX, worldZ);
                }
            }

            return splat;
        }

        /// <summary>
        /// Hands one material's weight to its two rotation layers. The choice is a low-frequency
        /// mask pushed towards 0 or 1, so the terrain gets patches of one orientation rather than a
        /// half-and-half blend everywhere - blending two rotations of the same painting evenly just
        /// averages back into mush, while patches actually break up the repeat. Each material gets
        /// its own mask seed so their patch borders do not stack into one visible seam.
        /// </summary>
        private static void SplitByRotation(
            float[,,] splat, int z, int x, int material, float weight, float worldX, float worldZ)
        {
            // ~90 m patches: big enough to read as a different stretch of ground, small enough that
            // one orientation never fills a view.
            float mask = Perlin(worldX * 0.011f, worldZ * 0.011f, 101 + material * 7);
            float rotated = SStep(0.44f, 0.56f, mask);

            splat[z, x, material * 2] = weight * (1f - rotated);
            splat[z, x, material * 2 + 1] = weight * rotated;
        }

        /// <summary>
        /// One paintable layer per material per rotation, in splat order. They are ordinary
        /// TerrainLayer assets in <see cref="TerrainDir"/>, so Paint Texture in the Terrain
        /// inspector picks them up as brushes and hand-painting on top of the generated splat
        /// works - a rebuild is what overwrites it, nothing else.
        /// </summary>
        private static TerrainLayer[] BuildTerrainLayers()
        {
            DeleteLegacyAssets();

            var layers = new TerrainLayer[LayerCount];
            for (int i = 0; i < Materials.Length; i++)
            {
                BeachMaterial material = Materials[i];
                layers[i * 2] = MakeLayer(material, RotationA, material.TileSize);
                layers[i * 2 + 1] =
                    MakeLayer(material, RotationB, material.TileSize * RotationBTileScale);
            }

            return layers;
        }

        private static TerrainLayer MakeLayer(BeachMaterial material, string rotation, float tile)
        {
            string name = $"BeachLab_{material.Name}_{RotationSuffix(rotation)}";
            string layerPath = $"{TerrainDir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(layerPath);

            var layer = new TerrainLayer
            {
                diffuseTexture = LoadPackTexture(material, rotation),
                tileSize = new Vector2(tile, tile),
                // Offset the rotated copy by half a tile as well, so its seams never land on the
                // seams of the copy it is blended against.
                tileOffset = rotation == RotationA ? Vector2.zero : new Vector2(tile, tile) * 0.5f,
                smoothness = material.Smoothness,
                metallic = 0f,
                name = name
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static string PackTexturePath(BeachMaterial material, string rotation)
        {
            return $"{PackTextures}/{material.Tile}_{rotation}.png";
        }

        private static Texture2D LoadPackTexture(BeachMaterial material, string rotation)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PackTexturePath(material, rotation));
        }

        // "up" is the pack's unrotated tile; the names say the angle instead, because that is what
        // matters when picking a brush in the terrain inspector.
        private static string RotationSuffix(string rotation)
        {
            return rotation == RotationA ? "R0" : "R90";
        }

        /// <summary>
        /// Checks the pack is present and imports its ground tiles the way a terrain wants them:
        /// anisotropic filtering, which is what keeps sand from smearing at the grazing angles this
        /// lab is looked at from. Returns false (having logged what is missing) if a tile is gone.
        /// </summary>
        private static bool PrepareGroundTextures()
        {
            var missing = new List<string>();
            foreach (BeachMaterial material in Materials)
            {
                foreach (string rotation in new[] { RotationA, RotationB })
                {
                    string path = PackTexturePath(material, rotation);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    {
                        missing.Add(path);
                        continue;
                    }

                    if (importer.anisoLevel >= 4 && importer.wrapMode == TextureWrapMode.Repeat)
                        continue;

                    importer.anisoLevel = 4;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                }
            }

            if (missing.Count == 0) return true;

            Debug.LogError("[BeachLabSceneBuilder] Missing ground textures from the Handpainted " +
                $"Grass & Ground pack, aborting:\n{string.Join("\n", missing)}");
            return false;
        }

        private static void DeleteLegacyAssets()
        {
            foreach (string name in LegacyAssetNames)
            {
                AssetDatabase.DeleteAsset($"{TerrainDir}/{name}.terrainlayer");
                AssetDatabase.DeleteAsset($"{TerrainDir}/{name}_Tex.asset");
            }
        }

        // ---- Helpers ------------------------------------------------------------------------

        /// <summary>
        /// Perlin noise on a seeded offset. Mathf.PerlinNoise is mirror-symmetric about 0 and
        /// returns 0.5 on integer lattice lines, so sampling it near the origin - which a terrain
        /// centred on (0, 0) does - would put a visible seam down the middle of the beach.
        /// </summary>
        private static float Perlin(float x, float y, int seed)
        {
            // Cached because the splat asks for six noise fields per texel across a 512 grid, and
            // seeding a System.Random for each of those is most of the build's runtime.
            if (!PerlinOffsets.TryGetValue(seed, out Vector2 offset))
            {
                var rng = new System.Random(Seed ^ seed);
                offset = new Vector2((float)rng.NextDouble() * 1000f + 100f,
                    (float)rng.NextDouble() * 1000f + 100f);
                PerlinOffsets[seed] = offset;
            }

            return Mathf.PerlinNoise(x + offset.x, y + offset.y);
        }

        private static readonly Dictionary<int, Vector2> PerlinOffsets = new();

        private static float SStep(float edge0, float edge1, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, value));
        }

        private static void EnsureTerrainFolder()
        {
            if (AssetDatabase.IsValidFolder(TerrainDir)) return;
            AssetDatabase.CreateFolder("Assets/_Project/Art/Terrain", "BeachLab");
        }
    }
}
