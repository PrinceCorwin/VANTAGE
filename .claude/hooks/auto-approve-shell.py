import json
import sys

# Auto-approve Bash and PowerShell tool calls so shell commands never trigger a
# permission prompt.
#
# Why this exists: Claude Code refuses to wildcard-allowlist any shell command
# that contains embedded expressions ($var, $(...), string interpolation, "$env:").
# Such commands are ALWAYS forced to a manual approval prompt regardless of the
# allow-list, because they can't be statically pattern-matched. Since nearly every
# PowerShell command uses $env:USERPROFILE or a variable, allow-list entries can
# never stop the prompts. A PreToolUse hook returning an explicit "allow" decision
# is the only mechanism that overrides that gate.
#
# Scope: shell tools only (Bash, PowerShell). Non-shell tools fall through to the
# normal permission flow untouched. ALL shell commands are approved, including
# "cd ..." — block-cd-vantage.py runs as a separate PreToolUse hook and its "deny"
# takes precedence over this "allow", so it still blocks its specific
# `cd "...VANTAGE..." &&` pattern. Do NOT re-add a cd carve-out here: it created a
# gap where cd-prefixed commands that block-cd didn't match (e.g. newline instead
# of &&) fell through to a manual prompt.

def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        # Malformed input — stay silent, let the normal flow decide.
        sys.exit(0)

    tool = data.get("tool_name", "")
    if tool not in ("Bash", "PowerShell"):
        sys.exit(0)

    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "allow",
            "permissionDecisionReason": "Shell auto-approved per standing user request (auto-approve-shell.py hook)."
        }
    }))
    sys.exit(0)


if __name__ == "__main__":
    main()
