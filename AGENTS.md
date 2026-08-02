# Market Game - Agent Contract

Coding/architecture rules for `C:\Users\bogre\My project`. All C#/Unity work follows this file.
Don't duplicate the other contracts here:
- **Role, git process, versioning, response rules**: `CLAUDE.md`.
- **Plan + live progress checkboxes**: `dev_plan_4_1.md` (the ONLY progress log; the older
  `dev_plan_3.md` is superseded and archived under `_ArchiveAssets/docs/`).
- **Live state truth**: the open Unity Editor (MCP, port 8090), `game.log`, and serialized
  `.unity`/`.prefab`/`.asset` files.

> One plan step per task. Record what changed in `CHANGELOG.md [Unreleased]`, verify via MCP,
> report green/red. Commit/merge/tag/push only on explicit user instruction.

---

## Token discipline - don't burn context on junk

- Read only what you will edit or must verify. Don't re-read whole files or contracts "to be sure".
  **Never re-read a file you just edited** - `Edit`/`Write` already confirm the change succeeded.
- **`dev_plan_4_1.md` (~61 KB, ~15k tokens): never read it whole.** Progress truth = the `## Progress` section at
  the bottom; task details = the section of YOUR block only. Grep by step id (`C6`, `D8`...).
- **`CHANGELOG.md`: handoffs need only the head** - `[Unreleased]` + the latest release (first
  ~40 lines). Never read the version history. When the file exceeds ~30 KB, move entries older than
  the last 5 releases to `CHANGELOG.archive.md`.
- Diagnose from files: grep the relevant serialized `.unity`/`.prefab`/`.asset` with narrow patterns
  and tail `game.log`. **Never full-Read a scene/prefab - `Grep` the specific component** (`Market.unity`
  is 80 KB; `NPC_Visitor.prefab` is 2300+ lines of embedded rig - dumping it costs ~20k tokens).
- Verification is proportional: a normal change needs `recompile_scripts` + `get_health_report`, done.
  `run_tests` (filter `Market.Tests`) only for shared/risky logic.
- **A passed gate is final.** Never re-run recompile/health/tests on unchanged code "to be sure".
  After a new edit, re-run only the gates that edit invalidated (e.g. a comment-only fix needs
  recompile + health, not the test suite).
- **Cheap MCP calls by default:** **always** `get_health_report` with `includeTests: false` for a
  green/red check - `overallStatus` + `consoleErrors` + `dirtyScenes` already tell you ok/red, and the
  test-name list costs ~3k extra tokens per call (set `true` only to actually discover a specific test);
  `get_console_logs` with `includeStackTrace: false` + small `limit` (10-20); `run_tests` with
  `returnOnlyFailures: true`, `returnWithLogs: false`.
- **Scene edits must be deliberate and cheap.** Before any Unity scene edit, call `get_scene_info`
  and load the intended scene if needed. Prefer one small Editor builder or one precise MCP batch over
  interactive object fiddling. After editing, verify narrowly (`get_gameobject` or targeted `rg`) and
  avoid large scene diffs/dumps. Debug props stay minimal - no polish/material churn unless it matters.
- No progress essays in chat. Results are recorded once, in `CHANGELOG.md [Unreleased]`.

---

## Tech stack

| Area | Rule |
|---|---|
| Unity | `6000.5.3f1` (Unity 6.5), C# 9.0 language level |
| Rendering | URP 17.5.0, **Deferred** (`PC_Renderer.asset`) |
| Input | New Input System 1.19.0; legacy Input **disabled** (`activeInputHandler = 1`) |
| NavMesh | AI Navigation 2.0.13 - `NavMeshSurface`; never the old Navigation-Static workflow |
| Runtime UI | uGUI + TextMeshPro (UI Toolkit = editor tools only) |
| Persistence | JSON in `Application.persistentDataPath` |
| Networking | Netcode for GameObjects - Block N (before specializations E-I); see `dev_plan_4_1.md` |

