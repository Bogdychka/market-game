# Volumetric Clouds URP (vendored)

Volumetric clouds for URP, ported from HDRP, by jiaozi158, MIT licensed - see `LICENSE.md`.

- Upstream: https://github.com/jiaozi158/UnityVolumetricCloudsURP
- Upstream target: Unity 2022.2 / URP 14
- Vendored into: Unity 6000.5 / URP 17.5

Sibling package to `Packages/com.jiaozi158.unity-physically-based-sky-urp` - designed by the same
author to sit under the same sky, and integrated with it here (see "Sky integration" below).

## Scene setup (per upstream docs)

1. Add the **Volumetric Clouds URP** renderer feature to the active URP renderer.
2. Add the **Sky/Volumetric Clouds (URP)** volume override.
3. Set **State** to Enabled.

`Assets/_Project/Scenes/PhysicallyBasedSkyLab.unity`, built by
`Market/Debug/Build Physically Based Sky Lab` (`PhysicallyBasedSkyLabSceneBuilder`), adds this on
top of the Physically Based Sky and OceanURP water already there.

## Changes made to the upstream source

**Ported the "Non Render Graph Pass" fallback out of the Unity 6000.4+ build**, identical reasoning
and fix to the sky package: Unity 6000.4 removed the Render Graph Compatibility
Mode, and upstream only marks the fallback overrides `[Obsolete]` rather than excluding them, which
still fails to compile (CS0115) once the base methods are gone. All three passes in
`VolumetricCloudsURP.cs` (`VolumetricCloudsPass`, `VolumetricCloudsAmbientPass`,
`VolumetricCloudsShadowsPass`) now wrap their `#region Non Render Graph Pass` in
`#if !UNITY_6000_4_OR_NEWER`. Mirrors an upstream PR that fixed the same errors but was closed
unmerged.

**Null-guarded `VolumetricCloudsVolumeEditor.OnEnable`** against the same Unity 6 Play Mode timing
issue documented in the sky package's `README.md` (a momentarily null `target` throwing
`SerializedObjectNotCreatableException`).

**Fixed a vertical-wind bug.** `VolumetricCloudsURP.cs` accumulated the vertical erosion offset
from `erosionSpeedMultiplier` instead of `verticalErosionWindSpeed`:

```csharp
verticalShapeOffset   += deltaTime * cloudsVolume.verticalShapeWindSpeed.value;
verticalErosionOffset += deltaTime * cloudsVolume.erosionSpeedMultiplier.value;  // <- was wrong
```

`erosionSpeedMultiplier` is a 0-1 multiplier that already has a correct home (it is sent to the
shader as `_SmallWindSpeed`), so the effect was that the `Vertical Erosion Wind Speed` slider did
nothing at all while the erosion layer drifted vertically at a fixed 0.25 no matter how the volume
was configured - which also kept rewriting `_VerticalErosionWindDisplacement` in
`VolumetricClouds.mat` and dirtying it in version control.

## Sky integration

Upstream's README says to "install Physically Based Sky via the package manager" to customize the
planet radius and center. That is not optional garnish - it is the only way the integration works,
because this package reaches for the sky by *package path and package name*, not by assembly:

- `VolumetricCloudsURP.asmdef` has a `versionDefines` entry that defines `URP_PBSKY` when a package
  named `com.jiaozi158.unity-physically-based-sky-urp` at version >= 1.0.0 is present.
- `VolumetricClouds.shader`'s two atmosphere-integrated passes declare
  `PackageRequirements { "com.jiaozi158.unity-physically-based-sky-urp": "1.0.0" }` and
  `#include "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/..."`.

So the sky is vendored to `Packages/com.jiaozi158.unity-physically-based-sky-urp/` as an embedded
package rather than into `Assets/`, which satisfies both. `URP_PBSKY` is then defined automatically
- **do not add it to Scripting Define Symbols by hand.** Doing that while the sky sat in `Assets/`
turned on the C# path while ShaderLab still stripped the passes, so the clouds code requested a
non-existent pass by index (`Blitter.BlitCameraTexture(..., pass: 7)`) and crashed the Editor with
`invalid pass index 7 in DrawProcedural` plus an access violation.

## Known limitations

- Everything else is unmodified upstream source - only the changes above were needed to compile and
  run cleanly here.
- **Custom Cloud Map** overrides are upstream WIP.
- **Orthographic cameras are not supported** (upstream limitation).
- **Cloud shadows override the main directional light's cookie** when enabled (upstream behaviour),
  so leave `Shadows` off unless nothing else needs that light's cookie.
