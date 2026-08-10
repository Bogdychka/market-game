# Ocean URP (vendored)

FFT ocean for URP by Ivan Pensionerov (gasgiant), MIT licensed - see `LICENSE.md`.

- Upstream: https://github.com/gasgiant/Ocean-URP
- Upstream target: Unity 2020.3 / URP 10.7
- Vendored into: Unity 6000.5 / URP 17.5

Upstream is explicitly unfinished; the author published it because he stopped having time to
continue. Treat it as a working base to build on, not a finished asset.

## What it gives us over the prototype water

Compute-shader FFT simulation (four cascades, 256^2, mipped displacement + turbulence),
geoclipmap surface mesh with geomorphing, a rewritten surface shader with Bruneton-style
lighting, and a screen-space underwater volume effect.

## Scene

`Assets/_Project/Scenes/OceanURPLab.unity`, rebuilt by
`Market/Debug/Build Ocean URP Lab` (`OceanUrpLabSceneBuilder`).

Shader compile check: `Market/Debug/Water/Inspect Ocean URP Shader Errors`.

## Pipeline requirements

The lab camera renders through `Assets/Settings/Ocean_Renderer.asset`, which carries the
`Ocean` renderer feature. It is appended to both pipeline assets at index 1, so gameplay
scenes (renderer index 0) are unaffected.

`PC_RPAsset` must have **Depth Texture** on, **Opaque Texture** on, and **Opaque Downsampling
= None** - without these, refraction either does nothing or produces coloured fringes along
object edges. The builder sets all three. `Mobile_RPAsset` deliberately keeps them off; the
lab is not meant to run on that quality level.

`Ocean_Renderer` also copies depth **after opaques** rather than after transparents, because
the ocean is drawn at `BeforeRenderingTransparents` and samples the depth copy.

## Changes made to the upstream source

**Ported to the Unity 6 Render Graph API.** URP 17 removed the compatibility path, so
`OceanRenderingPasses.cs` was rewritten: the three original passes (sky map, underwater
effect, ocean geometry) all ran at `BeforeRenderingTransparents` and are now recorded by a
single `OceanRenderPass`. That lets the camera-submergence texture reach the surface shader
as a graph-tracked global instead of a temporary RT assumed to survive between passes. The
inverse view/projection globals are now written before the underwater pass rather than after
it, so the fog no longer reconstructs positions from the previous frame's camera.

**Dropped the MarkupAttributes dependency.** Upstream pulls a second git package purely for
inspector layout. Its `__ApplyMarkupAttributes.cs` registers a `[CustomEditor]` for *every*
`MonoBehaviour` and `ScriptableObject`, which would have taken over every inspector in this
project. Attributes were stripped (serialized fields and therefore all preset assets are
unchanged) and the four editors that derived from `MarkedUpEditor` now use the default
inspector plus their own extras. Grouping is approximated with `[Header]`.

**Fixed a wind-force bug.** `OceanSimulationInputsProvider.PopulateInputs(target, windForce01)`
overwrote its argument with the inspector preview value on the first line, so the scene's wind
slider was ignored. Removed.

**Dropped dead code**: `ShoreMap` and its editor/shader (the shore map include is not
referenced by any shader, its `tex2Dlod` on a `TEXTURE2D` would not compile, and the texture
the sample asset points at is not in the repo), plus the shore-map baking shaders.

**Shader fix**: `OceanSurface.hlsl` redeclared `_CameraDepthTexture_TexelSize`, which URP 17
already declares. Removed the `#pragma exclude_renderers gles` line, since `gles` is no longer
a renderer Unity 6 knows.

**Fixed black sky reflections (a bug in the Render Graph port above, not upstream).** The sky map
pass was the one piece left on the legacy path: it drew with `CommandBuffer.Blit(null, rt, mat, 0)`
inside a render graph unsafe pass, and `StereographicSky.shader` still used `BasicFullscreenVert`,
the vertex entry that reads `POSITION`/`TEXCOORD0` from a mesh. A legacy `Blit` inside a render
graph pass draws nothing and reports no error, so `Ocean_SkyMap` stayed cleared to black - and
since `Reflection()` reads the whole sky through `MeanSkyRadiance(Ocean_SkyMap, ...)`, the water
reflected nothing at all. Only the analytic `Specular()` sun survived, which is what the surface
looked like: black mirror plus a sun glitter path. The pass is now a raster pass over an imported
`RTHandle` drawing the same procedural quad as the other passes here, the shader uses
`ProceduralFullscreenVert`, and an unsafe pass after it calls `GenerateMips` (the map is sampled
with `SampleGrad`, so the mip chain carries surface roughness; rendering into mip 0 does not
trigger the texture's own auto-generation). `Ocean_SkyMap` is bound as an engine global once at
creation rather than per pass, because a render-graph global is reset when the graph finishes.
Measured in Play Mode in the sky lab: the map's average colour went from `(0, 0, 0)` to
`(0.14, 0.20, 0.26)`.

Note for future debugging: `Ocean_SpecCube_HDR` looks unset - nothing in C# writes it - but Unity
does fill in `<name>_HDR` for global cubemaps, and it measures `(1, 1, 0, 0)` at runtime. It is not
a bug. Likewise `ReflectionProbe.defaultTexture` *does* follow
`RenderSettings.customReflectionTexture`, so upstream's `Default` reflections mode already picks up
a dynamic sky's cubemap; both were measured, and neither needed a change.

**Preset**: `SimulationSettings.asset` has foam simulation enabled (upstream shipped it off).

## Known limitations

- The fullscreen passes draw with `MeshTopology.Quads`, which is a D3D-only topology. Fine for
  this project's Windows target; it would need converting to triangles for Vulkan/Metal.
- `OceanRenderer.ConfigureMaterial` writes `_Cull` to the shared material every frame, so the
  material asset can show up as modified after entering play mode.
- Wave-height readback for buoyancy (`OceanCollision`) is wired but nothing in this project
  consumes it yet; `SimulationSettings` has readback set to None.