Modern C# only where Unity's C# 9 compiles and behaves: no `record` for serialized data, no `init`
setters in gameplay data, `var` / target-typed `new()` only when the type stays obvious. Catch
specific exceptions; for I/O/JSON catch broadly, log, return `false`/`null`.

---

## Architecture

All game code/content under `Assets/_Project`; third-party packs stay where they are (don't
restructure); archived packs live in `_ArchiveAssets/` outside `Assets`.

`Scripts/<Subsystem>/` -> `namespace Market.<Subsystem>`:
**Core** (ServiceLocator, EventBus, SceneLoader, SceneNames, TimeSystem, DayPhaseSystem,
MarketOpenSystem, GameBootstrap) - **Player** - **Interaction** - **Economy** (MoneySystem, Inventory,
ItemSO, ItemCategory, ItemDatabase, PriceCalculator, SupplierShop, DailySummarySystem) -
**Market** (MarketStall, StallSlot, MarketStallRegistry) - **NPC** (NPCVisitor, NPCSpawner, NPCTypeSO,
NPCAnimator) - **World** (DaylightSystem, SeasonManager, Season, MoonVisualFactory, CropPlot, CropSO,
CropState) - **UI** - **Persistence** (SaveSystem, GameSaver, SaveData) - **Debug** (`Market.DebugTools`,
temporary) - **Progression** / **Specializations** (reserved for D9+ and Blocks E-H).

**Assemblies (v1.6.1+):** `Market.Runtime` (Scripts root) - `Market.Editor` (`Scripts/Debug/Editor`,
editor-only, references `McpUnity.Editor`) - `Market.Tests.EditMode` (`Assets/_Project/Tests/EditMode`).
A new Unity-package dependency must be added to the asmdef `references` (currently
`Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`) or you get CS0246.

**Services:** plain-C# lifecycle services live in Core and register in `GameBootstrap` (EventBus,
SceneLoader, SaveSystem, TimeSystem, PriceCalculator). MonoBehaviours with scene refs are scene
coordinators (GameSaver, DaylightSystem, SeasonManager, NPCSpawner, MoneySystem, Inventory) wired
via serialized references - never global singletons. Direct Market-scene Play must work:
`ServiceLocator.TryGet<T>()` + a safe local fallback.

**Scene flow:** Bootstrap -> MainMenu -> Market via `ServiceLocator.Get<SceneLoader>().Load(SceneNames.X)`.
Never `SceneManager.LoadScene` from gameplay/UI code.

**Runtime UI layer (v1.6.1+):** UI is built in code. Reuse `Market.UI.UiFactory` (CreateRect /
CreateText / CreateButton / AddImage / StretchToParent / AddLayoutHeight + shared palette).
Market-style screens reuse `MarketPanelView` (panel chrome + info/action rows + item tooltip) with a
plain-C# renderer per panel (`InventoryPanelRenderer`, `SupplierPanelRenderer`, `StallPanelRenderer`).
New screens (Evening Summary, Wishboard, HiringBoard...) follow this pattern - never copy factory
helpers into a controller, never hand-roll rects.

---

## C# rules

### Required
- `namespace Market.<Subsystem>`; PascalCase public members; `_camelCase` private fields.
- Inspector fields: `[SerializeField] private` grouped by `[Header("References|Settings|Tuning|Debug")]`;
  `[Tooltip]` where non-obvious. Never public fields.
- XML docs on public classes and non-trivial public methods. `[RequireComponent]` for same-GO deps.
- Cache components in `Awake()`; serialized-ref checks in `ValidateReferences()` / `Resolve...()`
  helpers called from `Awake()`.
- `OnEnable`/`OnDisable` only for subscriptions - every subscribe has a matching unsubscribe.
- Methods <= ~30 lines / one job. ScriptableObject = data, MonoBehaviour/plain C# = behavior.
- try/catch around all file/JSON I/O: log and return `false`/`null`, never crash gameplay.

