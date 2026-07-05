# Changelog

All notable changes to the Market Game project. Format follows
[Keep a Changelog](https://keepachangelog.com/); versioning follows
[Semantic Versioning](https://semver.org/) — each released version is tagged in git as `vX.Y.Z`.

This file doubles as the **worklog**: the `[Unreleased]` section is where in-flight work is
recorded before it is verified via Unity MCP, versioned, tagged, and pushed. Old entries keep
their historical agent attributions (Claude / Codex / user); new entries don't need one.

> **Agents: read only the head of this file** (`[Unreleased]` + the latest release, ~40 lines) —
> never the full history. When the file exceeds ~30 KB, move entries older than the last 5 releases
> to `CHANGELOG.archive.md`.

## [Unreleased]

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
  params: Idle⇄Walk blend tree + Talk state), and `NPCAnimator` driving it from
  `NavMeshAgent.velocity` and `NPCVisitor.CurrentState`. Idle/Walk/Talk play the UAL rig's own
  `Idle_Loop` / `Walk_Loop` / `Idle_Talking_Loop` clips on the shared UAL avatar (no Mixamo retarget
  needed). Visual mesh/outfit variety is deferred until more humanoid assets exist. (Codex)

### Changed
- Save data is now version 5 and persists crop plots (audit C2): each `CropPlot` has a stable
  `plotId` and its planted flag + plant timestamp are collected/applied by `GameSaver`. Pre-v5 saves
  (no `cropPlots` list) load unaffected — every plot restores to empty, matching prior behavior.
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

### Fixed
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
  restored visitors no longer teleport, jitter, or clip through geometry after Save → Continue.
  `NPCVisitorData` now stores only intent — `npcTypeKey`, `targetStallId`, `visitedStallIds` (no saved
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
  tuned for snappier stops/turns (Acceleration 8→24, AngularSpeed 120→520, StoppingDistance 0→1.2 so
  it brakes into the stall instead of arriving at full speed). NPC walk speed dropped from 3.5 to a
  realistic 1.4 m/s (`NPCType_Default` + prefab agent), which also keeps `WalkMult` near 1x so the feet
  match. Residual stop/turn slide is inherent to in-place clips without root motion. (Claude)

### Verification
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
  `SettingsPanel` GameObject with `onBack → MainMenuController.CloseSettings()`.

## [1.7.0] - 2026-06-13

### Added — C6 Settings menu (Claude)
- `SettingsSO` (`Assets/_Project/Data/SettingsSO.asset`) — ScriptableObject with default values for all
  player-configurable settings (mouse sensitivity min/max/default, invert-Y, master/music/sfx volumes).
- `SettingsService` — plain C# service registered in `ServiceLocator` at boot; loads/persists all
  settings via `PlayerPrefs`; fires `LookSettingsChanged` and `VolumesChanged` events.
- `SettingsPanelRenderer` — code-built settings panel: mouse-sensitivity slider (0.02–0.60),
  invert-Y toggle, Master/Music/SFX volume sliders (0–1, shown as %), and interactive key-rebind
  buttons for Interact / Jump / Sprint (Keyboard&Mouse group); rebind overrides saved as JSON to
  `PlayerPrefs`. Volume UI persists values; AudioMixer wiring deferred to C7.
- `GameBootstrap` now creates and registers `SettingsService` before any scene loads; `settingsSO`
  field wired in the Bootstrap scene Inspector.
- `FirstPersonController` loads saved sensitivity + invert-Y on `Awake`, applies binding-override
  JSON, and subscribes to `LookSettingsChanged` in `OnEnable`/`OnDisable` for live updates.
- `PauseMenuController` replaces the settings stub panel with `SettingsPanelRenderer`; references
  wired in the Market scene Inspector (`settingsSO`, `playerController`, `playerInput`).

### Verification
- `recompile_scripts` → 0 errors, 0 warnings. `get_health_report` → ok. (Claude)

## [1.6.3] - 2026-06-11

### Changed
- Token-economy hardening of the agent rules (follow-up to v1.6.2, based on measured costs):
  - `AGENTS.md` Token discipline: partial-read rules for `dev_plan_3.md` (Progress section +
    own block only; never the whole 36 KB) and `CHANGELOG.md` (head only + archive-at-30KB policy);
    "a passed gate is final" — re-run only gates invalidated by a later edit; cheap MCP defaults
    (`get_health_report includeTests:false`, console logs without stack traces + small limit,
    `run_tests` failures-only without logs). MCP loop section updated to match. (Claude)
  - `CLAUDE.md`: `unity-csharp-reviewer` scoped — only for non-trivial C# (new logic / economy /
    persistence / NPC / shared systems) with an exact file list + focus areas in the prompt
    (measured cost ~65k tokens/run); trivial diffs are reviewed inline. (Claude)
  - `dev_plan_3.md` and `CHANGELOG.md` got header notes telling agents to read them partially —
    the rule now sits where the file is opened. (Claude)

### Verification
- Docs-only change; no C# touched, MCP loop not required. (Claude)

## [1.6.2] - 2026-06-11

### Changed
- Agent contract docs slimmed and updated for token economy (loaded into every session of both
  agents): `AGENTS.md` 23.4KB→14.7KB, `CLAUDE.md` 5.8KB→3.3KB, `COLLAB.md` 5.1KB→3.2KB (−38%
  total). All substantive rules kept; removed cross-file duplication (collab protocol lived in
  three places, tech stack in two); gotcha numbering preserved as stable ids. (Claude)
- `AGENTS.md` gains: a "Token discipline" section (proportional verification, minimal reads, no
  ritual summaries); the v1.6.1 reality — asmdef layout + "new package deps go into asmdef
  references", the `UiFactory`/`MarketPanelView`/renderer UI pattern for future screens, a Tests
  section (where, how to run, what needs tests), the D–I save-version bump rule; planned Block D/E/H
  ScriptableObjects in the SO contract table; new gotcha 12 (MCP recompile does not import brand-new
  files — run Assets/Refresh first). (Claude)

### Verification
- Docs-only change; no C# touched, MCP loop not required. (Claude)

## [1.6.1] - 2026-06-11

### Changed
- UI refactor, no behavior change: extracted the duplicated code-built-UI helpers from
  `MarketUIController` (936 lines) and `PauseMenuController` into a shared static `UiFactory`
  (`Market.UI`), and split `MarketUIController` into a thin coordinator (~250 lines) plus
  `MarketPanelView` (panel chrome + shared row widgets), `ItemTooltipView`, and plain-C#
  `InventoryPanelRenderer` / `SupplierPanelRenderer` / `StallPanelRenderer`. Scene wiring
  untouched — the scene component, its GUID, and all serialized fields are unchanged. (Claude)
- `dev_plan_3.md`: added **D0 MarketStallRegistry** as an explicit step (the B9 temporary
  single-stall API must be retired before D11/D12 build on it); checkpoints D–I now each require
  a `SaveData.version` bump + migration + EditMode migration test. (Claude)
- Committed the warmed `LiberationSans SDF - Fallback` TMP dynamic-atlas state (runtime-added
  Cyrillic glyphs kept the working tree permanently dirty). (Claude)

### Added
- Assembly definitions: `Market.Runtime` (all gameplay scripts), `Market.Editor`
  (`Scripts/Debug/Editor`, editor-only, references `McpUnity.Editor`), and
  `Market.Tests.EditMode` — makes the plan-step 0.1 "asmdefs" claim true and enables a test
  assembly. (Claude)
- First EditMode tests (17, all green) under `Assets/_Project/Tests/EditMode`: `EconomyTests`
  (PriceCalculator read-through, ItemSO season availability, MoneySystem spend rules),
  `InventoryTests` (add/remove/OnChanged contract), `SaveMigrationTests` (ItemDatabase id/name
  resolution for v1 saves, v1-JSON time defaults, SaveData round-trip), plus a `TestItems`
  SerializedObject factory. (Claude)

### Removed
- Empty leftover script folders `Scripts/Influence` and `Scripts/Outcomes` (+ `.meta`) —
  planning artifacts that match no block of `dev_plan_3.md`. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings. (Claude)
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)
- MCP `run_tests` (EditMode, filter `Market.Tests`): 17/17 passed. (Claude)
- C# review via `unity-csharp-reviewer`: no blocking findings; its one HIGH note (season event
  re-subscribe on re-enable) was re-checked against the code and is a false positive —
  `UnwireSeasonEvents` resets `_seasonEventsWired`, so re-enable re-subscribes. (Claude)

