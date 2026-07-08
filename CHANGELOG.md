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

### Changed
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