### Never
- `FindObjectOfType` - `GameObject.Find` - static MonoBehaviour singletons - public Inspector fields.
- `UnityEngine.Input.*` / `KeyCode` - legacy Input Manager is disabled: it compiles, then throws
  `InvalidOperationException` at runtime. New Input System only (gotcha 9).
- `OnGUI` for runtime UI. Hidden gameplay math the player can't inspect or reason about.
- Heavy `Update()` logic without cached refs and clear justification.
- Unrelated refactors while implementing a requested step.
- **Non-ASCII text ANYWHERE in code and content** - `///` docs, `[Tooltip]`, `//` comments,
  `Debug.Log/*` strings, AND player-visible UI strings / SO defaults (`ItemSO.displayName`,
  `NPCTypeSO.typeName`, season names): all ASCII English. The game UI is English. Russian text
  repeatedly caused encoding corruption (42-file cleanup in v1.3.2; UI strings converted 2026-07);
  localization, if ever needed, will be a dedicated system - never inline non-ASCII literals.

### Performance
- Zero managed allocations per frame in hot paths: no per-frame List/array/string/delegate/closure/
  LINQ/boxing. Reuse collections with `Clear()`; pool frequently spawned objects; prefer non-alloc APIs.
- Update UI text only when data changes; `StringBuilder` for large/looped text.
- Logs are event-based or throttled, never per-frame. Cache shader property IDs in hot code.
- Profile before broad optimization - optimize measured hot paths only.
- Outdoor scenes target 60 FPS with CPU and GPU each below 16 ms in a representative Game view.
  Record a baseline before visual expansion; repeat the same camera/resolution after the change.
  Use `Market/Debug/Benchmark Island Camera Turn` for the repeatable Island 360-degree turn case;
  p95 must stay below 16.67 ms and the maximum must not show visible rotation spikes.
- Terrain defaults are not accepted blindly: enable instanced drawing, never use two-sided Terrain
  shadows, start `heightmapPixelError` at 15, and keep detail/tree/basemap distances bounded by the
  actual playable sightline. Raise quality only after a profiler capture proves headroom.
  For one compact Terrain, disable heightmap LOD frustum culling when the Scene View turn benchmark
  shows patch-entry spikes; large tiled worlds must compare both modes before choosing.
- Screen-sized transparent water renders front faces only and does not use motion vectors or probes.
  Do not sample `_CameraOpaqueTexture` or enable the global URP Opaque Texture for one effect without
  a before/after GPU measurement; prefer depth-only shoreline foam and normal alpha blending.
- Scene builders must serialize their performance settings explicitly. Outdoor scene changes must
  pass the Project Health performance checks so regenerated scenes cannot restore expensive defaults.

---

## Unity 6 patterns

- **Input:** `Keyboard.current[Key.X].wasPressedThisFrame`, `_action.ReadValue<Vector2>()`;
  InputAction callbacks subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **NavMesh:** bake via `NavMeshSurface`; `NavMeshObstacle` with carving for dynamic blockers;
  `NavMesh.SamplePosition()` before `SetDestination()`.
- **URP:** material props `_BaseColor`/`_BaseMap` (set `_Color`/`_MainTex` fallbacks when useful).
  Bright emissive: enable `_EMISSION`, `_EmissionColor` above 1, `globalIlluminationFlags = None`.
  URP/Unlit renders black without `_BaseMap` - set `Texture2D.whiteTexture`. Runtime skybox changes
  need a runtime instance of `RenderSettings.skybox`. Code-driven ambient:
  `RenderSettings.sun = sunLight` + `AmbientMode.Flat`.
- **Async:** coroutines for Unity-dependent timers; `async/await` for I/O; Unity objects on the main
  thread only; no fire-and-forget async without cancellation/error handling.

---

## Project patterns

- Save/load = Collect/Apply split (`CollectInventory(data)` / `ApplyInventory(data)`).
- Long `Update()` splits into helpers (`TickTime()`, `HandleEscape()`); UI controllers use
  `Wire... / Refresh... / Show... / Hide...` helpers.
