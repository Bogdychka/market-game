#!/usr/bin/env bash
# PreToolUse(Bash|PowerShell): block `git commit` / `git push` while on main/master.
# Enforces COLLAB.md "no direct commits to main". Reads the tool-call JSON on stdin.
input=$(cat)
case "$input" in
  *"git commit"*|*"git push"*) ;;
  *) exit 0 ;;
esac
# Allow Claude's versioning gate: pushing a tag is part of the COLLAB.md merge-to-main
# role, not a direct commit. Recognise an explicit tag-only push (refs/tags/… or --tags)
# and let it through, as long as no branch ref (main/master) is being pushed alongside.
case "$input" in
  *"git push"*)
    case "$input" in
      *"refs/tags/"*|*"--tags"*)
        case "$input" in
          *main*|*master*) ;;   # branch ref also present — fall through to the guard
          *) exit 0 ;;          # pure tag push — allowed on any branch
        esac
        ;;
    esac
    ;;
esac
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
case "$branch" in
  main|master)
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"COLLAB.md: no direct commit/push to %s. Create a feature branch (git switch -c claude/<step>-<slug>) and open a PR instead."}}\n' "$branch"
    ;;
esac
exit 0
