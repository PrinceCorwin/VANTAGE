---
name: finisher
description: >
  End-of-session workflow that updates project documentation and commits changes.
  Trigger when the user says "finisher" or "commit". Do NOT trigger on "finish",
  "wrap up", or other variations — only the exact words "finisher" or "commit".
---

# Finisher

End-of-session automation that documents completed work, updates the user manual,
and commits/pushes all changes. Run all steps automatically in sequence without
pausing for user review.

## Prerequisites

Before running finisher, confirm:
1. All code changes have been tested and confirmed working by the user
2. A `dotnet build` has succeeded (or is not applicable to the session's changes)

If these are not confirmed, ask the user before proceeding.

## Step 1: Update Plans/Project_Status.md

- Read the current `Plans/Project_Status.md`
- Analyze the conversation to identify which backlog/todo items were completed
- Remove completed items from the backlog
- If new work was identified during the session that is NOT yet complete, add it to the appropriate section
- Preserve the existing file structure and formatting

## Step 2: Update Plans/Completed_Work.md

### Month rollover check
Before adding entries, scan ALL `###` date headers in `Completed_Work.md` — not just the first one:
1. Read `Completed_Work.md` and collect every `###` date header
2. Check if ANY entries are from a month older than the current month
3. **If old-month entries exist:**
   - Move those entries (and only those) into an archive file at `Plans/Archives/Completed_Work_YYYY-MM.md` (using the OLD month's year-month)
   - If the archive file already exists, append to it
   - Archive file header format: `# VANTAGE: Milestone - Completed Work (Month YYYY)` (e.g., "March 2026")
   - Archive file contains only the entries (the `###` date sections), no `## Unreleased` header or intro text
   - Keep current-month entries in `Completed_Work.md` with the standard header/intro:
     ```
     # VANTAGE: Milestone - Completed Work

     This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

     ---

     ## Unreleased
     ```
   - End the file with: `---` and `**Archives:** See Plans/Archives/ for previous months.`
4. **If all entries are current month:** Add the new entry at the top as normal (below `## Unreleased`)

### Adding entries
- Add a new date-stamped section at the TOP of the changelog (below `## Unreleased`)
- Use today's date as the header, matching the existing date format in the file
- Write concise but descriptive bullet points covering what was accomplished
- Group related changes logically
- Include technical details that would help someone understand what changed (file names, feature names, behavioral changes)

## Step 3: Update Help/manual.html

- Read the current `Help/manual.html`
- Determine if the session's work affects any user-facing functionality
- If YES:
  - Add new sections/subsections for new features
  - Update existing sections if behavior changed
  - Remove documentation for deleted features
  - Update the Table of Contents with anchor links for any new sections or subsections added
  - Match the existing HTML structure, styling, and conventions in the file
  - **Screenshot check:** Review whether any new or modified sections need screenshots that don't exist yet. If so, add placeholder `<img>` tags with descriptive alt text, add a TODO note in `Plans/Project_Status.md` to capture them, and alert the user (e.g., "You'll need a screenshot of the Config Creator window for the AI Takeoff section"). Do NOT block the commit on this — just flag it.
  - If a section already has screenshots and the UI changed significantly, alert the user that the screenshots may need to be re-captured.
- If NO user-facing changes were made, skip this step

## Step 3.5: Update Plans/Decisions.md

- Review the session's work for any design decisions, architectural choices, or implementation rationale
- A "decision" is a choice between alternatives with reasoning — not routine bug fixes or feature additions
- Examples: "chose X over Y because...", "removed X because...", "changed approach from X to Y because...", data format choices, things intentionally NOT done and why
- If any decisions were made:
  - Read `Plans/Decisions.md` and add new entries under the appropriate section
  - Follow the existing format: `### Title`, `**Decision:**`, `**Why:**`, `**Date:**`
  - If no existing section fits, create one
- If no meaningful design decisions were made this session, skip this step

## Step 3.6: Check Feature-Specific Plan Docs

- If the session's work relates to a specific feature that has its own plan document in `Plans/` (e.g., `WorkPackage_Module_Plan.md`, `Schedule_Module_plan.md`), review and update that document as needed
- If not applicable, skip this step

## Step 3.7: UserSettings Registry Sync

If the session's changes did any of the following to user settings, update `Utilities/UserSettingsRegistry.cs` before commit:
- **Added** a new `SetUserSetting` / `GetUserSetting` / `RemoveUserSetting` call with a new key name
- **Removed** the last call site of a key (the key is now dead)
- **Renamed** a key
- **Changed the default value** passed to `GetUserSetting(name, default)`

When updating the registry:
- Decide whether the key belongs to an existing group, needs a new group, or should be added to the deny-list comment (system/bookkeeping keys, or keys already managed by a dedicated dialog — e.g. Theme submenu, Grid Layouts dialog, Manage Filters dialog, the Analysis chart filter Reset button, etc.).
- Keep the natural-language label and Description accurate.
- Keep the default value in the registry synced with what call sites pass to `GetUserSetting(name, default)`.

Rule of thumb for exclusion: if a user can already modify, add, or delete the setting via a UI control or a dedicated manager dialog, it does NOT belong in the Reset dialog — list it in the deny-list comment at the bottom of `UserSettingsRegistry.cs` with a reason.

Skip this step if the session only read user settings without adding/removing/renaming/redefaulting keys.

## Step 3.8: Update README.md

- Read the current `README.md`
- Determine if the session's work changed anything the README claims:
  - New module / view / dialog of significance, or removal of one
  - Theme added or removed (theme list and count)
  - New top-level service folder under `Services/` (Plugins, Procore, etc.)
  - Module status change (e.g., In Development → Production, or vice versa)
  - Technology stack change (new SDK, AWS service, integration)
  - Major feature added/removed within an existing module that contradicts the bullets listed
- If YES:
  - Update only the affected section(s) — preserve voice, structure, and ordering
  - Keep "Current Features" vs "In Development / Planned" buckets honest with the codebase
  - Update the Module Status table at the bottom when a module's status changes
  - Update the Project Structure tree only when a top-level folder is added or removed
- If NO (routine bug fixes, small UX tweaks, internal refactors, doc-only sessions): skip this step
- Do NOT rewrite untouched sections — bullet edits, not prose rewrites

## Step 4: Commit and Push

- Run `git add -A` to stage everything
- Check for a `nul` file in staged changes — if present, delete it with `rm -f nul` and re-stage
- Auto-generate a clear, concise commit message summarizing the session's work
  - Do NOT include "Generated with Claude" or "Co-Authored-By: Claude"
  - Do NOT include AI attribution of any kind
- Run `git commit` with the generated message
- Run `git push` to the current checked-out branch (no branch restrictions)

## Important Rules

- All file paths in this skill are relative to the repository root — NEVER use absolute paths
- Do NOT selectively commit files — always use `git add -A` unless the user explicitly excluded specific files during the session
- The commit message should describe the functional changes, not the documentation updates (e.g., "Add email notification for record assignments" not "Update docs and commit")
- If there are no uncommitted changes to code (only doc updates from steps 1-3), still commit the documentation updates with an appropriate message
