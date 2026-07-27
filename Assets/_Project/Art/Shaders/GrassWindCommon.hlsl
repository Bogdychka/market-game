#ifndef MARKET_GRASS_WIND_COMMON_INCLUDED
#define MARKET_GRASS_WIND_COMMON_INCLUDED

// Set every frame by GrassInteractionSystem.cs (Shader.SetGlobalVectorArray/SetGlobalInt) from the
// player and NPCs registered in GrassTrample. Global (not per-material), shared by every grass
// instance in the scene.
#define MAX_GRASS_INTERACTORS 16
float4 _GrassInteractors[MAX_GRASS_INTERACTORS];    // xyz = world position, w = push radius
float4 _GrassInteractorDirs[MAX_GRASS_INTERACTORS]; // xy = travel direction (XZ), z = speed factor [0..1]
int _GrassInteractorCount;

// Shared between the forward and shadow-caster passes so the animated silhouette and its cast
// shadow always match.
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
// authored flat like the tufts, so the legacy path would still pick the right axis -- but it needs
// _BladeTipHeight retuned per card size, and a 2 mm tip height on a 0.4 m card saturates the mask
// almost at the root, swaying the whole quad as a rigid slab. UV.y is scale-independent: one
// material drives a 0.2 m and a 2 m card with no retuning.
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

// Local wind direction for this blade. Far from every mover it is just the global _WindDirection.
// Near a MOVING player/NPC (within its radius) the direction is turned toward that mover's travel
// direction -- the same wind sway, just blowing the way they walk. Nothing is displaced or deformed
// by proximity: only the direction the existing wind blows changes. A stationary mover has
// speedFactor ~0, so the blade smoothly returns to the ambient wind direction.
float2 ComputeLocalWindDir(float3 instancePos)
{
    float2 baseDir = normalize(_WindDirection.xy + 1e-5);

    float2 bestDir = float2(0.0, 0.0);
    float bestWeight = 0.0;
    int count = min(_GrassInteractorCount, MAX_GRASS_INTERACTORS);
    for (int i = 0; i < count; i++)
    {
        float4 interactor = _GrassInteractors[i];
        float dist = length(instancePos.xz - interactor.xz);
        float radius = max(interactor.w, 1e-4);
        float falloff = smoothstep(0.0, 1.0, saturate(1.0 - dist / radius));
        float weight = falloff * _GrassInteractorDirs[i].z; // z = speed factor [0..1]
        if (weight > bestWeight)
        {
            bestWeight = weight;
            bestDir = _GrassInteractorDirs[i].xy;
        }
    }

    return normalize(lerp(baseDir, bestDir, saturate(bestWeight)) + 1e-5);
}

// Jelly-style motion (Slime Rancher-ish): the blade doesn't just sweep along the wind direction,
// it wiggles roughly perpendicular to it too, and bulges/thins like it's made of gel instead of
// bending stiffly. Phase is keyed off the instance's world position (not the per-vertex one) so
// the whole blade wobbles as a single soft body instead of rippling internally.
float3 ApplyJellyWind(float3 positionOS, float2 uv, out float windMask)
{
    windMask = ComputeWindMask(positionOS, uv);
    float3 instancePos = GetInstanceWorldPos();

    float2 windDir = ComputeLocalWindDir(instancePos);
    float basePhase = dot(instancePos.xz, windDir) * _WindScale + _Time.y * _WindSpeed;
    float sway = sin(basePhase) * 0.65 + sin(basePhase * 2.3 + 1.7) * 0.35;

    float2 perp = float2(-windDir.y, windDir.x);
    float wobblePhase = _Time.y * _WobbleSpeed + dot(instancePos.xz, perp) * _WobbleFrequency;
    float wobble = sin(wobblePhase) * 0.6 + sin(wobblePhase * 1.7 + 2.1) * 0.4;

    // Squash/stretch in object space: widen across the blade while it's wobbling, like a gel
    // body being pushed sideways, instead of just pivoting at the root.
    float squash = 1.0 + wobble * _SquashAmount * windMask;
    positionOS.x *= squash;
    positionOS.y *= (2.0 - squash);

    float3 positionWS = TransformObjectToWorld(positionOS);
    float2 offsetXZ = (windDir * sway * _WindStrength + perp * wobble * _WobbleAmount) * windMask;
    positionWS.xz += offsetXZ;
    positionWS.y -= length(offsetXZ) * 0.35 * windMask;

    return positionWS;
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
