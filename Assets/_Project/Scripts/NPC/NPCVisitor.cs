using System;
using System.Collections.Generic;
using Market.Core;
using Market.Core.Events;
using Market.Economy;
using Market.Market;
using UnityEngine;
using UnityEngine.AI;

namespace Market.NPC
{
    /// <summary>
    /// Market visitor NPC. States: WalkToStall -> Browsing -> WalkToExit -> Done.
    /// Buys the first item at an acceptable price, preferring PreferredCategories.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCVisitor : MonoBehaviour
    {
        public enum State { WalkToStall, Browsing, WalkToExit, Done }

        [Header("References")]
        [SerializeField] private MarketStallRegistry stallRegistry;
        [SerializeField] private MarketStall targetStall;
        [SerializeField] private Transform   exitPoint;
        [SerializeField] private MoneySystem playerMoney;

        [Header("Behaviour")]
        [SerializeField] private float maxAcceptablePrice = 50f;
        [SerializeField] private float browseTime         = 1.5f;
        [SerializeField] private float arrivalDistance    = 1.5f;

        [Header("Tuning")]
        [Tooltip("NavMesh sample radius for snapping target positions (in case targetStall/exitPoint are slightly off the baked mesh).")]
        [SerializeField] private float navMeshSampleRadius = 5f;

        public event Action<NPCVisitor> OnDespawned;
        public State CurrentState => _state;
        public NPCTypeSO Type => _type;
        public string TargetStallId => targetStall != null ? targetStall.StallId : null;
        public IReadOnlyList<MarketStall> VisitedStalls => _visitedStalls;

        private NavMeshAgent    _agent;
        private NPCTypeSO       _type;
        private State           _state;
        private float           _browseTimer;
        private ItemCategory[]  _preferredCategories;
        private EventBus        _eventBus;
        private bool            _isPasserby;
        private readonly List<MarketStall> _visitedStalls = new();

        // -- Lifecycle --------------------------------------------------
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            ServiceLocator.TryGet<EventBus>(out _eventBus);
        }

        /// <summary>
        /// Configure from NPCSpawner. Call after Instantiate, before Start.
        /// </summary>
        public void Initialize(NPCTypeSO type, MarketStall stall, Transform exit, MoneySystem money)
        {
            stallRegistry = null;
            _visitedStalls.Clear();
            _isPasserby = false;
            Configure(type, stall, exit, money);
        }

        /// <summary>
        /// Configure with the stall registry so the visitor can browse multiple stalls.
        /// </summary>
        public void Initialize(NPCTypeSO type, MarketStallRegistry registry, Transform exit, MoneySystem money)
        {
            stallRegistry = registry;
            _visitedStalls.Clear();
            _isPasserby = false;
            Configure(type, SelectInitialStall(), exit, money);
        }

        /// <summary>
        /// Configure a closed-market passerby. The NPC walks past the market and leaves without shopping.
        /// </summary>
        public void InitializePasserby(NPCTypeSO type, Transform exit, MoneySystem money)
        {
            stallRegistry = null;
            _visitedStalls.Clear();
            _isPasserby = true;
            Configure(type, null, exit, money);
        }

        private void Configure(NPCTypeSO type, MarketStall stall, Transform exit, MoneySystem money)
        {
            _type        = type;
            targetStall  = stall;
            exitPoint    = exit;
            playerMoney  = money;

            maxAcceptablePrice   = type.Budget;
            browseTime           = type.BrowseTime;
            _agent.speed         = type.WalkSpeed;
            _preferredCategories = type.PreferredCategories;
        }

        private void Start()
        {
            // Fresh spawns begin here (proven first-frame agent timing).
            // Pooled reuse calls Begin() directly, since Unity's Start runs only once per object.
            Begin();
        }

        /// <summary>
        /// Begins the visitor's behaviour. Call after Initialize/RestoreStalls on pool reuse, where
        /// Unity's Start does not run again. Fresh spawns reach this through Start.
        /// </summary>
        public void Begin()
        {
            ValidateReferences();
            EnterState(_isPasserby ? State.WalkToExit : State.WalkToStall);
        }

        /// <summary>
        /// Repositions a (possibly pooled) visitor and snaps its NavMeshAgent to the spawn point,
        /// clearing any path left over from a previous life.
        /// </summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (_agent == null) return;

