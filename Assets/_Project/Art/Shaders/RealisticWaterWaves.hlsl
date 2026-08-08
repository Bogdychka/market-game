#ifndef REALISTIC_WATER_WAVES_INCLUDED
#define REALISTIC_WATER_WAVES_INCLUDED

// Single source of the Gerstner wave bank. Every place that has to agree on where a crest is -
// the surface shader, the underwater surface shader and the whitecap compute - includes this file
// instead of keeping its own copy, and Market.World.WaveSampler mirrors it line for line in C# so
// gameplay can read the same surface the GPU draws.
//
// Layers come from a WaveProfile asset, uploaded as global arrays by WaveProfileBinder. When no
// profile is bound (_WaveLayerCount <= 0) the bank falls back to the four legacy per-material
// wave properties, so existing materials and the weather controller keep working untouched.
//
// Include AFTER the material CBUFFER: the legacy fallback references _Wave1Params.._Wave4Steepness.

#define REALISTIC_WATER_MAX_WAVE_LAYERS 8
#define REALISTIC_WATER_TWO_PI 6.28318530718
#define REALISTIC_WATER_GRAVITY 9.81

// The compute shader does not pull in the URP core headers that define this.
#ifndef UNITY_LOOP
    #define UNITY_LOOP [loop]
#endif

// Deliberately outside UnityPerMaterial: arrays in that CBUFFER break SRP Batcher compatibility,
// and one wave bank is a world-level property anyway - every water surface in a scene shares it.
// (directionAngleDegrees, wavelength, amplitude, speedMultiplier)
float4 _WaveLayerA[REALISTIC_WATER_MAX_WAVE_LAYERS];
// (steepness, mode: 0 = directional / 1 = circular, originX, originZ)
float4 _WaveLayerB[REALISTIC_WATER_MAX_WAVE_LAYERS];
float _WaveLayerCount;
// Upper bound on the sum of horizontal derivatives, i.e. how close to folding a crest may get.
// 0.95 is the value the four-wave bank shipped with; the profile exposes it as Steepness Clamping.
float _WaveFoldLimit;

struct RealisticWaterWave
{
    float2 direction;
    float2 origin;
    float wavelength;
    float amplitude;
    float speedMultiplier;
    float steepness;
    float mode;
};

float2 RealisticWaterWindDirection()
{
    float2 windDirection = _WindDirection.xz;
    float windLengthSq = dot(windDirection, windDirection);
    return windLengthSq > 0.0001
        ? windDirection * rsqrt(windLengthSq)
        : float2(1.0, 0.0);
}

// Authored angles are pulled toward the wind by _WindSpread, so one wind control still steers a
// whole bank of independently authored layers: spread 0 makes every wave travel with the wind,
// spread 1 keeps the authored fan.
float RealisticWaterWindAlignedAngle(float authoredAngleDegrees)
{
    float2 windDirection = RealisticWaterWindDirection();
    float windAngle = atan2(windDirection.y, windDirection.x);
    float authoredAngle = radians(authoredAngleDegrees);
    float angleDelta = authoredAngle - windAngle;
    float shortestDelta = atan2(sin(angleDelta), cos(angleDelta));
    return windAngle + shortestDelta * saturate(_WindSpread);
}

RealisticWaterWave RealisticWaterMakeWave(float4 layerA, float4 layerB)
{
    RealisticWaterWave wave;

    float waveAngle = RealisticWaterWindAlignedAngle(layerA.x);
    wave.direction = float2(cos(waveAngle), sin(waveAngle));
    wave.origin = layerB.zw;
    wave.wavelength = max(0.05, layerA.y);
    wave.amplitude = max(0.0, layerA.z);
    wave.speedMultiplier = max(0.0, layerA.w);
    wave.mode = layerB.y;

    // Bound the sum of horizontal derivatives below the fold limit. This keeps the displacement
    // map invertible at normal tuning values while leaving headroom for the Jacobian to approach
    // zero and drive crest foam.
    float waveNumberAmplitude =
        REALISTIC_WATER_TWO_PI * wave.amplitude / wave.wavelength;
    float foldLimit = _WaveFoldLimit > 0.0001 ? _WaveFoldLimit : 0.95;
    float foldSafeSteepness = foldLimit / max(4.0 * waveNumberAmplitude, 0.0001);
    wave.steepness = min(saturate(layerB.x), foldSafeSteepness);
    return wave;
}

