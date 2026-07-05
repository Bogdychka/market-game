using System;
using Market.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.Interaction
{
    /// <summary>
    /// Raycasts from the camera to find IInteractable objects. Fires CurrentChanged when the target changes.
    /// On the Interact input action, calls Interact() on the current target.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cam;
        [SerializeField] private PlayerInput playerInput;

        [Header("Raycast")]
        [SerializeField] private float maxDistance = 2.5f;
        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        public event Action<IInteractable> CurrentChanged;
        public IInteractable Current => _current;
        /// <summary>PlayerInput used by the interaction action and prompt display.</summary>
        public PlayerInput PlayerInput => playerInput;
        /// <summary>Cached Interact action used to trigger the current target.</summary>
        public InputAction InteractAction => _interactAction;

        private InputAction _interactAction;
        private IInteractable _current;

        // Probe cache: avoids calling GetComponentInParent every frame
        // while the ray hits the same collider.
        private Collider      _cachedCollider;
        private IInteractable _cachedInteractable;

        private void Awake()
        {
            if (cam == null)         cam = Camera.main;
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();

            if (cam == null)
                Debug.LogError("[InteractionSystem] Camera not found", this);

            if (playerInput == null)
            {
                Debug.LogError("[InteractionSystem] PlayerInput not found -- component disabled.", this);
                enabled = false;
                return;
            }

            _interactAction = playerInput.actions["Interact"];
        }

        private void OnEnable()
        {
            if (_interactAction != null) _interactAction.started += OnInteractStarted;
        }

        private void OnDisable()
        {
            if (_interactAction != null) _interactAction.started -= OnInteractStarted;
            ClearCurrent();
        }

        private void Update()
        {
            IInteractable hit = Probe();
            if (ReferenceEquals(hit, _current)) return;

            _current = hit;
            CurrentChanged?.Invoke(_current);
        }

        private IInteractable Probe()
        {
            if (cam == null) return null;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit info, maxDistance, layerMask, triggerInteraction))
            {
                _cachedCollider     = null;
                _cachedInteractable = null;
                return null;
            }

            // Same collider as last frame -- use cache, skip GetComponentInParent
            if (info.collider != _cachedCollider)
            {
                _cachedCollider     = info.collider;
                _cachedInteractable = info.collider.GetComponentInParent<IInteractable>();
            }

            return _cachedInteractable != null && _cachedInteractable.CanInteract
                ? _cachedInteractable
                : null;
        }

        private void ClearCurrent()
        {
            _cachedCollider = null;
            _cachedInteractable = null;

            if (_current == null) return;

            _current = null;
            CurrentChanged?.Invoke(null);
        }

        private void OnInteractStarted(InputAction.CallbackContext _)
        {
            if (_current != null && _current.CanInteract)
                _current.Interact(transform.root.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (cam == null) return;
            Gizmos.color = _current != null ? Color.green : Color.gray;
            Gizmos.DrawLine(cam.transform.position, cam.transform.position + cam.transform.forward * maxDistance);
        }
    }
}
