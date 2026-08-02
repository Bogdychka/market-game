using Market.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Smoothly coordinates realistic-water shader properties from calm water to a storm.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterWeatherController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private RealisticWaterCausticProjection causticProjection;
        [SerializeField] private RealisticWaterUnderwaterSurface underwaterSurface;
        [Tooltip("Optional. When a wave profile drives the surface, weather scales its bank " +
            "instead of the material's legacy four waves.")]
        [SerializeField] private WaveProfileBinder waveProfileBinder;
        [SerializeField] private TextMesh statusLabel;

        [Header("Weather")]
        [SerializeField] private RealisticWaterWeather weather =
            RealisticWaterWeather.Breeze;
        [SerializeField] private Vector2 windDirection =
            new(0.4226f, -0.9063f);
        [SerializeField, Min(0f)] private float transitionDuration = 3f;

        [Header("Debug")]
        [SerializeField] private bool enableLabHotkeys;

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
        private static readonly int NormalLayerASpeedId =
            Shader.PropertyToID("_NormalLayerASpeed");
        private static readonly int NormalLayerBSpeedId =
            Shader.PropertyToID("_NormalLayerBSpeed");
        private static readonly int MicroWaveStrengthId =
            Shader.PropertyToID("_MicroWaveStrength");
        private static readonly int RefractionStrengthId =
            Shader.PropertyToID("_RefractionStrength");
        private static readonly int RoughnessId =
            Shader.PropertyToID("_Roughness");
        private static readonly int FoamCrestGainId =
            Shader.PropertyToID("_FoamCrestGain");
        private static readonly int FoamCrestBiasId =
            Shader.PropertyToID("_FoamCrestBias");
        private static readonly int FoamCrestStrengthId =
            Shader.PropertyToID("_FoamCrestStrength");
        private static readonly int FoamNoiseSpeedId =
            Shader.PropertyToID("_FoamNoiseSpeed");
        private static readonly int CausticIntensityId =
            Shader.PropertyToID("_CausticIntensity");
        private static readonly int CausticSpeedId =
            Shader.PropertyToID("_CausticSpeed");

        private Material _sourceMaterial;
        private Material _runtimeMaterial;
        private RealisticWaterWeather _observedWeather;
        private RealisticWaterWeatherProfile _fromProfile;
        private RealisticWaterWeatherProfile _targetProfile;
        private RealisticWaterWeatherProfile _appliedProfile;
        private float _transitionElapsed;
        private bool _transitioning;

        public RealisticWaterWeather Weather => weather;

        private void Awake()
        {
            CacheReferences();
            EnsureRuntimeMaterial();
            ApplyWeatherImmediate(weather);
        }

        private void Update()
        {
            HandleLabHotkeys();
            DetectInspectorWeatherChange();
            TickTransition();
        }

        private void OnDestroy()
        {
            RestoreSourceMaterial();
        }

        /// <summary>
        /// Starts a smooth transition to the selected water weather state.
        /// </summary>
        public void SetWeather(RealisticWaterWeather selectedWeather)
        {
            weather = selectedWeather;
            _observedWeather = selectedWeather;
            BeginTransition(selectedWeather);
        }

        /// <summary>
        /// Applies the selected water weather state without a transition.
        /// </summary>
        public void SetWeatherImmediate(RealisticWaterWeather selectedWeather)
        {
            CacheReferences();
            EnsureRuntimeMaterial();
            weather = selectedWeather;
            ApplyWeatherImmediate(selectedWeather);
        }

        /// <summary>
        /// Updates the world-space wind direction used by every weather profile.
        /// </summary>
        public void SetWindDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            windDirection = direction.normalized;
            ApplyProfile(_appliedProfile);
        }

        private void CacheReferences()
        {
            if (waterRenderer == null)
                waterRenderer = GetComponent<Renderer>();
            if (causticProjection == null)
                causticProjection = GetComponent<RealisticWaterCausticProjection>();
            if (underwaterSurface == null)
                underwaterSurface = GetComponent<RealisticWaterUnderwaterSurface>();
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null ||
                waterRenderer == null ||
                waterRenderer.sharedMaterial == null)
            {
                return;
            }

            _sourceMaterial = waterRenderer.sharedMaterial;
            _runtimeMaterial = new Material(_sourceMaterial)
            {
                name = $"{_sourceMaterial.name} (Weather Runtime)",
                hideFlags = HideFlags.DontSave,
            };
            waterRenderer.sharedMaterial = _runtimeMaterial;
        }

        private void RestoreSourceMaterial()
        {
            if (_runtimeMaterial == null)
                return;

            if (waterRenderer != null &&
                waterRenderer.sharedMaterial == _runtimeMaterial)
            {
                waterRenderer.sharedMaterial = _sourceMaterial;
            }

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }

        private void HandleLabHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (!enableLabHotkeys || keyboard == null)
                return;

            if (keyboard[Key.LeftBracket].wasPressedThisFrame)
                CycleWeather(-1);
            else if (keyboard[Key.RightBracket].wasPressedThisFrame)
                CycleWeather(1);
        }

        private void CycleWeather(int direction)
        {
            const int StateCount = (int)RealisticWaterWeather.Storm + 1;
            int next = ((int)weather + direction + StateCount) % StateCount;
            SetWeather((RealisticWaterWeather)next);
        }

        private void DetectInspectorWeatherChange()
        {
            if (_observedWeather != weather)
            {
                _observedWeather = weather;
                BeginTransition(weather);
            }
        }

        private void BeginTransition(RealisticWaterWeather selectedWeather)
        {
            EnsureRuntimeMaterial();
            if (_runtimeMaterial == null)
                return;

            _fromProfile = _appliedProfile;
            _targetProfile = RealisticWaterWeatherProfiles.Get(selectedWeather);
            _transitionElapsed = 0f;
            _transitioning = transitionDuration > 0f;
            UpdateStatusLabel(selectedWeather);
            if (!_transitioning)
                ApplyWeatherImmediate(selectedWeather);
        }

        private void TickTransition()
        {
            if (!_transitioning)
                return;

            _transitionElapsed += Time.deltaTime;
            float linearBlend = Mathf.Clamp01(
                _transitionElapsed / transitionDuration);
            float smoothBlend = linearBlend * linearBlend *
                (3f - 2f * linearBlend);
            _appliedProfile = RealisticWaterWeatherProfile.Lerp(
                _fromProfile, _targetProfile, smoothBlend);
            ApplyProfile(_appliedProfile);
            _transitioning = linearBlend < 1f;
        }

        private void ApplyWeatherImmediate(RealisticWaterWeather selectedWeather)
        {
            _observedWeather = selectedWeather;
            _appliedProfile = RealisticWaterWeatherProfiles.Get(selectedWeather);
            _fromProfile = _appliedProfile;
            _targetProfile = _appliedProfile;
            _transitionElapsed = transitionDuration;
            _transitioning = false;
            ApplyProfile(_appliedProfile);
            UpdateStatusLabel(selectedWeather);
        }

        private void ApplyProfile(RealisticWaterWeatherProfile profile)
        {
            if (_runtimeMaterial == null)
                return;

            ApplyWaveProperties(profile);
            ApplySurfaceProperties(profile);
            if (causticProjection != null)
            {
                causticProjection.SetWeatherAppearance(
                    profile.ProjectedCausticIntensity,
                    profile.ProjectedCausticSpeeds);
            }
            if (underwaterSurface != null)
                underwaterSurface.SynchronizeFromWaterMaterial();
        }

        private void ApplyWaveProperties(RealisticWaterWeatherProfile profile)
        {
            Vector2 normalizedWind = windDirection.sqrMagnitude > 0.0001f
                ? windDirection.normalized
                : Vector2.right;
            _runtimeMaterial.SetVector(
                WindDirectionId,
                new Vector4(normalizedWind.x, 0f, normalizedWind.y, 0f));
            _runtimeMaterial.SetFloat(WindSpreadId, profile.WindSpread);
            _runtimeMaterial.SetVector(Wave1ParamsId, profile.Wave1Params);
            _runtimeMaterial.SetVector(Wave2ParamsId, profile.Wave2Params);
            _runtimeMaterial.SetVector(Wave3ParamsId, profile.Wave3Params);
            _runtimeMaterial.SetVector(Wave4ParamsId, profile.Wave4Params);
            _runtimeMaterial.SetFloat(
                Wave1SteepnessId, profile.WaveSteepness.x);
            _runtimeMaterial.SetFloat(
                Wave2SteepnessId, profile.WaveSteepness.y);
            _runtimeMaterial.SetFloat(
                Wave3SteepnessId, profile.WaveSteepness.z);
            _runtimeMaterial.SetFloat(
                Wave4SteepnessId, profile.WaveSteepness.w);

            // With a wave profile bound the four properties above are inert, so the same weather
            // step is expressed as a scale on the profile's bank.
            if (waveProfileBinder != null)
            {
                waveProfileBinder.BankScale =
                    RealisticWaterWeatherProfiles.GetBankScale(profile);
            }
        }

        private void ApplySurfaceProperties(
            RealisticWaterWeatherProfile profile)
        {
            _runtimeMaterial.SetFloat(
                NormalLayerASpeedId, profile.NormalSpeeds.x);
            _runtimeMaterial.SetFloat(
                NormalLayerBSpeedId, profile.NormalSpeeds.y);
            _runtimeMaterial.SetFloat(
                MicroWaveStrengthId, profile.MicroWaveStrength);
            _runtimeMaterial.SetFloat(
                RefractionStrengthId, profile.RefractionStrength);
            _runtimeMaterial.SetFloat(RoughnessId, profile.Roughness);
            _runtimeMaterial.SetFloat(
                FoamCrestGainId, profile.FoamCrestGain);
            _runtimeMaterial.SetFloat(
                FoamCrestBiasId, profile.FoamCrestBias);
            _runtimeMaterial.SetFloat(
                FoamCrestStrengthId, profile.FoamCrestStrength);
            _runtimeMaterial.SetFloat(
                FoamNoiseSpeedId, profile.FoamNoiseSpeed);
            _runtimeMaterial.SetFloat(
                CausticIntensityId, profile.SurfaceCausticIntensity);
            _runtimeMaterial.SetFloat(
                CausticSpeedId, profile.SurfaceCausticSpeed);
        }

        private void UpdateStatusLabel(RealisticWaterWeather selectedWeather)
        {
            if (statusLabel != null)
            {
                statusLabel.text =
                    $"WEATHER: {selectedWeather.ToString().ToUpperInvariant()}  |  BRACKET KEYS TO CHANGE";
            }
        }
    }
}
