---
name: analyze-wave-video
description: Analyze shore, ocean, surf, wake, and water-reference videos frame by frame, measure repeatable visual motion and foam evidence, and translate the evidence into a technically honest Unity water-shader implementation and Shader Vision validation plan. Use for MP4, MOV, AVI, MKV, or image-sequence references when Codex or Claude must understand wave period, apparent crest direction and speed, wavelength in pixels or calibrated units, foam persistence, shoreline run-up, color, breaking behavior, camera motion, or which shader and simulation modules are required to reproduce the reference.
---

# Analyze Wave Video

Turn a water-reference video into measured evidence, a Unity shader brief, and an A/B validation loop. Treat image motion as apparent pattern motion unless physical calibration proves otherwise.

## Workflow

1. Locate the video and choose a representative 10-30 second interval. Preserve the source file.
2. Run the analyzer from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .claude/skills/analyze-wave-video/scripts/analyze-wave-video.ps1 -VideoPath "C:\path\shore.mp4"
```

3. Read `report.md` and `unity_shader_brief.json`. Inspect every `source_timeline_*.jpg` and `roi_timeline_*.jpg` page before interpreting metrics. The 12-frame `contact_sheet.jpg` is only a summary and is not enough to validate a surf event.
4. Inspect every `foam_mask_review_*.jpg` page before accepting foam coverage, persistence, or run-up. Reject the foam mapping when sun glitter, clipped highlights, bright sand, or static glare dominate the red mask.
5. Read the "Apparent crest speed" table before quoting any speed. Three independent methods are reported and reconciled; a `null` consensus means this run did not resolve a crest speed and none should be carried into the shader.
6. Cross-check the temporal and spatial spectra, autocorrelation values, three transect bands, crest-track overlay, motion direction rose, and camera timeline. Treat a method conflict as unresolved until the source frames explain it.
7. If the first pass selected the wrong water region, rerun with `-Roi "x,y,w,h"`. Coordinates are in the resized analysis image; values from 0 to 1 are normalized fractions.
8. If the propagation axis is wrong, rerun with `-Transect "x1,y1,x2,y2"`. Prefer a line parallel to crest travel and crossing several crests.
9. Add `-MetersPerPixel` only when the video contains a defensible scale at the analyzed water plane. Otherwise keep all spatial measurements in pixels and mark world amplitude, wavelength, and speed unresolved.
10. Read [measurement-guide.md](references/measurement-guide.md) before interpreting a difficult handheld, perspective-heavy, slow-motion, or edited clip.
11. Read [unity-water-mapping.md](references/unity-water-mapping.md) before changing a Unity shader or simulation.

## Validating the analyzer itself

After changing anything under `scripts/`, run the self-test. It builds synthetic clips with
exact known spacing, period, speed, camera behavior, and foam duty cycle, then asserts the
analyzer recovers them:

```powershell
powershell -ExecutionPolicy Bypass -File .claude/skills/analyze-wave-video/scripts/analyze-wave-video.ps1 -SelfTest
```

It takes about 20 seconds and prints one line per check. Add `-OutputDirectory` to keep the
generated clips and artifacts for inspection.

## Useful options

```powershell
# Analyze a stable 18 second segment and use a water-only ROI.
powershell -ExecutionPolicy Bypass -File .claude/skills/analyze-wave-video/scripts/analyze-wave-video.ps1 `
  -VideoPath "C:\path\shore.mp4" -Start 12 -Duration 18 -SampleFps 10 `
  -ReviewFrames 96 -Roi "0.05,0.35,0.90,0.60"

# Convert pixel measurements to meters only after calibration.
powershell -ExecutionPolicy Bypass -File .claude/skills/analyze-wave-video/scripts/analyze-wave-video.ps1 `
  -VideoPath "C:\path\shore.mp4" -MetersPerPixel 0.018
```

The default review is 72 evenly distributed frames, paginated at 24 frames per sheet. Raise `-ReviewFrames` to 96-144 for long, irregular, or event-heavy clips. This affects human-review sheets, not the denser measurement sampling. Use `-NoStabilization` for a locked camera when feature stabilization follows the waves instead of the background. Use `-OutputDirectory` to compare multiple ROIs or time windows without overwriting an earlier run.

