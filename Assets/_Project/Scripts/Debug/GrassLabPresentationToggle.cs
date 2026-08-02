using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Keeps GrassLab clean for visual review while preserving its scale and card-reference tools.
    /// Press F6 in Play Mode to reveal or hide the diagnostic roots without rebuilding the scene.
    /// </summary>
    public class GrassLabPresentationToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject[] _diagnosticRoots;

        [Header("Settings")]
        [SerializeField] private bool _diagnosticsVisible;

        /// <summary>Whether the lab's diagnostic objects are currently visible.</summary>
        public bool DiagnosticsVisible => _diagnosticsVisible;

        /// <summary>Assigns the controlled roots and applies their initial presentation state.</summary>
        public void Configure(GameObject[] diagnosticRoots, bool diagnosticsVisible)
        {
            _diagnosticRoots = diagnosticRoots;
            _diagnosticsVisible = diagnosticsVisible;
            ApplyVisibility();
        }

        /// <summary>Flips the diagnostic overlay state and applies it immediately.</summary>
        public void ToggleDiagnostics()
        {
            _diagnosticsVisible = !_diagnosticsVisible;
            ApplyVisibility();
        }

        private void Start() => ApplyVisibility();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Key.F6].wasPressedThisFrame)
                ToggleDiagnostics();
        }

        private void ApplyVisibility()
        {
            if (_diagnosticRoots == null)
                return;

            foreach (GameObject diagnosticRoot in _diagnosticRoots)
            {
                if (diagnosticRoot != null)
                    diagnosticRoot.SetActive(_diagnosticsVisible);
            }
        }
    }
}
