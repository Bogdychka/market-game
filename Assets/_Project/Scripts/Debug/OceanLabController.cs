using OceanSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Market.DebugTools
{
    /// <summary>
    /// Runtime controls for the Ocean URP lab scene: wind force drives the whole Beaufort ramp of
    /// wave spectra, the two direction values steer local wind chop and the long swell separately.
    /// The simulation only picks these up while the settings asset has Update Spectrum enabled.
    /// </summary>
    [RequireComponent(typeof(OceanSimulation))]
    public class OceanLabController : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float _windForce01 = 0.45f;
        [SerializeField, Range(0, 360)] private float _localWindDirection;
        [SerializeField, Range(0, 360)] private float _swellDirection;
        [SerializeField] private float _windForceStep = 0.35f;
        [SerializeField] private float _directionStep = 45f;
        [SerializeField] private bool _showLegend = true;

        private OceanSimulation _simulation;
        private GUIStyle _style;

        private void Awake()
        {
            _simulation = GetComponent<OceanSimulation>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame)
                    _showLegend = !_showLegend;

                float windDelta = 0f;
                if (keyboard.leftBracketKey.isPressed) windDelta -= 1f;
                if (keyboard.rightBracketKey.isPressed) windDelta += 1f;
                if (windDelta != 0f)
                    _windForce01 = Mathf.Clamp01(_windForce01 + windDelta * _windForceStep * Time.deltaTime);

                float localDelta = 0f;
                if (keyboard.commaKey.isPressed) localDelta -= 1f;
                if (keyboard.periodKey.isPressed) localDelta += 1f;
                if (localDelta != 0f)
                    _localWindDirection = Mathf.Repeat(_localWindDirection + localDelta * _directionStep * Time.deltaTime, 360f);

                float swellDelta = 0f;
                if (keyboard.semicolonKey.isPressed) swellDelta -= 1f;
                if (keyboard.quoteKey.isPressed) swellDelta += 1f;
                if (swellDelta != 0f)
                    _swellDirection = Mathf.Repeat(_swellDirection + swellDelta * _directionStep * Time.deltaTime, 360f);
            }

            _simulation.SetSceneVariables(_localWindDirection, _swellDirection, _windForce01);
        }

        private void OnGUI()
        {
            if (!_showLegend) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 190f), GUI.skin.box);
            GUILayout.Label("Ocean URP lab", _style);
            GUILayout.Label($"Wind force  [ / ]   {_windForce01:F2}", _style);
            GUILayout.Label($"Wind dir    , / .   {_localWindDirection:F0} deg", _style);
            GUILayout.Label($"Swell dir   ; / '   {_swellDirection:F0} deg", _style);
            GUILayout.Label("Look RMB, move WASD, up/down Space / Left Ctrl, boost Shift", _style);
            GUILayout.Label("H hides this legend", _style);
            GUILayout.EndArea();
        }
    }
}
