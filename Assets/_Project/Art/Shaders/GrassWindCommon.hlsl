#ifndef MARKET_GRASS_WIND_COMMON_INCLUDED
#define MARKET_GRASS_WIND_COMMON_INCLUDED

// ---------------------------------------------------------------------------------------------
// Per-material constants.
//
// Declared once here instead of in every pass: the SRP Batcher requires each pass of a shader to
// agree on UnityPerMaterial byte for byte, and four hand-copied blocks drift the first time one is
// edited. Note what is NOT here - wind. Wind is a property of the world, not of a plant.
// ---------------------------------------------------------------------------------------------
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
    float _WindResponse;
    float _BladeTipHeight;
    float _WindMaskFromUV;
    float _VertexColorTint;
    float _ColorSaturation;
    float _ColorVariation;
    float _PatchVariation;
    float _RootDarkening;
    float _WrapLighting;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

// ---------------------------------------------------------------------------------------------
// Scene globals.
//
// Wind is one field for the whole world: every plant reads the same direction, speed and gust, and
// a material only says how strongly it answers (_WindResponse). Written once per frame by
// GrassWindController. Trample is written by GrassInteractionSystem from the player and NPCs
// registered in GrassTrample.
// ---------------------------------------------------------------------------------------------
#define MAX_GRASS_INTERACTORS 16
float4 _GrassInteractors[MAX_GRASS_INTERACTORS];    // xyz = world position, w = push radius
float4 _GrassInteractorDirs[MAX_GRASS_INTERACTORS]; // xy = travel direction (XZ), z = speed factor [0..1]
int _GrassInteractorCount;
float _GrassInteractionBend;

float4 _GrassWindDirection; // xy = direction (XZ), z = sway speed, w = spatial frequency
float4 _GrassWindMotion;    // x = sway strength, y = wobble speed, z = wobble frequency, w = wobble amount
float _GrassWindSquash;

struct GrassWindState
{
    float2 direction;
    float speed;
    float frequency;
    float strength;
    float wobbleSpeed;
    float wobbleFrequency;
    float wobbleAmount;
    float squash;
};

// A scene with no GrassWindController leaves the globals at zero, which would freeze every blade
// solid - a silent, confusing failure. Fall back to a gentle default breeze instead. The spatial
// frequency doubles as the "someone is driving this" marker: zero is never a valid wind.
GrassWindState ResolveWind()
{
    GrassWindState wind;

    if (_GrassWindDirection.w > 0.0)
    {
        wind.direction = _GrassWindDirection.xy;
        wind.speed = _GrassWindDirection.z;
        wind.frequency = _GrassWindDirection.w;
        wind.strength = _GrassWindMotion.x;
        wind.wobbleSpeed = _GrassWindMotion.y;
        wind.wobbleFrequency = _GrassWindMotion.z;
        wind.wobbleAmount = _GrassWindMotion.w;
        wind.squash = _GrassWindSquash;
    }
    else
    {
        wind.direction = float2(1.0, 0.0);
        wind.speed = 1.6;
        wind.frequency = 1.2;
        wind.strength = 0.05;
        wind.wobbleSpeed = 2.4;
        wind.wobbleFrequency = 0.8;
        wind.wobbleAmount = 0.03;
        wind.squash = 0.15;
    }

    // The only per-material say in the matter: how hard this plant answers the same wind.
    wind.strength *= _WindResponse;
    wind.wobbleAmount *= _WindResponse;
    wind.squash *= _WindResponse;
    return wind;
}

