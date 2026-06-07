# Market Game - Codex Project Rules

This file is the standing project contract for Codex when working in
`C:\Users\bogre\My project`.

Keep it short, current, and actionable. Do not use it as a progress log:
`dev_plan_3.md`, the current worktree, Unity serialized files, console logs,
MCP health reports, and `game.log` are the sources of truth for current state.

> **Shared contract.** The coding, architecture, performance and Unity-6 rules in this file apply to
> **both** agents — `CLAUDE.md` points here instead of duplicating them. Codex-specific division of
> labour: Codex implements a plan step and records it in `CHANGELOG.md` `[Unreleased]`; Claude
> reviews, verifies via Unity MCP, versions, tags and merges to `main`. Full process: `COLLAB.md`.

---

## Version Control & Collaboration with Claude

Repo is under **git** (remote `Bogdychka/market-game`, private); binary assets via **Git LFS**. A second agent, **Claude Code**, works in this repo too. Full protocol: `COLLAB.md`.

**Workflow: branch-per-task + PR.** Essentials:
- `main` stays green (compiles, MCP health `ok`). **No direct commits to `main`.**
- One task = one branch named `codex/<step>-<slug>` = one PR.
- Start a task: `git switch main; git pull; git switch -c codex/<step>-<slug>`.
- Verify via MCP / WS fallback before committing; tick the matching `dev_plan_3.md` box in the same branch; Conventional Commit; `gh pr create`.
- Don't edit the same files Claude is editing. Scenes/prefabs: one agent at a time.
- **Your role (Codex):** implement the step and **record what you did** under `CHANGELOG.md` `[Unreleased]` (+ PR body). Do **not** merge to `main` or create version tags — Claude reviews, verifies via Unity MCP, versions (SemVer tag `vX.Y.Z`), and pushes.
- **Versioning:** every shipped change gets a `CHANGELOG.md` entry; releases are SemVer tags `vX.Y.Z` on `main` (current line **v1.x**).

---

## Tech Stack

| Area | Project Version / Rule |
|---|---|
| Unity | `6000.4.8f1` / Unity 6.4 |
| C# | Unity Roslyn compiler, C# `9.0` language level |
| Render Pipeline | URP `17.4.0`, Deferred rendering in `PC_Renderer.asset` |
| Input | New Input System `1.19.0`; legacy Input is disabled (`activeInputHandler = 1`) |
| NavMesh | AI Navigation `2.0.12`; use `NavMeshSurface`, not old Navigation Static workflow |
| Runtime UI | uGUI + TextMeshPro |
| Editor UI | UI Toolkit only for editor tooling |
| Persistence | `Application.persistentDataPath` + JSON |
| Networking | Netcode for GameObjects only when Block J starts |

Checked against official docs on 2026-06-06:

- Unity 6.4 manual: Roslyn + C# 9.0, with unsupported C# 9 features and record caveats.
- Microsoft C# conventions: clarity, specific exceptions, async for I/O, modern syntax when supported.
- Unity GC best practices: avoid per-frame managed allocations, reuse collections, avoid closures/boxing in hot paths.

Important: "latest C#" in Microsoft docs is newer than Unity's supported language level. For this project, use modern C# only where it compiles and behaves correctly under Unity 6.4 / C# 9.0.

---

## Architecture

All game code and content lives under `Assets/_Project`.

```text
Assets/
  _Project/
    Scripts/
      Core/          Market.Core        ServiceLocator, EventBus, SceneLoader, TimeSystem
      Player/        Market.Player      FirstPersonController, HeadBob
      Interaction/   Market.Interaction InteractionSystem, IInteractable
      Economy/       Market.Economy     MoneySystem, Inventory, ItemSO, ItemDatabase
      Market/        Market.Market      MarketStall, StallSlot
      NPC/           Market.NPC         NPCVisitor, NPCSpawner, NPCTypeSO
      World/         Market.World       DaylightSystem, SeasonManager, MoonVisualFactory
      UI/            Market.UI          HUD, menus, shop/inventory/stall UI
      Persistence/   Market.Persistence SaveSystem, GameSaver, SaveData
      Debug/         Market.DebugTools  temporary debug helpers only
    Data/            ScriptableObject instances
    Art/Prefabs/     project prefabs
    Scenes/          Bootstrap, MainMenu, Market
  ThirdParty/        third-party packs; do not restructure
_ArchiveAssets/      outside Assets; not imported by Unity
```

