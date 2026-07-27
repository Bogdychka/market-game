using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Market.DebugTools
{
    /// <summary>
    /// Selects the local reflection cost used by the experimental realistic water.
    /// </summary>
    public enum WaterPlanarReflectionQuality
    {
        SkyOnly,
        HalfResolution,
        FullResolution,
    }

    /// <summary>
    /// Vertical orientation of the planar reflection sample. <see cref="Never"/> is the verified
    /// correct value here - URP 17 already normalises render-texture orientation, so the extra
    /// flip mirrors the reflection and shows the seabed hemisphere on far water.
    /// <see cref="Auto"/> keeps the original R5 assumption (flip whenever the graphics API puts
    /// texture V at the top) for platforms where that turns out to be needed.
    /// </summary>
    public enum WaterReflectionFlip
    {
        Auto,
        Never,
        Always,
    }

    /// <summary>
    /// Renders one camera-relative planar reflection for the WaterShaderLab surface.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterPlanarReflection : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private WaterPlanarReflectionQuality quality =
            WaterPlanarReflectionQuality.HalfResolution;
        [SerializeField] private LayerMask reflectionMask = ~0;
        [SerializeField, Min(0f)] private float clipPlaneOffset = 0.05f;
        [SerializeField] private bool renderShadows = true;

        [Tooltip("Never is correct under URP 17: the pipeline already normalises render-texture " +
                 "orientation. Auto reproduces the original R5 assumption, which mirrored the " +
                 "reflection on D3D and painted the seabed hemisphere onto the far water.")]
        [SerializeField] private WaterReflectionFlip reflectionFlip = WaterReflectionFlip.Never;

        private static readonly int ReflectionTextureId =
            Shader.PropertyToID("_PlanarReflectionTexture");
        private static readonly int ReflectionAvailableId =
            Shader.PropertyToID("_PlanarReflectionAvailable");
        private static readonly int ReflectionFlipYId =
            Shader.PropertyToID("_PlanarReflectionFlipY");

        private Renderer _targetRenderer;
        private Camera _reflectionCamera;
        private RenderTexture _reflectionTexture;
        private MaterialPropertyBlock _propertyBlock;
        private bool _isRendering;
        private int _textureWidth;
        private int _textureHeight;

        public WaterPlanarReflectionQuality Quality => quality;
        public int TextureWidth => _textureWidth;
        public int TextureHeight => _textureHeight;
        public long EstimatedMemoryBytes =>
            (long)_textureWidth * _textureHeight * 12L;

        /// <summary>
        /// Selects sky-only, half-resolution, or full-resolution local reflection.
        /// </summary>
        public void SetQuality(WaterPlanarReflectionQuality selectedQuality)
        {
            if (quality == selectedQuality)
                return;

            quality = selectedQuality;
            CacheComponents();
            SetReflectionAvailable(false);
            ReleaseRenderTexture();
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            SetReflectionAvailable(false);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            SetReflectionAvailable(false);
            ReleaseResources();
        }

        private void OnValidate()
        {
            clipPlaneOffset = Mathf.Max(0f, clipPlaneOffset);
            ReleaseRenderTexture();
        }

        private void CacheComponents()
        {
            if (_targetRenderer == null)
                _targetRenderer = GetComponent<Renderer>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context, Camera sourceCamera)
        {
            if (_isRendering ||
                _targetRenderer == null ||
                sourceCamera == null ||
                sourceCamera == _reflectionCamera)
            {
                return;
            }

            // Any camera we do not render a reflection for must still have the flag cleared.
            // Leaving it at 1 makes the water sample the texture rendered for the *game* camera
            // with this camera's own screen UVs: the reflection stays locked to the game
            // viewpoint and swims across the surface as the scene view flies around. Falling back
            // to the probe/sky reflection is wrong-but-stable instead of wrong-and-moving.
            if (!IsSupportedCamera(sourceCamera) ||
                !_targetRenderer.enabled ||
                quality == WaterPlanarReflectionQuality.SkyOnly)
            {
                SetReflectionAvailable(false);
                return;
            }

            _isRendering = true;
            try
            {
                EnsureReflectionCamera();
                EnsureRenderTexture(sourceCamera);
                ConfigureReflectionCamera(sourceCamera);
                RenderReflection(context);
            }
            finally
            {
                _isRendering = false;
            }
        }

        private float ResolveFlipY()
        {
            return reflectionFlip switch
            {
                WaterReflectionFlip.Never => 0f,
                WaterReflectionFlip.Always => 1f,
                _ => SystemInfo.graphicsUVStartsAtTop ? 1f : 0f,
            };
        }

        private static bool IsSupportedCamera(Camera sourceCamera)
        {
            return sourceCamera.cameraType != CameraType.Preview &&
                sourceCamera.cameraType != CameraType.Reflection &&
                sourceCamera.cameraType != CameraType.SceneView;
        }

        private void EnsureReflectionCamera()
        {
            if (_reflectionCamera != null)
                return;

            var cameraObject = new GameObject(
                $"{name} Planar Reflection Camera",
                typeof(Camera),
                typeof(UniversalAdditionalCameraData));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            _reflectionCamera = cameraObject.GetComponent<Camera>();
            _reflectionCamera.enabled = false;
        }

        private void EnsureRenderTexture(Camera sourceCamera)
        {
            float renderScale = quality == WaterPlanarReflectionQuality.FullResolution
                ? 1f
                : 0.5f;
            int width = Mathf.Max(64, Mathf.RoundToInt(sourceCamera.pixelWidth * renderScale));
            int height = Mathf.Max(64, Mathf.RoundToInt(sourceCamera.pixelHeight * renderScale));
            if (_reflectionTexture != null &&
                width == _textureWidth &&
                height == _textureHeight)
            {
                return;
            }

            ReleaseRenderTexture();
            _textureWidth = width;
            _textureHeight = height;
            _reflectionTexture = new RenderTexture(
                width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "Realistic Water Planar Reflection",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            _reflectionTexture.Create();
        }

        private void ConfigureReflectionCamera(Camera sourceCamera)
        {
            _reflectionCamera.CopyFrom(sourceCamera);
            _reflectionCamera.enabled = false;
            _reflectionCamera.targetTexture = _reflectionTexture;
            _reflectionCamera.cullingMask = reflectionMask;
            _reflectionCamera.useOcclusionCulling = false;
            _reflectionCamera.allowMSAA = false;
            _reflectionCamera.depthTextureMode = DepthTextureMode.None;

            UniversalAdditionalCameraData cameraData =
                _reflectionCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = renderShadows;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;

            Vector3 planeNormal = transform.up.normalized;
            Vector3 planePosition = transform.position + planeNormal * clipPlaneOffset;
            Vector4 plane = new(
                planeNormal.x,
                planeNormal.y,
                planeNormal.z,
                -Vector3.Dot(planeNormal, planePosition));
            Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(plane);

            _reflectionCamera.worldToCameraMatrix =
                sourceCamera.worldToCameraMatrix * reflectionMatrix;
            Vector3 reflectedPosition =
                reflectionMatrix.MultiplyPoint(sourceCamera.transform.position);
            Vector3 reflectedForward =
                Vector3.Reflect(sourceCamera.transform.forward, planeNormal);
            Vector3 reflectedUp =
                Vector3.Reflect(sourceCamera.transform.up, planeNormal);
            _reflectionCamera.transform.SetPositionAndRotation(
                reflectedPosition,
                Quaternion.LookRotation(reflectedForward, reflectedUp));

            Vector4 clipPlane = CameraSpacePlane(
                _reflectionCamera, planePosition, planeNormal);
            _reflectionCamera.projectionMatrix =
                sourceCamera.CalculateObliqueMatrix(clipPlane);
        }

        private void RenderReflection(ScriptableRenderContext context)
        {
            bool rendererWasEnabled = _targetRenderer.enabled;
            bool previousInvertCulling = GL.invertCulling;
            _targetRenderer.enabled = false;
            GL.invertCulling = true;

            try
            {
#pragma warning disable 0618
                UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
#pragma warning restore 0618
                ApplyReflectionTexture();
            }
            finally
            {
                GL.invertCulling = previousInvertCulling;
                _targetRenderer.enabled = rendererWasEnabled;
            }
        }

        private void ApplyReflectionTexture()
        {
            _targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(ReflectionTextureId, _reflectionTexture);
            _propertyBlock.SetFloat(ReflectionAvailableId, 1f);
            _propertyBlock.SetFloat(ReflectionFlipYId, ResolveFlipY());
            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetReflectionAvailable(bool available)
        {
            if (_targetRenderer == null || _propertyBlock == null)
                return;

            _targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ReflectionAvailableId, available ? 1f : 0f);
            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ReleaseResources()
        {
            ReleaseRenderTexture();
            if (_reflectionCamera == null)
                return;

            GameObject cameraObject = _reflectionCamera.gameObject;
            _reflectionCamera = null;
            if (Application.isPlaying)
                Destroy(cameraObject);
            else
                DestroyImmediate(cameraObject);
        }

        private void ReleaseRenderTexture()
        {
            if (_reflectionTexture == null)
                return;

            _reflectionTexture.Release();
            if (Application.isPlaying)
                Destroy(_reflectionTexture);
            else
                DestroyImmediate(_reflectionTexture);
            _reflectionTexture = null;
            _textureWidth = 0;
            _textureHeight = 0;
        }

        private static Vector4 CameraSpacePlane(
            Camera camera, Vector3 position, Vector3 normal)
        {
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Vector3 cameraPosition = worldToCamera.MultiplyPoint(position);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized;
            return new Vector4(
                cameraNormal.x,
                cameraNormal.y,
                cameraNormal.z,
                -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.m00 = 1f - 2f * plane.x * plane.x;
            matrix.m01 = -2f * plane.x * plane.y;
            matrix.m02 = -2f * plane.x * plane.z;
            matrix.m03 = -2f * plane.w * plane.x;
            matrix.m10 = -2f * plane.y * plane.x;
            matrix.m11 = 1f - 2f * plane.y * plane.y;
            matrix.m12 = -2f * plane.y * plane.z;
            matrix.m13 = -2f * plane.w * plane.y;
            matrix.m20 = -2f * plane.z * plane.x;
            matrix.m21 = -2f * plane.z * plane.y;
            matrix.m22 = 1f - 2f * plane.z * plane.z;
            matrix.m23 = -2f * plane.w * plane.z;
            return matrix;
        }
    }
}
