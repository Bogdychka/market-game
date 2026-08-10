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
            "Assets/_Project/Art/Shaders/RealisticWetSand.shader",
            "Assets/_Project/Art/Shaders/RealisticWaterProjectedCaustics.shader",
            "Assets/_Project/Art/Shaders/RealisticWaterUnderwaterSurface.shader",
        };

        // The whitecap kernel shares the Gerstner include with the surface shaders, so a broken
        // edit there has to be visible here too - compute errors reach the console even less than
        // shader errors do.
        private static readonly string[] RealisticWaterComputePaths =
        {
            "Assets/_Project/Art/Shaders/RealisticWaterFoamUpdate.compute",
        };

        // Vendored gasgiant Ocean-URP: the surface shader and the two hidden fullscreen shaders,
        // plus the FFT/foam kernels the simulation drives them with.
        private static readonly string[] OceanUrpShaderPaths =
        {
            "Assets/OceanURP/Shaders/Ocean.shader",
            "Assets/OceanURP/Shaders/Resources/UnderwaterEffect.shader",
            "Assets/OceanURP/Shaders/Resources/StereographicSky.shader",
        };

        // Vendored jiaozi158 sky and clouds. passCount is the load-bearing number for the clouds
        // shader: its last two passes are gated behind PackageRequirements on the sky package, so
        // 9 means the sky integration compiled in and 7 means it was stripped - and a stripped
        // pass 7 is what the clouds code then asks DrawProcedural for, which takes the Editor down.
        private static readonly string[] SkyAndCloudsShaderPaths =
        {
            "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSky.shader",
            "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSkyPrecomputation.shader",
            "Assets/VolumetricCloudsURP/VolumetricClouds.shader",
        };

        private static readonly string[] OceanUrpComputePaths =
        {
            "Assets/OceanURP/Shaders/Resources/ComputeShaders/FFT.compute",
            "Assets/OceanURP/Shaders/Resources/ComputeShaders/InitialSpectrum.compute",
            "Assets/OceanURP/Shaders/Resources/ComputeShaders/TimeDependentSpectrum.compute",
            "Assets/OceanURP/Shaders/Resources/ComputeShaders/FoamSimulation.compute",
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

            foreach (string computePath in RealisticWaterComputePaths)
                InspectComputeShader(computePath);
        }

        /// <summary>
        /// Dumps compiler messages for the vendored Ocean URP shaders and compute kernels.
        /// </summary>
        [MenuItem("Market/Debug/Water/Inspect Ocean URP Shader Errors")]
        public static void InspectOceanUrp()
        {
            foreach (string shaderPath in OceanUrpShaderPaths)
                InspectShader(shaderPath);

            foreach (string computePath in OceanUrpComputePaths)
                InspectComputeShader(computePath);
        }

        /// <summary>
        /// Dumps compiler messages for the vendored physically based sky and volumetric clouds
        /// shaders. Check the clouds shader's passCount: 9 = sky integration active, 7 = stripped.
        /// </summary>
        [MenuItem("Market/Debug/Rendering/Inspect Sky and Clouds Shader Errors")]
        public static void InspectSkyAndClouds()
        {
            foreach (string shaderPath in SkyAndCloudsShaderPaths)
                InspectShader(shaderPath);
        }

        private static void InspectComputeShader(string computePath)
        {
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(computePath);
            if (compute == null)
            {
                Debug.LogError(
                    $"[ShaderCompileInspector] Could not load compute shader at {computePath}");
                return;
            }

            int messageCount = ShaderUtil.GetComputeShaderMessageCount(compute);
            Debug.Log(
                $"[ShaderCompileInspector] {compute.name}: messages={messageCount}");

            if (messageCount <= 0)
                return;

            ShaderMessage[] messages = ShaderUtil.GetComputeShaderMessages(compute);
            foreach (ShaderMessage msg in messages)
            {
                string severity =
                    msg.severity == ShaderCompilerMessageSeverity.Error ? "ERROR" : "WARNING";
                Debug.LogError(
                    $"[ShaderCompileInspector] {severity} ({msg.platform}) " +
                    $"{msg.file}:{msg.line} - {msg.message}");
            }
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