Core services registered by `GameBootstrap`:

- `EventBus`
- `SceneLoader`
- `SaveSystem`
- `TimeSystem`

Scene coordinators:

- `GameSaver` saves/loads money, inventory, stalls, player position, and scene state.
- `DaylightSystem` controls sun/moon, ambient, and skybox exposure.
- `SeasonManager` controls season state and season visuals.
- `NPCSpawner` owns visitor spawning and scene spawn/exit references.

Service pattern:

- Plain C# lifecycle services live in `Core` and are registered in `GameBootstrap`.
- MonoBehaviours with scene references are coordinators, not global singletons.
- Scene-bound services such as `MoneySystem` and `Inventory` are wired with serialized references.
- Direct scene Play must work: if Bootstrap services may be missing, use `ServiceLocator.TryGet<T>()` and a safe local fallback where the architecture supports it.

---

## Clean C# Rules

Use these rules for every script.

### Required

- Every file has `namespace Market.<Subsystem>`.
- Classes, methods, properties, and public events use PascalCase.
- Private fields use `_camelCase`.
- Inspector fields are `[SerializeField] private`, never public fields.
- Group Inspector fields with `[Header]`: `References`, `Settings`, `Tuning`, `Debug`.
- Add `[Tooltip]` for any field whose purpose is not obvious.
- Add XML doc-comments to public classes and non-trivial public methods.
- Use `[RequireComponent]` when a component dependency must be on the same GameObject.
- Cache components in `Awake()`.
- Put serialized reference checks in `ValidateReferences()` or `Resolve...()` helpers called from `Awake()`.
- Use `OnEnable` / `OnDisable` only for subscriptions and matching unsubscriptions.
- Keep methods focused. Split methods around 30 lines or when they do more than one job.
- Separate data from logic: ScriptableObject for data, MonoBehaviour/plain C# for behavior.

### C# 9 / Unity Compatibility

- Do not use C# features unsupported by Unity's C# 9 compiler/runtime.
- Do not use `record` types for serialized Unity data.
- Avoid `init` setters and record-style initialization in gameplay data.
- Do not enable `unsafe` code unless the feature explicitly requires it and the user approves.
- Use `var` only when the right side makes the type obvious.
- Use target-typed `new()` only when it improves clarity and Unity serialization is not involved.
- Use pattern matching and switch expressions when they make code simpler.
- Prefer explicit simple loops over LINQ in hot paths.
- Catch specific exceptions where possible. For broad I/O/JSON safety, catch, log, and return `false` or `null`.
- Use `using` declarations/statements for disposable non-Unity resources.

### Never

- No `FindObjectOfType`.
- No `GameObject.Find`.
- No static MonoBehaviour singletons.
- No public Inspector fields.
- No legacy `Input.GetKey`, `Input.GetKeyDown`, `KeyCode`, `Input.GetMouseButtonDown`, or `Input.anyKey`.
- No `OnGUI` for runtime UI.
- No hidden gameplay math that the player cannot inspect or reason about.
- No heavy logic in `Update()` without cached references and clear justification.
- No unrelated refactors while implementing a requested step.
- **No Russian (or any non-ASCII) text in `///` XML doc comments, `[Tooltip]` attributes, `//`
  inline comments, or `Debug.Log/Warning/Error` strings.** All developer-facing code text must be
  ASCII English. Player-visible UI strings (panel titles, button labels, season names, currency
  suffix, NPC `displayName` / `typeName` defaults) are the only exception and remain Russian.

---

## Performance Rules

Unity GC guidance matters in gameplay code.

- Aim for zero managed allocations per frame in hot paths.
- Do not allocate `List`, arrays, strings, delegates, closures, or LINQ queries every frame.
- Reuse collections with `Clear()` when used repeatedly.
- Use object pooling for frequently spawned/despawned runtime objects.
- Avoid repeated string concatenation in loops or per-frame UI updates; use `StringBuilder` for large/looped text and update UI only when data changes.
- Avoid closures and anonymous methods in per-frame or high-frequency code.
- Avoid boxing value types, especially through `object`, non-generic collections, or string formatting in hot loops.
- Avoid Unity APIs that allocate arrays in loops; prefer non-alloc APIs or cached collections when available.
- Cache shader property IDs for repeated material access when code runs frequently.
- Do not log every frame. Debug logs must be event-based, throttled, or behind debug tools.
- Profile before broad optimization. Optimize measured hot paths, not random code.

