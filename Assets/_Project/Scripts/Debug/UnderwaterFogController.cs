using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Blends global fog into a blue-green underwater volume while coordinating the optional
    /// underside surface transition.
    /// </summary>
    public sealed class UnderwaterFogController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform waterSurface;
        [SerializeField] private RealisticWaterUnderwaterSurface underwaterSurface;

        [Header("Volume")]
        [SerializeField] private Color underwaterFogColor =
            new(0.015f, 0.18f, 0.32f);
        [SerializeField, Min(0f)] private float underwaterFogDensity = 0.08f;
        [SerializeField, Min(0.01f)] private float transitionHalfHeight = 0.4f;

        private Camera _camera;
        private bool _isUnderwater;
        private float _transitionBlend = -1f;
        private bool _originalFogEnabled;
        private FogMode _originalFogMode;
        private Color _originalFogColor;
        private float _originalFogDensity;
        private float _currentSurfaceHeight;

        public bool IsUnderwater => _isUnderwater;
        public float TransitionBlend => Mathf.Max(_transitionBlend, 0f);
        public float WaterHeight =>
            waterSurface != null ? waterSurface.position.y : 0f;
        public float UnderwaterFogDensity => underwaterFogDensity;
        public float TransitionHalfHeight => transitionHalfHeight;
        public float CurrentSurfaceHeight => _currentSurfaceHeight;

        private void Awake()
        {
            _camera = Camera.main;
            _originalFogEnabled = RenderSettings.fog;
            _originalFogMode = RenderSettings.fogMode;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
        }

        private void Update()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null || waterSurface == null)
                return;

            Vector3 cameraPosition = _camera.transform.position;
            _currentSurfaceHeight = underwaterSurface != null
                ? underwaterSurface.EvaluateSurfaceHeight(
                    cameraPosition, Time.time)
                : waterSurface.position.y;
            float signedDepth = _currentSurfaceHeight - cameraPosition.y;
            float linearBlend = Mathf.InverseLerp(
                -transitionHalfHeight, transitionHalfHeight, signedDepth);
            float blend = Mathf.SmoothStep(0f, 1f, linearBlend);
            if (Mathf.Approximately(blend, _transitionBlend))
                return;

            _transitionBlend = blend;
            _isUnderwater = signedDepth > 0f;
            ApplyFogBlend(blend);
            if (underwaterSurface != null)
                underwaterSurface.SetTransitionState(_isUnderwater, blend);
        }

        private void OnValidate()
        {
            underwaterFogDensity = Mathf.Max(0f, underwaterFogDensity);
            transitionHalfHeight = Mathf.Max(0.01f, transitionHalfHeight);
        }

        private void OnDestroy()
        {
            RestoreFog();
            if (underwaterSurface != null)
                underwaterSurface.SetTransitionState(false, 0f);
        }

        private void ApplyFogBlend(float blend)
        {
            if (blend <= 0.0001f)
            {
                RestoreFog();
                return;
            }

            float surfaceDensity =
                _originalFogEnabled ? _originalFogDensity : 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(
                _originalFogColor, underwaterFogColor, blend);
            RenderSettings.fogDensity = Mathf.Lerp(
                surfaceDensity, underwaterFogDensity, blend);
        }

        private void RestoreFog()
        {
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogMode = _originalFogMode;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
        }
    }
}
