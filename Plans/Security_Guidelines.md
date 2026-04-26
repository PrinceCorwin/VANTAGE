# Guide: Security & Defensive Coding

For Claude Code when touching SQL, file paths, exports, AI/Bedrock calls, logging, or feedback submission. CLAUDE.md's Workflow Skills section names the trigger conditions that should send you here.

VANTAGE is internal Summit Industrial software, so the threat model is mostly accidental misuse, paste-bombs, and Excel formula injection at the customer's desk — not a determined external attacker. Apply judgment: defenses below are calibrated to that model.

---

## CSV / Excel formula injection

Cell values starting with `=`, `+`, `-`, or `@` execute as formulas when the file opens in Excel. User-typed values flowing into export cells must be prefixed with `'` to neutralize them.

Applies to `ScheduleExcelExporter` and any future CSV/XLSX export.

```csharp
private static string SanitizeForExcel(string? value)
{
    if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
    char first = value[0];
    return (first == '=' || first == '+' || first == '-' || first == '@')
        ? "'" + value
        : value;
}
```

Apply to free-text columns (`Description`, `MissedReason`, notes). Numeric / date / enum columns don't need it.

---

## File and path operations

- Always `Path.Combine`, never string concatenation.
- User-influenced filenames (WorkPackage → PDF, ProjectID → folder name) must be sanitized: strip path separators, control chars, and Windows reserved names.
- Reject `..` traversal. File reads/writes stay inside allowlisted base directories (the project's WorkPackages folder, exports folder, etc.).
- The accidental `nul` git artifact incident is the same Windows-reserved-name family — sanitize the same set.

```csharp
private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "CON", "PRN", "AUX", "NUL",
    "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
    "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
};

private static string SanitizeFilename(string input)
{
    foreach (char c in Path.GetInvalidFileNameChars())
        input = input.Replace(c, '_');
    string baseName = Path.GetFileNameWithoutExtension(input);
    if (ReservedNames.Contains(baseName))
        input = "_" + input;
    return input;
}
```

---

## AI / Bedrock input bounds and response validation

The AI Takeoff module sends user-supplied PDFs and prompt content to Bedrock and Claude API. Both directions need guards.

**Outbound (before sending):**
- Tokenize and bound prompt content. A 100k-row paste-bomb into a Description field shouldn't reach Bedrock.
- Validate file size and page count before sending PDFs. Pre-render guards in `render_pdf_page` exist for memory; add upstream size guards too so we fail fast.
- Reject obviously invalid inputs at the C# boundary, not inside the Lambda.

**Inbound (parsing AI responses):**
- Treat AI output as untrusted input. Validate JSON shape before deserializing.
- Range-check numeric fields: percentages 0–100, MHs ≥ 0, counts within plausible bounds.
- Reject malformed responses rather than coercing or defaulting silently — a malformed response should surface as an error, not as zeroed quantities.

---

## Logging hygiene

- Never log connection strings, passwords, API keys, `Credentials.cs` values, or full Anthropic/AWS request and response bodies.
- Log IDs and counts, not full `Activity` records — `Description` may contain client-identifying detail.
- `SqlException.Message` can echo parameter values back. Log via `AppLogger.Error(ex, "Class.Method")` (which is controlled). Never string-format `ex.Message` into a user-visible MessageBox or status bar.
- AI service errors surface as generic wording in UI ("AI service unavailable"). Detail goes to `AppLogger` only.

```csharp
// WRONG - exposes parameter values + connection details to the user
MessageBox.Show($"Database error: {ex.Message}");

// RIGHT
AppLogger.Error(ex, "ActivityRepository.UpdateActivityInDatabase");
MessageBox.Show("Could not save activity. See log for details.");
```

---

## FeedbackDialog secret-pattern check (post-V1 hardening)

Users paste arbitrary text into the Feedback Board, which syncs to Azure. Future hardening: scan submissions for connection-string and API-key patterns before send and warn the user. Not blocking for V1, but worth queuing.

Patterns to flag:
- `Server=...;Database=...;` style connection strings
- Anthropic key prefix `sk-ant-`
- AWS access key prefix `AKIA`
- Long base64-looking blobs (>64 chars, no spaces)
