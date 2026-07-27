using System.Text;
using Market.Core;
using Market.Economy;
using Market.Market;
using Market.NPC;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Automated debug runner for a short market loop:
    /// buy from supplier -> place on stall -> spawn NPC -> log snapshot.
    /// </summary>
    public class MarketAutoDebugger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SupplierShop supplierShop;
        [SerializeField] private Inventory inventory;
        [SerializeField] private MarketStall marketStall;
        [SerializeField] private NPCSpawner npcSpawner;
        [SerializeField] private MoneySystem moneySystem;

        [Header("Controls")]
        [Tooltip("Run the auto-test immediately on scene start.")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private Key toggleKey = Key.F9;
        [SerializeField] private Key singleStepKey = Key.F10;

        [Header("Scenario")]
        [Tooltip("Pause between automatic cycles.")]
        [SerializeField] private float stepInterval = 6f;
        [Tooltip("Index of the supplier stock item the auto-debugger will buy.")]
        [SerializeField] private int supplierStockIndex = 0;
        [Tooltip("Place item on stall at the suggested price. Otherwise uses debugSellPrice.")]
        [SerializeField] private bool useCalculatedSellPrice = true;
        [SerializeField] private float debugSellPrice = 20f;
        [SerializeField] private bool autoBuyIfNeeded = true;
        [SerializeField] private bool autoPlaceOnStall = true;
        [SerializeField] private bool autoSpawnNpc = true;
        [Tooltip("Guard against infinite force-spawning if NPCs get stuck or take too long to leave.")]
        [SerializeField] private int maxForcedNpcCount = 3;

        [Header("Logging")]
        [SerializeField] private bool logSnapshots = true;
        [SerializeField] private float snapshotInterval = 5f;

        private TimeSystem _timeSystem;
        private bool _running;
        private float _stepTimer;
        private float _snapshotTimer;
        private int _cycleIndex;

        private void Awake()
        {
            ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
            ValidateReferences();
        }

        private void Start()
        {
            if (runOnStart) StartAutoRun();
            else Debug.Log($"[AutoDebug] Ready. {toggleKey}=auto on/off, {singleStepKey}=single cycle.");
        }

        private void Update()
        {
            HandleHotkeys();

            if (!_running) return;

            _stepTimer -= Time.deltaTime;
            _snapshotTimer -= Time.deltaTime;

            if (_snapshotTimer <= 0f)
            {
                _snapshotTimer = Mathf.Max(0.5f, snapshotInterval);
                LogSnapshot("tick");
            }

            if (_stepTimer <= 0f)
            {
                _stepTimer = Mathf.Max(0.5f, stepInterval);
                RunCycle("auto");
            }
        }

        /// <summary>Start repeating auto-test.</summary>
        public void StartAutoRun()
        {
            _running = true;
            _stepTimer = 0f;
            _snapshotTimer = 0f;
            Debug.Log("[AutoDebug] Auto-test started.");
        }

        /// <summary>Stop repeating auto-test.</summary>
        public void StopAutoRun()
        {
            _running = false;
            Debug.Log("[AutoDebug] Auto-test stopped.");
            LogSnapshot("stop");
        }

        /// <summary>Run one full debug cycle.</summary>
        public void RunSingleStep()
        {
            RunCycle("manual");
        }

        private void HandleHotkeys()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (_running) StopAutoRun();
                else StartAutoRun();
            }

            if (Keyboard.current[singleStepKey].wasPressedThisFrame)
                RunSingleStep();
        }

        private void RunCycle(string source)
        {
            _cycleIndex++;
            Debug.Log($"[AutoDebug] Cycle #{_cycleIndex} started ({source}).");

            if (autoBuyIfNeeded || autoPlaceOnStall)
                EnsureStallHasStock();

            if (autoSpawnNpc)
                ForceSpawnNpc();

            LogSnapshot($"cycle-{_cycleIndex}");
        }

        private void EnsureStallHasStock()
        {
            if (marketStall == null || inventory == null) return;
            if (!HasFreeSlot()) return;

            ItemSO item = FindFirstInventoryItem();

            if (item == null && autoBuyIfNeeded)
            {
                if (supplierShop == null)
                {
                    Debug.LogWarning("[AutoDebug] supplierShop not assigned -- auto-buy impossible.", this);
                    return;
                }

                if (supplierShop.Buy(supplierStockIndex))
                    item = FindFirstInventoryItem();
            }

            if (item == null || !autoPlaceOnStall) return;

            int slotIndex = FindFirstFreeSlotIndex();
            if (slotIndex < 0) return;

            bool placed = useCalculatedSellPrice
                ? marketStall.PlaceItem(slotIndex, item)
                : marketStall.PlaceItem(slotIndex, item, debugSellPrice);

            if (!placed)
                Debug.LogWarning($"[AutoDebug] Failed to place {item.DisplayName} in slot {slotIndex}.", this);
        }

        private void ForceSpawnNpc()
        {
            if (npcSpawner == null)
            {
                Debug.LogWarning("[AutoDebug] npcSpawner not assigned -- auto-spawn impossible.", this);
                return;
            }

            if (npcSpawner.ActiveCount >= maxForcedNpcCount)
            {
                Debug.Log($"[AutoDebug] NPC not spawned: active={npcSpawner.ActiveCount}, limit={maxForcedNpcCount}.");
                return;
            }

            if (!npcSpawner.ForceSpawnForDebug())
                Debug.LogWarning("[AutoDebug] NPC did not spawn. Check npcTypes/spawnPoints/prefab.", this);
        }

        private bool HasFreeSlot() => FindFirstFreeSlotIndex() >= 0;

        private int FindFirstFreeSlotIndex()
        {
            if (marketStall?.Slots == null) return -1;

            var slots = marketStall.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slots[i].IsOccupied) return i;
            }
            return -1;
        }

        private ItemSO FindFirstInventoryItem()
        {
            if (inventory == null) return null;

            foreach (var kv in inventory.Items)
                if (kv.Key != null && kv.Value > 0)
                    return kv.Key;

            return null;
        }

        private void LogSnapshot(string reason)
        {
            if (!logSnapshots) return;

            string time = _timeSystem != null ? _timeSystem.FormatTime() : "no-time";
            string money = moneySystem != null ? moneySystem.Amount.ToString() : "no-money";

            Debug.Log($"[AutoDebug] Snapshot ({reason}) | time={time} | money={money} | " +
                      $"inventory={BuildInventorySummary()} | stall={BuildStallSummary()} | " +
                      $"npc={BuildNpcSummary()} | prices={BuildPriceSummary()}");
        }

        private string BuildPriceSummary()
        {
            if (supplierShop == null) return "no-supplier";

            ItemSO item = supplierShop.GetStockItem(supplierStockIndex);
            if (item == null) return "no-item";

            return $"{item.DisplayName}: buy={supplierShop.GetBuyPrice(item):0.##}, " +
                   $"sell={(marketStall != null ? marketStall.SuggestedSellPrice(item) : item.BaseSellPrice):0.##}";
        }

        private string BuildInventorySummary()
        {
            if (inventory == null) return "missing";

            var sb = new StringBuilder();
            foreach (var kv in inventory.Items)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key.DisplayName).Append('x').Append(kv.Value);
            }
            return sb.Length > 0 ? sb.ToString() : "empty";
        }

        private string BuildStallSummary()
        {
            if (marketStall?.Slots == null) return "missing";

            var slots = marketStall.Slots;
            var sb = new StringBuilder();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !slots[i].IsOccupied) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append('#').Append(i).Append(':')
                  .Append(slots[i].Item.DisplayName)
                  .Append('@').Append(slots[i].SellPrice.ToString("0.##"));
            }
            return sb.Length > 0 ? sb.ToString() : "empty";
        }

        private string BuildNpcSummary()
        {
            if (npcSpawner == null) return "missing";
            return $"active={npcSpawner.ActiveCount}, density={npcSpawner.CurrentDensity:0.00}";
        }

        private void ValidateReferences()
        {
            if (supplierShop == null && autoBuyIfNeeded)
                Debug.LogWarning("[AutoDebug] supplierShop not assigned.", this);
            if (inventory == null)
                Debug.LogWarning("[AutoDebug] inventory not assigned.", this);
            if (marketStall == null)
                Debug.LogWarning("[AutoDebug] marketStall not assigned.", this);
            if (npcSpawner == null && autoSpawnNpc)
                Debug.LogWarning("[AutoDebug] npcSpawner not assigned.", this);
            if (moneySystem == null)
                Debug.LogWarning("[AutoDebug] moneySystem not assigned.", this);
        }
    }
}