## [1.6.0] - 2026-06-08

### Added
- C5 PauseMenu: `Esc` in the Market scene opens a runtime uGUI/TMP pause menu with Resume, Save,
  Settings placeholder, and Main Menu actions. The pause menu enters `UIModeService` menu mode,
  sets `Time.timeScale = 0`, resumes through Escape or the Resume button, and restores
  `Time.timeScale = 1` before closing or loading MainMenu. (Codex)

### Changed
- `GameBootstrap` now preserves existing UI Escape priority in Market: supplier/stall/inventory
  panels consume Escape first, otherwise Escape opens the pause menu instead of immediately
  returning to MainMenu. (Codex)
- Review fixes: `PauseMenuController.OnMainMenu` calls `Resume()` instead of duplicating its
  timeScale/visibility/menu-mode reset; added a `[Tooltip]` on the `uiModeService` field; replaced
  the player-visible "C6" plan id with neutral placeholder text. (Claude)
- `.gitattributes`: set `whitespace=-trailing-space` on Unity YAML formats (`*.unity`/`*.prefab`/
  `*.asset`/`*.mat`/`*.anim`/`*.controller`/`*.physicMaterial`). Unity intentionally writes trailing
  spaces (e.g. `m_Name: `), so `git diff --check` no longer flags them and whitespace-fixing ops no
  longer strip them — eliminating spurious scene/prefab diffs. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings. (Codex + Claude after review fixes)
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Codex + Claude)
- MCP scene inspection: `HUD` has `PauseMenuController` with `gameSaver` and `uiModeService` wired. (Codex)
- C# review via `unity-csharp-reviewer`: two HIGH findings addressed, MCP re-verified green. (Claude)

