using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Selects projected receiver overlays or the water-surface composite fallback.
    /// </summary>
    public enum WaterCausticQuality
    {
        SurfaceFallback = 0,
        ProjectedReceivers = 1,
    }

    /// <summary>
    /// Keeps the bounded caustic receiver overlays aligned with the laboratory water volume.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterCausticProjection : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject receiverRoot;
        [SerializeField] private Renderer[] receiverRenderers;

        [Header("Quality")]
        [SerializeField] private WaterCausticQuality quality =
            WaterCausticQuality.ProjectedReceivers;

        private static readonly int ProjectedAvailableId =
            Shader.PropertyToID("_ProjectedCausticsAvailable");
        private static readonly int WaterBoundsId =
            Shader.PropertyToID("_CausticWaterBounds");
        private static readonly int WaterHeightId =
            Shader.PropertyToID("_CausticWaterHeight");
        private static readonly int CausticIntensityId =
            Shader.PropertyToID("_CausticIntensity");
        private static readonly int CausticSpeedAId =
            Shader.PropertyToID("_CausticSpeedA");
        private static readonly int CausticSpeedBId =
            Shader.PropertyToID("_CausticSpeedB");

        private Renderer _waterRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Vector4 _appliedBounds;
        private float _appliedWaterHeight = float.NaN;
        private bool _appliedProjectedState;
        private float _weatherIntensity = 0.85f;
        private Vector2 _weatherSpeeds = new(0.035f, 0.024f);

        public WaterCausticQuality Quality => quality;
        public int ReceiverCount =>
            receiverRenderers != null ? receiverRenderers.Length : 0;
        public Vector4 ProjectionBounds => _appliedBounds;
        public bool ProjectedPathAvailable => IsProjectedPathAvailable();

        /// <summary>
        /// Selects the projected receiver path or the cheap water-surface fallback.
        /// </summary>
        public void SetQuality(WaterCausticQuality selectedQuality)
        {
            quality = selectedQuality;
            RefreshProjection();
        }

        /// <summary>
        /// Reapplies the water bounds and selected quality to all receiver overlays.
        /// </summary>
        public void RefreshProjection()
        {
            CacheComponents();
            ApplyProjectionState(true);
        }

        /// <summary>
        /// Applies weather-driven visibility and motion to the projected caustic receivers.
        /// </summary>
        public void SetWeatherAppearance(float intensity, Vector2 speeds)
        {
            _weatherIntensity = Mathf.Max(0f, intensity);
            _weatherSpeeds = Vector2.Max(Vector2.zero, speeds);
            ApplyProjectionState(true);
        }

        private void Awake()
        {
            CacheComponents();
            ApplyProjectionState(true);
        }

        private void Update()
        {
            ApplyProjectionState(false);
        }

        private void OnValidate()
        {
            CacheComponents();
            ApplyProjectionState(true);
        }

        private void OnDestroy()
        {
            SetWaterProjectedAvailability(false);
            if (receiverRoot != null)
                receiverRoot.SetActive(false);
        }

        private void CacheComponents()
        {
            if (_waterRenderer == null)
                _waterRenderer = GetComponent<Renderer>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyProjectionState(bool force)
        {
            if (_waterRenderer == null)
                return;

            bool projected = IsProjectedPathAvailable();
            Bounds waterBounds = _waterRenderer.bounds;
            Vector4 bounds = new(
                waterBounds.min.x,
                waterBounds.min.z,
                waterBounds.max.x,
                waterBounds.max.z);
            float waterHeight = transform.position.y;
            if (!force &&
                projected == _appliedProjectedState &&
                Approximately(bounds, _appliedBounds) &&
                Mathf.Approximately(waterHeight, _appliedWaterHeight))
            {
                return;
            }

            _appliedProjectedState = projected;
            _appliedBounds = bounds;
            _appliedWaterHeight = waterHeight;
            if (receiverRoot != null && receiverRoot.activeSelf != projected)
                receiverRoot.SetActive(projected);
            SetWaterProjectedAvailability(projected);
            ApplyReceiverProperties(bounds, waterHeight);
        }

        private bool IsProjectedPathAvailable()
        {
            return quality == WaterCausticQuality.ProjectedReceivers &&
                receiverRoot != null &&
                receiverRenderers != null &&
                receiverRenderers.Length > 0;
        }

        private void SetWaterProjectedAvailability(bool available)
        {
            if (_waterRenderer == null)
                return;

            _waterRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ProjectedAvailableId, available ? 1f : 0f);
            _waterRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ApplyReceiverProperties(Vector4 bounds, float waterHeight)
        {
            if (receiverRenderers == null)
                return;

            foreach (Renderer receiver in receiverRenderers)
            {
                if (receiver == null)
                    continue;

                receiver.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(WaterBoundsId, bounds);
                _propertyBlock.SetFloat(WaterHeightId, waterHeight);
                _propertyBlock.SetFloat(
                    CausticIntensityId, _weatherIntensity);
                _propertyBlock.SetFloat(CausticSpeedAId, _weatherSpeeds.x);
                _propertyBlock.SetFloat(CausticSpeedBId, _weatherSpeeds.y);
                receiver.SetPropertyBlock(_propertyBlock);
            }
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                Mathf.Approximately(a.y, b.y) &&
                Mathf.Approximately(a.z, b.z) &&
                Mathf.Approximately(a.w, b.w);
        }
    }
}
