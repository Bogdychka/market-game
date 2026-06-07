using System;
using Market.Economy;
using Market.Market;
using UnityEngine;
using UnityEngine.AI;

namespace Market.NPC
{
    /// <summary>
    /// Market visitor NPC. States: WalkToStall → Browsing → WalkToExit → Done.
    /// Buys the first item at an acceptable price, preferring PreferredCategories.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCVisitor : MonoBehaviour
    {
        public enum State { WalkToStall, Browsing, WalkToExit, Done }

        [Header("References")]
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
        public float BrowseTimer => _browseTimer;

        private NavMeshAgent    _agent;
        private NPCTypeSO       _type;
        private State           _state;
        private float           _browseTimer;
        private ItemCategory[]  _preferredCategories;
        private bool            _hasRestoredState;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Configure from NPCSpawner. Call after Instantiate, before Start.
        /// </summary>
        public void Initialize(NPCTypeSO type, MarketStall stall, Transform exit, MoneySystem money)
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
            ValidateReferences();
            if (_hasRestoredState) return;

            EnterState(State.WalkToStall);
        }

        public void RestoreState(State state, float savedBrowseTimer, Vector3 position, float rotationY)
        {
            Vector3 navPosition = SnapToNavMesh(position);
            transform.SetPositionAndRotation(navPosition, Quaternion.Euler(0f, rotationY, 0f));
            if (_agent.enabled)
                _agent.Warp(navPosition);

            _hasRestoredState = true;
            switch (state)
            {
                case State.Browsing:
                    _state = State.Browsing;
                    _agent.ResetPath();
                    _browseTimer = Mathf.Max(0.1f, savedBrowseTimer);
                    break;
                case State.WalkToExit:
                    EnterState(State.WalkToExit);
                    break;
                case State.Done:
                    EnterState(State.Done);
                    break;
                default:
                    EnterState(State.WalkToStall);
                    break;
            }
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

        // ── State machine: dispatcher ──────────────────────────────────
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

        // ── State: WalkToStall ─────────────────────────────────────────
        private void EnterWalkToStall()
        {
            _agent.SetDestination(SnapToNavMesh(targetStall.transform.position));
        }

        private void UpdateWalkToStall()
        {
            if (HasArrived()) EnterState(State.Browsing);
        }

        // ── State: Browsing ────────────────────────────────────────────
        private void EnterBrowsing()
        {
            _agent.ResetPath();
            _browseTimer = browseTime;
        }

        private void UpdateBrowsing()
        {
            _browseTimer -= Time.deltaTime;
            if (_browseTimer > 0f) return;

            TryBuy();
            EnterState(State.WalkToExit);
        }

        private void TryBuy()
        {
            if (TryBuyPreferredItem(out string failureReason)) return;

            Debug.Log($"[NPC] Did not buy: {failureReason}, leaving.");
        }

        /// <summary>
        /// Iterates slots and buys the first item of a preferred category within budget.
        /// </summary>
        private bool TryBuyPreferredItem(out string failureReason)
        {
            failureReason = "no items";
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
            Debug.Log($"[NPC] Bought {item.DisplayName} for {price}. Player funds: {playerMoney.Amount}");
        }

        private string BuildFailureReason(bool hasStock, bool hasInterestingCategory, bool hasOverBudgetItem)
        {
            if (!hasStock) return "no items";
            if (!hasInterestingCategory) return "uninteresting category";
            if (hasOverBudgetItem) return $"over budget (budget {maxAcceptablePrice:0.##})";
            return "no deal available";
        }

        // ── State: WalkToExit ──────────────────────────────────────────
        private void EnterWalkToExit()
        {
            _agent.SetDestination(SnapToNavMesh(exitPoint.position));
        }

        private void UpdateWalkToExit()
        {
            if (HasArrived()) EnterState(State.Done);
        }

        // ── State: Done ────────────────────────────────────────────────
        private void EnterDone()
        {
            Debug.Log("[NPC] Left the market.");
            OnDespawned?.Invoke(this);
            Destroy(gameObject);
        }

        // ── Helpers ────────────────────────────────────────────────────
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
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                return hit.position;

            Debug.LogWarning($"[NPCVisitor] Point {worldPos} is not on the NavMesh — NPC may not reach it!");
            return worldPos;
        }

        private void ValidateReferences()
        {
            if (targetStall == null) Debug.LogError("[NPCVisitor] targetStall not assigned", this);
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
