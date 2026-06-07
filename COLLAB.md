# Agent Collaboration Protocol — Claude Code ↔ Codex

Two AI agents work on this repo, orchestrated by the user. This file is the single source of truth for how
they share the codebase and ship changes. `CLAUDE.md` and `AGENTS.md` both point here.

Repo: **git**, remote `Bogdychka/market-game` (private). Binary assets via **Git LFS**.
Releases are **SemVer** git tags `vX.Y.Z` on `main` (see *Versioning* below).

## Roles (default division of labour)
- **Codex = implementer + recorder.** Writes the code/scene changes for a plan step and **records what it did**
  in the `CHANGELOG.md` `[Unreleased]` section (+ the PR body). Codex does **not** merge to `main` or tag.
- **Claude = reviewer + verifier + publisher.** Reads Codex's diff and its recorded notes, runs the
  `unity-csharp-reviewer` subagent and the Unity MCP loop (`recompile_scripts` → `get_health_report`),
  and only then **versions and pushes** (bump `VERSION`, move the entry to a `vX.Y.Z` heading, tag, merge to `main`).

Either agent may do gameplay work, but **the push to `main` and the version tag are Claude's gate.**

## Shared context (read these before starting)
`CLAUDE.md` + `AGENTS.md` (contracts) · `dev_plan_3.md` (plan + progress checkboxes) · this `COLLAB.md` ·
**`CHANGELOG.md`** (the shared worklog — who did what, per version; `[Unreleased]` = in-flight handoff).
Plus live state: the Unity Editor (MCP on port 8090), `game.log`, and serialized scene/prefab/asset files.

## Golden rules
1. **`main` is always green** — compiles and `get_health_report` is `ok`. Never push broken code to `main`.
2. **One task = one branch = one PR.** No direct commits to `main` (a PreToolUse hook enforces this for Claude).
3. **Don't edit the same files as the other agent at the same time.** Split by subsystem.
4. **One Unity Editor + one working tree.** The agent actively editing owns them; the other must not switch the
   working-tree branch or trigger recompiles underneath it.

## Branch naming
`<agent>/<plan-step>-<slug>`, lowercase. The prefix makes ownership obvious and avoids clashes.
- Claude: `claude/b10-seasonal-assortment`
- Codex:  `codex/c4-stall-ui`

## Workflow

### Codex (implement + record)
1. `git switch main && git pull` → `git switch -c codex/<step>-<slug>`.
2. Implement **one** plan step (per `AGENTS.md` "one step per request").
3. **Record the change** under `CHANGELOG.md` → `[Unreleased]`: what changed, why, and how to verify.
4. Commit (Conventional Commits), push the branch, open a PR (or hand the branch to Claude).
   Do **not** merge to `main` or create tags.

### Claude (review + verify + publish)
5. Review Codex's diff **and** its `[Unreleased]` note. Run the `unity-csharp-reviewer` subagent.
6. Verify in Unity: `recompile_scripts` → `get_health_report` must be `ok` (0 errors).
7. If good: bump `VERSION`, move the `[Unreleased]` entry under a new `## [X.Y.Z]` heading + tick `dev_plan_3.md`.
8. Merge the PR to `main`, then tag the merge commit: `git tag -a vX.Y.Z -m "..."` and `git push origin vX.Y.Z`.
   (Push tags from a feature branch or right after merge — the no-commit-to-main hook blocks `git push` while on `main`.)
9. If not good: leave review comments / request changes; Codex iterates on its branch.

## Versioning (SemVer, tag every shipped change)
- Format `vMAJOR.MINOR.PATCH`, current line **v1.x**. The `VERSION` file holds the number; tags mark `main`.
- **PATCH** (`v1.0.x`): fixes, chores, docs, no-behavior-change refactors.
- **MINOR** (`v1.x.0`): a new feature / completed gameplay plan step.
- **MAJOR** (`vX`): a big milestone / breaking save or architecture change.
- Every merged change gets a `CHANGELOG.md` entry and a tag. Tag the commit on `main` after merge.

## Conflict-prone files — extra care
- **Scenes / prefabs / assets** (`*.unity`, `*.prefab`, `*.asset`): YAML, painful to merge. Only ONE agent
  touches a given scene/prefab per task; commit and merge promptly; rebase on fresh `main` if the other merged scene changes.
- `dev_plan_3.md`, `CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md`, `COLLAB.md`: line-level conflicts are easy — keep both agents' lines.

## Don't
- No force-push to `main` or to the other agent's branch. Force-push only your **own** feature branch.
- No rebasing shared/merged history.
- No `git push` of branch refs from `main` (use the PR + tag flow). No committing build output, `Library/`, `node_modules/`.
- Codex does not merge to `main` or create version tags.

## Cheatsheet
```bash
# Codex: start + record
git switch main && git pull && git switch -c codex/<step>-<slug>
#   ...implement, add an entry under CHANGELOG.md [Unreleased]...
git push -u origin HEAD && gh pr create --fill

# Claude: review, verify (MCP), then publish
gh pr merge <n> --squash --delete-branch
git fetch origin
git tag -a vX.Y.Z origin/main -m "vX.Y.Z - <summary>"   # tag the merged tip
git push origin vX.Y.Z                                   # from a feature branch (hook blocks push on main)
git switch main && git pull
```
