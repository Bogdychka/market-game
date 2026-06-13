using Market.Core;
using Market.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.1f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gamepadSensitivity = 2f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private bool invertY = false;

        private CharacterController _controller;
        private PlayerInput _input;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _sprintHeld;
        private float _verticalVelocity;
        private float _pitch;

        public bool IsMoving => _controller.isGrounded && _moveInput.sqrMagnitude > 0.01f;
        public float CurrentSpeed => new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();

            if (cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)}: cameraPivot not assigned — component disabled.", this);
                enabled = false;
                return;
            }

            _moveAction   = _input.actions["Move"];
            _lookAction   = _input.actions["Look"];
            _sprintAction = _input.actions["Sprint"];
            _jumpAction   = _input.actions["Jump"];

            LoadSettingsAndRebinds();
        }

        private void LoadSettingsAndRebinds()
        {
            if (!ServiceLocator.TryGet<SettingsService>(out SettingsService svc)) return;

            mouseSensitivity = svc.MouseSensitivity;
            invertY          = svc.InvertY;

            string json = svc.GetRebindsJson();
            if (!string.IsNullOrEmpty(json))
                _input.actions.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Applies look settings from <see cref="SettingsService"/> at runtime.</summary>
        public void ApplyLookSettings(float sensitivity, bool invert)
        {
            mouseSensitivity = sensitivity;
            invertY          = invert;
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet<SettingsService>(out SettingsService svc))
                svc.LookSettingsChanged += ApplyLookSettings;
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet<SettingsService>(out SettingsService svc))
                svc.LookSettingsChanged -= ApplyLookSettings;
        }

        private void Update()
        {
            ReadInput();
            HandleLook();
            HandleMovement();
        }

        private void ReadInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();
            _sprintHeld = _sprintAction.IsPressed();
        }

        private void HandleLook()
        {
            bool isGamepad = _input.currentControlScheme == "Gamepad";
            float sens = isGamepad ? gamepadSensitivity : mouseSensitivity;
            float dt = isGamepad ? Time.deltaTime : 1f;

            float yaw = _lookInput.x * sens * dt;
            float pitchDelta = _lookInput.y * sens * dt * (invertY ? 1f : -1f);

            transform.Rotate(0f, yaw, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector3 forward = transform.forward * _moveInput.y;
            Vector3 right = transform.right * _moveInput.x;
            Vector3 planar = Vector3.ClampMagnitude(forward + right, 1f);
            float speed = _sprintHeld ? sprintSpeed : walkSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (_jumpAction.WasPressedThisFrame())
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = planar * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

    }
}