RealisticWaterWave RealisticWaterMakeLegacyWave(float4 packed, float steepness)
{
    return RealisticWaterMakeWave(packed, float4(steepness, 0.0, 0.0, 0.0));
}

// GPU Gems 1, Ch.1 "Effective Water Simulation from Physical Models": Gerstner displacement and
// exact surface derivatives. Deep-water dispersion derives angular frequency from wavelength;
// speedMultiplier remains an art-directed frequency multiplier.
// The time term is SUBTRACTED, unlike GPU Gems' printed "+ phi*t": with a plus sign the crest
// travels along -direction, i.e. straight into the wind, and the temporal foam (which advects
// along +_WindDirection) then smears its history against the crests.
void RealisticWaterEvaluateWave(
    RealisticWaterWave wave,
    float2 worldXZ,
    float time,
    inout float3 offset,
    inout float3 tangentX,
    inout float3 tangentZ)
{
    // Circular layers radiate from an origin, which is how a lake or a pond reads: the travel
    // direction is the outward radial and the phase runs on distance instead of a projection.
    // The derivatives below ignore the 1/r turning of that direction - the standard approximation,
    // valid a few wavelengths out from the origin, which is where a circular layer is authored.
    float2 direction = wave.direction;
    float phaseDistance = dot(direction, worldXZ);
    if (wave.mode > 0.5)
    {
        float2 toPoint = worldXZ - wave.origin;
        float distance = length(toPoint);
        direction = distance > 0.0001 ? toPoint / distance : direction;
        phaseDistance = distance;
    }

    float waveNumber = REALISTIC_WATER_TWO_PI / wave.wavelength;
    float waveNumberAmplitude = waveNumber * wave.amplitude;
    float angularFrequency = sqrt(REALISTIC_WATER_GRAVITY * waveNumber);
    float phase =
        waveNumber * phaseDistance - time * angularFrequency * wave.speedMultiplier;
    float sine = sin(phase);
    float cosine = cos(phase);

    offset.x += wave.steepness * wave.amplitude * direction.x * cosine;
    offset.z += wave.steepness * wave.amplitude * direction.y * cosine;
    offset.y += wave.amplitude * sine;

    float horizontalDerivative = wave.steepness * waveNumberAmplitude * sine;
    tangentX += float3(
        -horizontalDerivative * direction.x * direction.x,
        waveNumberAmplitude * direction.x * cosine,
        -horizontalDerivative * direction.x * direction.y);
    tangentZ += float3(
        -horizontalDerivative * direction.x * direction.y,
        waveNumberAmplitude * direction.y * cosine,
        -horizontalDerivative * direction.y * direction.y);
}

// Accumulates the whole bank. Start from offset 0 and the identity tangents:
// tangentX = (1, 0, 0), tangentZ = (0, 0, 1).
void RealisticWaterAccumulateWaves(
    float2 worldXZ,
    float time,
    inout float3 offset,
    inout float3 tangentX,
    inout float3 tangentZ)
{
    int layerCount = (int)_WaveLayerCount;
    if (layerCount > 0)
    {
        UNITY_LOOP
        for (int i = 0; i < REALISTIC_WATER_MAX_WAVE_LAYERS; i++)
        {
            if (i >= layerCount)
                break;

            RealisticWaterEvaluateWave(
                RealisticWaterMakeWave(_WaveLayerA[i], _WaveLayerB[i]),
                worldXZ, time, offset, tangentX, tangentZ);
        }

        return;
    }

    RealisticWaterEvaluateWave(
        RealisticWaterMakeLegacyWave(_Wave1Params, _Wave1Steepness),
        worldXZ, time, offset, tangentX, tangentZ);
    RealisticWaterEvaluateWave(
        RealisticWaterMakeLegacyWave(_Wave2Params, _Wave2Steepness),
        worldXZ, time, offset, tangentX, tangentZ);
    RealisticWaterEvaluateWave(
        RealisticWaterMakeLegacyWave(_Wave3Params, _Wave3Steepness),
        worldXZ, time, offset, tangentX, tangentZ);
    RealisticWaterEvaluateWave(
        RealisticWaterMakeLegacyWave(_Wave4Params, _Wave4Steepness),
        worldXZ, time, offset, tangentX, tangentZ);
}

