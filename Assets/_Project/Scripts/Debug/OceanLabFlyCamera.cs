using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Free-fly camera for the Ocean URP lab scene. The FPS player controller is not used there:
    /// the ocean has no collision surface to walk on, and the point of the scene is to look at the
    /// water from any angle, including from below it.
    /// Hold RMB to look, WASD to move, Space / Left Ctrl for up and down, Shift to boost.
    /// </summary>
    public class OceanLabFlyCamera : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 12f;
        [SerializeField] private float _boostMultiplier = 5f;
        [SerializeField] private float _lookSensitivity = 0.12f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null) return;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * _lookSensitivity;
                _yaw += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.spaceKey.isPressed) move += Vector3.up;
            if (keyboard.leftCtrlKey.isPressed) move -= Vector3.up;

            if (move == Vector3.zero) return;

            float speed = _moveSpeed * (keyboard.leftShiftKey.isPressed ? _boostMultiplier : 1f);
            transform.position += move.normalized * (speed * Time.deltaTime);
        }
    }
}
