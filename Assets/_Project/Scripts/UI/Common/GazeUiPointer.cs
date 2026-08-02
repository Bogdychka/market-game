using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Market.UI
{
    /// <summary>
    /// Drives world-space uGUI from the centre of the screen so an FPS player can use real
    /// buttons and sliders with the cursor locked: the crosshair is the pointer and the left
    /// mouse button is the click.
    /// <para>
    /// Sliders work because the pointer position stays at the screen centre while the camera
    /// turns - the same screen point maps to a moving point on a world-space rect, so aiming
    /// along the slider drags its handle.
    /// </para>
    /// While the cursor is unlocked (a menu is open) the pointer stands down and hands the
    /// EventSystem back to the normal mouse input module.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GazeUiPointer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera the ray starts from. Empty = Camera.main.")]
        [SerializeField] private Camera sourceCamera;

        [Tooltip("Crosshair told when an interactive control is under the pointer.")]
        [SerializeField] private CrosshairView crosshair;

        [Header("Settings")]
        [Tooltip("How far the player can reach a control, in metres.")]
        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Only take over while the cursor is locked, so menus keep the real mouse.")]
        [SerializeField] private bool onlyWhenCursorLocked = true;

        private readonly List<RaycastResult> _raycastResults = new();

        private EventSystem _eventSystem;
        private BaseInputModule _inputModule;
        private PointerEventData _pointerData;
        private InputAction _clickAction;
        private GameObject _hovered;
        private GameObject _pressed;
        private GameObject _dragging;
        private bool _active;

        /// <summary>Object currently under the crosshair, if it handles pointer events.</summary>
        public GameObject Hovered => _hovered;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _clickAction?.Enable();
        }

        private void OnDisable()
        {
            ReleasePress(false);
            SetHovered(null);
            SetActive(false);
        }

        private void Update()
        {
            if (_eventSystem == null || sourceCamera == null)
                return;

            bool shouldRun = !onlyWhenCursorLocked ||
                Cursor.lockState == CursorLockMode.Locked;
            SetActive(shouldRun);
            if (!shouldRun)
                return;

            UpdatePointerPosition();
            GameObject target = RaycastForHandler();
            SetHovered(target);

            if (WasClickPressedThisFrame())
                BeginPress(target);
            else if (_pressed != null && IsClickHeld())
                DragPress();
            else if (_pressed != null)
                ReleasePress(true);
        }

        private void ResolveReferences()
        {
            if (sourceCamera == null)
                sourceCamera = Camera.main;

            if (crosshair == null)
                crosshair = GetComponentInChildren<CrosshairView>();

            _eventSystem = EventSystem.current;
            if (_eventSystem != null)
                _inputModule = _eventSystem.GetComponent<BaseInputModule>();

            if (_eventSystem != null && _pointerData == null)
                _pointerData = new PointerEventData(_eventSystem);

            if (_clickAction == null)
            {
                // The Player map's Attack action is the project's left mouse button; falling back
                // to the device keeps the pointer working in scenes with no PlayerInput.
                var playerInput = GetComponentInParent<PlayerInput>();
                InputActionAsset actions = playerInput != null ? playerInput.actions : null;
                _clickAction = actions != null ? actions.FindAction("Attack", false) : null;
            }
        }

        private void SetActive(bool active)
        {
            if (active == _active)
                return;

            _active = active;
            if (crosshair != null)
                crosshair.SetVisible(active);

            // Two pointers on one EventSystem fight over the same widgets: the module's mouse
            // pointer sits wherever the OS left it, which is not where the player is aiming.
            if (_inputModule is InputSystemUIInputModule mouseModule)
                mouseModule.enabled = !active;

            if (!active)
            {
                ReleasePress(false);
                SetHovered(null);
            }
        }

        private void UpdatePointerPosition()
        {
            Vector2 screenCentre = new(Screen.width * 0.5f, Screen.height * 0.5f);
            _pointerData.position = screenCentre;
            _pointerData.delta = Vector2.zero;
            _pointerData.button = PointerEventData.InputButton.Left;
            _pointerData.pressPosition = screenCentre;
            _pointerData.pointerId = -1;
        }

        private GameObject RaycastForHandler()
        {
            _raycastResults.Clear();
            _eventSystem.RaycastAll(_pointerData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                RaycastResult result = _raycastResults[i];
                if (result.gameObject == null || result.distance > maxDistance)
                    continue;

                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    result.gameObject);
                if (handler == null)
                {
                    handler = ExecuteEvents.GetEventHandler<IDragHandler>(result.gameObject);
                    if (handler == null)
                        continue;
                }

                // pressEventCamera / enterEventCamera are derived from the stored raycast results,
                // which is why the raycast is assigned rather than the camera.
                _pointerData.pointerCurrentRaycast = result;
                return handler;
            }

            _pointerData.pointerCurrentRaycast = default;
            return null;
        }

        private void SetHovered(GameObject target)
        {
            if (target == _hovered)
                return;

            if (_hovered != null)
                ExecuteEvents.Execute(_hovered, _pointerData, ExecuteEvents.pointerExitHandler);

            _hovered = target;
            _pointerData.pointerEnter = target;

            if (_hovered != null)
                ExecuteEvents.Execute(_hovered, _pointerData, ExecuteEvents.pointerEnterHandler);

            if (crosshair != null)
                crosshair.SetHighlighted(_hovered != null);
        }

        private void BeginPress(GameObject target)
        {
            ReleasePress(false);
            if (target == null)
                return;

            _pressed = target;
            _pointerData.pointerPress = target;
            _pointerData.rawPointerPress = target;
            _pointerData.pressPosition = _pointerData.position;
            _pointerData.pointerPressRaycast = _pointerData.pointerCurrentRaycast;
            _pointerData.eligibleForClick = true;
            _pointerData.dragging = false;

            ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.pointerDownHandler);

            _dragging = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            _pointerData.pointerDrag = _dragging;
            if (_dragging != null)
            {
                ExecuteEvents.Execute(
                    _dragging, _pointerData, ExecuteEvents.beginDragHandler);
                _pointerData.dragging = true;
            }
        }

        private void DragPress()
        {
            if (_dragging == null)
                return;

            // The screen position never moves; the camera does. Re-sending the drag every frame
            // is what turns "look along the slider" into a value change.
            ExecuteEvents.Execute(_dragging, _pointerData, ExecuteEvents.dragHandler);
        }

        private void ReleasePress(bool allowClick)
        {
            if (_pressed == null && _dragging == null)
                return;

            if (_pressed != null)
                ExecuteEvents.Execute(_pressed, _pointerData, ExecuteEvents.pointerUpHandler);

            if (allowClick && _pressed != null && _pressed == _hovered &&
                _pointerData.eligibleForClick)
            {
                ExecuteEvents.Execute(
                    _pressed, _pointerData, ExecuteEvents.pointerClickHandler);
            }

            if (_dragging != null)
                ExecuteEvents.Execute(_dragging, _pointerData, ExecuteEvents.endDragHandler);

            _pressed = null;
            _dragging = null;
            _pointerData.pointerPress = null;
            _pointerData.rawPointerPress = null;
            _pointerData.pointerDrag = null;
            _pointerData.dragging = false;
            _pointerData.eligibleForClick = false;
        }

        private bool WasClickPressedThisFrame()
        {
            if (_clickAction != null)
                return _clickAction.WasPressedThisFrame();

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private bool IsClickHeld()
        {
            if (_clickAction != null)
                return _clickAction.IsPressed();

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }
}
