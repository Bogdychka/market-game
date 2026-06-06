#!/usr/bin/env bash
# PreToolUse(Bash|PowerShell): block `git commit` / `git push` while on main/master.
# Enforces COLLAB.md "no direct commits to main". Reads the tool-call JSON on stdin.
input=$(cat)
case "$input" in
  *"git commit"*|*"git push"*) ;;
  *) exit 0 ;;
esac
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
case "$branch" in
  main|master)
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"COLLAB.md: no direct commit/push to %s. Create a feature branch (git switch -c claude/<step>-<slug>) and open a PR instead."}}\n' "$branch"
    ;;
esac
exit 0
