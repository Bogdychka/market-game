# Agent Collaboration Protocol — Claude Code ↔ Codex

Two AI agents share this repo, orchestrated by the user. This file is the single source of truth for
the process. Repo: **git**, remote `Bogdychka/market-game` (private), **Git LFS** for binaries.
Releases: SemVer tags `vX.Y.Z` on `main`.

## Roles
- **Codex = implementer + recorder.** Implements one plan step per branch and records what it did in
  `CHANGELOG.md [Unreleased]` (+ PR body). Never merges to `main`, never tags.
- **Claude = verifier.** Runs the MCP loop (`recompile_scripts` → `get_health_report`), reports
  green/red to the user. Does **not** commit, merge, tag, or push without explicit user instruction.

Either agent may do gameplay work.

## Golden rules
1. **`main` is always green** — compiles, health `ok`. Never push broken code to `main`.
2. **Don't edit the same files as the other agent at the same time.** Scenes/prefabs: one agent per task.
3. **One Unity Editor + one working tree.** The actively-editing agent owns them; the other must not
   switch branches or trigger recompiles underneath.

## Branches
`<agent>/<plan-step>-<slug>`, lowercase — e.g. `claude/b10-seasonal-assortment`, `codex/c4-stall-ui`.

## Workflow
**Codex:** `git switch main && git pull` → `git switch -c codex/<step>-<slug>` → implement ONE plan
step → record under `CHANGELOG.md [Unreleased]` (what/why/how to verify) → Conventional Commit →
`git push -u origin HEAD` → `gh pr create --fill`.

**Claude:** MCP verify → report green/red to the user. Wait for user to instruct
commit / merge / tag / push.

## Versioning
`VERSION` file holds the number; every merged change gets a `CHANGELOG.md` entry and a tag on `main`.
**PATCH** fixes/chores/docs/no-behavior refactors · **MINOR** feature / completed plan step ·
**MAJOR** milestone / breaking save or architecture change. Current line **v1.x**.

## Conflict-prone files
- Scenes/prefabs/assets (`*.unity`, `*.prefab`, `*.asset`): Unity YAML, painful merges — ONE agent
  per task, merge promptly, rebase on fresh `main` if the other merged scene changes.
- `dev_plan_3.md`, `CHANGELOG.md`, contract files: line-level conflicts — keep both agents' lines.

## Don't
- No force-push to `main` or to the other agent's branch (own feature branch only).
- No rebasing shared/merged history. No branch pushes from `main` (PR + tag flow only).
- No committing build output / `Library/` / `node_modules/`.
- Codex does not merge to `main` or create version tags.
