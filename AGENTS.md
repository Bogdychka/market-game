# Market Game — Agent Contract

Coding/architecture rules for `C:\Users\bogre\My project`. All C#/Unity work follows this file.
Don't duplicate the other contracts here:
- **Role, git process, versioning, response rules**: `CLAUDE.md`.
- **Plan + live progress checkboxes**: `dev_plan_3.md` (the ONLY progress log).
- **Live state truth**: the open Unity Editor (MCP, port 8090), `game.log`, and serialized
  `.unity`/`.prefab`/`.asset` files.

> One plan step per task. Record what changed in `CHANGELOG.md [Unreleased]`, verify via MCP,
> report green/red. Commit/merge/tag/push only on explicit user instruction.

---

## Token discipline — don't burn context on junk

- Read only what you will edit or must verify. Don't re-read whole files or contracts "to be sure".
  **Never re-read a file you just edited** — `Edit`/`Write` already confirm the change succeeded.
- **`dev_plan_3.md` (36 KB): never read it whole.** Progress truth = the `## Progress` section at
  the bottom; task details = the section of YOUR block only. Grep by step id (`C6`, `D8`…).
- **`CHANGELOG.md`: handoffs need only the head** — `[Unreleased]` + the latest release (first
  ~40 lines). Never read the version history. When the file exceeds ~30 KB, move entries older than
  the last 5 releases to `CHANGELOG.archive.md`.
- Diagnose from files: grep the relevant serialized `.unity`/`.prefab`/`.asset` with narrow patterns
  and tail `game.log`. **Never full-Read a scene/prefab — `Grep` the specific component** (`Market.unity`
  is 80 KB; `NPC_Visitor.prefab` is 2300+ lines of embedded rig — dumping it costs ~20k tokens).
- Verification is proportional: a normal change needs `recompile_scripts` + `get_health_report`, done.
  `run_tests` (filter `Market.Tests`) only for shared/risky logic.
- **A passed gate is final.** Never re-run recompile/health/tests on unchanged code "to be sure".
  After a new edit, re-run only the gates that edit invalidated (e.g. a comment-only fix needs
  recompile + health, not the test suite).
- **Cheap MCP calls by default:** **always** `get_health_report` with `includeTests: false` for a
  green/red check — `overallStatus` + `consoleErrors` + `dirtyScenes` already tell you ok/red, and the
  test-name list costs ~3k extra tokens per call (set `true` only to actually discover a specific test);
  `get_console_logs` with `includeStackTrace: false` + small `limit` (10–20); `run_tests` with
  `returnOnlyFailures: true`, `returnWithLogs: false`.
- **Scene edits must be deliberate and cheap.** Before any Unity scene edit, call `get_scene_info`
  and load the intended scene if needed. Prefer one small Editor builder or one precise MCP batch over
  interactive object fiddling. After editing, verify narrowly (`get_gameobject` or targeted `rg`) and
  avoid large scene diffs/dumps. Debug props stay minimal — no polish/material churn unless it matters.
- No progress essays in chat. Results are recorded once, in `CHANGELOG.md [Unreleased]`.

---

## Tech stack

| Area | Rule |
|---|---|
| Unity | `6000.4.8f1` (Unity 6.4), C# 9.0 language level |
| Rendering | URP 17.4.0, **Deferred** (`PC_Renderer.asset`) |
| Input | New Input System 1.19.0; legacy Input **disabled** (`activeInputHandler = 1`) |
| NavMesh | AI Navigation 2.0.12 — `NavMeshSurface`; never the old Navigation-Static workflow |
| Runtime UI | uGUI + TextMeshPro (UI Toolkit = editor tools only) |
| Persistence | JSON in `Application.persistentDataPath` |
| Networking | Netcode for GameObjects — Block J only |

Modern C# only where Unity's C# 9 compiles and behaves: no `record` for serialized data, no `init`
setters in gameplay data, `var` / target-typed `new()` only when the type stays obvious. Catch
specific exceptions; for I/O/JSON catch broadly, log, return `false`/`null`.

