using Market.Core;
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
    /// Координатор сохранения/загрузки в сцене Market.
    /// — F5 сохраняет вручную
    /// — Загрузка автоматическая, если флаг SaveSystem.ShouldLoadOnStart выставлен главным меню
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class GameSaver : MonoBehaviour
    {
        private const float LocalMinutesPerRealSecond = 2f;

        [Header("Scene References")]
        [SerializeField] private MoneySystem  moneySystem;
        [SerializeField] private Inventory    inventory;
        // TEMP single-stall API (B9). Multi-stall save/load will iterate a
        // MarketStallRegistry instead of this single reference.
        [Tooltip("TEMP: single stall. Future multi-stall iterates MarketStallRegistry.")]
        [SerializeField] private MarketStall  marketStall;
        [SerializeField] private Transform    playerTransform;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private NPCSpawner   npcSpawner;

        [Header("Input")]
        [SerializeField] private Key saveKey = Key.F5;

        [Header("Autosave")]
        [Tooltip("Автоматически сохранять игру при выгрузке сцены Market, например при возврате в меню.")]
        [SerializeField] private bool autoSaveOnSceneExit = true;

        private SaveSystem    _saveSystem;
        private SceneLoader    _sceneLoader;
        private TimeSystem    _timeSystem;
        private SeasonManager _seasonManager;
        private bool          _ownsLocalTimeSystem;
        private bool          _startedInPlayMode;
        private bool          _hasAutoSavedOnExit;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            _startedInPlayMode = Application.isPlaying;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FileLogger.Initialize();
#endif
            ResolveSaveSystem();
            ResolveTimeSystem();
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out _sceneLoader))
                _sceneLoader.OnSceneLoadStarted += OnSceneLoadStarted;
        }

        private void Start()
        {
            // SeasonManager регистрируется в своём Awake
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

        // ── Public ─────────────────────────────────────────────────────
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
                Debug.LogWarning("[GameSaver] Сейв старого формата: время будет взято из дефолтов SaveData.");

            ApplySaveData(data);
            Debug.Log(LoadSummary(data));
        }

        // ── Save: collect ──────────────────────────────────────────────
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
            var slots = marketStall.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!slot.IsOccupied) continue;
                data.stallSlots.Add(new StallSlotData
                {
                    slotIndex = i,
                    itemId    = slot.Item.Id,
                    itemName  = slot.Item.DisplayName,
                    sellPrice = slot.SellPrice
                });
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

        // ── Load: apply ────────────────────────────────────────────────
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
            // Очищаем текущее состояние прилавка
            foreach (var slot in marketStall.Slots)
                if (slot.IsOccupied) slot.Clear();

            if (data.stallSlots == null) return;

            foreach (var slotData in data.stallSlots)
            {
                if (slotData.slotIndex < 0 || slotData.slotIndex >= marketStall.Slots.Length) continue;
                var so = itemDatabase.Resolve(slotData.itemId, slotData.itemName);
                if (so != null)
                    marketStall.Slots[slotData.slotIndex].Place(so, slotData.sellPrice);
            }
        }

        private void ApplyTime(SaveData data)
        {
            if (_timeSystem == null) return;
            _timeSystem.SetTime(data.day, data.hour, data.minute);

            // Сезон выводится из дня — пересчитываем после установки времени
            _seasonManager?.RefreshSeason();
        }

        private void ApplyPlayerTransform(SaveData data)
        {
            // CharacterController нужно отключить чтобы перенести позицию
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
                && marketStall != null
                && playerTransform != null
                && itemDatabase != null
                && npcSpawner != null;

            if (!canSave)
                Debug.LogWarning($"[GameSaver] Автосохранение пропущено ({reason}): не все ссылки сцены доступны.");

            return canSave;
        }

        // ── Setup ──────────────────────────────────────────────────────
        private void ResolveSaveSystem()
        {
            if (ServiceLocator.TryGet<SaveSystem>(out _saveSystem)) return;

            // Запуск напрямую из Market сцены — создаём локальный экземпляр
            _saveSystem = new SaveSystem();
            ServiceLocator.Register(_saveSystem);
            Debug.LogWarning("[GameSaver] SaveSystem не найден в ServiceLocator. " +
                             "Запускай сцену через Bootstrap, а не напрямую.");
        }

        private void ResolveTimeSystem()
        {
            if (ServiceLocator.TryGet<TimeSystem>(out _timeSystem)) return;

            _timeSystem = new TimeSystem(LocalMinutesPerRealSecond);
            _ownsLocalTimeSystem = true;
            ServiceLocator.Register(_timeSystem);
            Debug.LogWarning("[GameSaver] TimeSystem не найден в ServiceLocator. " +
                             "Создан локальный TimeSystem для прямого запуска Market.");
        }

        private void ValidateReferences()
        {
            if (moneySystem     == null) Debug.LogError("[GameSaver] moneySystem не назначен",     this);
            if (inventory       == null) Debug.LogError("[GameSaver] inventory не назначен",       this);
            if (marketStall     == null) Debug.LogError("[GameSaver] marketStall не назначен",     this);
            if (playerTransform == null) Debug.LogError("[GameSaver] playerTransform не назначен", this);
            if (itemDatabase    == null) Debug.LogError("[GameSaver] itemDatabase не назначен",    this);
            if (npcSpawner      == null) Debug.LogError("[GameSaver] npcSpawner не назначен",      this);
        }

        private static string LoadSummary(SaveData data)
        {
            int inventoryCount = data.inventory?.Count ?? 0;
            int stallCount = data.stallSlots?.Count ?? 0;
            return $"[GameSaver] Игра загружена: v{data.version}, money={data.money:0.##}, " +
                   $"inventory={inventoryCount}, stall={stallCount}, time=День {data.day} {data.hour:00}:{data.minute:00}";
        }

        private static string SaveSummary(SaveData data, string reason)
        {
            int inventoryCount = data.inventory?.Count ?? 0;
            int stallCount = data.stallSlots?.Count ?? 0;
            return $"[GameSaver] Игра сохранена ({reason}): v{data.version}, money={data.money:0.##}, " +
                   $"inventory={inventoryCount}, stall={stallCount}, time=День {data.day} {data.hour:00}:{data.minute:00}";
        }
    }
}
