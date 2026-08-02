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
        // Per-metre Beer-Lambert extinction of the view path through the water column. Red must die
        // far faster than blue or the body reads as neutral grey haze instead of water; these are
        // tuned so a ~0.5 m shore is still clear, ~2.5 m is tinted, and ~6 m hides the seabed.
        _AbsorptionCoefficients("Absorption Coefficients (R, G, B)", Vector) = (1.6, 0.62, 0.34, 0)
        _ScatteringColor("In-Scattering Color", Color) = (0.015, 0.18, 0.32, 1)
        _ScatteringStrength("In-Scattering Strength", Range(0, 1)) = 0.4

        [Header(Crest Subsurface)]
        // Sunlight transmitted through the thin water at the top of a wave. This is what gives real
        // swell its turquoise glow on the sun side; without it the surface only ever reflects and
        // deep water reads as flat dark paint no matter how the reflection is tuned.
        _SubsurfaceColor("Crest Subsurface Color", Color) = (0.08, 0.42, 0.36, 1)
        _SubsurfaceStrength("Crest Subsurface Strength", Range(0, 4)) = 1.2
        _SubsurfacePower("Crest Subsurface Focus", Range(1, 16)) = 4.0
        _SubsurfaceHeight("Crest Subsurface Height", Range(0.05, 3)) = 0.5

        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (0.95, 0.98, 1.0, 1)
        _FoamCrestGain("Whitecap Gain", Range(0, 12)) = 4.0
        _FoamCrestBias("Whitecap Bias", Range(0, 1)) = 0.12
        // The Jacobian above only detects a folding crest, so it stays silent on anything short of
        // a near-breaking sea - at moderate wave settings J never leaves ~1 and no whitecap ever
        // appears. Real crests foam on the windward face well before they fold, so this second
        // driver marks the surface that is both high above still water and steeply tilted.
        // Whitecaps are a few percent of a moderate sea, not half of it. A threshold low enough to
        // catch the whole upper face of every swell leaves the crest mask sitting near 0.5 over
        // most of the surface, and the dissolve then shows the breakup noise rather than the waves.
        _FoamCrestHeight("Whitecap Height Threshold", Range(0, 3)) = 0.45
        _FoamCrestHeightFalloff("Whitecap Height Falloff", Range(0.01, 2)) = 0.2
        // Slope gain is deliberately low: the gradient term saturates across most of the surface
        // well before 6, which would foam the flats as readily as the crests.
        _FoamCrestSlopeGain("Whitecap Slope Gain", Range(0, 20)) = 3.0
        _FoamShoreWidth("Shoreline Foam Width", Range(0.1, 10)) = 1.0
        _FoamNoiseTiling("Foam Noise Tiling", Range(0.02, 2)) = 0.3
        _FoamNoiseSpeed("Foam Noise Speed", Range(0, 2)) = 0.4
        // 0 = the coverage mask is drawn as-is (a soft gradient, which reads as haze), 1 = the mask
        // is thresholded against the breakup noise, so the patch interior stays solid and only its
        // rim dissolves into speckle. Real foam has a ragged edge, not a fade.
        _FoamBreakup("Foam Edge Breakup", Range(0, 1)) = 0.7
        _FoamBubbleTiling("Foam Bubble Tiling Multiplier", Range(1, 12)) = 4.7
        _FoamCrestStrength("Whitecap Visual Strength", Range(0, 2)) = 1
        _FoamShoreStrength("Shoreline Visual Strength", Range(0, 2)) = 1
        [HideInInspector] [NoScaleOffset] _FoamHistoryTexture("Foam History", 2D) = "black" {}
        [HideInInspector] _FoamHistoryAvailable("Foam History Available", Float) = 0
        [HideInInspector] _FoamHistoryWorldRect("Foam History World Rect", Vector) = (0, 0, 0, 0)

        [Header(Shore and Object Contact)]
        // Depth over which the Gerstner waves ramp up from flat. Without this the full offset is
        // applied everywhere, so crests lift the surface above the beach and troughs sink it
        // through the seabed - the water visibly passes through the terrain.
        _ShoreWaveDepth("Wave Shoaling Depth", Range(0.05, 12)) = 2.5
        // Shore band measured in horizontal metres, not in metres of depth: a depth-based band
        // smears over a shallow slope and collapses to a line on a steep one.
        _ShoreBandWidth("Shoreline Band Width", Range(0.05, 12)) = 2.5
        _ShoreLineWidth("Waterline Width", Range(0.01, 3)) = 0.35
        _ShoreLineStrength("Waterline Strength", Range(0, 2)) = 1
        // Contact terms use the view-ray distance to the scene, so they wrap anything sticking out
        // of the water, including vertical faces where the vertical column depth is discontinuous.
        _ContactFoamWidth("Object Contact Foam Width", Range(0.01, 5)) = 0.7
        _ContactFadeWidth("Contact Softness", Range(0.005, 3)) = 0.3
        _ContactRippleStrength("Contact Ripple Strength", Range(0, 1)) = 0.35
        _ContactRippleFrequency("Contact Ripple Frequency", Range(0.5, 20)) = 5
        _ContactRippleSpeed("Contact Ripple Speed", Range(0, 8)) = 2.5
        [HideInInspector] [NoScaleOffset] _ShoreDepthTexture("Shore Depth Map", 2D) = "black" {}
        [HideInInspector] _ShoreDepthAvailable("Shore Depth Available", Float) = 0
        [HideInInspector] _ShoreDepthWorldRect("Shore Depth World Rect", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ShoreDepthTexelWorldSize("Shore Depth Texel World Size", Vector) = (1, 1, 0, 0)
        [HideInInspector] _ShoreDepthMaximum("Shore Depth Maximum", Float) = 60

        [Header(Caustics)]
        [NoScaleOffset] _CausticMap("Caustic Flipbook", 2D) = "black" {}
        _CausticColor("Caustic Tint", Color) = (1, 0.97, 0.9, 1)
        _CausticTiling("Caustic Tiling", Range(0.005, 0.5)) = 0.222
        _CausticSpeed("Caustic Boil Rate", Range(0, 2)) = 0.28
        _CausticIntensity("Caustic Intensity", Range(0, 3)) = 0.9
        _CausticPedestal("Caustic Pedestal", Range(0, 4)) = 1.28
        _CausticContrast("Caustic Contrast", Range(0.5, 3)) = 1.15
        _CausticSoften("Caustic Distance Soften", Range(0, 120)) = 30
        [HideInInspector] _CausticEncodeRange("Caustic Encode Range", Float) = 8
        [HideInInspector] _CausticAtlasLayout("Atlas Columns Rows Frames", Vector) = (8, 4, 32, 0)
        [HideInInspector] _CausticAtlasFrame("Atlas Frame Rect", Vector) = (0.12109375, 0.2421875, 0.001953125, 0.00390625)
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
            TEXTURE2D(_ShoreDepthTexture);
            SAMPLER(sampler_ShoreDepthTexture);
            TEXTURE2D(_CausticMap);
            SAMPLER(sampler_CausticMap);

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
                half4 _SubsurfaceColor;
                float _SubsurfaceStrength;
                float _SubsurfacePower;
                float _SubsurfaceHeight;
                half4 _FoamColor;
                float _FoamCrestGain;
                float _FoamCrestBias;
                float _FoamCrestHeight;
                float _FoamCrestHeightFalloff;
                float _FoamCrestSlopeGain;
                float _FoamShoreWidth;
                float _FoamNoiseTiling;
                float _FoamNoiseSpeed;
                float _FoamBreakup;
                float _FoamBubbleTiling;
                float _FoamCrestStrength;
                float _FoamShoreStrength;
                float _FoamHistoryAvailable;
                float4 _FoamHistoryWorldRect;
                half4 _CausticColor;
                float _CausticTiling;
                float _CausticSpeed;
                float _CausticIntensity;
                float _CausticPedestal;
                float _CausticContrast;
                float _CausticSoften;
                float _CausticEncodeRange;
                float4 _CausticAtlasLayout;
                float4 _CausticAtlasFrame;
                float _ProjectedCausticsAvailable;
                float _FresnelBase;
                float _SpecStrength;
                float _Roughness;
                float _ReflectionStrength;
                float _PlanarReflectionStrength;
                float _ReflectionEdgeFade;
                float _PlanarReflectionAvailable;
                float _PlanarReflectionFlipY;
                float _ShoreWaveDepth;
                float _ShoreBandWidth;
                float _ShoreLineWidth;
                float _ShoreLineStrength;
                float _ContactFoamWidth;
                float _ContactFadeWidth;
                float _ContactRippleStrength;
                float _ContactRippleFrequency;
                float _ContactRippleSpeed;
                float _ShoreDepthAvailable;
                float4 _ShoreDepthWorldRect;
                float4 _ShoreDepthTexelWorldSize;
                float _ShoreDepthMaximum;
            CBUFFER_END

            // Baked top-down shore map: x = water column depth, y = horizontal distance to the
            // waterline, both in metres. Returns the open-water maximum when there is no bake, so
            // an unbaked material behaves exactly as it did before the map existed. Outside the
            // baked rect it also reads as open water - clamping instead would drag a false
            // shoreline along the map border.
            float2 SampleShoreMap(float2 worldXZ)
            {
                if (_ShoreDepthAvailable < 0.5)
                    return _ShoreDepthMaximum.xx;

                float2 uv = (worldXZ - _ShoreDepthWorldRect.xy) * _ShoreDepthWorldRect.zw;
                if (any(uv < 0.0) || any(uv > 1.0))
                    return _ShoreDepthMaximum.xx;

                return SAMPLE_TEXTURE2D_LOD(
                    _ShoreDepthTexture, sampler_ShoreDepthTexture, uv, 0).rg;
            }

            float SampleShoreDepth(float2 worldXZ)
            {
                return SampleShoreMap(worldXZ).x;
            }

            // How much of the wave motion survives at this point. Waves flatten as the water
            // shallows, which is both what real shoaling does and what stops the surface from
            // punching through the beach.
            float ShoalingFactor(float2 worldXZ)
            {
                float depth = SampleShoreDepth(worldXZ);
                return saturate(depth / max(_ShoreWaveDepth, 0.001));
            }

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

                // Shoaling: fade the whole wave - offset and the derivatives that build the macro
                // normal - back to still water as the column shallows. Scaling the offset alone
                // would leave a lit, sloped surface on geometrically flat water.
                float shoaling = ShoalingFactor(baseWorldXZ);
                offset *= shoaling;
                tangentX = lerp(float3(1, 0, 0), tangentX, shoaling);
                tangentZ = lerp(float3(0, 0, 1), tangentZ, shoaling);

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

            // Same breakup noise technique as MarketWater.shader's FoamNoise - three crossed sine
            // waves, cheap and seamless, no texture dependency - but evaluated at two scales.
            // One octave can either place foam patches or texture them, never both: at the clump
            // tiling its features are metres wide, so on its own it just dims a large patch
            // uniformly and the whitecap reads as airbrushed haze. x = clump scale (which part of
            // the crest is foaming), y = bubble scale (the structure inside that patch).
            half2 FoamBreakupNoise(float2 worldXZ, float time)
            {
                float2 uv = worldXZ * _FoamNoiseTiling;
                float t = time * _FoamNoiseSpeed;
                half a = sin(dot(uv, float2(0.9, 0.35)) + t);
                half b = sin(dot(uv, float2(-0.35, 0.95)) - t * 0.7 + a * 0.8);
                half c = sin(dot(uv, float2(0.6, -0.8)) + t * 0.5 + b * 0.6);
                half clump = saturate((a * 0.5 + b * 0.3 + c * 0.2) * 0.5 + 0.5);

                // Bubble octave. It drifts against the clump layer so the two never lock into a
                // visible repeat, and faster, because fine foam churns quicker than it travels.
                float2 bubbleUv = uv * _FoamBubbleTiling;
                float bubbleT = t * -1.6;
                half d = sin(dot(bubbleUv, float2(0.8, -0.6)) + bubbleT);
                half e = sin(dot(bubbleUv, float2(0.45, 0.89)) + bubbleT * 0.8 + d * 0.9);
                half f = sin(dot(bubbleUv, float2(-0.7, -0.72)) - bubbleT * 0.6 + e * 0.7);
                half bubbles = saturate((d * 0.45 + e * 0.35 + f * 0.2) * 0.5 + 0.5);

                return half2(clump, bubbles);
            }

            // Foam coverage arrives as a continuous mask, but foam itself is binary: a patch of
            // bubbles is either there or it is not. Multiplying the mask by noise only dims it and
            // keeps the soft gradient - the fog look. Thresholding the mask against the noise
            // instead keeps the interior solid and dissolves the rim into speckle, and because the
            // mask still falls off away from the crest the speckle thins out with it.
            half FoamDissolve(half mask, half noise, half breakup)
            {
                // The mask is treated as a coverage FRACTION, not as a brightness: thresholding
                // the noise at 1 - mask lets through roughly `mask` of the area, so a mask peaking
                // at 0.4 yields 40 percent speckle. Thresholding the mask against a fixed 0.5
                // instead erases anything that never reaches 0.5 - which is every crest here.
                half width = max(0.5h - breakup * 0.45h, 0.02h);
                // The window is one-sided, anchored AT 1 - mask rather than centred on it. A
                // centred window puts its upper edge at 1 - mask + width, which noise peaks clear
                // even when mask is 0 - so foam sprayed across open water that had no foam mask at
                // all, in every channel at once, and no strength control could switch it off.
                // Anchored this way, mask 0 gives a window of [1, 1 + width] that noise cannot
                // enter, and coverage still tracks the mask everywhere above it.
                half threshold = 1.0h - mask;
                half speckle = smoothstep(threshold, threshold + width, noise);
                // breakup 0 leaves the original soft mask untouched, 1 is full speckle.
                return lerp(mask, speckle, breakup);
            }

            // Folds a tiling UV into one padded cell of the photon-traced caustic flipbook.
            // Explicit gradients are required because frac() would otherwise force the coarsest
            // mip at every tile seam; the baked wrap border keeps the taps inside the frame.
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

            // Cheap composite fallback for when the projected receiver overlays are off. World
            // space UVs keep the light attached to the reconstructed seabed, and consecutive
            // flipbook frames cross-fade so the filament network boils instead of sliding.
            // Returns the light above the mean seabed irradiance, which is what is additive.
            half3 CausticExcess(float2 worldXZ, float2 stableXZ, float time)
            {
                float2 uv = worldXZ * _CausticTiling;
                // Screen-space derivatives of the refracted seabed explode across depth
                // discontinuities, so mip selection follows the water surface instead.
                float2 uvDdx = ddx(stableXZ) * _CausticTiling;
                float2 uvDdy = ddy(stableXZ) * _CausticTiling;
                float frames = max(_CausticAtlasLayout.z, 1.0);
                float cursor = fmod(max(time, 0.0) * frames, frames);
                float frameIndex = floor(cursor);
                float blend = smoothstep(0.0, 1.0, cursor - frameIndex);
                half3 current = SampleCausticFrame(uv, frameIndex, uvDdx, uvDdy);
                half3 next = SampleCausticFrame(
                    uv, fmod(frameIndex + 1.0, frames), uvDdx, uvDdy);
                half3 field = lerp(current, next, blend) * _CausticEncodeRange;

                // Once a pixel covers more than a filament the mipped field flattens towards
                // its mean, so keeping the sharpening would only turn it into crawling dots.
                half footprint = max(length(uvDdx), length(uvDdy)) * _CausticSoften;
                half pedestal = lerp(_CausticPedestal, 1.0, saturate(footprint));
                return pow(max(field - pedestal, 0.0), _CausticContrast);
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

                // Distance along the view ray from this water fragment to whatever solid surface is
                // behind it. Unlike the vertical column depth this stays continuous across vertical
                // faces, so it wraps anything sticking out of the water instead of breaking at the
                // silhouette. Fragments in front of opaque geometry are already killed by ZTest, so
                // the difference cannot go meaningfully negative.
                float centerSceneEyeDepth = LinearEyeDepth(centerRawDepth, _ZBufferParams);
                bool hasContactSurface = SceneSurfaceMask(centerRawDepth) > 0.5;
                float contactDistance = hasContactSurface
                    ? max(centerSceneEyeDepth - waterEyeDepth, 0.0)
                    : _ShoreDepthMaximum;

                // Ripples hugging whatever the water touches. The world-space direction of
                // increasing contact distance points away from the obstacle, which is the axis the
                // rings have to travel along; it is reconstructed from screen derivatives because
                // the obstacle itself is only known through the depth buffer.
                if (_ContactRippleStrength > 0.0001 && hasContactSurface)
                {
                    float3 worldDdx = ddx(IN.worldPos);
                    float3 worldDdy = ddy(IN.worldPos);
                    float2 contactGradient =
                        ddx(contactDistance) * worldDdx.xz +
                        ddy(contactDistance) * worldDdy.xz;
                    float gradientLength = length(contactGradient);
                    if (gradientLength > 0.000001)
                    {
                        contactGradient /= gradientLength;
                        float rippleFalloff = exp(
                            -contactDistance / max(_ContactFoamWidth * 3.0, 0.001));
                        float ripple = sin(
                            contactDistance * _ContactRippleFrequency * TWO_PI -
                            _Time.y * _ContactRippleSpeed) *
                            rippleFalloff * _ContactRippleStrength;
                        worldNormal = normalize(
                            worldNormal +
                            float3(contactGradient.x, 0.0, contactGradient.y) * ripple);
                    }
                }

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
                half3 caustic = CausticExcess(
                    seabedPosWS.xz, IN.worldPos.xz, _Time.y * _CausticSpeed) *
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

                // Crest subsurface: strongest looking into the sun through a raised crest, and it
                // fades out with the wave height so troughs stay dark. Scaled by (1 - Fresnel)
                // because it is transmitted light, so it must vanish at grazing angles where the
                // surface turns into a mirror.
                half crestRise = saturate(
                    (IN.worldPos.y - IN.baseWaterY) / max(_SubsurfaceHeight, 0.001));
                half backScatter = saturate(dot(viewDir, -mainLight.direction));
                half subsurface =
                    pow(backScatter, _SubsurfacePower) * crestRise * _SubsurfaceStrength;
                color += _SubsurfaceColor.rgb * mainLight.color * subsurface *
                    (1.0 - viewFresnel) * shadow;

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
                half foldFoam = saturate((1.0 - IN.foamJacobian - _FoamCrestBias) * _FoamCrestGain);

                // Height-over-still-water gated by surface tilt. Both factors are required: the
                // height alone would paint the whole broad top of a swell, and the slope alone
                // would foam the troughs' flanks just as readily as the crests'. Combined they
                // land on the upper windward face, which is where spray actually sits. Taken as a
                // max with the folding term so a genuinely breaking wave still saturates.
                half heightTerm = saturate(
                    ((IN.worldPos.y - IN.baseWaterY) - _FoamCrestHeight) /
                    max(_FoamCrestHeightFalloff, 0.001));
                // Surface gradient, not (1 - n.y): on the long, low swells a calm sea uses, the
                // macro normal tilts only a few degrees, so 1 - n.y peaks around 0.05 and any
                // sane gain leaves the term at nil. tan(tilt) keeps a usable range on gentle
                // water and still grows without bound as a crest steepens.
                half slopeTerm = saturate(
                    (length(macroNormal.xz) / max(macroNormal.y, 0.001h)) * _FoamCrestSlopeGain);
                half crestFoam = max(foldFoam, heightTerm * slopeTerm);

                // Horizontal distance to the waterline, read straight out of the baked map. It is a
                // distance field, not a local gradient estimate, so a terraced seabed does not put
                // a false shoreline on every riser. Without a bake this falls back to the original
                // depth-based band.
                float shoreDistance = SampleShoreMap(IN.baseWorldXZ).y;

                half shoreFoam = _ShoreDepthAvailable > 0.5
                    ? 1.0h - saturate(shoreDistance / max(_ShoreBandWidth, 0.001))
                    : 1.0h - saturate(columnDepth / _FoamShoreWidth);
                half2 breakupNoise = FoamBreakupNoise(IN.worldPos.xz, _Time.y);
                // The clump octave decides which stretch of crest foams, the bubble octave gives
                // the rim its grain; the dissolve wants them as one threshold field.
                // Weighted towards the bubble octave on purpose. The clump octave's features are
                // metres wide, so when it dominates the threshold the dissolve just prints the
                // clump field onto the water as big soft blobs that ignore where the crests are.
                // Contrast-expanded about its midpoint as well: averaging two octaves narrows the
                // distribution towards 0.5, and a threshold field that never reaches its extremes
                // makes the dissolve fire almost nowhere.
                half foamNoise = saturate(
                    (breakupNoise.x * 0.3h + breakupNoise.y * 0.7h - 0.5h) * 1.7h + 0.5h);
                // Raw masks - the dissolve is applied after the history blend below, so the
                // temporal path in Play Mode gets the same breakup as this Edit Mode fallback
                // instead of coming back smooth.
                half2 instantFoam = half2(crestFoam, shoreFoam);
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
                half crestAmount = saturate(
                    FoamDissolve(foamTerms.r, foamNoise, _FoamBreakup) *
                    _FoamCrestStrength);
                // With a baked shore map the band comes from the map, not from the history: the
                // history's shoreline channel is injected by its own screen-independent scan and
                // would otherwise overwrite the geometry-accurate band computed above. The history
                // keeps owning the crest channel, which is what it is actually good at.
                // Shoreline foam gets roughly half the breakup the open-water crests do. It is a
                // BAND hugging the waterline, so it wants a ragged edge, not the full stochastic
                // dissolve - at full breakup a wide band stops reading as a band at all and
                // scatters into detached blobs across open water.
                half shoreBreakup = _FoamBreakup * 0.5h;
                half shoreAmount = _ShoreDepthAvailable > 0.5
                    ? saturate(
                        FoamDissolve(shoreFoam, foamNoise, shoreBreakup) * _FoamShoreStrength)
                    : saturate(
                        FoamDissolve(foamTerms.g, foamNoise, shoreBreakup) * _FoamShoreStrength);
                half foamAmount =
                    1.0h - (1.0h - crestAmount) * (1.0h - shoreAmount);

                // Contact foam and the waterline are applied after the history blend on purpose:
                // the history texture only carries the crest and broken-shore channels, so mixing
                // them in beforehand would let the history overwrite them entirely.
                half contactFoam = hasContactSurface
                    ? 1.0h - saturate(contactDistance / max(_ContactFoamWidth, 0.001))
                    : 0.0h;
                // Contact foam is a thin ring around an object; at full breakup the dissolve would
                // eat it, so it gets a gentler threshold than the open-water crests.
                half contactAmount = saturate(
                    FoamDissolve(contactFoam, foamNoise, _FoamBreakup * 0.6h));
                foamAmount = 1.0h - (1.0h - foamAmount) * (1.0h - contactAmount);

                half waterLine = _ShoreDepthAvailable > 0.5
                    ? saturate(
                        (1.0h - saturate(shoreDistance / max(_ShoreLineWidth, 0.001))) *
                        _ShoreLineStrength)
                    : 0.0h;
                foamAmount = 1.0h - (1.0h - foamAmount) * (1.0h - waterLine);

                // Foam is not one flat tone: the bubble octave shades it so the patch keeps
                // internal structure instead of reading as a solid decal once it is opaque.
                half foamShade = 0.72h + 0.28h * breakupNoise.y;
                color = lerp(
                    color,
                    _FoamColor.rgb * saturate(ndotl * 0.5 + 0.5) * foamShade,
                    foamAmount);

                // Dissolve the surface into the scene as the water column between it and the
                // geometry behind it goes to zero. The pass composites opaquely, so "alpha" has to
                // be done here as a blend towards the already-sampled scene colour - without it the
                // mesh simply stops at the intersection and the water reads as a sheet of plastic
                // cutting through the rocks.
                half presence = hasContactSurface
                    ? saturate(contactDistance / max(_ContactFadeWidth, 0.001))
                    : 1.0h;
                color = lerp(sceneColor, color, presence);

                // The pass composites opaquely (alpha 1), so scene fog has to be applied here or
                // the water stays crisp against fogged terrain. Island.unity has linear fog on.
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
