import json
import sys
import re

data = json.load(sys.stdin)
cmd = data.get("tool_input", {}).get("command", "")

# Matches:  cd "...VANTAGE..." &&     or     cd '...VANTAGE...' &&
# Both single- and double-quoted forms with VANTAGE (any case) inside the path.
if re.match(r"^cd\s+[\"\'][^\"\']*[Vv][Aa][Nn][Tt][Aa][Gg][Ee][^\"\']*[\"\']\s*&&", cmd):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": (
                "Command starts with `cd \"...VANTAGE...\" &&` — the cwd is already "
                "the VANTAGE repo root. Call git/dotnet/etc. bare so the existing "
                "`git:*` / `dotnet:*` allowlist matches. The cd prefix breaks "
                "allowlist matching and forces a permission prompt on every op."
            )
        }
    }))
