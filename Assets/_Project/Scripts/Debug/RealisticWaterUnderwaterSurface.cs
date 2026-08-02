using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Selects the complete underside pass or the low-cost front-face-only fallback.
    /// </summary>
    public enum WaterUnderwaterSurfaceQuality
    {
        FrontFaceOnly = 0,
        UnderwaterSurface = 1,
    }

    /// <summary>
    /// Synchronizes the optional underside renderer with the laboratory water material and volume.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterUnderwaterSurface : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer underwaterRenderer;

        [Header("Quality")]
        [SerializeField] private WaterUnderwaterSurfaceQuality quality =
            WaterUnderwaterSurfaceQuality.UnderwaterSurface;

        private static readonly int WaterHeightId =
            Shader.PropertyToID("_UnderwaterWaterHeight");
        private static readonly int TransitionBlendId =
            Shader.PropertyToID("_UnderwaterTransitionBlend");
        private static readonly int FogColorId =
            Shader.PropertyToID("_UnderwaterFogColor");

        private Renderer _waterRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private WaterUnderwaterSurfaceQuality _appliedQuality;
        private float _transitionBlend;
        private bool _cameraUnderwater;
        private float _appliedWaterHeight = float.NaN;
        private Vector4 _windDirection = new(0.906f, 0f, 0.423f, 0f);
        private float _windSpread = 0.55f;
        private Vector4 _wave1Params = new(25f, 14f, 0.35f, 1f);
        private Vector4 _wave2Params = new(95f, 8f, 0.2f, 1.4f);
        private Vector4 _wave3Params = new(200f, 4.5f, 0.1f, 1.8f);
        private Vector4 _wave4Params = new(320f, 2.2f, 0.05f, 2.4f);

        public WaterUnderwaterSurfaceQuality Quality => quality;
        public float TransitionBlend => _transitionBlend;
        public bool UnderwaterRendererEnabled =>
            underwaterRenderer != null && underwaterRenderer.enabled;

        /// <summary>
        /// Evaluates the approximate displaced surface height at a world position.
        /// </summary>
        public float EvaluateSurfaceHeight(Vector3 worldPosition, float time)
        {
            float height = transform.position.y;
            Vector2 worldXZ = new(worldPosition.x, worldPosition.z);
            height += EvaluateWaveHeight(_wave1Params, worldXZ, time);
            height += EvaluateWaveHeight(_wave2Params, worldXZ, time);
            height += EvaluateWaveHeight(_wave3Params, worldXZ, time);
            height += EvaluateWaveHeight(_wave4Params, worldXZ, time);
            return height;
        }

        /// <summary>
        /// Selects the underside renderer or the front-face-only fallback.
        /// </summary>
        public void SetQuality(WaterUnderwaterSurfaceQuality selectedQuality)
        {
            quality = selectedQuality;
            RefreshSurface();
        }

        /// <summary>
        /// Sets the normalized camera transition shared with the underwater volume.
        /// </summary>
        public void SetTransitionBlend(float blend)
        {
            SetTransitionState(blend >= 0.5f, blend);
        }

        /// <summary>
        /// Sets the camera side and normalized transition shared with the underwater volume.
        /// </summary>
        public void SetTransitionState(bool underwater, float blend)
        {
            float clampedBlend = Mathf.Clamp01(blend);
            if (underwater == _cameraUnderwater &&
                Mathf.Approximately(clampedBlend, _transitionBlend))
            {
                return;
            }

            _cameraUnderwater = underwater;
            _transitionBlend = clampedBlend;
            ApplyQuality();
            ApplyRuntimeProperties(true);
        }

        /// <summary>
        /// Reapplies quality, material synchronization, and water-volume properties.
        /// </summary>
        public void RefreshSurface()
        {
            CacheComponents();
            ApplyQuality();
            SynchronizeMaterialProperties();
            ApplyRuntimeProperties(true);
        }

        /// <summary>
        /// Copies the current surface material state to the underwater renderer.
        /// </summary>
        public void SynchronizeFromWaterMaterial()
        {
            CacheComponents();
            SynchronizeMaterialProperties();
            ApplyRuntimeProperties(true);
        }

        private void Awake()
        {
            RefreshSurface();
        }

        private void Update()
        {
            if (_appliedQuality != quality)
                ApplyQuality();
            ApplyRuntimeProperties(false);
        }

        private void OnValidate()
        {
            RefreshSurface();
        }

        private void OnDestroy()
        {
            if (underwaterRenderer != null)
                underwaterRenderer.enabled = false;
        }

        private void CacheComponents()
        {
            if (_waterRenderer == null)
                _waterRenderer = GetComponent<Renderer>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyQuality()
        {
            _appliedQuality = quality;
            if (underwaterRenderer != null)
            {
                underwaterRenderer.enabled =
                    quality == WaterUnderwaterSurfaceQuality.UnderwaterSurface &&
                    _cameraUnderwater;
            }
        }

        private void ApplyRuntimeProperties(bool force)
        {
            if (underwaterRenderer == null)
                return;

            float waterHeight = transform.position.y;
            if (!force && Mathf.Approximately(waterHeight, _appliedWaterHeight))
                return;

            _appliedWaterHeight = waterHeight;
            underwaterRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(WaterHeightId, waterHeight);
            _propertyBlock.SetFloat(TransitionBlendId, _transitionBlend);
            underwaterRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SynchronizeMaterialProperties()
        {
            if (_waterRenderer == null ||
                underwaterRenderer == null ||
                _waterRenderer.sharedMaterial == null)
            {
                return;
            }

            Material source = _waterRenderer.sharedMaterial;
            CacheWaveState(source);
            underwaterRenderer.GetPropertyBlock(_propertyBlock);
            SynchronizeWaveProperties(source);
            SynchronizeNormalProperties(source);
            SynchronizeOpticalProperties(source);
            underwaterRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SynchronizeWaveProperties(Material source)
        {
            CopyVector(source, "_WindDirection");
            CopyFloat(source, "_WindSpread");
            CopyVector(source, "_Wave1Params");
            CopyVector(source, "_Wave2Params");
            CopyVector(source, "_Wave3Params");
            CopyVector(source, "_Wave4Params");
            CopyFloat(source, "_Wave1Steepness");
            CopyFloat(source, "_Wave2Steepness");
            CopyFloat(source, "_Wave3Steepness");
            CopyFloat(source, "_Wave4Steepness");
        }

        private void SynchronizeNormalProperties(Material source)
        {
            CopyTexture(source, "_NormalMapA");
            CopyTexture(source, "_NormalMapB");
            CopyFloat(source, "_NormalLayerATiling");
            CopyFloat(source, "_NormalLayerBTiling");
            CopyFloat(source, "_NormalLayerASpeed");
            CopyFloat(source, "_NormalLayerBSpeed");
            CopyFloat(source, "_NormalLayerBRotation");
            CopyFloat(source, "_MicroWaveStrength");
            CopyFloat(source, "_DetailFadeStart");
            CopyFloat(source, "_DetailFadeEnd");
        }

        private void SynchronizeOpticalProperties(Material source)
        {
            CopyVector(source, "_AbsorptionCoefficients");
            CopyColor(source, "_ScatteringColor");
            CopyFloat(source, "_ScatteringStrength");
            CopyFloat(source, "_FresnelBase");
            CopyFloat(source, "_Roughness");
            CopyFloat(source, "_ReflectionStrength");
            if (source.HasProperty("_ScatteringColor"))
            {
                _propertyBlock.SetColor(
                    FogColorId, source.GetColor("_ScatteringColor"));
            }
        }

        private void CacheWaveState(Material source)
        {
            if (source.HasProperty("_WindDirection"))
                _windDirection = source.GetVector("_WindDirection");
            if (source.HasProperty("_WindSpread"))
                _windSpread = source.GetFloat("_WindSpread");
            if (source.HasProperty("_Wave1Params"))
                _wave1Params = source.GetVector("_Wave1Params");
            if (source.HasProperty("_Wave2Params"))
                _wave2Params = source.GetVector("_Wave2Params");
            if (source.HasProperty("_Wave3Params"))
                _wave3Params = source.GetVector("_Wave3Params");
            if (source.HasProperty("_Wave4Params"))
                _wave4Params = source.GetVector("_Wave4Params");
        }

        private float EvaluateWaveHeight(
            Vector4 packed, Vector2 worldXZ, float time)
        {
            Vector2 wind = new(_windDirection.x, _windDirection.z);
            if (wind.sqrMagnitude < 0.0001f)
                wind = Vector2.right;
            else
                wind.Normalize();

            float windAngle = Mathf.Atan2(wind.y, wind.x);
            float authoredAngle = packed.x * Mathf.Deg2Rad;
            float angleDelta = authoredAngle - windAngle;
            float shortestDelta = Mathf.Atan2(
                Mathf.Sin(angleDelta), Mathf.Cos(angleDelta));
            float waveAngle =
                windAngle + shortestDelta * Mathf.Clamp01(_windSpread);
            Vector2 direction = new(
                Mathf.Cos(waveAngle), Mathf.Sin(waveAngle));
            float wavelength = Mathf.Max(0.05f, packed.y);
            float waveNumber = Mathf.PI * 2f / wavelength;
            float angularFrequency = Mathf.Sqrt(9.81f * waveNumber);
            float phase = waveNumber * Vector2.Dot(direction, worldXZ) +
                time * angularFrequency * Mathf.Max(0f, packed.w);
            return Mathf.Max(0f, packed.z) * Mathf.Sin(phase);
        }

        private void CopyFloat(Material source, string propertyName)
        {
            if (source.HasProperty(propertyName))
            {
                _propertyBlock.SetFloat(
                    propertyName, source.GetFloat(propertyName));
            }
        }

        private void CopyVector(Material source, string propertyName)
        {
            if (source.HasProperty(propertyName))
            {
                _propertyBlock.SetVector(
                    propertyName, source.GetVector(propertyName));
            }
        }

        private void CopyColor(Material source, string propertyName)
        {
            if (source.HasProperty(propertyName))
            {
                _propertyBlock.SetColor(
                    propertyName, source.GetColor(propertyName));
            }
        }

        private void CopyTexture(Material source, string propertyName)
        {
            if (source.HasProperty(propertyName))
            {
                _propertyBlock.SetTexture(
                    propertyName, source.GetTexture(propertyName));
            }
        }
    }
}
