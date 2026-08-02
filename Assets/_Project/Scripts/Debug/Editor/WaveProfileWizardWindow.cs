using Market.World;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Procedural editor for wave profiles: pick a profile, drag the seeded parameters, and the
    /// whole layer bank is rebuilt and pushed to the open scene on every change. The settings live
    /// in the profile asset, so a look can always be regenerated from its seed rather than being a
    /// pile of hand-typed layers nobody can reproduce.
    /// Menu: Market/Debug/Water/Wave Creation Wizard.
    /// </summary>
    public sealed class WaveProfileWizardWindow : EditorWindow
    {
        private const string DefaultProfileFolder = "Assets/_Project/Art/Materials/Water/Profiles";

        [SerializeField] private WaveProfile _profile;

        private SerializedObject _serializedProfile;
        private Vector2 _scroll;

        /// <summary>Opens the wizard from the menu.</summary>
        [MenuItem("Market/Debug/Water/Wave Creation Wizard")]
        public static void Open()
        {
            Open(Selection.activeObject as WaveProfile);
        }

        /// <summary>Opens the wizard already editing a profile.</summary>
        public static void Open(WaveProfile profile)
        {
            WaveProfileWizardWindow window = GetWindow<WaveProfileWizardWindow>(
                utility: false, title: "Wave creation wizard", focus: true);
            window.minSize = new Vector2(340f, 420f);
            if (profile != null)
                window.SetProfile(profile);

            window.Show();
        }

        private void OnEnable()
        {
            if (_profile != null)
                SetProfile(_profile);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Wave creation wizard", EditorStyles.boldLabel);
            DrawProfileField();

            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a Wave Profile asset to edit, or create a new one.",
                    MessageType.Info);
                DrawCreateButton();
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            DrawProceduralSettings();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            DrawSummary();

            EditorGUILayout.EndScrollView();
        }

        private void DrawProfileField()
        {
            EditorGUI.BeginChangeCheck();
            var selected = (WaveProfile)EditorGUILayout.ObjectField(
                "Editing", _profile, typeof(WaveProfile), false);
            if (EditorGUI.EndChangeCheck())
                SetProfile(selected);
        }

        private void DrawCreateButton()
        {
            if (!GUILayout.Button("Create new wave profile", GUILayout.Height(24f)))
                return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Wave Profile",
                "WaveProfile",
                "asset",
                "Where should the wave profile be stored?",
                DefaultProfileFolder);

            if (string.IsNullOrEmpty(path))
                return;

            WaveProfile created = CreateInstance<WaveProfile>();
            created.RegenerateLayers();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            SetProfile(created);
            Selection.activeObject = created;
        }

        private void DrawProceduralSettings()
        {
            if (_serializedProfile == null)
                return;

            _serializedProfile.Update();
            SerializedProperty generation = _serializedProfile.FindProperty("_generation");
            if (generation == null)
                return;

            EditorGUILayout.LabelField("Procedural settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(
                    generation.FindPropertyRelative("_seed"), new GUIContent("Seed"));
                if (GUILayout.Button("Randomize", GUILayout.Width(80f)))
                {
                    generation.FindPropertyRelative("_seed").intValue =
                        Random.Range(int.MinValue, int.MaxValue);
                }
            }

            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_layerCount"), new GUIContent("Num Layers"));

            EditorGUILayout.LabelField("Procedural parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_minMaxWavelength"),
                new GUIContent("Min Max Wave Length"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_amplitudeByLength"),
                new GUIContent("Amplitude By Length"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_minMaxAmplitude"),
                new GUIContent("Min Max Amplitude"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_steepnessByLength"),
                new GUIContent("Steepness By Length"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_minMaxSteepness"),
                new GUIContent("Min Max Steepness"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_baseDirectionAngle"),
                new GUIContent("Base Direction Angle"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_directionAngleVariation"),
                new GUIContent("Direction Angle Variation"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_wavelengthJitter"),
                new GUIContent("Wave Length Jitter"));
            EditorGUILayout.PropertyField(
                generation.FindPropertyRelative("_mode"), new GUIContent("Mode"));

            if (generation.FindPropertyRelative("_mode").enumValueIndex ==
                (int)WaveLayerMode.Circular)
            {
                EditorGUILayout.PropertyField(
                    generation.FindPropertyRelative("_origin"),
                    new GUIContent("Origin (world XZ)"));
            }

            if (EditorGUI.EndChangeCheck())
            {
                _serializedProfile.ApplyModifiedProperties();
                Regenerate();
            }
            else
            {
                _serializedProfile.ApplyModifiedProperties();
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate", GUILayout.Height(24f)))
                    Regenerate();

                if (GUILayout.Button("Select asset", GUILayout.Height(24f)))
                    Selection.activeObject = _profile;
            }
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField("Generated bank", EditorStyles.boldLabel);
            for (int i = 0; i < _profile.Layers.Count; i++)
            {
                WaveLayer layer = _profile.Layers[i];
                if (layer == null)
                    continue;

                EditorGUILayout.LabelField(
                    $"#{i + 1}",
                    $"length {layer.Wavelength:0.00} m, amplitude {layer.Amplitude:0.000} m, " +
                    $"steepness {layer.Steepness:0.00}, direction {layer.DirectionAngle:0} deg");
            }

            EditorGUILayout.HelpBox(
                "The generated bank is the authored layer list - edit it by hand in the profile " +
                "inspector at any time. Regenerating overwrites those edits.",
                MessageType.None);
        }

        private void Regenerate()
        {
            if (_profile == null)
                return;

            Undo.RecordObject(_profile, "Generate Wave Layers");
            _profile.RegenerateLayers();
            EditorUtility.SetDirty(_profile);
            WaveProfileEditorUtility.PushToScene(_profile);
            Repaint();
        }

        private void SetProfile(WaveProfile profile)
        {
            _profile = profile;
            _serializedProfile = profile != null ? new SerializedObject(profile) : null;
        }
    }
}
