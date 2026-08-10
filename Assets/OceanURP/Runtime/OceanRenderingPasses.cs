// Ported from gasgiant/Ocean-URP (MIT) to the URP 17 / Unity 6 Render Graph API.
// The three original passes (sky map, underwater effect, ocean geometry) all ran at
// BeforeRenderingTransparents, so they are recorded here by a single ScriptableRenderPass.
// That lets the submergence texture be handed to the surface shader as a graph-tracked global
// instead of relying on a temporary RT that survives between passes.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OceanSystem
{
    public class OceanRenderPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId OceanShaderTagId = new ShaderTagId("OceanMain");
        private const int SubmergenceResolution = 32;
        private const int SubmergencePass = 0;
        private const int UnderwaterPostEffectPass = 1;
        private const int SkyMapPass = 0;

        private readonly OceanRendererFeature.OceanRenderingSettings _settings;
        private readonly Material _underwaterEffectMaterial;
        private readonly Material _skyMapMaterial;
        private RenderTexture _skyMap;
        private RTHandle _skyMapHandle;
        private bool _skyMapRendered;

        private bool NeedToRenderSkyMap => _settings.updateSkyMap || !_skyMapRendered;

        public OceanRenderPass(OceanRendererFeature.OceanRenderingSettings settings)
        {
            _settings = settings;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            _underwaterEffectMaterial = CoreUtils.CreateEngineMaterial("Hidden/Ocean/UnderwaterEffect");
            _skyMapMaterial = CoreUtils.CreateEngineMaterial("Hidden/Ocean/StereographicSky");
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_underwaterEffectMaterial);
            CoreUtils.Destroy(_skyMapMaterial);
            ReleaseSkyMap();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
#if UNITY_EDITOR
            if (!OceanRendererFeature.IsRendering) return;
#endif
            if (_underwaterEffectMaterial == null || _skyMapMaterial == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!OceanRendererFeature.IsCorrectCameraType(cameraData.cameraType)) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            RecordCameraGlobals(renderGraph, cameraData.camera);

            if (NeedToRenderSkyMap)
                RecordSkyMap(renderGraph);

            if (_settings.underwaterEffect)
                RecordUnderwaterEffect(renderGraph, resourceData);

            RecordOceanGeometry(renderGraph, resourceData, renderingData, cameraData, lightData);
        }

        // The ocean surface and the underwater effect both reconstruct world positions from the
        // depth buffer, so these have to be in place before any of the passes below execute.
        private void RecordCameraGlobals(RenderGraph renderGraph, Camera camera)
        {
            using (var builder = renderGraph.AddUnsafePass<CameraGlobalsPassData>(
                "Ocean Camera Globals", out var passData))
            {
                passData.inverseView = camera.cameraToWorldMatrix;
                passData.inverseProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CameraGlobalsPassData data, UnsafeGraphContext context) =>
                {
                    context.cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseViewMatrix, data.inverseView);
                    context.cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseProjectionMatrix, data.inverseProjection);
                });
            }
        }

        // The sky map is a persistent off-screen cubemap-substitute, so it is imported into the graph
        // and drawn with the same procedural fullscreen quad as the other passes here. It must not go
        // back to the legacy CommandBuffer.Blit: inside a render graph pass that silently draws
        // nothing, which leaves the map black and the ocean with no sky reflection at all.
        private void RecordSkyMap(RenderGraph renderGraph)
        {
            CreateSkyMapTexture();

            TextureHandle skyMap = renderGraph.ImportTexture(_skyMapHandle);

            using (var builder = renderGraph.AddRasterRenderPass<FullscreenPassData>(
                "Ocean Sky Map", out var passData))
            {
                passData.material = _skyMapMaterial;
                passData.shaderPass = SkyMapPass;
                builder.SetRenderAttachment(skyMap, 0);
                builder.UseAllGlobalTextures(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((FullscreenPassData data, RasterGraphContext context) =>
                    DrawFullscreenQuad(context.cmd, data));
            }

            _skyMapRendered = true;
        }

        private void RecordUnderwaterEffect(RenderGraph renderGraph, UniversalResourceData resourceData)
        {
            TextureDesc submergenceDesc = new TextureDesc(SubmergenceResolution, SubmergenceResolution)
            {
                format = GraphicsFormat.R8_UNorm,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                name = "OceanCameraSubmergence"
            };
            TextureHandle submergence = renderGraph.CreateTexture(submergenceDesc);

            // How deep the camera is under the surface, evaluated once per frame at a tiny resolution.
            using (var builder = renderGraph.AddRasterRenderPass<FullscreenPassData>(
                "Ocean Camera Submergence", out var passData))
            {
                passData.material = _underwaterEffectMaterial;
                passData.shaderPass = SubmergencePass;
                builder.SetRenderAttachment(submergence, 0);
                builder.UseAllGlobalTextures(true);
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(submergence, GlobalShaderVariables.Misc.SubmergenceTexture);
                builder.SetRenderFunc((FullscreenPassData data, RasterGraphContext context) =>
                    DrawFullscreenQuad(context.cmd, data));
            }

            // Fog the already-rendered opaque scene while the camera is submerged. Runs before the
            // ocean surface so the surface itself is not tinted twice.
            using (var builder = renderGraph.AddRasterRenderPass<FullscreenPassData>(
                "Ocean Underwater Effect", out var passData))
            {
                passData.material = _underwaterEffectMaterial;
                passData.shaderPass = UnderwaterPostEffectPass;
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.UseTexture(submergence);
                UseCameraTextures(builder, resourceData);
                builder.UseAllGlobalTextures(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((FullscreenPassData data, RasterGraphContext context) =>
                    DrawFullscreenQuad(context.cmd, data));
            }
        }

        private void RecordOceanGeometry(RenderGraph renderGraph, UniversalResourceData resourceData,
            UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<GeometryPassData>(
                "Ocean Geometry", out var passData))
            {
                DrawingSettings drawingSettings = UnityEngine.Rendering.Universal.RenderingUtils.CreateDrawingSettings(
                    OceanShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawingSettings.perObjectData = PerObjectData.LightProbe;
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);

                passData.rendererList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                UseCameraTextures(builder, resourceData);
                builder.UseAllGlobalTextures(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((GeometryPassData data, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(data.rendererList));
            }
        }

        // Refraction and the underwater fog read the opaque colour copy and the depth copy; declaring
        // them keeps the graph from culling the copies or reordering them after the ocean.
        private static void UseCameraTextures(IRasterRenderGraphBuilder builder, UniversalResourceData resourceData)
        {
            if (resourceData.cameraOpaqueTexture.IsValid())
                builder.UseTexture(resourceData.cameraOpaqueTexture);
            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture);
        }

        private static void DrawFullscreenQuad(RasterCommandBuffer cmd, FullscreenPassData data)
        {
            cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Quads, 4, 1);
        }

        private void CreateSkyMapTexture()
        {
            if (_skyMap != null && _skyMap.height == _settings.skyMapResolution) return;

            ReleaseSkyMap();
            _skyMap = new RenderTexture(_settings.skyMapResolution, _settings.skyMapResolution, 0,
                RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear)
            {
                name = "OceanSkyMap",
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 9
            };
            // MeanSkyRadiance samples the map with SampleGrad, so it needs the mip chain above;
            // autoGenerateMips fills it once the graph is done using the texture as a target.
            _skyMap.Create();
            _skyMapHandle = RTHandles.Alloc(_skyMap);

            // The map is a persistent texture, so bind it as an engine global once here rather than
            // per-pass: a render-graph global is reset once the graph finishes executing.
            Shader.SetGlobalTexture(GlobalShaderVariables.Misc.SkyMap, _skyMap);
        }

        private void ReleaseSkyMap()
        {
            if (_skyMap == null) return;
            _skyMapHandle?.Release();
            _skyMapHandle = null;
            _skyMap.Release();
            CoreUtils.Destroy(_skyMap);
            _skyMap = null;
            _skyMapRendered = false;
        }

        private class CameraGlobalsPassData
        {
            public Matrix4x4 inverseView;
            public Matrix4x4 inverseProjection;
        }

        private class FullscreenPassData
        {
            public Material material;
            public int shaderPass;
        }

        private class GeometryPassData
        {
            public RendererListHandle rendererList;
        }
    }
}