## [1.5.6] - 2026-06-08

### Fixed
- Stall price-input value never displayed (field looked like an empty box): the input's `text` and
  `placeholder` TMP components set `offsetMin/offsetMax` without stretch anchors, producing a
  zero/negative-size rect so the suggested price never rendered. Added `StretchToParent` before the
  offsets so the value is visible. (Claude)

### Changed
- `AGENTS.md`: gotcha #10 — use `RectMask2D` for rectangular UI clipping; a legacy `Mask` with an
  alpha-0 graphic gets culled, never writes the stencil, and hides all masked children while they stay
  clickable. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.5.5] - 2026-06-08

### Fixed
- Stall price-input fields rendered as harsh near-black bars (background `0.07,0.08,0.09`, darker than
  the row), making the field look broken and the price hard to read. Lightened the input background to
  `0.24,0.27,0.31` so it reads as a proper input field. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.5.4] - 2026-06-08

### Fixed
- Market panel list rows (supplier, stall, inventory) were invisible — only the floating hover tooltip
  rendered. The scroll viewport used a legacy `Mask` whose mask graphic was a fully transparent
  (alpha 0) `Image`; uGUI culls fully-transparent graphics, so the `Mask` never wrote the stencil and
  every masked child failed the stencil test and disappeared — while still receiving raycasts (hence
  rows were clickable/hoverable but unseen). Replaced `Mask` with `RectMask2D` (rect-based clipping,
  no graphic needed); kept a transparent raycast-target image for ScrollRect drags. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.5.3] - 2026-06-08

### Fixed
- Stall could not be opened: the MarketStall's only interaction collider was a thin 0.2m-tall counter
  slab on the child "Cube", which the player's horizontal crosshair passed over. Added a generous
  trigger `BoxCollider` on the MarketStall GameObject (center 2.84,1.4,0; size 4x2.2x1.6) so the
  "Управлять прилавком" prompt reliably appears at eye level. Confirmed via game.log (previously zero
  stall-interaction events; the stall had only been exercised through `DebugStallPlace` F3). (Claude)
- `MarketUIController.AddItemIcon`: no longer draws the letter fallback on top of an assigned sprite
  (early return when `item.Icon != null`); the food sprite is now shown alone. (Claude)

### Added
- `CodexBranchCheckCube` + `CodexBranchCheck_Magenta` material: a temporary, nonblocking magenta marker
  in the Market scene to confirm the editor is running the latest `main`. Remove once the branch-sync
  confusion is settled. (Codex)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.5.2] - 2026-06-08

### Changed
- `AGENTS.md`: added gotcha #9 — never use `UnityEngine.Input.*`; legacy Input is disabled
  (`activeInputHandler = 1`); use New Input System (`Mouse.current`, `Keyboard.current`, etc.). (Claude)
- `MarketUIController`: entire item row is now a `Button` — clicking anywhere on the row triggers
  the action; the separate action button widget is replaced by a right-aligned text label. (Codex)
- `MarketUIController`: `TextOverflowModes.Ellipsis` → `Truncate` on all TMP text, eliminating
  missing-ellipsis-glyph console warnings from LiberationSans SDF. (Codex)
- `MarketUIController`: item icon now renders the sprite on a child RectTransform (3 px inset) over
  the category-color background, so the colour fallback stays visible. (Codex)
- `MarketUIController`: `raycastTarget = false` on all TMP text components for correct click-through. (Codex)

