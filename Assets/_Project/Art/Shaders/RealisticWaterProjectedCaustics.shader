// Bounded geometry overlay for R7 world-space caustic projection in WaterShaderLab.
// The pattern comes from the photon-traced flipbook baked by RealisticWaterCausticBaker:
// stored 1.0 equals _CausticEncodeRange times the average seabed irradiance, so the shader
// only has to place, animate and attenuate a physically metered light field.
Shader "Market/World/RealisticWaterProjectedCaustics"
{
    Properties
    {
        [NoScaleOffset] _CausticMap("Caustic Flipbook", 2D) = "black" {}
        _CausticColor("Caustic Tint", Color) = (1, 0.97, 0.9, 1)
        _CausticIntensity("Caustic Intensity", Range(0, 4)) = 1.1
        _CausticScale("Tile Size (m)", Range(1, 40)) = 4.5
        _CausticDepthSpread("Depth Spread", Range(0, 1)) = 0.35
        _CausticSpeedA("Boil Rate (loops per second)", Range(0, 4)) = 0.28
        _CausticSpeedB("Drift Speed", Range(0, 4)) = 0.4
        _CausticFlow("Drift Direction XZ (m per second)", Vector) = (0.42, 0.15, 0, 0)
        _CausticWarp("Large Scale Warp", Range(0, 0.5)) = 0.06
        _CausticPedestal("Pedestal", Range(0, 4)) = 1.28
        _CausticContrast("Contrast", Range(0.5, 3)) = 1.15
        _CausticSoften("Distance Soften", Range(0, 120)) = 30
        _CausticAbsorption("Water Absorption (per metre)", Color) = (0.36, 0.06, 0.03, 1)
        _CausticDepthStart("Shallow Fade", Range(0.01, 2)) = 0.12
        _CausticDepthEnd("Deep Fade", Range(1, 30)) = 12
        _CausticTurbidity("Turbidity", Range(0, 1)) = 0.06
        [HideInInspector] _CausticEncodeRange("Encode Range", Float) = 8
        [HideInInspector] _CausticAtlasLayout("Atlas Columns Rows Frames", Vector) = (8, 4, 32, 0)
        [HideInInspector] _CausticAtlasFrame("Atlas Frame Rect", Vector) = (0.12109375, 0.2421875, 0.001953125, 0.00390625)
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

            TEXTURE2D(_CausticMap);
            SAMPLER(sampler_CausticMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _CausticColor;
                half4 _CausticAbsorption;
                float _CausticIntensity;
                float _CausticScale;
                float _CausticDepthSpread;
                float _CausticSpeedA;
                float _CausticSpeedB;
                float4 _CausticFlow;
                float _CausticWarp;
                float _CausticPedestal;
                float _CausticContrast;
                float _CausticSoften;
                float _CausticDepthStart;
                float _CausticDepthEnd;
                float _CausticTurbidity;
                float _CausticEncodeRange;
                float4 _CausticAtlasLayout;
                float4 _CausticAtlasFrame;
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

            // Folds a tiling UV into one padded atlas cell. Explicit gradients are required
            // because frac() would otherwise make the hardware pick the coarsest mip at the
            // tile seams; the baked wrap border keeps the taps inside the frame.
            half3 SampleCausticFrame(
                float2 uv, float frame, float2 uvDdx, float2 uvDdy)
            {
                float columns = max(_CausticAtlasLayout.x, 1.0);
                float rows = max(_CausticAtlasLayout.y, 1.0);
                float2 frameRect = _CausticAtlasFrame.xy;
                float2 cell = float2(1.0 / columns, 1.0 / rows);
                float2 cellIndex = float2(fmod(frame, columns), floor(frame / columns));
                float2 atlasUv =
                    cellIndex * cell + _CausticAtlasFrame.zw + frac(uv) * frameRect;
                return SAMPLE_TEXTURE2D_GRAD(
                    _CausticMap,
                    sampler_CausticMap,
                    atlasUv,
                    uvDdx * frameRect,
                    uvDdy * frameRect).rgb;
            }

            half3 SampleCausticField(
                float2 uv, float2 uvDdx, float2 uvDdy, float time)
            {
                float frames = max(_CausticAtlasLayout.z, 1.0);
                float cursor = fmod(time * max(_CausticSpeedA, 0.0) * frames, frames);
                float frameIndex = floor(cursor);
                float blend = smoothstep(0.0, 1.0, cursor - frameIndex);
                half3 current = SampleCausticFrame(uv, frameIndex, uvDdx, uvDdy);
                half3 next = SampleCausticFrame(
                    uv, fmod(frameIndex + 1.0, frames), uvDdx, uvDdy);
                return lerp(current, next, blend) * _CausticEncodeRange;
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
                float sunHeight = max(mainLight.direction.y, 0.08);

                // Slide the pattern to where the refracted sun actually lands, so a low sun
                // pushes the caustics away from the point they are drawn on.
                float travel = depth / sunHeight;
                float2 projectedXZ = IN.positionWS.xz + mainLight.direction.xz * travel;

                float time = _Time.y;
                // A deeper seabed sees the same surface focus spread out, so the cells grow.
                float tile = max(_CausticScale, 0.5) * (1.0 + depth * _CausticDepthSpread);
                float2 drift = _CausticFlow.xy * (_CausticSpeedB * time);
                float2 warp = float2(
                    sin(projectedXZ.y * 0.11 + time * 0.21),
                    sin(projectedXZ.x * 0.09 - time * 0.17)) * _CausticWarp;
                float2 uv = (projectedXZ + drift) / tile + warp;

                // Only the part above the mean seabed irradiance is additive light; the rest
                // is the ambient the receiver already renders on its own.
                float2 uvDdx = ddx(uv);
                float2 uvDdy = ddy(uv);
                half3 field = SampleCausticField(uv, uvDdx, uvDdy, time);

                // Once a pixel covers more than a filament the mipped field flattens towards
                // its mean, so keeping the sharpening would only turn it into crawling dots.
                // Relaxing the pedestal lets distant caustics dissolve into an even shimmer.
                half footprint = max(length(uvDdx), length(uvDdy)) * _CausticSoften;
                half pedestal = lerp(_CausticPedestal, 1.0, saturate(footprint));
                half3 excess = pow(max(field - pedestal, 0.0), _CausticContrast);

                half3 extinction =
                    max(_CausticAbsorption.rgb, 0.0) + max(_CausticTurbidity, 0.0);
                half3 transmission = exp(-extinction * travel);

                half shallowFade = smoothstep(
                    0.0, max(_CausticDepthStart, 0.02), depth);
                half deepFade = 1.0 - smoothstep(
                    _CausticDepthStart,
                    max(_CausticDepthEnd, _CausticDepthStart + 0.01),
                    depth);
                half sunAngle = smoothstep(0.03, 0.3, mainLight.direction.y);
                half receiverLight = saturate(
                    dot(normalize(IN.normalWS), mainLight.direction));
                half attenuation = shallowFade * deepFade * sunAngle *
                    mainLight.shadowAttenuation * receiverLight * _CausticIntensity;

                half3 color = _CausticColor.rgb * mainLight.color *
                    excess * transmission * attenuation;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
