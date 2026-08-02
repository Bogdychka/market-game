# Shader Vision - capture jobs

An agent editing a shader cannot see the result. These presets close that loop: each one is a
capture job the Editor executes, producing a labelled contact sheet plus measured numbers under
`Artifacts/ShaderVision/<outputName>/`.

```bash
powershell -File .claude/tools/shader-vision.ps1 water-lab
```

- `water-lab` - six fixed poses in `WaterShaderLab` (deck, grazing angle, shoreline, close-up,
  reflection beacons, top-down). The default "what does the water look like now" run.
- `water-foam-sweep` - one pose, six values of `_FoamShoreWidth` side by side. The template for
  "which value is right" questions.
- `grass-lab` - four-angle turntable around `Authored Meadow`, two wind phases per angle.
- `powershell -File .claude/tools/shader-vision.ps1 -SceneView` - one 720p shot of whatever the Scene
  view currently frames, no job needed.

## A/B loop

Poses, sun and the shader clock are pinned, so two runs differ only where the change landed:

```bash
powershell -File .claude/tools/shader-vision.ps1 water-lab
# edit the shader, then:
powershell -File .claude/tools/shader-vision.ps1 water-lab -CompareRun water-lab
```

The second run diffs against the first and prints `mean / max / changed%` per pose, plus a
`diff_<pose>.png` heatmap. `changed 0.0%` means the edit did nothing - worth knowing before
spending a paragraph explaining why it looks better.

## Job fields

| Field | Meaning |
| --- | --- |
| `outputName` | Output folder under `Artifacts/ShaderVision/`. |
| `scene` | Opened if it is not already active. Refuses to open over unsaved changes. |
| `width`/`height`/`columns` | Per-cell capture size and contact sheet layout. |
| `freezeTime`/`time` | Pins `_Time`/`_SinTime`/`_CosTime`/`_TimeParameters` on the capture camera so waves and scrolls hold their phase. |
| `timeSamples`/`timeStep` | Extra shots per pose at `time + n * timeStep` - animation over stills. |
| `sun` | `apply` overrides the directional light to a fixed `yaw`/`pitch` (`intensity: -1` keeps it). |
| `views[]` | Named poses: `position`, then `lookAt` (preferred) or `euler`, plus `fov`/`near`/`far`. |
| `turntable` | Orbits `target` at `angles` yaw steps, framed from its renderer bounds. |
| `useSceneViewCamera` | Adds the current Scene view pose as a first view. |
| `material` | Default material asset path for `overrides` and `sweep`. |
| `overrides[]` | `property` + `type` (`float`/`color`/`vector`) applied before all shots, restored after. |
| `sweep` | Same pose per `values[]` entry (or `vectorValues[]` in flat RGBA groups). |
| `compareRun` | `outputName` of an earlier run to diff against. |

A sweep whose cells all measure identically is a finding, not a bug: the property is inert in that
scene. `_FoamShoreWidth` behaves that way in `WaterShaderLab` - the shader only reads it when
`_ShoreDepthAvailable` is 0, and the lab has a baked shore map, so `_ShoreBandWidth` is the live
control. Checking a parameter this way costs one run.

Every shot is measured: mean/min/max/percentile luminance, RGB means, contrast, a `detail`
number (neighbour-pixel energy - catches lost normal-map or foam detail), and the failure
tells - `nonFinitePct` for NaN pixels and `magentaPct` for Unity's error shader.

Max 24 shots per run; extra cells are dropped.
