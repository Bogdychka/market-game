using UnityEditor;
using UnityEngine;

namespace OceanSystem.Editor
{
    [CustomEditor(typeof(OceanSimulationInputsProvider))]
    public class OceanSimulationInputsProviderEditor : UnityEditor.Editor
    {
        private WavesScaleEditorWindow _scaleEditorWindow;

        private void OnDisable()
        {
            if (_scaleEditorWindow != null)
                _scaleEditorWindow.Close();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawEditLocalWavesButton();
        }

        // The wind-force ramp lives in a hidden array, so Scale mode is only editable through the
        // dedicated window.
        private void DrawEditLocalWavesButton()
        {
            if (serializedObject.FindProperty("_mode").enumValueIndex == 0) return;

            EditorGUILayout.Space();
            if (GUILayout.Button("Edit Local Waves"))
            {
                _scaleEditorWindow = WavesScaleEditorWindow.Open(
                    serializedObject.FindProperty("_localWavesArray"));
            }
        }
    }
}
