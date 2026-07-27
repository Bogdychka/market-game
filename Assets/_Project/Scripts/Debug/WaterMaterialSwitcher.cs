using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Cycles a water renderer through its configured materials with F6.
    /// Hold Shift while pressing F6 to cycle backward.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterMaterialSwitcher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer _waterRenderer;
        [SerializeField] private Material[] _materials;

        [Header("State")]
        [SerializeField] private int _materialIndex;

        /// <summary>
        /// Wires the target renderer and available materials from a scene builder.
        /// </summary>
        public void Configure(Renderer waterRenderer, Material[] materials)
        {
            _waterRenderer = waterRenderer;
            _materials = materials;
            _materialIndex = 0;
            ApplyCurrentMaterial();
        }

        private void Awake()
        {
            if (_waterRenderer == null)
                _waterRenderer = GetComponent<Renderer>();
            ApplyCurrentMaterial();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f6Key.wasPressedThisFrame)
                return;

            bool reverse =
                keyboard.leftShiftKey.isPressed ||
                keyboard.rightShiftKey.isPressed;
            CycleMaterial(reverse ? -1 : 1);
        }

        private void CycleMaterial(int direction)
        {
            if (_materials == null || _materials.Length == 0)
                return;

            _materialIndex =
                (_materialIndex + direction + _materials.Length) % _materials.Length;
            ApplyCurrentMaterial();
            Debug.Log(
                $"Water material: {_waterRenderer.sharedMaterial.name}",
                this);
        }

        private void ApplyCurrentMaterial()
        {
            if (_waterRenderer == null ||
                _materials == null ||
                _materials.Length == 0)
            {
                return;
            }

            _materialIndex = Mathf.Clamp(_materialIndex, 0, _materials.Length - 1);
            if (_materials[_materialIndex] != null)
                _waterRenderer.sharedMaterial = _materials[_materialIndex];
        }
    }
}
