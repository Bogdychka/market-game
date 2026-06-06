#!/usr/bin/env bash
# PostToolUse(Edit|Write|MultiEdit): after a .cs edit, remind the agent to verify
# via Unity MCP before committing (COLLAB.md). Non-blocking. Reads tool JSON on stdin.
input=$(cat)
case "$input" in
  *'.cs"'*)
    printf '%s' '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"C# changed - verify with Unity MCP (recompile_scripts then get_health_report) before committing, per COLLAB.md."}}'
    ;;
esac
exit 0
