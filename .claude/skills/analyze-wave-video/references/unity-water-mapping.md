# Unity Water Mapping

## Contents

1. Observation-to-module map
2. Near-shore architecture
3. Parameter translation
4. Validation
5. Market Game adapter

## Observation-to-module map

| Video evidence | Unity representation | Main control |
| --- | --- | --- |
| Long coherent swell | Directional Gerstner or spectral displacement bank | direction, period, wavelength, steepness |
| Two strong periods with distinct direction evidence | Multiple directional wave banks | period and direction per accepted mode |
| Crest compression near shore | Bathymetry or shore-distance driven shoaling | depth band, wavelength scale, amplitude scale |
| Period or spacing changes along the transect | Shoaling or authored shore transition | depth ramp, wavelength scale, speed scale |
| Curling or spilling breaker | Crest sharpening plus breaking mask; geometry or particles for true overturn | breaker threshold, crest bias, lip amount |
| Whitecaps that appear on steep crests | Curvature or Jacobian foam source | threshold, gain |
| Foam that remains and drifts | Temporal foam state or flow-map advection | decay seconds, advection velocity, diffusion |
| Short crest tracks followed by persistent foam | Breaking source plus temporal foam state | break threshold, source duration, decay |
| Shore wash and retreat | Signed shore distance plus directional run-up phase | band width, run-up distance, retreat time |
| Small capillary detail | Two or more distance-faded normal layers | scale, speed, fade distance |
| Green-blue depth change | Beer-Lambert absorption with scene depth | absorption coefficients, optical depth |
| Distorted seabed | Refraction from surface normal and thickness | strength, depth fade |
| Sky and bright glints | Planar/probe/SSR reflection plus Fresnel | roughness, Fresnel, reflection source |

## Near-shore architecture

Use separate scales and state:

1. Geometry displacement for low-frequency swell.
2. A shore or bathymetry field for shoaling, alignment, and breaking onset.
3. Analytic crest foam as a source, not the entire foam result.
4. A persistent foam texture updated over time for lingering streaks and backwash.
5. Scene-depth intersection foam for contact with terrain and props.
6. Distance-faded micro normals for close detail without far-field shimmer.
7. Depth-aware absorption, refraction, reflection, and optional caustics.

A surface shader alone cannot conserve or advect foam history. If the reference shows foam surviving after a crest collapses, allocate explicit temporal state. A half- or quarter-resolution render texture is usually sufficient and can be updated below full frame rate.

## Parameter translation

- `period_s`: use directly as angular frequency `omega = 2 pi / period` after verifying playback speed.
- `temporal_peaks`: use only strong non-harmonic modes that survive ROI and time-window checks; do not instantiate every FFT peak as a wave.
- `spacing_px`: keep as an image-space comparison target until calibrated.
- `transect_bands`: drive a bathymetric or authored near-shore transition only when the change is repeatable and not just perspective.
- `crest_tracks`: compare track speed with wavelength/period phase speed; large disagreement lowers confidence rather than becoming another speed control.
- `wavelength_m`: set the dominant displacement-bank wavelength only when metric calibration is valid.
- `phase_speed_mps`: use to cross-check `wavelength / period`; do not independently force inconsistent values.
- `direction_image`: convert through the water-plane camera transform, not by copying screen X/Y into world X/Z.
- `foam_persistence_s`: initialize temporal foam decay near `exp(-dt / persistence)`, then tune with an A/B sweep.
- `foam_coverage`: compare rendered masks at matched camera/time; do not turn it into an unrestricted global multiplier.
- palette colors: seed absorption/scattering and foam tint, then validate under the project's lighting and tonemapping.

Amplitude is not derivable from uncalibrated monocular video. Choose steepness from visible crest shape and constrain it below self-intersection for the chosen wave model. Use geometry/particles when the reference requires an overturning lip; a height field cannot represent a multi-valued surface.

## Validation

Match camera pose, field of view, sun direction, exposure, and wave phase before comparing. Validate in this order:

1. Silhouette and crest travel.
2. Break location and timing.
3. Foam birth, advection, and decay.
4. Depth color and refraction.
5. Reflection and micro detail.

Use fixed captures and parameter sweeps. Track period, crest spacing, foam occupancy, clipped pixels, temporal stability, and frame cost. Visual similarity without matching motion is not a successful video match.

## Market Game adapter

Reuse `RealisticWater.shader`, `RealisticWaterWaves.hlsl`, `WaveProfile`, `WaveProfileBinder`, and `Market.World.WaveSampler`. Keep GPU wave math and CPU height sampling synchronized. The existing project rejects global opaque-texture cost for one water effect and requires front-face-only transparent water without motion vectors or probes.

Use Shader Vision jobs under `.claude/shader-vision/`. Start with `water-lab`, add a reference-specific job when the camera differs, and compare against the previous run. Inspect the generated `sheet.png`, `report.json`, and diff images. Run the project health performance checks after an outdoor water change.
