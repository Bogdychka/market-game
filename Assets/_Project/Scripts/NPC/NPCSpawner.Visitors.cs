using System.Collections.Generic;
using Market.Market;
using Market.Persistence;
using UnityEngine;

namespace Market.NPC
{
    public partial class NPCSpawner
    {
        public void CollectActiveVisitors(List<NPCVisitorData> visitors)
        {
            if (visitors == null) return;

            RemoveMissingVisitors();
            foreach (NPCVisitor visitor in _spawnedVisitors)
            {
                if (visitor == null || visitor.Type == null) continue;

                // Only persist visitors that still intend to shop; ones already leaving
                // are transient and simply regenerate as new traffic.
                if (visitor.CurrentState != NPCVisitor.State.WalkToStall &&
                    visitor.CurrentState != NPCVisitor.State.Browsing)
                    continue;

                NPCVisitorData data = new()
                {
                    npcTypeKey = visitor.Type.Id,
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

        private bool TrySpawn()
        {
            if (npcTypes == null || npcTypes.Length == 0) return false;
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

            var type = npcTypes[Random.Range(0, npcTypes.Length)];
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

            // Schedule-style restore: re-spawn at an entrance and let the visitor walk in.
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

        private NPCVisitor AcquireVisitor(NPCTypeSO type, Transform point, out bool reused)
        {
            if (_pool.TryGetValue(type, out Stack<NPCVisitor> pooled))
            {
                while (pooled.Count > 0)
                {
                    NPCVisitor candidate = pooled.Pop();
                    if (candidate == null) continue;

                    candidate.gameObject.SetActive(true);
                    candidate.PlaceAt(point.position, point.rotation);
                    reused = true;
                    return candidate;
                }
            }

            reused = false;
            return InstantiateVisitor(type, point);
        }

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
            ClearActiveVisitors();
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
    }
}