- Complex runtime construction goes into factories (`MoonVisualFactory`, `UiFactory`).
- NPC state machines: `EnterX`/`UpdateX` methods + one state dispatcher.
- After `Destroy(_x)`, set `_x = null`. Don't duplicate validations - if `MoneySystem.TrySpend`
  already checks balance, just call it.

---

## Economy rules (transparent market)

Base buy/sell prices live in `ItemSO`; the player sets stall sell prices; NPCs buy by budget,
category preference, and concrete stall prices; seasons change *availability* and visuals, never
price. No hidden multipliers (drought / festival / rumor / demand xN) - ever.
`PriceCalculator` is the single price-read point: `GetBuyPrice(item)` / `GetSuggestedSellPrice(item)`.
NPC refusal reasons stay concrete: no stock - category not interesting - price above budget - bought.

---

## ScriptableObject contracts

| SO | Purpose |
|---|---|
| `ItemSO` | id (stable save key), displayName, category, base prices, icon, worldPrefab, seasons |
| `ItemDatabase` | save lookup: `Resolve(id, name)` - id primary, name = legacy fallback |
| `NPCTypeSO` | budget, speed, browse time, preferences, prefab |
| `CropSO` | crop growth data (E block) |
| Planned | `OrderSO` / `NPCPersonalitySO` / `StaffSO` / `AttractionObjectSO` (D), `RecipeSO` (H) - data-only, same pattern |

---

## Persistence

`SaveSystem` = plain C# service - `GameSaver` = scene coordinator - `SaveData` = DTO only.
Item identity restores via `ItemDatabase.Resolve(id, name)`. All file/JSON ops in try/catch.
`Continue` uses `SaveSystem.ShouldLoadOnStart`. Preserve old-save compatibility. Saves are written
atomically (temp + `File.Replace` + `.bak`); `Load` falls back to the backup.
**At each block checkpoint D-I: bump `SaveData.version`, migrate old saves, and extend
`SaveMigrationTests` with the migration case** (required by `dev_plan_4_1.md`).
**Save guardrail:** any new system that owns state which must survive a day boundary or Continue
ships its `Collect`/`Apply` pair, its `SaveData` field, and a `SaveMigrationTests` case **in the same
step** - never "wire persistence later" (this rule is why crops were lost pre-v5; audit C2).

---

## Tests (v1.6.1+)

EditMode tests in `Assets/_Project/Tests/EditMode` (namespace `Market.Tests`, asmdef
`Market.Tests.EditMode`). Use `TestItems` (SerializedObject factory) to build ItemSO/ItemDatabase;
`Object.DestroyImmediate` everything you create. New pure-logic systems (orders, reputation, rating,
wages, unlocks...) ship with tests; UI/scene-wiring code does not need them.
Run via MCP `run_tests`: mode `EditMode`, filter `Market.Tests`.

---

## Debug tooling (`Market.DebugTools`, temporary)

