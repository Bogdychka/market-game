using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Pins the shader clock for one capture camera.
    ///
    /// A plain <c>Shader.SetGlobalVector("_Time", ...)</c> before <c>Camera.Render()</c> does
    /// nothing: URP rewrites <c>_Time</c>, <c>_SinTime</c>, <c>_CosTime</c> and
    /// <c>_TimeParameters</c> inside the render graph (<c>ScriptableRenderer</c>), in Edit mode
    /// from <c>Time.realtimeSinceStartup</c>. So the override has to be recorded as a pass that
    /// runs after URP's own - before shadows, so wind-animated shadow casters match the frame.
    /// Without this every "before/after" capture of an animated shader compares two random
    /// wave phases and the diff is meaningless.
    /// </summary>
    public sealed class ShaderVisionTimePass : ScriptableRenderPass
    {
        private class PassData
        {
            public Vector4 Time;
            public Vector4 SinTime;
            public Vector4 CosTime;
            public Vector4 TimeParameters;
        }

        private static readonly int TimeId = Shader.PropertyToID("_Time");
        private static readonly int SinTimeId = Shader.PropertyToID("_SinTime");
        private static readonly int CosTimeId = Shader.PropertyToID("_CosTime");
        private static readonly int TimeParametersId = Shader.PropertyToID("_TimeParameters");

        public float ShaderTime;

        /// <summary>
        /// URP resets the time variables twice per camera - once at the start of the frame and
        /// again in <c>SetupRenderGraphCameraProperties</c>, after shadows. One injection point is
        /// therefore not enough; the caller enqueues this pass at several events so the last write
        /// before each draw is ours.
        /// </summary>
        public ShaderVisionTimePass(RenderPassEvent injectionPoint)
        {
            renderPassEvent = injectionPoint;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (IUnsafeRenderGraphBuilder builder =
                   renderGraph.AddUnsafePass("ShaderVision Frozen Time", out PassData data))
            {
                // Same layout Unity documents for the built-in time variables, so shaders that
                // read any of them see one consistent instant.
                float time = ShaderTime;
                data.Time = time * new Vector4(1f / 20f, 1f, 2f, 3f);
                data.SinTime = new Vector4(
                    Mathf.Sin(time / 8f), Mathf.Sin(time / 4f), Mathf.Sin(time / 2f), Mathf.Sin(time));
                data.CosTime = new Vector4(
                    Mathf.Cos(time / 8f), Mathf.Cos(time / 4f), Mathf.Cos(time / 2f), Mathf.Cos(time));
                data.TimeParameters = new Vector4(time, Mathf.Sin(time), Mathf.Cos(time), 0f);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(TimeId, data.Time);
                    context.cmd.SetGlobalVector(SinTimeId, data.SinTime);
                    context.cmd.SetGlobalVector(CosTimeId, data.CosTime);
                    context.cmd.SetGlobalVector(TimeParametersId, data.TimeParameters);
                });
            }
        }
    }
}
