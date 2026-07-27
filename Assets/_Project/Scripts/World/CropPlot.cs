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

        [Header("Visual Stages")]
        [Tooltip("Visible while the crop has just been planted.")]
        [SerializeField] private Transform sproutVisual;
        [Tooltip("Visible when the crop is ready to harvest.")]
        [SerializeField] private Transform readyVisual;

        [Header("Soil Visuals")]
        [Tooltip("Furrowed soil visual, enabled after tilling.")]
        [SerializeField] private GameObject tilledVisual;
        [Tooltip("Renderers tinted darker after watering. Leave empty when no soil tint is needed.")]
        [SerializeField] private Renderer[] tilledRenderers;

        [Header("Debug")]
        [Tooltip("When enabled, interacting with a growing plot instantly marks it ready.")]
        [SerializeField] private bool debugInstantGrowOnInteract;

        private TimeSystem _timeSystem;
        private SeasonManager _seasonManager;
        // True while a crop occupies the plot. The fine-grained phase
        // (Planted/Growing/Ready) is always derived from elapsed game time
        // in CurrentState(), never stored, so it can never go stale.
        private bool _planted;
        private float _plantedAtMinutes;
        private CropSoilState _soilState;
        // Last growth progress pushed to the visual, so Update only rescales on a meaningful change.
        private float _lastVisualProgress = -1f;
        private int _lastVisualStage = -1;
        private MaterialPropertyBlock _soilPropertyBlock;

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
        /// <summary>Current tilling and watering state of this plot's soil.</summary>
        public CropSoilState SoilState => _soilState;

        private void Awake()
        {
            ResolveServices();
            ValidateReferences();
            RefreshSoilVisual();
            RefreshVisual();
        }

        private void Update()
        {
            // Drive the stub growth visual while a crop is in the ground, but only when the growth
            // progress actually changed enough to matter -- not every frame (audit L2).
            if ((growthVisual == null && !HasStageVisuals()) || !_planted)
                return;

            float progress = CalculateGrowthProgress();
            if (Mathf.Abs(progress - _lastVisualProgress) < 0.01f)
                return;

            RefreshVisual();
        }

        public void Interact(GameObject actor)
        {
            if (!_planted)
            {
                switch (_soilState)
                {
                    case CropSoilState.Untilled:
                        TryTill();
                        return;
                    case CropSoilState.Tilled:
                        TryWater();
                        return;
                }
            }

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

        /// <summary>Tills empty, untouched soil so it can be watered.</summary>
        public bool TryTill()
        {
            if (_planted || _soilState != CropSoilState.Untilled)
                return false;

            _soilState = CropSoilState.Tilled;
            RefreshSoilVisual();
            Debug.Log("[CropPlot] Soil tilled.", this);
            return true;
        }

        /// <summary>Waters tilled, empty soil so it can receive one seed.</summary>
        public bool TryWater()
        {
            if (_planted || _soilState != CropSoilState.Tilled)
                return false;

            _soilState = CropSoilState.Watered;
            RefreshSoilVisual();
            Debug.Log("[CropPlot] Soil watered.", this);
            return true;
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
            _soilState = CropSoilState.Tilled;
            RefreshSoilVisual();
            RefreshVisual();
            Debug.Log($"[CropPlot] Harvested: {crop.DisplayName} x{crop.YieldAmount}.", this);
            return true;
        }

        /// <summary>
        /// Restores saved plant state (from SaveData). The crop type is the plot's serialized
        /// CropSO; only the planted flag and absolute plant timestamp are restored.
        /// </summary>
        public void RestoreState(bool planted, float plantedAtMinutes, CropSoilState soilState)
        {
            _planted = planted && crop != null;
            _plantedAtMinutes = plantedAtMinutes;
            _soilState = _planted && soilState == CropSoilState.Untilled
                ? CropSoilState.Watered
                : soilState;
            RefreshSoilVisual();
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

            return _soilState == CropSoilState.Watered
                   && (_seasonManager == null || crop.CanPlantIn(_seasonManager.CurrentSeason));
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
                    return BuildEmptyPrompt();
                case CropState.Ready:
                    return $"Harvest {crop.DisplayName}";
                default:
                    return debugInstantGrowOnInteract
                        ? $"Instant grow {crop.DisplayName}"
                        : $"{crop.DisplayName} growing";
            }
        }

        private string BuildEmptyPrompt()
        {
            switch (_soilState)
            {
                case CropSoilState.Untilled:
                    return "Till soil";
                case CropSoilState.Tilled:
                    return "Water soil";
                default:
                    if (crop.SeedItem == null)
                        return "Missing seed";

                    return CanPlant()
                        ? $"Plant {crop.DisplayName}"
                        : $"Need {crop.SeedItem.DisplayName}";
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
            if (HasStageVisuals())
            {
                RefreshStageVisuals();
                return;
            }

            if (growthVisual == null)
                return;

            if (!_planted)
            {
                if (growthVisual.gameObject.activeSelf)
                    growthVisual.gameObject.SetActive(false);
                _lastVisualProgress = -1f;
                return;
            }

            if (!growthVisual.gameObject.activeSelf)
                growthVisual.gameObject.SetActive(true);

            float progress = CalculateGrowthProgress();
            float height = Mathf.Lerp(0.18f, 1f, progress);
            growthVisual.localScale = new Vector3(1f, height, 1f);
            _lastVisualProgress = progress;
        }

        private bool HasStageVisuals()
        {
            return sproutVisual != null && readyVisual != null;
        }

        private void RefreshStageVisuals()
        {
            int stage = _planted ? GetVisualStage(CalculateGrowthProgress()) : -1;
            if (stage == _lastVisualStage)
                return;

            SetStageActive(sproutVisual, stage == 0);
            SetStageActive(readyVisual, stage == 1);
            _lastVisualStage = stage;
            _lastVisualProgress = _planted ? CalculateGrowthProgress() : -1f;
        }

        private static int GetVisualStage(float progress)
        {
            return progress >= 1f ? 1 : 0;
        }

        private static void SetStageActive(Transform stageVisual, bool active)
        {
            if (stageVisual != null && stageVisual.gameObject.activeSelf != active)
                stageVisual.gameObject.SetActive(active);
        }

        private void RefreshSoilVisual()
        {
            if (tilledVisual == null)
                return;

            bool showTilled = _soilState != CropSoilState.Untilled;
            if (tilledVisual.activeSelf != showTilled)
                tilledVisual.SetActive(showTilled);

            if (!showTilled || tilledRenderers == null)
                return;

            Color tint = _soilState == CropSoilState.Watered
                ? new Color(0.38f, 0.24f, 0.12f)
                : Color.white;
            _soilPropertyBlock ??= new MaterialPropertyBlock();

            foreach (Renderer soilRenderer in tilledRenderers)
                ApplySoilTint(soilRenderer, tint);
        }

        private void ApplySoilTint(Renderer soilRenderer, Color tint)
        {
            if (soilRenderer == null || soilRenderer.sharedMaterial == null)
                return;

            _soilPropertyBlock.Clear();
            Material material = soilRenderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
                _soilPropertyBlock.SetColor("_BaseColor", tint);
            else if (material.HasProperty("_Color"))
                _soilPropertyBlock.SetColor("_Color", tint);
            else
                return;

            soilRenderer.SetPropertyBlock(_soilPropertyBlock);
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
