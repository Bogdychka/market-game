using Market.Core;
using Market.Economy;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Interactive farm plot that consumes one seed, grows over game time, and yields items.
    /// </summary>
    public class CropPlot : MonoBehaviour, IInteractable
    {
        private const float MinutesPerHour = 60f;
        private const float MinutesPerDay = 24f * MinutesPerHour;

        [Header("Identity")]
        [Tooltip("Stable id used as the save key for this plot. Must be unique per plot in a scene.")]
        [SerializeField] private string plotId = "CropPlot_0";

        [Header("Crop")]
        [SerializeField] private CropSO crop;

        [Header("References")]
        [SerializeField] private Inventory inventory;
        [Tooltip("Optional stub visual scaled by growth progress.")]
        [SerializeField] private Transform growthVisual;

        [Header("Debug")]
        [Tooltip("When enabled, interacting with a growing plot instantly marks it ready.")]
        [SerializeField] private bool debugInstantGrowOnInteract = true;

        private TimeSystem _timeSystem;
        private SeasonManager _seasonManager;
        // True while a crop occupies the plot. The fine-grained phase
        // (Planted/Growing/Ready) is always derived from elapsed game time
        // in CurrentState(), never stored, so it can never go stale.
        private bool _planted;
        private float _plantedAtMinutes;

        public string PromptText => BuildPromptText();
        public bool CanInteract => crop != null && inventory != null;
        public CropState State => CurrentState();
        public float GrowthProgress => CalculateGrowthProgress();

        /// <summary>Stable save key for this plot.</summary>
        public string PlotId => plotId;
        /// <summary>True while a crop occupies the plot.</summary>
        public bool IsPlanted => _planted;
        /// <summary>Absolute game-minute timestamp the current crop was planted at.</summary>
        public float PlantedAtMinutes => _plantedAtMinutes;

        private void Awake()
        {
            ResolveServices();
            ValidateReferences();
            RefreshVisual();
        }

        private void Update()
        {
            // Drive the stub growth visual smoothly while a crop is in the ground.
            if (growthVisual != null && _planted)
                RefreshVisual();
        }

        public void Interact(GameObject actor)
        {
            switch (CurrentState())
            {
                case CropState.Empty:
                    TryPlant();
                    break;
                case CropState.Planted:
                case CropState.Growing:
                    TryDebugGrow();
                    break;
                case CropState.Ready:
                    TryHarvest();
                    break;
            }
        }

        /// <summary>Consumes one seed and starts growth if the plot is empty.</summary>
        public bool TryPlant()
        {
            if (CurrentState() != CropState.Empty || !CanPlant())
                return false;

            if (!inventory.TryRemove(crop.SeedItem))
            {
                Debug.Log($"[CropPlot] Need seed: {crop.SeedItem.DisplayName}.", this);
                return false;
            }

            _plantedAtMinutes = CurrentGameMinutes();
            _planted = true;
            RefreshVisual();
            Debug.Log($"[CropPlot] Planted: {crop.DisplayName}.", this);
            return true;
        }

        /// <summary>Adds the crop yield to inventory if growth is complete.</summary>
        public bool TryHarvest()
        {
            if (CurrentState() != CropState.Ready)
                return false;

            inventory.Add(crop.HarvestItem, crop.YieldAmount);
            _planted = false;
            RefreshVisual();
            Debug.Log($"[CropPlot] Harvested: {crop.DisplayName} x{crop.YieldAmount}.", this);
            return true;
        }

        /// <summary>
        /// Restores saved plant state (from SaveData). The crop type is the plot's serialized
        /// CropSO; only the planted flag and absolute plant timestamp are restored.
        /// </summary>
        public void RestoreState(bool planted, float plantedAtMinutes)
        {
            _planted = planted && crop != null;
            _plantedAtMinutes = plantedAtMinutes;
            RefreshVisual();
        }

        /// <summary>Debug helper for E1: instantly completes the active crop.</summary>
        public bool DebugGrowNow()
        {
            if (!_planted || crop == null)
                return false;

            // Backdate planting so the time-derived progress reads as complete.
            _plantedAtMinutes = CurrentGameMinutes() - crop.GrowthHours * MinutesPerHour;
            RefreshVisual();
            Debug.Log($"[CropPlot] Debug grow complete: {crop.DisplayName}.", this);
            return true;
        }

        private CropState CurrentState()
        {
            if (!_planted)
                return CropState.Empty;

            float progress = CalculateGrowthProgress();
            if (progress >= 1f)
                return CropState.Ready;

            return progress <= 0f ? CropState.Planted : CropState.Growing;
        }

        private float CalculateGrowthProgress()
        {
            if (!_planted || crop == null)
                return 0f;

            float elapsed = Mathf.Max(0f, CurrentGameMinutes() - _plantedAtMinutes);
            return Mathf.Clamp01(elapsed / (crop.GrowthHours * MinutesPerHour));
        }

        private bool CanPlant()
        {
            if (crop == null || crop.SeedItem == null || crop.HarvestItem == null || inventory == null)
                return false;

            if (!inventory.Has(crop.SeedItem))
                return false;

            return _seasonManager == null || crop.CanPlantIn(_seasonManager.CurrentSeason);
        }

        private void TryDebugGrow()
        {
            if (debugInstantGrowOnInteract)
                DebugGrowNow();
        }

        private string BuildPromptText()
        {
            if (crop == null || inventory == null)
                return "Crop plot";

            switch (CurrentState())
            {
                case CropState.Empty:
                    if (crop.SeedItem == null)
                        return "Missing seed";

                    return CanPlant()
                        ? $"Plant {crop.DisplayName}"
                        : $"Need {crop.SeedItem.DisplayName}";
                case CropState.Ready:
                    return $"Harvest {crop.DisplayName}";
                default:
                    return debugInstantGrowOnInteract
                        ? $"Instant grow {crop.DisplayName}"
                        : $"{crop.DisplayName} growing";
            }
        }

        private float CurrentGameMinutes()
        {
            ResolveServices();
            if (_timeSystem == null)
                return 0f;

            return _timeSystem.Day * MinutesPerDay
                   + _timeSystem.Hour * MinutesPerHour
                   + _timeSystem.Minute;
        }

        private void RefreshVisual()
        {
            if (growthVisual == null)
                return;

            if (!_planted)
            {
                if (growthVisual.gameObject.activeSelf)
                    growthVisual.gameObject.SetActive(false);
                return;
            }

            if (!growthVisual.gameObject.activeSelf)
                growthVisual.gameObject.SetActive(true);

            float height = Mathf.Lerp(0.18f, 1f, CalculateGrowthProgress());
            growthVisual.localScale = new Vector3(1f, height, 1f);
        }

        private void ResolveServices()
        {
            if (_timeSystem == null)
                ServiceLocator.TryGet<TimeSystem>(out _timeSystem);
            if (_seasonManager == null)
                ServiceLocator.TryGet<SeasonManager>(out _seasonManager);
        }

        private void ValidateReferences()
        {
            if (crop == null) Debug.LogError("[CropPlot] crop not assigned.", this);
            if (inventory == null) Debug.LogError("[CropPlot] inventory not assigned.", this);
        }
    }
}
