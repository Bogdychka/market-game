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

        _Smoothness ("Smoothness (Glossy/Wet)", Range(0,1)) = 0.6
        _Translucency ("Translucency (Backlight)", Range(0,3)) = 1.2

        _ToonBands ("Toon Shading Bands", Range(1,6)) = 2
        _ToonSoftness ("Toon Band Softness", Range(0.01,0.5)) = 0.35
        _NormalSoftness ("Normal Softness (Soapy)", Range(0,1)) = 0.7
        _RimColor ("Rim Glow Color", Color) = (0.85, 1.0, 0.9, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0,3)) = 0.8

        _WindDirection ("Wind Direction (XZ)", Vector) = (1, 0, 0, 0)
        _WindSpeed ("Wind Speed", Float) = 1.6
        _WindScale ("Wind Frequency", Float) = 1.2
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.06
        _WobbleSpeed ("Jelly Wobble Speed", Float) = 2.4
        _WobbleFrequency ("Jelly Wobble Frequency", Float) = 0.8
        _WobbleAmount ("Jelly Wobble Amount", Range(0, 0.3)) = 0.05
        _SquashAmount ("Jelly Squash Amount", Range(0, 1)) = 0.35
        _BladeTipHeight ("Blade Tip Height (object space)", Float) = 0.002
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off

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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _TipColor;
                float _Cutoff;
                float _Smoothness;
                float _Translucency;
                float _ToonBands;
                float _ToonSoftness;
                float _NormalSoftness;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float4 _WindDirection;
                float _WindSpeed;
                float _WindScale;
                float _WindStrength;
                float _WobbleSpeed;
                float _WobbleFrequency;
                float _WobbleAmount;
                float _SquashAmount;
                float _BladeTipHeight;
                float _WindMaskFromUV;
                float _VertexColorTint;
            CBUFFER_END

            #include "GrassWindCommon.hlsl"

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
                half alpha = albedo.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                float faceSign = IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                float3 normalWS = normalize(IN.normalWS) * faceSign;
                // Soap/soft look: bend the flat card normal toward world-up so lighting reads as a
                // rounded, blurry gradient across the clump instead of a hard flat plane.
                normalWS = normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), _NormalSoftness));
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
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
            #pragma shader_feature_local _ _WINDMASK_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _TipColor;
                float _Cutoff;
                float _Smoothness;
                float _Translucency;
                float _ToonBands;
                float _ToonSoftness;
                float _NormalSoftness;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float4 _WindDirection;
                float _WindSpeed;
                float _WindScale;
                float _WindStrength;
                float _WobbleSpeed;
                float _WobbleFrequency;
                float _WobbleAmount;
                float _SquashAmount;
                float _BladeTipHeight;
                float _WindMaskFromUV;
                float _VertexColorTint;
            CBUFFER_END

            #include "GrassWindCommon.hlsl"

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float windMask;
                float3 positionWS = ApplyJellyWind(IN.positionOS.xyz, IN.uv, windMask);

                OUT.positionCS = TransformWorldToHClip(positionWS);
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
    }

    Fallback Off
}