### Fixed
- `MarketUIController`: stall inventory rows now place items on click while keeping price field
  editable; placement logic extracted to `PlaceInventoryItemInFirstFreeSlot`. (Codex)

### Added
- World prefabs for carrot, corn, pumpkin, and bread; assigned to matching `ItemSO.worldPrefab`
  fields so stall placement spawns the correct 3D mesh for all seasonal items. (Codex)
- Kenney preview sprites (`apple/bread/carrot/corn/pumpkin.png`) assigned as `ItemSO.icon`
  so inventory and stall rows show coloured item sprites instead of letter placeholders. (Codex)

### Verification
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Codex + Claude)

## [1.5.1] - 2026-06-08

### Fixed
- `MarketUIController.PositionTooltip`: replaced legacy `UnityEngine.Input.mousePosition` with
  `Mouse.current.position.ReadValue()` (New Input System). Legacy `Input` is disabled in this project
  (`activeInputHandler = 1`), causing ~5000 `InvalidOperationException` per session in Play Mode. (Claude)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.5.0] - 2026-06-08

### Added
- C4: Stall placement price inputs now warn when the entered price is below `ItemSO.BaseBuyPrice` by
  coloring the TMP input red and showing "< закупочной"; placement remains allowed at any positive
  price. (Codex)
- Assigned Kenney preview sprites to the five current `ItemSO` assets so item icons render in market
  UI rows instead of falling back to empty icon slots. (Codex)

### Verification
- Unity `Assets/Refresh`: success after item icon sprite import updates.
- MCP `recompile_scripts`: success, 0 warnings. (Codex)
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Codex + Claude)

## [1.4.0] - 2026-06-08

### Added
- C2: `ItemTooltipTrigger` component (`Market.UI`) — attaches to any UI row and fires
  show/hide callbacks on pointer enter/exit via `IPointerEnterHandler`/`IPointerExitHandler`. (Claude)
- `MarketUIController`: floating hover tooltip (item name, description, buy/sell prices) built
  at runtime; appears for every item row across the Inventory, Supplier, and Stall panels.
  Tooltip follows the mouse cursor and is dismissed on panel open/close. (Claude)

### Changed
- `AGENTS.md`: added a three-line "every session" reminder at the very top —
  `git pull`, implementer/recorder role, no merge/tag. (Claude)

### Verification
- MCP `recompile_scripts`: success after Asset/Refresh (new .cs file needed meta generation).
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0. (Claude)

## [1.3.2] - 2026-06-07

### Changed
- Translated all Russian developer-facing text (XML doc comments, `[Tooltip]` attributes, `Debug.Log/Warning/Error` messages, inline `//` comments) to English across all 42 C# scripts. Player-visible UI strings (panel titles, button labels, season names, currency suffix) intentionally remain in Russian. Eliminates the mojibake patch-targeting issue documented in AGENTS.md gotcha #8. (Claude)

## [1.3.1] - 2026-06-07

### Changed
- `AGENTS.md`: added an "MCP gotchas (hard-won)" subsection capturing recurring time-sinks — test-noise
  vs project failure, don't over-verify, check the Editor is up first, MCP-Play doesn't tick time,
  prefer `update_component` over hand-written scene YAML, new serialized fields need explicit scene
  values, new package `.cs` needs `.meta` + refresh, and keep new code comments in English. (Claude)

## [1.3.0] - 2026-06-07

### Added
- C1: Added `UIModeService` as the single runtime coordinator for game/menu mode. It owns cursor
  lock/visibility, suppresses first-person and interaction input while panels are open, and exposes a
  shared close request for UI panels. (Codex)

### Changed
- Market UI panels now enter/exit UI mode through `UIModeService`; `GameBootstrap` consumes Escape while
  a Market panel is open instead of returning to MainMenu, and `InteractionSystem` clears the active
  prompt when disabled. MainMenu also uses `UIModeService` for menu cursor state. (Codex)

### Verification
- MCP `recompile_scripts`: success, 0 warnings.
- MCP `get_health_report`: ok, compileFailed=false, consoleErrors=0, dirtyScenes=0.
- MCP EditMode tests: 55/55 passed. (Codex)

## [1.2.3] - 2026-06-07

### Changed
- Docs unified to a single-source-of-truth model and translated to English. `dev_plan_3.md` is now
  the only plan + progress file: rewritten in English, merged with the `dev_plan_4` "fun-first"
  direction, pending steps expanded into sub-steps, and every world object tagged by asset
  availability (`[assets: ready/stub/backlog]`) so art-gated work is stubbed or deferred, not blocked.
