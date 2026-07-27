using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Editor window that exposes every property of the Bitgem stylized water shader as a labelled
    /// slider with a short explanation, and stores named presets as JSON next to the water
    /// materials. Edits go straight to the selected material, so the Scene and Game views update
    /// live in both edit and play mode. The property table and the preset format are shared with
    /// the in-game panel (<see cref="StylizedWaterRuntimeTuner"/>).
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public sealed class StylizedWaterTunerWindow : EditorWindow
    {
        private const string MaterialCopyDir = "Assets/_Project/Art/Materials/Water";
        private const string ProjectRoot = "Assets/_Project/";
        private const string WaterObjectName = "Stylized Water Volume";
        private const string UndoLabel = "Stylized Water Tuner";

        private static readonly string[] PackageMaterialPaths =
        {
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-01.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-02.mat",
            "Assets/Bitgem/StylisedWater/URP/Materials/example-water-03.mat",
        };

        private Material _material;
        private Vector2 _scroll;
        private string _presetName = "MyWater";
        private string[] _presetNames = Array.Empty<string>();
        private int _presetIndex;
        private GUIStyle _descriptionStyle;

        /// <summary>Opens the tuner and picks up the water material of the open scene.</summary>
        [MenuItem("Market/Debug/Water/Stylized Water Tuner")]
        public static void Open()
        {
            StylizedWaterTunerWindow window = GetWindow<StylizedWaterTunerWindow>();
            window.titleContent = new GUIContent("Water Tuner");
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_material == null)
                _material = FindSceneWaterMaterial();
            RefreshPresetList();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawMaterialSelector();
            if (_material == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a material that uses the Bitgem stylized water shader.",
                    MessageType.Info);
                return;
            }

            DrawPackageWarning();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (StylizedWaterGroup group in StylizedWaterShaderCatalog.Groups)
                DrawGroup(group);
            DrawPresetSection();
            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (_descriptionStyle != null)
                return;

            _descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            Color color = _descriptionStyle.normal.textColor;
            _descriptionStyle.normal.textColor = new Color(color.r, color.g, color.b, 0.75f);
        }

        // ---- Material ---------------------------------------------------------------------

        private void DrawMaterialSelector()
        {
            EditorGUILayout.Space(4f);
            var picked = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Water material"),
                _material,
                typeof(Material),
                false);
            if (picked != _material)
                _material = picked;

            using (new EditorGUI.DisabledScope(FindSceneWaterMaterial() == null))
            {
                if (GUILayout.Button("Use the water material of the open scene"))
                    _material = FindSceneWaterMaterial();
            }
        }

        private void DrawPackageWarning()
        {
            string path = AssetDatabase.GetAssetPath(_material);
            if (path.StartsWith(ProjectRoot, StringComparison.Ordinal))
                return;

            EditorGUILayout.HelpBox(
                "This material belongs to the imported package and is shared with the package " +
                "showcase scene. Make a project copy before tuning it.",
                MessageType.Warning);
            if (GUILayout.Button("Create a project copy and assign it to the scene water"))
                CreateProjectCopy();
        }

        private void CreateProjectCopy()
        {
            try
            {
                Directory.CreateDirectory(MaterialCopyDir);
                AssetDatabase.Refresh();
                string source = AssetDatabase.GetAssetPath(_material);
                string target = AssetDatabase.GenerateUniqueAssetPath(
                    $"{MaterialCopyDir}/{_material.name}-tuned.mat");
                if (!AssetDatabase.CopyAsset(source, target))
                {
                    Debug.LogError($"StylizedWaterTunerWindow: could not copy '{source}'.");
                    return;
                }

                AssetDatabase.ImportAsset(target);
                _material = AssetDatabase.LoadAssetAtPath<Material>(target);
                AssignToSceneWater(_material);
                Debug.Log($"StylizedWaterTunerWindow: created '{target}'.");
            }
            catch (IOException exception)
            {
                Debug.LogError(
                    $"StylizedWaterTunerWindow: could not create the copy: {exception.Message}");
            }
        }

        private static void AssignToSceneWater(Material material)
        {
            Renderer renderer = FindSceneWaterRenderer();
            if (renderer == null)
                return;

            Undo.RecordObject(renderer, UndoLabel);
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        private static Renderer FindSceneWaterRenderer()
        {
            GameObject water = GameObject.Find(WaterObjectName);
            return water != null ? water.GetComponent<Renderer>() : null;
        }

        private static Material FindSceneWaterMaterial()
        {
            Renderer renderer = FindSceneWaterRenderer();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        // ---- Property fields --------------------------------------------------------------

        private void DrawGroup(StylizedWaterGroup group)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
            foreach (StylizedWaterField field in group.Fields)
                DrawField(field);
        }

        private void DrawField(StylizedWaterField field)
        {
            if (!_material.HasProperty(field.Property))
                return;

            EditorGUI.BeginChangeCheck();
            object value = DrawFieldValue(field);
            if (EditorGUI.EndChangeCheck())
                ApplyValue(field, value);

            EditorGUILayout.LabelField(field.Description, _descriptionStyle);
            EditorGUILayout.Space(2f);
        }

        private object DrawFieldValue(StylizedWaterField field)
        {
            var label = new GUIContent(field.Label, field.Description);
            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    return EditorGUILayout.ColorField(
                        label,
                        _material.GetColor(field.Property),
                        true,
                        true,
                        true);
                case StylizedWaterFieldKind.Texture:
                    return EditorGUILayout.ObjectField(
                        label,
                        _material.GetTexture(field.Property),
                        typeof(Texture),
                        false);
                case StylizedWaterFieldKind.Tiling:
                    Vector4 stored = _material.GetVector(field.Property);
                    Vector2 tiling = EditorGUILayout.Vector2Field(
                        label,
                        new Vector2(stored.x, stored.y));
                    return new Vector4(tiling.x, tiling.y, stored.z, stored.w);
                default:
                    return EditorGUILayout.Slider(
                        label,
                        _material.GetFloat(field.Property),
                        field.Min,
                        field.Max);
            }
        }

        private void ApplyValue(StylizedWaterField field, object value)
        {
            Undo.RecordObject(_material, UndoLabel);
            switch (field.Kind)
            {
                case StylizedWaterFieldKind.Color:
                    _material.SetColor(field.Property, (Color)value);
                    break;
                case StylizedWaterFieldKind.Texture:
                    _material.SetTexture(field.Property, (Texture)value);
                    break;
                case StylizedWaterFieldKind.Tiling:
                    _material.SetVector(field.Property, (Vector4)value);
                    break;
                default:
                    _material.SetFloat(field.Property, (float)value);
                    break;
            }

            EditorUtility.SetDirty(_material);
            SceneView.RepaintAll();
        }

        // ---- Presets ----------------------------------------------------------------------

        private void DrawPresetSection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"JSON files under {StylizedWaterPresets.EditorAssetFolder}, shared with the " +
                "in-game tuner panel (F7).",
                _descriptionStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                _presetName = EditorGUILayout.TextField("Preset name", _presetName);
                if (GUILayout.Button("Save", GUILayout.Width(60f)))
                    SavePreset(_presetName);
            }

            using (new EditorGUI.DisabledScope(_presetNames.Length == 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                _presetIndex = EditorGUILayout.Popup("Saved presets", _presetIndex, _presetNames);
                if (GUILayout.Button("Load", GUILayout.Width(60f)))
                    LoadPreset(_presetNames[_presetIndex]);
            }

            DrawPackageValueButtons();
        }

        private void DrawPackageValueButtons()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Copy every value from one of the three package materials.",
                _descriptionStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int index = 0; index < PackageMaterialPaths.Length; index++)
                {
                    if (!GUILayout.Button($"Water {index + 1:00}"))
                        continue;

                    var source =
                        AssetDatabase.LoadAssetAtPath<Material>(PackageMaterialPaths[index]);
                    if (source != null)
                        CopyValues(source, _material);
                }
            }
        }

        private void CopyValues(Material source, Material target)
        {
            Undo.RecordObject(target, UndoLabel);
            StylizedWaterPresets.Apply(StylizedWaterPresets.Capture(source), target);
            Texture normalMap = source.GetTexture(StylizedWaterShaderCatalog.NormalMapProperty);
            target.SetTexture(StylizedWaterShaderCatalog.NormalMapProperty, normalMap);
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void SavePreset(string presetName)
        {
            Texture normalMap = _material.GetTexture(StylizedWaterShaderCatalog.NormalMapProperty);
            if (!StylizedWaterPresets.Save(presetName, _material, GuidOf(normalMap)))
                return;

            AssetDatabase.Refresh();
            RefreshPresetList();
        }

        private void LoadPreset(string presetName)
        {
            StylizedWaterPreset preset = StylizedWaterPresets.Load(presetName);
            if (preset == null)
                return;

            Undo.RecordObject(_material, UndoLabel);
            StylizedWaterPresets.Apply(preset, _material);
            ApplyPresetTexture(preset.normalMapGuid);
            EditorUtility.SetDirty(_material);
            SceneView.RepaintAll();
        }

        private void ApplyPresetTexture(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture != null &&
                _material.HasProperty(StylizedWaterShaderCatalog.NormalMapProperty))
            {
                _material.SetTexture(StylizedWaterShaderCatalog.NormalMapProperty, texture);
            }
        }

        private void RefreshPresetList()
        {
            _presetNames = StylizedWaterPresets.List();
            _presetIndex = _presetNames.Length == 0
                ? 0
                : Mathf.Clamp(_presetIndex, 0, _presetNames.Length - 1);
        }

        private static string GuidOf(Texture texture)
        {
            if (texture == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}
