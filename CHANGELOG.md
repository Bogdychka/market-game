# Changelog

All notable changes to the Market Game project. Format follows
[Keep a Changelog](https://keepachangelog.com/); versioning follows
[Semantic Versioning](https://semver.org/) — each released version is tagged in git as `vX.Y.Z`.

Entries note the authoring agent (Claude / Codex / user). This file also doubles as the **shared worklog**:
the `[Unreleased]` section is where in-flight work is recorded (especially by Codex) **before** Claude
reviews it, verifies via Unity MCP, bumps the version, tags it, and pushes. See `COLLAB.md`.

## [Unreleased]

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

[Unreleased]: https://github.com/Bogdychka/market-game/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/Bogdychka/market-game/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Bogdychka/market-game/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Bogdychka/market-game/releases/tag/v1.0.0
