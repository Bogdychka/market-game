using Market.Core;
using Market.UI;
using Market.World;
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

        [Header("World Interaction")]
        [SerializeField] private float grassTrampleRadius = 0.6f;

        [Header("Debug Fly")]
        [SerializeField] private float flySpeed = 8f;
        [SerializeField] private float flySprintMultiplier = 1.75f;
        [SerializeField] private float flyVerticalSpeed = 6f;

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
        private bool _flyMode;
        private float _flyVerticalInput;

        public bool IsMoving => _controller.isGrounded && _moveInput.sqrMagnitude > 0.01f;
        public float CurrentSpeed => new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
        public bool FlyMode => _flyMode;

        /// <summary>Enables or disables debug fly mode; clears vertical momentum on change.</summary>
        public void SetFlyMode(bool enabled)
        {
            if (_flyMode == enabled) return;

            _flyMode = enabled;
            _verticalVelocity = 0f;
            _flyVerticalInput = 0f;
        }

        /// <summary>Sets the ascend/descend input (-1..1) used while fly mode is active.</summary>
        public void SetFlyVerticalInput(float value)
        {
            _flyVerticalInput = Mathf.Clamp(value, -1f, 1f);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();

            if (cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)}: cameraPivot not assigned -- component disabled.", this);
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

            GrassTrample.Register(transform, grassTrampleRadius);
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet<SettingsService>(out SettingsService svc))
                svc.LookSettingsChanged -= ApplyLookSettings;

            GrassTrample.Unregister(transform);
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

            ApplyLookDelta(yaw, pitchDelta);
        }

        private void ApplyLookDelta(float yaw, float pitchDelta)
        {
            transform.Rotate(0f, yaw, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            HandleMovement(
                _moveInput,
                _sprintHeld,
                _jumpAction.WasPressedThisFrame(),
                Time.deltaTime);
        }

        private void HandleMovement(Vector2 moveInput, bool sprintHeld, bool jumpPressed, float deltaTime)
        {
            Vector3 forward = transform.forward * moveInput.y;
            Vector3 right = transform.right * moveInput.x;
            Vector3 planar = Vector3.ClampMagnitude(forward + right, 1f);

            if (_flyMode)
            {
                float flightSpeed = sprintHeld ? flySpeed * flySprintMultiplier : flySpeed;
                Vector3 flyMotion = planar * flightSpeed + Vector3.up * (_flyVerticalInput * flyVerticalSpeed);
                _controller.Move(flyMotion * deltaTime);
                return;
            }

            float speed = sprintHeld ? sprintSpeed : walkSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (jumpPressed)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                _verticalVelocity += gravity * deltaTime;
            }

            Vector3 motion = planar * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * deltaTime);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Executes a deterministic editor-only player action for MCP visual inspection.
        /// Movement still passes through the live CharacterController and scene collisions.
        /// </summary>
        public void RunAgentAction(
            Vector2 move,
            float yawDegrees,
            float pitchDegrees,
            bool sprint,
            bool jump,
            float duration)
        {
            const float MaxStepSeconds = 1f / 60f;

            move = Vector2.ClampMagnitude(move, 1f);
            duration = Mathf.Max(0f, duration);
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(duration / MaxStepSeconds));
            float deltaTime = duration / stepCount;
            float yawStep = yawDegrees / stepCount;
            float pitchStep = -pitchDegrees / stepCount;

            for (int i = 0; i < stepCount; i++)
            {
                ApplyLookDelta(yawStep, pitchStep);
                HandleMovement(move, sprint, jump && i == 0, deltaTime);
            }
        }
#endif
    }
}
