# Volumetric Clouds URP (vendored)

Volumetric clouds for URP, ported from HDRP, by jiaozi158, MIT licensed - see `LICENSE.md`.

- Upstream: https://github.com/jiaozi158/UnityVolumetricCloudsURP
- Upstream target: Unity 2022.2 / URP 14
- Vendored into: Unity 6000.5 / URP 17.5

Sibling package to `Assets/PhysicallyBasedSkyURP` - designed by the same author to sit under the
same sky.

## Scene setup (per upstream docs)

1. Add the **Volumetric Clouds URP** renderer feature to the active URP renderer.
2. Add the **Sky/Volumetric Clouds (URP)** volume override.
3. Set **State** to Enabled.

`Assets/_Project/Scenes/PhysicallyBasedSkyLab.unity`, built by
`Market/Debug/Build Physically Based Sky Lab` (`PhysicallyBasedSkyLabSceneBuilder`), adds this on
top of the Physically Based Sky and OceanURP water already there.

## Changes made to the upstream source

**Ported the "Non Render Graph Pass" fallback out of the Unity 6000.4+ build**, identical reasoning
and fix to `Assets/PhysicallyBasedSkyURP`: Unity 6000.4 removed the Render Graph Compatibility
Mode, and upstream only marks the fallback overrides `[Obsolete]` rather than excluding them, which
still fails to compile (CS0115) once the base methods are gone. All three passes in
`VolumetricCloudsURP.cs` (`VolumetricCloudsPass`, `VolumetricCloudsAmbientPass`,
`VolumetricCloudsShadowsPass`) now wrap their `#region Non Render Graph Pass` in
`#if !UNITY_6000_4_OR_NEWER`. Mirrors an upstream PR that fixed the same errors but was closed
unmerged.

**Null-guarded `VolumetricCloudsVolumeEditor.OnEnable`** against the same Unity 6 Play Mode timing
issue documented in `Assets/PhysicallyBasedSkyURP/README.md` (a momentarily null `target` throwing
`SerializedObjectNotCreatableException`).

## Known limitations

- Everything else is unmodified upstream source - only the changes above were needed to compile and
  run cleanly here.
- **`URP_PBSKY` must stay undefined - do not add it to Scripting Define Symbols.** This package and
  `PhysicallyBasedSkyURP` gate their cross-package code (clouds reading the sky's planet
  radius/center, plus an atmosphere-integrated cloud "Combine" shader pass) behind a Package
  Manager version-define keyed on a package named `com.jiaozi158.unity-physically-based-sky-urp`.
  Setting `URP_PBSKY` manually only flips the *C#* side of that check - it cannot make the gated
  shader pass exist, because `VolumetricClouds.shader`'s atmosphere-combine passes carry their own
  `PackageRequirements { "com.jiaozi158.unity-physically-based-sky-urp": "1.0.0" }` directive,
  which ShaderLab strips at compile time unless that literal package is installed. With the C#
  define on and the shader pass missing, the atmospheric-scattering code path in
  `VolumetricCloudsURP.cs` requests that stripped pass by index (`Blitter.BlitCameraTexture(...,
  pass: 7)`), which threw `invalid pass index 7 in DrawProcedural` and crashed the Unity Editor
  (access violation) when this was tried. Sky and clouds still render together fine without the
  define - they just don't share the one shader pass that would otherwise blend clouds through the
  sky's precomputed atmosphere at the pixel level. Fixing this for real means re-vendoring
  `PhysicallyBasedSkyURP` as an embedded package under `Packages/com.jiaozi158.unity-physically-
  based-sky-urp/` instead of a plain `Assets/` folder - not done here.
