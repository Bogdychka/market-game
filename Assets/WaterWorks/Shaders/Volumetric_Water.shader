// Underwater volumetric pass of the GapperGames WaterWorks package.
// The shipped version imitated a Shader Graph unlit pass and included the URP internal
// ShaderGraph headers (Varyings.hlsl / UnlitPass.hlsl), whose BuildVaryings signature changed in
// URP 17, so it stopped compiling in Unity 6. The material is only ever used as a full screen blit
// by the Water_Volume renderer feature, so the pass is now a plain URP blit shader and the ray
// marching in Water_Volume.hlsl is untouched.
Shader "GapperGames/Volumetric_Water"
{
    Properties
    {
        [HDR] Albedo("Albedo", Color) = (1, 1, 1, 1)
        density("Density", Range(0, 1)) = 0.5
        pos("Position", Vector) = (0, 0, 0, 0)
        bounds("Bounds", Vector) = (5, 5, 5, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            Name "Water Volume"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            struct SurfaceDescriptionInputs
            {
                float4 ScreenPosition;
            };

            struct SurfaceDescription
            {
                float3 BaseColor;
            };

            #include "Water_Volume.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceDescriptionInputs surfaceInput;
                surfaceInput.ScreenPosition = float4(input.texcoord.xy, 0.0, 1.0);

                SurfaceDescription surface = SurfaceDescriptionFunction(surfaceInput);
                return half4(surface.BaseColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
