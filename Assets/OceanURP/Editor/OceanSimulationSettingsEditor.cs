using UnityEditor;

namespace OceanSystem.Editor
{
    [CustomEditor(typeof(OceanSimulationSettings))]
    public class OceanSimulationSettingsEditor : UnityEditor.Editor
    {
        private const string ShowPlotPrefName = "OceanSimulationSettingsShowPlot";

        private OceanSimulationSettings _simulationSettings;
        private readonly OceanSimulationInputs _simulationInputs = new OceanSimulationInputs();

        private void OnEnable()
        {
            _simulationSettings = (OceanSimulationSettings)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            bool showPlot = EditorGUILayout.Toggle("Show Plot", EditorPrefs.GetBool(ShowPlotPrefName));
            EditorPrefs.SetBool(ShowPlotPrefName, showPlot);
            if (!showPlot) return;

            SerializedProperty displaySpectrum = serializedObject.FindProperty("_displaySpectrum");
            EditorGUILayout.PropertyField(displaySpectrum);
            OceanSimulationInputsProvider inputsProvider =
                displaySpectrum.objectReferenceValue as OceanSimulationInputsProvider;
            if (inputsProvider != null)
            {
                inputsProvider.PopulateInputs(_simulationInputs);
                SpectrumPlotter.DrawGraphWithCascades(_simulationSettings, _simulationInputs);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
