// MarkupAttributes is not vendored with this port, so the inspector is the default one plus the
// pipeline-asset requirements and the edit mode toggle the original editor added.
using System.Threading.Tasks;
using UnityEditor;

namespace OceanSystem.Editor
{
    [CustomEditor(typeof(OceanRendererFeature))]
    public class OceanRendererFeatureEditor : UnityEditor.Editor
    {
        private const string RequirementTextures = "Depth Texture and Opaque Texture must " +
            "be enabled in the pipeline asset.";
        private const string RequirementDownsampling = "Opaque downsampling must be None.";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUI.BeginChangeCheck();

            SerializedProperty settings = serializedObject.FindProperty("_settings");
            SerializedProperty transparency = settings.FindPropertyRelative("transparency");
            SerializedProperty underwater = settings.FindPropertyRelative("underwaterEffect");

            if (transparency.boolValue || underwater.boolValue)
            {
                string message = RequirementTextures;
                if (underwater.boolValue)
                    message += " " + RequirementDownsampling;
                EditorGUILayout.HelpBox(message, MessageType.Info, true);
            }

            EditorGUILayout.Space();
            bool newValue = EditorGUILayout.Toggle("Render In Edit Mode",
                EditorPrefs.GetBool(OceanRendererFeature.RenderInEditModePrefName));
            OceanRendererFeature.RenderInEditMode = newValue;
            EditorPrefs.SetBool(OceanRendererFeature.RenderInEditModePrefName, newValue);

            if (EditorGUI.EndChangeCheck())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                QueueDelayedPlayerLoopUpdate();
                serializedObject.ApplyModifiedProperties();
            }
        }

        private async void QueueDelayedPlayerLoopUpdate()
        {
            await Task.Delay(100);
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
