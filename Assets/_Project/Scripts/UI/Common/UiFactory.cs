using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Shared primitives for building runtime uGUI/TMP views in code.
    /// Single home for the rect/text/button/image helpers so every panel follows the
    /// same construction rules instead of keeping a private copy per controller.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>Dark panel background shared by runtime menus.</summary>
        public static readonly Color PanelBackground = new Color(0.08f, 0.09f, 0.10f, 0.96f);

        /// <summary>Primary action-button background.</summary>
        public static readonly Color ButtonBackground = new Color(0.22f, 0.45f, 0.55f, 1f);

        /// <summary>Muted secondary text (subtitles, hints, empty states).</summary>
        public static readonly Color MutedText = new Color(0.72f, 0.78f, 0.82f);

        /// <summary>Create an empty RectTransform child on the given layer.</summary>
        public static RectTransform CreateRect(string name, Transform parent, int layer)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.layer = layer;
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        /// <summary>Add a flat-color Image to an existing object.</summary>
        public static Image AddImage(GameObject obj, Color color)
        {
            Image image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Create a TMP label with the project's default text settings.</summary>
        public static TMP_Text CreateText(string name, Transform parent, int layer, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.layer = layer;
            obj.transform.SetParent(parent, false);

            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        /// <summary>Create a button with a bold centered label stretched to the button rect.</summary>
        public static Button CreateButton(string name, Transform parent, int layer, string label, UnityAction onClick, float labelFontSize = 16f)
        {
            RectTransform rect = CreateRect(name, parent, layer);
            Image image = AddImage(rect.gameObject, ButtonBackground);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", rect, layer, labelFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            StretchToParent(text.rectTransform);
            return button;
        }

        /// <summary>
        /// Stretch a rect to fill its parent. Anchors must be stretched BEFORE applying
        /// offsetMin/offsetMax insets: with collapsed anchors the offsets produce a
        /// zero/negative-size rect and the element never renders.
        /// </summary>
        public static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Place a rect by top-left corner and size, in the parent's pixel space. For panels laid
        /// out from a table of rows rather than by a layout group, where "row 3 sits 96 px down"
        /// is the natural way to express the position.
        /// Anchors and pivot are set to the parent's top-left before the offsets, per the same
        /// rule as <see cref="StretchToParent"/>.
        /// </summary>
        public static void PlaceTopLeft(
            RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        /// <summary>Pin min/preferred height for a layout-group child.</summary>
        public static LayoutElement AddLayoutHeight(GameObject obj, float height)
        {
            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return layout;
        }
    }
}
