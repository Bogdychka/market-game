// Stylized cartoon water for the Market game (URP 17, Forward transparent pass).
// Design target: bright, readable cartoon sea that reads as water from ANY angle -
// depth-graded color, animated wave normals, crisp toon sun glint, sky fresnel,
// alpha blending and shoreline foam. No grazing-angle gating, no near-black plate.
Shader "Market/World/StylizedWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor("Shallow Color", Color) = (0.20, 0.62, 0.72, 0.72)
        _DeepColor("Deep Color", Color) = (0.05, 0.28, 0.52, 0.93)
        _SkyColor("Sky / Horizon Color", Color) = (0.60, 0.82, 0.92, 1.0)
        _FoamColor("Foam Color", Color) = (0.95, 0.98, 1.0, 1.0)
        _SpecColor("Sun Glint Color", Color) = (1.0, 0.98, 0.9, 1.0)

        [NoScaleOffset] _FlowMap("Wave Break-up Map", 2D) = "gray" {}

        [Header(Depth)]
        _DepthFadeDistance("Depth Fade Distance", Range(0.5, 60)) = 12
        [Header(Waves)]
        _WaveTiling("Wave Tiling", Range(0.005, 0.3)) = 0.06
        _WaveSpeed("Wave Speed", Range(0, 2)) = 0.5
        _NormalStrength("Wave Normal Strength", Range(0, 2)) = 0.55
        _DetailFadeStart("Detail Fade Start", Range(1, 200)) = 40
        _DetailFadeEnd("Detail Fade End", Range(2, 600)) = 220

        [Header(Lighting)]
        _AmbientBoost("Ambient Boost", Range(0, 1.5)) = 0.55
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 4.0
        _FresnelStrength("Fresnel Strength", Range(0, 1)) = 0.45
        _SpecPower("Glint Sharpness", Range(4, 512)) = 180
        _SpecStrength("Glint Strength", Range(0, 4)) = 1.6

        [Header(Foam)]
        _FoamWidth("Shore Foam Width", Range(0.05, 20)) = 3.5
        _FoamScale("Foam Scale", Range(0.02, 2)) = 0.25
        _FoamSpeed("Foam Speed", Range(0, 2)) = 0.35
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SkyColor;
                half4 _FoamColor;
                half4 _SpecColor;
                float _DepthFadeDistance;
                float _WaveTiling;
                float _WaveSpeed;
                float _NormalStrength;
                float _DetailFadeStart;
                float _DetailFadeEnd;
                float _AmbientBoost;
                float _FresnelPower;
                float _FresnelStrength;
                float _SpecPower;
                float _SpecStrength;
                float _FoamWidth;
                float _FoamScale;
                float _FoamSpeed;
                float _FoamCutoff;
            CBUFFER_END

            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            // Slope (dNormal) contribution of one directional sine wave.
            float2 WaveSlope(float2 pos, float2 dir, float freq, float phase, float amp)
            {
                float k = dot(pos, dir) * freq + phase;
                return dir * (cos(k) * freq * amp);
            }

            // Animated cartoon wave normal from a few crossing sine waves plus a
            // scrolling break-up sample so the surface never looks like a tiled grid.
            // 'detail' flattens the ripples with distance to kill far-field moire.
            half3 WaterNormal(float2 pos, half detail)
            {
                float t = _Time.y * _WaveSpeed;
                float f = _WaveTiling;

                float2 slope = 0.0;
                slope += WaveSlope(pos, normalize(float2(1.0, 0.25)), f * 5.0,  t * 1.00, 1.0);
                slope += WaveSlope(pos, normalize(float2(-0.4, 1.0)), f * 7.5,  t * 0.80, 0.6);
                slope += WaveSlope(pos, normalize(float2(0.7, -0.7)), f * 11.0, t * 1.30, 0.35);

                float2 fuv = pos * (f * 0.6) + float2(t * 0.03, -t * 0.021);
                half2 breakup = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, fuv).rg * 2.0h - 1.0h;
                slope += breakup * 0.5;

                slope *= _NormalStrength * detail;
                return SafeNormalize(half3(-slope.x, 1.0, -slope.y));
            }

            // Vertical thickness of the water column under this pixel.
            // Falls back to a mid depth when nothing opaque is behind (open sea, sky).
            float WaterColumnDepth(float2 screenUV, float3 waterPosWS)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                float3 scenePosWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
#if UNITY_REVERSED_Z
                float hasSurface = step(0.00001, rawDepth);
#else
                float hasSurface = 1.0 - step(0.99999, rawDepth);
#endif
                float vertical = max(waterPosWS.y - scenePosWS.y, 0.0);
                return lerp(_DepthFadeDistance, vertical, hasSurface);
            }

            float FoamNoise(float2 pos)
            {
                float t = _Time.y * _FoamSpeed;
                float2 uv = pos * _FoamScale;
                float a = sin(dot(uv, float2(0.9, 0.35)) + t);
                float b = sin(dot(uv, float2(-0.35, 0.95)) - t * 0.7 + a * 0.8);
                float c = sin(dot(uv, float2(0.6, -0.8)) + t * 0.5 + b * 0.6);
                return saturate((a * 0.5 + b * 0.3 + c * 0.2) * 0.5 + 0.5);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 V = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float camDist = distance(GetCameraPositionWS(), input.positionWS);
                half detail = saturate(1.0 - (camDist - _DetailFadeStart) / max(_DetailFadeEnd - _DetailFadeStart, 0.001));
                detail = lerp(0.18h, 1.0h, detail);
                half3 N = WaterNormal(input.positionWS.xz, detail);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float columnDepth = WaterColumnDepth(screenUV, input.positionWS);
                half depthFade = saturate(columnDepth / _DepthFadeDistance);

                // Depth-graded body color.
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFade);

                half opacity = lerp(_ShallowColor.a, _DeepColor.a, depthFade);
                half3 color = waterColor;

                // Soft diffuse + ambient, kept bright for a cartoon read.
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(N, mainLight.direction)) * 0.5h + 0.5h; // wrapped
                half3 ambient = SampleSH(N) + _AmbientBoost.xxx;
                color *= ambient + mainLight.color * ndotl * 0.35h;

                // Sky fresnel toward the horizon.
                half fresnel = pow(1.0h - saturate(dot(N, V)), _FresnelPower);
                color = lerp(color, _SkyColor.rgb, fresnel * _FresnelStrength);

                // Crisp toon sun glint.
                half3 H = SafeNormalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), _SpecPower);
                spec = smoothstep(0.3h, 0.55h, spec) * _SpecStrength * detail;
                color += mainLight.color * _SpecColor.rgb * spec;

                // Stylized shoreline foam ring: solid at the waterline, broken up by
                // animated noise on its inner edge so it never reads as a flat ribbon.
                half shore = 1.0h - saturate(columnDepth / _FoamWidth);
                half foamNoise = FoamNoise(input.positionWS.xz);
                half foamMask = shore * (0.45h + foamNoise * 0.9h);
                half foam = smoothstep(_FoamCutoff, _FoamCutoff + 0.12h, foamMask);
                color = lerp(color, _FoamColor.rgb, foam * _FoamColor.a);

                color = MixFog(color, input.fogFactor);
                half alpha = saturate(opacity + foam * 0.5h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