---

## Unity 6 Required Patterns

### Input

```csharp
// Correct:
Keyboard.current[Key.F1].wasPressedThisFrame
_moveAction.ReadValue<Vector2>()
_interactAction.started += OnInteract;

// Wrong:
Input.GetKeyDown(KeyCode.F1)
```

Subscribe InputAction callbacks in `OnEnable`; unsubscribe in `OnDisable`.

### NavMesh

- Add `NavMeshSurface` to the floor/root navigation object and bake through that component.
- Use `NavMeshObstacle` with carving where dynamic blocking is needed.
- For NPC destinations, call `NavMesh.SamplePosition()` before `SetDestination()`.
- Never rely on Navigation Static flags or the old Navigation window bake workflow.

### URP

- Deferred mode is set in `PC_Renderer.asset`.
- Use URP material properties: `_BaseColor`, `_BaseMap`.
- Set built-in fallbacks when useful: `_Color`, `_MainTex`.
- For bright emissive objects, enable `_EMISSION`, set `_EmissionColor` above 1, and set `globalIlluminationFlags = None`.
- URP/Unlit can render black without `_BaseMap`; set `Texture2D.whiteTexture` when no texture is used.
- For controllable day/night skybox exposure, create a runtime instance of `RenderSettings.skybox` before changing properties.
- Set `RenderSettings.sun = sunLight` and use `AmbientMode.Flat` when code controls ambient light.

### Coroutines and Async

- Use coroutines for Unity-dependent timers and `WaitForSeconds`.
- Use `async/await` for I/O and network work when useful.
- Keep Unity object access on the main thread.
- Do not fire-and-forget async work without cancellation/error handling.

### Scene Loading

```csharp
ServiceLocator.Get<SceneLoader>().Load(SceneNames.Market);
```

Do not call `SceneManager.LoadScene(...)` directly from gameplay/UI code unless implementing `SceneLoader` itself.

---

## Project Patterns

- Save/load uses Collect/Apply split: `CollectInventory(data)`, `ApplyInventory(data)`, etc.
- Long `Update()` methods split into helpers such as `TickTime()` and `HandleEscape()`.
- UI controllers use `Wire...`, `Refresh...`, `Show...`, and `Hide...` helpers.
- Complex runtime object construction moves into factories, such as `MoonVisualFactory`.
- NPC state machines use `EnterX` / `UpdateX` methods and one state dispatcher.
- After `Destroy(_visual)`, set `_visual = null`.
- Do not duplicate validations: if `MoneySystem.TrySpend` checks balance, call it directly.
- When using `Mathf.Clamp01`, check raw values first if logic cares about values outside `[0,1]`.

---

## Economy Rules

The market economy must stay transparent.

- Base buy/sell prices live in `ItemSO`.
- The player chooses stall sale prices.
- NPCs buy based on budget, category preferences, and concrete stall prices.
- Seasons affect availability and visuals, not hidden price multipliers.
- Do not add drought/festival/world-factor demand multipliers unless the plan explicitly reintroduces them.
- Existing `PriceContext` / `IPriceModifier` code is legacy cleanup territory: B6 in `dev_plan_3.md` says to simplify it. Do not build new gameplay on top of modifiers.

Recommended public API after cleanup:

```csharp
GetBuyPrice(ItemSO item)
GetSuggestedSellPrice(ItemSO item)
```

NPC purchase debug reasons should be concrete:

- no stock
- category not interesting
- price above budget
- purchase succeeded

---

## ScriptableObject Contracts

| SO | Purpose |
|---|---|
| `ItemSO` | Product data: display name, category, base prices, icon, world prefab, season availability |
| `ItemDatabase` | Registry of all `ItemSO` assets for save/load lookup |
| `NPCTypeSO` | NPC budget, speed, browse time, preferences, prefab |
| `CropSO` | Crop growth data for Block D |
| `RecipeSO` | Crafting recipe for Block H |
| `MarketOutcomeSO` | Narrative outcome data only if/when the plan uses it without hidden price math |