---

## Architecture

All game code/content under `Assets/_Project`; third-party packs stay where they are (don't
restructure); archived packs live in `_ArchiveAssets/` outside `Assets`.

`Scripts/<Subsystem>/` → `namespace Market.<Subsystem>`:
**Core** (ServiceLocator, EventBus, SceneLoader, SceneNames, TimeSystem, GameBootstrap) · **Player** ·
**Interaction** · **Economy** (MoneySystem, Inventory, ItemSO, ItemCategory, ItemDatabase,
PriceCalculator, SupplierShop) · **Market** (MarketStall, StallSlot) · **NPC** (NPCVisitor,
NPCSpawner, NPCTypeSO) · **World** (DaylightSystem, SeasonManager, Season, MoonVisualFactory) ·
**UI** · **Persistence** (SaveSystem, GameSaver, SaveData) · **Debug** (`Market.DebugTools`,
temporary) · **Progression** / **Specializations** (reserved for D9+ and Blocks E–H).

**Assemblies (v1.6.1+):** `Market.Runtime` (Scripts root) · `Market.Editor` (`Scripts/Debug/Editor`,
editor-only, references `McpUnity.Editor`) · `Market.Tests.EditMode` (`Assets/_Project/Tests/EditMode`).
A new Unity-package dependency must be added to the asmdef `references` (currently
`Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`) or you get CS0246.

**Services:** plain-C# lifecycle services live in Core and register in `GameBootstrap` (EventBus,
SceneLoader, SaveSystem, TimeSystem, PriceCalculator). MonoBehaviours with scene refs are scene
coordinators (GameSaver, DaylightSystem, SeasonManager, NPCSpawner, MoneySystem, Inventory) wired
via serialized references — never global singletons. Direct Market-scene Play must work:
`ServiceLocator.TryGet<T>()` + a safe local fallback.

**Scene flow:** Bootstrap → MainMenu → Market via `ServiceLocator.Get<SceneLoader>().Load(SceneNames.X)`.
Never `SceneManager.LoadScene` from gameplay/UI code.

**Runtime UI layer (v1.6.1+):** UI is built in code. Reuse `Market.UI.UiFactory` (CreateRect /
CreateText / CreateButton / AddImage / StretchToParent / AddLayoutHeight + shared palette).
Market-style screens reuse `MarketPanelView` (panel chrome + info/action rows + item tooltip) with a
plain-C# renderer per panel (`InventoryPanelRenderer`, `SupplierPanelRenderer`, `StallPanelRenderer`).
New screens (Evening Summary, Wishboard, HiringBoard…) follow this pattern — never copy factory
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
- `OnEnable`/`OnDisable` only for subscriptions — every subscribe has a matching unsubscribe.
- Methods ≤ ~30 lines / one job. ScriptableObject = data, MonoBehaviour/plain C# = behavior.
- try/catch around all file/JSON I/O: log and return `false`/`null`, never crash gameplay.

### Never
- `FindObjectOfType` · `GameObject.Find` · static MonoBehaviour singletons · public Inspector fields.
- `UnityEngine.Input.*` / `KeyCode` — legacy Input Manager is disabled: it compiles, then throws
  `InvalidOperationException` at runtime. New Input System only (gotcha 9).
- `OnGUI` for runtime UI. Hidden gameplay math the player can't inspect or reason about.
- Heavy `Update()` logic without cached refs and clear justification.
- Unrelated refactors while implementing a requested step.
- **Non-ASCII text ANYWHERE in code and content** — `///` docs, `[Tooltip]`, `//` comments,
  `Debug.Log/*` strings, AND player-visible UI strings / SO defaults (`ItemSO.displayName`,
  `NPCTypeSO.typeName`, season names): all ASCII English. The game UI is English. Russian text
  repeatedly caused encoding corruption (42-file cleanup in v1.3.2; UI strings converted 2026-07);
  localization, if ever needed, will be a dedicated system — never inline non-ASCII literals.

