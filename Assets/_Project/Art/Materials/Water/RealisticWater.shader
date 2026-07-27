// Experimental realistic water (URP 17, Forward transparent pass). Feature set: wind-coherent,
// deep-water-dispersion Gerstner waves with derivative normals + horizontal-displacement Jacobian,
// two-layer texture micro normals, edge-safe thickness-scaled refraction (_CameraOpaqueTexture),
// RGB Beer-Lambert absorption and in-scattering from view-ray water thickness
// (_CameraDepthTexture), bounded planar reflection with a sky/probe fallback,
// energy-conserving GGX/Schlick lighting,
// world-space temporal whitecap + shoreline foam with a no-history fallback, bounded projected
// receiver caustics with a cheap surface-composite fallback, shadows, and scene fog.
// Everything animated travels along +_WindDirection: crests, micro ripples, and the temporal foam
// advection all agree on one direction.
// Distance detail-fade tames specular/normal aliasing on far water. Independent of MarketWater /
// M_Ocean.
Shader "Market/World/RealisticWater"
{
    Properties
    {
        [Header(Wind)]
        // Direction the waves TRAVEL (not where they come from). The lab default points shoreward:
        // WaterShaderLab's beach terrace sits at -Z, the deep trench at +Z.
        _WindDirection("Wind Direction (X, Z)", Vector) = (0.4226, 0, -0.9063, 0)
        _WindSpread("Wind Directional Spread", Range(0, 1)) = 0.55

        [Header(Wave 1)]
        _Wave1Params("Wave 1 (Angle, Wavelength, Amplitude, Speed Multiplier)", Vector) = (25, 14, 0.35, 1.0)
        _Wave1Steepness("Wave 1 Steepness", Range(0, 1)) = 0.5

        [Header(Wave 2)]
        _Wave2Params("Wave 2 (Angle, Wavelength, Amplitude, Speed Multiplier)", Vector) = (95, 8, 0.2, 1.4)
        _Wave2Steepness("Wave 2 Steepness", Range(0, 1)) = 0.4

        [Header(Wave 3)]
        _Wave3Params("Wave 3 (Angle, Wavelength, Amplitude, Speed Multiplier)", Vector) = (200, 4.5, 0.1, 1.8)
        _Wave3Steepness("Wave 3 Steepness", Range(0, 1)) = 0.3

        [Header(Wave 4)]
        _Wave4Params("Wave 4 (Angle, Wavelength, Amplitude, Speed Multiplier)", Vector) = (320, 2.2, 0.05, 2.4)
        _Wave4Steepness("Wave 4 Steepness", Range(0, 1)) = 0.25

        [Header(Micro Detail)]
        [NoScaleOffset] _NormalMapA("Normal Layer A", 2D) = "bump" {}
        [NoScaleOffset] _NormalMapB("Normal Layer B", 2D) = "bump" {}
        _NormalLayerATiling("Normal Layer A Tiling", Range(0.01, 2)) = 0.18
        _NormalLayerBTiling("Normal Layer B Tiling", Range(0.01, 2)) = 0.55
        _NormalLayerASpeed("Normal Layer A Speed", Range(-0.5, 0.5)) = 0.025
        _NormalLayerBSpeed("Normal Layer B Speed", Range(-0.5, 0.5)) = 0.045
        _NormalLayerBRotation("Normal Layer B Wind Offset", Range(-90, 90)) = 32
        _MicroWaveStrength("Micro Ripple Strength", Range(0, 1)) = 0.25
        _DetailFadeStart("Detail Fade Start", Range(1, 200)) = 30
        _DetailFadeEnd("Detail Fade End", Range(2, 600)) = 180

        [Header(Refraction and Depth)]
        _RefractionStrength("Refraction Strength", Range(0, 0.2)) = 0.03
        _RefractionEdgeFade("Refraction Edge Fade", Range(0.001, 0.25)) = 0.08
        _RefractionDepthScale("Refraction Full Strength Depth", Range(0.1, 10)) = 2
        _DepthFadeDistance("Open Water Optical Path", Range(0.5, 60)) = 6
        _AbsorptionCoefficients("Absorption Coefficients (R, G, B)", Vector) = (0.22, 0.10, 0.04, 0)
        _ScatteringColor("In-Scattering Color", Color) = (0.015, 0.18, 0.32, 1)
        _ScatteringStrength("In-Scattering Strength", Range(0, 1)) = 0.4

        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (0.95, 0.98, 1.0, 1)
        _FoamCrestGain("Whitecap Gain", Range(0, 12)) = 4.0
        _FoamCrestBias("Whitecap Bias", Range(0, 1)) = 0.12
        _FoamShoreWidth("Shoreline Foam Width", Range(0.1, 10)) = 1.0
        _FoamNoiseTiling("Foam Noise Tiling", Range(0.02, 2)) = 0.3
        _FoamNoiseSpeed("Foam Noise Speed", Range(0, 2)) = 0.4
        _FoamCrestStrength("Whitecap Visual Strength", Range(0, 2)) = 1
        _FoamShoreStrength("Shoreline Visual Strength", Range(0, 2)) = 1
        [HideInInspector] [NoScaleOffset] _FoamHistoryTexture("Foam History", 2D) = "black" {}
        [HideInInspector] _FoamHistoryAvailable("Foam History Available", Float) = 0
        [HideInInspector] _FoamHistoryWorldRect("Foam History World Rect", Vector) = (0, 0, 0, 0)

        [Header(Caustics)]
        _CausticColor("Caustic Color", Color) = (0.6, 0.9, 0.85, 1)
        _CausticTiling("Caustic Tiling", Range(0.05, 2)) = 0.7
        _CausticSpeed("Caustic Speed", Range(0, 2)) = 0.5
        _CausticIntensity("Caustic Intensity", Range(0, 3)) = 1.8
        [HideInInspector] _ProjectedCausticsAvailable("Projected Caustics Available", Float) = 0

        [Header(Lighting)]
        _FresnelBase("Water F0 Reflectance", Range(0.01, 0.08)) = 0.0204
        _SpecStrength("Direct Reflection Strength", Range(0, 1)) = 1.0
        _Roughness("Perceptual Roughness", Range(0.08, 1)) = 0.08
        _ReflectionStrength("Environment Reflection Strength", Range(0, 1)) = 1.0
        _PlanarReflectionStrength("Planar Reflection Blend", Range(0, 1)) = 0.85
        _ReflectionEdgeFade("Planar Reflection Edge Fade", Range(0.001, 0.25)) = 0.08
        [HideInInspector] [NoScaleOffset] _PlanarReflectionTexture("Planar Reflection", 2D) = "black" {}
        [HideInInspector] _PlanarReflectionAvailable("Planar Reflection Available", Float) = 0
        [HideInInspector] _PlanarReflectionFlipY("Planar Reflection Flip Y", Float) = 0
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
            Name "ForwardRealisticWater"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMapA);
            SAMPLER(sampler_NormalMapA);
            TEXTURE2D(_NormalMapB);
            SAMPLER(sampler_NormalMapB);
            TEXTURE2D(_PlanarReflectionTexture);
            SAMPLER(sampler_PlanarReflectionTexture);
            TEXTURE2D(_FoamHistoryTexture);
            SAMPLER(sampler_FoamHistoryTexture);

            CBUFFER_START(UnityPerMaterial)
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
                float4 _NormalMapA_TexelSize;
                float4 _NormalMapB_TexelSize;
                float _NormalLayerATiling;
                float _NormalLayerBTiling;
                float _NormalLayerASpeed;
                float _NormalLayerBSpeed;
                float _NormalLayerBRotation;
                float _MicroWaveStrength;
                float _DetailFadeStart;
                float _DetailFadeEnd;
                float _RefractionStrength;
                float _RefractionEdgeFade;
                float _RefractionDepthScale;
                float _DepthFadeDistance;
                float4 _AbsorptionCoefficients;
                half4 _ScatteringColor;
                float _ScatteringStrength;
                half4 _FoamColor;
                float _FoamCrestGain;
                float _FoamCrestBias;
                float _FoamShoreWidth;
                float _FoamNoiseTiling;
                float _FoamNoiseSpeed;
                float _FoamCrestStrength;
                float _FoamShoreStrength;
                float _FoamHistoryAvailable;
                float4 _FoamHistoryWorldRect;
                half4 _CausticColor;
                float _CausticTiling;
                float _CausticSpeed;
                float _CausticIntensity;
                float _ProjectedCausticsAvailable;
                float _FresnelBase;
                float _SpecStrength;
                float _Roughness;
                float _ReflectionStrength;
                float _PlanarReflectionStrength;
                float _ReflectionEdgeFade;
                float _PlanarReflectionAvailable;
                float _PlanarReflectionFlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 macroNormal : TEXCOORD1;
                float foamJacobian : TEXCOORD2;
                float baseWaterY : TEXCOORD3;
                float2 baseWorldXZ : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            struct GerstnerWave
            {
                float2 direction;
                float wavelength;
                float amplitude;
                float speedMultiplier;
                float steepness;
            };

            GerstnerWave MakeWave(float4 packed, float steepness)
            {
                GerstnerWave w;

                float2 windDirection = _WindDirection.xz;
                float windLengthSq = dot(windDirection, windDirection);
                windDirection = windLengthSq > 0.0001
                    ? windDirection * rsqrt(windLengthSq)
                    : float2(1.0, 0.0);

                float windAngle = atan2(windDirection.y, windDirection.x);
                float authoredAngle = radians(packed.x);
                float angleDelta = authoredAngle - windAngle;
                float shortestDelta = atan2(sin(angleDelta), cos(angleDelta));
                float waveAngle = windAngle + shortestDelta * saturate(_WindSpread);

                w.direction = float2(cos(waveAngle), sin(waveAngle));
                w.wavelength = max(0.05, packed.y);
                w.amplitude = max(0.0, packed.z);
                w.speedMultiplier = max(0.0, packed.w);

                // Bound the sum of horizontal derivatives below the fold limit. This keeps the
                // displacement map invertible at normal tuning values while leaving headroom for
                // the Jacobian to approach zero and drive crest foam.
                float waveNumberAmplitude =
                    6.28318530718 * w.amplitude / w.wavelength;
                float foldSafeSteepness =
                    0.95 / max(4.0 * waveNumberAmplitude, 0.0001);
                w.steepness = min(saturate(steepness), foldSafeSteepness);
                return w;
            }

            // GPU Gems 1, Ch.1 "Effective Water Simulation from Physical Models": Gerstner
            // displacement and exact surface derivatives. Deep-water dispersion derives angular
            // frequency from wavelength; packed.w remains an art-directed frequency multiplier.
            // The time term is SUBTRACTED, unlike GPU Gems' printed "+ phi*t": with a plus sign the
            // crest travels along -direction, i.e. straight into the wind, and the temporal foam
            // (which advects along +_WindDirection) then smears its history against the crests.
            // Keep this sign identical to RealisticWaterFoamUpdate.compute::AccumulateJacobian.
            void EvaluateWave(
                GerstnerWave w, float2 xz, float t,
                inout float3 offset, inout float3 tangentX, inout float3 tangentZ)
            {
                float k = 6.28318530718 / w.wavelength;
                float wa = k * w.amplitude;
                float omega = sqrt(9.81 * k);
                float phase =
                    k * dot(w.direction, xz) - t * omega * w.speedMultiplier;
                float s = sin(phase);
                float c = cos(phase);

                offset.x += w.steepness * w.amplitude * w.direction.x * c;
                offset.z += w.steepness * w.amplitude * w.direction.y * c;
                offset.y += w.amplitude * s;

                float horizontalDerivative = w.steepness * wa * s;
                tangentX += float3(
                    -horizontalDerivative * w.direction.x * w.direction.x,
                    wa * w.direction.x * c,
                    -horizontalDerivative * w.direction.x * w.direction.y);
                tangentZ += float3(
                    -horizontalDerivative * w.direction.x * w.direction.y,
                    wa * w.direction.y * c,
                    -horizontalDerivative * w.direction.y * w.direction.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float baseWaterY = worldPos.y;
                float2 baseWorldXZ = worldPos.xz;
                float t = _Time.y;

                float3 offset = float3(0, 0, 0);
                float3 tangentX = float3(1, 0, 0);
                float3 tangentZ = float3(0, 0, 1);

                EvaluateWave(
                    MakeWave(_Wave1Params, _Wave1Steepness),
                    worldPos.xz, t, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave2Params, _Wave2Steepness),
                    worldPos.xz, t, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave3Params, _Wave3Steepness),
                    worldPos.xz, t, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave4Params, _Wave4Steepness),
                    worldPos.xz, t, offset, tangentX, tangentZ);

                worldPos += offset;

                // The horizontal Jacobian is taken directly from the same derivatives as the
                // macro normal. J == 1 on flat water and approaches 0 near a folding crest.
                float jxx = tangentX.x;
                float jzz = tangentZ.z;
                float jxz = 0.5 * (tangentX.z + tangentZ.x);
                float3 macroNormal = cross(tangentZ, tangentX);
                if (dot(macroNormal, macroNormal) < 0.000001)
                    macroNormal = float3(0, 1, 0);

                OUT.worldPos = worldPos;
                OUT.macroNormal = normalize(macroNormal);
                OUT.foamJacobian = jxx * jzz - jxz * jxz;
                OUT.baseWaterY = baseWaterY;
                OUT.baseWorldXZ = baseWorldXZ;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float2 RotateDirection(float2 direction, float angleDegrees)
            {
                float angle = radians(angleDegrees);
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float2(
                    direction.x * cosine - direction.y * sine,
                    direction.x * sine + direction.y * cosine);
            }

            half DetailFootprintFade(float2 uv, float2 textureSize)
            {
                float2 footprintX = ddx(uv) * textureSize;
                float2 footprintY = ddy(uv) * textureSize;
                float texelsPerPixel = max(length(footprintX), length(footprintY));
                return 1.0h - smoothstep(1.0h, 2.5h, texelsPerPixel);
            }

            half3 RotateLayerNormalToWorldBasis(
                half3 layerNormal, float2 layerAxisU, float2 layerAxisV)
            {
                return normalize(half3(
                    layerNormal.x * layerAxisU.x + layerNormal.y * layerAxisV.x,
                    layerNormal.x * layerAxisU.y + layerNormal.y * layerAxisV.y,
                    layerNormal.z));
            }

            // Reoriented normal mapping preserves the base direction better than direct component
            // addition when both sampled layers contain steep texels.
            half3 BlendReorientedNormals(half3 baseNormal, half3 detailNormal)
            {
                half3 t = baseNormal + half3(0, 0, 1);
                half3 u = detailNormal * half3(-1, -1, 1);
                return normalize(t * dot(t, u) - u * t.z);
            }

            half3 EvaluateMicroNormal(float2 baseWorldXZ, half distanceFade)
            {
                float2 windDirection = _WindDirection.xz;
                float windLengthSq = dot(windDirection, windDirection);
                windDirection = windLengthSq > 0.0001
                    ? windDirection * rsqrt(windLengthSq)
                    : float2(1.0, 0.0);
                float2 crossWind = float2(-windDirection.y, windDirection.x);
                float2 layerBAxisU =
                    RotateDirection(windDirection, _NormalLayerBRotation);
                float2 layerBAxisV = float2(-layerBAxisU.y, layerBAxisU.x);

                float2 uvA = float2(
                    dot(baseWorldXZ, windDirection),
                    dot(baseWorldXZ, crossWind)) * _NormalLayerATiling;
                float2 uvB = float2(
                    dot(baseWorldXZ, layerBAxisU),
                    dot(baseWorldXZ, layerBAxisV)) * _NormalLayerBTiling;
                // Subtract, so the sampled pattern drifts along +_WindDirection like the Gerstner
                // crests. Adding scrolls the UV window forward, which drags the ripples upwind.
                uvA -= float2(_Time.y * _NormalLayerASpeed, 0);
                uvB -= float2(
                    _Time.y * _NormalLayerBSpeed,
                    _Time.y * _NormalLayerBSpeed * 0.12);

                half strengthA = saturate(_MicroWaveStrength) * distanceFade *
                    DetailFootprintFade(uvA, _NormalMapA_TexelSize.zw);
                half strengthB = saturate(_MicroWaveStrength) * 0.7h * distanceFade *
                    DetailFootprintFade(uvB, _NormalMapB_TexelSize.zw);
                half3 sampleA = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMapA, sampler_NormalMapA, uvA), strengthA);
                half3 sampleB = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMapB, sampler_NormalMapB, uvB), strengthB);
                half3 normalA = RotateLayerNormalToWorldBasis(
                    sampleA, windDirection, crossWind);
                half3 normalB = RotateLayerNormalToWorldBasis(
                    sampleB, layerBAxisU, layerBAxisV);
                return BlendReorientedNormals(normalA, normalB);
            }

            float SceneSurfaceMask(float rawDepth)
            {
#if UNITY_REVERSED_Z
                return step(0.00001, rawDepth);
#else
                return 1.0 - step(0.99999, rawDepth);
#endif
            }

            // Reject sky depth, invalid reconstruction, foreground geometry, and opaque geometry
            // above the mean water surface. LinearEyeDepth handles both regular and reversed Z;
            // using the displaced fragment depth keeps steep waves from pulling foreground rocks
            // into the refraction lookup. NaN comparisons evaluate false and produce a zero mask.
            float UnderwaterSceneMask(
                float rawDepth, float3 scenePosWS, float waterWorldY, float waterEyeDepth)
            {
                float maximumCoordinate = max(
                    abs(scenePosWS.x), max(abs(scenePosWS.y), abs(scenePosWS.z)));
                float finitePosition = 1.0 - step(100000.0, maximumCoordinate);
                float belowWater = step(scenePosWS.y, waterWorldY + 0.01);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float behindWater = step(waterEyeDepth + 0.02, sceneEyeDepth);
                return SceneSurfaceMask(rawDepth) * finitePosition * belowWater * behindWater;
            }

            float ScreenEdgeFade(float2 screenUV, float fadeWidth)
            {
                float2 distanceToEdge = min(screenUV, 1.0 - screenUV);
                float nearestEdge = min(distanceToEdge.x, distanceToEdge.y);
                return smoothstep(0.0, max(fadeWidth, 0.0001), nearestEdge);
            }

            // Same breakup noise technique as MarketWater.shader's FoamNoise: three crossed sine
            // waves, cheap and seamless, no texture dependency.
            half FoamBreakupNoise(float2 worldXZ, float time)
            {
                float2 uv = worldXZ * _FoamNoiseTiling;
                float t = time * _FoamNoiseSpeed;
                half a = sin(dot(uv, float2(0.9, 0.35)) + t);
                half b = sin(dot(uv, float2(-0.35, 0.95)) - t * 0.7 + a * 0.8);
                half c = sin(dot(uv, float2(0.6, -0.8)) + t * 0.5 + b * 0.6);
                return saturate((a * 0.5 + b * 0.3 + c * 0.2) * 0.5 + 0.5);
            }

            // Cheap procedural caustic lattice: two crossed, drifting sine grids sharpened into
            // bright thin lines. Evaluated at the seabed's reconstructed world position, not the
            // water surface, so the pattern reads as light projected onto what's underneath.
            half CausticPattern(float2 worldXZ, float time)
            {
                float2 p1 = worldXZ * _CausticTiling + float2(time * 0.6, time * 0.4);
                float2 p2 = worldXZ * (_CausticTiling * 1.3) - float2(time * 0.5, time * 0.7);
                half n1 = abs(sin(p1.x) + sin(p1.y));
                half n2 = abs(sin(p2.x) + sin(p2.y));
                return pow(saturate(1.0 - (n1 + n2) * 0.4), 6.0);
            }

            float3 FresnelSchlick(float cosTheta, float3 f0)
            {
                float oneMinusCos = 1.0 - saturate(cosTheta);
                float oneMinusCos2 = oneMinusCos * oneMinusCos;
                float oneMinusCos5 = oneMinusCos2 * oneMinusCos2 * oneMinusCos;
                return f0 + (1.0 - f0) * oneMinusCos5;
            }

            float DistributionGGX(float ndoth, float alpha)
            {
                float alpha2 = alpha * alpha;
                float denominator = ndoth * ndoth * (alpha2 - 1.0) + 1.0;
                return alpha2 /
                    max(3.14159265359 * denominator * denominator, 0.00001);
            }

            // Height-correlated Smith visibility. This form already contains the 1/(4*NdotL*NdotV)
            // denominator used by a Cook-Torrance BRDF.
            float VisibilitySmithGGXCorrelated(float ndotv, float ndotl, float alpha)
            {
                float alpha2 = alpha * alpha;
                float lambdaV = ndotl * sqrt(
                    max((-ndotv * alpha2 + ndotv) * ndotv + alpha2, 0.0));
                float lambdaL = ndotv * sqrt(
                    max((-ndotl * alpha2 + ndotl) * ndotl + alpha2, 0.0));
                return 0.5 / max(lambdaV + lambdaL, 0.00001);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Fade micro detail (and, below, specular) out with camera distance: at the horizon
                // a whole ripple covers a fraction of a pixel, so keeping it at full strength gives
                // specular/normal aliasing (shimmering white speckle). MarketWater.shader solves the
                // same problem with _DetailFadeStart/End.
                float camDist = distance(GetCameraPositionWS(), IN.worldPos);
                half detail = saturate(1.0 - (camDist - _DetailFadeStart) / max(_DetailFadeEnd - _DetailFadeStart, 0.001));

                half3 macroNormal = normalize(IN.macroNormal);
                half3 macroNormalBasis =
                    half3(macroNormal.x, macroNormal.z, macroNormal.y);
                half3 detailNormalBasis =
                    EvaluateMicroNormal(IN.baseWorldXZ, detail);
                half3 combinedNormalBasis =
                    BlendReorientedNormals(macroNormalBasis, detailNormalBasis);
                half3 worldNormal = normalize(half3(
                    combinedNormalBasis.x,
                    combinedNormalBasis.z,
                    combinedNormalBasis.y));

                half3 viewDir = normalize(GetWorldSpaceViewDir(IN.worldPos));

                // A perturbed wave facet can end up pointing away from the camera (the back of a
                // steep ripple). Left as-is that gives NdotV < 0 -> Fresnel saturates to 1 -> a
                // white reflection firefly on the wave's underside. Flip the normal to face the
                // viewer so shading stays well-defined.
                if (dot(worldNormal, viewDir) < 0.0)
                    worldNormal = -worldNormal;

                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float waterEyeDepth = max(
                    -TransformWorldToView(IN.worldPos).z, 0.0);
                float centerRawDepth = SampleSceneDepth(screenUV);
                float3 centerScenePosWS = ComputeWorldSpacePosition(
                    screenUV, centerRawDepth, UNITY_MATRIX_I_VP);
                float centerSceneMask = UnderwaterSceneMask(
                    centerRawDepth, centerScenePosWS, IN.baseWaterY, waterEyeDepth);
                bool hasCenterUnderwaterScene = centerSceneMask > 0.5;
                float3 stableWaterPos =
                    float3(IN.worldPos.x, IN.baseWaterY, IN.worldPos.z);

                // Use the undistorted center ray to obtain stable thickness, then reduce distortion
                // in thin shoreline water. The edge fade drives the offset to zero before clamped
                // sampling can smear or wrap the opaque texture.
                float columnDepth = hasCenterUnderwaterScene
                    ? max(IN.baseWaterY - centerScenePosWS.y, 0.0)
                    : _DepthFadeDistance;
                float refractionThickness =
                    saturate(columnDepth / max(_RefractionDepthScale, 0.001));
                float refractionEdge = ScreenEdgeFade(
                    screenUV, _RefractionEdgeFade);
                float2 candidateRefractedUV = screenUV +
                    worldNormal.xz * _RefractionStrength *
                    refractionThickness * refractionEdge;
                const float screenMargin = 0.001;
                bool refractedUvInside =
                    all(candidateRefractedUV >= screenMargin) &&
                    all(candidateRefractedUV <= 1.0 - screenMargin);
                float2 refractedUV = clamp(
                    candidateRefractedUV, screenMargin, 1.0 - screenMargin);

                // Don't refract into sky, invalid reconstruction, or geometry above the water.
                float refractedRawDepth = SampleSceneDepth(refractedUV);
                float3 refractedScenePosWS = ComputeWorldSpacePosition(
                    refractedUV, refractedRawDepth, UNITY_MATRIX_I_VP);
                float refractedSceneMask = UnderwaterSceneMask(
                    refractedRawDepth, refractedScenePosWS, IN.baseWaterY, waterEyeDepth);
                bool refractionValid =
                    refractedUvInside && refractedSceneMask > 0.5;
                float2 sampleUV = refractionValid ? refractedUV : screenUV;
                float3 refractedSamplePosWS =
                    refractionValid ? refractedScenePosWS : centerScenePosWS;
                float refractedSampleMask =
                    refractionValid ? refractedSceneMask : centerSceneMask;
                float3 seabedPosWS = refractedSampleMask > 0.5
                    ? refractedSamplePosWS
                    : stableWaterPos - float3(0, _DepthFadeDistance, 0);

                // The screen-space refraction offset is only a color lookup approximation, not a
                // metric ray. Deriving thickness from its reconstructed point makes neighboring
                // seabed terraces leak into shallow pixels as green patches. Wave troughs can also
                // dip below shallow geometry and incorrectly trigger the open-water fallback. Use
                // the unperturbed center ray and mean water level for stable optical and vertical
                // depth, while retaining displaced geometry and refracted color.
                float opticalPathLength = hasCenterUnderwaterScene
                    ? min(distance(stableWaterPos, centerScenePosWS), 100.0)
                    : _DepthFadeDistance;
                float3 absorptionCoefficients =
                    max(_AbsorptionCoefficients.rgb, float3(0, 0, 0));
                half3 transmittance = exp(
                    -absorptionCoefficients * opticalPathLength);

                half3 sceneColor = SampleSceneColor(sampleUV);
                half causticVisibility = dot(
                    transmittance, half3(0.2126, 0.7152, 0.0722));
                half caustic = CausticPattern(
                    seabedPosWS.xz, _Time.y * _CausticSpeed) *
                    _CausticIntensity * causticVisibility * refractedSampleMask *
                    (1.0 - saturate(_ProjectedCausticsAvailable));
                sceneColor += _CausticColor.rgb * caustic;
                half3 scattering = _ScatteringColor.rgb *
                    saturate(_ScatteringStrength) * (1.0 - transmittance);
                half3 transmissionColor =
                    sceneColor * transmittance + scattering;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotlRaw = saturate(dot(worldNormal, mainLight.direction));   // shadow-free
                half shadow = mainLight.shadowAttenuation;
                half ndotl = ndotlRaw * shadow;                                    // shadowed lighting

                half nv = saturate(dot(worldNormal, viewDir));
                float3 f0 = saturate(_FresnelBase).xxx;
                float3 viewFresnel = FresnelSchlick(nv, f0);
                float perceptualRoughness = lerp(
                    max(_Roughness, 0.08), 0.35, 1.0 - detail);

                // Clamp to the upper hemisphere: a water surface only ever mirrors the sky, never
                // the skybox's lower "ground" hemisphere, which reads as an ugly brown tint on any
                // grazing/tilted wave facet whose raw reflect vector dips below horizontal.
                half3 reflectVector = reflect(-viewDir, worldNormal);
                reflectVector.y = max(reflectVector.y, 0.02h);
                reflectVector = normalize(reflectVector);
                half3 environmentReflection = GlossyEnvironmentReflection(
                    reflectVector, IN.worldPos, perceptualRoughness, 1.0, screenUV) *
                    saturate(_ReflectionStrength);
                float2 planarUV = clamp(
                    screenUV + worldNormal.xz * (_RefractionStrength * 0.5),
                    screenMargin,
                    1.0 - screenMargin);
                if (_PlanarReflectionFlipY > 0.5)
                    planarUV.y = 1.0 - planarUV.y;
                half3 planarReflection = SAMPLE_TEXTURE2D(
                    _PlanarReflectionTexture,
                    sampler_PlanarReflectionTexture,
                    planarUV).rgb * saturate(_ReflectionStrength);
                half planarWeight =
                    saturate(_PlanarReflectionAvailable) *
                    saturate(_PlanarReflectionStrength) *
                    ScreenEdgeFade(screenUV, _ReflectionEdgeFade);
                half3 reflectionColor = lerp(
                    environmentReflection, planarReflection, planarWeight);

                half3 color =
                    transmissionColor * (1.0 - viewFresnel) +
                    reflectionColor * viewFresnel;

                // GGX direct sun reflection. A roughness floor approximates the finite angular size
                // of the sun and avoids an impulse-like highlight on calm water. Only direct light
                // receives main-light shadows; environment reflection and transmission do not.
                float3 halfVectorUnnormalized = mainLight.direction + viewDir;
                float halfVectorLengthSq =
                    max(dot(halfVectorUnnormalized, halfVectorUnnormalized), 0.00001);
                float3 halfDir =
                    halfVectorUnnormalized * rsqrt(halfVectorLengthSq);
                float ndoth = saturate(dot(worldNormal, halfDir));
                float vdoth = saturate(dot(viewDir, halfDir));
                float alpha = perceptualRoughness * perceptualRoughness;
                float distribution = DistributionGGX(ndoth, alpha);
                float visibility =
                    VisibilitySmithGGXCorrelated(nv, ndotlRaw, alpha);
                float3 directFresnel = FresnelSchlick(vdoth, f0);
                float3 directSpecular =
                    distribution * visibility * directFresnel * ndotlRaw;
                color += mainLight.color * directSpecular *
                    shadow * detail * saturate(_SpecStrength);

                // Instantaneous Jacobian and shoreline terms remain the no-history fallback.
                // The default R6 path samples a camera-independent RG world-space history:
                // red stores advected/decaying whitecaps, green stores the broken shoreline band.
                half crestFoam = saturate((1.0 - IN.foamJacobian - _FoamCrestBias) * _FoamCrestGain);
                half shoreFoam = 1.0 - saturate(columnDepth / _FoamShoreWidth);
                half foamNoise = FoamBreakupNoise(IN.worldPos.xz, _Time.y);
                half2 instantFoam = half2(crestFoam, shoreFoam) *
                    (0.6h + 0.4h * foamNoise);
                float2 historyUV =
                    (IN.baseWorldXZ - _FoamHistoryWorldRect.xy) *
                    _FoamHistoryWorldRect.zw;
                half historyInside =
                    step(0.0, historyUV.x) *
                    step(historyUV.x, 1.0) *
                    step(0.0, historyUV.y) *
                    step(historyUV.y, 1.0);
                half2 historyFoam = SAMPLE_TEXTURE2D(
                    _FoamHistoryTexture,
                    sampler_FoamHistoryTexture,
                    saturate(historyUV)).rg;
                half historyWeight =
                    saturate(_FoamHistoryAvailable) * historyInside;
                half2 foamTerms = lerp(
                    instantFoam, historyFoam, historyWeight);
                half crestAmount =
                    saturate(foamTerms.r * _FoamCrestStrength);
                half shoreAmount =
                    saturate(foamTerms.g * _FoamShoreStrength);
                half foamAmount =
                    1.0h - (1.0h - crestAmount) * (1.0h - shoreAmount);
                color = lerp(color, _FoamColor.rgb * saturate(ndotl * 0.5 + 0.5), foamAmount);

                // The pass composites opaquely (alpha 1), so scene fog has to be applied here or
                // the water stays crisp against fogged terrain. Island.unity has linear fog on.
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
