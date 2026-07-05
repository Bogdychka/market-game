# Market Game — Claude Code contract

Standing contract for `C:\Users\bogre\My project`. Keep it short and current; it is NOT a progress log.
Claude is the only agent on this project: it implements, verifies, and records.

## Sources of truth (read these, don't duplicate them)
- **`AGENTS.md`** — coding/architecture/Unity-6/performance rules, MCP gotchas, token discipline,
  available assets. Applies to ALL C# work.
- **`dev_plan_3.md`** — the plan and the live progress checkboxes (the only place progress lives).
- Live state: Unity Editor via MCP (port 8090), `game.log`, serialized `.unity`/`.prefab`/`.asset`
  files — the truth about Inspector wiring.

## Claude's role: implementer + verifier

Implement one plan step per request, record what changed in `CHANGELOG.md [Unreleased]`
(what/why/how to verify), then verify via MCP: `recompile_scripts` → `get_health_report` must be
`ok` (0 errors, 0 dirty scenes). Report green/red to the user. **Do not commit, merge, tag, or
push** — wait for explicit instruction. If MCP can't run, state exactly what was NOT verified.

## Git process
- **`main` is always green** — compiles, health `ok`. Never leave broken code on `main`.
- **Branch only from fresh `main`**: `git switch main && git pull --ff-only` → then branch.
  Never fork the next branch while a scene/prefab fix is still unmerged — `.unity`/`.prefab`
  merge badly, so the fix gets silently lost when forking from a stale `main`.
- Branch naming: `claude/<plan-step>-<slug>`, lowercase (e.g. `claude/e2-crop-watering`).
  Keep **one active feature branch at a time**; delete it once merged.
- Conventional Commits. No force-push to `main`; no rebasing merged history;
  no committing build output / `Library/` / `node_modules/`.
- **SemVer (v1.x line):** `VERSION` file holds the number; every merged change gets a
  `CHANGELOG.md` entry and a `vX.Y.Z` tag on `main`. PATCH = fixes/chores/docs/no-behavior
  refactors · MINOR = feature / completed plan step · MAJOR = milestone / breaking save or
  architecture change.

## How to respond
1. **Unity API answers strictly for Unity 6 (6000.x).** Unsure → say so or check
   `docs.unity3d.com/6000.0`.
2. **Scripts follow `AGENTS.md`**: right `_Project/Scripts/<Subsystem>/` folder, ASCII-English
   everywhere (code text AND player-visible UI strings — the game UI is English), sensible code
   defaults instead of "set 5 fields in the Inspector". Include Editor setup steps — there is no
   direct Editor access, only MCP.
3. **One plan step per request.** Don't skip ahead; tick `dev_plan_3.md` after completing a step.
4. **Diagnose from project files first**: serialized `.unity`/`.prefab`/`.asset` values and
   `game.log` (Play Mode issues; mouse is locked in the FPS controller) before guessing from C#.
5. **Refactor as you go** to `AGENTS.md` patterns; new UI reuses `UiFactory`/`MarketPanelView`
   (never hand-rolled rects). Debug scripts → `_Project/Scripts/Debug/`, `Market.DebugTools`.
6. **Token discipline** per `AGENTS.md`: minimal reads, proportional verification, no ritual
   summaries or re-narration of the contracts.
   Unity scene edits: confirm active scene first, use one precise MCP batch or tiny Editor builder,
   then verify narrowly (`get_gameobject` / targeted `rg`) instead of dumping scene diffs.
7. **Compare against how other games solve it.** For any non-trivial gameplay/system/UX decision
   (saves, NPC AI, economy, progression, controls, content flow…), briefly say how comparable games
   handle it, where our approach sits relative to them, and proactively propose the better-fitting
   option for this game — don't wait to be asked. Keep it a short recommendation, not a survey.

## Current state
See `dev_plan_3.md` for what's done and what's next. Don't track progress here.
Debug tooling and keybinds: `AGENTS.md` → "Debug tooling".
