using System;
using System.Collections.Generic;
using Market.Core;
using Market.Core.Events;
using Market.Economy;
using Market.Market;
using Market.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.UI
{
    /// <summary>
    /// Coordinator for the market-loop UI: inventory, supplier shop, and stall panels.
    /// Owns the scene references, panel switching, and event wiring; view construction
    /// lives in <see cref="MarketPanelView"/>/<see cref="UiFactory"/>, per-panel content
    /// in the panel renderers.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MarketUIController : MonoBehaviour
    {
        private enum PanelMode
        {
            None,
            Inventory,
            Supplier,
            Stall,
            EveningSummary
        }

        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private MoneySystem moneySystem;
        [SerializeField] private SupplierShop supplierShop;
        [SerializeField] private MarketStallRegistry stallRegistry;
        [SerializeField] private MarketStall marketStall;
        [SerializeField] private UIModeService uiModeService;

        [Header("Controls")]
        [Tooltip("Open/close the inventory.")]
        [SerializeField] private Key inventoryKey = Key.Tab;

        private MarketPanelView _view;
        private InventoryPanelRenderer _inventoryRenderer;
        private HotbarView _hotbarView;
        private SupplierPanelRenderer _supplierRenderer;
        private StallPanelRenderer _stallRenderer;
        private EveningSummaryPanelRenderer _eveningSummaryRenderer;
        private PanelMode _mode;
        private SeasonManager _seasonManager;
        private EventBus _eventBus;
        private TimeSystem _timeSystem;
        private DailySummarySystem _dailySummarySystem;
        private DailySummarySnapshot _activeSummary;
        private bool _hasActiveSummary;
        private int _lastSummaryDayShown = -1;
        private bool _seasonEventsWired;

        private void Awake()
        {
            ResolveUIModeService();
            ResolveStallRegistry();
            ValidateReferences();

            _view = new MarketPanelView(transform, gameObject.layer, ClosePanel);
            _inventoryRenderer = new InventoryPanelRenderer(_view);
            _hotbarView = new HotbarView(transform, gameObject.layer, inventory);
            _supplierRenderer = new SupplierPanelRenderer(_view, Refresh);
            _stallRenderer = new StallPanelRenderer(_view, gameObject.layer, Refresh);
            _eveningSummaryRenderer = new EveningSummaryPanelRenderer(_view);
            _view.SetVisible(false);
        }

        private void OnEnable()
        {
            WireEvents();
        }

        private void OnDisable()
        {
            UnwireEvents();
            uiModeService?.ExitMenuMode(this);
        }

        private void Start()
        {
            WireSeasonEvents();
            Refresh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (HandleCloseInput(keyboard))
                return;

            HandleInventoryInput(keyboard);
            if (uiModeService == null || !uiModeService.IsMenuMode)
                _hotbarView.HandleInput(keyboard);
            UpdateTooltipPosition();
        }

        private void OnDestroy()
        {
            _hotbarView?.Dispose();
        }

        /// <summary>Opens the inventory panel.</summary>
        public void ShowInventory()
        {
            OpenPanel(PanelMode.Inventory);
        }

        private void ToggleInventory()
        {
            if (_mode == PanelMode.Inventory)
            {
                ClosePanel();
                return;
            }

            ShowInventory();
        }

        private bool HandleCloseInput(Keyboard keyboard)
        {
            if (_mode == PanelMode.None || !keyboard.escapeKey.wasPressedThisFrame)
                return false;

            if (uiModeService != null && uiModeService.TryConsumeCloseRequest())
                return true;

            ClosePanel();
            return true;
        }

        private void HandleInventoryInput(Keyboard keyboard)
        {
            if (keyboard[inventoryKey].wasPressedThisFrame)
                ToggleInventory();
        }

        private void UpdateTooltipPosition()
        {
            if (_view != null && _view.Tooltip.IsVisible)
                _view.Tooltip.UpdatePosition();
        }

        private void ShowSupplier(SupplierShop shop, GameObject actor)
        {
            supplierShop = shop;
            OpenPanel(PanelMode.Supplier);
        }

        private void ShowStall(MarketStall stall, GameObject actor)
        {
            marketStall = stall;
            OpenPanel(PanelMode.Stall);
        }

        private void OpenPanel(PanelMode mode)
        {
            _view.Tooltip.Hide();
            _mode = mode;
            _view.SetVisible(true);
            uiModeService?.EnterMenuMode(this);
            Refresh();
        }

        private void ClosePanel()
        {
            _mode = PanelMode.None;
            _view.SetVisible(false);
            uiModeService?.ExitMenuMode(this);
            _view.Tooltip.Hide();
        }

        private void Refresh()
        {
            if (_mode == PanelMode.None || _view == null) return;

            _view.ClearContent();

            switch (_mode)
            {
                case PanelMode.Inventory:
                    _inventoryRenderer.Render(inventory, MoneyText());
                    break;
                case PanelMode.Supplier:
                    _supplierRenderer.Render(supplierShop, moneySystem, SupplierSubtitle());
                    break;
                case PanelMode.Stall:
                    _stallRenderer.Render(marketStall, inventory, MoneyText());
                    break;
                case PanelMode.EveningSummary:
                    _eveningSummaryRenderer.Render(CreateSummarySnapshot());
                    break;
            }
        }

        private string MoneyText()
        {
            return moneySystem != null ? $"{moneySystem.Amount} coins" : string.Empty;
        }

        private string SupplierSubtitle()
        {
            string money = MoneyText();
            if (_seasonManager == null)
                ResolveSeasonManager();

            if (_seasonManager == null)
                return money;

            string season = SeasonManager.GetName(_seasonManager.CurrentSeason);
            return string.IsNullOrEmpty(money) ? season : $"{money} | {season}";
        }

        private void WireEvents()
        {
            if (supplierShop != null) supplierShop.OpenRequested += ShowSupplier;
            WireStallEvents();
            if (inventory != null) inventory.OnChanged += Refresh;
            if (moneySystem != null) moneySystem.OnChanged += RefreshMoney;
            if (uiModeService != null) uiModeService.CloseRequested += ClosePanel;
            WireSummaryEvents();
            WireSeasonEvents();
        }

        private void UnwireEvents()
        {
            if (supplierShop != null) supplierShop.OpenRequested -= ShowSupplier;
            UnwireStallEvents();
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (moneySystem != null) moneySystem.OnChanged -= RefreshMoney;
            if (uiModeService != null) uiModeService.CloseRequested -= ClosePanel;
            UnwireSummaryEvents();
            UnwireSeasonEvents();
        }

        private void WireStallEvents()
        {
            foreach (MarketStall stall in GetRegisteredStalls())
            {
                if (stall == null) continue;
                stall.OpenRequested += ShowStall;
                stall.OnStockChanged += Refresh;
            }
        }

        private void UnwireStallEvents()
        {
            foreach (MarketStall stall in GetRegisteredStalls())
            {
                if (stall == null) continue;
                stall.OpenRequested -= ShowStall;
                stall.OnStockChanged -= Refresh;
            }
        }

        private void RefreshMoney(int amount)
        {
            Refresh();
        }

        private void WireSummaryEvents()
        {
            ResolveSummaryServices();
            if (_eventBus != null)
                _eventBus.Subscribe<DailySummaryReadyEvent>(HandleDailySummaryReady);
        }

        private void UnwireSummaryEvents()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<DailySummaryReadyEvent>(HandleDailySummaryReady);
        }

        private void HandleDailySummaryReady(DailySummaryReadyEvent evt)
        {
            if (_lastSummaryDayShown == evt.Summary.Day)
                return;

            _activeSummary = evt.Summary;
            _hasActiveSummary = true;
            _lastSummaryDayShown = evt.Summary.Day;
            OpenPanel(PanelMode.EveningSummary);
        }

        private DailySummarySnapshot CreateSummarySnapshot()
        {
            if (_hasActiveSummary)
                return _activeSummary;

            ResolveSummaryServices();
            return _dailySummarySystem != null
                ? _dailySummarySystem.CreateSnapshot(CurrentDay())
                : new DailySummarySnapshot(CurrentDay(), 0f, 0f, 0f, 0, 0, null, 0, 0f);
        }

        private int CurrentDay()
        {
            ResolveSummaryServices();
            return _timeSystem != null ? _timeSystem.Day : 1;
        }

        private void RefreshSeasonalSupplier(Season season)
        {
            if (_mode == PanelMode.Supplier)
                Refresh();
        }

        private void WireSeasonEvents()
        {
            if (_seasonEventsWired) return;

            ResolveSeasonManager();
            if (_seasonManager == null) return;

            _seasonManager.OnSeasonChanged += RefreshSeasonalSupplier;
            _seasonEventsWired = true;
        }

        private void UnwireSeasonEvents()
        {
            if (!_seasonEventsWired || _seasonManager == null) return;

            _seasonManager.OnSeasonChanged -= RefreshSeasonalSupplier;
            _seasonEventsWired = false;
        }

        private void ResolveSeasonManager()
        {
            if (_seasonManager != null) return;
            ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
        }

        private void ResolveSummaryServices()
        {
            if (_eventBus == null)
                ServiceLocator.TryGet<EventBus>(out _eventBus);
            if (_timeSystem == null)
                ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
            if (_dailySummarySystem == null)
                ServiceLocator.TryGet<DailySummarySystem>(out _dailySummarySystem);
        }

        private void ResolveUIModeService()
        {
            if (uiModeService != null) return;
            uiModeService = GetComponent<UIModeService>();
        }

        private void ResolveStallRegistry()
        {
            if (stallRegistry != null) return;
            ServiceLocator.TryGet<MarketStallRegistry>(out stallRegistry);
        }

        private IEnumerable<MarketStall> GetRegisteredStalls()
        {
            ResolveStallRegistry();
            if (stallRegistry != null && stallRegistry.Count > 0)
                return stallRegistry.Stalls;

            return marketStall != null
                ? new[] { marketStall }
                : Array.Empty<MarketStall>();
        }

        private void ValidateReferences()
        {
            if (inventory    == null) Debug.LogError("[MarketUIController] inventory not assigned",    this);
            if (moneySystem  == null) Debug.LogError("[MarketUIController] moneySystem not assigned",  this);
            if (supplierShop == null) Debug.LogError("[MarketUIController] supplierShop not assigned", this);
            if ((stallRegistry == null || stallRegistry.Count == 0) && marketStall == null)
                Debug.LogError("[MarketUIController] stallRegistry not assigned or empty", this);
            if (uiModeService == null) Debug.LogError("[MarketUIController] uiModeService not assigned", this);
        }
    }
}
