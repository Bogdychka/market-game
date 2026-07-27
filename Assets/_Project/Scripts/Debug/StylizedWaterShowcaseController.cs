using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Orbits the stylized-water showcase camera and switches between package materials.
    /// </summary>
    public class StylizedWaterShowcaseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _focusTarget;
        [SerializeField] private Renderer _waterRenderer;
        [SerializeField] private Material[] _waterMaterials;

        [Header("Tuning")]
        [SerializeField] private float _orbitSpeed = 6f;
        [SerializeField] private bool _autoOrbit = true;

        /// <summary>
        /// Wires the standalone showcase without requiring manual Inspector setup.
        /// </summary>
        public void Configure(
            Transform focusTarget,
            Renderer waterRenderer,
            Material[] waterMaterials)
        {
            _focusTarget = focusTarget;
            _waterRenderer = waterRenderer;
            _waterMaterials = waterMaterials;
        }

        private void Awake()
        {
            ValidateReferences();
        }

        private void Update()
        {
            HandleMaterialInput();
            HandleOrbitInput();
            TickOrbit();
        }

        private void HandleMaterialInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                ApplyMaterial(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                ApplyMaterial(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                ApplyMaterial(2);
        }

        private void HandleOrbitInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                _autoOrbit = !_autoOrbit;
        }

        private void TickOrbit()
        {
            if (!_autoOrbit || _focusTarget == null)
                return;

            transform.RotateAround(
                _focusTarget.position,
                Vector3.up,
                _orbitSpeed * Time.deltaTime);
            transform.LookAt(_focusTarget.position);
        }

        private void ApplyMaterial(int index)
        {
            if (_waterRenderer == null ||
                _waterMaterials == null ||
                index < 0 ||
                index >= _waterMaterials.Length ||
                _waterMaterials[index] == null)
            {
                return;
            }

            _waterRenderer.sharedMaterial = _waterMaterials[index];
        }

        private void ValidateReferences()
        {
            if (_focusTarget == null)
                Debug.LogError(
                    "StylizedWaterShowcaseController: focus target is not assigned.",
                    this);
            if (_waterRenderer == null)
                Debug.LogError(
                    "StylizedWaterShowcaseController: water renderer is not assigned.",
                    this);
        }
    }
}
