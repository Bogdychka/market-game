using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>Kind of editor/runtime control a stylized water property needs.</summary>
    public enum StylizedWaterFieldKind
    {
        /// <summary>Single float with a min/max range.</summary>
        Slider,

        /// <summary>RGBA colour.</summary>
        Color,

        /// <summary>Vector whose X and Y are texture tiling.</summary>
        Tiling,

        /// <summary>Texture reference; edit-time only.</summary>
        Texture
    }

    /// <summary>One tunable property of the Bitgem stylized water shader.</summary>
    public sealed class StylizedWaterField
    {
        /// <summary>Shader reference name.</summary>
        public string Property;

        /// <summary>Human readable name shown next to the control.</summary>
        public string Label;

        /// <summary>One line explaining what the property changes.</summary>
        public string Description;

        /// <summary>Control kind to draw.</summary>
        public StylizedWaterFieldKind Kind;

        /// <summary>Lowest useful value for slider kinds.</summary>
        public float Min;

        /// <summary>Highest useful value for slider kinds.</summary>
        public float Max;
    }

    /// <summary>Named group of related water properties.</summary>
    public sealed class StylizedWaterGroup
    {
        /// <summary>Group heading.</summary>
        public string Title;

        /// <summary>Properties in the group.</summary>
        public StylizedWaterField[] Fields;
    }

    /// <summary>
    /// Every exposed property of the Bitgem stylized water shader with a label, a description and
    /// a usable range. Shared by the editor tuner window and the in-game tuner panel so both show
    /// the same list. Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public static class StylizedWaterShaderCatalog
    {
        /// <summary>Reference name of the ripple normal map property.</summary>
        public const string NormalMapProperty = "Texture2D_6490A223";

        /// <summary>All tunable properties, grouped for display.</summary>
        public static readonly StylizedWaterGroup[] Groups =
        {
            new StylizedWaterGroup
            {
                Title = "Colour and depth",
                Fields = new[]
                {
                    new StylizedWaterField
                    {
                        Property = "Color_F01C36BF",
                        Label = "Shallow colour",
                        Description = "Tint of the water where the bottom is close to the surface, " +
                                      "so it drives the colour of the band along the shore.",
                        Kind = StylizedWaterFieldKind.Color
                    },
                    new StylizedWaterField
                    {
                        Property = "Color_7D9A58EC",
                        Label = "Deep colour",
                        Description = "Tint over deep ground. Its alpha decides how much of the " +
                                      "underwater view the deep colour hides.",
                        Kind = StylizedWaterFieldKind.Color
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_17E53C12",
                        Label = "Depth blend distance",
                        Description = "World depth over which shallow fades into deep. Larger " +
                                      "values push the deep colour further from the shore.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 5f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_A0EAD698",
                        Label = "Depth blend curve",
                        Description = "Shape of that fade. Above 1 keeps the shallow tint near the " +
                                      "shore and darkens faster after it.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 5f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_E5C51606",
                        Label = "Depth foam",
                        Description = "Foam generated where the water meets shallow ground, on top " +
                                      "of the painted shoreline foam.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 2f
                    }
                }
            },
            new StylizedWaterGroup
            {
                Title = "Surface ripples",
                Fields = new[]
                {
                    new StylizedWaterField
                    {
                        Property = NormalMapProperty,
                        Label = "Ripple normal map",
                        Description = "Normal texture that creates the small surface ripples.",
                        Kind = StylizedWaterFieldKind.Texture
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector2_37B21477",
                        Label = "Ripple tiling",
                        Description = "How often the ripple texture repeats. Higher numbers give " +
                                      "smaller, denser ripples.",
                        Kind = StylizedWaterFieldKind.Tiling,
                        Min = 0f,
                        Max = 5f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_244B0600",
                        Label = "Ripple scroll speed",
                        Description = "How fast the ripple texture drifts across the water.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 5f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_F38B44AA",
                        Label = "Detail tiling",
                        Description = "Tiling of the second, finer ripple layer that breaks up the " +
                                      "repetition of the first one.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 2f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_46E42935",
                        Label = "Detail strength",
                        Description = "How much of that finer ripple layer is mixed in.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 1f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_B9F56378",
                        Label = "Ripple strength",
                        Description = "Strength of the ripple normals, so how strongly light and " +
                                      "reflections break up on the surface.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 2f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_A6A0BC26",
                        Label = "Refraction",
                        Description = "How far the view under the water is bent. Needs the camera " +
                                      "opaque texture, which the water scenes enable.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 0.2f
                    }
                }
            },
            new StylizedWaterGroup
            {
                Title = "Waves (vertex movement)",
                Fields = new[]
                {
                    new StylizedWaterField
                    {
                        Property = "_WaveFrequency",
                        Label = "Wave frequency",
                        Description = "Waves per world unit. Higher values give shorter, choppier " +
                                      "waves.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 10f
                    },
                    new StylizedWaterField
                    {
                        Property = "_WaveScale",
                        Label = "Wave height",
                        Description = "Vertical size of the waves in world units. The mesh needs " +
                                      "enough vertices to show large values.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 1f
                    },
                    new StylizedWaterField
                    {
                        Property = "_WaveSpeed",
                        Label = "Wave speed",
                        Description = "How fast the waves travel across the surface.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 5f
                    }
                }
            },
            new StylizedWaterGroup
            {
                Title = "Shore foam",
                Fields = new[]
                {
                    new StylizedWaterField
                    {
                        Property = "Vector1_36E8227",
                        Label = "Foam width",
                        Description = "Width of the foam band drawn where the mesh is painted with " +
                                      "red vertex colour, which is the shoreline ring.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 2f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_C360A163",
                        Label = "Foam noise",
                        Description = "Break-up of the foam edge. Zero gives a clean band, higher " +
                                      "values a ragged, bubbly one.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 10f
                    }
                }
            },
            new StylizedWaterGroup
            {
                Title = "Lighting response",
                Fields = new[]
                {
                    new StylizedWaterField
                    {
                        Property = "Vector1_47683D42",
                        Label = "Smoothness",
                        Description = "Higher values give a tighter and brighter sun highlight.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 1f
                    },
                    new StylizedWaterField
                    {
                        Property = "Vector1_3D886DA1",
                        Label = "Metallic",
                        Description = "Metalness of the surface. Water normally stays at zero.",
                        Kind = StylizedWaterFieldKind.Slider,
                        Min = 0f,
                        Max = 1f
                    }
                }
            }
        };
    }

    /// <summary>Serializable float entry of a water preset.</summary>
    [Serializable]
    public sealed class StylizedWaterFloatEntry
    {
        /// <summary>Shader reference name.</summary>
        public string name;

        /// <summary>Stored value.</summary>
        public float value;
    }

    /// <summary>Serializable colour entry of a water preset.</summary>
    [Serializable]
    public sealed class StylizedWaterColorEntry
    {
        /// <summary>Shader reference name.</summary>
        public string name;

        /// <summary>Stored value.</summary>
        public Color value;
    }

    /// <summary>Serializable vector entry of a water preset.</summary>
    [Serializable]
    public sealed class StylizedWaterVectorEntry
    {
        /// <summary>Shader reference name.</summary>
        public string name;

        /// <summary>Stored value.</summary>
        public Vector4 value;
    }

    /// <summary>Saved set of stylized water property values.</summary>
    [Serializable]
    public sealed class StylizedWaterPreset
    {
        /// <summary>Material the preset was captured from.</summary>
        public string sourceMaterial;

        /// <summary>Asset GUID of the ripple normal map, empty when unknown.</summary>
        public string normalMapGuid;

        /// <summary>Float property values.</summary>
        public List<StylizedWaterFloatEntry> floats = new List<StylizedWaterFloatEntry>();

        /// <summary>Colour property values.</summary>
        public List<StylizedWaterColorEntry> colors = new List<StylizedWaterColorEntry>();

        /// <summary>Vector property values.</summary>
        public List<StylizedWaterVectorEntry> vectors = new List<StylizedWaterVectorEntry>();
    }

    /// <summary>
    /// Reads and writes stylized water presets as JSON. In the Editor they live next to the water
    /// materials so they are part of the project; in a build they go to the persistent data path.
    /// </summary>
    public static class StylizedWaterPresets
    {
        /// <summary>Project-relative preset folder used by the Editor tools.</summary>
        public const string EditorAssetFolder = "Assets/_Project/Art/Materials/Water/Presets";

        /// <summary>Absolute folder the presets are read from and written to.</summary>
        public static string Directory =>
            Application.isEditor
                ? Path.Combine(Application.dataPath, "_Project/Art/Materials/Water/Presets")
                : Path.Combine(Application.persistentDataPath, "WaterPresets");

        /// <summary>Names of the presets currently on disk.</summary>
        public static string[] List()
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    return Array.Empty<string>();

                string[] files = System.IO.Directory.GetFiles(Directory, "*.json");
                var names = new string[files.Length];
                for (int index = 0; index < files.Length; index++)
                    names[index] = Path.GetFileNameWithoutExtension(files[index]);
                return names;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WaterPresets] Could not list presets: {exception.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>Reads every catalogued property of the material into a preset.</summary>
        public static StylizedWaterPreset Capture(Material material, string normalMapGuid = null)
        {
            var preset = new StylizedWaterPreset
            {
                sourceMaterial = material != null ? material.name : string.Empty,
                normalMapGuid = normalMapGuid
            };
            if (material == null)
                return preset;

            foreach (StylizedWaterGroup group in StylizedWaterShaderCatalog.Groups)
            {
                foreach (StylizedWaterField field in group.Fields)
                {
                    if (!material.HasProperty(field.Property))
                        continue;

                    CaptureField(material, field, preset);
                }
            }

            return preset;
        }

        private static void CaptureField(
            Material material,
            StylizedWaterField field,
            StylizedWaterPreset preset)
        {
            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    preset.colors.Add(new StylizedWaterColorEntry
                    {
                        name = field.Property,
                        value = material.GetColor(field.Property)
                    });
                    break;
                case StylizedWaterFieldKind.Tiling:
                    preset.vectors.Add(new StylizedWaterVectorEntry
                    {
                        name = field.Property,
                        value = material.GetVector(field.Property)
                    });
                    break;
                case StylizedWaterFieldKind.Texture:
                    break;
                default:
                    preset.floats.Add(new StylizedWaterFloatEntry
                    {
                        name = field.Property,
                        value = material.GetFloat(field.Property)
                    });
                    break;
            }
        }

        /// <summary>Writes every stored value of the preset back onto the material.</summary>
        public static void Apply(StylizedWaterPreset preset, Material material)
        {
            if (preset == null || material == null)
                return;

            foreach (StylizedWaterFloatEntry entry in preset.floats)
                if (material.HasProperty(entry.name))
                    material.SetFloat(entry.name, entry.value);
            foreach (StylizedWaterColorEntry entry in preset.colors)
                if (material.HasProperty(entry.name))
                    material.SetColor(entry.name, entry.value);
            foreach (StylizedWaterVectorEntry entry in preset.vectors)
                if (material.HasProperty(entry.name))
                    material.SetVector(entry.name, entry.value);
        }

        /// <summary>Saves the material values under the given preset name.</summary>
        public static bool Save(string presetName, Material material, string normalMapGuid = null)
        {
            if (string.IsNullOrWhiteSpace(presetName) || material == null)
            {
                Debug.LogError("[WaterPresets] A preset needs a name and a material.");
                return false;
            }

            string path = PathOf(presetName);
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(
                    path,
                    JsonUtility.ToJson(Capture(material, normalMapGuid), true));
                Debug.Log($"[WaterPresets] Saved '{path}'.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WaterPresets] Could not save '{path}': {exception.Message}");
                return false;
            }
        }

        /// <summary>Reads a preset by name, or null when it is missing or unreadable.</summary>
        public static StylizedWaterPreset Load(string presetName)
        {
            string path = PathOf(presetName);
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogError($"[WaterPresets] Preset '{path}' is missing.");
                    return null;
                }

                return JsonUtility.FromJson<StylizedWaterPreset>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WaterPresets] Could not read '{path}': {exception.Message}");
                return null;
            }
        }

        /// <summary>Absolute file path of a preset name.</summary>
        public static string PathOf(string presetName) =>
            Path.Combine(Directory, $"{presetName.Trim()}.json");
    }
}
