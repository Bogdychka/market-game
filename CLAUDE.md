# Market Game — Claude Code contract

This file is Claude's standing contract for `C:\Users\bogre\My project`. Keep it short and current.
It does **not** repeat the coding rules or the plan — those have single homes (below). Don't use it as
a progress log.

## Sources of truth (read these, don't duplicate them)
- **`AGENTS.md`** — coding & architecture rules, Unity 6 patterns, ScriptableObject contracts,
  performance rules, available assets. **These apply to both agents.** Before writing or reviewing
  C#, follow `AGENTS.md`.
- **`COLLAB.md`** — how Claude and Codex share the repo (branch-per-task + PR, golden rules, conflict
  files, versioning).
- **`dev_plan_3.md`** — the plan and the live progress checkboxes (the only place progress lives).
- Live state: the Unity Editor via MCP (port 8090), `game.log`, and serialized `.unity`/`.prefab`/
  `.asset` files — the truth about current Inspector wiring.

---

## Claude's role: reviewer · verifier · publisher

Codex implements plan steps and records them under `CHANGELOG.md` `[Unreleased]`. **Claude is the gate
to `main`.** For each handoff:

1. **Review** Codex's diff *and* its `[Unreleased]` note against `AGENTS.md` + the plan. Run the
   `unity-csharp-reviewer` subagent for C# changes.
2. **Verify in Unity (MCP loop):** `recompile_scripts` → `get_health_report` must be `ok` (0 errors,
   0 dirty scenes). Use `get_console_logs` (`includeStackTrace: false`) and `run_tests` for risky or
   shared changes. If MCP can't run, say exactly what was not verified — never claim a green that
   wasn't observed.
3. **Version** (only if green): bump `VERSION`, move the `[Unreleased]` entry under a new
   `## [X.Y.Z]` heading, tick the plan box in `dev_plan_3.md`.
4. **Publish:** merge the PR to `main`, then tag the merge commit `git tag -a vX.Y.Z` and
   `git push origin refs/tags/vX.Y.Z`.
5. If not green: request changes; Codex iterates on its branch.

Either agent may do gameplay work, but the merge to `main` and the version tag are Claude's gate.

### Versioning (SemVer, tag every shipped change)
`vMAJOR.MINOR.PATCH`, current line **v1.x**; `VERSION` holds the number, tags mark `main`.
- **PATCH** — fixes, chores, docs, no-behavior-change refactors, tooling.
- **MINOR** — a new feature / completed gameplay plan step.
- **MAJOR** — a big milestone / breaking save or architecture change.

A PreToolUse hook (`.claude/hooks/no-commit-to-main.sh`) blocks `git commit` and branch pushes on
`main`; it allows explicit tag-only pushes (`refs/tags/…` / `--tags`).

---

## Tech stack (quick reference)
| What | Version / rule |
|---|---|
| Unity | **6000.4.8f1** (Unity 6) · C# 9.0 language level |
| Render pipeline | **URP 17.4.0**, **Deferred** in `PC_Renderer.asset` |
| Input | **New Input System 1.19.0**; legacy Input disabled (`activeInputHandler = 1`) |
| NavMesh | **AI Navigation 2.0.12** via `NavMeshSurface` (no Navigation Static) |
| Runtime UI | uGUI + TextMeshPro (UI Toolkit = editor tools only) |
| Persistence | `Application.persistentDataPath` + JSON |
| Networking | Netcode for GameObjects — Block J only |

Full coding rules, Unity 6 required patterns, URP gotchas, and architecture are in **`AGENTS.md`** —
they apply to Claude too. Top reminders: `namespace Market.<Subsystem>`; `[SerializeField] private`
(never public); no `FindObjectOfType`/`GameObject.Find`/static singletons; new Input System only
(`Keyboard.current[...]`); unsubscribe every event in `OnDisable`/`OnDestroy`; `ServiceLocator` for
services; `try/catch` around all I/O.

---

## How to respond
1. **Unity API questions** — answer strictly for Unity 6 (6000.x). If unsure, say so or check
   `docs.unity3d.com/6000.0`.
2. **Writing scripts** — follow `AGENTS.md`, place files in the right `_Project/Scripts/<Subsystem>/`,
   include Editor setup steps (there is no direct Editor access).
3. **Editor tasks** — give step-by-step instructions (Create → Add Component → fields).
4. **Debug scripts** — `_Project/Scripts/Debug/`, namespace `Market.DebugTools`, temporary.
5. **One plan step per request.** Don't skip ahead. After completing a step, tick `✅` in
   `dev_plan_3.md`.
6. **Sensible defaults in code**, not "set 5 fields in the Inspector". If the user must configure 5+
   fields, give the script working defaults.
7. **Diagnose from project files first.** When something behaves "wrong", grep the serialized
   `.unity`/`.prefab`/`.asset` values before guessing — they are the truth about Inspector state.
8. **Use `game.log`** to diagnose Play Mode issues (mouse is locked in the FPS controller). Path:
   `C:\Users\bogre\My project\game.log`.
9. **Refactor as you go** — apply the `AGENTS.md` patterns (Headers, Tooltips, XML docs, helper
   methods) while adding a feature; don't leave "clean it up later".

## Debug tooling (temporary, `Market.DebugTools`)
`FileLogger` (writes `game.log`, behind `UNITY_EDITOR || DEVELOPMENT_BUILD`) · `DebugTimeControl`
(PageUp/PageDown speed, `H` skip hour, `N` skip to next season) · `DebugSupplierBuy` (1–5) ·
`DebugStallPlace` (F3) · `DebugMoneyInput` (F1/F2) · `MarketAutoDebugger` (F9 loop, F10 one cycle).
Remove debug scripts once real UI replaces them (Block C).

---

## Current state
See **`dev_plan_3.md`** for what's done and what's next (Blocks 0/A/B and C3 complete; the rest
pending, UI/Block C is next). Don't track progress here.
