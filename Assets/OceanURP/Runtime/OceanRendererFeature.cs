using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OceanSystem
{
    public class OceanRendererFeature : ScriptableRendererFeature
    {
        public static bool IsCorrectCameraType(CameraType t) => t == CameraType.Game
            || t == CameraType.SceneView || t == CameraType.VR;

        [SerializeField] private OceanRenderingSettings _settings;

        private OceanRenderPass _oceanPass;

        public override void Create()
        {
            _oceanPass = new OceanRenderPass(_settings);
            name = "Ocean";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            SetupGlobalKeywords();
            renderer.EnqueuePass(_oceanPass);
        }

        protected override void Dispose(bool disposing)
        {
            _oceanPass?.Dispose();
            _oceanPass = null;
        }

        // These drive multi_compile variants of Ocean.shader and are read back by OceanRenderer to
        // pick the cull mode, so they are set from the CPU rather than from inside a render pass.
        private void SetupGlobalKeywords()
        {
            SetGlobalKeyword("OCEAN_TRANSPARENCY_ENABLED", _settings.transparency);
            SetGlobalKeyword("OCEAN_UNDERWATER_ENABLED", _settings.underwaterEffect);
        }

        private static void SetGlobalKeyword(string keyword, bool enabled)
        {
            if (enabled)
                Shader.EnableKeyword(keyword);
            else
                Shader.DisableKeyword(keyword);
        }

        private void OnValidate()
        {
            _settings.skyMapResolution = Mathf.Clamp(_settings.skyMapResolution, 16, 2048);
        }

        [System.Serializable]
        public class OceanRenderingSettings
        {
            public int skyMapResolution = 256;
            public bool updateSkyMap;
            public bool transparency;
            public bool underwaterEffect;
        }

#if UNITY_EDITOR
        public const string RenderInEditModePrefName = "RenderOceanInEditMode";
        public static bool RenderInEditMode
        {
            get
            {
                if (_renderInEditMode == null)
                    _renderInEditMode = EditorPrefs.GetBool(RenderInEditModePrefName);
                return _renderInEditMode.Value;
            }

            set
            {
                _renderInEditMode = value;
            }
        }
        private static bool? _renderInEditMode = null;
        public static bool IsRendering => Application.isPlaying || RenderInEditMode;
#endif
    }
}
