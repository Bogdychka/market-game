# Physically Based Sky URP (vendored)

Physically based sky and precomputed atmospheric scattering for URP, by jiaozi158, MIT licensed -
see `LICENSE.md`.

- Upstream: https://github.com/jiaozi158/UnityPhysicallyBasedSkyURP
- Upstream target: Unity 2022.3 / URP 14
- Vendored into: Unity 6000.5 / URP 17.5

## Scene setup (per upstream README)

1. Add the **Physically Based Sky URP** renderer feature to the active URP renderer.
2. Add the **Sky/Visual Environment (URP)** volume override.
3. Add the **Sky/Physically Based Sky (URP)** volume override.
4. Add the **Sky/Fog (URP)** volume override.

Recommended starting point: Sun Intensity 3 (3.030782), Exposure 0.

`Assets/_Project/Scenes/PhysicallyBasedSkyLab.unity`, built by
`Market/Debug/Build Physically Based Sky Lab` (`PhysicallyBasedSkyLabSceneBuilder`), wires all
four pieces plus the vendored `Assets/OceanURP` water for context.

## Changes made to the upstream source

**Ported the "Non Render Graph Pass" fallback out of the Unity 6000.4+ build.** Unity 6000.4
removed the Render Graph Compatibility Mode, which drops the base-class `OnCameraSetup`/`Execute`
methods these fallback paths override; upstream only marks them `[Obsolete]`, which does not stop
the compiler from erroring on the removed override (CS0115) once those base methods are gone. Each
of the five passes in `Runtime/PhysicallyBasedSkyURP.cs` (`PBSkyPrePass`, `SkyViewLUTPass`,
`AtmosphericScatteringPass`, `PBSkyPostPass`, `AmbientProbePass`) now wraps its `#region Non
Render Graph Pass` in `#if !UNITY_6000_4_OR_NEWER`, leaving only the modern `RecordRenderGraph`
path active on this project's Unity version. This mirrors an upstream PR that fixed the same
CS0115 errors but was closed unmerged.

**Null-guarded the three custom Volume editors' `OnEnable`** (`FogVolumeEditor.cs`,
`PhysicallyBasedSkyEditor.cs`, `VisualEnvironmentVolumeEditor.cs`). On Unity 6, entering Play Mode
can momentarily hand these editors a null `target`, and touching `serializedObject` before that
resolves throws `SerializedObjectNotCreatableException` (editor-only console spam, harmless but
still an unaddressed open upstream issue). Each `OnEnable` now returns immediately if `target` is
null, before any `serializedObject` access.

## Known limitations

- Everything else is unmodified upstream source (including its own `[Obsolete]` markers on the
  fallback passes) - only the two changes above were needed to compile and run cleanly here.