            _agent.Warp(position);
            if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        /// <summary>Redirect this visitor away from stalls, usually because the market was closed.</summary>
        public void LeaveMarket()
        {
            if (_state == State.WalkToExit || _state == State.Done)
                return;

            _isPasserby = true;
            targetStall = null;
            EnterState(State.WalkToExit);
        }

        /// <summary>
        /// Restore the saved target/visited stalls (resolved via the registry) so a loaded NPC walks
        /// in from the entrance toward the stall it still wanted, skipping ones it already browsed.
        /// Call after Initialize(registry, ...). Unknown ids (old saves / removed stalls) are ignored,
        /// leaving the random initial stall from Initialize as a fallback.
        /// </summary>
        public void RestoreStalls(string targetStallId, List<string> visitedStallIds)
        {
            _visitedStalls.Clear();
            if (stallRegistry == null) return;

            if (visitedStallIds != null)
            {
                foreach (string id in visitedStallIds)
                    if (stallRegistry.TryGetStall(id, out MarketStall visited))
                        RememberVisitedStall(visited);
            }

            if (stallRegistry.TryGetStall(targetStallId, out MarketStall target))
                targetStall = target;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.WalkToStall: UpdateWalkToStall(); break;
                case State.Browsing:    UpdateBrowsing();    break;
                case State.WalkToExit:  UpdateWalkToExit();  break;
            }
        }

        // -- State machine: dispatcher ----------------------------------
        private void EnterState(State next)
        {
            _state = next;
            switch (next)
            {
                case State.WalkToStall: EnterWalkToStall(); break;
                case State.Browsing:    EnterBrowsing();    break;
                case State.WalkToExit:  EnterWalkToExit();  break;
                case State.Done:        EnterDone();        break;
            }
        }

        // -- State: WalkToStall -----------------------------------------
        private void EnterWalkToStall()
        {
            if (targetStall == null)
            {
                EnterState(State.WalkToExit);
                return;
            }

            RememberVisitedStall(targetStall);
            _agent.SetDestination(SnapToNavMesh(targetStall.transform.position));
        }

        private void UpdateWalkToStall()
        {
            if (HasArrived()) EnterState(State.Browsing);
        }

        // -- State: Browsing --------------------------------------------
        private void EnterBrowsing()
        {
            _agent.ResetPath();
            _browseTimer = browseTime;
        }

        private void UpdateBrowsing()
        {
            _browseTimer -= Time.deltaTime;
            if (_browseTimer > 0f) return;

            if (TryBuy())
            {
                EnterState(State.WalkToExit);
                return;
            }

            if (TrySelectNextStall())
            {
                EnterState(State.WalkToStall);
                return;
            }

            EnterState(State.WalkToExit);
        }

        private bool TryBuy()
        {
            if (TryBuyPreferredItem(out string failureReason))
                return true;

            Debug.Log($"[NPC] Did not buy at {GetStallLabel(targetStall)}: {failureReason}.");
            return false;
        }

        /// <summary>
        /// Iterates slots and buys the first item of a preferred category within budget.
        /// </summary>
        private bool TryBuyPreferredItem(out string failureReason)
        {
            failureReason = "no items";
            if (targetStall == null || targetStall.Slots == null)
            {
                failureReason = "missing stall";
                return false;
            }

            var slots = targetStall.Slots;
            bool hasStock = false;
            bool hasInterestingCategory = false;
            bool hasOverBudgetItem = false;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!slot.IsOccupied) continue;

                hasStock = true;
                if (!IsPreferredCategory(slot.Item.Category)) continue;

                hasInterestingCategory = true;
                if (slot.SellPrice > maxAcceptablePrice)
                {
                    hasOverBudgetItem = true;
                    continue;
                }

                if (!targetStall.TakeSale(i, out var item, out float price)) continue;

                CompletePurchase(item, price);
                return true;
            }

            failureReason = BuildFailureReason(hasStock, hasInterestingCategory, hasOverBudgetItem);
            return false;
        }

        private bool IsPreferredCategory(ItemCategory category)
        {
            return _preferredCategories == null
                   || _preferredCategories.Length == 0
                   || Array.Exists(_preferredCategories, c => c == category);
        }

        private void CompletePurchase(ItemSO item, float price)
        {
            playerMoney.Add(price);
            _eventBus?.Publish(new ItemSoldEvent(item, price));
            Debug.Log($"[NPC] Bought {item.DisplayName} for {price}. Player funds: {playerMoney.Amount}");
        }

        private string BuildFailureReason(bool hasStock, bool hasInterestingCategory, bool hasOverBudgetItem)
        {
            if (!hasStock) return "no items";
            if (!hasInterestingCategory) return "uninteresting category";
            if (hasOverBudgetItem) return $"over budget (budget {maxAcceptablePrice:0.##})";
            return "no deal available";
        }

        private MarketStall SelectInitialStall()
        {
            return stallRegistry != null ? stallRegistry.GetRandomStall() : targetStall;
        }

        private bool TrySelectNextStall()
        {
            MarketStall nextStall = SelectNextStall();
            if (nextStall == null)
                return false;

            targetStall = nextStall;
            Debug.Log($"[NPC] Browsing next stall: {GetStallLabel(targetStall)}.");
            return true;
        }

        private MarketStall SelectNextStall()
        {
            if (stallRegistry == null || stallRegistry.Count == 0)
                return null;

            MarketStall fallback = null;
            IReadOnlyList<MarketStall> stalls = stallRegistry.Stalls;
            for (int i = 0; i < stalls.Count; i++)
            {
                MarketStall stall = stalls[i];
                if (!CanVisitStall(stall)) continue;

                fallback ??= stall;
                if (HasStock(stall))
                    return stall;
            }

            return fallback;
        }

        private bool CanVisitStall(MarketStall stall)
        {
            return stall != null
                   && stall != targetStall
                   && !_visitedStalls.Contains(stall);
        }

        private bool HasStock(MarketStall stall)
        {
            if (stall == null || stall.Slots == null)
                return false;

            var slots = stall.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsOccupied)
                    return true;
            }

            return false;
        }

        private void RememberVisitedStall(MarketStall stall)
        {
            if (stall != null && !_visitedStalls.Contains(stall))
                _visitedStalls.Add(stall);
        }

        private string GetStallLabel(MarketStall stall)
        {
            return stall != null ? stall.StallId : "missing stall";
        }

        // -- State: WalkToExit ------------------------------------------
        private void EnterWalkToExit()
        {
            _agent.SetDestination(SnapToNavMesh(exitPoint.position));
        }

        private void UpdateWalkToExit()
        {
            if (HasArrived()) EnterState(State.Done);
        }

        // -- State: Done ------------------------------------------------
        private void EnterDone()
        {
            Debug.Log("[NPC] Left the market.");

            // The owner (NPCSpawner) decides the fate -- return to pool or destroy.
            // With no owner (e.g. spawner disabled), self-destruct as before so we never leak.
            if (OnDespawned != null)
                OnDespawned.Invoke(this);
            else
                Destroy(gameObject);
        }

        // -- Helpers ----------------------------------------------------
        private bool HasArrived()
        {
            if (_agent.pathPending) return false;
            if (!_agent.hasPath)    return false;
            return _agent.remainingDistance <= arrivalDistance;
        }

        /// <summary>
        /// If the point is not on the NavMesh, snap to the nearest position.
        /// </summary>
        private Vector3 SnapToNavMesh(Vector3 worldPos)
        {
            if (TrySampleNavMesh(worldPos, out Vector3 navPos))
                return navPos;

            Debug.LogWarning($"[NPCVisitor] Point {worldPos} is not on the NavMesh -- NPC may not reach it!");
            return navPos;
        }

        private bool TrySampleNavMesh(Vector3 worldPos, out Vector3 navPos)
        {
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                navPos = hit.position;
                return true;
            }

            navPos = worldPos;
            return false;
        }

        private void ValidateReferences()
        {
            if (!_isPasserby && targetStall == null) Debug.LogError("[NPCVisitor] targetStall not assigned", this);
            if (exitPoint   == null) Debug.LogError("[NPCVisitor] exitPoint not assigned",   this);
            if (playerMoney == null) Debug.LogError("[NPCVisitor] playerMoney not assigned", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (targetStall != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, targetStall.transform.position);
            }
            if (exitPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, exitPoint.position);
                Gizmos.DrawSphere(exitPoint.position, 0.3f);
            }
        }
    }
}
