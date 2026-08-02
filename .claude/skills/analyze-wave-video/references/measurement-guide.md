# Wave Video Measurement Guide

## Contents

1. Evidence classes
2. Clip selection
3. Camera and perspective
4. Measurements
5. Calibration
6. Failure modes

## Evidence classes

Keep four classes separate:

1. Observed: directly visible, such as plunging crests or foam streaks moving shoreward.
2. Measured: computed in image coordinates, such as 1.8 seconds per brightness cycle or 42 pixels between bands.
3. Inferred: a model-based interpretation, such as shoaling near shallower water.
4. Authored: a deliberate Unity choice, such as using a temporal foam buffer at half resolution.

Confidence is high only when several independent views agree: dense source and ROI timelines, mask pages, kymograph, spectrum, crest tracks, optical flow, and the original clip. A 12-frame contact sheet is only a navigation aid.

## Clip selection

Use an uninterrupted 10-30 second interval with at least four wave cycles. Avoid cuts, speed ramps, autofocus jumps, heavy compression, and exposure changes. A fixed camera with visible static background is best. For slow motion, supply the real-time playback factor separately; container timestamps describe playback, not necessarily capture time.

Crop the ROI to water. Exclude the horizon when measuring near-shore waves because distant high-contrast lines dominate periodic analysis. Exclude bright sand when measuring foam.

## Camera and perspective

The analyzer estimates a per-pair 2D similarity transform from trackable features, accumulates it into a camera trajectory, smooths that trajectory over roughly 0.6 seconds, and warps each frame by the difference. Only jitter is removed; slow intentional motion is left alone. Every correction is therefore bounded, so a solve that latches onto travelling water can no longer drift the frame out of view and quietly destroy the measurement area.

`camera_motion` reports which regime the clip is in:

- `locked`: inter-frame motion is below a third of a pixel. No correction is applied and no warning is raised - this is the good case, not a failure.
- `handheld_or_moving`: normal jitter. Stabilization is used when the solve is strong, keeps at least 80 percent of the frame valid, and measurably reduces jitter (`jitter_reduction_ratio`).
- `solve_latched_onto_moving_water`: the trajectory walks steadily in one direction (`trajectory_drift_ratio` near 1) while jitter does not fall. The features are riding the waves, so the run falls back to raw frames.

This compensates translation, rotation, and small zoom, not parallax or rolling shutter. Inspect the raw/stabilized review and compare a `-NoStabilization` run when motion contradicts the clip.

Cut candidates are flagged only when a frame difference is a genuine outlier against the clip's own p90 and the feature solve collapses. A surge or foam flash that recurs every cycle is not a cut.

Pixel spacing varies with depth in a perspective image. A single meters-per-pixel value is valid only near one plane and distance. For a long shore transect, use multiple local ROIs or rectify the water plane from surveyed control points before treating pixel distance as metric distance.

## Measurements

- Temporal period: autocorrelation and aggregated FFT along the transect. The strongest accepted cycle describes repeating visible bands; secondary peaks may reveal multiple banks or harmonics.
- Spatial spacing: autocorrelation plus aggregated spatial FFT along the transect. Strong autocorrelation is preferred for the final spacing because short clips and finite transects quantize FFT bins.
- Method disagreement: relative difference between autocorrelation and spectral estimates. More than 20 percent is a conflict that requires visual review or a new ROI/transect.
- Crest tracks: Hough candidates on the kymograph provide apparent signed speed, visible lifetime, and directional agreement. They are evidence about moving patterns, not tracked fluid parcels.
- Transect bands: start, middle, and end spectra reveal changes in period, spacing, and breaking behavior along propagation.
- Apparent speed: three independent estimates, reconciled in `apparent_speed_cross_check`. Optical flow, crest-track slope, and accepted spacing divided by accepted period.
  - **Optical flow systematically under-reads the speed of a repeating crest pattern.** Coarse-to-fine matching aliases when the pattern repeats every wavelength, so a synthetic clip travelling at a known 20 px/s reads back as roughly 8 px/s. Use optical flow for *direction*, never as the primary speed.
  - The consensus is built only from the two geometric methods, and only from values that passed their own acceptance tests: a crest-track speed whose spread rejects it is reported but excluded, and spacing over period is used only when both the spacing and the period were accepted and neither is in method conflict.
  - When the two geometric methods disagree by more than 25 percent the consensus is `null`. That is the correct answer for that run; fix the transect or the ROI rather than picking a number.
- Direction coherence and spread: agreement and angular distribution among strong optical-flow vectors. Low coherence or broad spread means turbulence, crossing waves, camera contamination, or insufficient texture. `direction_confidence` and `speed_confidence` are reported separately because coherent directions say nothing about whether the magnitude is right.
- Foam coverage: low-saturation, high-value white-water proxy within the ROI.
- Foam persistence: median time a fixed image location remains classified as foam. This is Eulerian persistence and includes advection.
- Run-up proxy: change in the leading foam coordinate projected onto the optical-flow direction. It is not vertical tide height, and it is reported as `null` when that direction is itself unreliable (`runup_proxy_status`).
- Foam events: automatically selected low, rising, peak, falling, and high-coverage timestamps are review shortcuts, not a substitute for reading every mask page.
- Appearance: trough, midtone, and highlight colors; luminance percentiles; clipping; low-saturation highlight fraction; exposure drift; and detail energy. Treat auto exposure, white balance, glare, and tonemapping as part of the reference look, not measured water optics.
- Crest orientation: line-angle distribution estimates how parallel or fragmented bright crest structures are in image space.

## Calibration

Metric wavelength and speed require a known length on the analyzed plane, a rectified plane, or camera calibration plus geometry. Wave height also requires a vertical reference and suitable view. Water depth cannot be recovered reliably from appearance alone.

When calibrated, cross-check deep-water plausibility instead of forcing it: deep-water gravity waves roughly follow `L = g T^2 / (2 pi)`, while shallow-water phase speed roughly follows `sqrt(g h)`. Breaking surf, finite depth, currents, and perspective violate the simplest assumptions. Use these equations as diagnostics, not as automatic shader parameters.

## Failure modes

- Sun glitter is classified as foam: inspect all mask pages, tighten the ROI, analyze overcast frames, or reject foam mapping. Persistent low-saturation highlights and exposure clipping raise the automatic glare warning.
- Stabilization follows crests: disable stabilization or include static coast/background in the frame while keeping the measurement ROI on water.
- Kymograph has vertical noise with no diagonal bands: wrong transect, irregular chop, or insufficient duration.
- One dominant period hides two wave systems: inspect secondary spectral peaks and direction spread, then split ROIs or time windows before modeling two directional banks.
- Compression blocks dominate motion: use the original file and increase analysis width modestly.
- Drone motion or parallax remains after stabilization: use a stabilized source or camera solve before physical measurement.
