using System.Collections.Generic;
using Market.Core;
using Market.Economy;
using Market.Market;
using Market.Persistence;
using UnityEngine;

namespace Market.NPC
{
    /// <summary>
    /// Спавнит NPC из случайной точки.
    /// Плотность трафика зависит от времени суток через <see cref="trafficDensityCurve"/>:
    /// пик в полдень, почти ноль ночью.
    /// </summary>
    public class NPCSpawner : MonoBehaviour
    {
        [Header("NPC Types")]
        [Tooltip("Из этого пула случайно выбирается тип каждого нового NPC.")]
        [SerializeField] private NPCTypeSO[] npcTypes;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Traffic Density")]
        [Tooltip("Плотность трафика по часу суток.\nOsь X: 0..1 = 0..24 ч  |  Ось Y: 0..1 = плотность.")]
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

        [Tooltip("Интервал спавна при плотности = 1 (час пик).")]
        [SerializeField] private float peakSpawnInterval    = 4f;
        [Tooltip("Интервал спавна при плотности → 0 (глубокая ночь).")]
        [SerializeField] private float offPeakSpawnInterval = 30f;
        [Tooltip("Порог: если плотность ниже — спавн остановлен.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float minDensityToSpawn    = 0.05f;
        [Tooltip("Максимум активных NPC при плотности = 1. Масштабируется линейно вниз.")]
        [SerializeField] private int   maxActiveNPCsAtPeak  = 5;

        [Header("Scene References")]
        [SerializeField] private MarketStall targetStall;
        [SerializeField] private Transform   exitPoint;
        [SerializeField] private MoneySystem playerMoney;

        private TimeSystem _timeSystem;
        private int        _activeCount;
        private float      _spawnTimer;
        private bool       _restoredFromSave;
        private readonly List<NPCVisitor> _spawnedVisitors = new();

        /// <summary>Количество NPC, которые сейчас живут в сцене через этот спавнер.</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Текущая плотность трафика [0..1] по игровому времени.</summary>
        public float CurrentDensity => GetCurrentDensity();

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            ValidateReferences();

            if (!ServiceLocator.TryGet<TimeSystem>(out _timeSystem))
                Debug.LogWarning("[NPCSpawner] TimeSystem не найден — плотность будет постоянной.", this);
        }

        private void Start()
        {
            if (_restoredFromSave) return;

            _spawnTimer = 0f; // первый NPC появится сразу
        }

        private void Update()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            float density        = GetCurrentDensity();
            float effectiveInterval = ComputeInterval(density);
            _spawnTimer = effectiveInterval;

            if (density < minDensityToSpawn) return;   // ночь — не спавним

            int effectiveMax = EffectiveMaxNPCs(density);
            if (_activeCount >= effectiveMax) return;

            TrySpawn();
        }

        /// <summary>
        /// Принудительный спавн для debug-сценариев. Игнорирует таймер и дневную плотность.
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
                if (visitor == null || visitor.Type == null || visitor.CurrentState == NPCVisitor.State.Done)
                    continue;

