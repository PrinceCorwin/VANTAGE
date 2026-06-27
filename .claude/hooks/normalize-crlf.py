#!/usr/bin/env python3
# PostToolUse hook: normalize the just-written/edited file to CRLF line endings.
# Removes the need to run manual PowerShell line-ending fix-ups after every edit.
import sys, json, os

TEXT_EXTS = {
    ".cs", ".xaml", ".md", ".json", ".txt", ".html", ".htm", ".xml",
    ".csproj", ".sln", ".config", ".ps1", ".py", ".js", ".ts", ".css",
    ".sql", ".yml", ".yaml", ".gitignore", ".gitattributes", ".editorconfig",
}


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        return

    ti = data.get("tool_input") or {}
    tr = data.get("tool_response") or {}
    path = ti.get("file_path") or tr.get("filePath")
    if not path or not os.path.isfile(path):
        return

    _, ext = os.path.splitext(path)
    if ext.lower() not in TEXT_EXTS:
        return

    try:
        with open(path, "rb") as f:
            raw = f.read()
        if b"\x00" in raw:  # binary guard
            return
        text = raw.decode("utf-8", errors="surrogateescape")
        norm = text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n")
        if norm != text:
            with open(path, "wb") as f:
                f.write(norm.encode("utf-8", errors="surrogateescape"))
    except Exception:
        return


if __name__ == "__main__":
    main()
