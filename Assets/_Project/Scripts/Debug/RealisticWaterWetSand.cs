using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Market.DebugTools
{
    /// <summary>
    /// Projects the water foam history onto opaque shore renderers as run-up and wet-sand state.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RealisticWaterWetSand : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RealisticWaterTemporalFoam foamSource;
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();

        private static readonly int HistoryTextureId =
            Shader.PropertyToID("_WetSandHistoryTexture");
        private static readonly int HistoryAvailableId =
            Shader.PropertyToID("_WetSandHistoryAvailable");
        private static readonly int HistoryWorldRectId =
            Shader.PropertyToID("_WetSandHistoryWorldRect");
        private static readonly int ShoreDepthTextureId =
            Shader.PropertyToID("_ShoreDepthTexture");
        private static readonly int ShoreDepthAvailableId =
            Shader.PropertyToID("_ShoreDepthAvailable");
        private static readonly int ShoreDepthWorldRectId =
            Shader.PropertyToID("_ShoreDepthWorldRect");
        private static readonly int ShoreDepthTexelSizeId =
            Shader.PropertyToID("_ShoreDepthTexelWorldSize");
        private static readonly int ShoreDepthMaximumId =
            Shader.PropertyToID("_ShoreDepthMaximum");
        private static readonly int WindDirectionId =
            Shader.PropertyToID("_WindDirection");
        private static readonly int WindSpreadId =
            Shader.PropertyToID("_WindSpread");
        private static readonly int Wave1ParamsId =
            Shader.PropertyToID("_Wave1Params");
        private static readonly int Wave2ParamsId =
            Shader.PropertyToID("_Wave2Params");
        private static readonly int Wave3ParamsId =
            Shader.PropertyToID("_Wave3Params");
        private static readonly int Wave4ParamsId =
            Shader.PropertyToID("_Wave4Params");
        private static readonly int Wave1SteepnessId =
            Shader.PropertyToID("_Wave1Steepness");
        private static readonly int Wave2SteepnessId =
            Shader.PropertyToID("_Wave2Steepness");
        private static readonly int Wave3SteepnessId =
            Shader.PropertyToID("_Wave3Steepness");
        private static readonly int Wave4SteepnessId =
            Shader.PropertyToID("_Wave4Steepness");

        private MaterialPropertyBlock _propertyBlock;

        public int TargetCount => targetRenderers?.Length ?? 0;

        private void OnEnable()
        {
            CacheReferences();
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            ApplyBindings();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            SetHistoryAvailable(false);
        }

        private void OnValidate()
        {
            CacheReferences();
            ApplyBindings();
        }

        private void CacheReferences()
        {
            foamSource ??= GetComponent<RealisticWaterTemporalFoam>();
            waterRenderer ??= GetComponent<Renderer>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context, Camera sourceCamera)
        {
            if (sourceCamera == null || sourceCamera.cameraType == CameraType.Preview)
                return;

            ApplyBindings();
        }

        private void ApplyBindings()
        {
            if (_propertyBlock == null || targetRenderers == null)
                return;

            RenderTexture history = foamSource != null
                ? foamSource.HistoryTexture
                : null;
            bool available = history != null;
            for (int i = 0; i < targetRenderers.Length; i++)
                ApplyBinding(targetRenderers[i], history, available);
        }

        private void ApplyBinding(
            Renderer target, Texture history, bool available)
        {
            if (target == null)
                return;

            target.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(
                HistoryTextureId,
                history != null ? history : Texture2D.blackTexture);
            _propertyBlock.SetFloat(HistoryAvailableId, available ? 1f : 0f);
            _propertyBlock.SetVector(
                HistoryWorldRectId,
                foamSource != null ? foamSource.HistoryWorldRect : Vector4.zero);
            CopyShoreProperties(_propertyBlock);
            CopyWaveProperties(_propertyBlock);
            target.SetPropertyBlock(_propertyBlock);
        }

        private void CopyShoreProperties(MaterialPropertyBlock block)
        {
            Material material = waterRenderer != null
                ? waterRenderer.sharedMaterial
                : null;
            if (material == null)
                return;

            CopyTextureIfAvailable(material, block, ShoreDepthTextureId);
            CopyFloatIfAvailable(material, block, ShoreDepthAvailableId);
            CopyVectorIfAvailable(material, block, ShoreDepthWorldRectId);
            CopyVectorIfAvailable(material, block, ShoreDepthTexelSizeId);
            CopyFloatIfAvailable(material, block, ShoreDepthMaximumId);
        }

        private void CopyWaveProperties(MaterialPropertyBlock block)
        {
            Material material = waterRenderer != null
                ? waterRenderer.sharedMaterial
                : null;
            if (material == null)
                return;

            CopyVectorIfAvailable(material, block, WindDirectionId);
            CopyFloatIfAvailable(material, block, WindSpreadId);
            CopyVectorIfAvailable(material, block, Wave1ParamsId);
            CopyVectorIfAvailable(material, block, Wave2ParamsId);
            CopyVectorIfAvailable(material, block, Wave3ParamsId);
            CopyVectorIfAvailable(material, block, Wave4ParamsId);
            CopyFloatIfAvailable(material, block, Wave1SteepnessId);
            CopyFloatIfAvailable(material, block, Wave2SteepnessId);
            CopyFloatIfAvailable(material, block, Wave3SteepnessId);
            CopyFloatIfAvailable(material, block, Wave4SteepnessId);
        }

        private static void CopyVectorIfAvailable(
            Material material, MaterialPropertyBlock block, int propertyId)
        {
            if (material.HasProperty(propertyId))
                block.SetVector(propertyId, material.GetVector(propertyId));
        }

        private static void CopyFloatIfAvailable(
            Material material, MaterialPropertyBlock block, int propertyId)
        {
            if (material.HasProperty(propertyId))
                block.SetFloat(propertyId, material.GetFloat(propertyId));
        }

        private static void CopyTextureIfAvailable(
            Material material, MaterialPropertyBlock block, int propertyId)
        {
            if (!material.HasProperty(propertyId))
                return;

            Texture texture = material.GetTexture(propertyId);
            block.SetTexture(
                propertyId, texture != null ? texture : Texture2D.blackTexture);
        }

        private void SetHistoryAvailable(bool available)
        {
            if (_propertyBlock == null || targetRenderers == null)
                return;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer target = targetRenderers[i];
                if (target == null)
                    continue;
                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(
                    HistoryAvailableId, available ? 1f : 0f);
                target.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
