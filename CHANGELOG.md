# Changelog

All notable changes to the Market Game project. Format follows
[Keep a Changelog](https://keepachangelog.com/); versioning follows
[Semantic Versioning](https://semver.org/) - each released version is tagged in git as `vX.Y.Z`.

This file doubles as the **worklog**: the `[Unreleased]` section is where in-flight work is
recorded before it is verified via Unity MCP, versioned, tagged, and pushed. Old entries keep
their historical agent attributions (Claude / Codex / user); new entries don't need one.

> **Agents: read only the head of this file** (`[Unreleased]` + the latest release, ~40 lines) -
> never the full history. When the file exceeds ~30 KB, move entries older than the last 5 releases
> to `CHANGELOG.archive.md`.

## [Unreleased]

### Added
- **Wave profile assets with a procedural wave editor** - the water's Gerstner bank is now an
  asset, not four hardcoded shader properties. `WaveProfile` (`Market/Water/Wave Profile`) holds up
  to 8 layers (wave length, amplitude, steepness, directional or circular travel, direction/origin,
  speed), three multipliers with an **Apply** that bakes them into the layers, per-index curves for
  length/amplitude/steepness, and a Steepness Clamping fold limit. `WaveProfileBinder` on the water
  object uploads the resolved bank to the shaders; with no profile bound the shaders fall back to
  the legacy four wave properties, so nothing that already exists changes behaviour.
- **Procedural editor** (`Market/Debug/Water/Wave Creation Wizard`, also reachable from the profile
  inspector): seed, layer count, min/max wave length, amplitude-by-length and steepness-by-length
  curves, min/max amplitude and steepness, base direction and angle variation, wave length jitter,
  and travel mode. Generation is a seeded integer hash, not `UnityEngine.Random`, so a bank is
  reproducible from its settings on any machine; the settings live in the asset next to the layers
  they produced.
- **Preset profiles** built by `Market/Debug/Water/Create Preset Wave Profiles`:
  `WP_OceanSwell` (long swell + chop, wide fan), `WP_LakeChop` (short, low, aligned), `WP_PondRings`
  (circular rings from an origin). Re-running the menu item regenerates them in place.
- **`WaveSampler`** - the wave bank evaluated in C#, formula for formula against the HLSL, including
  the fixed-point inverse of the horizontal displacement. `WaveProfileBinder.SampleHeight/SampleNormal`
  read the same surface the GPU draws, which is what buoyancy, splashes or a boat need.
  Covered by `WaveProfileTests` (reproducibility, multiplier/curve resolution, the inverse solve
  landing on the displaced surface, fold-safe clamping, circular radiation).

### Changed
- **The Gerstner bank now lives in one file** (`RealisticWaterWaves.hlsl`), included by
  `RealisticWater.shader`, `RealisticWaterUnderwaterSurface.shader` and
  `RealisticWaterFoamUpdate.compute`. It had been copy-pasted into all three, which is how the
  underwater copy drifted: it advanced the phase with `+ time`, so seen from below the crests
  travelled into the wind while the surface above them travelled with it. Fixed by unification.
  The whitecap kernel now derives its Jacobian from the shared evaluator instead of a
  derivative-only twin.
- `RealisticWaterUnderwaterSurface.EvaluateSurfaceHeight` and the weather controller read the wave
  bank instead of their own copies. Weather scales a bound profile through
  `WaveProfileBinder.BankScale` (a runtime scale, never written to the asset), so calm-to-storm
  still moves the sea rather than only the foam and micro normals.
- `Market/Debug/Water/Inspect Realistic Water Shader Errors` also inspects the underwater surface
  shader and the foam compute kernel - compute compile errors reach the console even less than
  shader errors do.
- `WaterShaderLab`: the `Water` object carries a `WaveProfileBinder` bound to `WP_OceanSwell`, and
  the weather controller points at it.

## [1.9.1] - 2026-08-02

### Changed
- **RealisticWater is no longer see-through at depth.** `_AbsorptionCoefficients` was
  `(0.65, 0.32, 0.18)`, which over `WaterShaderLab`'s seabed (shelves at -0.6 / -2.5 / -6 / -14 m)
  left green transmittance at 45% on the 2.5 m shelf and 15% at 6 m - the lit seabed and its
  caustics read straight through as a bright green floor under glass, worst in any downward-looking
  pose. Retuned to `(1.6, 0.62, 0.34)`: ~0.5 m of shore water stays clear, the 2.5 m shelf drops to
  a tinted ghost, and by 6 m the bottom is gone, so transparency now falls off with distance from
  shore instead of switching off at a terrace edge. Red is pulled 2.6x harder than blue (was 3.6:1
  overall but far too weak absolutely) so the body colour stays blue-green rather than grey. The
  shader default moves with it - it was `(0.22, 0.10, 0.04)`, clear enough to be useless as a
  starting point. Caustics dim automatically, since `causticVisibility` is the luminance of the same
  transmittance.
  Verify: `shader-vision.ps1 water-lab` - topdown mean luminance 0.086 -> 0.061 with 60% of pixels
  changed; grazing/deck unchanged in character (they were already opaque).

## [1.9.0] - 2026-08-02

### Added
- **RealisticWater crest subsurface scattering** (`_SubsurfaceColor`, `_SubsurfaceStrength`,
  `_SubsurfacePower`, `_SubsurfaceHeight`). Sunlight transmitted through the thin water at the top
  of a wave, strongest looking into the sun, faded by wave height so troughs stay dark and scaled
  by `(1 - Fresnel)` so it vanishes at grazing angles where the surface becomes a mirror. This is
  what gives real swell its turquoise glow on the sun side; without it the surface only ever
  reflects and deep water reads as flat dark paint no matter how the reflection is tuned.

### Fixed
- **RealisticWater: the whitecap system never fired.** Crest foam was driven solely by the Gerstner
  horizontal Jacobian, which only departs from 1 near a *folding* wave. At `WaterShaderLab`'s wave
  settings it never does, so `_FoamCrestBias 0.12` (requiring `J < 0.88`) meant zero whitecaps
  anywhere - the lab renders a sign reading "WAVES + WHITECAPS" over water that had none. Verified
  by rendering the driver to screen: the Jacobian channel was black across every pose, and sweeping
  `_FoamCrestBias` all the way to 0 moved mean luminance by 0.006. Added a second driver -
  displacement above still water (`_FoamCrestHeight` / `_FoamCrestHeightFalloff`) gated by surface
  gradient (`_FoamCrestSlopeGain`), combined with the Jacobian via `max` so a genuinely breaking
  wave still saturates. Slope uses `tan(tilt)`, not `1 - n.y`: on long low swells the macro normal
  tilts a few degrees and `1 - n.y` peaks near 0.05, leaving any sane gain at nil.
  Verify: `shader-vision.ps1 water-lab` - crests carry foam; `detail` rises at all six poses.

### Changed
- **RealisticWater foam reads as foam instead of airbrushed haze.** The coverage mask was broken up
  by a single 0.3-tiling noise at +/-20% (`0.6 + 0.4 * n`), whose features are ~3 m across, so a
  foam patch was one near-uniform pale blob with a soft gradient rim. Now: `FoamBreakupNoise`
  returns two octaves (clump placement + bubble structure), and `FoamDissolve` thresholds the mask
  against that noise so the patch interior stays solid and only its rim dissolves into speckle.
  The mask is treated as a coverage *fraction* (threshold at `1 - mask`), not as a brightness - an
  earlier revision centred the threshold at 0.5 and silently erased every mask that never reached
  0.5, which is all of them. `_FoamBreakup` (0 = old soft mask, 1 = full speckle) defaults to 0.7;
  contact foam gets 0.6x that so the thin ring around objects survives. The dissolve now runs
  *after* the foam-history blend, so Play Mode's temporal path gets the same breakup rather than
  coming back smooth. Foam colour is modulated by the bubble octave so a patch is not one flat tone.
  Verify: `water-lab` topdown foam has torn, irregular edges instead of soft blobs; `detail` 0.0110
  -> 0.0122 there, up at every pose, with mean luminance held within 0.007.
- **Foam no longer appears on water that has no foam mask.** `FoamDissolve` thresholded the noise
  with a window *centred* on `1 - mask`, so its upper edge sat at `1 - mask + width`. At `mask = 0`
  that edge is below 1, and every peak of the breakup noise cleared it - spraying pale patches
  across open water in all four foam channels at once (crest, shoreline, contact, waterline),
  identically, because the noise field is shared. No strength property could switch them off:
  `contactAmount` has no strength multiplier at all, so zeroing every exposed foam control still
  left the patches. Found by rendering each foam channel to screen and seeing the same speckle in
  all of them, which pointed at the one function they share. The window is now one-sided, anchored
  AT `1 - mask`, so `mask = 0` yields `[1, 1 + width]` - unreachable by a saturated noise field -
  while coverage still tracks the mask above it.
  Verify: `water-lab` topdown - foam only as contact rings around the rocks and along the
  waterline; `black` 13.6% -> 18.3% as the false foam stops lifting dark pixels.
- **Whitecap and shoreline foam no longer print the breakup noise onto the water.** The crest
  threshold was low enough to hold the mask near 0.5 across roughly half the surface; a mask that
  flat makes the dissolve show its own threshold field, so the metre-wide clump octave appeared as
  big soft blobs scattered without regard to where the crests were. `_FoamCrestHeight` 0.2 -> 0.45
  with falloff 0.2 keeps whitecaps to the few percent of a moderate sea that actually breaks, and
  the dissolve's threshold field is reweighted towards the bubble octave (0.3 clump / 0.7 bubble)
  so its visible grain is bubble-scale. Shoreline foam now takes half the breakup the open-water
  crests do and `_ShoreBandWidth` drops 2.5 -> 1.2: it is a band hugging the waterline, and at full
  breakup a wide band stopped reading as a band and scattered into detached patches over open
  water. `T_ShoreDepth` re-baked (the previous bake predated seabed edits).
  Verify: `shader-vision.ps1 water-lab` - no large foam patches away from the waterline.
- **Shader Vision** - an Editor capture rig that gives an off-Editor agent something to look at
  when it works on a shader. A JSON job (`Artifacts/ShaderVision/job.json`, presets in
  `.claude/shader-vision/`) describes camera poses, a turntable around a named object, a sun
  override, material overrides, or a parameter sweep; the run writes per-shot PNGs, a labelled
  contact sheet, measured statistics and an optional pixel diff against an earlier run to
  `Artifacts/ShaderVision/<outputName>/` (git-ignored). Menu: `Market/Debug/Shader Vision/Run Job`
  and `.../Capture Scene View`. Driver: `.claude/tools/shader-vision.ps1 <preset> [-CompareRun x]`.
  Captures are repeatable because the shader clock is pinned: URP rewrites `_Time`/`_SinTime`/
  `_CosTime`/`_TimeParameters` inside the render graph (twice per camera, from
  `Time.realtimeSinceStartup` in Edit mode), so `ShaderVisionTimePass` re-injects the frozen values
  at three injection points on the capture camera only. Without it, two captures of the same
  unchanged scene differed by 5% of pixels on the animated grass; with it they are bit-identical,
  which is what makes a before/after diff mean anything.
  Every shot is measured - luminance mean/percentiles/contrast, RGB means, a neighbour-pixel
  `detail` figure, plus `nonFinitePct` (NaN output) and `magentaPct` (Unity's error shader) as
  explicit failure tells.
  *Verify:* `powershell -File .claude/tools/shader-vision.ps1 water-lab`, then read
  `Artifacts/ShaderVision/water-lab/sheet.png`. Running the same preset twice with
  `-CompareRun water-lab` must report `changed 0.0%`.

### Fixed
- MCP bridge no longer fails calls made while Unity enters Play mode. The drop is by design in the
  package (clients closed with code 4001, then the play-mode domain reload takes the server
  instance with it) and lasts ~4.8 s; the bridge works normally *during* Play mode and exiting has
  no gap at all. `unity-ws-call.mjs` now waits the window out (`UNITY_RECONNECT_WINDOW_MS`,
  default 30 s) instead of erroring, and only while the request is still unsent - a request that
  was already sent is reported as "may or may not have run" rather than silently re-executed, so
  `execute_menu_item` can never fire twice. `McpUnityAutoStart` retries until the socket actually
  listens instead of making one attempt after a fixed 2 s wait; the package's own restart hooks
  (`DidReloadScripts`, `afterAssemblyReload`) only cover compilation reloads, so without it the
  bridge could stay down for a whole play session.
  *Verify:* start Play mode, then immediately
  `node .claude/tools/unity-ws-call.mjs get_play_mode_status '{}'` - it blocks a few seconds and
  returns `Play mode` instead of `ECONNREFUSED`.

### Fixed
- **MCP bridge no longer drops when entering Play Mode.** Measured before: the Editor closed every
  client with code 4001, the play-mode domain reload took the server instance with it, and the
  bridge was unreachable for ~4.8 s - dominated by the reload, not by the restart delay. Now the
  project enters Play Mode without a domain reload, so the server object and its socket survive:
  play + status round trip is 713 ms with no disconnect.
  Three parts: `Market/Debug/MCP/Enable Fast Play Mode (no domain reload)` sets the Enter Play Mode
  Options (menu item rather than a hand-edited `EditorSettings.asset`, which the running Editor
  would overwrite); `McpUnityServer.OnPlayModeStateChanged` only closes clients when a reload is
  actually coming (local patch to the vendored package); `McpUnityAutoStart` retries until the
  socket listens instead of firing once after a fixed 2 s.
  **Consequence: statics survive between Play sessions.** `ServiceLocator`, `FileLogger`,
  `GameBootstrap` and `GrassTrample` now reset at `SubsystemRegistration`; any new static state
  must do the same.
  *Verify:* `Market/Debug/MCP/Log Play Mode Options` reports `domainReloadOnPlay=False`; two
  consecutive Play sessions from `Bootstrap.unity` produced 0 errors and 0 warnings, with
  `game.log` rewritten by the second run. `.../Restore Domain Reload On Play` reverts the setting.
- `unity-ws-call.mjs` waits out a bridge restart (`UNITY_RECONNECT_WINDOW_MS`, default 30 s)
  instead of failing on `ECONNREFUSED`, and only while the request is still unsent - a request sent
  before the socket dropped is reported as "may or may not have run" rather than silently retried,
  since re-running something like `execute_menu_item` is worse than an honest report.

### Changed
- GrassLab interaction and presentation polish (T21): grass now physically bends around the
  player/NPC body in a blend of radial push and travel direction instead of only turning its local
  wind. Engagement follows mover speed and releases after stopping, so clumps rise smoothly rather
  than staying permanently flattened. The same deformation runs in forward, shadow, depth and
  depth-normal passes. `GrassTrampleTests` covers movement direction and release.
- Grass color now has a second, low-frequency world-space variation layer, creating broad warm/cool
  meadow patches on top of per-clump randomness without material instances. GrassLab starts in a
  clean presentation view: the card row and 1.8 m scale post are preserved but hidden, and F6
  toggles both through `GrassLabPresentationToggle`. The field sign documents the key, while a
  slightly lower exposure/ambient balance restores Play Mode contrast.
  *Verify:* enter GrassLab Play Mode, walk through a dense patch, stop, and press F6. Nearby blades
  should yield then recover, and the reference diagnostics should toggle. EditMode tests pass
  85/85; Unity compiles with 0 warnings and GrassWind reports 0 shader messages.
- GrassLab received a complete meadow beauty pass (T20). The Player now starts with the prefab root
  at ground level instead of 1.2 m above it, so the camera reads grass from a real 1.7 m eye line.
  `GrassLabVisualUpgrade` (`Market/Debug/Grass Lab/Apply Visual Upgrade`) preserves hand-painted
  `GrassScatter` content while rebuilding its own deterministic visual root: 960 mixed single/cross
  card clumps, 260 GPU-instanced fine tufts built from one reusable 14-blade curved mesh, four
  wildflower patches, a collider-free tree/bush/rock habitat frame, a physical field sign, bounded
  terrain settings, neutral daylight, depth fog and a dedicated `GrassLabPostFX` profile.
- `GrassWind.shader` now breaks up the uniform neon field with stable per-clump warm/cool variation,
  controlled saturation, root contact darkening and soft wrapped foliage light. Wind gained a
  coherent travelling gust band plus a small stable phase offset per clump; cards and procedural
  blades share the same global `GrassWindController`, shadow, depth and depth-normal deformation.
  Grass card materials keep instancing, disable useless per-object motion vectors and expose the
  new look controls through `GrassCardBuilder`.
  *Verify:* open GrassLab, run Apply Visual Upgrade, enter Play Mode and walk forward. The field
  should show broad clumps, fine moving blades, flowers and a framed horizon at eye level. Unity
  compilation is 0 warnings, GrassWind reports 0 shader messages, and health is `ok` with 0 console
  errors/warnings and 0 dirty scenes.
- Water caustics are now traced instead of drawn. `RealisticWaterCausticBaker`
  (`Market/Debug/Water/Bake Caustic Flipbook`) builds a synthetic wind-wave surface from waves
  picked off the tile's integer lattice, refracts one downward sun photon per surface sample
  through it and accumulates where each photon lands on a flat seabed. The result is a real
  refraction caustic - a network of thin bright filaments around darker cells, the pattern the
  reference beach photo shows - rather than the painted cell texture that was used before.
  Output is `T_WaterCausticFlipbook.png`: 32 frames in a 4096x2048 atlas, seamless in space
  (lattice wave vectors) and in time (frequencies quantised to the loop), with per-channel
  dispersion in RGB and a wrap border per cell so tiling survives mip filtering. A stored 1.0
  equals 8x the average seabed irradiance, so the shaders receive a metered light field.
  *Verify:* re-run the bake, then look at the depth/caustics station in WaterShaderLab.
- `RealisticWaterProjectedCaustics.shader` and the surface-composite fallback in
  `RealisticWater.shader` were reworked around that field. Both cross-fade consecutive flipbook
  frames so the network boils in place instead of sliding, add only the light above the mean
  seabed irradiance, grow the cells with water depth, tint the light per channel by the sun's
  actual path length through the water (shallow reads warm-white, deep reads teal), and relax
  the sharpening once a pixel covers more than a filament - without that, distant caustics
  degrade into crawling dots. New tuning: `_CausticScale` (tile size in metres, replaces the
  two tiling factors), `_CausticDepthSpread`, `_CausticPedestal`, `_CausticContrast`,
  `_CausticSoften`, `_CausticAbsorption`, `_CausticFlow`, `_CausticWarp`.
- `_CausticSpeedA/B` and `SurfaceCausticSpeed` changed meaning from UV scroll rates to flipbook
  boil rate (loops per second) and drift speed; the calm-to-storm ladder in
  `RealisticWaterWeatherProfiles` was re-scaled to match. The ladder runs at 0.18/0.28/0.38/0.52
  loops per second - roughly 6 to 17 frames per second out of the 32-frame loop, slow enough to
  read as water rather than a flickering texture.

### Fixed
- `GrassWind.shader` ShadowCaster wrote raw clip positions - no `ApplyShadowBias`, no near-plane
  clamp, no normal in the vertex input. Every card shadow-mapped onto itself and the patch stippled
  with acne. It now applies the standard URP bias (including the punctual-light variant) and clamps
  to the near plane.
- The jelly squash scaled the wrong axis on grass cards. Cards are baked standing (height along
  object Y) while the older geometry tufts lie flat (height along Z), and the shared code scaled Y
  for both - so cards pumped up and down by +-15% instead of fattening sideways, and the two quads
  of an X-cross sheared against each other. The squash is now picked per mesh family off the same
  `_WINDMASK_UV` branch that already knows which axis is up.
- `GrassWind.shader` had no `DepthOnly` or `DepthNormals` pass, so grass was absent from
  `_CameraDepthTexture` and `_CameraNormalsTexture` - and `PC_Renderer` runs SSAO with the
  DepthNormals source, which therefore could not see a single blade. Both passes added, animated by
  the same wind so depth matches the visible silhouette. No `UniversalGBuffer` pass on purpose: this
  shader does its own toon/translucency/rim lighting, and a G-buffer pass would hand the pixels to
  URP's deferred PBR and discard all of it. Custom-lit materials belong in the forward-only pass.
- Card materials were built with `_NormalSoftness` 0.85, which bends the card normal so far toward
  world-up that every card ends up with the same normal. That collapsed the toon ramp to a single
  saturated band, zeroed the backlight term (`_Translucency` 1.1 had no effect at all) and turned
  the Fresnel rim into a flat ~0.2 wash over the whole field - the "washed out and flat" look.
  Now 0.4, with `_ToonBands` 3, `_RimStrength` 0.15 and a tip tint that is light green instead of
  near-white, which used to bleach the top of every card.

### Changed
- Wind is now one field for the whole scene instead of a per-material constant. All wind properties
  left `UnityPerMaterial`; `GrassWindController` (`Market/World`) pushes direction, sway, gusts and
  jelly into shader globals once per frame, and a material only says how hard it answers
  (`_WindResponse`). This is how wind is normally modelled - one global source per world, as with
  Unity's WindZone or Unreal's Wind Directional Source - and it means the grass can follow the same
  weather ladder the water already has. Materials had already drifted apart under the old scheme:
  `Grass_1.mat` swayed at 0.06 while every card swayed at 0.05.
  The shader falls back to a default breeze when a scene has no controller, so grass is never
  frozen solid. `UnityPerMaterial` and the wind functions moved into `GrassWindCommon.hlsl` so all
  four passes share one declaration - the SRP Batcher needs them identical, and four hand-copied
  blocks drift.
  *Verify:* open GrassLab, change Heading on the `Grass Wind` object, watch the whole field turn.
- The grass lab lights with Unity's procedural sky, not `M_SkyboxLab`. That material is tuned live
  by the skybox lab and was parked on a night blend, and grass colour judged under a coloured sky is
  judged wrong. `Market/Debug/Grass Lab/Reset Lighting To Daylight` re-applies it to an already-open
  lab without rebuilding, so painted clumps survive.

### Added
- `GrassLabSceneBuilder` (`Market/Debug/Build Grass Lab`) builds `Scenes/GrassLab.unity`: a 100 m
  terrain with a flat middle to paint on, rolling ground around it, one ~24 deg hillside to check
  Align To Slope and the random lean against, and a winding dirt path - grass reads very
  differently where it meets bare ground. Plus a reference row holding one of every built card
  (singles in front, X-crosses behind, each labelled with its source PNG), a post banded every
  30 cm up to 1.8 m so clump height can be judged against something of a known size, the project
  post-processing volume, and the Player prefab for walking the patch at eye level.
  *Verify:* run the menu item, then paint with the Grass Scatter Brush.
- Grass brush: six painted grass cards instead of three, and every clump now exists in two
  flavours. `Grass_6.1`..`Grass_9.1` joined `Grass_4.1`/`Grass_5.1` in the `GrassCardBuilder`
  variant table; `Grass_3.1` stays buildable but is off the brush palette because it is painted a
  full stop darker than the rest of the set. Each variant bakes a single-quad `*_Clump.prefab` and
  an X-cross `*_Clump_Cross.prefab` (two quads at 90 deg in one mesh, so a cross still costs one
  renderer) - the cross never goes edge-on invisible, the single quad is half the fill rate.
  *Verify:* `Market/Debug/Grass Card/2. Build Material + Clump Prefab` logs 7 variants; the
  `_Cross` meshes have 8 verts against the singles' 4.
- `GrassScatterBrush` randomises each painted clump: variant, yaw over a full turn (a flat card
  seen from behind is its own mirror, so this doubles the silhouettes for free), width and height
  jittered separately, and a random lean. New controls: Cross Chance (share of X-clumps,
  default 0.35), Width/Height Jitter, Max Lean, Sink Into Ground (scaled by height, so leaning
  cards do not hover). Shift+drag erases inside the brush disc.
  *Verify:* open `Market/Debug/Grass Scatter Brush`, Reload grass cards -> 6 single + 6 cross
  sources, enable painting and drag over a terrain.
- `RealisticWaterWeatherController` adds a weather-ready calm-to-storm ladder with four coordinated
  states: Calm, Breeze, Windy, and Storm. `SetWeather(...)` performs a smooth three-second blend on
  a runtime material instance while keeping Gerstner waves, wind spread, micro normals, refraction,
  roughness, temporal whitecaps, projected/surface caustics, and the underwater surface in sync.
  WaterShaderLab starts at Breeze and exposes bracket-key cycling with a live world-space label.
  Calm, Breeze, Windy, and both wide/close Storm views were visually checked from the same scene;
  EditMode tests passed 84/84 and Unity compiled with 0 warnings. The final health report had no
  compile or scene errors; its only console exception was the expected test-suite negative case.
- `WaterShaderLabSceneBuilder` now produces a deliberate walk-through showcase instead of an empty
  terrace test: an elevated observation deck and entry frame lead to labelled stations for shore
  shoaling/contact foam, depth absorption and projected caustics, striped refraction columns,
  emissive planar-reflection beacons, wave/whitecap gauges, and a lit deep-water gallery for the
  underwater surface transition. Neutral calibration tiles span four known depths, the contact
  rocks use deterministic irregular silhouettes, and the project post-processing profile plus
  bounded surface fog make regenerated lab scenes visually consistent.
  *Verify:* build `Market/Debug/Build Water Shader Lab`, Play from the observation deck, walk the
  stations, then use F4 and Left Ctrl to inspect the underwater gallery.
- `RealisticWater.shader` now reasons about the geometry it touches, driven by a baked shore map
  (`Market/Debug/Water/Bake Shore Depth Map`, `ShoreDepthBaker`). The map is a top-down
  `T_ShoreDepth.asset` over the water bounds: red is the water column depth, green is the
  horizontal distance to the waterline. It is baked with downward raycasts rather than a depth
  render - no render-pass plumbing, and it reads the colliders objects actually stand on. Same
  world-rect convention as the foam history. Re-bake whenever the seabed moves; a stale map
  silently misplaces the shoreline instead of failing.
  - **Wave shoaling.** `Vert` scales the whole Gerstner result - offset *and* the derivatives
    behind the macro normal - by depth. Previously the full offset was applied everywhere, so
    crests lifted the surface above the beach and troughs sank it through the seabed; that is the
    water visibly passing through the terrain. Scaling the offset alone would have left a lit,
    sloped surface on geometrically flat water, hence lerping the tangents too.
  - **Contact softening.** The pass composites opaquely, so the mesh used to simply stop where it
    met a rock. The final colour now dissolves into the already-sampled scene colour as the view-ray
    distance to the geometry behind it goes to zero.
  - **Shoreline in metres of beach, not metres of depth.** The band is read straight from the
    distance field. The first attempt derived it at runtime as `depth / slope`, which is only valid
    on a monotonic slope - on the lab's terraced seabed every vertical riser has a near-infinite
    slope and got its own false shoreline. Plus a narrow high-contrast waterline on top.
  - **Object reaction.** Contact foam and ripples come from the view-ray distance to the scene, not
    from the vertical column, so they wrap anything sticking out of the water including vertical
    faces where the column depth is discontinuous. The ripple axis is the world-space gradient of
    that distance, reconstructed from screen derivatives, because the obstacle is only known
    through the depth buffer.
  An unbaked material takes the old code paths unchanged.
- `WaterShaderLab` gains a sloped beach over the Beach/Shallows step. The terraces are good for
  reading effects at known depths but their risers are vertical, so the waterline sat behind a lip
  where no camera could see it and a surf band had nowhere to sit.

### Fixed
- `ShoreDepthBaker` now uses Unity 6.5's current parameterless `FindObjectsByType` overload, removing
  the two obsolete-API compile warnings without changing renderer selection.
- The new shore band was invisible: `RealisticWaterTemporalFoam` runs in edit mode, so
  `_FoamHistoryAvailable` was 1 and the history's own shoreline channel overwrote it. With a baked
  map the shore term now bypasses the history, which keeps owning the crest channel.
- `ShoreDepthBaker` picked the caustic projector instead of the water: it matched the shader name
  by substring, and `RealisticWaterProjectedCaustics` contains `RealisticWater`. Exact match now.

### Removed
- WaterWorks evaluated and rejected; `Scenes/WaterWorksLab.unity`, its generated materials in
  `Art/WaterWorksLab/` and `Settings/WaterWorksLab_Renderer.asset` are deleted and the extra
  renderer is unregistered from `PC_RPAsset`, so the pipeline asset is back to a single renderer.
  `RealisticWater.shader` beats it on every overlapping feature - 4 Gerstner waves vs one vertex
  displacement, per-channel absorption plus in-scattering vs a flat colour, thickness-scaled
  edge-faded refraction vs a flat screen-UV offset, planar reflection vs screen-space (which loses
  everything past the screen edge), and Jacobian whitecaps with a temporal history vs a depth edge.
  Its shimmer at range is the direct result of having no distance fade on its micro normals, which
  `RealisticWater` already does via `_DetailFadeStart/End` - a useful confirmation that the
  existing design is right.
  Worth keeping in mind if underwater gameplay ever appears: the package's `rayBoxDst` slab
  intersection plus clamping the march to scene depth, and the trick of sliding the volume box with
  the camera in XZ so a small box reads as endless. Not the ray march itself - up to 250 steps per
  pixel at a 0.5 step. Games with underwater gameplay (Subnautica, ABZU) pay for volumetrics; games
  that only glance below the surface use analytic fog, which is what `UnderwaterFogController`
  already does here.
  The builder and the F6 panel are kept so the evaluation can be reproduced in one menu click; the
  imported package itself is left in place with its Unity 6 port.

### Fixed
- Imported "WaterWorks" (GapperGames) did not compile on Unity 6 / URP 17 and left `main` red.
  `Water_Volume.cs` used the URP 12 pass API (`RenderTargetHandle`, `renderer.cameraColorTarget`,
  `Configure`/`Execute`) - ported to Render Graph (`RecordRenderGraph` + `RenderGraphUtils
  .AddBlitPass`, `requiresIntermediateTexture`, `ConfigureInput(Depth)`), and it now skips
  reflection/preview cameras and the backbuffer instead of blitting blindly.
  `Volumetric_Water.shader` imitated a Shader Graph unlit pass and included URP's internal
  `Varyings.hlsl` / `UnlitPass.hlsl`, whose `BuildVaryings` signature changed in URP 17 - rewritten
  as a plain full-screen blit pass. The ray march in `Water_Volume.hlsl` is unchanged apart from
  its inputs: the colour source is `_BlitTexture` (was `_MainTex`), depth is `SampleSceneDepth`
  (was `SHADERGRAPH_SAMPLE_SCENE_DEPTH`), and the box centre is a local copy instead of a write to
  a cbuffer uniform. `Water_Settings.cs` no longer runs a `Resources.Load` plus a shared-material
  write every editor frame. Added `Assets/WaterWorks/WaterWorks.asmdef` - without it the package
  lives in `Assembly-CSharp`, which an asmdef like `Market.Editor` can never reference.
  *Verify:* `recompile_scripts` -> `get_health_report` is `ok`.

### Fixed
- The lab rendered as a flat teal fog with no sky: `Water_Settings` wrote the volume box position
  into the package material in `Resources`, while the renderer feature was pointed at the project
  copy. The copy kept the author's `pos.y = -245` against `bounds.y = 500`, which puts the top of
  the box at `+5` - above the camera - so the underwater pass fogged the entire above-water view.
  `Water_Settings` now takes an explicit volume material (`SetVolumeMaterial`), the builder hands
  it the same copy the feature uses and records the prefab-instance override, and the builder also
  writes `pos` itself instead of waiting for the `[ExecuteAlways]` tick that may not run before the
  scene is saved.
- `SceneCameraCapture` rendered to an LDR `ARGB32` target while the project renders HDR and
  tonemaps in post, so any bright scene clipped to white - it reported the WaterWorks demo scene as
  a blown-out blank. Now `DefaultHDR`, and without MSAA (the PC renderer is Deferred).

### Added
- `WaterWorksLabSceneBuilder.UseAuthorDemoLook` reproduces the package's own demo conditions so the
  asset can be judged the way its store page shows it: the package ocean plane at demo scale (no
  horizon edge), untouched author material values, the demo sun (intensity 5 - four times the
  project sun, and most of what makes this water sparkle rather than shimmer), the demo post
  profile (ACES + bloom with lens dirt), and a spawn standing in the shallows rather than up on the
  beach, because this water only reads from about a metre above the surface. Set the flag to false
  for the project-lit variant on the dense water grid, which is the only way vertex displacement is
  visible at all - the package plane is an 11x11 quad scaled to 10000 units, which is why the
  package ships with `_Displacement_Amount` at 0.
- WaterWorks lab: `Market/Debug/Water/Build WaterWorks Lab` (`WaterWorksLabSceneBuilder`) builds
  `Assets/_Project/Scenes/WaterWorksLab.unity` - one pool whose seabed steps from a dry beach down
  to a -34 trench, plus four stations that isolate one shader feature each: A depth fade (submerged
  staircase at known depths), B refraction (striped poles crossing the waterline, so the stripe
  offset *is* the refraction), C screen-space reflection (pillars and emissive beacons), D waves
  (fixed-height gauges marching away from shore, showing where `_MaxWaveDist` kills the amplitude).
  Water is the existing 200x200 `RealisticWaterGrid` mesh, not the package's 10x10 ocean quad -
  vertex displacement is invisible on the shipped plane, which is why the package ships with
  `_Displacement_Amount` at 0.
  Package materials are copied into `Art/WaterWorksLab/` and tuned there; the imported originals
  stay untouched.
- `WaterWorksLabController` (F6, in-game): one on/off button per shader feature - SSR, shoreline
  foam, wave displacement, caustics, refraction - plus the underwater volume and a water-material
  switch, so each feature can be A/B compared without leaving the view.
- The underwater volumetric pass is a full-screen blit every frame, so it runs on its own
  `Assets/Settings/WaterWorksLab_Renderer.asset` (a copy of `PC_Renderer` with the feature and
  `Intermediate Texture = Always`), registered as renderer index 1 on `PC_RPAsset`; only the lab
  camera opts into it. Market stays on renderer 0 and pays nothing - per the `AGENTS.md` rule
  against adding a global full-screen effect without a measurement.
- `Market/Debug/Inspect Selected Shader Errors`: the existing GrassWind-only dump now also works on
  whatever shader or material is selected. Shader compiler messages don't reach the MCP console
  bridge on their own, which is what hid the WaterWorks shader break at import time.
  *Verify:* run the builder, then Play in `WaterWorksLab` - console stays clean, F6 opens the panel.

### Changed
- Realistic-water caustics now use the downloaded project-owned `WaterCaustics.png` lookup instead
  of repeating sine lattices. Both the High projected-receiver path and the Low surface-composite
  fallback sample two counter-moving world-space layers, convert the source's baked chromatic
  fringes into one neutral intensity, and share the same material-installed texture. The lookup is
  imported as linear, uncompressed data so the thin focus lines do not acquire gamma or block
  artifacts. Projected caustics now use a slower scale-appropriate drift and stronger
  depth/turbidity attenuation: crisp cellular lines in the shallows, subtle light at the mid shelf,
  and none in the deep trench. WaterWorks was inspected as the other downloaded candidate, but its
  caustics are generated from Voronoi/Gradient Noise inside the full nine-pass Shader Graph and
  provide no reusable texture advantage.
  `Market/Debug/Water/Inspect Realistic Water Shader Errors` checks both affected shaders directly.
  *Verify:* rebuild and Play `WaterShaderLab`; compare the shallow calibration floor with the mid
  shelf, then switch quality tiers to confirm both paths keep the same non-repeating pattern.
- `M_RealisticWaterLab` now reads as a body of water instead of clear glass: stronger wavelength-
  dependent absorption and in-scattering hide the seabed progressively with depth while preserving
  readable shallows. The open-water path is longer, caustics and refraction are restrained, and
  slightly broader surface reflections remove the razor-sharp synthetic sheen.
  *Verify:* Play `WaterShaderLab` from the shoreline; the shallow terrace still shows the bottom,
  while the deeper shelves become dense blue water instead of exposing the full seabed.

## [1.8.0] - 2026-07-27

### Added
- Skybox lab: `Market/Debug/Build Skybox Lab` (`SkyboxLabSceneBuilder`) builds
  `Assets/_Project/Scenes/SkyboxLab.unity` - a copy of the WaterShaderLab water setup (same
  `M_RealisticWaterLab` material, so both labs stay in sync) under the newly imported BOXOPHOBIC
  "Skybox Cubemap Extended" pack. The sky uses the pack's `Skybox/Cubemap Blend` shader through a
  project-owned material `Art/Materials/Skybox/M_SkyboxLab.mat` (Sky A = Blue Sky, Sky B = Night
  Sky), with ambient and reflections sourced from the skybox; the imported demo materials are left
  untouched. Post processing is set up in the scene by the existing rendering tool.
  Why the blend shader instead of `Skybox/Cubemap Extended`: it gives a single day -> night
  transition slider, which is how sky/time-of-day iteration is usually driven in games
  (Enviro/Azure-style two-cubemap crossfade) and it directly feeds a future day-night cycle.
  *Verify:* run the menu item, enter Play in `SkyboxLab` - sky renders, panel opens.
- Tuned the lab look and kept the untouched baseline next to it as
  `Art/Materials/Skybox/M_SkyboxLab_AuthorDefaults.mat`: exposure `1.1 -> 0.95` (the bright horizon
  band clipped under Neutral tonemapping), a slight warm tint `0.53/0.51/0.47` (0.5 is neutral for
  this `[Gamma]` property), sky rotation 35 with drift `0.4 -> 0.12` (sparse stylized clouds read
  as a spinning sky at 0.4), sky height fog on (intensity 0.85, height 0.22, smoothness 0.4, fill
  0.3) against fog colour `0.66/0.72/0.78` so the cubemap's hard horizon line dissolves where it
  meets the water plane, and a low sun at pitch 22 / yaw 150 with intensity `1.3 -> 1.15` - the
  specular path now runs from the sun towards the player start, and skybox ambient carries the fill
  the flat ambient used to add. Ambient intensity 0.85: a bright stylized sky at full skybox
  ambient flattened the beach and the wave shading. Scene fog stays off: `UnderwaterFogController` owns
  `RenderSettings` fog while submerged. Two menu items manage this:
  `Market/Debug/Rendering/Reset Skybox Lab Material` (back to the tuned values) and
  `Market/Debug/Rendering/Restore Skybox Lab Author Material` (back to the backup asset).
  *Verify:* the two menu items flip the sky between the tuned and the as-imported look.
- In-game sky panel `SkyboxRuntimeTuner` (F8, same UiFactory/UIModeService pattern as the F7 water
  tuner): cubemap slot cycling across the three cubemaps in the pack, day->night blend, exposure,
  tint RGB, rotation angle/speed with the rotation keyword, the sky height fog block with the
  `RenderSettings` fog colour, plus sun yaw/pitch/intensity and ambient/reflection intensity.
  Values are written to the shared sky material, so tweaks survive leaving play mode in the Editor;
  environment lighting is refreshed at most 4x/s instead of every slider frame.
  *Verify:* Play `SkyboxLab`, F8 opens/closes the panel, dragging "Sky A -> Sky B blend" fades the
  sky to night and the water reflection follows.
- Post processing actually runs now. The game camera had `Post Processing` and `Anti-aliasing`
  switched off in `Player.prefab`, and `Market.unity` contained no Volume at all - so the project
  rendered HDR and clipped it with no tonemapping. The camera now renders post processing with
  SMAA (High) and dithering (MSAA is not an option: the PC renderer is Deferred), and
  `Market/Debug/Rendering/Setup Post Processing In Open Scene` creates the project profile
  `Art/PostProcessing/MarketPostFX.asset` plus a global Volume in the open scene. Applied to
  `Market.unity` and `Island.unity`.
  Profile targets the cozy look: Neutral tonemapping (ACES desaturates a cartoon palette),
  Bloom threshold 1.1 / intensity 0.35, Color Adjustments +8 contrast and saturation, warm White
  Balance, light Vignette. The Volume sits at priority 1 so it wins over the Bitgem demo Volume
  still present in `Island.unity`.
  *Verify:* enter Play in Market - the image is tonemapped, edges are antialiased.

### Changed
- PC render pipeline settings tuned for image quality: Color Grading `LDR -> HDR` with LUT 32 -> 33
  (LDR grading clamped the frame before tonemapping, wasting the whole HDR path), Opaque
  Downsampling `2x Bilinear -> None` (water refraction was reading a half-resolution opaque
  texture), HDR Color Buffer `32 -> 64 bits`, Shadow Distance `50 -> 100`, Light Probe System
  `Light Probe Groups -> Adaptive Probe Volumes` (no legacy probe groups existed in any scene, so
  nothing regressed). SSAO now takes normals from the G-buffer instead of reconstructing them from
  depth (`Source: Depth -> Depth Normals`), with radius `0.3 -> 0.5` and `Samples: Medium -> High`.

### Fixed
- Realistic water: the planar reflection no longer swims when the viewpoint changes. Scene view,
  preview, and probe cameras skip the reflection pass, but `_PlanarReflectionAvailable` was left at
  1, so the water sampled the texture rendered for the *game* camera using its own screen UVs - the
  reflection stayed glued to the game viewpoint. Non-rendering cameras now clear the flag and fall
  back to the probe/sky reflection.
  *Verify:* fly the scene view - the reflection no longer slides across the surface.
- Realistic water: the planar reflection was vertically mirrored. `_PlanarReflectionFlipY` came from
  `SystemInfo.graphicsUVStartsAtTop`, which is true on D3D - but URP 17 already normalises
  render-texture orientation, so the extra flip made far water reflect the seabed hemisphere as a
  brown band running up to the horizon while the sky above it was clear blue. R5 introduced the
  assumption and no stage report ever compared it against the alternative. It is now an inspector
  `Auto/Never/Always` toggle defaulting to `Never`, and `WaterShaderLab` is serialized with `Never`.
  *Verify:* capture from `(6, 1, -16)` looking down +Z - `Auto` shows the brown band across the
  middle of the frame, `Never` shows sky-blue water to the horizon.
- Realistic water: the lab wind now points at the beach. `WaterShaderLab` puts the beach terrace at
  `-Z` and the deep trench at `+Z`, while `_WindDirection` was `(0.9063, 0, 0.4226)` - along `+Z`,
  i.e. out to sea. Both `M_RealisticWaterLab.mat` and the shader default are now
  `(0.4226, 0, -0.9063)`, so the swell rolls shoreward with a slight along-shore skew.
- Realistic water: crests now travel *with* `_WindDirection` instead of against it. The Gerstner
  phase used GPU Gems' printed `+ phi*t`, which sends the crest along `-direction`, while
  `RealisticWaterTemporalFoam` advects its history along `+_WindDirection` - so the persistent
  whitecaps smeared against the waves that spawned them. The time term is now subtracted in both
  `RealisticWater.shader` and `RealisticWaterFoamUpdate.compute` (they must stay in lockstep), and
  the two micro-normal layers scroll the same way.
  *Verify:* in `WaterShaderLab`, foam streaks trail the crests instead of running ahead of them.
- Realistic water now applies scene fog (`multi_compile_fog` + `MixFog`). The pass composites
  opaquely, so without it the water stayed crisp against fogged terrain - visible the moment it
  meets `Island.unity`, which has linear fog from 250 m.
- `Island.unity` really is on the project-owned water materials now. The previous entry only wired
  `StylizedWaterIslandSceneBuilder` (the proto scene); `IslandSceneBuilder` and the serialized
  `Island.unity` were still bound to `Assets/Bitgem/.../example-water-01..03.mat`, so the F7 panel
  and the tuner window kept editing the imported package assets in place. `IslandSceneBuilder` now
  goes through the same `EnsureProjectCopy` path, and the scene's `MeshRenderer` +
  `WaterMaterialSwitcher` point at `Art/Materials/Water/StylizedWater_01..03.mat`.
  *Verify:* open `Island.unity`, drag an F7 slider, and only the `_Project` copy changes on disk.
- `SHADERS.md` no longer claims `MarketWater` is the shipped island water; it runs in `Map.unity`
  only since the island water swap.
- The island scene now uses project-owned copies of the three package water materials
  (`Art/Materials/Water/StylizedWater_01..03.mat`, created on first build and never overwritten
  afterwards). Tuning through the editor window or the in-game panel writes to the shared material,
  which previously meant the imported package materials were edited in place; the package assets are
  now left untouched and the tuned copies survive scene rebuilds.
  *Verify:* rebuild the island scene, drag a tuner slider, and only the `_Project` copy changes.
- Updated the imported Bitgem water runtime for Unity 6.5: generated mesh names now use
  `GetEntityId`, and early floater queries safely return until the water volume has built its tile
  cache. This removes the package compile failure and the first-frame Play Mode exception.
  *Verify:* scripts compile with zero warnings; Play Mode renders the scene without package
  exceptions.

### Removed
- Removed the previous third-party water integration completely: package remnants, renderer
  features, reserved layers, gizmo icons, test scene, migration/build tools, and archived backups.
- Removed the Island scene's old `Ocean` primitive and `M_Ocean` material binding from both the
  serialized scene and `IslandSceneBuilder`.

### Added
- Completed the R9 realistic-water production gate. `RealisticWaterQualityController` now applies
  coherent High/Low tiers across planar reflection, temporal foam, projected caustics, and the
  underwater surface. The material migration removes ten orphaned serialized properties, and the
  main-light shadow keywords now use one mutually exclusive variant group instead of three
  independent groups. A fixed 1280x720 subsystem profile measured High at 2.44 ms observed p95 and
  0.70 ms GPU p95; the 360-degree High camera turn measured 2.15 ms p95 with no frame above
  16.67 ms. The final DX12 Windows development build completed with zero build warnings and its
  standalone High capture measured 1.84 ms observed p95 and 0.43 ms GPU p95, with no frame above
  16.67 ms.
  `MarketWater` remains 0.33 ms cheaper at GPU p95 in the same lab view and better matches a simple
  cartoon style, so the realistic candidate remains isolated in `WaterShaderLab` pending an
  explicit promotion decision; Island water is unchanged.
  *Verify:* run `Capture R9 Subsystem Profile`, inspect `Artifacts/RealisticWater/R9`, build the R9
  standalone player, and confirm the scene returns to serialized High with health `ok`.
- Added R8 underwater surface rendering to `WaterShaderLab`. An optional front-face-culling
  underside renderer now mirrors the four Gerstner waves and optical parameters, rebuilds the
  inverted normal for the view from below, and blends transmission with total internal reflection
  at grazing angles. `UnderwaterFogController` evaluates the local displaced height and shares a
  0.4-unit crossing blend with the surface; `FrontFaceOnly` keeps the additional renderer disabled.
  A paired fixed-camera benchmark measured the pass at +0.03 ms GPU p95 over its fallback
  (0.43 ms versus 0.41 ms), while the standard 1280x720 harness remained at 2.97 ms observed p95
  and 0.70 ms GPU p95.
  *Verify:* run `Capture R8 Underwater Performance`, inspect the transition and underwater
  diagnostics under `Artifacts/RealisticWater/R8`, then select `FrontFaceOnly` and confirm the fog
  remains active while the underside renderer stays disabled.
- Added R7 world-space caustics to `WaterShaderLab`. A bounded receiver-overlay path now projects
  two animated patterns along the sun direction onto five seabed terraces and four rocks, with
  depth, turbidity, sun-angle, normal, bounds, above-water, and main-light-shadow rejection.
  `SurfaceFallback` disables all nine overlays and restores the previous cheap water composite.
  The same 1280x720 harness measured 3.38 ms observed p95 and 0.69 ms GPU p95; projected caustics
  added about 0.02 ms to GPU p95 without allocating an auxiliary render target.
  *Verify:* open `WaterShaderLab`, inspect the receiver-only R7 capture for stable scale, deep-water
  fade, rock shadows, and the unlit dry beach; switch the component to `SurfaceFallback` and confirm
  the receiver root disables while caustics remain visible through the water.
- Added R6 temporal foam to `WaterShaderLab`: a bounded 256x256 world-space compute history now
  accumulates, wind-advects, and decays Jacobian whitecaps while a separate channel keeps broken-up
  shoreline/obstacle foam anchored in world space. The shader exposes independent crest/shore
  strengths and retains its instantaneous no-history fallback. The selected 100x100-unit setup
  uses 0.563 MiB; the same 1280x720 harness measured 4.43 ms observed p95 and 0.67 ms GPU p95,
  remaining below the 16.67 ms target.
  *Verify:* open `WaterShaderLab`, enter Play Mode, capture the R6 buffers, suppress whitecap
  injection, and confirm the existing red history survives the next rendered frame before
  decaying; set quality to `NoHistory` and confirm water still renders with reactive foam.
- Grass cards are now a set instead of a single card: `GrassCardBuilder` builds one cropped mesh,
  material and clump prefab per painted texture (`Grass_3.1`, `Grass_4.1`, `Grass_5.1`), keeping the
  original `GrassCard` asset names for the first one so existing scene references stay valid.
  `Market/Debug/Grass Card/3. Scatter Patch In Scene` now mixes the variants at random, and the
  Grass Scatter Brush palette takes any number of sources - it loads every built `*_Clump` prefab on
  open ("Reload grass cards" refreshes it) and only forces a material override when one is set, so
  each card keeps its own.
  *Verify:* run grass card step 2, then step 3 on a terrain scene - the patch shows all three card
  shapes; the brush window lists three sources.
- Added the in-game water tuner (`StylizedWaterRuntimeTuner`, **F7** in the island scene): the same
  labelled sliders as the editor window, but inside play mode, so the water can be tuned while
  walking around. Two side panels leave the middle of the screen free; each row shows the value and
  a one-line explanation, colours get one slider per channel. Presets are cycled with `<` / `>` and
  applied with `Load`, `Overwrite` saves onto the selected one and `Save new` writes the next free
  `water-NN` - the same JSON files the editor window uses. Opening the panel enters UI mode through
  `UIModeService` (cursor free, player input suspended) and F7 closes it again. The property table,
  descriptions, ranges and preset format now live once in `StylizedWaterShaderCatalog` /
  `StylizedWaterPresets`, shared by the window and the panel. The island scene ships the service and
  the panel wired to the player and the water.
  *Verify:* play the island scene, press F7, drag a slider and the water changes immediately; press
  `Save new`, change values, then `Load` restores them.
- Added the `Market/Debug/Water/Stylized Water Tuner` window: every exposed property of the Bitgem
  water shader (shallow/deep colour, depth blend distance and curve, depth foam, ripple normal map
  with tiling, scroll speed, detail tiling and strength, ripple strength, refraction, wave
  frequency/height/speed, foam width and noise, smoothness, metallic) as a labelled slider with a
  one-line explanation of what it does. Edits apply to the selected material live in edit and play
  mode and are undoable. Settings are saved and restored as named JSON presets under
  `Assets/_Project/Art/Materials/Water/Presets`, and one click copies all values back from any of
  the three package materials. Because the imported package materials are shared with the package
  showcase scene, the window warns about them and can create a project-owned copy and assign it to
  the water in the open scene.
  *Verify:* open the window on the island scene water, drag any slider and the Game view updates;
  save a preset, change values, load it back and the values return.
- Added `StylizedWaterIsland.unity`: a compact Unity Terrain island (48x48 units) ringed by the
  Bitgem stylized water, built by `Market/Debug/Water/Build Stylized Water Island Scene`.
  The shared first-person `Player` prefab spawns on the island and its camera gets the same render
  options as the showcase one; the orbit rig ships as a disabled `Showcase Camera` (enable it, and
  disable the player, for package-style fly-around shots with its 1-3 material keys and Space
  pause), while F6 / Shift+F6 on the water cycles the three materials during normal play.
  The water and environment are taken from `StylizedWaterProto.unity` - same three package
  materials, same sun/backlight rig, ambient, skybox,
  `SampleSceneProfile` post processing, reflection probe and camera render options (solid colour
  clear, FOV 45, depth + opaque textures, post processing on, no MSAA/AA). Fog colour and mode are
  the proto values; only the distances are scaled (40..120 instead of 10..30) because the proto
  range is authored for a lagoon-sized view. The water mesh is one asset built from concentric
  zones - 0.5 unit cells (the Bitgem tile size) around the shore, coarser further out, reaching
  220 units so its border always dies inside the fog - with red vertex colours painting the
  package foam along the shoreline. The sea floor dives to its deepest shade before the terrain
  rim, so the terrain square never shows through as shallow water.
  *Verify:* open the scene and capture the Game camera - the island sits in deep blue water with a
  foam ring, a turquoise shallow band and no visible mesh or terrain border; health stays `ok`.
- Imported `URP Stylized Water Shader - Proto Series` under `Assets/Bitgem/StylisedWater/URP`.
  `StylizedWaterProto.unity` is a project-local showcase with the package lagoon mesh, generated
  water volume, URP post processing, per-camera depth/color textures, a slow orbit camera, and
  runtime switching across the three supplied water materials with keys 1-3 (Space pauses orbit).
  `StylizedWaterProtoSceneBuilder` rebuilds it from the untouched package example through
  `Market/Debug/Water/Build Stylized Water Proto Scene`; it is not added to Build Settings.
  *Verify:* the scene loads clean with nine roots; the 1280x720 Game camera capture shows animated
  depth color, refraction, reflections, and shoreline foam with no magenta output.
- Replaced Island water with the Bitgem shader on a project-owned 128x128-cell, 950x950-unit grid.
  `WaterMaterialSwitcher` cycles the three supplied materials with F6 (Shift+F6 reverses), while
  the Island cameras request the depth and opaque textures needed for foam and refraction.
  `Market/Debug/Water/Replace Island Water` reapplies only the water integration without
  rebuilding or losing other Island content; a full `IslandSceneBuilder` rebuild uses the same
  setup. Water motion vectors, shadows, and probe sampling remain disabled.
  *Verify:* Play Mode Game View renders animated blue water around the terrain; Island health is
  `ok` with zero console errors, warnings, or dirty scenes.
- Applied the complete Bitgem example environment preset to Island: its procedural skybox,
  warm sun and cool backlight, flat ambient lighting, linear blue fog, shared tonemapping/Bloom/
  Vignette profile, original realtime Lighting Settings asset, HDR camera output, and a scaled
  realtime reflection probe. Fog distances and probe bounds are adapted to the 500-unit Island
  instead of the package's compact demo scale.
  `Market/Debug/Environment/Apply Bitgem Preset to Island` can reapply only this environment, and
  `IslandSceneBuilder` now persists the same setup on a full rebuild.
  *Verify:* scripts compile with zero warnings; Island Play Mode and project health are `ok` with
  zero console errors, warnings, or dirty scenes.
- `REALISTIC_WATER_IMPLEMENTATION_PLAN.md` defines the staged R0-R9 technical roadmap for the
  experimental shader: fixed verification conditions, physical wave migration, GGX lighting,
  spectral absorption, natural normal detail, reflection/refraction integration, temporal foam,
  projected caustics, underwater rendering, quality tiers, performance gates, affected files, and
  a per-stage execution template. It is explicitly a design guide rather than a second progress
  log; live progress remains in `dev_plan_4_1.md`.
- R0 baseline tooling for the experimental realistic-water track:
  `Market/Debug/Water/Capture R0 Baseline` captures three fixed 1280x720 diagnostic views
  (elevated overview, shoreline detail, and near-horizon aliasing), snapshots the live material
  values plus shader/material dependency hashes, and runs an 8-second fixed-resolution Editor Play
  Mode frame-time sample after a 2-second warmup. Results land under the git-ignored
  `Artifacts/RealisticWater/R0/`; unavailable GPU timing is reported explicitly. The tool restores
  the edit-mode camera and label after capture and makes no shader, material, or scene changes.
  *Verify:* run the menu command from `WaterShaderLab`; confirm three PNGs plus `baseline.md`,
  automatic return to Edit Mode, a clean scene, and Unity health `ok`.
- Debug fly mode for the player: `FirstPersonController` gained `SetFlyMode`/`SetFlyVerticalInput`
  plus `flySpeed`/`flySprintMultiplier`/`flyVerticalSpeed` fields, and `HandleMovement` branches to
  fly physics (no gravity, free vertical move) when active. New `DebugFlyMode` component
  (`Market.DebugTools`) toggles it with **F4** and reads **Space/Left Ctrl** for ascend/descend;
  wired onto `Player.prefab` via the re-runnable `PlayerDebugToolsInstaller`
  (menu `Market/Debug/Add Fly Mode To Player`), so it's available in every scene using that prefab.
  *Verify:* `recompile_scripts` -> health `ok`; Play Mode, press F4, confirm free flight with no
  gravity, then F4 again to confirm normal grounded movement resumes.
- `WaterShaderLabSceneBuilder` (menu `Market/Debug/Build Water Shader Lab`) builds a standalone
  `WaterShaderLab.unity` scene for iterating on the water shader without loading the full Island
  terrain: five stepped seabed terraces (dry beach down to a deep trench) under the game's actual
  `M_Ocean` material/`MarketWater.shader`, a few partially-submerged rocks for foam testing, sun +
  ambient lighting, an in-scene instructions label, and the Player prefab (with fly mode) spawned on
  the beach. Not added to Build Settings - it's an authoring tool scene, not shipped content.
  *Verify:* `recompile_scripts` -> health `ok`; `Capture Active Scene Camera` renders the terraces
  and water gradient correctly.
- `RealisticWaterMeshGenerator` (menu `Market/Debug/Water/Generate Realistic Water Mesh`) bakes a
  dense 200x200-vertex grid mesh (`Assets/_Project/Art/Meshes/Water/RealisticWaterGrid.asset`,
  100x100 world units, 32-bit indices) for an upcoming experimental realistic (PBR, Gerstner-wave)
  water shader - step 1 of that track. A flat primitive `Plane` (10x10 verts) has nowhere near
  enough resolution for vertex-displaced waves. Purely a mesh asset for now; no shader/material
  changes yet, and `MarketWater.shader`/`M_Ocean` are untouched.
  *Verify:* `recompile_scripts` -> health `ok`; menu logs `200x200 verts (40000 total, 79202 tris)`.
- Step 2 of the realistic-water track: `RealisticWater.shader` (`Market/World/RealisticWater`) -
  a vertex-displaced Gerstner wave stack (4 layers: angle/wavelength/amplitude/speed/steepness,
  analytic normals per GPU Gems 1 ch.1) plus a procedural sine-based micro-ripple normal (no
  texture dependency), lit with a Fresnel/specular model. No reflections/refraction/foam/caustics
  yet - later steps. `RealisticWaterMaterialInstaller` (menu `Market/Debug/Water/Create Realistic
  Water Material`) creates `M_RealisticWaterLab.mat` at shader defaults. `WaterShaderLabSceneBuilder`
  now also builds a second, separate zone ~110 units north of the existing terraces running this
  material on the `RealisticWaterGrid` mesh, for direct side-by-side comparison with the cartoon
  `M_Ocean` water (undecided whether it replaces it - see it first).
  *Verify:* `recompile_scripts` -> health `ok`; rebuilding the lab scene renders both zones with
  0 console errors; captured render shows layered wave motion with plausible shading/specular.
- Step 3 of the realistic-water track: `RealisticWater.shader` switched to the Transparent queue
  (`ZWrite Off`, no GPU blend - the shader fully replaces each pixel itself) and gained refraction
  (distorted `_CameraOpaqueTexture` read, with a guard that falls back to the undistorted UV when
  the distorted sample would refract into geometry sitting in front of the water), depth-based
  color absorption (`_CameraDepthTexture` via the same `ComputeWorldSpacePosition`/`hasSurface`
  technique already proven in `MarketWater.shader`), and reflections via URP's own
  `GlossyEnvironmentReflection` (reflection-probe/skybox today; becomes real screen-space
  reflections for free if the "Screen Space Reflections" renderer feature is ever added to
  `PC_Renderer.asset` - that one step is a manual Inspector action, not done here, since it's a
  project-wide renderer asset and the realistic-water track is still experimental/undecided).
  `RealisticWaterPipelineSetup` (menu `Market/Debug/Water/Enable Opaque Texture (PC Pipeline)`)
  turns on `supportsCameraOpaqueTexture` on the PC URP asset (depth texture was already on;
  Mobile_RPAsset is untouched - this track is PC-only).
  *Bug found and fixed during verification:* grazing-angle reflect vectors off tilted wave facets
  were dipping into the skybox's lower "ground" hemisphere, which defaults to a brown/tan color -
  rendered as ugly tan streaks across the whole surface. Fixed by clamping the reflect vector to
  the upper hemisphere before sampling (water only ever mirrors the sky, never "ground"), matching
  standard practice for water shaders. Found via `player_agent` (real Game View, not just the
  editor capture tool) plus isolating the term by zeroing `_ReflectionStrength`/`_SpecStrength`
  live via `modify_material` before touching any code.
  *Verify:* `recompile_scripts` -> health `ok`; `player_agent` render over both zones shows sky-blue
  reflections and no tan artifacts.
- Removed the cartoon `M_Ocean` comparison water from `WaterShaderLab.unity`: `WaterShaderLabSceneBuilder`
  now places the realistic water plane directly over the existing terraced shoreline (dry beach down
  to deep trench) and foam-test rocks instead of a separate side-by-side zone, giving the realistic
  shader a proper depth/shoreline test bed for the upcoming foam/caustics work. Scene simplified from
  9 to 7 root objects.
  *Verify:* `recompile_scripts` -> health `ok`; `player_agent` render from spawn shows the beach,
  foam-test rock, and realistic water filling the terraces with no cartoon water present.
- Step 4 of the realistic-water track (final step for now): `RealisticWater.shader` gained
  Jacobian-based whitecap foam on breaking wave crests (the vertex shader now accumulates the
  horizontal-displacement Jacobian `sumXX/sumZZ/sumXZ` alongside the existing Gerstner sums; where
  it collapses toward zero the surface is folding/breaking), shoreline foam from the existing water-
  column-depth calculation, a shared sine-based breakup noise so neither reads as a flat mask, and
  procedural underwater caustics (two crossed, drifting sine grids sharpened into a bright lattice,
  evaluated at the reconstructed seabed world position so the pattern sits on what's underneath,
  fading out with depth/absorption). New properties: `_FoamColor`, `_FoamCrestCutoff/Softness`,
  `_FoamShoreWidth`, `_FoamNoiseTiling/Speed`, `_CausticColor/Tiling/Speed/Intensity`.
  *Tuning found live via `modify_material` before touching the shader:* the shader's first-guess
  defaults were both wrong in the same direction (too generous) - `_FoamShoreWidth` 2.0 covered the
  entire "Shallows" terrace shelf edge-to-edge (the lab's terraces are flat steps, not a smooth
  beach slope, so a wide shore-foam threshold reads as a solid white shelf rather than a coastline);
  `_FoamCrestCutoff` 0.55 triggered on nearly every wave facet instead of just breaking crests.
  Isolated by zeroing each term independently before concluding "not a bug" and re-tuning: shipped
  defaults are `_FoamShoreWidth` 1.0, `_FoamCrestCutoff` 0.15, `_FoamCrestSoftness` 0.1,
  `_CausticIntensity` 1.8, `_CausticTiling` 0.7 (first guesses of 1.2/0.4 were barely visible).
  *Verify:* `recompile_scripts` -> health `ok`; capture from an elevated, downward-angled view (a
  flat grazing view reads mostly as sky reflection regardless of foam/caustic settings - not a bug,
  correct Fresnel behavior, but a bad angle to judge them from) shows foam localized to the rocks/
  shoreline/crests and a visible animated caustic lattice on the sand that fades with depth.
- Step 5 (polish) of the realistic-water track: `RealisticWater.shader` now receives main-light
  shadows (added the standard `_MAIN_LIGHT_SHADOWS`/`_MAIN_LIGHT_SHADOWS_CASCADE`/
  `_MAIN_LIGHT_SHADOWS_SCREEN`/`_SHADOWS_SOFT` multi_compiles, `TransformWorldToShadowCoord` +
  `GetMainLight(shadowCoord)`, `shadowAttenuation` applied to both diffuse and specular) so rocks/
  terrain shadow the surface instead of it always reading fully lit. New `UnderwaterFogController`
  (`Market.DebugTools`) switches `RenderSettings.fog` to a blue-green tint while the main camera
  sits below the water surface's Y and restores the previous fog state on surfacing - needs no
  shader changes at all, since the water mesh back-face-culls from below (invisible once
  submerged) and the surrounding `Universal Render Pipeline/Lit` geometry already reads
  `RenderSettings.fog` natively. Wired into `WaterShaderLabSceneBuilder` on a new
  "Underwater Fog Controller" object referencing the water transform.
  *Verify:* `recompile_scripts` -> health `ok`; `player_agent` with the player placed below the
  water's Y shows the hazy blue-green underwater fog; surfacing restores the normal view.
  *Not done yet - flagged rather than attempted quietly:* foam persistence over time (whitecaps
  currently are fully reactive to the instantaneous wave state, with no accumulate-then-decay
  memory). That needs a genuinely bigger piece of infrastructure - a world-space foam accumulation
  buffer (e.g. a persistent RenderTexture updated by a decay-and-inject pass each frame, sampled by
  the shader instead of computing foam purely from the current frame) - so it's deferred as its own
  follow-up rather than bundled in here.
- Correctness pass over `RealisticWater.shader` (review of the steps 1-5 work; web-checked against
  GPU-Gems Gerstner-Jacobian foam and standard water-normal LOD practice). Six real defects fixed:
  (1) **Jacobian whitecap foam was dead code** - the "breaking crest" foam claimed and "verified"
  in step 4 produced zero pixels: with the gentle default waves the horizontal-displacement
  Jacobian never drops below ~0.55, but the foam threshold was `J < 0.15`, so `crestFoam` was
  identically 0 everywhere; every whitecap in the step-4 screenshots was actually shoreline foam +
  Fresnel + specular. Replaced the unreachable `_FoamCrestCutoff`/`_FoamCrestSoftness` cutoff with a
  `(1 - J - _FoamCrestBias) * _FoamCrestGain` ramp so foam scales with closeness-to-folding and is
  actually visible on crests (confirmed in a horizon render). (2) **No distance detail-fade** on the
  micro-ripple normal caused specular/normal aliasing (firefly speckle) on far water - a regression
  vs `MarketWater.shader`, which fades detail with camera distance; added `_DetailFadeStart/End` and
  fade the micro normal toward flat with distance. (3) **Specular firefly** from the same source -
  specular is now multiplied by the distance-fade so far water stops sparkling. (4) **Back-facing
  wave facets** (NdotV < 0) saturated Fresnel to 1 and flashed white on wave undersides - the normal
  is now flipped to face the viewer before shading. (5) **No Fresnel base reflectance** - swapped the
  plain `(1-cos)^p` for Schlick with an `_FresnelBase` floor (~0.02) so water still mirrors ~2% of
  the sky head-on instead of going non-reflective. (6) **Shadow bled into transparency** - the
  shallow-water body tint keyed on a shadowed NdotL, so a rock/cloud shadow made *more* seabed show
  through; the body tint now uses a shadow-free NdotL while shadow still darkens the lit result and
  glint. Orphaned `_FoamCrestCutoff`/`_FoamCrestSoftness` values remain in `M_RealisticWaterLab.mat`
  but are ignored (harmless); the new properties fall back to shader defaults.
  *Verify:* `recompile_scripts` -> health `ok`; elevated downward capture shows live crest foam on
  the wave tops (previously absent) and a horizon capture shows clean far water with coherent
  whitecaps instead of firefly speckle.
- Added alpha-cutout card grass built from the artist's `Assets/blender/Grass_3.fbx` quad and
  `Grass_3.1.png` (1024x1024 RGBA, hard alpha). New re-runnable builder `GrassCardBuilder`
  (menu `Market/Debug/Grass Card/1..4`) inspects the source card, fixes the texture import
  (coverage-preserving mips, cutoff-matched alpha reference, Clamp wrap, 512 max, aniso 4), bakes the
  clump mesh, creates `GrassCard.mat`, saves a `GrassCard_Clump` prefab, scatters a 400-clump demo
  patch onto the island terrain, and renders an eye-height preview to
  `Artifacts/Capture/grass_card_patch.png`. Assets land in `Assets/_Project/Art/Nature/Grass/`.
  *Verify:* run steps 1-4; the preview shows the patch and two consecutive captures differ by ~20%
  of their pixels (the grass is animating).
- `CardsPerClump` (default **1**) controls how many cards are baked into one clump mesh, spread over
  180 deg - the shader is `Cull Off`, so a half turn already covers every orientation. Baking rather
  than parenting keeps a multi-card clump at a single renderer. *Why 1:* a lone quad goes invisible
  edge-on, but scatter density hides that, and every extra card is another full layer of
  alpha-cutout overdraw - the expensive axis for this kind of grass. Raise to 2 for the usual X-cross
  if a clump ever has to read solid on its own.
- `GrassCardBuilder` measures the texture's alpha bounding box and crops the card **geometry** to it
  while leaving the mesh UVs at a full 0..1, pushing the crop into `_BaseMap_ST` instead.
  *Why:* the artwork covers only 42% of its square (bottom ~half), so an uncropped card shades 58%
  fully transparent pixels, and a UV.y wind mask would peak over empty space while the visible
  blades barely moved. Keeping mesh UVs at 0..1 keeps the mask running root-to-tip across the
  actual blades. *Verify:* step 2 logs the measured UV rect and its coverage percentage.

### Changed
- R5 of the realistic-water plan hardens screen refraction and adds one bounded local planar
  reflection to `WaterShaderLab`. Refraction distortion now fades at screen edges and in thin
  shoreline water, clamps its lookup, and rejects foreground geometry through reversed-Z-safe
  linear eye depth plus the existing sky/finite/below-water checks. The new
  `RealisticWaterPlanarReflection` renders a clipped half-resolution mirrored camera only for the
  lab surface; Sky Only is the zero-pass fallback and Full Resolution remains diagnostic. The
  reflection pass omits post processing, opaque/depth copies, MSAA, probes, water self-rendering,
  motion vectors, and water shadows; no Island or project-wide renderer feature was changed.
  `RealisticWaterMaterialInstaller`, `WaterShaderLabSceneBuilder`, the baseline snapshot, and
  `SHADERS.md` persist and document the R5 setup.
  *Verify:* deterministic elevated/shoreline/horizon captures show reflected rock silhouettes
  without foreground refraction, edge smear, black borders, or fallback flashes. Same-harness R4
  sky-only -> R5 planar-half timings (avg / p95): observed `1.68 / 2.12` ->
  `2.55 / 3.56 ms`, CPU `1.68 / 2.12` -> `2.55 / 3.58 ms`, GPU `0.47 / 0.61` ->
  `0.52 / 0.67 ms`; p95 remains below `16.67 ms`. Evidence is under
  `Artifacts/RealisticWater/R5/`.
- R4 of the realistic-water plan replaces the crossed-sine micro-ripple field with two generated
  seamless 256x256 normal maps. `RealisticWaterNormalTextureGenerator` deterministically rebuilds
  both assets, configures them as repeat-wrapped normal maps with mipmaps, trilinear filtering,
  compressed high-quality import, and anisotropy 4, then assigns them to the lab material. The
  shader samples exactly two layers in mesh-anchored world space: one follows `_WindDirection`,
  the second uses an authored wind offset, different tiling, and different speed. Reoriented
  normal mapping combines both layers and the Gerstner derivative normal without component
  addition. Existing distance fade is retained and a derivative-based texel-footprint fade removes
  each layer before it becomes sub-pixel noise. `_MicroWaveStrength` remains the master amplitude;
  `_MicroWaveTiling` and `_MicroWaveSpeed` are retired harmless material orphans, replaced by
  `_NormalLayerATiling`, `_NormalLayerBTiling`, `_NormalLayerASpeed`, `_NormalLayerBSpeed`, and
  `_NormalLayerBRotation`.
  *Verify:* macro-only versus two-layer environment captures show irregular close-range breakup
  without the previous diagonal sine grid; the detail remains attached to the displaced mesh and
  fades cleanly toward the horizon. Same-harness R3 -> R4 timings (avg / p95): observed
  `6.41 / 15.10` -> `1.64 / 1.92 ms`, CPU `6.41 / 15.07` -> `1.64 / 1.92 ms`, GPU
  `0.46 / 0.60` -> `0.46 / 0.47 ms`; p95 remains below `16.67 ms`. Evidence is under
  `Artifacts/RealisticWater/R4/`.
- R3 of the realistic-water plan replaces scalar depth-to-`_DeepColor` fading with per-channel
  Beer-Lambert transmittance and controlled in-scattering. The selected refracted opaque position
  now passes explicit sky-depth, finite-position, and below-water validation; optical extinction
  uses the actual water-to-scene view-ray distance, while shoreline foam keeps a separate vertical
  depth. `_DepthFadeDistance` is explicitly migrated to the open-water fallback path.
  `_AbsorptionCoefficients`, `_ScatteringColor`, and `_ScatteringStrength` are new material
  controls. `_DeepColor` and `_ShallowColor` are retired, and their serialized values remain
  harmless orphans. Caustics use transmittance luminance for the down-light path and are then
  attenuated again with the refracted scene on the view path.
  *Verify:* isolated transmission preserves shallow sand and progressively removes red before
  green/blue; oblique rays converge smoothly to deep blue-green, sky fallback remains stable, and
  disabling scattering makes deep water visibly darker. Restored full-composite capture is clean.
  Same-harness R2 -> final R3 timings (avg / p95): observed `3.73 / 7.08` ->
  `6.41 / 15.10 ms`, CPU `3.73 / 7.42` -> `6.41 / 15.07 ms`, GPU `0.45 / 0.46` ->
  `0.46 / 0.60 ms`;
  p95 remains below `16.67 ms`. Evidence is under `Artifacts/RealisticWater/R3/`.
  Follow-up: optical and shoreline depth now come from the unperturbed center ray because the
  screen-space refracted lookup is not a metric ray. This removes false green shallow-water
  patches when the offset crosses a neighboring seabed terrace; refraction still affects scene
  color and caustic placement. Out-of-viewport refraction also falls back to the center sample.
  The tuned absorption `(0.22, 0.10, 0.04)` and scattering color `(0.015, 0.18, 0.32)` with
  strength `0.4` retain blue-green deep water while keeping the shallow seabed neutral.
  Motion review found a second shallow-water failure: displaced troughs could fall below the
  seabed, reject valid center depth, and animate the `6 m` open-water fallback as large color
  islands. Depth validation and Beer-Lambert distance now use the mesh's mean water level, while
  displacement remains active for geometry, normals, reflection, and foam.
- R2 of the realistic-water plan replaces the legacy `pow(NdotH, _SpecPower)` sun glint and
  variable-power Fresnel blend with a Cook-Torrance GGX BRDF: Schlick Fresnel at water
  `F0 = 0.0204`, GGX normal distribution, height-correlated Smith visibility, and a roughness floor
  that approximates finite sun size. Environment reflection and transmission now share Fresnel
  energy instead of a generic color lerp; only direct sun reflection receives the main-light
  shadow. Perceptual roughness increases with the existing distance-detail fade to suppress
  sub-pixel horizon glints. `_SpecColor`, `_SpecPower`, and `_FresnelPower` are retired (their
  serialized material values remain harmless orphans); `_SpecStrength` is now a bounded direct
  reflection control.
  *Verify:* isolated transmission/environment/direct captures confirm that GGX produces sparse,
  smooth sun highlights without fireflies; the broad white bands originate in the existing
  procedural micro-normal/environment path and remain an R4 concern. Same-harness 1280x720 p95:
  observed `7.31 -> 7.08 ms`, CPU `7.37 -> 7.42 ms`, GPU `0.44 -> 0.46 ms`.
- R1 of the realistic-water plan replaces independent linear wave speeds with deep-water
  dispersion (`omega = sqrt(g * k)`) while preserving every `_WaveNParams.w` value as a
  dimensionless speed multiplier. New `_WindDirection` and `_WindSpread` properties compress the
  four authored wave angles around one normalized wind direction without removing crossing-wave
  energy. Gerstner displacement now accumulates exact X/Z surface derivatives; the macro normal
  and horizontal Jacobian come from those same derivatives instead of separate approximations.
  Effective steepness is bounded per wave so the combined horizontal derivative remains below the
  normal fold limit while still allowing the Jacobian to approach zero for crest foam.
  *Verify:* same-harness pre/post captures show coherent wind-led groups, attached crest/trough
  normals, and no new inverted facets or horizon fireflies. Fixed 1280x720 p95 stayed effectively
  flat: observed `7.29 -> 7.31 ms`, CPU `7.42 -> 7.37 ms`, GPU `0.43 -> 0.44 ms`.
- `RealisticWaterBaseline` now resumes a pending measurement after Play Mode domain reload, enables
  background ticking for the duration, requests PlayerLoop updates when MCP does not focus the Game
  View, and exits through a five-second safety timeout if Unity still supplies no frames. This
  prevents an R0/R1 benchmark from leaving the Editor stuck in Play Mode.
- `GrassWind.shader` / `GrassWindCommon.hlsl`: the root-to-tip wind mask is now selectable per
  material - `[Toggle(_WINDMASK_UV)] _WindMaskFromUV` switches it from object-space Z
  (`_BladeTipHeight`, unchanged default for the Grass_1/Grass_2 geometry tufts) to `UV.y`.
  Added `_VertexColorTint` (0..1) so the base tint isn't multiplied by a vertex-colour set the mesh
  doesn't have. *Why:* `_BladeTipHeight` is 2 mm, tuned for the tufts; on a 0.4 m card it saturates
  the mask almost at the root and the quad sways as a rigid slab. `UV.y` is scale-independent, so
  one material drives any card size. *Verify:* `recompile_scripts` -> health `ok`; the tufts still
  use the legacy path (keyword off).
- Added the `Island` scene (`Assets/_Project/Scenes/Island.unity`) as the main gameplay location:
  a cozy temperate farm/trading island (Ginger-Island-style layout reference) on a 500x500 Unity
  Terrain, ringed by shallow cartoon water using the `M_Ocean` material. Generated by the re-runnable
  `IslandSceneBuilder` (menu `Market/Debug/Build Island Scene`): radial island heightmap with a
  wobbled coastline, a broad buildable interior and inland hills; three flat-colour terrain layers
  (grass/sand/rock) splat-painted by height and slope so the ground reads as cartoon colour, not the
  untextured grey default; a large water plane at sea level; `ZoneAnchors` empties marking homes for
  every subsystem (market square, harbor/supplier, fishing dock, farm fields, animal pasture,
  crafting yard, town centre, forest hill); a directional light, trilight ambient, skybox and gentle
  linear fog; and an establishing camera. Verified via aerial, top-down and offshore renders from the
  new capture tool; recompilation and health passed with 0 console errors, scene saved clean.
  Generated terrain assets live under `Assets/_Project/Art/Terrain/`.
- Added a local `.claude/tools/video-analyze.ps1` helper that reads video metadata, extracts evenly
  spaced JPEG frames, builds a timestamped contact sheet, and reports high-motion timestamps. Its
  OpenCV decoder is bootstrapped into a machine-local cache and generated analysis stays ignored.
- Added an `Ocean` plane to the `Map` scene on the Water layer, with shadows and collision disabled.
  Added the custom URP `Market/World/StylizedWater` shader and `M_Ocean` material. Reworked the shader
  from the earlier dark near-opaque plate into a bright cartoon lake that reads as water from any
  angle: depth-graded shallow/deep color, animated crossing-sine wave normals with a flow-map
  break-up, a crisp toon sun glint, sky fresnel toward the horizon, gentle shallow-only refraction,
  and a noise-broken shoreline foam ring. Wave detail (and the glint) fade with camera distance to
  kill the far-field moire, and surface opacity was raised so the untextured lake bed no longer
  shows through. Shader structure adapted from TinyPlay's MIT-licensed URP shader collection with its
  license included. Verified by rendering the `Map` camera at eye level and 3/4 angles via the new
  capture tool: clean cartoon water, live wave bands, tidy foam, no moire or pink fallback;
  recompilation and health passed with 0 console errors. The already-dirty `Map` scene was left
  unsaved.
- Added `SceneCameraCapture` (menu `Market/Debug/Capture Active Scene Camera`), an Editor-only
  debug tool that renders the active scene's camera to `Artifacts/Capture/scene_camera.png` (git
  ignored) so an off-Editor agent can inspect a scene visually without Play Mode or the FPS
  controller.
- Added an isolated `Prototypes/harbor-library` Tauri 2, React, and TypeScript UI prototype for
  the two-person Harbor Market reference workspace. It includes a responsive three-pane desktop
  layout, searchable and filterable mock references, editable material properties, favorite and
  delete actions, focused collaborator presence, sync status, and a project-owned six-scene concept
  art atlas. The frontend production build passes; native Tauri packaging remains unavailable on
  this machine because Rust and the Windows MSVC build tools are not installed.
- Added compact MCP output for Asset Pipeline analysis. `analyze_asset_model` returns one short
  JSON summary with metrics and up to five issue IDs, stores the full analysis under ignored
  `Artifacts/AssetPipeline/`, and `get_asset_pipeline_issue` retrieves only one requested
  finding at a time. A live `wood_box.fbx` analysis returned a compact warning payload without
  changing the asset or importer; all 126 MCP server tests and 75 Market EditMode tests passed.
- Added a focused Asset Pipeline Assistant for selected FBX/OBJ models. It reports bounds, mesh
  statistics, profile-scale mismatches, pivot placement, invalid transforms, generic Blender names,
  material/URP issues, importer settings, colliders, and project wrapper coverage. Explicit,
  confirmation-gated actions can apply a static importer preset or create a non-destructive
  project-owned wrapper prefab with a bounds-based BoxCollider; no batch mutation or background
  postprocessor is used. The compact local gate passed GREEN with 75/75 Market tests, 145 scanned
  assets, and a clean Market scene; opening the UI also left the scene clean.
- Added a local verification gate that orchestrates Unity compilation, health, Project Health,
  EditMode tests, and final scene cleanliness. The new MCP surface returns only a compact summary
  and up to five issue IDs, writes full details under ignored `Artifacts/Verification/`, and
  exposes one-issue-at-a-time detail retrieval to avoid sending large logs to the model. Live
  verification returned GREEN with 62/62 focused Market tests, 140 scanned assets, and a clean
  Market scene; all 124 MCP server tests also passed.
- Added a read-only Market Project Health Scanner Editor window with focused checks for item,
  crop, and NPC data contracts, ItemDatabase integrity, missing prefab scripts, and non-ASCII
  project content. Results can be filtered, selected, pinged, copied, or saved outside Assets;
  focused EditMode tests cover the shared validation rules. The live scan checked 139 assets and
  returned GREEN with six informational legacy-ID notes; Unity compilation, all 121 Market tests,
  and health passed with the Market scene unchanged and clean.
- Added a five-slot bottom quickbar for the player inventory. It updates from inventory changes,
  shows item icons, names, and quantities, and selects slots with keys 1-5 or mouse clicks.

### Fixed
- Removed Island Scene View rotation stalls caused by compact-Terrain patch entry: the Island and
  Map builders now disable heightmap LOD frustum culling explicitly, keeping their small Terrain
  patch sets stable while the editor camera turns. Added repeatable gameplay and Scene View
  360-degree benchmarks under `Market/Debug/` to measure p95/max frame time instead of judging feel.
  On the same focused Island Scene View turn, average/p95/max improved from
  3.03/3.79/131.55 ms (2 frames over budget) to 2.12/2.54/9.05 ms (0 over budget); the gameplay
  camera remained stable at 1.57/1.91/5.05 ms with 0 frames over budget.
- Added a recoverable ocean basin to the Map Terrain: the broad center is lowered to Y=75 below the
  Y=120 water surface, with a smooth 380-480 unit transition and an untouched TerrainData backup.
  Removed the camera-centered dark circle by preventing the water shader from sampling URP main-light
  shadow attenuation; the circle was the shadow-distance/cascade boundary moving with the camera.
  Water remains lit, while depth sampling stays active only for shoreline foam.

### Changed
- K5a Island rendering pass: removed the ocean shader's full-frame opaque-color copy, redundant
  scene-color blending and backface pass; disabled water motion vectors and probe work; switched the
  Terrain to instanced drawing, one-sided shadows and bounded LOD/detail distances; lowered the
  directional light's soft-shadow filter cost; capped Island cameras at 750 units with HDR/MSAA and
  opaque-color capture disabled. The re-runnable builder now serializes these settings. Added
  Project Health performance checks and EditMode coverage so Terrain or ocean regressions fail the
  local gate, normalized the same Terrain defaults in the Map builder/scene, and added matching
  outdoor-scene rules in `AGENTS.md`. `Market.Editor` now explicitly references the installed URP
  runtime assembly for the camera/light override APIs used by the builder.
- Reworked the Map ocean material from the MIT-licensed TinyPlay URP water structure to match the
  supplied calm-night reference: dark depth color, two scrolling flow-map ripple layers, moving
  RGB caustic highlights, horizon Fresnel, broad broken shoreline foam, and flat shadow-free
  diffuse lighting without a procedural grid or a
  camera-relative specular lobe that could produce a moving circle.
- E2 farm bed visual fix: disabled crop-stage prefabs immediately when the editor builder creates a
  cell, so empty soil never shows plants before planting. Raised the tilled soil and crop visuals
  above the grass cell surface to prevent geometry clipping.
- E2 farm bed: added an idempotent 3x3 interactive bed in the MainFarm zone. Each cell progresses
  through untilled grass, tilled soil, watered dark soil, sprout, and harvest-ready carrot; planting
  consumes supplier-bought seeds only after tilling and watering. Soil state now persists in SaveData
  v7, with migration coverage for v6 saves. The builder also ensures the H debug key skips one game hour.
- E2 Crop visual stages: the carrot plot now switches between sprout and harvest-ready Cartoon Farm
  Crop visuals based on timer progress. The idempotent crop builder creates and wires both stages
  while preserving the existing E1 planting, harvest, and save flow.
- Added the `player_agent` MCP tool for embodied Play Mode inspection. It drives the real
  first-person `CharacterController` through collision-aware movement, sprint, jump, look, and the
  existing interaction path, then returns a 960x540 Game View PNG with the HUD plus scene, player
  pose, grounded state, and current interaction telemetry. Direct camera capture works while Unity
  is unfocused, and the Node bridge exposes the PNG as MCP image content. Verified with a live
  observe/move/turn capture, Node tests (121/121), Market EditMode tests (103/103), script
  recompilation, and a green Unity health report.
- Replaced the Market scene's isolated 50x50 test floor surroundings with an idempotent 280x260
  old-market valley Terrain: the preserved central market now anchors town, fair, two farm,
  livestock, fishing/shipyard, crafting, horse/race, and expansion reserves connected by a
  4-7 meter road loop. Eight terrain surfaces distinguish worn market ground, paths, both field
  soils, grass, moist shore, rock, and workshop ground; asymmetric ridges and a future water basin
  form the boundary. Sparse Textured Stylized Trees and Low-Poly Medieval Market trees, bushes,
  and stone elements frame only the perimeter, while the builder preserves existing gameplay,
  rebakes the scene NavMeshSurface, validates zone grades and spawn heights, and captures five
  review views under `Artifacts/MarketLandscapeViews`.
- Added a standalone walkable `AssetMuseum` scene and an idempotent editor builder that displays
  all 285 imported buildings, animals, fish, trees, crops, and food-kit models in 17 labeled,
  logically grouped exhibition zones without modifying the source assets. The scene reuses the
  first-person player prefab, locks the cursor through `UIModeService`, assigns the tree pack's
  bark/leaf textures through generated URP materials, and stays outside the game build list as a
  development-only visual catalog. Open the scene and press Play, or rebuild it via
  `Market/Debug/Build Asset Museum` after importing more assets.
- Prepared the dedicated `Map` scene with a centered 256x256 URP Terrain, 30-unit height range,
  513 heightmap resolution, and reusable grass/dirt Terrain Layers from the hand-painted ground pack.
- Added an idempotent Simple Nature Pack material converter and converted its two imported
  Built-in Standard materials to URP/Lit while preserving their base textures and colors.
- Updated the embedded MCP Unity package from release 1.3.0 to upstream `main` commit `c35f184d`:
  added Play Mode play/pause/stop/step controls, the Unity Dashboard MCP App, bounded
  `get_gameobject` responses, private serialized-field updates, project-local Cursor/Claude/Codex
  configuration, WebSocket lifecycle/origin/retry fixes, request diagnostics, and expanded tests.
  Preserved the project's compact health/test tools and added a Unity 6.5-safe main-thread request
  queue after upstream `delayCall` dispatch stalled live WebSocket requests. The direct-call helper
  now also strips PowerShell's UTF-8 BOM from piped JSON. Node tests pass 119/119, MCP Unity
  EditMode tests pass 59/59, Market EditMode tests pass 44/44, and the final health report is green.
- Restored the embedded MCP Unity package and its local verification helpers after Unity's official
  MCP reported a zero direct-connection entitlement for the current account.
- Updated the E1 editor builder to Unity 6.5's non-order-dependent `FindAnyObjectByType` API, removing
  the migration warnings without changing its scene-building behavior.
- Restored Unity 6.5 compilation by explicitly narrowing the MCP package's `EntityId` raw value for
  its legacy integer JSON field instead of using the now-erroring implicit conversion.
- Removed the unused Unity Version Control package, which raised editor errors on machines without
  a configured Plastic SCM client while the project uses Git.
- Updated the project contract to the migrated Unity 6.5.3f1, URP 17.5.0, and AI Navigation 2.0.13
  versions now serialized by the project.
- Reduced development friction in serialized/project context: `InteractionSystem` now defaults to
  the `Interactable` layer instead of raycasting every layer, and a one-shot editor cleanup can apply
  the layer, disable Market-scene keyboard auto-debug helpers, normalize ASCII PlayerSettings, and
  reserialize core scenes/prefabs. The Market scene no longer wires the keyboard/auto debug helpers,
  crop instant-grow debug is off, and local architecture/audit docs now point to current project
  truth instead of stale plan/audit findings. (Codex)
- Migrated player balance storage to integer coins: `MoneySystem` now stores int coins, `SaveData`
  version is 6 with `moneyCoins`, and legacy float `money` remains as an old-save fallback with
  migration coverage. Price fields still remain float at the price/stall/report layer for a smaller
  compatibility surface. (Codex)
- Split NPC hotspots into partial files: `NPCSpawner.Visitors.cs` owns spawn/restore/pool visitor
  logic, and `NPCVisitor.Shopping.cs` owns buying and stall-selection helpers. Behavior is unchanged;
  the split reduces future merge pressure around D/N NPC work. (Codex)
- Split `GameSaver` into partial files: lifecycle/service wiring stays in `GameSaver.cs`, while
  save-state collect/apply helpers now live in `GameSaver.State.cs`. This is a behavior-neutral
  persistence refactor to reduce future merge conflicts as D/E/N systems add their own saved state;
  `SaveData.version` and JSON shape are unchanged. (Codex)
- MCP verification loop is now one-command: added `.claude/tools/mcp-doctor.ps1` for connection
  diagnosis and `.claude/tools/verify-unity.ps1` for `doctor -> optional Assets/Refresh -> recompile
  -> health -> optional EditMode tests`, with retries for transient WebSocket disconnects after
  Unity compilation/domain reload. `verify-unity.ps1` now fails when MCP reports any failed tests,
  even if the transport-level call succeeded. `check-mcp-unity.ps1` now wraps the doctor. (Codex)
- Docs housekeeping: archived pre-1.6.1 release notes to CHANGELOG.archive.md, keeping CHANGELOG.md focused on [Unreleased] plus the latest five releases; normalized project Markdown punctuation to ASCII for terminal-safe handoffs. (Codex)

### Added
- E1 farming slice: added `CropSO`, `CropPlot`, carrot seed data, a carrot crop asset, supplier
  seed stock, a debug Market-scene crop plot, and EditMode coverage for plant/grow/harvest. (Codex)
- D3 Evening Summary: added a daily summary service that tracks revenue, expenses, profit, items
  sold, orders completed placeholder count, and best-selling item from supplier/NPC sale events;
  sleeping to the next day now opens an end-of-day report panel using the shared market UI chrome. (Codex)
- D2/D5 day controls: added `MarketOpenSystem`, root-level debug cubes for Open/Close Market and
  Sleep Until Morning, and tests for explicit market state plus sleep-gated day advancement. NPCs
  now spawn as shoppers only while the market is open; when closed, traffic still appears as
  passersby that walk out without browsing or buying. (Codex)
- D1 `DayPhaseSystem`: game time now maps to Morning Prep, Market Open, Evening Summary, and
  Night / Next Day phases; the service publishes phase changes, direct Market scene startup gets a
  local fallback, and the HUD shows the current phase next to day/time/season. (Codex)
- D0 `MarketStallRegistry`: Market scene now owns two registered stalls through a registry
  coordinator; NPC spawning, stall UI wiring, and save/load no longer depend on a single stall
  reference. (Codex)
- C9 interaction prompt polish: the HUD prompt now resolves the displayed Interact key from the
  active Input System control scheme and binding overrides, with keyboard/gamepad fallbacks. (Codex)
- C8 NPC animated model: replaced the gray capsule visual in `NPC_Visitor.prefab` with the UAL
  humanoid model (skinned mesh + Humanoid avatar), added `NPC_Anim.controller` (Speed/Talking
  params: Idle<->Walk blend tree + Talk state), and `NPCAnimator` driving it from
  `NavMeshAgent.velocity` and `NPCVisitor.CurrentState`. Idle/Walk/Talk play the UAL rig's own
  `Idle_Loop` / `Walk_Loop` / `Idle_Talking_Loop` clips on the shared UAL avatar (no Mixamo retarget
  needed). Visual mesh/outfit variety is deferred until more humanoid assets exist. (Codex)

- Kenney preview thumbnails used as item icons are capped at 256px on all platforms (audit M9),
  cutting VRAM for UI-sized sprites (previously 2048). A SpriteAtlas over the used icons is
  deferred: at the current item count draw-call batching gains little and it would add the
  `com.unity.2d.sprite` dependency. (Claude)
- Static-prop FBX packs (Kenney Food Kit, Stylized Trees, Quaternius Farm Buildings, blender box)
  no longer import a rig or animation (audit H4): 259 models set to Rig=None / Import Animation off,
  so static meshes stop importing an Avatar/Animator. Added a re-runnable `StaticPropImportFixer`
  editor tool; animated packs (animals, fish, UAL, Mixamo) are deliberately excluded. (Claude)
- Cartoon_Farm_Crops materials converted from built-in Standard to URP/Lit (audit M1) so crops no
  longer render magenta under URP; base texture/color carried over. Added a re-runnable
  `CropMaterialUrpUpgrader` editor tool. Turned off Read/Write on the two crop FBX meshes. (Claude)
- Save data is now version 5 and persists crop plots (audit C2): each `CropPlot` has a stable
  `plotId` and its planted flag + plant timestamp are collected/applied by `GameSaver`. Pre-v5 saves
  (no `cropPlots` list) load unaffected - every plot restores to empty, matching prior behavior.
  `CropE1SceneBuilder` now registers the debug plot into `GameSaver.cropPlots`. (Claude)
- Game UI language switched from Russian to English: all player-visible strings in scripts
  (panels, buttons, HUD, settings, seasons, prompts) and serialized assets (`ItemSO.displayName`,
  `NPCTypeSO.typeName`, `CropSO`, Market scene prompts) are now ASCII English. Typographic
  characters in comments normalized to ASCII. This ends the recurring encoding-corruption issues;
  `.editorconfig` added as a guardrail. (Claude)
- Docs consolidated for single-agent development: `COLLAB.md` and `.codex/` removed (Codex no
  longer works on the project), git process folded into `CLAUDE.md`, `AGENTS.md` rewritten
  without two-agent references; MCP helper tools moved to `.claude/tools/`. (Claude)
- `MarketUIController` input polling is split into small helpers while preserving Escape, inventory,
  and tooltip update behavior. (Codex)
- Time now stops at 00:00 and waits for the player to sleep before advancing to the next day, so
  day/season rollover is player-driven instead of automatic. (Codex)
- Save data is now version 4 and records `stallId` for stall slots, while old saves without
  `stallId` restore to the first registered stall for compatibility. (Codex)
- `UIModeService` reapplies cursor lock/visibility when the app regains focus or resumes, reducing
  cursor state drift after focus changes. (Codex)
- `NPC_Visitor` keeps the `Animator` + `NPCAnimator` on the UAL rig root so Humanoid clip bindings
  reach the skeleton; `ApplyRootMotion` is off (the NavMeshAgent drives movement). UAL model import
  now builds a Humanoid avatar so the prefab's avatar reference resolves at runtime, and the NPC uses
  a neutral URP/Lit material to avoid the pink built-in-shader fallback. (Codex)

- Archived the unused `Mixamo_animations` pack to `_ArchiveAssets/` (audit L3); C8 uses the UAL rig's
  own clips, so nothing referenced it. `blender/wood_box.fbx` and the Standard Assets ToonShading
  textures (audit L4) were kept after verification found them still referenced (wood_box in the Market
  scene; the toon ramp by 14 crop materials) - the audit's "unused" assumption was wrong. (Claude)
- Micro-perf pass (audit L1/L2/L6): `NPCVisitor` category check uses a manual loop instead of an
  `Array.Exists` closure; `CropPlot` only rescales its growth visual when progress changes instead of
  every frame; `FileLogger` no longer flushes to disk on every routine Log line (severe messages and
  shutdown still flush), all editor/development-build only. (Claude)

### Fixed
- The interaction prompt now re-reads the current target's text on a low-rate timer (audit M4), so a
  target whose prompt changes over time (e.g. a crop plot reaching Ready) updates instead of showing
  stale text; redundant label writes are skipped to avoid needless TMP rebuilds. (Claude)
- `NPCSpawner` now releases tracked visitors to the pool and resets its active counter when disabled
  (audit M3). Previously a disable/enable cycle left `_activeCount` inflated (permanent under-spawn)
  and stranded visitors self-destroyed instead of pooling. (Claude)
- `EventBus.Publish` now invokes each subscriber in isolation (audit M2): a single throwing handler
  is logged but no longer prevents the remaining subscribers from receiving the event. Added
  `EventBusTests` covering delivery, isolation, and unsubscribe. (Claude)
- NPC visitors now save by a stable `NPCTypeSO.id` instead of the asset name (audit H2), so renaming
  an NPC type asset no longer orphans saved visitors. `Id` falls back to the asset name when unset,
  and restore resolves id first with name/typeName fallbacks, so old saves load unchanged. (Claude)
- Planted crops now survive save/load (audit C2): previously planting a seed then saving lost both
  the crop and the seed because `CropPlot` state was runtime-only and never written to `SaveData`.
- Save writes are now atomic (audit H1): `SaveSystem` serializes to `save.json.tmp` then swaps it
  into place with `File.Replace`, keeping the previous save as `save.json.bak`. `Load` falls back to
  the backup if the primary file is missing or unreadable, so a crash mid-write can no longer destroy
  the only save. (Claude)
- NPC visitors now keep browsing other registered stalls after an empty, uninteresting, or over-budget
  stall instead of leaving after the first failed purchase attempt. (Codex)
- Reworked NPC save/load to a schedule-style, intent-only model (like Stardew/Animal Crossing) so
  restored visitors no longer teleport, jitter, or clip through geometry after Save -> Continue.
  `NPCVisitorData` now stores only intent - `npcTypeKey`, `targetStallId`, `visitedStallIds` (no saved
  transform/timer). On load, only still-shopping visitors are re-spawned at an entrance (always a valid
  navmesh spot) and walk in toward their saved target stall, skipping already-browsed ones; visitors
  already leaving regenerate as fresh traffic. Removed the fragile mid-stride position restore
  (`RestoreState`/`PlaceOnNavMesh`/deferred pathing). Old saves load unaffected. (Claude)
- Disabled the extra root `BoxCollider` on the Market `Supplier` object in the D0 scene version;
  the visible child capsule still provides supplier collision/interaction. (Codex)
- Enabled Loop Time on the three UAL clips the controller uses (`Idle_Loop`, `Walk_Loop`,
  `Idle_Talking_Loop`). Without it each clip played once and froze on its last frame, so NPCs walked,
  locked up, then appeared to slide while the agent kept moving the frozen body. Applied via a small
  re-importable editor tool (`NpcAnimationLoopFixer`) that preserves the clips' fileIDs so the
  controller references stay valid. (Claude)
- Reduced NPC foot-sliding: `NPCAnimator` scales Walk playback to the agent's real ground speed via a
  `WalkMult` controller parameter (floored at 1 so Idle never freezes), and the NPC NavMeshAgent was
  tuned for snappier stops/turns (Acceleration 8->24, AngularSpeed 120->520, StoppingDistance 0->1.2 so
  it brakes into the stall instead of arriving at full speed). NPC walk speed dropped from 3.5 to a
  realistic 1.4 m/s (`NPCType_Default` + prefab agent), which also keeps `WalkMult` near 1x so the feet
  match. Residual stop/turn slide is inherent to in-place clips without root motion. (Claude)

### Verification
- GameSaver split: `.claude/tools/verify-unity.ps1 -Refresh -RunTests -WaitSeconds 5` passed:
  `Assets/Refresh`, health ok, EditMode tests 42/42 passed. A follow-up unchanged
  `recompile_scripts` reported 0 warnings and `get_health_report` stayed ok with 0 console errors and
  0 dirty scenes. (Codex)
- `.claude/tools/mcp-doctor.ps1 -WaitSeconds 5`: OK, Unity WebSocket on 127.0.0.1:8090, active scene
  `Market`. `.claude/tools/verify-unity.ps1 -WaitSeconds 5`: recompile success, 0 warnings, health
  ok after retrying transient post-compile WebSocket disconnects. `.claude/tools/verify-unity.ps1
  -Refresh -WaitSeconds 5`: `Assets/Refresh`, recompile, health ok. `.claude/tools/verify-unity.ps1
  -RunTests -WaitSeconds 5`: recompile, health ok, EditMode tests 42/42 passed. (Codex)
- Docs-only cleanup: `rg` found no non-ASCII or trailing whitespace in root project Markdown, and
  `git diff --check` passed for the touched docs. Unity MCP not required; this cleanup changed no
  C#/Unity assets. (Codex)
- NPC multi-stall browse fix MCP `recompile_scripts`: success, 0 warnings. MCP
  `get_health_report`: ok, 0 errors, 0 dirty scenes. (Codex)
- D0 MCP `recompile_scripts`: success, 0 warnings. MCP `get_health_report`: ok, 0 errors,
  0 dirty scenes. `Market.Tests.SaveMigrationTests`: 6/6 passed. (Codex)
- C9 MCP `recompile_scripts`: success, 0 warnings. MCP `get_health_report`: ok, 0 errors,
  0 dirty scenes. (Codex)
- MCP `recompile_scripts`: success, 0 warnings. `get_health_report`: ok (0 errors, 0 dirty scenes).
  Play-mode visual confirmation pending user. (Claude)

## [1.7.1] - 2026-06-13

### Fixed (Claude)
- Settings button in MainMenu no longer shows empty screen. Added `SettingsMenuController`
  MonoBehaviour that builds a centered `SettingsPanelRenderer` on Awake; wired to the pre-existing
  `SettingsPanel` GameObject with `onBack -> MainMenuController.CloseSettings()`.

## [1.7.0] - 2026-06-13

### Added - C6 Settings menu (Claude)
- `SettingsSO` (`Assets/_Project/Data/SettingsSO.asset`) - ScriptableObject with default values for all
  player-configurable settings (mouse sensitivity min/max/default, invert-Y, master/music/sfx volumes).
- `SettingsService` - plain C# service registered in `ServiceLocator` at boot; loads/persists all
  settings via `PlayerPrefs`; fires `LookSettingsChanged` and `VolumesChanged` events.
- `SettingsPanelRenderer` - code-built settings panel: mouse-sensitivity slider (0.02-0.60),
  invert-Y toggle, Master/Music/SFX volume sliders (0-1, shown as %), and interactive key-rebind
  buttons for Interact / Jump / Sprint (Keyboard&Mouse group); rebind overrides saved as JSON to
  `PlayerPrefs`. Volume UI persists values; AudioMixer wiring deferred to C7.
- `GameBootstrap` now creates and registers `SettingsService` before any scene loads; `settingsSO`
  field wired in the Bootstrap scene Inspector.
- `FirstPersonController` loads saved sensitivity + invert-Y on `Awake`, applies binding-override
  JSON, and subscribes to `LookSettingsChanged` in `OnEnable`/`OnDisable` for live updates.
- `PauseMenuController` replaces the settings stub panel with `SettingsPanelRenderer`; references
  wired in the Market scene Inspector (`settingsSO`, `playerController`, `playerInput`).

### Verification
- `recompile_scripts` -> 0 errors, 0 warnings. `get_health_report` -> ok. (Claude)

## [1.6.3] - 2026-06-11

### Changed
- Token-economy hardening of the agent rules (follow-up to v1.6.2, based on measured costs):
  - `AGENTS.md` Token discipline: partial-read rules for `dev_plan_3.md` (Progress section +
    own block only; never the whole 36 KB) and `CHANGELOG.md` (head only + archive-at-30KB policy);
    "a passed gate is final" - re-run only gates invalidated by a later edit; cheap MCP defaults
    (`get_health_report includeTests:false`, console logs without stack traces + small limit,
    `run_tests` failures-only without logs). MCP loop section updated to match. (Claude)
  - `CLAUDE.md`: `unity-csharp-reviewer` scoped - only for non-trivial C# (new logic / economy /
    persistence / NPC / shared systems) with an exact file list + focus areas in the prompt
    (measured cost ~65k tokens/run); trivial diffs are reviewed inline. (Claude)
  - `dev_plan_3.md` and `CHANGELOG.md` got header notes telling agents to read them partially -
    the rule now sits where the file is opened. (Claude)

### Verification
- Docs-only change; no C# touched, MCP loop not required. (Claude)

## [1.6.2] - 2026-06-11

### Changed
- Agent contract docs slimmed and updated for token economy (loaded into every session of both
  agents): `AGENTS.md` 23.4KB->14.7KB, `CLAUDE.md` 5.8KB->3.3KB, `COLLAB.md` 5.1KB->3.2KB (-38%
  total). All substantive rules kept; removed cross-file duplication (collab protocol lived in
  three places, tech stack in two); gotcha numbering preserved as stable ids. (Claude)
- `AGENTS.md` gains: a "Token discipline" section (proportional verification, minimal reads, no
  ritual summaries); the v1.6.1 reality - asmdef layout + "new package deps go into asmdef
  references", the `UiFactory`/`MarketPanelView`/renderer UI pattern for future screens, a Tests
  section (where, how to run, what needs tests), the D-I save-version bump rule; planned Block D/E/H
  ScriptableObjects in the SO contract table; new gotcha 12 (MCP recompile does not import brand-new
  files - run Assets/Refresh first). (Claude)

### Verification
- Docs-only change; no C# touched, MCP loop not required. (Claude)

## [1.6.1] - 2026-06-11

### Changed
- UI refactor, no behavior change: extracted the duplicated code-built-UI helpers from
  `MarketUIController` (936 lines) and `PauseMenuController` into a shared static `UiFactory`
  (`Market.UI`), and split `MarketUIController` into a thin coordinator (~250 lines) plus
  `MarketPanelView` (panel chrome + shared row widgets), `ItemTooltipView`, and plain-C#
  `InventoryPanelRenderer` / `SupplierPanelRenderer` / `StallPanelRenderer`. Scene wiring
  untouched - the scene component, its GUID, and all serialized fields are unchanged. (Claude)
- `dev_plan_3.md`: added **D0 MarketStallRegistry** as an explicit step (the B9 temporary
  single-stall API must be retired before D11/D12 build on it); checkpoints D-I now each require
  a `SaveData.version` bump + migration + EditMode migration test. (Claude)
- Committed the warmed `LiberationSans SDF - Fallback` TMP dynamic-atlas state (runtime-added
  Cyrillic glyphs kept the working tree permanently dirty). (Claude)

### Added
- Assembly definitions: `Market.Runtime` (all gameplay scripts), `Market.Editor`
  (`Scripts/Debug/Editor`, editor-only, references `McpUnity.Editor`), and
  `Market.Tests.EditMode` - makes the plan-step 0.1 "asmdefs" claim true and enables a test
  assembly. (Claude)
- First EditMode tests (17, all green) under `Assets/_Project/Tests/EditMode`: `EconomyTests`
  (PriceCalculator read-through, ItemSO season availability, MoneySystem spend rules),
  `InventoryTests` (add/remove/OnChanged contract), `SaveMigrationTests` (ItemDatabase id/name
  resolution for v1 saves, v1-JSON time defaults, SaveData round-trip), plus a `TestItems`
  SerializedObject factory. (Claude)

### Removed
- Empty leftover script folders `Scripts/Influence` and `Scripts/Outcomes` (+ `.meta`) -
  planning artifacts that match no block of `dev_plan_3.md`. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings. (Claude)
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)
- MCP `run_tests` (EditMode, filter `Market.Tests`): 17/17 passed. (Claude)
- C# review via `unity-csharp-reviewer`: no blocking findings; its one HIGH note (season event
  re-subscribe on re-enable) was re-checked against the code and is a false positive -
  `UnwireSeasonEvents` resets `_seasonEventsWired`, so re-enable re-subscribes. (Claude)

## Older Releases

Entries before 1.6.1 live in CHANGELOG.archive.md.