Folders like `WorldFactors` may exist from earlier planning, but hidden world-factor price modifiers are not active design direction.

---

## UI Rules

- Runtime UI is uGUI + TextMeshPro.
- UI Toolkit is allowed only for editor tools.
- UI scripts go under `Assets/_Project/Scripts/UI` with `Market.UI`.
- Use serialized references and validate them in `Awake()`.
- Subscribe to model/system events in `OnEnable`; unsubscribe in `OnDisable`.
- UI reflects source systems; it must not own duplicate gameplay state.
- Update text only when values change.
- If a UI requires many scene references, provide sensible defaults or prefab structure so setup is not fragile.

---

## Persistence Rules

- `SaveSystem` is a plain C# service.
- `GameSaver` is a scene coordinator.
- `SaveData` is DTO-only serializable data.
- Use `ItemDatabase` for item identity restoration.
- Wrap file read/write/delete and JSON parse in `try/catch`.
- Log failures and return `false` or `null`; never let save/load exceptions crash gameplay.
- Preserve old save compatibility when changing `SaveData`.
- `Continue` uses `SaveSystem.ShouldLoadOnStart`.

---

## Debug Tooling

- `FileLogger` writes Unity logs to `C:\Users\bogre\My project\game.log`.
- `DebugTimeControl`: `PageUp` / `PageDown`, `H`.
- `DebugSupplierBuy`: keys `1`-`5`.
- `DebugStallPlace`: `F3`.
- `DebugMoneyInput`: `F1` / `F2`.
- `MarketAutoDebugger`: use current bindings/log markers when present.

Debug scripts are temporary and belong in `Assets/_Project/Scripts/Debug` under `Market.DebugTools`.

When the user reports Play Mode behavior, check `game.log` and serialized scene/prefab values before guessing.

---

## Serialized Unity State

Unity behavior is often controlled by Inspector values, prefab overrides, and ScriptableObject assets.

Before diagnosing behavior only from C#:

1. Search relevant `.unity`, `.prefab`, `.asset`, and `.meta` files.
2. Map script GUIDs from `.cs.meta` to scene/prefab components.
3. Compare serialized field values with code defaults.
4. Fix scene/prefab/data wiring when that is the true source.

Useful commands:

```powershell
rg "ScriptName|fieldName|guid:" Assets/_Project -g "*.unity" -g "*.prefab" -g "*.asset" -g "*.meta"
```

```powershell
Get-Content .\game.log -Tail 200
```

---

## MCP Unity Verification

Prefer `mcp__mcp_unity` tools when available:

1. `recompile_scripts`
2. `get_health_report`
3. `get_console_logs` with `includeStackTrace: false`
4. `run_tests` for shared behavior or risky changes

If MCP says `Transport closed`, use the direct WebSocket fallback from project root:

```powershell
@'
{"returnWithLogs":true,"logsLimit":120}
'@ | node .codex/tools/unity-ws-call.mjs recompile_scripts - --timeout 120000 --client "Codex Recompile"
```

```powershell
@'
{"includeTests":true,"testMode":"","maxConsoleErrors":20,"maxTests":20}
'@ | node .codex/tools/unity-ws-call.mjs get_health_report - --timeout 60000 --client "Codex Health"
```

```powershell
@'
{"logType":"error","limit":50,"includeStackTrace":false}
'@ | node .codex/tools/unity-ws-call.mjs get_console_logs - --timeout 10000 --client "Codex Console Errors"
```

If WebSocket fails with `ECONNREFUSED 127.0.0.1:8090`, confirm Unity is open and the MCP Unity server window says Server Online.

### Gotchas (hard-won — read before a long MCP session)

These are the things that have repeatedly burned time. Internalize them.

1. **Test noise is not a project failure.** `run_tests` populates the Unity Console with *expected*
   negative-case logs from `McpUnity.Tests` (e.g. `Server path is null`, fake temp paths). A post-test
   `get_health_report` showing `attention` *caused only by those* is **not** a project error — note it
   and move on. **Never add a temporary Editor script to "clean" the Console** — that's a self-dug hole.
2. **Don't over-verify.** For a normal change, `recompile_scripts` + `get_health_report` is enough.
   Run `run_tests` only for shared/risky logic, and don't re-run the full gate Claude will run anyway.
