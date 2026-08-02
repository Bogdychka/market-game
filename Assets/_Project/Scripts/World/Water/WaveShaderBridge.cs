using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Uploads a resolved wave bank to the shaders. The layer arrays are global rather than
    /// per-material because every water surface in a scene - the top surface, the underwater
    /// surface and the whitecap compute - must evaluate the identical bank, and because arrays in
    /// the UnityPerMaterial CBUFFER would break SRP Batcher compatibility.
    /// Uploading nothing (<see cref="Clear"/>) leaves the shaders on their legacy four-wave
    /// material properties.
    /// </summary>
    public static class WaveShaderBridge
    {
        private static readonly int WaveLayerAId = Shader.PropertyToID("_WaveLayerA");
        private static readonly int WaveLayerBId = Shader.PropertyToID("_WaveLayerB");
        private static readonly int WaveLayerCountId = Shader.PropertyToID("_WaveLayerCount");
        private static readonly int WaveFoldLimitId = Shader.PropertyToID("_WaveFoldLimit");

        private static readonly Vector4[] LayerRowsA = new Vector4[WaveProfile.MaxLayers];
        private static readonly Vector4[] LayerRowsB = new Vector4[WaveProfile.MaxLayers];
        private static readonly List<ResolvedWaveLayer> ResolvedLayers = new(WaveProfile.MaxLayers);
        private static readonly List<ResolvedWaveLayer> ScratchLayers = new(WaveProfile.MaxLayers);

        private static int _uploadedLayerCount;
        private static float _uploadedFoldLimit = WaveSampler.DefaultFoldLimit;

        /// <summary>Layers currently uploaded to the shaders. Read-only view for samplers.</summary>
        public static IReadOnlyList<ResolvedWaveLayer> UploadedLayers => ResolvedLayers;

        /// <summary>Number of layers currently driving the water shaders.</summary>
        public static int UploadedLayerCount => _uploadedLayerCount;

        /// <summary>Fold limit currently driving the water shaders.</summary>
        public static float UploadedFoldLimit => _uploadedFoldLimit;

        /// <summary>
        /// Resolves <paramref name="profile"/> and uploads it as the global wave bank.
        /// A null or empty profile clears the bank instead.
        /// </summary>
        public static void Upload(WaveProfile profile)
        {
            if (profile == null)
            {
                Clear();
                return;
            }

            profile.ResolveLayers(ScratchLayers);
            Upload(ScratchLayers, profile.SteepnessClamping);
        }

        /// <summary>
        /// Uploads an already-resolved bank. Callers that scale a profile from outside the asset -
        /// the weather controller, a quality tier - resolve once and upload the scaled result, so
        /// the asset itself is never written to at runtime.
        /// </summary>
        public static void Upload(IReadOnlyList<ResolvedWaveLayer> layers, float foldLimit)
        {
            if (layers == null || layers.Count == 0)
            {
                Clear();
                return;
            }

            if (!ReferenceEquals(layers, ResolvedLayers))
            {
                ResolvedLayers.Clear();
                for (int i = 0; i < layers.Count && i < WaveProfile.MaxLayers; i++)
                    ResolvedLayers.Add(layers[i]);
            }

            for (int i = 0; i < WaveProfile.MaxLayers; i++)
            {
                if (i < ResolvedLayers.Count)
                {
                    ResolvedLayers[i].Pack(out Vector4 rowA, out Vector4 rowB);
                    LayerRowsA[i] = rowA;
                    LayerRowsB[i] = rowB;
                }
                else
                {
                    LayerRowsA[i] = Vector4.zero;
                    LayerRowsB[i] = Vector4.zero;
                }
            }

            _uploadedLayerCount = ResolvedLayers.Count;
            _uploadedFoldLimit = foldLimit > 0.0001f
                ? foldLimit
                : WaveSampler.DefaultFoldLimit;

            Shader.SetGlobalVectorArray(WaveLayerAId, LayerRowsA);
            Shader.SetGlobalVectorArray(WaveLayerBId, LayerRowsB);
            Shader.SetGlobalFloat(WaveLayerCountId, _uploadedLayerCount);
            Shader.SetGlobalFloat(WaveFoldLimitId, _uploadedFoldLimit);
        }

        /// <summary>
        /// Drops the uploaded bank, returning the shaders to their legacy four-wave properties.
        /// </summary>
        public static void Clear()
        {
            ResolvedLayers.Clear();
            _uploadedLayerCount = 0;
            _uploadedFoldLimit = WaveSampler.DefaultFoldLimit;
            Shader.SetGlobalFloat(WaveLayerCountId, 0f);
            Shader.SetGlobalFloat(WaveFoldLimitId, _uploadedFoldLimit);
        }

        /// <summary>
        /// Copies the uploaded bank onto a compute shader. Compute kernels are dispatched with
        /// their own constant state, so the whitecap pass has to be fed explicitly or it would
        /// keep injecting foam where the old four-wave crests used to be.
        /// </summary>
        public static void ApplyTo(ComputeShader compute)
        {
            if (compute == null)
                return;

            compute.SetVectorArray(WaveLayerAId, LayerRowsA);
            compute.SetVectorArray(WaveLayerBId, LayerRowsB);
            compute.SetFloat(WaveLayerCountId, _uploadedLayerCount);
            compute.SetFloat(WaveFoldLimitId, _uploadedFoldLimit);
        }

        /// <summary>
        /// Clears the cached upload. Play Mode runs without a domain reload in this project, so
        /// static state has to reset itself or it leaks into the next Play session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResolvedLayers.Clear();
            ScratchLayers.Clear();
            _uploadedLayerCount = 0;
            _uploadedFoldLimit = WaveSampler.DefaultFoldLimit;
        }
    }
}
