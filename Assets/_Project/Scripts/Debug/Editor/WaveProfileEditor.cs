using Market.World;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Inspector for <see cref="WaveProfile"/>: multipliers with an Apply that bakes them into the
    /// layers, per-index curves, bulk enable/disable, a compact per-layer editor that only shows
    /// the fields the selected mode uses, and the entry point to the procedural wizard.
    /// </summary>
    [CustomEditor(typeof(WaveProfile))]
    public sealed class WaveProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _layers;
        private SerializedProperty _wavelengthMultiplier;
        private SerializedProperty _amplitudeMultiplier;
        private SerializedProperty _steepnessMultiplier;
        private SerializedProperty _wavelengthCurve;
        private SerializedProperty _amplitudeCurve;
        private SerializedProperty _steepnessCurve;
        private SerializedProperty _steepnessClamping;

        private void OnEnable()
        {
            _layers = serializedObject.FindProperty("_layers");
            _wavelengthMultiplier = serializedObject.FindProperty("_wavelengthMultiplier");
            _amplitudeMultiplier = serializedObject.FindProperty("_amplitudeMultiplier");
            _steepnessMultiplier = serializedObject.FindProperty("_steepnessMultiplier");
            _wavelengthCurve = serializedObject.FindProperty("_wavelengthCurve");
            _amplitudeCurve = serializedObject.FindProperty("_amplitudeCurve");
            _steepnessCurve = serializedObject.FindProperty("_steepnessCurve");
            _steepnessClamping = serializedObject.FindProperty("_steepnessClamping");
        }

        /// <summary>Draws the wave profile inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var profile = (WaveProfile)target;

            DrawMultipliers(profile);
            EditorGUILayout.Space();
            DrawCurves();
            EditorGUILayout.Space();
            DrawProceduralEntry(profile);
            EditorGUILayout.Space();
            DrawLayers(profile);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMultipliers(WaveProfile profile)
        {
            EditorGUILayout.LabelField("Multipliers", EditorStyles.boldLabel);
            DrawMultiplierRow(profile, _wavelengthMultiplier, "Wave Length Multiplier", true, false, false);
            DrawMultiplierRow(profile, _amplitudeMultiplier, "Amplitude Multiplier", false, true, false);
            DrawMultiplierRow(profile, _steepnessMultiplier, "Steepness Multiplier", false, false, true);

            EditorGUILayout.PropertyField(
                _steepnessClamping, new GUIContent("Steepness Clamping"));
        }

        private void DrawMultiplierRow(
            WaveProfile profile,
            SerializedProperty property,
            string label,
            bool wavelength,
            bool amplitude,
            bool steepness)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
                using (new EditorGUI.DisabledScope(
                    Mathf.Approximately(property.floatValue, 1f)))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60f)))
                    {
                        // Bake the multiplier into the layers so the tuned look becomes the
                        // authored baseline and the knob returns to 1.
                        serializedObject.ApplyModifiedProperties();
                        Undo.RecordObject(profile, "Apply Wave Multiplier");
                        profile.ApplyMultipliers(wavelength, amplitude, steepness);
                        EditorUtility.SetDirty(profile);
                        serializedObject.Update();
                        WaveProfileEditorUtility.PushToScene(profile);
                    }
                }
            }
        }

        private void DrawCurves()
        {
            EditorGUILayout.LabelField(
                "Curves (value over layer index)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _wavelengthCurve, new GUIContent("Wave Length Curve"));
            EditorGUILayout.PropertyField(
                _amplitudeCurve, new GUIContent("Amplitude Curve"));
            EditorGUILayout.PropertyField(
                _steepnessCurve, new GUIContent("Steepness Curve"));
        }

        private void DrawProceduralEntry(WaveProfile profile)
        {
            if (GUILayout.Button("Open procedural editor", GUILayout.Height(24f)))
                WaveProfileWizardWindow.Open(profile);
        }

        private void DrawLayers(WaveProfile profile)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Layers ({_layers.arraySize})", EditorStyles.boldLabel);

                if (GUILayout.Button("Enable all", GUILayout.Width(80f)))
                    SetAllEnabled(profile, true);
                if (GUILayout.Button("Disable all", GUILayout.Width(80f)))
                    SetAllEnabled(profile, false);
            }

            for (int i = 0; i < _layers.arraySize; i++)
                DrawLayer(profile, i);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                    _layers.arraySize >= WaveProfile.MaxLayers))
                {
                    if (GUILayout.Button("Add layer"))
                    {
                        _layers.arraySize++;
                        serializedObject.ApplyModifiedProperties();
                        WaveProfileEditorUtility.PushToScene(profile);
                    }
                }

                using (new EditorGUI.DisabledScope(_layers.arraySize == 0))
                {
                    if (GUILayout.Button("Remove last"))
                    {
                        _layers.arraySize--;
                        serializedObject.ApplyModifiedProperties();
                        WaveProfileEditorUtility.PushToScene(profile);
                    }
                }
            }
        }

        private void DrawLayer(WaveProfile profile, int index)
        {
            SerializedProperty layer = _layers.GetArrayElementAtIndex(index);
            SerializedProperty enabled = layer.FindPropertyRelative("_enabled");
            SerializedProperty mode = layer.FindPropertyRelative("_mode");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(
                        enabled, GUIContent.none, GUILayout.Width(18f));
                    EditorGUILayout.LabelField(
                        $"Layer #{index + 1}", EditorStyles.boldLabel);
                }

                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        layer.FindPropertyRelative("_wavelength"),
                        new GUIContent("Wave Length"));
                    EditorGUILayout.PropertyField(
                        layer.FindPropertyRelative("_amplitude"),
                        new GUIContent("Amplitude"));
                    EditorGUILayout.PropertyField(
                        layer.FindPropertyRelative("_steepness"),
                        new GUIContent("Steepness"));
                    EditorGUILayout.PropertyField(mode, new GUIContent("Mode"));

                    if (mode.enumValueIndex == (int)WaveLayerMode.Circular)
                    {
                        EditorGUILayout.PropertyField(
                            layer.FindPropertyRelative("_origin"),
                            new GUIContent("Origin (world XZ)"));
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(
                            layer.FindPropertyRelative("_directionAngle"),
                            new GUIContent("Direction"));
                    }

                    EditorGUILayout.PropertyField(
                        layer.FindPropertyRelative("_speedMultiplier"),
                        new GUIContent("Speed"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        WaveProfileEditorUtility.PushToScene(profile);
                    }
                }
            }
        }

        private void SetAllEnabled(WaveProfile profile, bool enabled)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(profile, "Toggle Wave Layers");
            profile.SetAllLayersEnabled(enabled);
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            WaveProfileEditorUtility.PushToScene(profile);
        }
    }
}