- `CLAUDE.md` slimmed to Claude's reviewer/verifier/publisher role; coding & architecture rules now
  live once in `AGENTS.md` (shared by both agents) instead of being duplicated. Stale progress and
  `dev_plan_4` references removed. (Claude)

### Removed
- `dev_plan_4.md` (merged into `dev_plan_3.md`).
- `market_game_overview.md` moved to `_ArchiveAssets/docs/` with an "archived, not a spec" banner — it
  described hidden-coefficient mechanics the project rejected. (Claude)

## [1.2.2] - 2026-06-07

### Fixed
- `no-commit-to-main.sh` hook: tighten the branch-ref detection inside the tag-push allowance.
  It now matches an actual ref in the push command (`origin main`, `:master`, `push --all`, …)
  instead of the bare word `main` anywhere in the tool-call JSON, so a Bash `description`
  mentioning "main" no longer trips the guard and blocks a legitimate tag push. (Claude)

## [1.2.1] - 2026-06-07

### Fixed
- `no-commit-to-main.sh` hook: allow Claude's versioning gate to push tags from `main`. An explicit
  tag-only push (`refs/tags/…` or `--tags`, with no branch ref) is now permitted; `git commit` and
  branch pushes to `main`/`master` stay blocked per COLLAB.md. (Claude)

## [1.2.0] - 2026-06-07

### Added
- B10: Added four seasonal `ItemSO` entries to the supplier assortment: carrot (spring), corn (summer),
  pumpkin (autumn), and bread (winter). Apple remains available year-round. (Codex)

### Changed
- Supplier shop UI now refreshes when the season changes, keeps out-of-season goods visible but muted and
  unbuyable, and continues to show the same base buy price for each item. `DebugTimeControl` can skip to
  the next season with `N` for Play Mode verification. (Codex)

### Verification
- Open the supplier, note item prices, press `N` to advance seasons, and confirm only availability changes
  while item prices stay fixed. (Codex)

## [1.1.0] - 2026-06-06
### Added
- Versioning: SemVer git tags `vX.Y.Z`, a root `VERSION` file, and this `CHANGELOG.md`. (Claude)
- `COLLAB.md`: asymmetric review workflow — Codex implements and records changes under `[Unreleased]`;
  Claude reviews, verifies via Unity MCP, bumps the version, tags, and pushes. (Claude)

## [1.0.1] - 2026-06-06
### Changed
- B9: marked `NPCSpawner.targetStall` and `GameSaver.marketStall` as temporary single-stall API and
  documented the planned `MarketStallRegistry`. Comments/attributes only — no behavior change. (Claude)

## [1.0.0] - 2026-06-06
### Added
- Unity 6 / URP Market Game baseline through Block B8: first-person controller, interaction, money +
  inventory, supplier + market stall, NPC visitors/spawner, save format v3, time/season/daylight, ShopUI.
  (Claude + Codex)
- Agent tooling: Unity MCP verification loop; git + Git LFS + private GitHub remote
  (`Bogdychka/market-game`); `COLLAB.md` branch-per-task protocol; Claude Code enforcement hooks;
  Unity-aware C# reviewer subagent. (Claude)

[Unreleased]: https://github.com/Bogdychka/market-game/compare/v1.6.0...HEAD
[1.6.0]: https://github.com/Bogdychka/market-game/compare/v1.5.6...v1.6.0
[1.5.6]: https://github.com/Bogdychka/market-game/compare/v1.5.5...v1.5.6
[1.5.5]: https://github.com/Bogdychka/market-game/compare/v1.5.4...v1.5.5
[1.5.4]: https://github.com/Bogdychka/market-game/compare/v1.5.3...v1.5.4
[1.5.3]: https://github.com/Bogdychka/market-game/compare/v1.5.2...v1.5.3
[1.5.2]: https://github.com/Bogdychka/market-game/compare/v1.5.1...v1.5.2
[1.5.1]: https://github.com/Bogdychka/market-game/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/Bogdychka/market-game/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/Bogdychka/market-game/compare/v1.3.2...v1.4.0
[1.3.2]: https://github.com/Bogdychka/market-game/compare/v1.3.1...v1.3.2
[1.3.1]: https://github.com/Bogdychka/market-game/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/Bogdychka/market-game/compare/v1.2.3...v1.3.0
[1.2.3]: https://github.com/Bogdychka/market-game/compare/v1.2.2...v1.2.3
[1.2.2]: https://github.com/Bogdychka/market-game/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/Bogdychka/market-game/compare/v1.1.0...v1.2.1
[1.1.0]: https://github.com/Bogdychka/market-game/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Bogdychka/market-game/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Bogdychka/market-game/releases/tag/v1.0.0