`FileLogger` -> `game.log` - `DebugTimeControl` (PgUp/PgDn speed, `H` +1h, `N` next season) -
`DebugSupplierBuy` (keys 1-5) - `DebugStallPlace` (F3) - `DebugMoneyInput` (F1/F2) -
`MarketAutoDebugger` (F9 loop, F10 one cycle) - manual save F5 - D2/D5 root debug cubes
(Open/Close Market, Sleep Until Morning) - E1 debug crop plot.
Editor scene/asset builders (menu `Market/Debug/...`): `CropE1SceneBuilder`, `CropMaterialUrpUpgrader`,
`StaticPropImportFixer`, `StylizedWaterIslandSceneBuilder`.
Rendering: `Market/Debug/Rendering/Setup Post Processing In Open Scene` ensures the project volume
profile (`Art/PostProcessing/MarketPostFX.asset`) and a global Volume in the open scene. Every
playable scene needs one - the camera renders post processing, but with no Volume in range nothing
is tonemapped. Post-processing AA is **SMAA**: the PC renderer is Deferred, where MSAA does nothing.
Sky look tuning: `Market/Debug/Build Skybox Lab` builds `Scenes/SkyboxLab.unity` (WaterShaderLab
water + BOXOPHOBIC `Skybox/Cubemap Blend` sky); the in-game panel on **F8**
(`SkyboxRuntimeTuner`) tunes `Art/Materials/Skybox/M_SkyboxLab.mat` - cubemap slots, day/night
blend, exposure, tint, rotation, sky fog, sun and ambient.
Water look tuning: `Market/Debug/Water/Stylized Water Tuner` (editor window) and the in-game panel
on **F7** (`StylizedWaterRuntimeTuner`) - same labelled sliders and the same JSON presets under
`Art/Materials/Water/Presets`; the property table lives once in `StylizedWaterShaderCatalog`.
Tune a project copy of the material, never the imported package material.
In-world tuning: `Market/Debug/Water/Build Water Settings Wall` puts a panel of sliders/steppers
next to the lab spawn, operated with the crosshair and LMB (`CrosshairView` + `GazeUiPointer`, a
screen-centre pointer for world-space uGUI). Add a property by adding a row to `WaterWallFields`.
A world-space canvas is read from its **-Z** side - point its forward away from the reader or the
text renders mirrored.
Wave shape for `RealisticWater` is an asset, not shader properties: `WaveProfile` under
`Art/Materials/Water/Profiles` (presets rebuilt by `Market/Debug/Water/Create Preset Wave
Profiles`), authored in `Market/Debug/Water/Wave Creation Wizard`, uploaded by `WaveProfileBinder`
on the water object. The math lives once in `Art/Shaders/RealisticWaterWaves.hlsl` and is mirrored
in `Market.World.WaveSampler` - edit the two together, and read wave height from the sampler rather
than adding a fourth copy.
WaterWorks (GapperGames SSR water) was evaluated and rejected - `RealisticWater.shader` beats it on
waves, absorption, refraction, reflection and foam, and it has no distance fade on its micro
normals, which is why it boils at range. `Market/Debug/Water/Build WaterWorks Lab` still rebuilds
the evaluation scene (F6 panel) if it needs re-checking; it puts its full-screen underwater blit on
its own renderer asset - never add that feature to `PC_Renderer`.
Shader compiler errors do not reach the MCP console bridge: use
`Market/Debug/Inspect Selected Shader Errors` after touching any shader.
Seeing a shader result: **Shader Vision**, see its own section below.
Remove each one once real UI covers it. Play Mode issues -> check `game.log` and serialized scene
values before guessing.

---

## Shader Vision - look at the shader, don't guess

Never claim a shader/material/lighting change "looks better" without a capture. Runs write PNGs +
`report.json` to `Artifacts/ShaderVision/<outputName>/` (git-ignored); **read `sheet.png`** - it is
one image holding every pose or sweep value, with the label burned into each cell.

```powershell
powershell -File .claude/tools/shader-vision.ps1 -SceneView          # one shot of the current Scene view
powershell -File .claude/tools/shader-vision.ps1 water-lab           # preset: 6 fixed poses
powershell -File .claude/tools/shader-vision.ps1 water-lab -CompareRun water-lab   # A/B vs the previous run
```

Presets: `water-lab`, `water-foam-sweep`, `grass-lab`. Full job schema (poses, turntable, sun,
overrides, sweep, time samples): `.claude/shader-vision/README.md`. A job file is cheap - write a
new one for a new scene instead of forcing an existing preset.

**The A/B loop is the point.** Capture -> edit the shader -> capture with `-CompareRun <same name>`.
Poses, sun and the shader clock are pinned, so two runs of an unchanged scene are bit-identical and
`changed 0.0%` means the edit did nothing - check that before writing a paragraph about why it
looks better. Changed pixels also come out as `diff_<pose>.png`, which shows *where*.

