using UnityEngine;
using UnityEngine.Rendering;

namespace Market.DebugTools
{
    /// <summary>
    /// Selects the temporal foam buffer resolution or the no-history fallback.
    /// </summary>
    public enum WaterFoamHistoryQuality
    {
        NoHistory,
        History256,
        History512,
    }

    /// <summary>
    /// Maintains bounded world-space whitecap and shoreline foam history for WaterShaderLab.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterTemporalFoam : MonoBehaviour
    {
        private const string UpdateKernelName = "UpdateFoam";

        [Header("References")]
        [SerializeField] private ComputeShader foamUpdateCompute;

        [Header("Quality")]
        [SerializeField] private WaterFoamHistoryQuality quality =
            WaterFoamHistoryQuality.History256;

        [Header("Whitecaps")]
        [SerializeField, Min(0f)] private float whitecapDecayRate = 1f;
        [SerializeField, Min(0f)] private float whitecapInjectionStrength = 1f;

        [Header("Shoreline")]
        [SerializeField, Min(0f)] private float shorelineDecayRate = 0.9f;
        [SerializeField, Min(0f)] private float shorelineInjectionStrength = 1f;
        [SerializeField, Min(0.1f)] private float shorelineWidth = 1.25f;
        [SerializeField] private LayerMask shorelineLayers = ~0;
        [SerializeField, Min(1f)] private float scanHeight = 24f;
        [SerializeField, Min(1f)] private float scanDepth = 64f;

        [Header("Flow")]
        [SerializeField, Min(0f)] private float advectionSpeed = 0.65f;

        private static readonly int HistoryTextureId =
            Shader.PropertyToID("_FoamHistoryTexture");
        private static readonly int HistoryAvailableId =
            Shader.PropertyToID("_FoamHistoryAvailable");
        private static readonly int HistoryWorldRectId =
            Shader.PropertyToID("_FoamHistoryWorldRect");
        private static readonly int UpdateWorldRectId =
            Shader.PropertyToID("_FoamUpdateWorldRect");
        private static readonly int ShorelineMaskId =
            Shader.PropertyToID("_ShorelineMask");
        private static readonly int HistorySourceId =
            Shader.PropertyToID("_FoamHistorySource");
        private static readonly int HistoryDestinationId =
            Shader.PropertyToID("_FoamHistoryDestination");
        private static readonly int BufferResolutionId =
            Shader.PropertyToID("_FoamBufferResolution");
        private static readonly int FoamDecayRatesId =
            Shader.PropertyToID("_FoamDecayRates");
        private static readonly int FoamInjectionStrengthsId =
            Shader.PropertyToID("_FoamInjectionStrengths");
        private static readonly int FoamAdvectionVelocityId =
            Shader.PropertyToID("_FoamAdvectionVelocity");
        private static readonly int FoamDeltaTimeId =
            Shader.PropertyToID("_FoamDeltaTime");
        private static readonly int FoamTimeId =
            Shader.PropertyToID("_FoamTime");
        private static readonly int WindDirectionId =
            Shader.PropertyToID("_WindDirection");
        private static readonly int WindSpreadId =
            Shader.PropertyToID("_WindSpread");
        private static readonly int Wave1ParamsId =
            Shader.PropertyToID("_Wave1Params");
        private static readonly int Wave2ParamsId =
            Shader.PropertyToID("_Wave2Params");
        private static readonly int Wave3ParamsId =
            Shader.PropertyToID("_Wave3Params");
        private static readonly int Wave4ParamsId =
            Shader.PropertyToID("_Wave4Params");
        private static readonly int Wave1SteepnessId =
            Shader.PropertyToID("_Wave1Steepness");
        private static readonly int Wave2SteepnessId =
            Shader.PropertyToID("_Wave2Steepness");
        private static readonly int Wave3SteepnessId =
            Shader.PropertyToID("_Wave3Steepness");
        private static readonly int Wave4SteepnessId =
            Shader.PropertyToID("_Wave4Steepness");
        private static readonly int FoamCrestGainId =
            Shader.PropertyToID("_FoamCrestGain");
        private static readonly int FoamCrestBiasId =
            Shader.PropertyToID("_FoamCrestBias");
        private static readonly int FoamNoiseTilingId =
            Shader.PropertyToID("_FoamNoiseTiling");
        private static readonly int FoamNoiseSpeedId =
            Shader.PropertyToID("_FoamNoiseSpeed");

        private Renderer _targetRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private ComputeShader _updateCompute;
        private int _updateKernel = -1;
        private RenderTexture _historyRead;
        private RenderTexture _historyWrite;
        private Texture2D _shorelineMask;
        private Vector4 _worldRect;
        private int _bufferResolution;
        private int _lastUpdateFrame = -1;
        private int _resourceGeneration;
        private bool _whitecapInjectionEnabled = true;

        public WaterFoamHistoryQuality Quality => quality;
        public int ActiveResolution => _bufferResolution;
        public Vector2 WorldCoverage => new(_worldRect.z, _worldRect.w);
        public long EstimatedMemoryBytes =>
            (long)_bufferResolution * _bufferResolution * 9L;
        public RenderTexture HistoryTexture => _historyRead;
        public Texture ShorelineMaskTexture => _shorelineMask;
        public int ResourceGeneration => _resourceGeneration;
        public string HistoryTextureEntityId =>
            _historyRead != null ? _historyRead.GetEntityId().ToString() : "None";
        public bool WhitecapInjectionEnabled => _whitecapInjectionEnabled;

        /// <summary>
        /// Selects the no-history fallback or a bounded temporal history resolution.
        /// </summary>
        public void SetQuality(WaterFoamHistoryQuality selectedQuality)
        {
            if (quality == selectedQuality)
                return;

            quality = selectedQuality;
            CacheComponents();
            SetHistoryAvailable(false);
            ReleaseHistoryTextures();
        }

        /// <summary>
        /// Enables or suppresses whitecap injection without resetting the history buffer.
        /// </summary>
        public void SetWhitecapInjectionEnabled(bool enabled)
        {
            _whitecapInjectionEnabled = enabled;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            SetHistoryAvailable(false);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            SetHistoryAvailable(false);
            ReleaseResources();
        }

        private void OnValidate()
        {
            whitecapDecayRate = Mathf.Max(0f, whitecapDecayRate);
            whitecapInjectionStrength = Mathf.Max(
                0f, whitecapInjectionStrength);
            shorelineDecayRate = Mathf.Max(0f, shorelineDecayRate);
            shorelineInjectionStrength = Mathf.Max(
                0f, shorelineInjectionStrength);
            shorelineWidth = Mathf.Max(0.1f, shorelineWidth);
            scanHeight = Mathf.Max(1f, scanHeight);
            scanDepth = Mathf.Max(1f, scanDepth);
            advectionSpeed = Mathf.Max(0f, advectionSpeed);
            ReleaseHistoryTextures();
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
            if (!ShouldUpdate(sourceCamera))
                return;

            if (quality == WaterFoamHistoryQuality.NoHistory)
            {
                SetHistoryAvailable(false);
                return;
            }

            if (!EnsureResources())
            {
                SetHistoryAvailable(false);
                return;
            }

            if (Application.isPlaying && _lastUpdateFrame == Time.frameCount)
                return;

            _lastUpdateFrame = Time.frameCount;
            ConfigureUpdateCompute();
            ExecuteUpdate();
            ApplyHistoryTexture();
        }

        private bool ShouldUpdate(Camera sourceCamera)
        {
            if (_targetRenderer == null || !_targetRenderer.enabled)
                return false;
            if (sourceCamera == null)
                return false;
            return sourceCamera.cameraType != CameraType.Preview &&
                sourceCamera.cameraType != CameraType.Reflection &&
                sourceCamera.cameraType != CameraType.SceneView;
        }

        private bool EnsureResources()
        {
            int desiredResolution = quality == WaterFoamHistoryQuality.History512
                ? 512
                : 256;
            Vector4 desiredRect = CalculateWorldRect();
            bool regionChanged = HasRegionChanged(desiredRect, desiredResolution);
            if (!regionChanged &&
                _historyRead != null &&
                _historyWrite != null &&
                _shorelineMask != null)
            {
                return true;
            }

            ReleaseHistoryTextures();
            if (!EnsureUpdateCompute())
                return false;
            if (!SystemInfo.supportsComputeShaders ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf))
                return false;

            _bufferResolution = desiredResolution;
            _worldRect = desiredRect;
            _historyRead = CreateHistoryTexture("Temporal Foam History A");
            _historyWrite = CreateHistoryTexture("Temporal Foam History B");
            _resourceGeneration++;
            ClearHistoryTexture(_historyRead);
            ClearHistoryTexture(_historyWrite);
            BuildShorelineMask();
            return _shorelineMask != null;
        }