// Shared between every pass so the animated silhouette, its cast shadow and its depth/normals all
// match. A card that sways in colour but not in depth tears against SSAO and fog.
//
// Two ways to find "how far up the blade is this vertex", picked per material:
//
// _WINDMASK_UV off (Grass_1 / Grass_2, geometry tufts): object-space Z, root = 0, tip =
// _BladeTipHeight. Not the FBX "wind mask" vertex color -- Unity meshes only keep one vertex-color
// set, and for these two the set that survived import is the base tint (verified via
// Market/Debug/Inspect Grass Vertex Colors), not the authored mask. Root-to-tip is object-space Z
// because they're modeled lying flat (root at local origin) and stood upright by the instance's
// transform rotation.
//
// _WINDMASK_UV on (alpha-cutout cards): UV.y, which runs 0 at the root to 1 at the tip. Cards are
// baked standing upright by GrassCardBuilder, so their height is object-space Y -- and UV.y is
// scale-independent anyway: one material drives a 0.2 m and a 2 m card with no retuning.
float ComputeWindMask(float3 positionOS, float2 uv)
{
#if defined(_WINDMASK_UV)
    float mask = saturate(uv.y);
#else
    float mask = saturate(positionOS.z / max(_BladeTipHeight, 1e-5));
#endif
    return mask * mask; // ease toward the tip so the base stays stiff
}

float3 GetInstanceWorldPos()
{
    return float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
}

// Stable per-clump hash. World-space variation breaks up a meadow without material instances or
// per-renderer property blocks, so GPU instancing stays intact.
float GrassInstanceHash(float2 positionXZ)
{
    float2 cell = floor(positionXZ * 8.0 + 0.5);
    return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
}

half3 ApplyGrassColorVariation(half3 color, float hash, float amount)
{
    half signedVariation = (half)hash * 2.0h - 1.0h;
    half3 warmCool = half3(
        1.0h + signedVariation * 0.16h,
        1.0h - abs(signedVariation) * 0.04h,
        1.0h - signedVariation * 0.18h);
    return color * lerp(half3(1.0h, 1.0h, 1.0h), warmCool, (half)amount);
}

half3 ApplyGrassPatchVariation(half3 color, float2 positionXZ, float amount)
{
    float patch = sin(positionXZ.x * 0.11) * 0.5 +
        sin(positionXZ.y * 0.085 + 1.7) * 0.3 +
        sin(dot(positionXZ, float2(0.055, -0.07)) + 0.8) * 0.2;
    half signedPatch = (half)patch;
    half3 patchTint = half3(
        1.0h + signedPatch * 0.14h,
        1.0h + signedPatch * 0.04h,
        1.0h - signedPatch * 0.12h);
    return color * lerp(half3(1.0h, 1.0h, 1.0h), patchTint, (half)amount);
}

struct GrassInteractionState
{
    float2 windDirection;
    float2 bendDirection;
    float weight;
};

// The strongest nearby mover owns the clump. Its travel direction steers local wind while a blend
// of radial push and travel direction supplies the actual body bend.
GrassInteractionState ResolveGrassInteraction(float3 instancePos, float2 baseDir)
{
    GrassInteractionState state;
    state.windDirection = baseDir;
    state.bendDirection = baseDir;
    state.weight = 0.0;

    float2 bestTravelDir = baseDir;
    float2 bestBendDir = baseDir;
    float bestWeight = 0.0;
    int count = min(_GrassInteractorCount, MAX_GRASS_INTERACTORS);
    for (int i = 0; i < count; i++)
    {
        float4 interactor = _GrassInteractors[i];
        float2 delta = instancePos.xz - interactor.xz;
        float dist = length(delta);
        float radius = max(interactor.w, 1e-4);
        float falloff = smoothstep(0.0, 1.0, saturate(1.0 - dist / radius));
        float weight = falloff * _GrassInteractorDirs[i].z;
        if (weight > bestWeight)
        {
            bestWeight = weight;
            bestTravelDir = _GrassInteractorDirs[i].xy;
            float2 radialDir = normalize(delta + 1e-5);
            bestBendDir = normalize(lerp(radialDir, bestTravelDir, 0.65) + 1e-5);
        }
    }

    state.windDirection = normalize(
        lerp(baseDir, bestTravelDir, saturate(bestWeight)) + 1e-5);
    state.bendDirection = bestBendDir;
    state.weight = bestWeight;
    return state;
}