**Tuning a number** is a `sweep`, not six edit-and-look cycles: one pose, N values of one property,
one sheet. All cells measuring identically means the property is inert on that material (wrong
property, or a branch disables it) - a finding worth one run.

Read the numbers, not just the picture: `nonFinitePct` > 0 = NaN in the shader, `magentaPct` > 0 =
Unity's error shader, `clippedPct` high = blown out, `detail` dropping = micro-normals/foam lost.

Constraints: Edit Mode only, max 24 cells per run, and the capture camera is built by the tool -
put poses in the job file, don't hand-place a camera in the scene.

---

## Serialized state debugging

Unity behavior often lives in Inspector values, prefab overrides, and SO assets - not C#.
Before blaming code: `rg "ScriptName|fieldName|guid:" Assets/_Project -g "*.unity" -g "*.prefab"
-g "*.asset" -g "*.meta"`, map the script GUID from its `.cs.meta`, compare serialized values to
code defaults, and fix the wiring when that is the truth. Logs: `Get-Content .\game.log -Tail 200`.
Note: Unity escapes non-ASCII in YAML as `\uXXXX` - grep for `\\u04` to find any stray Cyrillic.

---

## MCP Unity verification

Fast local helpers live in `.claude/tools/`:
- `verify-unity.ps1` = one-command loop: MCP doctor -> optional `Assets/Refresh` -> `recompile_scripts`
  -> `get_health_report includeTests:false` -> optional EditMode tests. It retries transient WebSocket
  disconnects after compilation/domain reload.
- `mcp-doctor.ps1` = checks Node, MCP package server files, `ws`, settings, Unity process, TCP 8090,
  and active scene.
- `check-mcp-unity.ps1` is a compatibility wrapper around `mcp-doctor.ps1`.

Loop: `recompile_scripts` -> `get_health_report` (`includeTests: false`; must be `ok`, 0 errors) ->
`get_console_logs` (`includeStackTrace: false`, small `limit`) when needed -> `run_tests`
(filter `Market.Tests`, `returnOnlyFailures`) for shared/risky logic. A passed gate is final -
re-run only what a later edit invalidated.