        private bool EnsureUpdateCompute()
        {
            if (_updateCompute != null && _updateKernel >= 0)
                return true;
            if (foamUpdateCompute == null)
                return false;

            _updateCompute = Instantiate(foamUpdateCompute);
            _updateCompute.name = "Realistic Water Foam Update";
            _updateCompute.hideFlags = HideFlags.HideAndDontSave;
            _updateKernel = _updateCompute.FindKernel(UpdateKernelName);
            if (_updateKernel < 0)
            {
                DestroyTexture(_updateCompute);
                _updateCompute = null;
                return false;
            }

            return true;
        }

        private Vector4 CalculateWorldRect()
        {
            Bounds bounds = _targetRenderer.bounds;
            float sizeX = Mathf.Max(bounds.size.x, 1f);
            float sizeZ = Mathf.Max(bounds.size.z, 1f);
            return new Vector4(
                bounds.center.x - sizeX * 0.5f,
                bounds.center.z - sizeZ * 0.5f,
                sizeX,
                sizeZ);
        }

        private bool HasRegionChanged(Vector4 desiredRect, int desiredResolution)
        {
            if (_bufferResolution != desiredResolution)
                return true;
            float cellSize = Mathf.Max(
                desiredRect.z, desiredRect.w) / desiredResolution;
            return Mathf.Abs(desiredRect.x - _worldRect.x) > cellSize ||
                Mathf.Abs(desiredRect.y - _worldRect.y) > cellSize ||
                Mathf.Abs(desiredRect.z - _worldRect.z) > cellSize ||
                Mathf.Abs(desiredRect.w - _worldRect.w) > cellSize;
        }

