using Market.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Floating item tooltip (name, description, base prices) that follows the mouse.
    /// Built in code; parent it to a panel root so it renders above the panel content.
    /// </summary>
    public class ItemTooltipView
    {
        private const float Width = 220f;

        private readonly RectTransform _panel;
        private readonly TMP_Text _nameText;
        private readonly TMP_Text _descText;
        private readonly TMP_Text _metaText;

        public ItemTooltipView(RectTransform parent, int layer)
        {
            _panel = UiFactory.CreateRect("ItemTooltip", parent, layer);
            _panel.pivot     = new Vector2(0f, 0f);
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.zero;
            _panel.sizeDelta = new Vector2(Width, 0f);

            Image bg = UiFactory.AddImage(_panel.gameObject, new Color(0.06f, 0.07f, 0.08f, 0.97f));
            bg.raycastTarget = false;

            VerticalLayoutGroup layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding             = new RectOffset(12, 12, 10, 10);
            layout.spacing             = 4f;
            layout.childControlHeight  = true;
            layout.childControlWidth   = true;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _nameText = UiFactory.CreateText("TooltipName", _panel, layer, 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            _nameText.textWrappingMode = TextWrappingModes.Normal;

            _descText = UiFactory.CreateText("TooltipDesc", _panel, layer, 13f, FontStyles.Normal, TextAlignmentOptions.Left);
            _descText.color            = UiFactory.MutedText;
            _descText.textWrappingMode = TextWrappingModes.Normal;

            _metaText = UiFactory.CreateText("TooltipMeta", _panel, layer, 12f, FontStyles.Normal, TextAlignmentOptions.Left);
            _metaText.color            = new Color(0.55f, 0.65f, 0.45f);
            _metaText.textWrappingMode = TextWrappingModes.Normal;

            _panel.gameObject.SetActive(false);
        }

        /// <summary>True while the tooltip is shown; the owner repositions it from Update.</summary>
        public bool IsVisible => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>Show the tooltip for the given item.</summary>
        public void Show(ItemSO item)
        {
            if (_panel == null || item == null) return;

            _nameText.text = item.DisplayName;

            bool hasDesc = !string.IsNullOrWhiteSpace(item.Description);
            _descText.gameObject.SetActive(hasDesc);
            if (hasDesc) _descText.text = item.Description;

            _metaText.text =
                $"Закупка: {item.BaseBuyPrice:0.##} | Продажа: {item.BaseSellPrice:0.##}";

            _panel.gameObject.SetActive(true);
            UpdatePosition();
        }

        /// <summary>Hide the tooltip.</summary>
        public void Hide()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        /// <summary>Move the tooltip panel to follow the mouse cursor.</summary>
        public void UpdatePosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            float x = Mathf.Clamp(pos.x + 18f, 0f, Mathf.Max(0f, Screen.width - Width));
            float y = pos.y + 20f;
            _panel.position = new Vector3(x, y, 0f);
        }

        /// <summary>Add an <see cref="ItemTooltipTrigger"/> to a row for the given item.</summary>
        public void AttachTrigger(RectTransform row, ItemSO item)
        {
            ItemTooltipTrigger trigger = row.gameObject.AddComponent<ItemTooltipTrigger>();
            trigger.Setup(item, Show, Hide);
        }
    }
}
