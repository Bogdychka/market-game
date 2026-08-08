Shader "Market/World/RealisticWetSand"
{
    Properties
    {
        [Header(Sand)]
        _DryColor("Dry Sand Color", Color) = (0.8, 0.72, 0.5, 1)
        _WetColor("Wet Sand Color", Color) = (0.34, 0.25, 0.13, 1)
        _SwashColor("Swash Highlight", Color) = (0.78, 0.82, 0.76, 1)
        _DrySmoothness("Dry Smoothness", Range(0, 1)) = 0.1
        _WetSmoothness("Wet Smoothness", Range(0, 1)) = 0.62

        [Header(Runup)]
        _WaterLevel("Water Level", Float) = 0
        _RunupHeight("Maximum Runup Height", Range(0.05, 3)) = 0.85
        _RunupDistance("Maximum Runup Distance", Range(0.1, 30)) = 6.5
        _RetreatWidth("Swash Front Width", Range(0.05, 4)) = 0.8
        _HistoryProbeOffset("Seaward History Probe", Range(0, 12)) = 2
        _EventGain("Breaker Event Gain", Range(0, 4)) = 2.2
        _BreakupScale("Wet Edge Breakup Scale", Range(0.01, 2)) = 0.18
        _BreakupStrength("Wet Edge Breakup", Range(0, 1)) = 0.35
        _FallbackEventStrength("No-History Event Strength", Range(0, 1)) = 0.35
        _ShoreDirection("Shore To Sea Direction", Vector) = (0, 0, 1, 0)
        _ShoreOriginXZ("Reference Waterline XZ", Vector) = (0, -20, 0, 0)
        [Toggle] _UseShoreDistanceField("Use Curved Shore Distance Field", Float) = 0

        [HideInInspector] [NoScaleOffset]
        _ShoreDepthTexture("Shore Depth Map", 2D) = "black" {}
        [HideInInspector] _ShoreDepthAvailable("Shore Depth Available", Float) = 0
        [HideInInspector] _ShoreDepthWorldRect("Shore Depth World Rect", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ShoreDepthTexelWorldSize("Shore Depth Texel World Size", Vector) = (1, 1, 0, 0)
        [HideInInspector] _ShoreDepthMaximum("Shore Depth Maximum", Float) = 60

        [HideInInspector] [NoScaleOffset]
        _WetSandHistoryTexture("Wet Sand History", 2D) = "black" {}
        [HideInInspector] _WetSandHistoryAvailable("Wet Sand History Available", Float) = 0
        [HideInInspector] _WetSandHistoryWorldRect("Wet Sand History World Rect", Vector) = (0, 0, 0, 0)

        [HideInInspector] _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        [HideInInspector] _WindSpread("Wind Spread", Float) = 1
        [HideInInspector] _Wave1Params("Legacy Wave 1", Vector) = (0, 1, 0, 1)
        [HideInInspector] _Wave2Params("Legacy Wave 2", Vector) = (0, 1, 0, 1)
        [HideInInspector] _Wave3Params("Legacy Wave 3", Vector) = (0, 1, 0, 1)
        [HideInInspector] _Wave4Params("Legacy Wave 4", Vector) = (0, 1, 0, 1)
        [HideInInspector] _Wave1Steepness("Legacy Steepness 1", Float) = 0
        [HideInInspector] _Wave2Steepness("Legacy Steepness 2", Float) = 0
        [HideInInspector] _Wave3Steepness("Legacy Steepness 3", Float) = 0
        [HideInInspector] _Wave4Steepness("Legacy Steepness 4", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "WetSandForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WetSandHistoryTexture);
            SAMPLER(sampler_WetSandHistoryTexture);
            TEXTURE2D(_ShoreDepthTexture);
            SAMPLER(sampler_ShoreDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _DryColor;
                half4 _WetColor;
                half4 _SwashColor;
                float _DrySmoothness;
                float _WetSmoothness;
                float _WaterLevel;
                float _RunupHeight;
                float _RunupDistance;
                float _RetreatWidth;
                float _HistoryProbeOffset;
                float _EventGain;
                float _BreakupScale;
                float _BreakupStrength;
                float _FallbackEventStrength;
                float4 _ShoreDirection;
                float4 _ShoreOriginXZ;
                float _UseShoreDistanceField;
                float _ShoreDepthAvailable;
                float4 _ShoreDepthWorldRect;
                float4 _ShoreDepthTexelWorldSize;
                float _ShoreDepthMaximum;
                float _WetSandHistoryAvailable;
                float4 _WetSandHistoryWorldRect;
                float4 _WindDirection;
                float _WindSpread;
                float4 _Wave1Params;
                float4 _Wave2Params;
                float4 _Wave3Params;
                float4 _Wave4Params;
                float _Wave1Steepness;
                float _Wave2Steepness;
                float _Wave3Steepness;
                float _Wave4Steepness;
            CBUFFER_END

            #include "Assets/_Project/Art/Shaders/RealisticWaterWaves.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(
                    input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float2 PlanarShoreDirection()
            {
                float2 direction = _ShoreDirection.xz;
                float lengthSq = dot(direction, direction);
                return lengthSq > 0.0001
                    ? direction * rsqrt(lengthSq)
                    : float2(0.0, 1.0);
            }

            half ShoreFieldContains(float2 worldXZ)
            {
                float2 uv = (worldXZ - _ShoreDepthWorldRect.xy) *
                    _ShoreDepthWorldRect.zw;
                return step(0.0, uv.x) * step(uv.x, 1.0) *
                    step(0.0, uv.y) * step(uv.y, 1.0) *
                    step(0.5, _ShoreDepthAvailable) *
                    step(0.5, _UseShoreDistanceField);
            }

            float SampleSignedShoreDistance(float2 worldXZ)
            {
                float2 uv = (worldXZ - _ShoreDepthWorldRect.xy) *
                    _ShoreDepthWorldRect.zw;
                return SAMPLE_TEXTURE2D_LOD(
                    _ShoreDepthTexture,
                    sampler_ShoreDepthTexture,
                    saturate(uv),
                    0).g;
            }

            float2 ShoreDirection(float2 worldXZ)
            {
                float2 direction = PlanarShoreDirection();
                if (ShoreFieldContains(worldXZ) > 0.5h)
                {
                    float2 texel = max(
                        _ShoreDepthTexelWorldSize.xy, float2(0.001, 0.001));
                    float gradientX =
                        SampleSignedShoreDistance(
                            worldXZ + float2(texel.x, 0.0)) -
                        SampleSignedShoreDistance(
                            worldXZ - float2(texel.x, 0.0));
                    float gradientZ =
                        SampleSignedShoreDistance(
                            worldXZ + float2(0.0, texel.y)) -
                        SampleSignedShoreDistance(
                            worldXZ - float2(0.0, texel.y));
                    float2 gradient = float2(gradientX, gradientZ);
                    float gradientLengthSq = dot(gradient, gradient);
                    if (gradientLengthSq > 0.000001)
                        direction = gradient * rsqrt(gradientLengthSq);
                }

                return direction;
            }

            half2 SampleHistory(float2 worldXZ)
            {
                float2 uv = (worldXZ - _WetSandHistoryWorldRect.xy) *
                    _WetSandHistoryWorldRect.zw;
                half inside =
                    step(0.0, uv.x) * step(uv.x, 1.0) *
                    step(0.0, uv.y) * step(uv.y, 1.0);
                return SAMPLE_TEXTURE2D(
                    _WetSandHistoryTexture,
                    sampler_WetSandHistoryTexture,
                    saturate(uv)).rg * inside *
                    saturate(_WetSandHistoryAvailable);
            }

            half AnalyticBreaker(float2 worldXZ)
            {
                float3 offset = float3(0.0, 0.0, 0.0);
                float3 tangentX = float3(1.0, 0.0, 0.0);
                float3 tangentZ = float3(0.0, 0.0, 1.0);
                RealisticWaterAccumulateWaves(
                    worldXZ, _Time.y, offset, tangentX, tangentZ);
                return saturate((offset.y - 0.04) / 0.32);
            }

            half WetEdgeNoise(float2 worldXZ)
            {
                float2 uv = worldXZ * _BreakupScale;
                half broad = sin(dot(uv, float2(0.83, 0.31))) * 0.5h + 0.5h;
                half fine = sin(dot(uv, float2(-0.27, 1.07)) + broad) *
                    0.5h + 0.5h;
                return saturate(broad * 0.65h + fine * 0.35h);
            }

            void EvaluateWetness(
                float3 positionWS,
                out half wetness,
                out half swash)
            {
                half useShoreField = ShoreFieldContains(positionWS.xz);
                float2 shoreDirection = ShoreDirection(positionWS.xz);
                float shoreCoordinate = dot(
                    positionWS.xz - _ShoreOriginXZ.xy, shoreDirection);
                float signedShoreDistance = SampleSignedShoreDistance(
                    positionWS.xz);
                float dryDistance = useShoreField > 0.5h
                    ? max(-signedShoreDistance, 0.0)
                    : max(-shoreCoordinate, 0.0);
                float2 probe = positionWS.xz + shoreDirection *
                    (dryDistance + _HistoryProbeOffset);

                half fresh;
                half residual;
                if (_WetSandHistoryAvailable < 0.5)
                {
                    fresh = AnalyticBreaker(probe) *
                        _FallbackEventStrength;
                    residual = 0.0h;
                }
                else
                {
                    half2 historyA = SampleHistory(probe);
                    half2 historyB = SampleHistory(
                        probe + shoreDirection * _RetreatWidth * 1.5);
                    half2 historyC = SampleHistory(
                        probe + shoreDirection * _RunupDistance * 0.5);
                    half2 historyD = SampleHistory(
                        probe + shoreDirection * _RunupDistance);
                    fresh = max(
                        max(historyA.r, historyB.r),
                        max(historyC.r, historyD.r));
                    residual = max(
                        max(historyA.g, historyB.g),
                        max(historyC.g, historyD.g));
                }

                half eventStrength = saturate(
                    max(fresh, residual * 0.82h) * _EventGain);
                eventStrength *= lerp(
                    1.0h,
                    WetEdgeNoise(positionWS.xz),
                    saturate(_BreakupStrength));
                float frontDistance = _RunupDistance * sqrt(eventStrength);
                half distanceGate = 1.0h - smoothstep(
                    max(frontDistance - _RetreatWidth, 0.0),
                    frontDistance + _RetreatWidth,
                    dryDistance);
                float heightAboveWater = max(
                    positionWS.y - _WaterLevel, 0.0);
                half heightGate = 1.0h - smoothstep(
                    _RunupHeight * 0.7,
                    max(_RunupHeight, 0.001),
                    heightAboveWater);

                half shorelineDampness = saturate(
                    1.0h - dryDistance /
                    max(_RunupDistance * 0.42, 0.001)) * 0.42h;
                half reachedWetness = distanceGate * lerp(
                    0.55h, 1.0h, eventStrength);
                wetness = saturate(max(
                    reachedWetness, shorelineDampness) * heightGate);
                half frontBand = 1.0h - saturate(
                    abs(dryDistance - frontDistance) /
                    max(_RetreatWidth, 0.001));
                swash = saturate(frontBand * fresh * _EventGain * heightGate);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half wetness;
                half swash;
                EvaluateWetness(input.positionWS, wetness, swash);

                half3 albedo = lerp(_DryColor.rgb, _WetColor.rgb, wetness);
                albedo = lerp(albedo, _SwashColor.rgb, swash * 0.36h);
                half smoothness = lerp(
                    _DrySmoothness, _WetSmoothness, saturate(wetness + swash));

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(
                    input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(
                    input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half attenuation = max(
                    mainLight.distanceAttenuation, 0.25h) *
                    lerp(0.25h, 1.0h, mainLight.shadowAttenuation);
                half3 ambient = max(
                    SampleSH(normalWS), half3(0.42h, 0.42h, 0.42h));
                half3 color = albedo *
                    (ambient + mainLight.color * ndotl * attenuation);

                half3 halfDirection = SafeNormalize(
                    mainLight.direction + viewDirection);
                half specularPower = exp2(lerp(3.0h, 10.0h, smoothness));
                half specular = pow(
                    saturate(dot(normalWS, halfDirection)), specularPower) *
                    smoothness * attenuation;
                color += mainLight.color * specular * 0.35h;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(
                        _LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0.0h;
            }
            ENDHLSL
        }
    }
}
