using System.Globalization;
using Market.Core;
using Market.Economy;
using Market.Market;
using Market.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Runtime UI for the market loop: inventory, supplier shop, and stall management.
    /// Builds its uGUI/TMP view at runtime so the scene needs only one controller component.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MarketUIController : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float RowHeight = 44f;
        private const float Spacing = 8f;
        private const float IconSize = 34f;
        private const float ActionButtonWidth = 126f;
        private const float PriceInputWidth = 86f;

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

        private RectTransform _root;
        private RectTransform _content;
        private TMP_Text _titleLabel;
        private TMP_Text _subtitleLabel;
        private Button _closeButton;
        private PanelMode _mode;
        private SeasonManager _seasonManager;
        private bool _seasonEventsWired;

        private void Awake()
        {
            ResolveUIModeService();
            ValidateReferences();
            BuildUi();
            SetVisible(false);
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
            _mode = mode;
            SetVisible(true);
            uiModeService?.EnterMenuMode(this);
            Refresh();
        }

        private void ClosePanel()
        {
            _mode = PanelMode.None;
            SetVisible(false);
            uiModeService?.ExitMenuMode(this);
        }

        private void Refresh()
        {
            if (_mode == PanelMode.None || _content == null) return;

            ClearContent();

            switch (_mode)
            {
                case PanelMode.Inventory:
                    RefreshInventory();
                    break;
                case PanelMode.Supplier:
                    RefreshSupplier();
                    break;
                case PanelMode.Stall:
                    RefreshStall();
                    break;
            }
        }

        private void RefreshInventory()
        {
            SetHeader("Инвентарь", MoneyText());

            if (inventory == null || inventory.Items.Count == 0)
            {
                CreateEmptyText("Инвентарь пуст.");
                return;
            }

            foreach (var entry in inventory.Items)
            {
                ItemSO item = entry.Key;
                int count = entry.Value;
                if (item == null) continue;

                CreateInfoRow(item, item.DisplayName, $"x{count}", item.Category.ToString());
            }
        }

        private void RefreshSupplier()
        {
            SetHeader("Поставщик", SupplierSubtitle());

            if (supplierShop == null || supplierShop.StockCount == 0)
            {
                CreateEmptyText("Ассортимент пуст.");
                return;
            }

            for (int i = 0; i < supplierShop.StockCount; i++)
            {
                int index = i;
                ItemSO item = supplierShop.GetStockItem(index);
                if (item == null) continue;

                float price = supplierShop.GetBuyPrice(item);
                bool available = supplierShop.IsAvailable(item);
                Button button = CreateActionRow(
                    item,
                    item.DisplayName,
                    available ? $"{price:0.##} монет" : "Не сезон",
                    "Купить",
                    () =>
                    {
                        supplierShop.Buy(index);
                        Refresh();
                    },
                    !available);

                button.interactable = available;
                if (moneySystem != null)
                    button.interactable = button.interactable && moneySystem.Amount >= price;
            }
        }

        private void RefreshStall()
        {
            SetHeader("Прилавок", MoneyText());

            CreateSectionLabel("Слоты");
            if (marketStall == null || marketStall.Slots == null || marketStall.Slots.Length == 0)
            {
                CreateEmptyText("Слоты не настроены.");
                return;
            }

            for (int i = 0; i < marketStall.Slots.Length; i++)
            {
                int slotIndex = i;
                StallSlot slot = marketStall.Slots[i];
                if (slot != null && slot.IsOccupied)
                {
                    CreateActionRow(
                        slot.Item,
                        slot.Item.DisplayName,
                        $"{slot.SellPrice:0.##} монет",
                        "Снять",
                        () =>
                        {
                            marketStall.RemoveItem(slotIndex);
                            Refresh();
                        });
                    continue;
                }

                CreateInfoRow(null, "Пусто", $"Слот {i + 1}", string.Empty);
            }

            CreateSectionLabel("Выложить товар");
            if (inventory == null || inventory.Items.Count == 0)
            {
                CreateEmptyText("В инвентаре нет товаров.");
                return;
            }

            int freeSlot = FindFirstFreeSlot();
            foreach (var entry in inventory.Items)
            {
                ItemSO item = entry.Key;
                int count = entry.Value;
                if (item == null) continue;

                CreateStallPlaceRow(item, count, freeSlot >= 0);
            }
        }

        private int FindFirstFreeSlot()
        {
            if (marketStall == null || marketStall.Slots == null) return -1;

            for (int i = 0; i < marketStall.Slots.Length; i++)
            {
                StallSlot slot = marketStall.Slots[i];
                if (slot != null && !slot.IsOccupied)
                    return i;
            }

            return -1;
        }

        private void BuildUi()
        {
            _root = CreateRect("MarketUIRoot", transform);
            StretchToParent(_root);

            Image backdrop = AddImage(_root.gameObject, new Color(0f, 0f, 0f, 0.42f));
            backdrop.raycastTarget = true;

            RectTransform panel = CreateRect("Panel", _root);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, 0f);
            AddImage(panel.gameObject, new Color(0.08f, 0.09f, 0.10f, 0.96f));

            RectTransform header = CreateRect("Header", panel);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 96f);

            _titleLabel = CreateText("Title", header, 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            _titleLabel.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            _titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            _titleLabel.rectTransform.offsetMax = new Vector2(-84f, -12f);

            _subtitleLabel = CreateText("Subtitle", header, 16f, FontStyles.Normal, TextAlignmentOptions.Left);
            _subtitleLabel.color = new Color(0.72f, 0.78f, 0.82f);
            _subtitleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _subtitleLabel.rectTransform.offsetMin = new Vector2(24f, 8f);
            _subtitleLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);

            _closeButton = CreateButton("CloseButton", header, "X", ClosePanel);
            RectTransform closeTransform = (RectTransform)_closeButton.transform;
            closeTransform.anchorMin = new Vector2(1f, 1f);
            closeTransform.anchorMax = new Vector2(1f, 1f);
            closeTransform.pivot = new Vector2(1f, 1f);
            closeTransform.anchoredPosition = new Vector2(-20f, -20f);
            closeTransform.sizeDelta = new Vector2(48f, 40f);

            ScrollRect scroll = CreateScrollArea(panel);
            _content = (RectTransform)scroll.content;
        }

        private ScrollRect CreateScrollArea(RectTransform parent)
        {
            RectTransform viewport = CreateRect("Viewport", parent);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(20f, 20f);
            viewport.offsetMax = new Vector2(-20f, -108f);
            AddImage(viewport.gameObject, new Color(0f, 0f, 0f, 0f));
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        private void CreateStallPlaceRow(ItemSO item, int count, bool canPlace)
        {
            float suggestedPrice = marketStall != null ? marketStall.SuggestedSellPrice(item) : item.BaseSellPrice;
            RectTransform row = CreateRow("PlaceRow");
            AddItemIcon(row, item);

            TMP_Text titleLabel = CreateRowText(
                "Title",
                row,
                $"{item.DisplayName}\n<size=70%><color=#9AA7AE>x{count}</color></size>",
                17f,
                TextAlignmentOptions.Left);
            titleLabel.rectTransform.offsetMin = new Vector2(58f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-246f, 0f);

            TMP_InputField priceInput = CreatePriceInput(row, suggestedPrice);

            Button button = CreateButton(
                "Action",
                row,
                "Выложить",
                () =>
                {
                    int targetSlot = FindFirstFreeSlot();
                    if (targetSlot >= 0 && TryReadPrice(priceInput.text, out float price))
                        marketStall.PlaceItem(targetSlot, item, price);
                    Refresh();
                });

            RectTransform buttonTransform = (RectTransform)button.transform;
            buttonTransform.anchorMin = new Vector2(1f, 0.5f);
            buttonTransform.anchorMax = new Vector2(1f, 0.5f);
            buttonTransform.pivot = new Vector2(1f, 0.5f);
            buttonTransform.anchoredPosition = new Vector2(-10f, 0f);
            buttonTransform.sizeDelta = new Vector2(ActionButtonWidth, 34f);
            button.interactable = canPlace;
        }

        private void CreateInfoRow(ItemSO item, string title, string value, string detail)
        {
            RectTransform row = CreateRow("InfoRow");
            AddItemIcon(row, item);

            TMP_Text titleLabel = CreateRowText("Title", row, title, 17f, TextAlignmentOptions.Left);
            TMP_Text valueLabel = CreateRowText("Value", row, value, 15f, TextAlignmentOptions.Right);

            titleLabel.rectTransform.offsetMin = new Vector2(item != null ? 58f : 14f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-160f, 0f);
            valueLabel.rectTransform.offsetMin = new Vector2(320f, 0f);
            valueLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);

            if (!string.IsNullOrEmpty(detail))
                titleLabel.text = $"{title}\n<size=70%><color=#9AA7AE>{detail}</color></size>";
        }

        private Button CreateActionRow(
            ItemSO item,
            string title,
            string value,
            string action,
            UnityEngine.Events.UnityAction onClick,
            bool muted = false)
        {
            RectTransform row = CreateRow("ActionRow");
            if (muted)
            {
                Image rowImage = row.GetComponent<Image>();
                if (rowImage != null)
                    rowImage.color = new Color(0.10f, 0.11f, 0.12f, 0.90f);
            }

            AddItemIcon(row, item);

            TMP_Text titleLabel = CreateRowText("Title", row, $"{title}\n<size=70%><color=#9AA7AE>{value}</color></size>", 17f, TextAlignmentOptions.Left);
            titleLabel.rectTransform.offsetMin = new Vector2(item != null ? 58f : 14f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-156f, 0f);
            if (muted)
                titleLabel.color = new Color(0.62f, 0.66f, 0.68f);

            Button button = CreateButton("Action", row, action, onClick);
            RectTransform buttonTransform = (RectTransform)button.transform;
            buttonTransform.anchorMin = new Vector2(1f, 0.5f);
            buttonTransform.anchorMax = new Vector2(1f, 0.5f);
            buttonTransform.pivot = new Vector2(1f, 0.5f);
            buttonTransform.anchoredPosition = new Vector2(-10f, 0f);
            buttonTransform.sizeDelta = new Vector2(ActionButtonWidth, 34f);
            return button;
        }

        private TMP_InputField CreatePriceInput(RectTransform parent, float suggestedPrice)
        {
            RectTransform rect = CreateRect("PriceInput", parent);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-ActionButtonWidth - 20f, 0f);
            rect.sizeDelta = new Vector2(PriceInputWidth, 34f);

            Image image = AddImage(rect.gameObject, new Color(0.07f, 0.08f, 0.09f, 1f));
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 6;

            TMP_Text text = CreateText("Text", rect, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.offsetMin = new Vector2(6f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);

            TMP_Text placeholder = CreateText("Placeholder", rect, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
            placeholder.text = "Цена";
            placeholder.color = new Color(0.45f, 0.50f, 0.54f);
            placeholder.rectTransform.offsetMin = new Vector2(6f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-6f, 0f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = Mathf.Max(0f, suggestedPrice).ToString("0.##", CultureInfo.InvariantCulture);
            return input;
        }

        private void AddItemIcon(RectTransform row, ItemSO item)
        {
            if (item == null) return;

            RectTransform icon = CreateRect("Icon", row);
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(12f, 0f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);

            Image image = AddImage(icon.gameObject, CategoryColor(item.Category));
            image.raycastTarget = false;

            if (item.Icon != null)
            {
                image.sprite = item.Icon;
                image.preserveAspect = true;
                image.color = Color.white;
                return;
            }

            TMP_Text fallback = CreateText("Letter", icon, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            fallback.text = IconLetter(item);
            StretchToParent(fallback.rectTransform);
        }

        private bool TryReadPrice(string text, out float price)
        {
            string normalized = text.Replace(',', '.');
            bool parsed = float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out price);
            if (!parsed || price <= 0f)
            {
                Debug.LogWarning("[MarketUIController] Invalid item price.", this);
                price = 0f;
                return false;
            }

            return true;
        }

        private static Color CategoryColor(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Food => new Color(0.35f, 0.55f, 0.28f),
                ItemCategory.Fish => new Color(0.22f, 0.46f, 0.66f),
                ItemCategory.Animal => new Color(0.55f, 0.42f, 0.28f),
                ItemCategory.Craft => new Color(0.45f, 0.45f, 0.52f),
                ItemCategory.Flower => new Color(0.62f, 0.34f, 0.52f),
                ItemCategory.Ingredient => new Color(0.60f, 0.50f, 0.30f),
                ItemCategory.Tool => new Color(0.36f, 0.48f, 0.50f),
                _ => new Color(0.38f, 0.42f, 0.45f)
            };
        }

        private static string IconLetter(ItemSO item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.DisplayName))
                return "?";

            return item.DisplayName.Substring(0, 1).ToUpperInvariant();
        }

        private RectTransform CreateRow(string name)
        {
            RectTransform row = CreateRect(name, _content);
            row.sizeDelta = new Vector2(0f, RowHeight);
            AddImage(row.gameObject, new Color(0.14f, 0.16f, 0.18f, 0.95f));

            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = RowHeight;
            layout.minHeight = RowHeight;
            return row;
        }

        private void CreateSectionLabel(string text)
        {
            TMP_Text label = CreateText("Section", _content, 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            label.text = text.ToUpperInvariant();
            label.color = new Color(0.72f, 0.78f, 0.82f);

            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;
        }

        private void CreateEmptyText(string text)
        {
            TMP_Text label = CreateText("Empty", _content, 17f, FontStyles.Normal, TextAlignmentOptions.Center);
            label.text = text;
            label.color = new Color(0.72f, 0.78f, 0.82f);

            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.minHeight = 72f;
        }

        private TMP_Text CreateRowText(string name, RectTransform parent, string text, float size, TextAlignmentOptions alignment)
        {
            TMP_Text label = CreateText(name, parent, size, FontStyles.Normal, alignment);
            label.text = text;
            StretchToParent(label.rectTransform);
            return label;
        }

        private Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect.gameObject, new Color(0.22f, 0.45f, 0.55f, 1f));

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", rect, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            StretchToParent(text.rectTransform);
            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.layer = gameObject.layer;
            obj.transform.SetParent(parent, false);

            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.layer = gameObject.layer;
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        private Image AddImage(GameObject obj, Color color)
        {
            Image image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private void SetHeader(string title, string subtitle)
        {
            _titleLabel.text = title;
            _subtitleLabel.text = subtitle;
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

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
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

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