### Performance
- Zero managed allocations per frame in hot paths: no per-frame List/array/string/delegate/closure/
  LINQ/boxing. Reuse collections with `Clear()`; pool frequently spawned objects; prefer non-alloc APIs.
- Update UI text only when data changes; `StringBuilder` for large/looped text.
- Logs are event-based or throttled, never per-frame. Cache shader property IDs in hot code.
- Profile before broad optimization — optimize measured hot paths only.

---

## Unity 6 patterns

- **Input:** `Keyboard.current[Key.X].wasPressedThisFrame`, `_action.ReadValue<Vector2>()`;
  InputAction callbacks subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **NavMesh:** bake via `NavMeshSurface`; `NavMeshObstacle` with carving for dynamic blockers;
  `NavMesh.SamplePosition()` before `SetDestination()`.
- **URP:** material props `_BaseColor`/`_BaseMap` (set `_Color`/`_MainTex` fallbacks when useful).
  Bright emissive: enable `_EMISSION`, `_EmissionColor` above 1, `globalIlluminationFlags = None`.
  URP/Unlit renders black without `_BaseMap` — set `Texture2D.whiteTexture`. Runtime skybox changes
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
- After `Destroy(_x)`, set `_x = null`. Don't duplicate validations — if `MoneySystem.TrySpend`
  already checks balance, just call it.

---

## Economy rules (transparent market)

Base buy/sell prices live in `ItemSO`; the player sets stall sell prices; NPCs buy by budget,
category preference, and concrete stall prices; seasons change *availability* and visuals, never
price. No hidden multipliers (drought / festival / rumor / demand ×N) — ever.
`PriceCalculator` is the single price-read point: `GetBuyPrice(item)` / `GetSuggestedSellPrice(item)`.
NPC refusal reasons stay concrete: no stock · category not interesting · price above budget · bought.

---

## ScriptableObject contracts

| SO | Purpose |
|---|---|
| `ItemSO` | id (stable save key), displayName, category, base prices, icon, worldPrefab, seasons |
| `ItemDatabase` | save lookup: `Resolve(id, name)` — id primary, name = legacy fallback |
| `NPCTypeSO` | budget, speed, browse time, preferences, prefab |
| `CropSO` | crop growth data (E block) |
| Planned | `OrderSO` / `NPCPersonalitySO` / `StaffSO` / `AttractionObjectSO` (D), `RecipeSO` (H) — data-only, same pattern |

---

## Persistence

`SaveSystem` = plain C# service · `GameSaver` = scene coordinator · `SaveData` = DTO only.
Item identity restores via `ItemDatabase.Resolve(id, name)`. All file/JSON ops in try/catch.
`Continue` uses `SaveSystem.ShouldLoadOnStart`. Preserve old-save compatibility.
**At each block checkpoint D–I: bump `SaveData.version`, migrate old saves, and extend
`SaveMigrationTests` with the migration case** (required by `dev_plan_3.md`).

---

## Tests (v1.6.1+)

EditMode tests in `Assets/_Project/Tests/EditMode` (namespace `Market.Tests`, asmdef
`Market.Tests.EditMode`). Use `TestItems` (SerializedObject factory) to build ItemSO/ItemDatabase;
`Object.DestroyImmediate` everything you create. New pure-logic systems (orders, reputation, rating,
wages, unlocks…) ship with tests; UI/scene-wiring code does not need them.
Run via MCP `run_tests`: mode `EditMode`, filter `Market.Tests`.

---

## Debug tooling (`Market.DebugTools`, temporary)

`FileLogger` → `game.log` · `DebugTimeControl` (PgUp/PgDn speed, `H` +1h, `N` next season) ·
`DebugSupplierBuy` (keys 1–5) · `DebugStallPlace` (F3) · `DebugMoneyInput` (F1/F2) ·
`MarketAutoDebugger` (F9 loop, F10 one cycle) · manual save F5.
Remove each one once real UI covers it. Play Mode issues → check `game.log` and serialized scene
values before guessing.

