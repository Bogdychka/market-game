# Unity Water Shader Guides — Research Notes

Curated notes from experienced Unity developers on building water shaders. Compiled 2026-07-27
from public tutorials/articles (not project-integrated — water is not currently in
`dev_plan_4_1.md`; keep this as reference for if/when a pond/irrigation feature is scoped).

Two tracks below: **stylized/toon water** (URP, Shader Graph or hand-written — closest fit for
this game's cartoon farm art style) and **realistic/ocean water** (Gerstner waves, HDRP). A
performance section covers mobile/low-end pitfalls.

## 1. Depth-based color — the foundation of almost every water shader

Nearly every guide starts here: sample `_CameraDepthTexture` / Scene Depth node, convert to
linear world units with `LinearEyeDepth()`, and take the difference between the water surface's
own depth and the depth of whatever is behind it (the lake bed, a rock, the shore).

```
depthDifference = LinearEyeDepth(sceneDepth) - LinearEyeDepth(surfaceDepth)
depth01 = saturate(depthDifference / _DepthMaxDistance)
color = lerp(shallowColor, deepColor, depth01)
```

This single value (`depth01`) then drives color, opacity, refraction strength, and foam — it's
the backbone value the rest of the shader reads from.
[roystan.net — Toon Water Shader](https://roystan.net/articles/toon-water/),
[ameye.dev — Stylized Water Shader](https://ameye.dev/notes/stylized-water-shader/)

Requires: URP asset with **Depth Texture** enabled (and **Opaque Texture** for refraction).

## 2. Shoreline / intersection foam

Two related but distinct foam techniques, often combined:

- **Shoreline foam (depth-based):** thin foam band where the shore depth difference is small.
  Cheap, but on its own looks wrong around submerged objects — foam appears too thick near flat
  shorelines and too thin around steep submerged geometry, because it only reacts to the *scalar*
  depth difference, not surface shape.
- **Intersection foam (normal-aware):** roystan.net's fix samples a second buffer,
  `_CameraNormalsTexture` (rendered via a custom "normals replacement" shader pass), and compares
  the water's normal against the scene's normal with a dot product. Steep surfaces (rocks) get a
  different foam falloff distance than flat ones (sand), which reads correctly at any geometry
  angle.

```
normalDot = saturate(dot(existingSceneNormal, viewNormal))
foamDistance = lerp(_FoamMinDistance, _FoamMaxDistance, normalDot)
```

Animated variants exist too — halisavakis.dev drives foam with a sine wave so lines of foam
visibly travel toward shore instead of sitting static:

```
foam = step(foamDiff - sin((foamDiff - _Time.y) * 8 * PI) * (1 - foamDiff), noiseTex)
```

Sources: [roystan.net](https://roystan.net/articles/toon-water/),
[halisavakis.com](https://halisavakis.com/my-take-on-shaders-stylized-water-shader/)

## 3. Surface waves / ripples

Three tiers of complexity, pick based on budget and how "real" the water needs to look:

1. **Scrolling normal/noise textures** — cheapest. Sample 1–2 tiling noise or normal maps panning
   in different directions/speeds, blend them. Good enough for small ponds, puddles, troughs.
2. **Flow maps** — a texture encodes per-pixel flow direction (not just a fixed scroll direction),
   so water visibly follows a riverbed or drains toward a specific point.
   [danielilett.com — Stylised Water in Shader Graph and URP](https://danielilett.com/2020-04-05-tut5-3-urp-stylised-water/)
   walks through this (Wind Waker-style): flow speed divided by tiling size, multiplied by time,
   added to UVs — "as size increases, flow amount should decrease" to avoid excessive distortion.
3. **Vertex displacement (sine / Gerstner waves)** — actually moves the mesh, not just the
   texture, so silhouettes and large-scale motion read correctly (needed once the water is large
   enough or the camera gets close/low).

## 4. Gerstner waves (vertex displacement math)

The authoritative walkthrough is Catlike Coding's Flow/Waves tutorial. Key progression:

**Sine wave** (simple vertical bob, adequate for small ripples):
```
f = k * (x - c * t)              // k = 2π / wavelength, c = phase speed
p.y = amplitude * sin(f)
```
Tangent for normal recomputation: `T = normalize([1, k * amplitude * cos(f), 0])`.

**Gerstner wave** (points orbit an anchor instead of moving purely vertically — matches real
water motion, and importantly doesn't loop/self-intersect the surface at high amplitude):
```
f = k * (x - c * t)
P = [x + (steepness/k) * cos(f), (steepness/k) * sin(f), z]
```
`steepness` (0–1) replaces amplitude directly, preventing the wave crest from folding over itself.
Phase speed derives from gravity so wavelength and speed stay physically consistent instead of
being hand-tuned separately: `c = sqrt(9.8 / k)`.

**Multi-directional + summed waves:** give each wave a direction vector `D = [Dx, Dz]`, so
`f = k * (dot(D, [x, z]) - c * t)`, then sum 3+ waves (each with its own direction/steepness/
wavelength) and accumulate their tangent/binormal contributions before normalizing the final
normal — this is what turns a single uniform ripple into something that reads as "ocean."

[catlikecoding.com — Waves](https://catlikecoding.com/unity/tutorials/flow/waves/) has the full
derivation and downloadable project; worth reading directly if implementing this, since the
normal-recalculation math (tangent × binormal per wave) is easy to get subtly wrong from a
summary alone.

## 5. Refraction, reflection, caustics

- **Refraction:** sample the opaque/color texture at a UV offset derived from the water's normal
  map (`screenUV + normal.xy * _RefractionStrength`), then blend with the depth-based color. This
  is the "see-through, slightly distorted" look. **Mobile caveat:** GrabPass-based refraction
  measurably hurts perf on several mobile GPUs — prefer the URP opaque texture (single sample,
  no extra pass) over a raw GrabPass, and disable/simplify refraction entirely in mobile shader
  variants if targeting low-end hardware.
- **Reflection:** cheap options are a reflection probe or a simple Fresnel-blended skybox
  sample; planar reflections (real-time mirrored camera) look best but cost a second render pass
  — usually reserved for hero water bodies, not background ponds.
- **Fresnel:** `pow(1 - saturate(dot(normal, viewDir)), power)` — used both to fade reflection
  intensity at grazing angles and to fade alpha/transparency near-vertical vs. straight-down.
- **Caustics:** a scrolling/animated caustics texture projected onto the lake bed and nearby
  submerged geometry, masked by the depth term so it only appears in shallow, lit areas. Covered
  in [ameye.dev's stylized water shader notes](https://ameye.dev/notes/stylized-water-shader/)
  alongside the rest of the depth/foam/refraction/reflection/caustics/Gerstner stack — it's the
  single most complete "build it all in Shader Graph" reference of this set and worth reading
  end-to-end if committing to this feature.

## 6. Realistic / large-scale ocean (Unity 6 HDRP built-in Water System)

If the target is a genuinely large, realistic ocean/lake/river and the project is on HDRP (not
applicable to this URP project today, but useful context), Unity 6 ships a first-party Water
System rather than requiring a hand-rolled shader:

- Three surface types: **Pool**, **River**, **Ocean/Sea/Lake** — the latter two support
  multi-band simulation (Ripples / Agitation / Swells) driven by wind and current settings, built
  on FFT wave simulation running on the GPU.
- GPU tessellation subdivides the mesh near the camera for fine ripple detail without paying that
  cost far away.
- The simulation can mirror to the CPU so gameplay code can sample water height/current at a
  world position — this is how boats/floating objects get real buoyancy without a separate wave
  system.
- Known incompatibilities: no baked lighting on water surfaces, no MSAA with water, water doesn't
  occlude lens flares, and water can receive ray-traced reflections but can't contribute to them.

[Unity — new HDRP Water System in 2022 LTS/2023.1](https://unity.com/blog/engine-platform/new-hdrp-water-system-in-2022-lts-and-2023-1),
[HDRP manual — Water System capabilities](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/WaterSystem-Overview.html)

For URP/hand-rolled Gerstner-wave ocean + buoyancy, see the community
[Gerstner-Waves-in-Unity](https://github.com/belzecue/Gerstner-Waves-in-Unity) reference
implementation, which also documents CPU-side height sampling for buoyancy.

## 7. Performance / mobile pitfalls (applies to any platform with a fillrate budget)

- Water is usually **fillrate-bound**, not vertex-bound — the shader runs per-pixel over a large
  screen area, so cutting per-pixel math matters more than cutting vertex count.
- Avoid expensive transcendental math (`pow`, `sin`, `cos`, `exp`, `log`) in the fragment shader
  where a lookup texture or a cheaper approximation would do.
- Use `half` instead of `float` wherever full precision isn't needed — mobile GPUs process half
  substantially faster.
- Skip GrabPass-based refraction on mobile variants (see §5); prefer URP's opaque texture, or cut
  refraction entirely for a lower quality tier.
- Bake what you can: static caustics, precomputed foam masks, and fixed reflection probes cost
  nothing at runtime compared to real-time equivalents.
- If shipping multiple quality tiers, gate Gerstner wave count, refraction, and planar reflection
  behind a quality setting rather than one shader trying to do everything everywhere.

Source: [Unity Learn — Optimizing Shaders for Mobile Platforms](https://learn.unity.com/tutorial/optimizing-shaders-for-mobile-platforms-5216)

## Source list

- [ameye.dev — Stylized Water Shader](https://ameye.dev/notes/stylized-water-shader/) — most
  complete single reference (color, foam, refraction, reflection, caustics, Gerstner waves) in
  Shader Graph / URP.
- [roystan.net — Toon Water Shader Tutorial](https://roystan.net/articles/toon-water/) — depth +
  normals-buffer foam, hand-written CG, very precise about the "foam looks wrong near objects"
  problem and its fix.
- [halisavakis.com — My take on shaders: Stylized water shader](https://halisavakis.com/my-take-on-shaders-stylized-water-shader/) —
  animated shoreline foam via sine-driven step, ripple render-texture technique.
- [catlikecoding.com — Waves](https://catlikecoding.com/unity/tutorials/flow/waves/) — the
  canonical sine-wave → Gerstner-wave derivation with full math.
- [danielilett.com — Stylised Water in Shader Graph and URP](https://danielilett.com/2020-04-05-tut5-3-urp-stylised-water/) —
  Wind Waker-style flow maps, Voronoi foam, intersection foam via scene depth, vertex-displaced
  choppiness; includes a concrete perf number (~400 fps, 1024 water meshes, GTX 1070).
- [Unity — new HDRP Water System (2022 LTS / 2023.1)](https://unity.com/blog/engine-platform/new-hdrp-water-system-in-2022-lts-and-2023-1)
  and [HDRP manual — Water System overview](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/WaterSystem-Overview.html) —
  official large-scale/realistic ocean system (HDRP only).
- [Gerstner-Waves-in-Unity (GitHub)](https://github.com/belzecue/Gerstner-Waves-in-Unity) —
  open-source Gerstner wave shader + CPU height sampling for buoyancy.
- [Unity Learn — Optimizing Shaders for Mobile Platforms](https://learn.unity.com/tutorial/optimizing-shaders-for-mobile-platforms-5216) —
  general shader perf guidance, directly applicable to water's fillrate cost.
