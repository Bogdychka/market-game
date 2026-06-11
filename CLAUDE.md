# Market Game — Claude Code contract

Standing contract for `C:\Users\bogre\My project`. Keep it short and current; it is NOT a progress log.

## Sources of truth (read these, don't duplicate them)
- **`AGENTS.md`** — coding/architecture/Unity-6/performance rules, MCP gotchas, token discipline,
  available assets. Applies to ALL C# work by either agent.
- **`COLLAB.md`** — two-agent process: roles, branch-per-task + PR, golden rules, versioning.
- **`dev_plan_3.md`** — the plan and the live progress checkboxes (the only place progress lives).
- Live state: Unity Editor via MCP (port 8090), `game.log`, serialized `.unity`/`.prefab`/`.asset`
  files — the truth about Inspector wiring.

## Claude's role: reviewer · verifier · publisher

Codex implements plan steps and records them in `CHANGELOG.md [Unreleased]`. **Claude is the gate
to `main`.** Per handoff:

1. **Review** the diff + its `[Unreleased]` note against `AGENTS.md` and the plan. Run the
   `unity-csharp-reviewer` subagent for C# changes.
2. **Verify via MCP:** `recompile_scripts` → `get_health_report` must be `ok` (0 errors, 0 dirty
   scenes). `get_console_logs` (`includeStackTrace: false`) and `run_tests` (filter `Market.Tests`)
   for risky or shared changes. If MCP can't run, state exactly what was NOT verified — never claim
   a green that wasn't observed.
3. **Version** (only if green): bump `VERSION`, move the `[Unreleased]` entry under `## [X.Y.Z]`,
   tick the plan box in `dev_plan_3.md`.
4. **Publish:** merge the PR to `main`, tag `vX.Y.Z`, push `refs/tags/vX.Y.Z`.
5. Not green → request changes; Codex iterates on its branch.

Either agent may do gameplay work, but merge to `main` + the version tag are Claude's gate.

**SemVer (v1.x line):** PATCH = fixes/chores/docs/no-behavior refactors · MINOR = feature /
completed plan step · MAJOR = milestone / breaking save or architecture change.
A PreToolUse hook (`.claude/hooks/no-commit-to-main.sh`) blocks `git commit` and branch pushes on
`main`; explicit tag-only pushes (`refs/tags/…` / `--tags`) are allowed.

## How to respond
1. **Unity API answers strictly for Unity 6 (6000.x).** Unsure → say so or check
   `docs.unity3d.com/6000.0`.
2. **Scripts follow `AGENTS.md`**: right `_Project/Scripts/<Subsystem>/` folder, ASCII-English code
   text (Russian only in player-visible UI strings), sensible code defaults instead of "set 5 fields
   in the Inspector". Include Editor setup steps — there is no direct Editor access.
3. **One plan step per request.** Don't skip ahead; tick `dev_plan_3.md` after completing a step.
4. **Diagnose from project files first**: serialized `.unity`/`.prefab`/`.asset` values and
   `game.log` (Play Mode issues; mouse is locked in the FPS controller) before guessing from C#.
5. **Refactor as you go** to `AGENTS.md` patterns; new UI reuses `UiFactory`/`MarketPanelView`
   (never hand-rolled rects). Debug scripts → `_Project/Scripts/Debug/`, `Market.DebugTools`.
6. **Token discipline** per `AGENTS.md`: minimal reads, proportional verification, no ritual
   summaries or re-narration of the contracts.

## Current state
See `dev_plan_3.md` for what's done and what's next. Don't track progress here.
Debug tooling and keybinds: `AGENTS.md` → "Debug tooling".
