using System;
using Market.Core;
using Market.Core.Events;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Конфигурация одного сезона: солнечное склонение и оттенок неба.
    /// Индексируется по (int)Season: 0=Весна, 1=Лето, 2=Осень, 3=Зима.
    /// </summary>
    [Serializable]
    public struct SeasonConfig
    {
        [Range(-23.45f, 23.45f)]
        [Tooltip("Солнечное склонение (°): +23.45 = лето, 0 = равноденствие, -23.45 = зима.")]
        public float solarDeclination;

        [Tooltip("Оттенок неба (_SkyTint в Procedural Skybox). Влияет на цвет атмосферы.")]
        public Color skyTint;
    }

    /// <summary>
    /// Управляет сменой времён года.
    /// — Каждый сезон длится <see cref="daysPerSeason"/> игровых дней.
    /// — При смене сезона: обновляет <see cref="DaylightSystem.SetSolarDeclination"/>,
    ///   skybox-tint и публикует <see cref="SeasonChangedEvent"/>.
    /// — Регистрируется в ServiceLocator — доступен для SupplierShop, TimeHUD и т.д.
    /// </summary>
    public class SeasonManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность одного сезона в игровых днях.")]
        [SerializeField] private int daysPerSeason = 30;

        [Header("Season Config")]
        [Tooltip("Ровно 4 элемента: 0=Весна, 1=Лето, 2=Осень, 3=Зима.")]
        [SerializeField] private SeasonConfig[] seasons = new SeasonConfig[]
        {
            new SeasonConfig { solarDeclination =  13.0f, skyTint = new Color(0.50f, 0.51f, 0.55f) }, // Весна: закат ~19:17
            new SeasonConfig { solarDeclination =  23.45f, skyTint = new Color(0.48f, 0.50f, 0.58f) }, // Лето:  закат ~20:33
            new SeasonConfig { solarDeclination =   8.0f, skyTint = new Color(0.54f, 0.51f, 0.47f) }, // Осень: закат ~18:45
            new SeasonConfig { solarDeclination = -23.45f, skyTint = new Color(0.46f, 0.47f, 0.55f) }, // Зима:  закат ~15:27
        };

        [Header("References")]
        [Tooltip("Необходим для обновления солнечного склонения при смене сезона.")]
        [SerializeField] private DaylightSystem daylightSystem;

        /// <summary>Текущий сезон.</summary>
        public Season CurrentSeason { get; private set; }

        /// <summary>День внутри текущего сезона (1..daysPerSeason).</summary>
        public int DayInCurrentSeason
        {
            get
            {
                int day = _timeSystem?.Day ?? 1;
                return (day - 1) % (daysPerSeason * 4) % daysPerSeason + 1;
            }
        }

        /// <summary>Сколько дней осталось до следующего сезона.</summary>
        public int DaysUntilNextSeason => daysPerSeason - DayInCurrentSeason + 1;

        /// <summary>Срабатывает при смене сезона (не вызывается при старте).</summary>
        public event Action<Season> OnSeasonChanged;

        private TimeSystem _timeSystem;
        private EventBus   _eventBus;
        private Material   _skyboxMaterial;
        private static readonly int SkyTintID = Shader.PropertyToID("_SkyTint");

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            ServiceLocator.Register(this);

            if (!ServiceLocator.TryGet<TimeSystem>(out _timeSystem))
                Debug.LogWarning("[SeasonManager] TimeSystem не найден.", this);

            ServiceLocator.TryGet<EventBus>(out _eventBus);

            if (daylightSystem == null)
                Debug.LogWarning("[SeasonManager] daylightSystem не назначен — склонение не будет меняться.", this);
        }

        private void Start()
        {
            // DaylightSystem.Awake уже создал runtime-инстанс skybox — берём его здесь
            _skyboxMaterial = RenderSettings.skybox;

            // Применяем стартовый сезон без события
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
        /// Пересчитывает сезон по текущему дню TimeSystem. Вызывается после загрузки сейва,
        /// когда день мог измениться в обход OnDayChanged.
        /// </summary>
        public void RefreshSeason()
        {
            Season s = ComputeSeason(_timeSystem?.Day ?? 1);
            ApplySeason(s, fireEvent: s != CurrentSeason);
        }

        // ── Season logic ───────────────────────────────────────────────
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
                      $"(склонение: {cfg.solarDeclination:+0.0;-0.0}°, день {DayInCurrentSeason}/{daysPerSeason})");

            if (!fireEvent) return;
            OnSeasonChanged?.Invoke(season);
            _eventBus?.Publish(new SeasonChangedEvent(season));
        }

        // ── Helpers ────────────────────────────────────────────────────
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

        /// <summary>Локализованное название сезона.</summary>
        public static string GetName(Season s) => s switch
        {
            Season.Spring => "Весна",
            Season.Summer => "Лето",
            Season.Autumn => "Осень",
            Season.Winter => "Зима",
            _             => s.ToString()
        };
    }
}
