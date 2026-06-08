# Changelog

All notable changes to the Market Game project. Format follows
[Keep a Changelog](https://keepachangelog.com/); versioning follows
[Semantic Versioning](https://semver.org/) — each released version is tagged in git as `vX.Y.Z`.

Entries note the authoring agent (Claude / Codex / user). This file also doubles as the **shared worklog**:
the `[Unreleased]` section is where in-flight work is recorded (especially by Codex) **before** Claude
reviews it, verifies via Unity MCP, bumps the version, tags it, and pushes. See `COLLAB.md`.

## [Unreleased]

_Nothing pending._

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