**Play mode:** the bridge stays connected through the whole Play Mode cycle - no 4001 drop, no
dead window (measured: play + status round trip 713 ms, was ~4.8 s of nothing). This depends on
**Enter Play Mode Options with the domain reload disabled**
(`Market/Debug/MCP/Enable Fast Play Mode (no domain reload)`; `.../Log Play Mode Options` prints
the current state, `.../Restore Domain Reload On Play` reverts). Without the reload the server
object survives the transition, and `McpUnityServer.OnPlayModeStateChanged` only closes clients
when a reload is actually coming (local patch to the vendored package).
**The cost: statics no longer reset between Play sessions.** Every static holder must reset itself
in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` - done for
`ServiceLocator`, `FileLogger`, `GameBootstrap`, `GrassTrample`. Add the same to any new static
state, or it leaks from one Play session into the next.
`unity-ws-call.mjs` also waits out a bridge restart (`UNITY_RECONNECT_WINDOW_MS`, default 30 s)
for recompiles and manual restarts - but only while the request is still unsent; one that was sent
before the drop is reported as "may or may not have run" rather than silently retried.

WS fallback if MCP transport is closed (from project root; same pattern for any tool):

```powershell
@'
{"returnWithLogs":true,"logsLimit":120}
'@ | node .claude/tools/unity-ws-call.mjs recompile_scripts - --timeout 120000
```

`ECONNREFUSED 127.0.0.1:8090` -> Unity is closed or the MCP server window is offline.
Helper scripts live in `.claude/tools/` (`check-mcp-unity.ps1`, `start-mcp-unity.ps1`,
`unity-ws-call.mjs`).

### Gotchas (hard-won; numbers are stable ids - keep them)

1. `run_tests` fills the console with expected `McpUnity.Tests` negative-case logs; a post-test
   `attention` health caused only by those is NOT a project error. Never write an Editor script to
   "clean" the console.
2. Don't over-verify - see Token discipline. Recompile + health is enough for a normal change.
3. Ping the Editor at task start; if it's closed, pick a strategy immediately, not mid-task.
4. MCP-driven Play Mode does NOT tick time/animations like a focused player. Advance time explicitly
   (`TimeSystem.SkipHours`) - "time isn't moving under MCP" is not a gameplay bug.
5. Prefer MCP `update_component` over hand-writing scene YAML; manual YAML only for object-reference
   wiring or inactive scenes, and run `git diff --check` *before* committing scene edits.
   Before creating/moving objects, confirm the active scene with `get_scene_info`; if the scene is
   wrong, `load_scene` first. For new debug/scene props, prefer a tiny Editor builder that creates the
   exact root objects once, then inspect those objects directly instead of poking around repeatedly.
6. A new `[SerializeField]` field does NOT backfill into already-serialized scenes - set it
   explicitly (`update_component` / `save_scene`) or it silently stays `null`/`false`.
7. A new `.cs` under `Packages/...` needs its `.meta` + `AssetDatabase.Refresh` before the class exists.
8. ASCII English everywhere - code text AND UI strings/SO defaults (see Never). No exceptions.
9. Never `UnityEngine.Input.*` - disabled legacy Input compiles, then throws at runtime
   (5000+ exceptions/session for a per-frame call; bit v1.4.0's tooltip).
10. Rect clipping = `RectMask2D`, never legacy `Mask` with an alpha-0 graphic: the culled graphic
    never writes the stencil, so ALL masked children render invisible while staying clickable
    (hid every C3 list row; fixed v1.5.4).
11. Code-built UI: set stretch anchors BEFORE `offsetMin`/`offsetMax` - collapsed anchors + offsets
    = zero-size rect that never renders (hid the price input; fixed v1.5.6).
    `UiFactory.StretchToParent` does it right.
12. MCP `recompile_scripts` does NOT import brand-new files (no `.meta` yet). CS0246 against your own
    new class -> run `execute_menu_item` `Assets/Refresh` first, then recompile.
13. **Every MCP call timing out while the Unity process burns no CPU = a modal dialog, not a dead
    bridge.** Editing a scene/asset file on disk while the Editor has it open raises "The open
    scene(s) have been modified externally" and blocks everything until it is answered. Handle it:
    `powershell -File .claude/tools/unity-dialog.ps1` lists open dialogs, `-Action Accept
    -TitlePattern 'modified externally'` presses the default button (Reload), `-Action Cancel`
    presses Escape. Unity dialogs are IMGUI, so UI Automation cannot see their buttons and
    `SetForegroundWindow` alone loses the keystroke - the script focuses via `AppActivate` first.
    Prefer MCP `update_component` over hand-editing an open scene so the dialog never appears.

---

## Available assets

| Pack | Path | Use |
|---|---|---|
| Kenney Food Kit | `Assets/kenney_food-kit/Models/FBX format/` | ~200 item models |
| Cartoon Farm Crops | `Assets/Cartoon_Farm_Crops/Prefabs/` | Crop growth stages + Dirt_Pile (convert materials to URP/Lit) |
| Quaternius Farm Animals | `Assets/Farm Animals Animated by Quaternius/FBX/` | Livestock |
| Quaternius Farm Buildings | `Assets/Farm Buildings by Quaternius/FBX/` | Farm structures |
| Quaternius Fish Pack | `Assets/Fish Pack Animated by Quaternius/FBX/` | Fish items |
| Textured Stylized Trees | `Assets/Textured Stylized Trees - May 2020/.../FBX/` | Decoration |
| UAL Standard | `Assets/Universal Animation Library[Standard]/.../Unity/` | NPC rig |
| Mixamo | `Assets/Mixamo_animations/` | NPC animations |

Per-step availability tags (`[assets: ready/stub/backlog]`) live in `dev_plan_4_1.md`.