## Interpretation guardrails

- Report optical flow as apparent image or crest motion, not fluid-particle velocity.
- **Never quote the optical-flow speed as the crest speed.** It aliases on repeating crests and reads several times too low; a synthetic clip travelling at a known 20 px/s comes back as roughly 8 px/s. Use `apparent_speed_cross_check.consensus_px_s`, and optical flow only for direction. `direction_confidence` and `speed_confidence` are separate fields for this reason.
- Do not infer wave height, water depth, camera focal length, or metric wavelength from a monocular uncalibrated clip.
- Stabilization removes jitter by smoothing the solved camera trajectory, so corrections stay bounded. It is rejected when the solve is weak, when it crops too much of the frame, or when it fails to reduce measured jitter. `camera_motion: locked` means a fixed camera that needs no correction, which is not a failure. Still inspect `stabilization_review.jpg`.
- Foam thresholding is a white-water proxy. Sun glitter, exposure clipping, snow, and bright sand can create false positives.
- FFT peaks are candidate wave modes, not automatically independent physical waves. Compare their power fraction, prominence, autocorrelation agreement, crest tracks, and source frames; weak peaks are often harmonics or compression structure.
- Start, middle, and end transect bands expose shoaling or perspective changes, but a short band may not contain enough wavelengths for reliable spatial FFT.
- A single shader formula rarely reproduces surf. Persistent foam generally needs a temporal render texture, simulation state, or authored flow/shore maps.
- Separate observations, measurements, inferences, and artistic choices in the final recommendation.

## Unity implementation loop

1. Map each accepted observation to one module: displacement, crest shaping, bathymetric shoaling, breaking, persistent foam, shore intersection, micro-normal, absorption/refraction, reflection, or caustics.
2. Reuse the project's existing water shader and control assets. Do not add a competing water stack without evidence that the current one cannot represent the required behavior.
3. Preserve one source of wave math between GPU and CPU sampling. In Market Game, edit `RealisticWaterWaves.hlsl` and `Market.World.WaveSampler` together, and keep authored banks in `WaveProfile`.
4. Convert the reference camera into fixed Shader Vision poses. Sweep uncertain parameters instead of repeatedly editing one value.
5. Capture a baseline, implement one module, then run Shader Vision with `-CompareRun`. Inspect `sheet.png`, diff images, and numeric failures before claiming a visual improvement.

## Outputs

The analyzer writes under `Artifacts/VideoAnalysis/<video-name>/` by default:

- `analysis.json`: source metadata, camera stability, motion distribution, the apparent-speed cross-check, multi-peak spectra, crest tracks, transect bands, foam events, run-up proxy, exposure, detail, and palette.
- `unity_shader_brief.json`: measured, inferred, and unresolved shader inputs with confidence and provenance.
- `report.md`: compact human-readable interpretation and next actions.
- `source_timeline_*.jpg`: dense, paginated source review; inspect every page.
- `roi_timeline_*.jpg`: the same timestamps cropped to the measurement ROI.
- `foam_mask_review_*.jpg`: raw/mask pairs for every review timestamp; red marks white-water candidates.
- `contact_sheet.jpg`: 12-frame summary only.
- `stabilization_review.jpg`: raw-versus-stabilized pairs and transform acceptance evidence.
- `motion_overlay.jpg`: average residual optical-flow field after optional stabilization.
- `motion_timeline.png` and `motion_direction_rose.png`: speed/coherence over time and propagation spread.
- `kymograph.png`: time-versus-distance evidence along the selected propagation transect.
- `kymograph_tracks.png`: candidate crest tracks and their apparent slopes.
- `band_kymographs.png`: start, middle, and end behavior along the transect.
- `temporal_spectrum.png` and `spatial_spectrum.png`: multi-mode periodic evidence.
- `foam_occupancy.png`: how often each ROI pixel was classified as white water.
- `foam_timeline.png` and `foam_event_sheet.jpg`: coverage/front evolution and representative birth, peak, and retreat events.
- `camera_timeline.png` and `crest_orientation.png`: camera/exposure/cut diagnostics and crest-line orientation distribution.

Keep these artifacts out of source control; retain the job settings and final shader changes.
