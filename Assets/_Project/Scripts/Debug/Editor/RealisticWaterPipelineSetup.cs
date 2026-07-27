using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Enables the camera opaque texture on the PC URP asset - required for RealisticWater.shader's
    /// refraction. Depth texture was already on project-wide; opaque texture was not. Only touches
    /// the PC pipeline asset (Mobile_RPAsset is left untouched - the realistic water track is PC-only).
    /// </summary>
    public static class RealisticWaterPipelineSetup
    {
        private const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Market/Debug/Water/Enable Opaque Texture (PC Pipeline)")]
        public static void EnableOpaqueTexture()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (asset == null)
            {
                Debug.LogError($"[RealisticWaterPipelineSetup] URP asset not found at {PipelineAssetPath}.");
                return;
            }

            if (asset.supportsCameraOpaqueTexture)
            {
                Debug.Log("[RealisticWaterPipelineSetup] Opaque texture already enabled on PC_RPAsset.");
                return;
            }

            asset.supportsCameraOpaqueTexture = true;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[RealisticWaterPipelineSetup] Enabled opaque texture on PC_RPAsset.");
        }
    }
}
