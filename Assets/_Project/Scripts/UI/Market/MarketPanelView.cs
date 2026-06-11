using Market.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Shared chrome for the market-loop panels (inventory, supplier, stall):
    /// backdrop, side panel, header with title/subtitle/close, scrollable content,
    /// row widgets, and the hover tooltip. Panel renderers fill the content.
    /// </summary>
    public class MarketPanelView
    {
        private const float PanelWidth = 560f;
        private const float RowHeight = 44f;
        private const float Spacing = 8f;
        private const float IconSize = 34f;

        private static readonly Color MutedRowBackground = new Color(0.10f, 0.11f, 0.12f, 0.90f);
        private static readonly Color MutedRowText = new Color(0.62f, 0.66f, 0.68f);

        private readonly int _layer;
        private readonly RectTransform _root;
        private readonly RectTransform _content;
        private readonly TMP_Text _titleLabel;
        private readonly TMP_Text _subtitleLabel;
        private readonly ItemTooltipView _tooltip;

        public RectTransform Content => _content;
        public ItemTooltipView Tooltip => _tooltip;

        public MarketPanelView(Transform parent, int layer, UnityAction onClose)
        {
            _layer = layer;

            _root = UiFactory.CreateRect("MarketUIRoot", parent, layer);
            UiFactory.StretchToParent(_root);

            Image backdrop = UiFactory.AddImage(_root.gameObject, new Color(0f, 0f, 0f, 0.42f));
            backdrop.raycastTarget = true;

            RectTransform panel = UiFactory.CreateRect("Panel", _root, layer);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, 0f);
            UiFactory.AddImage(panel.gameObject, UiFactory.PanelBackground);

            RectTransform header = UiFactory.CreateRect("Header", panel, layer);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 96f);

            _titleLabel = UiFactory.CreateText("Title", header, layer, 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            _titleLabel.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            _titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            _titleLabel.rectTransform.offsetMax = new Vector2(-84f, -12f);

            _subtitleLabel = UiFactory.CreateText("Subtitle", header, layer, 16f, FontStyles.Normal, TextAlignmentOptions.Left);
            _subtitleLabel.color = UiFactory.MutedText;
            _subtitleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _subtitleLabel.rectTransform.offsetMin = new Vector2(24f, 8f);
            _subtitleLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);

            Button closeButton = UiFactory.CreateButton("CloseButton", header, layer, "X", onClose);
            RectTransform closeTransform = (RectTransform)closeButton.transform;
            closeTransform.anchorMin = new Vector2(1f, 1f);
            closeTransform.anchorMax = new Vector2(1f, 1f);
            closeTransform.pivot = new Vector2(1f, 1f);
            closeTransform.anchoredPosition = new Vector2(-20f, -20f);
            closeTransform.sizeDelta = new Vector2(48f, 40f);

            ScrollRect scroll = CreateScrollArea(panel);
            _content = (RectTransform)scroll.content;

            _tooltip = new ItemTooltipView(_root, layer);
        }

        private ScrollRect CreateScrollArea(RectTransform parent)
        {
            RectTransform viewport = UiFactory.CreateRect("Viewport", parent, _layer);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(20f, 20f);
            viewport.offsetMax = new Vector2(-20f, -108f);
            // Transparent raycast catcher so ScrollRect drags register over empty areas.
            // NOTE: clip with RectMask2D, NOT Mask. A legacy Mask whose graphic has alpha 0
            // gets culled by the canvas, so it never writes the stencil and ALL masked
            // children disappear (while still receiving raycasts). RectMask2D clips by rect
            // and needs no graphic, so rows stay visible.
            Image viewportImage = UiFactory.AddImage(viewport.gameObject, new Color(0f, 0f, 0f, 0f));
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiFactory.CreateRect("Content", viewport, _layer);
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

        /// <summary>Set the panel title and subtitle texts.</summary>
        public void SetHeader(string title, string subtitle)
        {
            _titleLabel.text = title;
            _subtitleLabel.text = subtitle;
        }

        /// <summary>Show or hide the whole panel root.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        /// <summary>Destroy all content rows (called before each refresh).</summary>
        public void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Object.Destroy(_content.GetChild(i).gameObject);
        }

        /// <summary>Create an empty content row with background and fixed height.</summary>
        public RectTransform CreateRow(string name)
        {
            RectTransform row = UiFactory.CreateRect(name, _content, _layer);
            row.sizeDelta = new Vector2(0f, RowHeight);
            UiFactory.AddImage(row.gameObject, new Color(0.14f, 0.16f, 0.18f, 0.95f));
            UiFactory.AddLayoutHeight(row.gameObject, RowHeight);
            return row;
        }

        /// <summary>Create a stretched text label inside a row.</summary>
        public TMP_Text CreateRowText(string name, RectTransform parent, string text, float size, TextAlignmentOptions alignment)
        {
            TMP_Text label = UiFactory.CreateText(name, parent, _layer, size, FontStyles.Normal, alignment);
            label.text = text;
            UiFactory.StretchToParent(label.rectTransform);
            return label;
        }

        /// <summary>Create an uppercase section divider label.</summary>
        public void CreateSectionLabel(string text)
        {
            TMP_Text label = UiFactory.CreateText("Section", _content, _layer, 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            label.text = text.ToUpperInvariant();
            label.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(label.gameObject, 28f);
        }

        /// <summary>Create a centered empty-state message.</summary>
        public void CreateEmptyText(string text)
        {
            TMP_Text label = UiFactory.CreateText("Empty", _content, _layer, 17f, FontStyles.Normal, TextAlignmentOptions.Center);
            label.text = text;
            label.color = UiFactory.MutedText;
            UiFactory.AddLayoutHeight(label.gameObject, 72f);
        }

        /// <summary>Create a non-interactive row: icon, title (+optional detail), right value.</summary>
        public void CreateInfoRow(ItemSO item, string title, string value, string detail)
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

            if (item != null)
                _tooltip.AttachTrigger(row, item);
        }

        /// <summary>Create a clickable row: icon, title with value line, right action label.</summary>
        public Button CreateActionRow(
            ItemSO item,
            string title,
            string value,
            string action,
            UnityAction onClick,
            bool muted = false)
        {
            RectTransform row = CreateRow("ActionRow");
            Image rowImage = row.GetComponent<Image>();
            if (muted && rowImage != null)
                rowImage.color = MutedRowBackground;

            Button rowButton = row.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            rowButton.onClick.AddListener(onClick);

            AddItemIcon(row, item);

            TMP_Text titleLabel = CreateRowText("Title", row, $"{title}\n<size=70%><color=#9AA7AE>{value}</color></size>", 17f, TextAlignmentOptions.Left);
            titleLabel.rectTransform.offsetMin = new Vector2(item != null ? 58f : 14f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-156f, 0f);
            if (muted)
                titleLabel.color = MutedRowText;

            TMP_Text actionLabel = CreateRowText("Action", row, action, 15f, TextAlignmentOptions.Right);
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
            actionLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
            if (muted)
                actionLabel.color = MutedRowText;

            if (item != null)
                _tooltip.AttachTrigger(row, item);

            return rowButton;
        }

        /// <summary>Add the item icon (sprite or first-letter fallback over category color).</summary>
        public void AddItemIcon(RectTransform row, ItemSO item)
        {
            if (item == null) return;

            RectTransform icon = UiFactory.CreateRect("Icon", row, _layer);
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(12f, 0f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);

            Image background = UiFactory.AddImage(icon.gameObject, CategoryColor(item.Category));
            background.raycastTarget = false;

            if (item.Icon != null)
            {
                RectTransform spriteRect = UiFactory.CreateRect("Sprite", icon, _layer);
                UiFactory.StretchToParent(spriteRect);
                spriteRect.offsetMin = new Vector2(3f, 3f);
                spriteRect.offsetMax = new Vector2(-3f, -3f);

                Image spriteImage = UiFactory.AddImage(spriteRect.gameObject, Color.white);
                spriteImage.raycastTarget = false;
                spriteImage.sprite = item.Icon;
                spriteImage.preserveAspect = true;
                return;
            }

            // No sprite assigned — fall back to the first letter over the category color.
            TMP_Text fallback = UiFactory.CreateText("Letter", icon, _layer, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            fallback.text = IconLetter(item);
            fallback.color = new Color(1f, 1f, 1f, 0.82f);
            UiFactory.StretchToParent(fallback.rectTransform);
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
    }
}
