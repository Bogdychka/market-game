// Bounded geometry overlay for R7 world-space caustic projection in WaterShaderLab.
Shader "Market/World/RealisticWaterProjectedCaustics"
{
    Properties
    {
        _CausticColor("Caustic Color", Color) = (0.55, 0.85, 0.78, 1)
        _CausticIntensity("Caustic Intensity", Range(0, 3)) = 1.1
        _CausticTilingA("Primary Scale", Range(0.05, 2)) = 0.72
        _CausticTilingB("Secondary Scale", Range(0.05, 2)) = 1.03
        _CausticSpeedA("Primary Speed", Range(0, 1)) = 0.12
        _CausticSpeedB("Secondary Speed", Range(0, 1)) = 0.08
        _CausticDepthStart("Shallow Fade", Range(0.01, 2)) = 0.15
        _CausticDepthEnd("Deep Fade", Range(1, 30)) = 12
        _CausticTurbidity("Turbidity", Range(0, 1)) = 0.1
        [HideInInspector] _CausticWaterBounds("Water Bounds", Vector) = (-50, -50, 50, 50)
        [HideInInspector] _CausticWaterHeight("Water Height", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+20"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ProjectedCaustics"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend One One
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CausticColor;
                float _CausticIntensity;
                float _CausticTilingA;
                float _CausticTilingB;
                float _CausticSpeedA;
                float _CausticSpeedB;
                float _CausticDepthStart;
                float _CausticDepthEnd;
                float _CausticTurbidity;
                float4 _CausticWaterBounds;
                float _CausticWaterHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half CausticLayer(float2 position)
            {
                half ridgeA = abs(sin(position.x + sin(position.y * 1.37)));
                half ridgeB = abs(sin(position.y + sin(position.x * 1.19)));
                return pow(saturate(1.0 - (ridgeA + ridgeB) * 0.48), 7.0);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float depth = _CausticWaterHeight - IN.positionWS.y;
                clip(depth - 0.01);
                clip(IN.positionWS.x - _CausticWaterBounds.x);
                clip(IN.positionWS.z - _CausticWaterBounds.y);
                clip(_CausticWaterBounds.z - IN.positionWS.x);
                clip(_CausticWaterBounds.w - IN.positionWS.z);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float sunHeight = max(mainLight.direction.y, 0.05);
                float travel = depth / sunHeight;
                float2 projectedXZ =
                    IN.positionWS.xz + mainLight.direction.xz * travel;
                float2 crossSun = normalize(
                    float2(mainLight.direction.z, -mainLight.direction.x) +
                    float2(0.001, 0.001));
                float time = _Time.y;
                float2 primaryPosition =
                    projectedXZ * _CausticTilingA +
                    crossSun * (time * _CausticSpeedA);
                float2 secondaryPosition =
                    projectedXZ.yx * _CausticTilingB -
                    crossSun.yx * (time * _CausticSpeedB);
                half pattern = saturate(
                    CausticLayer(primaryPosition) +
                    CausticLayer(secondaryPosition) * 0.7);

                half shallowFade = smoothstep(
                    0.01, max(_CausticDepthStart, 0.02), depth);
                half deepFade = 1.0 - smoothstep(
                    _CausticDepthStart,
                    max(_CausticDepthEnd, _CausticDepthStart + 0.01),
                    depth);
                half turbidity = exp(-max(_CausticTurbidity, 0.0) * depth);
                half sunAngle = smoothstep(0.05, 0.35, mainLight.direction.y);
                half receiverLight = saturate(
                    dot(normalize(IN.normalWS), mainLight.direction));
                half attenuation = shallowFade * deepFade * turbidity *
                    sunAngle * mainLight.shadowAttenuation * receiverLight;
                half3 color = _CausticColor.rgb * mainLight.color *
                    pattern * attenuation * _CausticIntensity;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
