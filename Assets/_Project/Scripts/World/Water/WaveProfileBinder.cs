using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Drives the water shaders from a <see cref="WaveProfile"/> asset and answers wave-height
    /// queries for gameplay. Put one on the water surface object: with a profile assigned the
    /// shaders read its layers, with the field empty they fall back to the four legacy wave
    /// properties on the material, so removing the component changes nothing else.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WaveProfileBinder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Wave bank uploaded to the water shaders. Empty = the material's legacy waves.")]
        [SerializeField] private WaveProfile _profile;

        [Tooltip("Water material the wind settings are read from. Empty = the renderer on this " +
            "object.")]
        [SerializeField] private Material _windSourceMaterial;

        [Header("Settings")]
        [Tooltip("Re-upload every frame. Needed while tuning a profile in the editor or when a " +
            "system changes the bank at runtime; a static bank only needs the upload on enable.")]
        [SerializeField] private bool _uploadEveryFrame = true;

        [Tooltip("Still water level used by the sampling API. Empty = this object's Y.")]
        [SerializeField] private bool _useTransformAsWaterLevel = true;

        [Tooltip("Still water level in world units when the transform is not used.")]
        [SerializeField] private float _waterLevel;

        private static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
        private static readonly int WindSpreadId = Shader.PropertyToID("_WindSpread");

        private readonly List<ResolvedWaveLayer> _sampleLayers = new(WaveProfile.MaxLayers);

        private Renderer _renderer;
        private WaveWindSettings _wind = WaveWindSettings.Default;
        private Vector3 _bankScale = Vector3.one;
        private bool _sampleLayersDirty = true;

        /// <summary>Wave bank this binder uploads.</summary>
        public WaveProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                _sampleLayersDirty = true;
                UploadProfile();
            }
        }

        /// <summary>Still water level the sampling API measures from.</summary>
        public float WaterLevel =>
            _useTransformAsWaterLevel ? transform.position.y : _waterLevel;

        /// <summary>Wind steering last read from the water material.</summary>
        public WaveWindSettings Wind => _wind;

        /// <summary>
        /// Runtime scale on (wavelength, amplitude, steepness) applied on top of the profile.
        /// This is how a weather state or a quality tier drives a shared profile asset without
        /// writing to it - the asset stays the authored baseline.
        /// </summary>
        public Vector3 BankScale
        {
            get => _bankScale;
            set
            {
                Vector3 clamped = new(
                    Mathf.Max(0.001f, value.x),
                    Mathf.Max(0f, value.y),
                    Mathf.Max(0f, value.z));

                if (clamped == _bankScale)
                    return;

                _bankScale = clamped;
                UploadProfile();
            }
        }

        /// <summary>
        /// Returns the world-space water height at a position, matching the rendered surface.
        /// Shoaling near a shore is not applied - it lives in the shader's depth map - so use this
        /// in open water.
        /// </summary>
        public float SampleHeight(Vector3 worldPosition)
        {
            EnsureSampleLayers();
            return WaveSampler.SampleHeight(
                _sampleLayers,
                _wind,
                new Vector2(worldPosition.x, worldPosition.z),
                CurrentTime,
                WaterLevel,
                _profile != null ? _profile.SteepnessClamping : WaveSampler.DefaultFoldLimit);
        }

        /// <summary>Returns the surface normal at a position, matching the rendered surface.</summary>
        public Vector3 SampleNormal(Vector3 worldPosition)
        {
            EnsureSampleLayers();
            return WaveSampler.SampleNormal(
                _sampleLayers,
                _wind,
                new Vector2(worldPosition.x, worldPosition.z),
                CurrentTime,
                _profile != null ? _profile.SteepnessClamping : WaveSampler.DefaultFoldLimit);
        }

        /// <summary>
        /// Re-reads the profile and pushes it to the shaders. Call after editing a profile from
        /// code; the editor tools call it on every change.
        /// </summary>
        public void UploadProfile()
        {
            _sampleLayersDirty = true;
            ReadWindFromMaterial();
            EnsureSampleLayers();

            if (_sampleLayers.Count == 0)
            {
                WaveShaderBridge.Clear();
                return;
            }

            WaveShaderBridge.Upload(
                _sampleLayers,
                _profile != null ? _profile.SteepnessClamping : WaveSampler.DefaultFoldLimit);
        }

        // The shaders animate on _Time.y, which is time since level load. Sampling on anything
        // else (Time.time, unscaled time) would put the CPU surface a fixed offset away from the
        // drawn one, which reads as objects floating beside the wave instead of on it.
        private static float CurrentTime => Time.timeSinceLevelLoad;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            _renderer = _renderer != null ? _renderer : GetComponent<Renderer>();
            UploadProfile();
        }

        private void OnDisable()
        {
            WaveShaderBridge.Clear();
        }

        private void LateUpdate()
        {
            if (!_uploadEveryFrame)
                return;

            UploadProfile();
        }

        private void OnValidate()
        {
            _sampleLayersDirty = true;
            if (isActiveAndEnabled)
                UploadProfile();
        }

        private void EnsureSampleLayers()
        {
            if (!_sampleLayersDirty)
                return;

            _sampleLayers.Clear();
            _sampleLayersDirty = false;

            if (_profile == null)
                return;

            _profile.ResolveLayers(_sampleLayers);

            if (_bankScale == Vector3.one)
                return;

            for (int i = 0; i < _sampleLayers.Count; i++)
                _sampleLayers[i] = _sampleLayers[i].Scaled(_bankScale);
        }

        private void ReadWindFromMaterial()
        {
            Material source = ResolveWindSource();
            if (source == null)
                return;

            Vector2 direction = _wind.Direction;
            float spread = _wind.Spread;

            if (source.HasProperty(WindDirectionId))
            {
                Vector4 windDirection = source.GetVector(WindDirectionId);
                direction = new Vector2(windDirection.x, windDirection.z);
            }

            if (source.HasProperty(WindSpreadId))
                spread = source.GetFloat(WindSpreadId);

            _wind = new WaveWindSettings(direction, spread);
        }

        private Material ResolveWindSource()
        {
            if (_windSourceMaterial != null)
                return _windSourceMaterial;

            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            // Shared, not instanced: the wind values are authored on the asset, and touching
            // .material in edit mode would leak a material instance per domain reload.
            return _renderer != null ? _renderer.sharedMaterial : null;
        }
    }
}
