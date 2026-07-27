using System.Collections.Generic;
using Market.Core;
using Market.Economy;
using Market.Market;
using Market.Persistence;
using UnityEngine;

namespace Market.NPC
{
    /// <summary>
    /// Spawns NPCs from a random spawn point.
    /// Traffic density depends on the time of day via <see cref="trafficDensityCurve"/>:
    /// peak at noon, nearly zero at night.
    /// </summary>
    public partial class NPCSpawner : MonoBehaviour
    {
        [Header("NPC Types")]
        [Tooltip("Each new NPC is randomly chosen from this pool.")]
        [SerializeField] private NPCTypeSO[] npcTypes;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Traffic Density")]
        [Tooltip("Traffic density by hour of day.\nX axis: 0..1 = 0..24 h  |  Y axis: 0..1 = density.")]
        [SerializeField] private AnimationCurve trafficDensityCurve = new AnimationCurve(
            new Keyframe( 0f / 24f, 0.00f, 0f, 0f),
            new Keyframe( 6f / 24f, 0.04f, 0f, 0f),
            new Keyframe( 9f / 24f, 0.50f, 0f, 0f),
            new Keyframe(12f / 24f, 1.00f, 0f, 0f),
            new Keyframe(15f / 24f, 0.85f, 0f, 0f),
            new Keyframe(18f / 24f, 0.55f, 0f, 0f),
            new Keyframe(20f / 24f, 0.10f, 0f, 0f),
            new Keyframe(24f / 24f, 0.00f, 0f, 0f)
        );

