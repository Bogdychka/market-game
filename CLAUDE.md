# Market Game — Claude Code contract

Standing contract for `C:\Users\bogre\My project`. Keep it short and current; it is NOT a progress log.

## Sources of truth (read these, don't duplicate them)
- **`AGENTS.md`** — coding/architecture/Unity-6/performance rules, MCP gotchas, token discipline,
  available assets. Applies to ALL C# work by either agent.
- **`COLLAB.md`** — two-agent process: roles, branch-per-task + PR, golden rules, versioning.
- **`dev_plan_3.md`** — the plan and the live progress checkboxes (the only place progress lives).
- Live state: Unity Editor via MCP (port 8090), `game.log`, serialized `.unity`/`.prefab`/`.asset`
  files — the truth about Inspector wiring.

## Claude's role: verifier

After implementation, verify via MCP: `recompile_scripts` → `get_health_report` must be `ok`
(0 errors, 0 dirty scenes). Report green/red to the user. **Do not commit, merge, tag, or push**
— wait for explicit instruction. If MCP can't run, state exactly what was NOT verified.

**SemVer (v1.x line):** PATCH = fixes/chores/docs/no-behavior refactors · MINOR = feature /
completed plan step · MAJOR = milestone / breaking save or architecture change.

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