---

## Serialized state debugging

Unity behavior often lives in Inspector values, prefab overrides, and SO assets — not C#.
Before blaming code: `rg "ScriptName|fieldName|guid:" Assets/_Project -g "*.unity" -g "*.prefab"
-g "*.asset" -g "*.meta"`, map the script GUID from its `.cs.meta`, compare serialized values to
code defaults, and fix the wiring when that is the truth. Logs: `Get-Content .\game.log -Tail 200`.
Note: Unity escapes non-ASCII in YAML as `\uXXXX` — grep for `\\u04` to find any stray Cyrillic.

---

## MCP Unity verification

Loop: `recompile_scripts` → `get_health_report` (`includeTests: false`; must be `ok`, 0 errors) →
`get_console_logs` (`includeStackTrace: false`, small `limit`) when needed → `run_tests`
(filter `Market.Tests`, `returnOnlyFailures`) for shared/risky logic. A passed gate is final —
re-run only what a later edit invalidated.

WS fallback if MCP transport is closed (from project root; same pattern for any tool):

```powershell
@'
{"returnWithLogs":true,"logsLimit":120}
'@ | node .claude/tools/unity-ws-call.mjs recompile_scripts - --timeout 120000
```

`ECONNREFUSED 127.0.0.1:8090` → Unity is closed or the MCP server window is offline.
Helper scripts live in `.claude/tools/` (`check-mcp-unity.ps1`, `start-mcp-unity.ps1`,
`unity-ws-call.mjs`).

### Gotchas (hard-won; numbers are stable ids — keep them)

1. `run_tests` fills the console with *expected* `McpUnity.Tests` negative-case logs; a post-test
   `attention` health caused only by those is NOT a project error. Never write an Editor script to
   "clean" the console.
2. Don't over-verify — see Token discipline. Recompile + health is enough for a normal change.
3. Ping the Editor at task start; if it's closed, pick a strategy immediately, not mid-task.
4. MCP-driven Play Mode does NOT tick time/animations like a focused player. Advance time explicitly
   (`TimeSystem.SkipHours`) — "time isn't moving under MCP" is not a gameplay bug.
5. Prefer MCP `update_component` over hand-writing scene YAML; manual YAML only for object-reference
   wiring or inactive scenes, and run `git diff --check` *before* committing scene edits.
   Before creating/moving objects, confirm the active scene with `get_scene_info`; if the scene is
   wrong, `load_scene` first. For new debug/scene props, prefer a tiny Editor builder that creates the
   exact root objects once, then inspect those objects directly instead of poking around repeatedly.
6. A new `[SerializeField]` field does NOT backfill into already-serialized scenes — set it
   explicitly (`update_component` / `save_scene`) or it silently stays `null`/`false`.
7. A new `.cs` under `Packages/…` needs its `.meta` + `AssetDatabase.Refresh` before the class exists.
8. ASCII English everywhere — code text AND UI strings/SO defaults (see Never). No exceptions.
9. Never `UnityEngine.Input.*` — disabled legacy Input compiles, then throws at runtime
   (5000+ exceptions/session for a per-frame call; bit v1.4.0's tooltip).
10. Rect clipping = `RectMask2D`, never legacy `Mask` with an alpha-0 graphic: the culled graphic
    never writes the stencil, so ALL masked children render invisible while staying clickable
    (hid every C3 list row; fixed v1.5.4).
11. Code-built UI: set stretch anchors BEFORE `offsetMin`/`offsetMax` — collapsed anchors + offsets
    = zero-size rect that never renders (hid the price input; fixed v1.5.6).
    `UiFactory.StretchToParent` does it right.
12. MCP `recompile_scripts` does NOT import brand-new files (no `.meta` yet). CS0246 against your own
    new class → run `execute_menu_item` `Assets/Refresh` first, then recompile (bit v1.6.1).

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

Per-step availability tags (`[assets: ready/stub/backlog]`) live in `dev_plan_3.md`.