        private RenderTexture CreateHistoryTexture(string textureName)
        {
            var texture = new RenderTexture(
                _bufferResolution,
                _bufferResolution,
                0,
                RenderTextureFormat.RGHalf)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
            };
            texture.Create();
            return texture;
        }

        private static void ClearHistoryTexture(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void BuildShorelineMask()
        {
            Physics.SyncTransforms();
            int pixelCount = _bufferResolution * _bufferResolution;
            var depths = new float[pixelCount];
            var pixels = new byte[pixelCount];
            float cellX = _worldRect.z / _bufferResolution;
            float cellZ = _worldRect.w / _bufferResolution;
            float waterY = transform.position.y;

            ScanWaterDepths(depths, cellX, cellZ, waterY);
            BuildShorelinePixels(depths, pixels, cellX, cellZ);

            _shorelineMask = new Texture2D(
                _bufferResolution,
                _bufferResolution,
                TextureFormat.R8,
                false,
                true)
            {
                name = "Realistic Water Shoreline Injection",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _shorelineMask.SetPixelData(pixels, 0);
            _shorelineMask.Apply(false, true);
        }

        private void ScanWaterDepths(
            float[] depths, float cellX, float cellZ, float waterY)
        {
            float rayDistance = scanHeight + scanDepth;
            for (int y = 0; y < _bufferResolution; y++)
            {
                float worldZ = _worldRect.y + (y + 0.5f) * cellZ;
                for (int x = 0; x < _bufferResolution; x++)
                {
                    float worldX = _worldRect.x + (x + 0.5f) * cellX;
                    Vector3 origin = new(worldX, waterY + scanHeight, worldZ);
                    int index = y * _bufferResolution + x;
                    depths[index] = Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        rayDistance,
                        shorelineLayers,
                        QueryTriggerInteraction.Ignore)
                        ? waterY - hit.point.y
                        : scanDepth;
                }
            }
        }

        private void BuildShorelinePixels(
            float[] depths, byte[] pixels, float cellX, float cellZ)
        {
            float gradientScale = 1f / Mathf.Max(
                Mathf.Max(cellX, cellZ), 0.001f);
            for (int y = 0; y < _bufferResolution; y++)
            {
                for (int x = 0; x < _bufferResolution; x++)
                {
                    int index = y * _bufferResolution + x;
                    float source = EvaluateShorelineSource(
                        depths, x, y, gradientScale, cellX, cellZ);
                    pixels[index] = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(source) * 255f);
                }
            }
        }

        private float EvaluateShorelineSource(
            float[] depths,
            int x,
            int y,
            float gradientScale,
            float cellX,
            float cellZ)
        {
            int index = y * _bufferResolution + x;
            float center = depths[index];
            if (center <= 0.01f)
                return 0f;

            float left = SampleDepth(depths, x - 1, y);
            float right = SampleDepth(depths, x + 1, y);
            float down = SampleDepth(depths, x, y - 1);
            float up = SampleDepth(depths, x, y + 1);
            float minimumNeighbor = Mathf.Min(left, right, down, up);
            float maximumDelta = Mathf.Max(
                Mathf.Abs(left - center),
                Mathf.Abs(right - center),
                Mathf.Abs(down - center),
                Mathf.Abs(up - center));
            float obstacleIntersection = minimumNeighbor <= 0.01f ? 1f : 0f;
            float shallow = 1f - Mathf.Clamp01(center / shorelineWidth);
            float depthGradient = Mathf.Clamp01(maximumDelta * gradientScale);
            float band = Mathf.Max(obstacleIntersection, shallow * depthGradient);

            float worldX = _worldRect.x + (x + 0.5f) * cellX;
            float worldZ = _worldRect.y + (y + 0.5f) * cellZ;
            float broadNoise = Mathf.PerlinNoise(
                worldX * 0.17f + 13.1f,
                worldZ * 0.17f + 7.7f);
            float fineNoise = Mathf.Sin(worldX * 0.73f - worldZ * 0.51f) *
                0.5f + 0.5f;
            float breakup = Mathf.SmoothStep(
                0.2f, 0.85f, broadNoise * 0.75f + fineNoise * 0.25f);
            return band * breakup;
        }

        private float SampleDepth(float[] depths, int x, int y)
        {
            int clampedX = Mathf.Clamp(x, 0, _bufferResolution - 1);
            int clampedY = Mathf.Clamp(y, 0, _bufferResolution - 1);
            return depths[clampedY * _bufferResolution + clampedX];
        }

        private void ConfigureUpdateCompute()
        {
            Material waterMaterial = _targetRenderer.sharedMaterial;
            if (waterMaterial == null)
                return;

            CopyWaterProperties(waterMaterial);
            float deltaTime = Application.isPlaying
                ? Mathf.Clamp(Time.deltaTime, 0f, 0.1f)
                : 1f / 30f;
            Vector4 wind = waterMaterial.GetVector(WindDirectionId);
            Vector2 windDirection = new(wind.x, wind.z);
            if (windDirection.sqrMagnitude < 0.0001f)
                windDirection = Vector2.right;
            else
                windDirection.Normalize();

            _updateCompute.SetInt(BufferResolutionId, _bufferResolution);
            _updateCompute.SetVector(UpdateWorldRectId, _worldRect);
            _updateCompute.SetVector(
                FoamDecayRatesId,
                new Vector4(whitecapDecayRate, shorelineDecayRate, 0f, 0f));
            _updateCompute.SetVector(
                FoamInjectionStrengthsId,
                new Vector4(
                    _whitecapInjectionEnabled ? whitecapInjectionStrength : 0f,
                    shorelineInjectionStrength,
                    0f,
                    0f));
            _updateCompute.SetVector(
                FoamAdvectionVelocityId,
                windDirection * advectionSpeed);
            _updateCompute.SetFloat(FoamDeltaTimeId, deltaTime);
            _updateCompute.SetFloat(
                FoamTimeId,
                Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
        }

        private void CopyWaterProperties(Material waterMaterial)
        {
            _updateCompute.SetVector(
                WindDirectionId, waterMaterial.GetVector(WindDirectionId));
            _updateCompute.SetFloat(
                WindSpreadId, waterMaterial.GetFloat(WindSpreadId));
            CopyVector(waterMaterial, Wave1ParamsId);
            CopyVector(waterMaterial, Wave2ParamsId);
            CopyVector(waterMaterial, Wave3ParamsId);
            CopyVector(waterMaterial, Wave4ParamsId);
            CopyFloat(waterMaterial, Wave1SteepnessId);
            CopyFloat(waterMaterial, Wave2SteepnessId);
            CopyFloat(waterMaterial, Wave3SteepnessId);
            CopyFloat(waterMaterial, Wave4SteepnessId);
            CopyFloat(waterMaterial, FoamCrestGainId);
            CopyFloat(waterMaterial, FoamCrestBiasId);
            CopyFloat(waterMaterial, FoamNoiseTilingId);
            CopyFloat(waterMaterial, FoamNoiseSpeedId);
        }

        private void CopyVector(Material source, int propertyId)
        {
            _updateCompute.SetVector(propertyId, source.GetVector(propertyId));
        }

        private void CopyFloat(Material source, int propertyId)
        {
            _updateCompute.SetFloat(propertyId, source.GetFloat(propertyId));
        }

        private void ExecuteUpdate()
        {
            _updateCompute.SetTexture(
                _updateKernel, HistorySourceId, _historyRead);
            _updateCompute.SetTexture(
                _updateKernel, HistoryDestinationId, _historyWrite);
            _updateCompute.SetTexture(
                _updateKernel, ShorelineMaskId, _shorelineMask);
            int threadGroups = Mathf.CeilToInt(_bufferResolution / 8f);
            _updateCompute.Dispatch(
                _updateKernel, threadGroups, threadGroups, 1);
            (_historyRead, _historyWrite) = (_historyWrite, _historyRead);
        }

        private void ApplyHistoryTexture()
        {
            _targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(HistoryTextureId, _historyRead);
            _propertyBlock.SetFloat(HistoryAvailableId, 1f);
            _propertyBlock.SetVector(
                HistoryWorldRectId,
                new Vector4(
                    _worldRect.x,
                    _worldRect.y,
                    1f / _worldRect.z,
                    1f / _worldRect.w));
            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetHistoryAvailable(bool available)
        {
            if (_targetRenderer == null || _propertyBlock == null)
                return;

            _targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(HistoryAvailableId, available ? 1f : 0f);
            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ReleaseResources()
        {
            ReleaseHistoryTextures();
            if (_updateCompute == null)
                return;

            if (Application.isPlaying)
                Destroy(_updateCompute);
            else
                DestroyImmediate(_updateCompute);
            _updateCompute = null;
            _updateKernel = -1;
        }

        private void ReleaseHistoryTextures()
        {
            DestroyTexture(_historyRead);
            DestroyTexture(_historyWrite);
            DestroyTexture(_shorelineMask);
            _historyRead = null;
            _historyWrite = null;
            _shorelineMask = null;
            _bufferResolution = 0;
            _lastUpdateFrame = -1;
        }

        private static void DestroyTexture(Object texture)
        {
            if (texture == null)
                return;

            if (texture is RenderTexture renderTexture)
                renderTexture.Release();
            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
        }
    }
}
