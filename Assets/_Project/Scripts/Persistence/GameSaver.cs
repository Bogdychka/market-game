using Market.Core;
using Market.Core.Events;
using Market.DebugTools;
using Market.Economy;
using Market.Market;
using Market.NPC;
using Market.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Market.Persistence
{
    /// <summary>
    /// Save/load coordinator in the Market scene.
    /// -- F5 saves manually.
    /// -- Load is automatic if SaveSystem.ShouldLoadOnStart was set by the main menu.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class GameSaver : MonoBehaviour
    {
        private const float LocalMinutesPerRealSecond = 2f;

        [Header("Scene References")]
        [SerializeField] private MoneySystem  moneySystem;
        [SerializeField] private Inventory    inventory;
        [SerializeField] private MarketStallRegistry stallRegistry;
        [SerializeField] private Transform    playerTransform;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private NPCSpawner   npcSpawner;

        [Header("Input")]
        [SerializeField] private Key saveKey = Key.F5;

        [Header("Autosave")]
        [Tooltip("Automatically save when the Market scene is unloaded, e.g. on return to menu.")]
        [SerializeField] private bool autoSaveOnSceneExit = true;

        private SaveSystem    _saveSystem;
        private SceneLoader    _sceneLoader;
        private TimeSystem    _timeSystem;
        private DayPhaseSystem _dayPhaseSystem;
        private MarketOpenSystem _marketOpenSystem;
        private DailySummarySystem _dailySummarySystem;
        private EventBus _eventBus;
        private SeasonManager _seasonManager;
        private bool          _ownsLocalEventBus;
        private bool          _ownsLocalTimeSystem;
        private bool          _ownsLocalDayPhaseSystem;
        private bool          _ownsLocalMarketOpenSystem;
        private bool          _ownsLocalDailySummarySystem;
        private bool          _startedInPlayMode;
        private bool          _hasAutoSavedOnExit;

        // -- Lifecycle --------------------------------------------------
        private void Awake()
        {
            _startedInPlayMode = Application.isPlaying;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FileLogger.Initialize();
#endif
            ResolveSaveSystem();
            ResolveEventBus();
            ResolveTimeSystem();
            ResolveDayPhaseSystem();
            ResolveMarketOpenSystem();
            ResolveDailySummarySystem();
            ResolveStallRegistry();
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out _sceneLoader))
                _sceneLoader.OnSceneLoadStarted += OnSceneLoadStarted;
        }

        private void Start()
        {
            // SeasonManager registers in its own Awake
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);

            if (!_saveSystem.ShouldLoadOnStart) return;
            _saveSystem.ShouldLoadOnStart = false;
            Load();
        }

        private void Update()
        {
            TickLocalTime();
            HandleSaveInput();
        }

        private void OnDestroy()
        {
            AutoSaveBeforeExit("destroy fallback");

            if (_ownsLocalTimeSystem)
                ServiceLocator.Unregister<TimeSystem>();

            if (_ownsLocalDayPhaseSystem)
            {
                _dayPhaseSystem?.Dispose();
                ServiceLocator.Unregister<DayPhaseSystem>();
            }

            if (_ownsLocalMarketOpenSystem)
            {
                _marketOpenSystem?.Dispose();
                ServiceLocator.Unregister<MarketOpenSystem>();
            }

            if (_ownsLocalDailySummarySystem)
            {
                _dailySummarySystem?.Dispose();
                ServiceLocator.Unregister<DailySummarySystem>();
            }

            if (_ownsLocalEventBus)
                ServiceLocator.Unregister<EventBus>();
        }

        private void OnDisable()
        {
            if (_sceneLoader != null)
                _sceneLoader.OnSceneLoadStarted -= OnSceneLoadStarted;
        }

        private void OnApplicationQuit()
        {
            AutoSaveBeforeExit("application quit");
        }

        private void TickLocalTime()
        {
            if (_ownsLocalTimeSystem)
                _timeSystem?.Tick(Time.deltaTime);
        }

        private void HandleSaveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[saveKey].wasPressedThisFrame)
                Save();
        }

        // -- Public -----------------------------------------------------
        public void Save()
        {
            Save($"manual {saveKey}");
        }

        private void Save(string reason)
        {
            var data = CollectSaveData();
            if (_saveSystem.Save(data))
                Debug.Log(SaveSummary(data, reason));
        }

        public void Load()
        {
            var data = _saveSystem.Load();
            if (data == null) return;

            if (data.version < 2)
                Debug.LogWarning("[GameSaver] Old save format: time will use SaveData defaults.");

            ApplySaveData(data);
            Debug.Log(LoadSummary(data));
        }

        // -- Save: collect ----------------------------------------------
        private SaveData CollectSaveData()
        {
            var data = new SaveData
            {
                money = moneySystem.Amount
            };

            CollectInventory(data);
            CollectStallSlots(data);
            CollectNpcVisitors(data);
            CollectPlayerTransform(data);
            CollectTime(data);

            return data;
        }

        private void CollectInventory(SaveData data)
        {
            foreach (var kv in inventory.Items)
                data.inventory.Add(new InventoryItemData
                {
                    itemId   = kv.Key.Id,
                    itemName = kv.Key.DisplayName,
                    count    = kv.Value
                });
        }

        private void CollectStallSlots(SaveData data)
        {
            foreach (MarketStall stall in stallRegistry.Stalls)
            {
                if (stall == null || stall.Slots == null) continue;

                StallSlot[] slots = stall.Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    StallSlot slot = slots[i];
                    if (slot == null || !slot.IsOccupied) continue;

                    data.stallSlots.Add(new StallSlotData
                    {
                        stallId = stall.StallId,
                        slotIndex = i,
                        itemId = slot.Item.Id,
                        itemName = slot.Item.DisplayName,
                        sellPrice = slot.SellPrice
                    });
                }
            }
        }

        private void CollectNpcVisitors(SaveData data)
        {
            npcSpawner?.CollectActiveVisitors(data.npcVisitors);
        }

        private void CollectTime(SaveData data)
        {
            if (_timeSystem == null) return;
            data.day    = _timeSystem.Day;
            data.hour   = _timeSystem.Hour;
            data.minute = _timeSystem.Minute;
        }

        private void CollectPlayerTransform(SaveData data)
        {
            var pos = playerTransform.position;
            data.playerX         = pos.x;
            data.playerY         = pos.y;
            data.playerZ         = pos.z;
            data.playerRotationY = playerTransform.eulerAngles.y;
        }

        // -- Load: apply ------------------------------------------------
        private void ApplySaveData(SaveData data)
        {
            moneySystem.SetAmount(data.money);
            ApplyInventory(data);
            ApplyStallSlots(data);
            ApplyPlayerTransform(data);
            ApplyTime(data);
            ApplyNpcVisitors(data);
        }

        private void ApplyInventory(SaveData data)
        {
            inventory.Clear();
            if (data.inventory == null) return;

            foreach (var itemData in data.inventory)
            {
                var so = itemDatabase.Resolve(itemData.itemId, itemData.itemName);
                if (so != null) inventory.Add(so, itemData.count);
            }
        }

        private void ApplyStallSlots(SaveData data)
        {
            foreach (MarketStall stall in stallRegistry.Stalls)
            {
                if (stall == null || stall.Slots == null) continue;

                foreach (StallSlot slot in stall.Slots)
                {
                    if (slot != null && slot.IsOccupied)
                        slot.Clear();
                }
            }

            if (data.stallSlots == null) return;

            foreach (var slotData in data.stallSlots)
            {
                MarketStall stall = ResolveSavedStall(slotData);
                if (stall == null || stall.Slots == null) continue;
                if (slotData.slotIndex < 0 || slotData.slotIndex >= stall.Slots.Length) continue;

                var so = itemDatabase.Resolve(slotData.itemId, slotData.itemName);
                if (so != null)
                    stall.Slots[slotData.slotIndex].Place(so, slotData.sellPrice);
            }
        }

        private void ApplyTime(SaveData data)
        {
            if (_timeSystem == null) return;
            _timeSystem.SetTime(data.day, data.hour, data.minute);

            // Season is derived from day -- refresh after setting time
            _seasonManager?.RefreshSeason();
        }

        private void ApplyPlayerTransform(SaveData data)
        {
            // CharacterController must be disabled to teleport the position
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position    = new Vector3(data.playerX, data.playerY, data.playerZ);
            playerTransform.eulerAngles = new Vector3(0f, data.playerRotationY, 0f);

            if (cc != null) cc.enabled = true;
        }

        private void ApplyNpcVisitors(SaveData data)
        {
            npcSpawner?.RestoreActiveVisitors(data.npcVisitors);
        }

        private void OnSceneLoadStarted(string targetSceneName)
        {
            if (SceneManager.GetActiveScene().name != SceneNames.Market) return;
            AutoSaveBeforeExit($"scene load to {targetSceneName}");
        }

        private void AutoSaveBeforeExit(string reason)
        {
            if (!autoSaveOnSceneExit || !_startedInPlayMode || _hasAutoSavedOnExit || _saveSystem == null) return;
            if (!CanSave(reason)) return;

            _hasAutoSavedOnExit = true;
            Save(reason);
        }

        private bool CanSave(string reason)
        {
            bool canSave = moneySystem != null
                && inventory != null
                && stallRegistry != null
                && stallRegistry.Count > 0
                && playerTransform != null
                && itemDatabase != null
                && npcSpawner != null;

            if (!canSave)
                Debug.LogWarning($"[GameSaver] Autosave skipped ({reason}): not all scene references are available.");

            return canSave;
        }

        // -- Setup ------------------------------------------------------
        private void ResolveSaveSystem()
        {
            if (ServiceLocator.TryGet<SaveSystem>(out _saveSystem)) return;

            // Direct Market scene startup -- create a local instance
            _saveSystem = new SaveSystem();
            ServiceLocator.Register(_saveSystem);
            Debug.LogWarning("[GameSaver] SaveSystem not found in ServiceLocator. " +
                             "Start via Bootstrap, not directly from Market scene.");
        }

        private void ResolveTimeSystem()
        {
            if (ServiceLocator.TryGet<TimeSystem>(out _timeSystem)) return;

            _timeSystem = new TimeSystem(LocalMinutesPerRealSecond);
            _ownsLocalTimeSystem = true;
            ServiceLocator.Register(_timeSystem);
            Debug.LogWarning("[GameSaver] TimeSystem not found in ServiceLocator. " +
                             "Created a local TimeSystem for direct Market scene startup.");
        }

        private void ResolveDayPhaseSystem()
        {
            if (ServiceLocator.TryGet<DayPhaseSystem>(out _dayPhaseSystem)) return;

            _dayPhaseSystem = new DayPhaseSystem(_timeSystem, _eventBus);
            _ownsLocalDayPhaseSystem = true;
            ServiceLocator.Register(_dayPhaseSystem);
            Debug.LogWarning("[GameSaver] DayPhaseSystem not found in ServiceLocator. " +
                             "Created a local DayPhaseSystem for direct Market scene startup.");
        }

        private void ResolveMarketOpenSystem()
        {
            if (ServiceLocator.TryGet<MarketOpenSystem>(out _marketOpenSystem)) return;

            _marketOpenSystem = new MarketOpenSystem(_dayPhaseSystem, _eventBus);
            _ownsLocalMarketOpenSystem = true;
            ServiceLocator.Register(_marketOpenSystem);
            Debug.LogWarning("[GameSaver] MarketOpenSystem not found in ServiceLocator. " +
                             "Created a local MarketOpenSystem for direct Market scene startup.");
        }

        private void ResolveEventBus()
        {
            if (ServiceLocator.TryGet<EventBus>(out _eventBus)) return;

            _eventBus = new EventBus();
            _ownsLocalEventBus = true;
            ServiceLocator.Register(_eventBus);
            Debug.LogWarning("[GameSaver] EventBus not found in ServiceLocator. " +
                             "Created a local EventBus for direct Market scene startup.");
        }

        private void ResolveDailySummarySystem()
        {
            if (ServiceLocator.TryGet<DailySummarySystem>(out _dailySummarySystem)) return;

            _dailySummarySystem = new DailySummarySystem(_eventBus, _timeSystem);
            _ownsLocalDailySummarySystem = true;
            ServiceLocator.Register(_dailySummarySystem);
            Debug.LogWarning("[GameSaver] DailySummarySystem not found in ServiceLocator. " +
                             "Created a local DailySummarySystem for direct Market scene startup.");
        }

        private void ResolveStallRegistry()
        {
            if (stallRegistry != null) return;
            ServiceLocator.TryGet<MarketStallRegistry>(out stallRegistry);
        }

        private MarketStall ResolveSavedStall(StallSlotData slotData)
        {
            if (slotData == null || stallRegistry == null) return null;

            if (!string.IsNullOrWhiteSpace(slotData.stallId) &&
                stallRegistry.TryGetStall(slotData.stallId, out MarketStall stall))
            {
                return stall;
            }

            return stallRegistry.GetFirstStall();
        }

        private void ValidateReferences()
        {
            if (moneySystem     == null) Debug.LogError("[GameSaver] moneySystem not assigned",     this);
            if (inventory       == null) Debug.LogError("[GameSaver] inventory not assigned",       this);
            if (stallRegistry == null || stallRegistry.Count == 0)
                Debug.LogError("[GameSaver] stallRegistry not assigned or empty", this);
            if (playerTransform == null) Debug.LogError("[GameSaver] playerTransform not assigned", this);
            if (itemDatabase    == null) Debug.LogError("[GameSaver] itemDatabase not assigned",    this);
            if (npcSpawner      == null) Debug.LogError("[GameSaver] npcSpawner not assigned",      this);
        }

        private static string LoadSummary(SaveData data) =>
            $"[GameSaver] Game loaded: {FormatState(data)}";

        private static string SaveSummary(SaveData data, string reason) =>
            $"[GameSaver] Game saved ({reason}): {FormatState(data)}";

        /// <summary>Compact one-line snapshot of save contents shared by the load/save log lines.</summary>
        private static string FormatState(SaveData data)
        {
            int inventoryCount = data.inventory?.Count ?? 0;
            int stallCount = data.stallSlots?.Count ?? 0;
            return $"v{data.version}, money={data.money:0.##}, " +
                   $"inventory={inventoryCount}, stall={stallCount}, time=Day {data.day} {data.hour:00}:{data.minute:00}";
        }
    }
}