// J == 1 on flat water and approaches 0 near a folding crest.
float RealisticWaterJacobian(float3 tangentX, float3 tangentZ)
{
    float jxx = tangentX.x;
    float jzz = tangentZ.z;
    float jxz = 0.5 * (tangentX.z + tangentZ.x);
    return jxx * jzz - jxz * jxz;
}

// Bounded near-shore post-process. It leaves deep water at scale 1, lifts a crest in the
// pre-break band, then flattens the wave before it can pass through the beach.
float RealisticWaterShoreWaveScale(
    float depth,
    float shoreWaveDepth,
    float shoalStrength)
{
    float normalizedDepth = saturate(depth / max(shoreWaveDepth, 0.001));
    float survival = smoothstep(0.05, 0.95, normalizedDepth);
    float shoalBand = 4.0 * normalizedDepth * (1.0 - normalizedDepth);
    return survival * (1.0 + max(shoalStrength, 0.0) * shoalBand);
}

void RealisticWaterApplyWaveScale(
    float waveScale,
    inout float3 offset,
    inout float3 tangentX,
    inout float3 tangentZ)
{
    offset *= waveScale;
    tangentX = lerp(float3(1.0, 0.0, 0.0), tangentX, waveScale);
    tangentZ = lerp(float3(0.0, 0.0, 1.0), tangentZ, waveScale);
}

// Breaking is strongest between deep swell and the final dry-beach flattening zone.
float RealisticWaterShoreBreakEnvelope(float depth, float shoreWaveDepth)
{
    float normalizedDepth = saturate(depth / max(shoreWaveDepth, 0.001));
    float shallowGate = smoothstep(0.08, 0.35, normalizedDepth);
    float deepGate = 1.0 - smoothstep(0.72, 1.0, normalizedDepth);
    return shallowGate * deepGate;
}

// Shared analytic source used by both the surface shader and temporal history compute.
float RealisticWaterCrestFoamSource(
    float3 offset,
    float3 tangentX,
    float3 tangentZ,
    float crestGain,
    float crestBias,
    float crestHeight,
    float crestHeightFalloff,
    float crestSlopeGain,
    float breakEnvelope,
    float breakStrength)
{
    float jacobian = RealisticWaterJacobian(tangentX, tangentZ);
    float foldFoam = saturate((1.0 - jacobian - crestBias) * crestGain);
    float3 macroNormal = normalize(cross(tangentZ, tangentX));
    float slope = length(macroNormal.xz) / max(macroNormal.y, 0.001);
    float heightTerm = saturate(
        (offset.y - crestHeight) / max(crestHeightFalloff, 0.001));
    float slopeTerm = saturate(slope * crestSlopeGain);
    float whitecap = max(foldFoam, heightTerm * slopeTerm);

    float breakingHeight = saturate(
        offset.y / max(crestHeight + crestHeightFalloff, 0.001));
    float breakingSlope = saturate(slope * max(crestSlopeGain * 0.55, 0.001));
    float breaker = breakingHeight * breakingSlope *
        breakEnvelope * max(breakStrength, 0.0);
    return saturate(max(whitecap, breaker));
}

#endif
