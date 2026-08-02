using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot diagnostic: dumps ShaderUtil compiler messages for GrassWind, since shader
    /// compiler errors don't route through Application.logMessageReceived (and so don't show up
    /// in the MCP console log bridge) unless explicitly re-logged via Debug.LogError.
    /// </summary>
    public static class ShaderCompileInspector
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/GrassWind.shader";
        private static readonly string[] RealisticWaterShaderPaths =
        {
            "Assets/_Project/Art/Materials/Water/RealisticWater.shader",
            "Assets/_Project/Art/Shaders/RealisticWaterProjectedCaustics.shader",
        };

        [MenuItem("Market/Debug/Inspect GrassWind Shader Errors")]
        public static void Inspect()
        {
            InspectShader(ShaderPath);
        }

        /// <summary>Same dump for whatever shader (or shader of a material) is selected in the Project window.</summary>
        [MenuItem("Market/Debug/Inspect Selected Shader Errors")]
        public static void InspectSelection()
        {
            foreach (Object selected in Selection.objects)
            {
                Shader shader = selected as Shader
                    ?? (selected as Material)?.shader;
                if (shader == null)
                    continue;

                InspectShader(AssetDatabase.GetAssetPath(shader));
            }
        }

        /// <summary>
        /// Dumps compiler messages for the realistic-water surface and projected-caustic shaders.
        /// </summary>
        [MenuItem("Market/Debug/Water/Inspect Realistic Water Shader Errors")]
        public static void InspectRealisticWater()
        {
            foreach (string shaderPath in RealisticWaterShaderPaths)
                InspectShader(shaderPath);
        }

        private static void InspectShader(string shaderPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Debug.LogError(
                    $"[ShaderCompileInspector] Could not load shader at {shaderPath}");
                return;
            }

            Debug.Log($"[ShaderCompileInspector] {shader.name}: isSupported={shader.isSupported}, passCount={shader.passCount}");

            int messageCount = ShaderUtil.GetShaderMessageCount(shader);
            Debug.Log($"[ShaderCompileInspector] Message count: {messageCount}");

            if (messageCount <= 0)
                return;

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            foreach (ShaderMessage msg in messages)
            {
                string severity = msg.severity == ShaderCompilerMessageSeverity.Error ? "ERROR" : "WARNING";
                Debug.LogError($"[ShaderCompileInspector] {severity} ({msg.platform}) {msg.file}:{msg.line} - {msg.message}");
            }
        }
    }
}