        [Tooltip("Spawn interval at density = 1 (peak hour).")]
        [SerializeField] private float peakSpawnInterval    = 4f;
        [Tooltip("Spawn interval at density -> 0 (deep night).")]
        [SerializeField] private float offPeakSpawnInterval = 30f;
        [Tooltip("Threshold: spawn stops when density falls below this value.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float minDensityToSpawn    = 0.05f;
        [Tooltip("Maximum active NPCs at density = 1. Scales linearly down.")]
        [SerializeField] private int   maxActiveNPCsAtPeak  = 5;

        [Header("Scene References")]
        [SerializeField] private MarketStallRegistry stallRegistry;
        [SerializeField] private Transform   exitPoint;
        [SerializeField] private MoneySystem playerMoney;

        private TimeSystem       _timeSystem;
        private MarketOpenSystem _marketOpenSystem;
        private int              _activeCount;
        private float            _spawnTimer;
        private bool             _restoredFromSave;
        private readonly List<NPCVisitor> _spawnedVisitors = new();
        // Inactive visitors kept for reuse, bucketed by type (each type has its own prefab).
        private readonly Dictionary<NPCTypeSO, Stack<NPCVisitor>> _pool = new();

        /// <summary>Number of NPCs currently alive in the scene through this spawner.</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Current traffic density [0..1] based on game time.</summary>
        public float CurrentDensity => GetCurrentDensity();

        // -- Lifecycle --------------------------------------------------
        private void Awake()
        {
            ResolveStallRegistry();
            ValidateReferences();

            if (!ServiceLocator.TryGet<TimeSystem>(out _timeSystem))
                Debug.LogWarning("[NPCSpawner] TimeSystem not found -- density will be constant.", this);

            ResolveMarketOpenSystem();
        }

        private void OnEnable()
        {
            WireMarketOpenEvents();
        }

        private void Start()
        {
            if (_restoredFromSave) return;

            _spawnTimer = 0f; // first NPC spawns immediately
        }

        private void Update()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            float density        = GetCurrentDensity();
            float effectiveInterval = ComputeInterval(density);
            _spawnTimer = effectiveInterval;

            if (density < minDensityToSpawn) return;   // night -- no spawning

            int effectiveMax = EffectiveMaxNPCs(density);
            if (_activeCount >= effectiveMax) return;

            TrySpawn();
        }

        /// <summary>
        /// Force-spawn for debug scenarios. Ignores timer and daytime density.
        /// </summary>
        public bool ForceSpawnForDebug()
        {
            return TrySpawn();
        }

        // -- Density helpers --------------------------------------------

        /// <summary>Current traffic density [0..1] based on the game hour.</summary>
        private float GetCurrentDensity()
        {
            if (_timeSystem == null) return 1f;

            float normalizedHour = (_timeSystem.Hour + _timeSystem.Minute / 60f) / 24f;
            return Mathf.Clamp01(trafficDensityCurve.Evaluate(normalizedHour));
        }

        /// <summary>Spawn interval: peakSpawnInterval at peak density, offPeakSpawnInterval at zero.</summary>
        private float ComputeInterval(float density)
        {
            return Mathf.Lerp(offPeakSpawnInterval, peakSpawnInterval, density);
        }

        /// <summary>Max active NPCs scales with density (minimum 1).</summary>
        private int EffectiveMaxNPCs(float density)
        {
            return Mathf.Max(1, Mathf.RoundToInt(maxActiveNPCsAtPeak * density));
        }

        private NPCTypeSO FindType(string npcTypeKey)
        {
            if (npcTypes == null || string.IsNullOrWhiteSpace(npcTypeKey)) return null;

            foreach (NPCTypeSO type in npcTypes)
            {
                if (type == null) continue;
                // Id is the stable key; asset name / TypeName remain fallbacks for legacy saves.
                if (type.Id == npcTypeKey || type.name == npcTypeKey || type.TypeName == npcTypeKey)
                    return type;
            }

            return null;
        }

        // -- Validation -------------------------------------------------
        private bool HasAvailableStall()
        {
            ResolveStallRegistry();
            return stallRegistry != null && stallRegistry.Count > 0;
        }

        private void ResolveStallRegistry()
        {
            if (stallRegistry != null) return;
            ServiceLocator.TryGet<MarketStallRegistry>(out stallRegistry);
        }

        private void ResolveMarketOpenSystem()
        {
            ServiceLocator.TryGet<MarketOpenSystem>(out _marketOpenSystem);
        }

        private void WireMarketOpenEvents()
        {
            ResolveMarketOpenSystem();
            if (_marketOpenSystem != null)
                _marketOpenSystem.OnOpenChanged += HandleMarketOpenChanged;
        }

        private void UnwireMarketOpenEvents()
        {
            if (_marketOpenSystem != null)
                _marketOpenSystem.OnOpenChanged -= HandleMarketOpenChanged;
        }

        private void HandleMarketOpenChanged(bool isOpen)
        {
            if (isOpen)
                return;

            for (int i = _spawnedVisitors.Count - 1; i >= 0; i--)
            {
                NPCVisitor visitor = _spawnedVisitors[i];
                if (visitor == null) continue;

                if (visitor.CurrentState == NPCVisitor.State.WalkToStall ||
                    visitor.CurrentState == NPCVisitor.State.Browsing)
                {
                    visitor.LeaveMarket();
                }
            }
        }

        private void ValidateReferences()
        {
            if (npcTypes == null || npcTypes.Length == 0)
                Debug.LogError("[NPCSpawner] npcTypes not assigned!", this);
            if (spawnPoints == null || spawnPoints.Length == 0)
                Debug.LogError("[NPCSpawner] spawnPoints not assigned!", this);
            if (stallRegistry == null || stallRegistry.Count == 0)
                Debug.LogError("[NPCSpawner] stallRegistry not assigned or empty!", this);
            if (exitPoint == null)
                Debug.LogError("[NPCSpawner] exitPoint not assigned!", this);
            if (playerMoney == null)
                Debug.LogError("[NPCSpawner] playerMoney not assigned!", this);
        }

        // -- Gizmos -----------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;

            Gizmos.color = Color.cyan;
            foreach (var p in spawnPoints)
            {
                if (p == null) continue;
                Gizmos.DrawSphere(p.position, 0.3f);
                Gizmos.DrawWireSphere(p.position, 0.5f);
            }
        }
    }
}
