using UnityEngine;
using UnityEngine.UI;

namespace Market.UI
{
    /// <summary>
    /// Screen-centre aiming dot. Builds its own overlay canvas in code, like the rest of the
    /// runtime UI, and grows into a highlighted ring while the player is aiming at something
    /// interactive - a dot that never reacts gives no feedback about what is under it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrosshairView : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Dot size in pixels when nothing interactive is under the crosshair.")]
        [SerializeField] private float idleSize = 6f;

        [Tooltip("Dot size in pixels while aiming at an interactive control.")]
        [SerializeField] private float highlightSize = 11f;

        [SerializeField] private Color idleColor = new(1f, 1f, 1f, 0.65f);
        [SerializeField] private Color highlightColor = new(0.45f, 0.85f, 1f, 1f);

        [Tooltip("Ring drawn behind the dot so it stays readable on bright water.")]
        [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 0.5f);

        private Canvas _canvas;
        private RectTransform _dot;
        private RectTransform _outline;
        private Image _dotImage;
        private bool _highlighted;

        /// <summary>Whether the crosshair currently reads as "aiming at a control".</summary>
        public bool Highlighted => _highlighted;

        /// <summary>Shows or hides the crosshair, e.g. while a menu holds the cursor.</summary>
        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        /// <summary>Switches the dot between its idle and highlighted look.</summary>
        public void SetHighlighted(bool highlighted)
        {
            EnsureBuilt();
            if (highlighted == _highlighted)
                return;

            _highlighted = highlighted;
            float size = highlighted ? highlightSize : idleSize;
            _dot.sizeDelta = new Vector2(size, size);
            _outline.sizeDelta = new Vector2(size + 4f, size + 4f);
            _dotImage.color = highlighted ? highlightColor : idleColor;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void EnsureBuilt()
        {
            if (_canvas != null)
                return;

            int layer = LayerMask.NameToLayer("UI");
            if (layer < 0)
                layer = gameObject.layer;

            GameObject canvasObject = new("Crosshair Canvas", typeof(RectTransform));
            canvasObject.layer = layer;
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the lab tuner panels: an aiming dot hidden behind a window is worse than none.
            _canvas.sortingOrder = 500;
            canvasObject.AddComponent<CanvasScaler>();

            _outline = UiFactory.CreateRect("Outline", canvasObject.transform, layer);
            Image outlineImage = UiFactory.AddImage(_outline.gameObject, outlineColor);
            outlineImage.raycastTarget = false;
            CentreDot(_outline, idleSize + 4f);

            _dot = UiFactory.CreateRect("Dot", canvasObject.transform, layer);
            _dotImage = UiFactory.AddImage(_dot.gameObject, idleColor);
            _dotImage.raycastTarget = false;
            CentreDot(_dot, idleSize);
        }

        private static void CentreDot(RectTransform rect, float size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
        }
    }
}
