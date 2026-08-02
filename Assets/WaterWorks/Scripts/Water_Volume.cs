using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full screen underwater volumetric pass of the GapperGames WaterWorks package.
/// The shipped version targeted URP 12 (RenderTargetHandle, cameraColorTarget, CommandBuffer
/// blits) which no longer exists in Unity 6 / URP 17, so the pass was ported to Render Graph.
/// Behaviour is unchanged: the camera colour is blitted through the "Water_Volume" material,
/// which ray marches the water box and only darkens pixels while the camera is inside it.
/// </summary>
public class Water_Volume : ScriptableRendererFeature
{
    private const string VolumeMaterialResource = "Water_Volume";

    /// <summary>Inspector settings, kept under the original names so existing renderer assets deserialize.</summary>
    [System.Serializable]
    public class _Settings
    {
        public Material material;
        public RenderPassEvent renderPass = RenderPassEvent.AfterRenderingSkybox;
    }

    public _Settings settings = new _Settings();

    private WaterVolumePass _pass;

    public override void Create()
    {
        if (settings.material == null)
            settings.material = Resources.Load<Material>(VolumeMaterialResource);

        _pass = new WaterVolumePass(settings.material)
        {
            renderPassEvent = settings.renderPass,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null || _pass == null)
            return;

        renderer.EnqueuePass(_pass);
    }

    private sealed class WaterVolumePass : ScriptableRenderPass
    {
        private const string PassName = "Water Volume";

        private readonly Material _material;

        public WaterVolumePass(Material material)
        {
            _material = material;

            // The pass reads the colour it writes to, so it can never run straight on the backbuffer,
            // and the ray march needs scene depth to stop at solid geometry.
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType == CameraType.Reflection ||
                cameraData.cameraType == CameraType.Preview)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
                return;

            TextureDesc description = renderGraph.GetTextureDesc(source);
            description.name = "_WaterVolumeColor";
            description.clearBuffer = false;
            description.depthBufferBits = 0;

            TextureHandle destination = renderGraph.CreateTexture(description);
            RenderGraphUtils.BlitMaterialParameters parameters =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            resourceData.cameraColor = destination;
        }
    }
}
