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
    public class NPCSpawner : MonoBehaviour
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

        public void CollectActiveVisitors(List<NPCVisitorData> visitors)
        {
            if (visitors == null) return;

            RemoveMissingVisitors();
            foreach (NPCVisitor visitor in _spawnedVisitors)
            {
                if (visitor == null || visitor.Type == null) continue;

                // Only persist visitors that still intend to shop; ones already leaving (WalkToExit/Done)
                // are transient and simply regenerate as new traffic.
                if (visitor.CurrentState != NPCVisitor.State.WalkToStall &&
                    visitor.CurrentState != NPCVisitor.State.Browsing)
                    continue;

                NPCVisitorData data = new()
                {
                    npcTypeKey    = visitor.Type.Id,
                    targetStallId = visitor.TargetStallId
                };

                foreach (MarketStall stall in visitor.VisitedStalls)
                    if (stall != null)
                        data.visitedStallIds.Add(stall.StallId);

                visitors.Add(data);
            }
        }

        public void RestoreActiveVisitors(List<NPCVisitorData> visitors)
        {
            ClearActiveVisitors();
            _restoredFromSave = true;
            _spawnTimer = ComputeInterval(GetCurrentDensity());

            if (_marketOpenSystem != null && !_marketOpenSystem.IsOpen)
                return;

            if (visitors == null || visitors.Count == 0) return;

            foreach (NPCVisitorData visitorData in visitors)
                RestoreVisitor(visitorData);

            Debug.Log($"[NPCSpawner] Restored NPCs from save: {_activeCount}");
        }

        // -- Spawn ------------------------------------------------------
        private bool TrySpawn()
        {
            if (npcTypes == null    || npcTypes.Length == 0)    return false;
            if (spawnPoints == null || spawnPoints.Length == 0) return false;
            if (exitPoint == null || playerMoney == null)
            {
                Debug.LogError("[NPCSpawner] exitPoint/playerMoney not assigned - NPC not created.", this);
                return false;
            }

            bool marketOpen = _marketOpenSystem == null || _marketOpenSystem.IsOpen;
            if (marketOpen && !HasAvailableStall())
            {
                Debug.LogError("[NPCSpawner] stallRegistry not assigned or empty - shopper NPC not created.", this);
                return false;
            }

            var type  = npcTypes[Random.Range(0, npcTypes.Length)];
            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];

            if (type == null)
            {
                Debug.LogError("[NPCSpawner] npcTypes contains a null entry!", this);
                return false;
            }

            if (point == null)
            {
                Debug.LogError("[NPCSpawner] spawnPoints contains a null entry!", this);
                return false;
            }

            if (type.NpcPrefab == null)
            {
                Debug.LogError($"[NPCSpawner] NpcPrefab not assigned in {type.name}!", this);
                return false;
            }

            NPCVisitor visitor = AcquireVisitor(type, point, out bool reused);
            if (visitor == null) return false;

            if (marketOpen)
                visitor.Initialize(type, stallRegistry, exitPoint, playerMoney);
            else
                visitor.InitializePasserby(type, exitPoint, playerMoney);

            RegisterVisitor(visitor);
            if (reused) visitor.Begin();

            string role = marketOpen ? "shopper" : "passerby";
            Debug.Log($"[NPCSpawner] Spawned {role} {type.TypeName} (density={GetCurrentDensity():F2}). " +
                      $"Active: {_activeCount}/{EffectiveMaxNPCs(GetCurrentDensity())}");
            return true;
        }

        private void RestoreVisitor(NPCVisitorData visitorData)
        {
            if (visitorData == null) return;

            NPCTypeSO type = FindType(visitorData.npcTypeKey);
            if (type == null)
            {
                Debug.LogWarning($"[NPCSpawner] NPC type '{visitorData.npcTypeKey}' from save not found.", this);
                return;
            }

            if (type.NpcPrefab == null)
            {
                Debug.LogWarning($"[NPCSpawner] NPC type '{type.name}' has no prefab assigned.", this);
                return;
            }

            if (!HasAvailableStall())
            {
                Debug.LogWarning("[NPCSpawner] No stall available for restored NPC.", this);
                return;
            }

            // Schedule-style restore: re-spawn at an entrance (always a valid navmesh spot) and let the
            // visitor walk in toward its saved target stall -- no mid-stride position to teleport into.
            Transform point = PickSpawnPoint();
            if (point == null)
            {
                Debug.LogWarning("[NPCSpawner] No spawn point for restored NPC.", this);
                return;
            }

            NPCVisitor visitor = AcquireVisitor(type, point, out bool reused);
            if (visitor == null) return;

            visitor.Initialize(type, stallRegistry, exitPoint, playerMoney);
            visitor.RestoreStalls(visitorData.targetStallId, visitorData.visitedStallIds);
            RegisterVisitor(visitor);
            if (reused) visitor.Begin();
        }

        private Transform PickSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }

        /// <summary>
        /// Returns a visitor for the type: reuses a pooled (inactive) one if available, otherwise
        /// instantiates a fresh prefab. Pooling avoids per-spawn Instantiate hitches and the GC churn
        /// of destroying NPCs every time they leave.
        /// </summary>
        private NPCVisitor AcquireVisitor(NPCTypeSO type, Transform point, out bool reused)
        {
            if (_pool.TryGetValue(type, out Stack<NPCVisitor> pooled))
            {
                while (pooled.Count > 0)
                {
                    NPCVisitor candidate = pooled.Pop();
                    if (candidate == null) continue; // destroyed by a scene change -- skip it

                    candidate.gameObject.SetActive(true);
                    candidate.PlaceAt(point.position, point.rotation);
                    reused = true;
                    return candidate;
                }
            }

            reused = false;
            return InstantiateVisitor(type, point);
        }

        /// <summary>Deactivates a finished visitor and returns it to its type pool for reuse.</summary>
        private void ReleaseVisitor(NPCVisitor visitor)
        {
            NPCTypeSO type = visitor.Type;
            if (type == null)
            {
                Destroy(visitor.gameObject);
                return;
            }

            visitor.gameObject.SetActive(false);

            if (!_pool.TryGetValue(type, out Stack<NPCVisitor> pooled))
            {
                pooled = new Stack<NPCVisitor>();
                _pool[type] = pooled;
            }

            pooled.Push(visitor);
        }

        /// <summary>
        /// Instantiates the type's prefab at the point and returns its NPCVisitor.
        /// Returns null (and destroys the GameObject) if the prefab lacks the component.
        /// </summary>
        private NPCVisitor InstantiateVisitor(NPCTypeSO type, Transform point)
        {
            GameObject go = Instantiate(type.NpcPrefab, point.position, point.rotation);
            NPCVisitor visitor = go.GetComponent<NPCVisitor>();
            if (visitor == null)
            {
                Debug.LogError("[NPCSpawner] Prefab does not contain NPCVisitor!", go);
                Destroy(go);
                return null;
            }

            return visitor;
        }

        private void RegisterVisitor(NPCVisitor visitor)
        {
            visitor.OnDespawned += OnVisitorDespawned;
            _spawnedVisitors.Add(visitor);
            _activeCount++;
        }

        private void OnVisitorDespawned(NPCVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.OnDespawned -= OnVisitorDespawned;
                _spawnedVisitors.Remove(visitor);
                ReleaseVisitor(visitor);
            }
            _activeCount = Mathf.Max(0, _activeCount - 1);
        }

        private void ClearActiveVisitors()
        {
            for (int i = _spawnedVisitors.Count - 1; i >= 0; i--)
            {
                NPCVisitor visitor = _spawnedVisitors[i];
                if (visitor == null) continue;

                visitor.OnDespawned -= OnVisitorDespawned;
                ReleaseVisitor(visitor);
            }

            _spawnedVisitors.Clear();
            _activeCount = 0;
        }

        private void OnDisable()
        {
            UnwireMarketOpenEvents();
            UnsubscribeSpawnedVisitors();
        }

        private void UnsubscribeSpawnedVisitors()
        {
            for (int i = _spawnedVisitors.Count - 1; i >= 0; i--)
            {
                var visitor = _spawnedVisitors[i];
                if (visitor != null)
                    visitor.OnDespawned -= OnVisitorDespawned;
            }

            _spawnedVisitors.Clear();
        }

        private void RemoveMissingVisitors()
        {
            for (int i = _spawnedVisitors.Count - 1; i >= 0; i--)
            {
                if (_spawnedVisitors[i] != null) continue;

                _spawnedVisitors.RemoveAt(i);
                _activeCount = Mathf.Max(0, _activeCount - 1);
            }
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
