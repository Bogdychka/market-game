using System;
using System.Collections.Generic;
using Market.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Five-slot quick-access bar for the player's inventory. It reflects inventory data without
    /// owning it and exposes the selected item for future use actions.
    /// </summary>
    public class HotbarView
    {
        private const int SlotCount = 5;
        private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        private static readonly Color SlotColor = new Color(0.08f, 0.09f, 0.10f, 0.94f);
        private static readonly Color SelectedSlotColor = new Color(0.26f, 0.55f, 0.33f, 0.98f);

        private readonly Inventory _inventory;
        private readonly List<ItemSO> _items = new();
        private readonly Image[] _slotBackgrounds = new Image[SlotCount];
        private readonly Image[] _slotIcons = new Image[SlotCount];
        private readonly TMP_Text[] _slotNames = new TMP_Text[SlotCount];
        private readonly TMP_Text[] _slotCounts = new TMP_Text[SlotCount];
        private int _selectedSlot;

        /// <summary>Raised when the player selects a quick-access slot.</summary>
        public event Action<ItemSO> SelectionChanged;

        /// <summary>Currently selected item, or null when its slot is empty.</summary>
        public ItemSO SelectedItem => _selectedSlot < _items.Count ? _items[_selectedSlot] : null;

        public HotbarView(Transform parent, int layer, Inventory inventory)
        {
            _inventory = inventory;
            CreateView(parent, layer);

            if (_inventory != null)
                _inventory.OnChanged += Refresh;

            Refresh();
        }

        /// <summary>Processes the 1-5 keys while gameplay input is active.</summary>
        public void HandleInput(Keyboard keyboard)
        {
            if (keyboard == null) return;

            for (int i = 0; i < SlotCount; i++)
            {
                if (keyboard[SlotKeys[i]].wasPressedThisFrame)
                {
                    SelectSlot(i);
                    return;
                }
            }
        }

        /// <summary>Releases the inventory event subscription before its owner is destroyed.</summary>
        public void Dispose()
        {
            if (_inventory != null)
                _inventory.OnChanged -= Refresh;
        }

        private void CreateView(Transform parent, int layer)
        {
            RectTransform root = UiFactory.CreateRect("Hotbar", parent, layer);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 24f);
            root.sizeDelta = new Vector2(560f, 96f);

            HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < SlotCount; i++)
                CreateSlot(root, layer, i);
        }

        private void CreateSlot(RectTransform parent, int layer, int index)
        {
            RectTransform slot = UiFactory.CreateRect($"Slot {index + 1}", parent, layer);
            slot.sizeDelta = new Vector2(104f, 96f);
            LayoutElement layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 104f;
            layout.preferredHeight = 96f;

            Image background = UiFactory.AddImage(slot.gameObject, SlotColor);
            _slotBackgrounds[index] = background;
            Button button = slot.gameObject.AddComponent<Button>();
            int slotIndex = index;
            button.onClick.AddListener(() => SelectSlot(slotIndex));

            TMP_Text keyLabel = UiFactory.CreateText("Key", slot, layer, 14f, FontStyles.Bold, TextAlignmentOptions.Left);
            keyLabel.text = (index + 1).ToString();
            keyLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            keyLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            keyLabel.rectTransform.pivot = new Vector2(0f, 1f);
            keyLabel.rectTransform.anchoredPosition = new Vector2(8f, -6f);
            keyLabel.rectTransform.sizeDelta = new Vector2(24f, 22f);

            Image icon = UiFactory.AddImage(UiFactory.CreateRect("Icon", slot, layer).gameObject, Color.white);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 6f);
            iconRect.sizeDelta = new Vector2(36f, 36f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            _slotIcons[index] = icon;

            TMP_Text name = UiFactory.CreateText("Name", slot, layer, 12f, FontStyles.Bold, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            name.rectTransform.sizeDelta = new Vector2(-10f, 18f);
            _slotNames[index] = name;

            TMP_Text count = UiFactory.CreateText("Count", slot, layer, 15f, FontStyles.Bold, TextAlignmentOptions.Right);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 0f);
            count.rectTransform.pivot = new Vector2(1f, 0f);
            count.rectTransform.anchoredPosition = new Vector2(-7f, 5f);
            count.rectTransform.sizeDelta = new Vector2(42f, 22f);
            _slotCounts[index] = count;
        }

        private void Refresh()
        {
            RemoveMissingItems();
            AddNewItems();

            for (int i = 0; i < SlotCount; i++)
                RefreshSlot(i);

            SelectionChanged?.Invoke(SelectedItem);
        }

        private void RemoveMissingItems()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!_inventory.Items.ContainsKey(_items[i]))
                    _items.RemoveAt(i);
            }
        }

        private void AddNewItems()
        {
            foreach (KeyValuePair<ItemSO, int> entry in _inventory.Items)
            {
                if (entry.Key != null && !_items.Contains(entry.Key))
                    _items.Add(entry.Key);
            }
        }

        private void RefreshSlot(int index)
        {
            ItemSO item = index < _items.Count ? _items[index] : null;
            bool occupied = item != null;
            _slotBackgrounds[index].color = index == _selectedSlot ? SelectedSlotColor : SlotColor;
            _slotIcons[index].sprite = occupied ? item.Icon : null;
            _slotIcons[index].enabled = occupied && item.Icon != null;
            _slotNames[index].text = occupied ? item.DisplayName : "Empty";
            _slotNames[index].color = occupied ? Color.white : UiFactory.MutedText;
            _slotCounts[index].text = occupied ? $"x{_inventory.GetCount(item)}" : string.Empty;
        }

        private void SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || _selectedSlot == slotIndex) return;

            _selectedSlot = slotIndex;
            for (int i = 0; i < SlotCount; i++)
                _slotBackgrounds[i].color = i == _selectedSlot ? SelectedSlotColor : SlotColor;

            SelectionChanged?.Invoke(SelectedItem);
        }
    }
}
