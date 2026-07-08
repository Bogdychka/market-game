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
    public partial class NPCVisitor : MonoBehaviour
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
