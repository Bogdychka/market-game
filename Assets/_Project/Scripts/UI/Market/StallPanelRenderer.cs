using System;
using System.Globalization;
using Market.Economy;
using Market.Market;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Fills the shared market panel with the stall view: occupied slots with a Remove
    /// action, and inventory rows with a price input + Place action. Warns (color +
    /// label) when the entered price is below the item's buy price.
    /// </summary>
    public class StallPanelRenderer
    {
        private const float ActionButtonWidth = 126f;
        private const float PriceInputWidth = 86f;

        private static readonly Color PriceInputDefaultColor = Color.white;
        private static readonly Color PriceInputWarningColor = new Color(1f, 0.41960785f, 0.41960785f, 1f);
        private static readonly Color DisabledActionColor = new Color(0.62f, 0.66f, 0.68f);

        private readonly MarketPanelView _view;
        private readonly int _layer;
        private readonly Action _requestRefresh;

        public StallPanelRenderer(MarketPanelView view, int layer, Action requestRefresh)
        {
            _view = view;
            _layer = layer;
            _requestRefresh = requestRefresh;
        }

        /// <summary>Render stall slots and placeable inventory into the cleared panel.</summary>
        public void Render(MarketStall stall, Inventory inventory, string subtitle)
        {
            _view.SetHeader("Прилавок", subtitle);

            _view.CreateSectionLabel("Слоты");
            if (stall == null || stall.Slots == null || stall.Slots.Length == 0)
            {
                _view.CreateEmptyText("Слоты не настроены.");
                return;
            }

            for (int i = 0; i < stall.Slots.Length; i++)
            {
                int slotIndex = i;
                StallSlot slot = stall.Slots[i];
                if (slot != null && slot.IsOccupied)
                {
                    _view.CreateActionRow(
                        slot.Item,
                        slot.Item.DisplayName,
                        $"{slot.SellPrice:0.##} монет",
                        "Снять",
                        () =>
                        {
                            stall.RemoveItem(slotIndex);
                            _requestRefresh?.Invoke();
                        });
                    continue;
                }

                _view.CreateInfoRow(null, "Пусто", $"Слот {i + 1}", string.Empty);
            }

            _view.CreateSectionLabel("Выложить товар");
            if (inventory == null || inventory.Items.Count == 0)
            {
                _view.CreateEmptyText("В инвентаре нет товаров.");
                return;
            }

            int freeSlot = FindFirstFreeSlot(stall);
            foreach (var entry in inventory.Items)
            {
                ItemSO item = entry.Key;
                int count = entry.Value;
                if (item == null) continue;

                CreatePlaceRow(stall, item, count, freeSlot >= 0);
            }
        }

        private void CreatePlaceRow(MarketStall stall, ItemSO item, int count, bool canPlace)
        {
            float suggestedPrice = stall != null ? stall.SuggestedSellPrice(item) : item.BaseSellPrice;
            RectTransform row = _view.CreateRow("PlaceRow");
            Image rowImage = row.GetComponent<Image>();
            Button rowButton = row.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            rowButton.interactable = canPlace;

            _view.AddItemIcon(row, item);

            TMP_Text titleLabel = _view.CreateRowText(
                "Title",
                row,
                $"{item.DisplayName}\n<size=70%><color=#9AA7AE>x{count}</color></size>",
                17f,
                TextAlignmentOptions.Left);
            titleLabel.rectTransform.offsetMin = new Vector2(58f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-246f, 0f);

            TMP_InputField priceInput = CreatePriceInput(row, suggestedPrice);
            TMP_Text priceWarning = CreatePriceWarning(row);
            RefreshPriceWarning(priceInput, priceWarning, item, priceInput.text);
            priceInput.onValueChanged.AddListener(value => RefreshPriceWarning(priceInput, priceWarning, item, value));

            rowButton.onClick.AddListener(() => PlaceInFirstFreeSlot(stall, item, priceInput));

            TMP_Text actionLabel = _view.CreateRowText("Action", row, "Выложить", 15f, TextAlignmentOptions.Right);
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
            actionLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
            if (!canPlace)
                actionLabel.color = DisabledActionColor;

            _view.Tooltip.AttachTrigger(row, item);
        }

        private void PlaceInFirstFreeSlot(MarketStall stall, ItemSO item, TMP_InputField priceInput)
        {
            int targetSlot = FindFirstFreeSlot(stall);
            if (targetSlot >= 0 && TryReadPrice(priceInput.text, out float price))
                stall.PlaceItem(targetSlot, item, price);

            _requestRefresh?.Invoke();
        }

        private static int FindFirstFreeSlot(MarketStall stall)
        {
            if (stall == null || stall.Slots == null) return -1;

            for (int i = 0; i < stall.Slots.Length; i++)
            {
                StallSlot slot = stall.Slots[i];
                if (slot != null && !slot.IsOccupied)
                    return i;
            }

            return -1;
        }

        private TMP_InputField CreatePriceInput(RectTransform parent, float suggestedPrice)
        {
            RectTransform rect = UiFactory.CreateRect("PriceInput", parent, _layer);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-ActionButtonWidth - 20f, 8f);
            rect.sizeDelta = new Vector2(PriceInputWidth, 26f);

            // Lighter than the row (0.14) so the field reads as an input, not a black bar.
            Image image = UiFactory.AddImage(rect.gameObject, new Color(0.24f, 0.27f, 0.31f, 1f));
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 6;

            // TMP_InputField requires a textViewport child with RectMask2D for text to render.
            RectTransform viewport = UiFactory.CreateRect("Text Area", rect, _layer);
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.StretchToParent(viewport);
            input.textViewport = viewport;

            TMP_Text text = UiFactory.CreateText("Text", viewport, _layer, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Stretch to fill the text area: without stretch anchors the offsets give a zero/negative
            // rect and the price value never renders (the field looks like an empty grey box).
            UiFactory.StretchToParent(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);

            TMP_Text placeholder = UiFactory.CreateText("Placeholder", viewport, _layer, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
            placeholder.text = "Цена";
            placeholder.color = new Color(0.45f, 0.50f, 0.54f);
            UiFactory.StretchToParent(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(6f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-6f, 0f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = Mathf.Max(0f, suggestedPrice).ToString("0.##", CultureInfo.InvariantCulture);
            return input;
        }

        private TMP_Text CreatePriceWarning(RectTransform parent)
        {
            TMP_Text warning = UiFactory.CreateText("PriceWarning", parent, _layer, 11f, FontStyles.Bold, TextAlignmentOptions.Center);
            warning.text = "< закупочной";
            warning.richText = false;
            warning.color = PriceInputWarningColor;
            warning.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform rect = warning.rectTransform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-ActionButtonWidth - 20f, -14f);
            rect.sizeDelta = new Vector2(PriceInputWidth, 14f);
            warning.gameObject.SetActive(false);
            return warning;
        }

        private static void RefreshPriceWarning(TMP_InputField priceInput, TMP_Text warningText, ItemSO item, string text)
        {
            bool showWarning = item != null
                && TryParsePrice(text, out float price)
                && price < item.BaseBuyPrice;

            if (priceInput != null && priceInput.textComponent != null)
                priceInput.textComponent.color = showWarning ? PriceInputWarningColor : PriceInputDefaultColor;

            if (warningText != null)
                warningText.gameObject.SetActive(showWarning);
        }

        private static bool TryParsePrice(string text, out float price)
        {
            string normalized = (text ?? string.Empty).Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out price);
        }

        private static bool TryReadPrice(string text, out float price)
        {
            bool parsed = TryParsePrice(text, out price);
            if (!parsed || price <= 0f)
            {
                Debug.LogWarning("[StallPanelRenderer] Invalid item price.");
                price = 0f;
                return false;
            }

            return true;
        }
    }
}
