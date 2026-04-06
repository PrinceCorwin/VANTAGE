---
name: finisher
description: >
  End-of-session workflow that updates project documentation and commits changes.
  Trigger ONLY when the user says "finisher". Do NOT trigger on "finish",
  "wrap up", or any other variation. The exact word "finisher" is the only trigger.
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

## Step 2: Archive Check & Update Plans/Completed_Work.md

### Archive check (run FIRST, before adding new entries)
- Read `Plans/Completed_Work.md` and check the date of the most recent entry
- If that entry is from a **previous month** (not the current month):
  1. Move all existing entries (everything between the `## Unreleased` header and the `---` / Archives footer) into a new archive file: `Plans/Archives/Completed_Work_YYYY-MM.md` (using the month of the entries, e.g., `Completed_Work_2026-04.md`)
  2. The archive file should have a header like `# Completed Work — April 2026` and contain all the moved entries
  3. Reset `Plans/Completed_Work.md` to just the standard header, empty `## Unreleased` section, and Archives footer
- If entries are from the current month, skip archiving — just proceed to add new entries

### Add new entries
- Add a new date-stamped section at the TOP of the changelog under `## Unreleased` (most recent first)
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
  - Determine if screenshots are needed to illustrate changes — if so, add placeholders for screenshots with descriptive alt text, add a note in the Project_Status.md to capture them and alert the user of their need
  - If section already has screenshots, alert user that they may need to update them if the UI changed significantly
- If NO user-facing changes were made, skip this step

## Step 3.5: Check Feature-Specific Plan Docs
- If the session's work relates to a specific feature that has its own plan document in `Plans/`, review and update that document as needed
- If not applicable, skip this step

## Step 4: Commit and Push

- Run `git add -A` to stage everything
- Check for a `nul` file in staged changes — if present, delete it with `rm -f nul` and re-stage
- Auto-generate a clear, concise commit message summarizing the session's work
  - Do NOT include "Generated with Claude" or "Co-Authored-By: Claude"
  - Do NOT include AI attribution of any kind
- Run `git commit` with the generated message
- Run `git push` to the current checked-out branch (no branch restrictions)

## Important Rules

- All file paths are relative to the repository root — NEVER use absolute paths
- Do NOT selectively commit files — always use `git add -A` unless the user explicitly excluded specific files during the session
- The commit message should describe the functional changes, not the documentation updates (e.g., "Add email notification for record assignments" not "Update docs and commit")
- If there are no uncommitted changes to code (only doc updates from steps 1-3), still commit the documentation updates with an appropriate message
