# Market Game - Claude Code contract

Standing contract for `C:\Users\bogre\My project`. Keep it short and current; it is NOT a progress log.
Claude is the only agent on this project: it implements, verifies, and records.

## Sources of truth (read these, don't duplicate them)
- **`AGENTS.md`** - coding/architecture/Unity-6/performance rules, MCP gotchas, token discipline,
  available assets. Applies to ALL C# work.
- **`dev_plan_4_1.md`** - the plan and the live progress checkboxes (the only place progress lives).
  (The older `dev_plan_3.md` is superseded and archived under `_ArchiveAssets/docs/`.)
- Live state: Unity Editor via MCP (port 8090), `game.log`, serialized `.unity`/`.prefab`/`.asset`
  files - the truth about Inspector wiring.

## Claude's role: implementer + verifier

Implement one plan step per request, record what changed in `CHANGELOG.md [Unreleased]`
(what/why/how to verify), then verify via MCP: `recompile_scripts` -> `get_health_report` must be
`ok` (0 errors, 0 dirty scenes). Report green/red to the user. **Commit, push and release the work
yourself once it is green** - see "Committing and pushing" below. If MCP can't run, state exactly
what was NOT verified - and don't commit.
Fast path: run `.claude/tools/verify-unity.ps1` (add `-Refresh` after creating files, `-RunTests`
for shared/risky logic).

## Committing and pushing (automatic - no need to ask)
- **Work directly on `main`** - commit and push there. No feature branch, no merge step. Branch only
  when there is a concrete reason (a spike you expect to throw away, or work that must sit
  unfinished across sessions); otherwise `main` is the working branch.
- **Verify BEFORE every commit, and treat that as the hard gate.** With no branch and no merge,
  nothing stands between a commit and `main` except this check, and pushing publishes it
  immediately. Green means `get_health_report` is `ok`: compiles, 0 errors, 0 dirty scenes.
  Red or unverified -> report it and leave the work uncommitted. Never commit on a guess that it
  "should still be fine" because the last check was green - re-run it.
- If something broken does reach `main`, **fix forward** and say so; don't rewrite pushed history.
- **Commit everything in the working tree, not just the files this task touched.** Claude and
  Codex share this repo, so work left uncommitted on one side is work the other silently diverges
  from or overwrites - `.unity`/`.prefab` merge badly enough that a stale tree is the expensive
  failure, not an untidy commit. Include tool-rewritten files (re-serialized scenes, re-baked
  textures) and new untracked assets. Separate anything clearly unrelated into its own commit
  where practical; a mixed commit still beats leaving it on disk.
- **Conventional Commits**, and update `CHANGELOG.md [Unreleased]` in the same push.
- The exclusion list is `.gitignore` (it already covers `Library/`, `Temp/`, `Logs/`, `obj/`,
  `Artifacts/`), plus: never commit secrets, credentials, or `.env` files even if unignored.
- **Finish the release yourself**, without being asked, once the work is committed:
  `git pull --ff-only` -> verify green -> commit -> bump `VERSION` + move `CHANGELOG.md
  [Unreleased]` into a `## [X.Y.Z] - YYYY-MM-DD` section -> `git tag vX.Y.Z` ->
  `git push origin main --follow-tags`.
  PATCH = fixes/chores/docs - MINOR = feature / completed plan step - MAJOR = milestone / breaking.
  Release once per coherent batch of work, not once per commit.
- **Still ask first for:** force-push, history rewrites (rebase/amend of pushed commits),
  `git reset --hard`, reverting someone else's commit, or committing when the user said they were
  mid-edit.
- If a commit fails a hook, fix the cause - never `--no-verify`.

## Git process
- **`main` is always green** - compiles, health `ok`. Never leave broken code on `main`. This is the
  whole safety model now that work goes straight onto `main`: the pre-commit check is the gate.
- **Start from fresh `main`**: `git pull --ff-only` before starting, so you are not building on a
  stale tree - Codex works in this repo too, and `.unity`/`.prefab` merge badly enough that
  diverging is expensive.
- If a branch is genuinely warranted, name it `claude/<plan-step>-<slug>`, lowercase, keep one at a
  time, and delete it once merged. It is the exception, not the default.
- Conventional Commits. No force-push to `main`; no rebasing pushed history;
  no committing build output / `Library/` / `node_modules/`.
- **SemVer (v1.x line):** `VERSION` file holds the number; every released batch gets a
  `CHANGELOG.md` entry and a `vX.Y.Z` tag on `main`. PATCH = fixes/chores/docs/no-behavior
  refactors - MINOR = feature / completed plan step - MAJOR = milestone / breaking save or
  architecture change.

## How to respond
1. **Unity API answers strictly for Unity 6 (6000.x).** Unsure -> say so or check
   `docs.unity3d.com/6000.0`.
2. **Scripts follow `AGENTS.md`**: right `_Project/Scripts/<Subsystem>/` folder, ASCII-English
   everywhere (code text AND player-visible UI strings - the game UI is English), sensible code
   defaults instead of "set 5 fields in the Inspector". Include Editor setup steps - there is no
   direct Editor access, only MCP.
3. **One plan step per request.** Don't skip ahead; tick `dev_plan_4_1.md` after completing a step.
4. **Diagnose from project files first**: serialized `.unity`/`.prefab`/`.asset` values and
   `game.log` (Play Mode issues; mouse is locked in the FPS controller) before guessing from C#.
5. **Refactor as you go** to `AGENTS.md` patterns; new UI reuses `UiFactory`/`MarketPanelView`
   (never hand-rolled rects). Debug scripts -> `_Project/Scripts/Debug/`, `Market.DebugTools`.
6. **Token discipline** per `AGENTS.md`: minimal reads, proportional verification, no ritual
   summaries or re-narration of the contracts.
   Unity scene edits: confirm active scene first, use one precise MCP batch or tiny Editor builder,
   then verify narrowly (`get_gameobject` / targeted `rg`) instead of dumping scene diffs.
7. **Compare against how other games solve it.** For any non-trivial gameplay/system/UX decision
   (saves, NPC AI, economy, progression, controls, content flow...), briefly say how comparable games
   handle it, where our approach sits relative to them, and proactively propose the better-fitting
   option for this game - don't wait to be asked. Keep it a short recommendation, not a survey.

## Current state
See `dev_plan_4_1.md` for what's done and what's next. Don't track progress here.
Debug tooling and keybinds: `AGENTS.md` -> "Debug tooling".