// Jelly-style motion (Slime Rancher-ish): the blade doesn't just sweep along the wind direction,
// it wiggles roughly perpendicular to it too, and bulges/thins like it's made of gel instead of
// bending stiffly. Phase is keyed off the instance's world position (not the per-vertex one) so
// the whole blade wobbles as a single soft body instead of rippling internally.
float3 ApplyJellyWind(float3 positionOS, float2 uv, out float windMask)
{
    windMask = ComputeWindMask(positionOS, uv);
    GrassWindState wind = ResolveWind();
    float3 instancePos = GetInstanceWorldPos();

    GrassInteractionState interaction =
        ResolveGrassInteraction(instancePos, normalize(wind.direction + 1e-5));
    float2 windDir = interaction.windDirection;
    float instancePhase = (GrassInstanceHash(instancePos.xz) - 0.5) * 0.45;
    float basePhase = dot(instancePos.xz, windDir) * wind.frequency +
        _Time.y * wind.speed + instancePhase;
    float sway = sin(basePhase) * 0.65 + sin(basePhase * 2.3 + 1.7) * 0.35;

    float2 perp = float2(-windDir.y, windDir.x);
    float wobblePhase = _Time.y * wind.wobbleSpeed + dot(instancePos.xz, perp) * wind.wobbleFrequency;
    float wobble = sin(wobblePhase) * 0.6 + sin(wobblePhase * 1.7 + 2.1) * 0.4;
    float gustPhase = dot(instancePos.xz, windDir) * 0.11 - _Time.y * wind.speed * 0.38;
    float localGust = lerp(0.72, 1.28, sin(gustPhase) * 0.5 + 0.5);

    // Squash/stretch in object space, like a gel body pushed sideways instead of pivoting at the
    // root. Only the two HORIZONTAL axes may move, and which those are differs per mesh family:
    // the geometry tufts are authored lying flat (height along Z), the alpha cards are baked
    // standing (height along Y). Scaling whichever axis is "up" makes the clump pump up and down
    // instead of fattening - which is exactly what the cards used to do.
    float squash = 1.0 + wobble * wind.squash * windMask;
#if defined(_WINDMASK_UV)
    positionOS.xz *= squash;
#else
    positionOS.x *= squash;
    positionOS.y *= (2.0 - squash);
#endif

    float3 positionWS = TransformObjectToWorld(positionOS);
    float2 offsetXZ = (
        windDir * sway * wind.strength * localGust +
        perp * wobble * wind.wobbleAmount) * windMask;
    float bendStrength = _GrassInteractionBend > 0.0 ? _GrassInteractionBend : 0.24;
    offsetXZ += interaction.bendDirection * interaction.weight * bendStrength * windMask;
    positionWS.xz += offsetXZ;
    positionWS.y -= (
        length(offsetXZ) * 0.35 +
        interaction.weight * bendStrength * 0.2) * windMask;

    return positionWS;
}

// The card is a flat plane, so its raw normal is horizontal and lights like a wall. Bending it
// toward world-up softens the clump into a rounded shape - but only part way: at full strength
// every card ends up with the same up normal, which flattens the toon ramp to one band, zeroes the
// backlight term and turns the Fresnel rim into a constant wash over the whole field.
float3 SoftenGrassNormal(float3 normalWS, float faceSign)
{
    normalWS = normalize(normalWS) * faceSign;
    return normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), _NormalSoftness));
}

// Smoothed stepped ramp for toon/cel shading: quantizes lighting into flat bands with a soft
// transition instead of Lambert's continuous falloff.
float ToonRamp(float x, float bands, float softness)
{
    float scaled = saturate(x) * bands;
    float band = floor(scaled);
    float t = smoothstep(0.5 - softness, 0.5 + softness, frac(scaled));
    return saturate((band + t) / max(bands - 1.0, 1.0));
}

#endif
