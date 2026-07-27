// Optional R8 underside surface for WaterShaderLab.
Shader "Market/World/RealisticWaterUnderwaterSurface"
{
    Properties
    {
        [Header(Wind)]
        _WindDirection("Wind Direction", Vector) = (0.906, 0, 0.423, 0)
        _WindSpread("Wind Spread", Range(0, 1)) = 0.55

        [Header(Gerstner Waves)]
        _Wave1Params("Wave 1", Vector) = (25, 14, 0.35, 1)
        _Wave1Steepness("Wave 1 Steepness", Range(0, 1)) = 0.5
        _Wave2Params("Wave 2", Vector) = (95, 8, 0.2, 1.4)
        _Wave2Steepness("Wave 2 Steepness", Range(0, 1)) = 0.4
        _Wave3Params("Wave 3", Vector) = (200, 4.5, 0.1, 1.8)
        _Wave3Steepness("Wave 3 Steepness", Range(0, 1)) = 0.3
        _Wave4Params("Wave 4", Vector) = (320, 2.2, 0.05, 2.4)
        _Wave4Steepness("Wave 4 Steepness", Range(0, 1)) = 0.25

        [Header(Micro Normals)]
        [NoScaleOffset] _NormalMapA("Primary Normal", 2D) = "bump" {}
        [NoScaleOffset] _NormalMapB("Secondary Normal", 2D) = "bump" {}
        _NormalLayerATiling("Primary Tiling", Range(0.01, 2)) = 0.18
        _NormalLayerBTiling("Secondary Tiling", Range(0.01, 4)) = 0.55
        _NormalLayerASpeed("Primary Speed", Range(0, 0.2)) = 0.025
        _NormalLayerBSpeed("Secondary Speed", Range(0, 0.2)) = 0.045
        _NormalLayerBRotation("Secondary Rotation", Range(-180, 180)) = 32
        _MicroWaveStrength("Micro Normal Strength", Range(0, 1)) = 0.25
        _DetailFadeStart("Detail Fade Start", Float) = 30
        _DetailFadeEnd("Detail Fade End", Float) = 180

        [Header(Underwater Optics)]
        _AbsorptionCoefficients("Absorption Coefficients", Vector) = (0.22, 0.1, 0.04, 0)
        _ScatteringColor("Scattering Color", Color) = (0.015, 0.18, 0.32, 1)
        _ScatteringStrength("Scattering Strength", Range(0, 1)) = 0.4
        _FresnelBase("Water F0 Reflectance", Range(0.01, 0.08)) = 0.0204
        _Roughness("Perceptual Roughness", Range(0.08, 1)) = 0.08
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 1
        _InternalReflectionStrength("Internal Reflection Strength", Range(0, 1.5)) = 1
        _WaterIOR("Water IOR", Range(1.01, 1.6)) = 1.333
        _UnderwaterFogColor("Underwater Fog Color", Color) = (0.015, 0.18, 0.32, 1)
        [HideInInspector] _UnderwaterWaterHeight("Water Height", Float) = 0
        [HideInInspector] _UnderwaterTransitionBlend("Transition Blend", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
        }

        Pass
        {
            Name "UnderwaterSurface"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMapA);
            SAMPLER(sampler_NormalMapA);
            TEXTURE2D(_NormalMapB);
            SAMPLER(sampler_NormalMapB);

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
                float _NormalLayerATiling;
                float _NormalLayerBTiling;
                float _NormalLayerASpeed;
                float _NormalLayerBSpeed;
                float _NormalLayerBRotation;
                float _MicroWaveStrength;
                float _DetailFadeStart;
                float _DetailFadeEnd;
                float4 _AbsorptionCoefficients;
                half4 _ScatteringColor;
                float _ScatteringStrength;
                float _FresnelBase;
                float _Roughness;
                float _ReflectionStrength;
                float _InternalReflectionStrength;
                float _WaterIOR;
                half4 _UnderwaterFogColor;
                float _UnderwaterWaterHeight;
                float _UnderwaterTransitionBlend;
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
                float2 baseWorldXZ : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                GerstnerWave wave;
                float2 windDirection = _WindDirection.xz;
                float windLengthSq = dot(windDirection, windDirection);
                windDirection = windLengthSq > 0.0001
                    ? windDirection * rsqrt(windLengthSq)
                    : float2(1.0, 0.0);
                float windAngle = atan2(windDirection.y, windDirection.x);
                float authoredAngle = radians(packed.x);
                float angleDelta = authoredAngle - windAngle;
                float shortestDelta = atan2(sin(angleDelta), cos(angleDelta));
                float waveAngle =
                    windAngle + shortestDelta * saturate(_WindSpread);
                wave.direction = float2(cos(waveAngle), sin(waveAngle));
                wave.wavelength = max(0.05, packed.y);
                wave.amplitude = max(0.0, packed.z);
                wave.speedMultiplier = max(0.0, packed.w);
                float waveNumberAmplitude =
                    6.28318530718 * wave.amplitude / wave.wavelength;
                float foldSafeSteepness =
                    0.95 / max(4.0 * waveNumberAmplitude, 0.0001);
                wave.steepness = min(
                    saturate(steepness), foldSafeSteepness);
                return wave;
            }

            void EvaluateWave(
                GerstnerWave wave,
                float2 worldXZ,
                float time,
                inout float3 offset,
                inout float3 tangentX,
                inout float3 tangentZ)
            {
                float waveNumber = 6.28318530718 / wave.wavelength;
                float waveNumberAmplitude = waveNumber * wave.amplitude;
                float angularFrequency = sqrt(9.81 * waveNumber);
                float phase = waveNumber * dot(wave.direction, worldXZ) +
                    time * angularFrequency * wave.speedMultiplier;
                float sine = sin(phase);
                float cosine = cos(phase);
                offset.x += wave.steepness * wave.amplitude *
                    wave.direction.x * cosine;
                offset.z += wave.steepness * wave.amplitude *
                    wave.direction.y * cosine;
                offset.y += wave.amplitude * sine;
                float horizontalDerivative =
                    wave.steepness * waveNumberAmplitude * sine;
                tangentX += float3(
                    -horizontalDerivative * wave.direction.x * wave.direction.x,
                    waveNumberAmplitude * wave.direction.x * cosine,
                    -horizontalDerivative * wave.direction.x * wave.direction.y);
                tangentZ += float3(
                    -horizontalDerivative * wave.direction.x * wave.direction.y,
                    waveNumberAmplitude * wave.direction.y * cosine,
                    -horizontalDerivative * wave.direction.y * wave.direction.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float2 baseWorldXZ = worldPos.xz;
                float3 offset = float3(0, 0, 0);
                float3 tangentX = float3(1, 0, 0);
                float3 tangentZ = float3(0, 0, 1);
                float time = _Time.y;
                EvaluateWave(
                    MakeWave(_Wave1Params, _Wave1Steepness),
                    baseWorldXZ, time, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave2Params, _Wave2Steepness),
                    baseWorldXZ, time, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave3Params, _Wave3Steepness),
                    baseWorldXZ, time, offset, tangentX, tangentZ);
                EvaluateWave(
                    MakeWave(_Wave4Params, _Wave4Steepness),
                    baseWorldXZ, time, offset, tangentX, tangentZ);
                worldPos += offset;
                float3 macroNormal = cross(tangentZ, tangentX);
                if (dot(macroNormal, macroNormal) < 0.000001)
                    macroNormal = float3(0, 1, 0);
                OUT.worldPos = worldPos;
                OUT.macroNormal = normalize(macroNormal);
                OUT.baseWorldXZ = baseWorldXZ;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float2 RotateDirection(float2 direction, float angleDegrees)
            {
                float sine;
                float cosine;
                sincos(radians(angleDegrees), sine, cosine);
                return float2(
                    direction.x * cosine - direction.y * sine,
                    direction.x * sine + direction.y * cosine);
            }

            half3 RotateLayerNormalToWorldBasis(
                half3 layerNormal, float2 layerAxisU, float2 layerAxisV)
            {
                return normalize(half3(
                    layerNormal.x * layerAxisU.x +
                        layerNormal.y * layerAxisV.x,
                    layerNormal.x * layerAxisU.y +
                        layerNormal.y * layerAxisV.y,
                    layerNormal.z));
            }

            half3 BlendReorientedNormals(
                half3 baseNormal, half3 detailNormal)
            {
                half3 tangent = baseNormal + half3(0, 0, 1);
                half3 detail = detailNormal * half3(-1, -1, 1);
                return normalize(
                    tangent * dot(tangent, detail) - detail * tangent.z);
            }

            half3 EvaluateMicroNormal(float2 baseWorldXZ, half detail)
            {
                float2 windDirection = _WindDirection.xz;
                float windLengthSq = dot(windDirection, windDirection);
                windDirection = windLengthSq > 0.0001
                    ? windDirection * rsqrt(windLengthSq)
                    : float2(1.0, 0.0);
                float2 crossWind =
                    float2(-windDirection.y, windDirection.x);
                float2 layerBAxisU = RotateDirection(
                    windDirection, _NormalLayerBRotation);
                float2 layerBAxisV =
                    float2(-layerBAxisU.y, layerBAxisU.x);
                float2 uvA = float2(
                    dot(baseWorldXZ, windDirection),
                    dot(baseWorldXZ, crossWind)) * _NormalLayerATiling;
                float2 uvB = float2(
                    dot(baseWorldXZ, layerBAxisU),
                    dot(baseWorldXZ, layerBAxisV)) * _NormalLayerBTiling;
                uvA += float2(_Time.y * _NormalLayerASpeed, 0);
                uvB += float2(
                    _Time.y * _NormalLayerBSpeed,
                    _Time.y * _NormalLayerBSpeed * 0.12);
                half strengthA = saturate(_MicroWaveStrength) * detail;
                half strengthB = strengthA * 0.7h;
                half3 sampleA = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _NormalMapA, sampler_NormalMapA, uvA),
                    strengthA);
                half3 sampleB = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _NormalMapB, sampler_NormalMapB, uvB),
                    strengthB);
                half3 normalA = RotateLayerNormalToWorldBasis(
                    sampleA, windDirection, crossWind);
                half3 normalB = RotateLayerNormalToWorldBasis(
                    sampleB, layerBAxisU, layerBAxisV);
                return BlendReorientedNormals(normalA, normalB);
            }

            float3 FresnelSchlick(float cosine, float3 f0)
            {
                float oneMinusCosine = 1.0 - saturate(cosine);
                float squared = oneMinusCosine * oneMinusCosine;
                float fifth = squared * squared * oneMinusCosine;
                return f0 + (1.0 - f0) * fifth;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float cameraDistance =
                    distance(GetCameraPositionWS(), IN.worldPos);
                half detail = saturate(
                    1.0 - (cameraDistance - _DetailFadeStart) /
                    max(_DetailFadeEnd - _DetailFadeStart, 0.001));
                half3 macroNormal = normalize(IN.macroNormal);
                half3 macroBasis =
                    half3(macroNormal.x, macroNormal.z, macroNormal.y);
                half3 detailBasis =
                    EvaluateMicroNormal(IN.baseWorldXZ, detail);
                half3 combinedBasis =
                    BlendReorientedNormals(macroBasis, detailBasis);
                half3 upwardNormal = normalize(half3(
                    combinedBasis.x,
                    combinedBasis.z,
                    combinedBasis.y));
                half3 worldNormal = -upwardNormal;
                half3 viewDirection =
                    normalize(GetWorldSpaceViewDir(IN.worldPos));
                if (dot(worldNormal, viewDirection) < 0.0)
                    worldNormal = -worldNormal;

                float2 screenUV =
                    GetNormalizedScreenSpaceUV(IN.positionCS);
                half3 sceneColor = SampleSceneColor(screenUV);
                float cameraDepth = max(
                    _UnderwaterWaterHeight - GetCameraPositionWS().y, 0.0);
                float3 transmittance = exp(
                    -max(_AbsorptionCoefficients.rgb, 0.0) * cameraDepth);
                half3 scattering = _ScatteringColor.rgb *
                    saturate(_ScatteringStrength) * (1.0 - transmittance);
                half3 transmission =
                    sceneColor * transmittance + scattering;
                transmission = lerp(
                    transmission,
                    _UnderwaterFogColor.rgb,
                    saturate(_UnderwaterTransitionBlend) * 0.2h);

                half normalView =
                    saturate(dot(worldNormal, viewDirection));
                float ior = max(_WaterIOR, 1.01);
                float eta = 1.0 / ior;
                float criticalCosine =
                    sqrt(saturate(1.0 - eta * eta));
                half totalInternalReflection = 1.0h - smoothstep(
                    criticalCosine - 0.08,
                    criticalCosine + 0.08,
                    normalView);
                half fresnel = FresnelSchlick(
                    normalView, saturate(_FresnelBase).xxx).r;
                half reflectionWeight = saturate(
                    max(fresnel, totalInternalReflection) *
                    _InternalReflectionStrength);
                reflectionWeight *= smoothstep(
                    0.2h,
                    0.8h,
                    saturate(_UnderwaterTransitionBlend));

                half3 reflectionVector =
                    reflect(-viewDirection, worldNormal);
                reflectionVector.y = abs(reflectionVector.y);
                reflectionVector = normalize(reflectionVector);
                half roughness = lerp(
                    max(_Roughness, 0.08), 0.3, 1.0 - detail);
                half3 environmentReflection =
                    GlossyEnvironmentReflection(
                        reflectionVector,
                        IN.worldPos,
                        roughness,
                        1.0,
                        screenUV) * saturate(_ReflectionStrength);
                environmentReflection = lerp(
                    environmentReflection,
                    _UnderwaterFogColor.rgb,
                    0.25h + saturate(_UnderwaterTransitionBlend) * 0.2h);
                half3 color = lerp(
                    transmission, environmentReflection, reflectionWeight);
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