                Vector3 position = visitor.transform.position;
                visitors.Add(new NPCVisitorData
                {
                    npcTypeKey  = visitor.Type.name,
                    state       = (int)visitor.CurrentState,
                    x           = position.x,
                    y           = position.y,
                    z           = position.z,
                    rotationY   = visitor.transform.eulerAngles.y,
                    browseTimer = visitor.BrowseTimer
                });
            }
        }

        public void RestoreActiveVisitors(List<NPCVisitorData> visitors)
        {
            ClearActiveVisitors();
            _restoredFromSave = true;
            _spawnTimer = ComputeInterval(GetCurrentDensity());

            if (visitors == null || visitors.Count == 0) return;

            foreach (NPCVisitorData visitorData in visitors)
                RestoreVisitor(visitorData);

            Debug.Log($"[NPCSpawner] Восстановлено NPC из сейва: {_activeCount}");
        }

        // ── Spawn ──────────────────────────────────────────────────────
        private bool TrySpawn()
        {
            if (npcTypes == null    || npcTypes.Length == 0)    return false;
            if (spawnPoints == null || spawnPoints.Length == 0) return false;
            if (targetStall == null || exitPoint == null || playerMoney == null)
            {
                Debug.LogError("[NPCSpawner] targetStall/exitPoint/playerMoney не назначены — NPC не создан.", this);
                return false;
            }

            var type  = npcTypes[Random.Range(0, npcTypes.Length)];
            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];

            if (type == null)
            {
                Debug.LogError("[NPCSpawner] В npcTypes есть пустой элемент!", this);
                return false;
            }

            if (point == null)
            {
                Debug.LogError("[NPCSpawner] В spawnPoints есть пустой элемент!", this);
                return false;
            }

            if (type.NpcPrefab == null)
            {
                Debug.LogError($"[NPCSpawner] NpcPrefab не назначен в {type.name}!", this);
                return false;
            }

            var go      = Instantiate(type.NpcPrefab, point.position, point.rotation);
            var visitor = go.GetComponent<NPCVisitor>();

            if (visitor == null)
            {
                Debug.LogError("[NPCSpawner] Префаб не содержит NPCVisitor!", go);
                Destroy(go);
                return false;
            }

            visitor.Initialize(type, targetStall, exitPoint, playerMoney);
            RegisterVisitor(visitor);

            Debug.Log($"[NPCSpawner] Заспавнен {type.TypeName} (density={GetCurrentDensity():F2}). " +
                      $"Активных: {_activeCount}/{EffectiveMaxNPCs(GetCurrentDensity())}");
            return true;
        }

        private void RestoreVisitor(NPCVisitorData visitorData)
        {
            if (visitorData == null) return;
            if (visitorData.state == (int)NPCVisitor.State.Done) return;

            NPCTypeSO type = FindType(visitorData.npcTypeKey);
            if (type == null)
            {
                Debug.LogWarning($"[NPCSpawner] Тип NPC '{visitorData.npcTypeKey}' из сейва не найден.", this);
                return;
            }

            if (type.NpcPrefab == null)
            {
                Debug.LogWarning($"[NPCSpawner] У типа NPC '{type.name}' не назначен prefab.", this);
                return;
            }

            Vector3 position = new Vector3(visitorData.x, visitorData.y, visitorData.z);
            Quaternion rotation = Quaternion.Euler(0f, visitorData.rotationY, 0f);
            GameObject go = Instantiate(type.NpcPrefab, position, rotation);
            NPCVisitor visitor = go.GetComponent<NPCVisitor>();

            if (visitor == null)
            {
                Debug.LogError("[NPCSpawner] Префаб из сейва не содержит NPCVisitor!", go);
                Destroy(go);
                return;
            }

            visitor.Initialize(type, targetStall, exitPoint, playerMoney);
            visitor.RestoreState((NPCVisitor.State)visitorData.state, visitorData.browseTimer, position, visitorData.rotationY);
            RegisterVisitor(visitor);
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
                Destroy(visitor.gameObject);
            }

            _spawnedVisitors.Clear();
            _activeCount = 0;
        }

        private void OnDisable()
        {
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

        // ── Density helpers ────────────────────────────────────────────

        /// <summary>Текущая плотность трафика [0..1] по игровому часу.</summary>
        private float GetCurrentDensity()
        {
            if (_timeSystem == null) return 1f;

            float normalizedHour = (_timeSystem.Hour + _timeSystem.Minute / 60f) / 24f;
            return Mathf.Clamp01(trafficDensityCurve.Evaluate(normalizedHour));
        }

        /// <summary>Интервал спавна: на пике — peakSpawnInterval, на нуле — offPeakSpawnInterval.</summary>
        private float ComputeInterval(float density)
        {
            return Mathf.Lerp(offPeakSpawnInterval, peakSpawnInterval, density);
        }

        /// <summary>Максимум активных NPC масштабируется с плотностью (минимум 1).</summary>
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
                if (type.name == npcTypeKey || type.TypeName == npcTypeKey)
                    return type;
            }

            return null;
        }

        // ── Validation ─────────────────────────────────────────────────
        private void ValidateReferences()
        {
            if (npcTypes == null || npcTypes.Length == 0)
                Debug.LogError("[NPCSpawner] npcTypes не назначены!", this);
            if (spawnPoints == null || spawnPoints.Length == 0)
                Debug.LogError("[NPCSpawner] spawnPoints не назначены!", this);
            if (targetStall == null)
                Debug.LogError("[NPCSpawner] targetStall не назначен!", this);
            if (exitPoint == null)
                Debug.LogError("[NPCSpawner] exitPoint не назначен!", this);
            if (playerMoney == null)
                Debug.LogError("[NPCSpawner] playerMoney не назначен!", this);
        }

        // ── Gizmos ─────────────────────────────────────────────────────
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
