using Market.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Market.World
{
    /// <summary>
    /// Реалистичное движение солнца и луны по игровому времени.
    ///
    /// Использует астрономическую формулу высоты светила:
    ///   sin(alt) = sin(lat)·sin(decl) + cos(lat)·cos(decl)·cos(HA)
    /// где HA = часовой угол от солнечного полудня.
    ///
    /// — <see cref="solarDeclination"/> задаёт сезон: +23.45° = лето, -23.45° = зима.
    ///   SeasonManager вызывает <see cref="SetSolarDeclination"/> при смене сезона.
    /// — Широта 55° (умеренный север): летний закат ~20:30, зимний ~16:00.
    /// — Луна — только directional light, без визуальной сферы.
    /// — Skybox exposure и ambient меняются по высоте солнца.
    /// </summary>
    public class DaylightSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;

        [Header("Geography")]
        [Range(0f, 89f)]
        [Tooltip("Широта. 55 = Россия/Сев. Европа. Влияет на угол дуги и длину дня.")]
        [SerializeField] private float latitude = 55f;

        [Header("Season")]
        [Range(-23.45f, 23.45f)]
        [Tooltip("Солнечное склонение (°). +23.45 = лето, 0 = равноденствие, -23.45 = зима.\n" +
                 "SeasonManager задаёт это значение через SetSolarDeclination().")]
        [SerializeField] private float solarDeclination = 20f;

        [Header("Sun")]
        [SerializeField] private float maxSunIntensity = 1.3f;
        [SerializeField] private Color sunNoonColor    = new Color(1f, 0.97f, 0.88f);
        [SerializeField] private Color sunHorizonColor = new Color(1f, 0.45f, 0.15f);

        [Header("Moon")]
        [SerializeField] private float maxMoonIntensity = 0.85f;
        [SerializeField] private Color moonColor        = new Color(0.75f, 0.85f, 1f);
        [Tooltip("Длительность лунного цикла в игровых днях. 28 = реалистично.")]
        [SerializeField] private float lunarCycleDays   = 28f;
        [Range(0f, 1f)]
        [Tooltip("Минимальная яркость луны в новолуние.")]
        [SerializeField] private float minIllumination  = 0.18f;
        [Range(0f, 1f)]
        [Tooltip("Стартовая фаза луны. 0.5 = день 1 будет полнолунием.")]
        [SerializeField] private float lunarPhaseOffset = 0.5f;

        [Header("Ambient")]
        [SerializeField] private Color nightAmbient    = new Color(0.15f, 0.16f, 0.24f);
        [SerializeField] private Color moonlitAmbient  = new Color(0.24f, 0.28f, 0.38f);
        [SerializeField] private Color twilightAmbient = new Color(0.30f, 0.18f, 0.12f);
        [SerializeField] private Color dayAmbient      = new Color(0.45f, 0.45f, 0.55f);

        [Header("Night Visibility")]
        [Range(0f, 1f)]
        [Tooltip("Насколько сильно луна поднимает ночной ambient.")]
        [SerializeField] private float moonAmbientInfluence = 0.75f;
        [Range(0f, 1f)]
        [Tooltip("Минимальная reflection intensity ночью, чтобы сцена не проваливалась в чёрный.")]
        [SerializeField] private float nightReflectionIntensity = 0.18f;

        [Header("Skybox")]
        [SerializeField] private bool  controlSkyboxExposure = true;
        [SerializeField] private float skyboxNightExposure   = 0.28f;
        [SerializeField] private float skyboxDayExposure     = 1f;

        private TimeSystem _timeSystem;
        private Material   _skyboxInstance;
        private bool       _skyboxHasExposure;
        private static readonly int ExposureID = Shader.PropertyToID("_Exposure");

        // ── Public API для SeasonManager ──────────────────────────────
        /// <summary>
        /// Устанавливает солнечное склонение. Вызывается SeasonManager при смене сезона.
        /// </summary>
        public void SetSolarDeclination(float degrees)
        {
            solarDeclination = Mathf.Clamp(degrees, -23.45f, 23.45f);
        }

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            ResolveTimeSystem();

            if (sunLight == null)
                Debug.LogError("[DaylightSystem] sunLight не назначен", this);

            RenderSettings.ambientMode = AmbientMode.Flat;
            if (sunLight != null) RenderSettings.sun = sunLight;

            InstanceSkyboxMaterial();
        }

        private void OnDestroy()
        {
            if (_skyboxInstance != null) Destroy(_skyboxInstance);
        }

        private void Update()
        {
            if (_timeSystem == null || sunLight == null) return;

            float t       = (_timeSystem.Hour + _timeSystem.Minute / 60f) / 24f;
            float latRad  = latitude         * Mathf.Deg2Rad;
            float declRad = solarDeclination * Mathf.Deg2Rad;

            // Часовой угол: 0 = солнечный полдень, ±π = полночь
            float ha = (t - 0.5f) * Mathf.PI * 2f;

            Vector3 sunPos    = SkyPosition(ha, latRad, declRad);
            float   sunHeight = sunPos.y;

            UpdateSun(sunPos, sunHeight);
            float moonVisibility = UpdateMoon(ha, latRad, declRad);
            UpdateEnvironment(sunHeight, moonVisibility);
        }

        private void ResolveTimeSystem()
        {
            if (ServiceLocator.TryGet<TimeSystem>(out _timeSystem)) return;

            Debug.LogWarning("[DaylightSystem] TimeSystem не найден — освещение будет ждать сервис.", this);
        }

        // ── Setup ──────────────────────────────────────────────────────
        private void InstanceSkyboxMaterial()
        {
            if (!controlSkyboxExposure || RenderSettings.skybox == null) return;

            _skyboxInstance    = new Material(RenderSettings.skybox) { name = "Skybox (Runtime Instance)" };
            RenderSettings.skybox = _skyboxInstance;
            _skyboxHasExposure = _skyboxInstance.HasProperty(ExposureID);
        }

        // ── Sun ────────────────────────────────────────────────────────
        private void UpdateSun(Vector3 sunPos, float sunHeight)
        {
            Color color = Color.Lerp(
                sunHorizonColor,
                sunNoonColor,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunHeight * 3f))
            );

            ApplyLightDirection(sunLight, sunPos, color, Mathf.Clamp01(sunHeight) * maxSunIntensity);
        }

        // ── Moon ───────────────────────────────────────────────────────
        private float UpdateMoon(float sunHA, float latRad, float declRad)
        {
            if (moonLight == null) return 0f;

            float lunarPhase = lunarCycleDays > 0f
                ? (((_timeSystem.Day - 1) / lunarCycleDays) + lunarPhaseOffset) % 1f
                : 0.5f;

            float illumination = (1f - Mathf.Cos(lunarPhase * Mathf.PI * 2f)) * 0.5f;
            illumination = Mathf.Lerp(minIllumination, 1f, illumination);

            float   moonHA     = sunHA - lunarPhase * Mathf.PI * 2f;
            Vector3 moonPos    = SkyPosition(moonHA, latRad, declRad);
            float   moonHeight = moonPos.y;
            float   visibility = Mathf.Clamp01(moonHeight) * illumination;

            ApplyLightDirection(moonLight, moonPos, moonColor,
                visibility * maxMoonIntensity);

            return visibility;
        }

        // ── Environment ─────────────────────────────────────────────────
        private void UpdateEnvironment(float sunHeight, float moonVisibility)
        {
            RenderSettings.ambientLight        = CalculateAmbient(sunHeight, moonVisibility);
            RenderSettings.reflectionIntensity = CalculateReflectionIntensity(sunHeight, moonVisibility);
            UpdateSkyboxExposure(sunHeight, moonVisibility);
        }

        private void UpdateSkyboxExposure(float sunHeight, float moonVisibility)
        {
            if (_skyboxInstance == null || !_skyboxHasExposure) return;

            float t = Mathf.Clamp01(sunHeight + 0.2f) / 1.2f;
            float exposure = Mathf.Lerp(skyboxNightExposure, skyboxDayExposure, t);
            exposure += moonVisibility * 0.16f;
            _skyboxInstance.SetFloat(ExposureID, exposure);
        }

        private Color CalculateAmbient(float sunHeight, float moonVisibility)
        {
            if (sunHeight > 0.15f)
            {
                float dayFactor = Mathf.Clamp01((sunHeight - 0.15f) / 0.35f);
                return Color.Lerp(twilightAmbient, dayAmbient, dayFactor);
            }
            if (sunHeight > -0.15f)
            {
                float twilightFactor = (sunHeight + 0.15f) / 0.30f;
                return Color.Lerp(nightAmbient, twilightAmbient, twilightFactor);
            }

            float moonFactor = Mathf.Clamp01(moonVisibility * moonAmbientInfluence);
            return Color.Lerp(nightAmbient, moonlitAmbient, moonFactor);
        }

        private float CalculateReflectionIntensity(float sunHeight, float moonVisibility)
        {
            float daylightReflection = Mathf.Clamp01(sunHeight + 0.1f);
            float nightReflection = nightReflectionIntensity + moonVisibility * 0.12f;
            return Mathf.Clamp01(Mathf.Max(daylightReflection, nightReflection));
        }

        // ── Астрономическая математика ─────────────────────────────────

        /// <summary>
        /// Возвращает направление на светило в мировом пространстве (x=восток, y=вверх, z=север).
        /// Использует формулу высоты: sin(alt) = sin(φ)·sin(δ) + cos(φ)·cos(δ)·cos(HA).
        /// </summary>
        private static Vector3 SkyPosition(float hourAngle, float latRad, float declRad)
        {
            float sinLat  = Mathf.Sin(latRad);
            float cosLat  = Mathf.Cos(latRad);
            float sinDecl = Mathf.Sin(declRad);
            float cosDecl = Mathf.Cos(declRad);
            float cosHA   = Mathf.Cos(hourAngle);
            float sinHA   = Mathf.Sin(hourAngle);

            float sinAlt = sinLat * sinDecl + cosLat * cosDecl * cosHA;
            float cosAlt = Mathf.Sqrt(Mathf.Max(0f, 1f - sinAlt * sinAlt));

            const float eps = 1e-5f;
            float sinAz = -cosDecl * sinHA / (cosAlt + eps);
            float cosAz = (sinDecl - sinLat * sinAlt) / (cosLat * cosAlt + eps);

            return new Vector3(sinAz * cosAlt, sinAlt, cosAz * cosAlt);
        }

        private static void ApplyLightDirection(Light light, Vector3 skyPos, Color color, float intensity)
        {
            Vector3 lightDir = -skyPos.normalized;
            Vector3 up = Mathf.Abs(lightDir.y) > 0.99f ? Vector3.forward : Vector3.up;

            light.transform.rotation = Quaternion.LookRotation(lightDir, up);
            light.color     = color;
            light.intensity = intensity;
        }
    }
}
