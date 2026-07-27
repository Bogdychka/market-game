using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Selects the coordinated production-candidate feature budget.
    /// </summary>
    public enum RealisticWaterQualityTier
    {
        Low = 0,
        High = 1,
    }

    /// <summary>
    /// Applies one High or Low tier across every optional realistic-water subsystem.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RealisticWaterQualityController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RealisticWaterPlanarReflection planarReflection;
        [SerializeField] private RealisticWaterTemporalFoam temporalFoam;
        [SerializeField] private RealisticWaterCausticProjection causticProjection;
        [SerializeField] private RealisticWaterUnderwaterSurface underwaterSurface;

        [Header("Quality")]
        [SerializeField] private RealisticWaterQualityTier qualityTier =
            RealisticWaterQualityTier.High;

        private RealisticWaterQualityTier _appliedTier = (RealisticWaterQualityTier)(-1);

        public RealisticWaterQualityTier QualityTier => qualityTier;
        public bool IsConfigurationValid =>
            planarReflection != null &&
            temporalFoam != null &&
            causticProjection != null &&
            underwaterSurface != null;

        /// <summary>
        /// Applies the selected feature tier immediately.
        /// </summary>
        public void SetQuality(RealisticWaterQualityTier selectedTier)
        {
            qualityTier = selectedTier;
            RefreshQuality();
        }

        /// <summary>
        /// Resolves local references and reapplies the selected tier.
        /// </summary>
        public void RefreshQuality()
        {
            ResolveReferences();
            ApplyQuality();
        }

        private void Awake()
        {
            RefreshQuality();
        }

        private void Update()
        {
            if (_appliedTier != qualityTier)
                ApplyQuality();
        }

        private void OnValidate()
        {
            RefreshQuality();
        }

        private void ResolveReferences()
        {
            planarReflection ??= GetComponent<RealisticWaterPlanarReflection>();
            temporalFoam ??= GetComponent<RealisticWaterTemporalFoam>();
            causticProjection ??= GetComponent<RealisticWaterCausticProjection>();
            underwaterSurface ??= GetComponent<RealisticWaterUnderwaterSurface>();
        }

        private void ApplyQuality()
        {
            _appliedTier = qualityTier;
            bool high = qualityTier == RealisticWaterQualityTier.High;
            planarReflection?.SetQuality(
                high
                    ? WaterPlanarReflectionQuality.HalfResolution
                    : WaterPlanarReflectionQuality.SkyOnly);
            temporalFoam?.SetQuality(
                high
                    ? WaterFoamHistoryQuality.History256
                    : WaterFoamHistoryQuality.NoHistory);
            causticProjection?.SetQuality(
                high
                    ? WaterCausticQuality.ProjectedReceivers
                    : WaterCausticQuality.SurfaceFallback);
            underwaterSurface?.SetQuality(
                high
                    ? WaterUnderwaterSurfaceQuality.UnderwaterSurface
                    : WaterUnderwaterSurfaceQuality.FrontFaceOnly);
        }
    }
}
