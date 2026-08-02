Shader "Market/Nature/GrassWind"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo (Alpha = Cutout)", 2D) = "white" {}
        [MainColor] _BaseColor ("Root Tint", Color) = (0.15, 0.62, 0.28, 1)
        _TipColor ("Tip Tint", Color) = (0.55, 0.95, 0.35, 1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35

        // Off: root-to-tip comes from object-space Z (geometry tufts, Grass_1/Grass_2).
        // On: from UV.y -- required for flat cards, whose object-space Z extent is 0.
        [Toggle(_WINDMASK_UV)] _WindMaskFromUV ("Wind Mask From UV.y (flat cards)", Float) = 0
        // Meshes authored without a vertex-color set read as black and would kill the tint.
        // Drop to 0 for those; keep 1 for meshes whose vertex color IS the base tint.
        _VertexColorTint ("Vertex Color Tint Strength", Range(0,1)) = 1
        _ColorSaturation ("Color Saturation", Range(0,1.5)) = 0.74
        _ColorVariation ("Per-Clump Color Variation", Range(0,1)) = 0.82
        _PatchVariation ("Broad Meadow Patch Variation", Range(0,0.4)) = 0.15
        _RootDarkening ("Root Contact Darkening", Range(0,0.6)) = 0.32

        _Smoothness ("Smoothness (Glossy/Wet)", Range(0,1)) = 0.6
        _Translucency ("Translucency (Backlight)", Range(0,3)) = 1.2
        _WrapLighting ("Soft Wrap Lighting", Range(0,0.5)) = 0.24

        _ToonBands ("Toon Shading Bands", Range(1,6)) = 3
        _ToonSoftness ("Toon Band Softness", Range(0.01,0.5)) = 0.35
        // Past ~0.5 the card normal is effectively world-up: the toon ramp collapses to one band,
        // the backlight term goes to zero and the rim becomes a flat wash. Keep it below that.
        _NormalSoftness ("Normal Softness (Soapy)", Range(0,1)) = 0.4
        _RimColor ("Rim Glow Color", Color) = (0.85, 1.0, 0.9, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0,3)) = 0.15

        // Wind itself is global (GrassWindController). This is the only per-material say in it:
        // how hard this particular plant answers the same scene wind. 0 = rigid, 1 = normal.
        _WindResponse ("Wind Response", Range(0, 2)) = 1
        _BladeTipHeight ("Blade Tip Height (object space)", Float) = 0.002
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off

        // No UniversalGBuffer pass on purpose. This shader does its own toon/translucency/rim
        // lighting; a G-buffer pass would hand the pixels to URP's deferred PBR lighting and throw
        // all of that away. In a Deferred renderer, custom-lit materials belong in the forward-only
        // pass - which is where a shader with no G-buffer pass lands.

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _WINDMASK_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassWindCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float windMask    : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float windMask;
                float3 positionWS = ApplyJellyWind(IN.positionOS.xyz, IN.uv, windMask);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                OUT.windMask = windMask;
                return OUT;
            }

            half4 Frag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 vertexTint = lerp(half3(1.0h, 1.0h, 1.0h), IN.color.rgb, _VertexColorTint);
                half3 tint = lerp(_BaseColor.rgb, _TipColor.rgb, IN.windMask) * vertexTint;
                half3 baseColor = albedo.rgb * tint;
                half luminance = dot(baseColor, half3(0.2126h, 0.7152h, 0.0722h));
                baseColor = lerp(luminance.xxx, baseColor, _ColorSaturation);
                float3 instanceWorldPos = GetInstanceWorldPos();
                baseColor = ApplyGrassColorVariation(
                    baseColor,
                    GrassInstanceHash(instanceWorldPos.xz),
                    _ColorVariation);
                baseColor = ApplyGrassPatchVariation(
                    baseColor,
                    instanceWorldPos.xz,
                    _PatchVariation);
                baseColor *= lerp(1.0h - _RootDarkening, 1.0h, smoothstep(0.0h, 0.42h, IN.windMask));
                half alpha = albedo.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                float faceSign = IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                float3 normalWS = SoftenGrassNormal(IN.normalWS, faceSign);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(
                    (dot(normalWS, mainLight.direction) + _WrapLighting) /
                    (1.0 + _WrapLighting));
                float toonNdotL = ToonRamp(NdotL, _ToonBands, _ToonSoftness);
                float3 diffuse = mainLight.color * mainLight.shadowAttenuation * toonNdotL;

                // Cheap backlight translucency: light bleeding through the blade when the camera
                // looks roughly toward the light through it, so it doesn't read as a flat cutout.
                float backTerm = saturate(dot(-normalWS, mainLight.direction)) * saturate(dot(viewDirWS, -mainLight.direction));
                float3 translucency = mainLight.color * mainLight.shadowAttenuation * backTerm * _Translucency;

                float specExponent = lerp(8.0, 128.0, _Smoothness);
                float3 halfVec = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfVec)), specExponent) * _Smoothness;

                // Fresnel rim glow: reads as a wet/gel highlight along the silhouette.
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;

                float3 ambient = SampleSH(normalWS);
                float3 color = baseColor * (diffuse + translucency + ambient)
                    + spec * mainLight.color
                    + rim * _RimColor.rgb;

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightsCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    float addNdotL = ToonRamp(saturate(dot(normalWS, light.direction)), _ToonBands, _ToonSoftness);
                    color += baseColor * light.color * light.distanceAttenuation * light.shadowAttenuation * addNdotL;
                }
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ _WINDMASK_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassWindCommon.hlsl"

            // Set by URP while rendering each shadow map. Normally they arrive with
            // ShadowCasterPass.hlsl, which this shader cannot use because it animates the vertex
            // itself, so they are declared here instead.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float windMask;
                float3 positionWS = ApplyJellyWind(IN.positionOS.xyz, IN.uv, windMask);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // Without the bias the card shadow-maps onto itself and the whole patch stipples
                // with acne; without the near-plane clamp casters in front of the shadow frustum
                // get clipped away instead of flattened onto it.
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // Depth and depth-normals prepasses. Without these the grass is simply absent from
        // _CameraDepthTexture and _CameraNormalsTexture: SSAO (which this project runs with the
        // DepthNormals source) cannot see a single blade, and anything else reading depth looks
        // straight through the patch.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _WINDMASK_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GrassWindCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float windMask;
                float3 positionWS = ApplyJellyWind(IN.positionOS.xyz, IN.uv, windMask);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _ _WINDMASK_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GrassWindCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings DepthNormalsVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float windMask;
                float3 positionWS = ApplyJellyWind(IN.positionOS.xyz, IN.uv, windMask);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthNormalsFrag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);

                // The same softened normal the forward pass shades with, so ambient occlusion
                // agrees with the lighting instead of darkening edges the light says are flat.
                float faceSign = IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                float3 normalWS = SoftenGrassNormal(IN.normalWS, faceSign);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
