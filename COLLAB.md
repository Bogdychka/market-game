# Agent Collaboration Protocol — Claude Code ↔ Codex

Two AI agents work on this repo, orchestrated by the user. This file is the
single source of truth for how they share the codebase without clobbering each
other. `CLAUDE.md` and `AGENTS.md` both point here.

Repo: **git**, remote `Bogdychka/market-game` (private). Binary assets via **Git LFS**.

## Golden rules
1. **`main` is always green** — it compiles and `get_health_report` is `ok`. Never push broken code to `main`.
2. **One agent = one task = one branch = one PR.** No direct commits to `main` (the VC bootstrap commit is the only exception).
3. **Don't edit the same files as the other agent at the same time.** Split by subsystem.
4. **There is ONE Unity Editor** (MCP on port 8090) and **ONE working tree.** The agent actively editing + verifying owns them. The other agent must not switch the working-tree branch or trigger recompiles underneath it.

## Branch naming
`<agent>/<plan-step>-<slug>`, lowercase. The agent prefix makes ownership obvious and prevents name clashes.
- Claude: `claude/b9-stall-registry`, `claude/c1-inventory-ui`
- Codex:  `codex/c4-stall-ui`, `codex/fix-save-npc`

## Workflow (every task)
1. `git switch main && git pull` — start from latest.
2. `git switch -c <agent>/<step>-<slug>` — branch for the task.
3. Implement **one** plan step (per `CLAUDE.md` / `AGENTS.md` "one step per request").
4. **Verify via MCP**: `recompile_scripts` → `get_health_report` must be `ok`.
5. Tick the matching checkbox in `dev_plan_3.md` **in the same branch**.
6. Commit — Conventional Commits: `feat(npc): …`, `fix(save): …`, `chore: …`.
   - Claude appends: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
7. `git push -u origin <branch>`.
8. `gh pr create` — title = the step; body = what changed + verification (paste the health-report summary) + plan-step reference.
9. User (or the other agent) reviews and merges. Then everyone `git switch main && git pull`.

## Conflict-prone files — extra care
- **Scenes / prefabs / assets** (`*.unity`, `*.prefab`, `*.asset`): YAML, painful to merge. Only ONE agent touches a given scene/prefab per task; commit and merge promptly; rebase your branch on fresh `main` before continuing if the other agent merged scene changes.
- `dev_plan_3.md`, `CLAUDE.md`, `AGENTS.md`, `COLLAB.md`: line-level conflicts are easy — keep both agents' lines.

## Don't
- No force-push to `main` or to the other agent's branch. Force-push only your **own** feature branch.
- No rebasing shared/merged history.
- No committing build output, `Library/`, `node_modules/` (already in `.gitignore`).

## Cheatsheet
```bash
# start new work
git switch main && git pull && git switch -c claude/<step>-<slug>
# after verifying + committing
git push -u origin HEAD && gh pr create --fill
# sync after a merge
git switch main && git pull
```
