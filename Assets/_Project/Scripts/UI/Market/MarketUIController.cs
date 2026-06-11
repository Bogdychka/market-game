using Market.Core;
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
            Stall
        }

        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private MoneySystem moneySystem;
        [SerializeField] private SupplierShop supplierShop;
        [SerializeField] private MarketStall marketStall;
        [SerializeField] private UIModeService uiModeService;

        [Header("Controls")]
        [Tooltip("Open/close the inventory.")]
        [SerializeField] private Key inventoryKey = Key.Tab;

        private MarketPanelView _view;
        private InventoryPanelRenderer _inventoryRenderer;
        private SupplierPanelRenderer _supplierRenderer;
        private StallPanelRenderer _stallRenderer;
        private PanelMode _mode;
        private SeasonManager _seasonManager;
        private bool _seasonEventsWired;

        private void Awake()
        {
            ResolveUIModeService();
            ValidateReferences();

            _view = new MarketPanelView(transform, gameObject.layer, ClosePanel);
            _inventoryRenderer = new InventoryPanelRenderer(_view);
            _supplierRenderer = new SupplierPanelRenderer(_view, Refresh);
            _stallRenderer = new StallPanelRenderer(_view, gameObject.layer, Refresh);
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

            if (_mode != PanelMode.None && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (uiModeService != null && uiModeService.TryConsumeCloseRequest())
                    return;

                ClosePanel();
                return;
            }

            if (keyboard[inventoryKey].wasPressedThisFrame)
                ToggleInventory();

            if (_view != null && _view.Tooltip.IsVisible)
                _view.Tooltip.UpdatePosition();
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
            }
        }

        private string MoneyText()
        {
            return moneySystem != null ? $"{Mathf.FloorToInt(moneySystem.Amount)} монет" : string.Empty;
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
            if (marketStall != null) marketStall.OpenRequested += ShowStall;
            if (inventory != null) inventory.OnChanged += Refresh;
            if (marketStall != null) marketStall.OnStockChanged += Refresh;
            if (moneySystem != null) moneySystem.OnChanged += RefreshMoney;
            if (uiModeService != null) uiModeService.CloseRequested += ClosePanel;
            WireSeasonEvents();
        }

        private void UnwireEvents()
        {
            if (supplierShop != null) supplierShop.OpenRequested -= ShowSupplier;
            if (marketStall != null) marketStall.OpenRequested -= ShowStall;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (marketStall != null) marketStall.OnStockChanged -= Refresh;
            if (moneySystem != null) moneySystem.OnChanged -= RefreshMoney;
            if (uiModeService != null) uiModeService.CloseRequested -= ClosePanel;
            UnwireSeasonEvents();
        }

        private void RefreshMoney(float amount)
        {
            Refresh();
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

        private void ResolveUIModeService()
        {
            if (uiModeService != null) return;
            uiModeService = GetComponent<UIModeService>();
        }

        private void ValidateReferences()
        {
            if (inventory    == null) Debug.LogError("[MarketUIController] inventory not assigned",    this);
            if (moneySystem  == null) Debug.LogError("[MarketUIController] moneySystem not assigned",  this);
            if (supplierShop == null) Debug.LogError("[MarketUIController] supplierShop not assigned", this);
            if (marketStall  == null) Debug.LogError("[MarketUIController] marketStall not assigned",  this);
            if (uiModeService == null) Debug.LogError("[MarketUIController] uiModeService not assigned", this);
        }
    }
}