3. **Check the Editor is up *first*.** At the start of an MCP task do one health/`check-mcp-unity` ping.
   If the Editor is closed (`ECONNREFUSED`), decide the strategy immediately (ask to open it, or go
   batchmode) instead of hitting it mid-task and paying for heavy batchmode compensation.
4. **MCP-driven Play Mode does NOT tick frames/time like a real focused game.** `TimeSystem` won't
   advance, animations won't play. For time-dependent logic don't trust "time isn't moving in the
   MCP test" as a gameplay bug — advance time explicitly (`TimeSystem.SkipHours`) or Play manually in
   a focused Editor. (This false trail once cost ~10 minutes chasing a non-bug.)
5. **Prefer MCP over hand-writing scene YAML.** `update_component` adds a component and sets primitive
   fields with Unity-valid serialization (no trailing-whitespace / wrong-block patch fights). Reserve
   manual YAML for object-reference wiring (`{fileID: …}` to another scene component) and inactive
   scenes — and always run `git diff --check` *before* committing scene edits, not after the fight.
6. **A new `[SerializeField]` field / reference does NOT appear in an already-serialized scene.**
   Code defaults don't backfill existing components. After adding a field or a ref, set it explicitly
   in the scene (via `update_component` / `save_scene`) — or the Inspector value silently stays
   `false`/`null` and the feature looks broken while the code is correct.
7. **New `.cs` in an embedded package (`Packages/…`) needs its `.meta` + an `AssetDatabase.Refresh`.**
   Until imported, the server sees the registration but not the class. Create the `.meta` and refresh;
   don't work around it by dumping the class into an unrelated already-imported file.
8. **ALL code text must be ASCII English — no Russian anywhere in comments, tooltips, or log strings.**
   Russian in source files causes mojibake that makes targeted patches land in the wrong place or fail
   to match context entirely. This rule was violated across the codebase and required a dedicated
   42-file cleanup pass (v1.3.2) to fix — do not repeat it.
   - ✅ English only: `/// <summary>`, `[Tooltip("...")]`, `// inline`, `Debug.Log/Warning/Error`
   - ✅ Russian kept: player-visible strings in uGUI/TMP (`text.text = "…"`), `ItemSO.displayName`
     defaults, `NPCTypeSO.typeName` defaults, season display names returned from `GetName()`
   - ❌ If you catch yourself typing a Russian word in a comment or tooltip — stop and write English.

---

## Codex Skills

Local project skills live in `C:\Users\bogre\.codex\skills`.

Use them when relevant:

- `market-game-architecture`
- `unity6-coding-standards`
- `mcp-unity-verification-loop`
- `serialized-scene-debugging`
- `unity-build-error-fixer`
- `game-log-debugging`
- `market-economy-safety`
- `npc-navmesh-workflow`
- `save-load-safety`
- `ugui-tmp-workflow`

Skills are short checklists. This `AGENTS.md` remains the full project contract.

---

## Workflow

- One plan step per request unless the user explicitly asks for a larger batch.
- Read nearby code before editing.
- Use `rg` / `rg --files` for searches.
- Use `apply_patch` for manual edits.
- Do not revert user changes.
- Do not modify `ThirdParty` structure.
- Do not invent new architecture when existing patterns fit.
- After implementing a project-plan step, update the matching checklist item in `dev_plan_3.md`.
- Verify after code/scene-sensitive changes with Unity MCP or the WebSocket fallback.
- If verification cannot be performed, say exactly what was not verified.

---

## Available Assets

| Pack | Path | Use |
|---|---|---|
| Kenney Food Kit | `Assets/kenney_food-kit/Models/FBX format/` | 3D item models |
| Quaternius Farm Animals | `Assets/Farm Animals Animated by Quaternius/FBX/` | Livestock |
| Quaternius Farm Buildings | `Assets/Farm Buildings by Quaternius/FBX/` | Farm structures |
| Quaternius Fish Pack | `Assets/Fish Pack Animated by Quaternius/FBX/` | Fishing |
| Textured Stylized Trees | `Assets/Textured Stylized Trees - May 2020/.../FBX/` | Scene decoration |
| UAL Standard | `Assets/Universal Animation Library[Standard]/.../Unity/` | NPC rig |
| Mixamo Animations | `Assets/Mixamo_animations/` | NPC animations |

