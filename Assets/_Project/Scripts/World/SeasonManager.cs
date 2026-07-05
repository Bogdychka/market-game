using System;
using Market.Core;
using Market.Core.Events;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Config for one season: solar declination and sky tint.
    /// Indexed by (int)Season: 0=Spring, 1=Summer, 2=Autumn, 3=Winter.
    /// </summary>
    [Serializable]
    public struct SeasonConfig
    {
        [Range(-23.45f, 23.45f)]
        [Tooltip("Solar declination (deg): +23.45 = summer, 0 = equinox, -23.45 = winter.")]
        public float solarDeclination;

        [Tooltip("Sky tint (_SkyTint in Procedural Skybox). Affects atmosphere colour.")]
        public Color skyTint;
    }

    /// <summary>
    /// Manages season transitions.
    /// -- Each season lasts <see cref="daysPerSeason"/> game days.
    /// -- On season change: updates <see cref="DaylightSystem.SetSolarDeclination"/>,
    ///   skybox tint, and publishes <see cref="SeasonChangedEvent"/>.
    /// -- Registered in ServiceLocator -- accessible from SupplierShop, TimeHUD, etc.
    /// </summary>
    public class SeasonManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Duration of one season in game days.")]
        [SerializeField] private int daysPerSeason = 30;

        [Header("Season Config")]
        [Tooltip("Exactly 4 elements: 0=Spring, 1=Summer, 2=Autumn, 3=Winter.")]
        [SerializeField] private SeasonConfig[] seasons = new SeasonConfig[]
        {
            new SeasonConfig { solarDeclination =  13.0f,  skyTint = new Color(0.50f, 0.51f, 0.55f) }, // Spring: sunset ~19:17
            new SeasonConfig { solarDeclination =  23.45f, skyTint = new Color(0.48f, 0.50f, 0.58f) }, // Summer: sunset ~20:33
            new SeasonConfig { solarDeclination =   8.0f,  skyTint = new Color(0.54f, 0.51f, 0.47f) }, // Autumn: sunset ~18:45
            new SeasonConfig { solarDeclination = -23.45f, skyTint = new Color(0.46f, 0.47f, 0.55f) }, // Winter: sunset ~15:27
        };

        [Header("References")]
        [Tooltip("Required for updating solar declination on season change.")]
        [SerializeField] private DaylightSystem daylightSystem;

        /// <summary>Current season.</summary>
        public Season CurrentSeason { get; private set; }

        /// <summary>Day within the current season (1..daysPerSeason).</summary>
        public int DayInCurrentSeason
        {
            get
            {
                int day = _timeSystem?.Day ?? 1;
                return (day - 1) % (daysPerSeason * 4) % daysPerSeason + 1;
            }
        }

        /// <summary>Days remaining until the next season.</summary>
        public int DaysUntilNextSeason => daysPerSeason - DayInCurrentSeason + 1;

        /// <summary>Fired on season change (not fired on start).</summary>
        public event Action<Season> OnSeasonChanged;

        private TimeSystem _timeSystem;
        private EventBus   _eventBus;
        private Material   _skyboxMaterial;
        private static readonly int SkyTintID = Shader.PropertyToID("_SkyTint");

        // -- Lifecycle --------------------------------------------------
        private void Awake()
        {
            ServiceLocator.Register(this);

            if (!ServiceLocator.TryGet<TimeSystem>(out _timeSystem))
                Debug.LogWarning("[SeasonManager] TimeSystem not found.", this);

            ServiceLocator.TryGet<EventBus>(out _eventBus);

            if (daylightSystem == null)
                Debug.LogWarning("[SeasonManager] daylightSystem not assigned -- declination will not change.", this);
        }

        private void Start()
        {
            // DaylightSystem.Awake already created the runtime skybox instance -- grab it here
            _skyboxMaterial = RenderSettings.skybox;

            // Apply starting season without firing an event
            ApplySeason(ComputeSeason(_timeSystem?.Day ?? 1), fireEvent: false);
        }

        private void OnEnable()
        {
            if (_timeSystem != null) _timeSystem.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            if (_timeSystem != null) _timeSystem.OnDayChanged -= HandleDayChanged;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<SeasonManager>();
        }

        /// <summary>
        /// Recomputes the season from the current TimeSystem day. Called after loading a save
        /// when the day may have changed without triggering OnDayChanged.
        /// </summary>
        public void RefreshSeason()
        {
            Season s = ComputeSeason(_timeSystem?.Day ?? 1);
            ApplySeason(s, fireEvent: s != CurrentSeason);
        }

        // -- Season logic -----------------------------------------------
        private void HandleDayChanged(int day)
        {
            Season newSeason = ComputeSeason(day);
            if (newSeason == CurrentSeason) return;
            ApplySeason(newSeason, fireEvent: true);
        }

        private void ApplySeason(Season season, bool fireEvent)
        {
            CurrentSeason = season;
            SeasonConfig cfg = GetConfig(season);

            if (daylightSystem != null)
                daylightSystem.SetSolarDeclination(cfg.solarDeclination);

            if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(SkyTintID))
                _skyboxMaterial.SetColor(SkyTintID, cfg.skyTint);

            Debug.Log($"[SeasonManager] {GetName(season)} " +
                      $"(declination: {cfg.solarDeclination:+0.0;-0.0} deg, day {DayInCurrentSeason}/{daysPerSeason})");

            if (!fireEvent) return;
            OnSeasonChanged?.Invoke(season);
            _eventBus?.Publish(new SeasonChangedEvent(season));
        }

        // -- Helpers ----------------------------------------------------
        private Season ComputeSeason(int day)
        {
            int cycleLength = daysPerSeason * 4;
            int dayInCycle  = (day - 1) % cycleLength;
            return (Season)(dayInCycle / daysPerSeason);
        }

        private SeasonConfig GetConfig(Season s)
        {
            int i = (int)s;
            return (seasons != null && i >= 0 && i < seasons.Length) ? seasons[i] : default;
        }

        /// <summary>Localised season name (player-facing).</summary>
        public static string GetName(Season s) => s switch
        {
            Season.Spring => "Spring",
            Season.Summer => "Summer",
            Season.Autumn => "Autumn",
            Season.Winter => "Winter",
            _             => s.ToString()
        };
    }
}
