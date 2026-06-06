using System;
using Market.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.Interaction
{
    /// <summary>
    /// Лучом из камеры ищет IInteractable. Триггерит CurrentChanged при смене цели.
    /// По кнопке Interact (Input Action) вызывает Interact() на текущей цели.
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

        private InputAction _interactAction;
        private IInteractable _current;

        // Кеш для Probe: чтобы не дёргать GetComponentInParent каждый кадр,
        // пока луч смотрит в тот же коллайдер
        private Collider      _cachedCollider;
        private IInteractable _cachedInteractable;

        private void Awake()
        {
            if (cam == null)         cam = Camera.main;
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();

            if (cam == null)
                Debug.LogError("[InteractionSystem] cam не найден", this);

            if (playerInput == null)
            {
                Debug.LogError("[InteractionSystem] playerInput не найден — компонент отключён.", this);
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

            // Если попали в тот же коллайдер — берём из кеша, не дёргаем GetComponentInParent
            if (info.collider != _cachedCollider)
            {
                _cachedCollider     = info.collider;
                _cachedInteractable = info.collider.GetComponentInParent<IInteractable>();
            }

            return _cachedInteractable != null && _cachedInteractable.CanInteract
                ? _cachedInteractable
                : null;
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
