---
name: publisher
description: >
  End-to-end release workflow that bumps the version, builds the update package,
  publishes to GitHub Releases, and updates the auto-updater manifest. Invoked
  explicitly by the user via /publisher.
---

# Publisher

Automates cutting a new release of VANTAGE: Milestone. Runs all steps in sequence without pausing for user review, except where explicit confirmation is required.

## Workflow Context

The typical release flow is: work → `/finisher` (commit session work) → test the committed build → `/publisher` (cut release). Publisher assumes finisher has already run — it does not commit outstanding session work.

## Prerequisites

Before running publisher, confirm:
1. Working tree is clean (`git status` shows nothing uncommitted) — if not, stop and ask the user to run `/finisher` first or handle manually
2. Current branch is the release branch (typically `main`/`master`) — if not, confirm with user
3. The committed build has been tested

If any of these are not confirmed, ask the user before proceeding.

## Publishability Gate (read first, before anything else)

Read the top of `Plans/Project_Status.md`. If you find an active **CRITICAL** banner (currently a `**CRITICAL — main is NOT publishable...**` block between the "Last Updated" line and the first `## ` section), STOP. Surface the banner to the user verbatim. Do not proceed with version bump, do not run the publish script, do not edit `ReleaseNotes.json` or `manifest.json`. Confirm with the user that the underlying issue has been resolved (and the banner removed from `Project_Status.md`) before continuing — even if the user explicitly invoked `/publisher`. The banner exists precisely because main has known-broken behavior the next release must not ship.

## Step 1: Determine Next Version

Read the current version from `VANTAGE.csproj` (the `<Version>` element).

Apply the `YY.Q.N` format:
- **YY** = last 2 digits of current year
- **Q** = current quarter (1 = Jan–Mar, 2 = Apr–Jun, 3 = Jul–Sep, 4 = Oct–Dec)
- **N** = sequential release number within the quarter, resets to 1 each new quarter

Compute the next version:
- If current version's YY.Q matches today's YY.Q → increment N
- If today is in a new quarter or new year → use new YY.Q with N = 1

Show the user the proposed version and **wait for explicit confirmation** before proceeding. Example:

```
Current version: 26.1.40
Proposed next version: 26.2.1 (new quarter)
Proceed? (yes/no)
```

## Step 2: Update ReleaseNotes.json

- Read `ReleaseNotes.json`
- Draft a new entry to go at the TOP of the `releases` array with:
  - `version`: the new version (no "v" prefix)
  - `date`: today's date in the format used by existing entries
  - `highlights`: user-facing bullet points (NOT technical/dev details)
- Source the highlights from the most recent entries in `Plans/Completed_Work.md`, translated to user-facing language
  - Example: "Fixed sync deadlock in ActivityRepository" → "Improved reliability of data sync"
  - Example: "Refactored TokenResolver to use dictionary lookup" → (skip — internal refactor, no user impact)
- **Order: non-Takeoff items first, AI Takeoff items last.** Only a small number of users use the AI Takeoff module; everyone else uses Progress / Schedule / Work Packages / Sync / etc. The first highlights a typical user reads should be the changes they'll actually notice. Group all AI Takeoff highlights together at the bottom of the list. If a release has only Takeoff changes, that's fine — list them as-is. Same ordering applies to the GitHub release notes body in Step 8.
- Print the proposed entry inline so the user can see what's shipping, but do NOT wait for confirmation — write the file immediately and continue. The user already approved the release at Step 1; second approval is friction. If they spot something wrong they can interrupt.
- This file is embedded in the app — it must be updated BEFORE the publish script runs

## Step 3: Bump Version in VANTAGE.csproj

Update all three elements to the new version:
- `<Version>X.Y.Z</Version>`
- `<AssemblyVersion>X.Y.Z</AssemblyVersion>`
- `<FileVersion>X.Y.Z</FileVersion>`

## Step 4: Run the Publish Script

Run:

```
powershell -ExecutionPolicy Bypass -File "Scripts\publish-update.ps1" -Version "X.Y.Z"
```

Capture the script's output. The script produces:
- ZIP path
- ZIP size in bytes
- SHA-256 hash

If the script fails, STOP. Do not proceed. Report the error to the user.

## Step 5: Update updates/manifest.json

Update `updates/manifest.json` with the values from Step 4:
- `currentVersion`: the new version
- `downloadUrl`: `https://github.com/PrinceCorwin/VANTAGE/releases/download/vX.Y.Z/VANTAGE-X.Y.Z.zip`
- `zipSizeBytes`: from script output
- `sha256`: from script output
- `releaseNotes`: brief one-line description (can mirror the first highlight from ReleaseNotes.json)

## Step 6: Verify Version Consistency

Before committing, confirm the new version appears consistently in:
- `VANTAGE.csproj` (Version, AssemblyVersion, FileVersion all match)
- `ReleaseNotes.json` (top entry)
- `updates/manifest.json` (currentVersion)

If any mismatch, STOP and report to the user.

## Step 7: Commit and Push

- Run `git add -A`
- Commit message format: `Release vX.Y.Z`
  - Do NOT include "Generated with Claude" or AI attribution
- Run `git push`

## Step 8: Create GitHub Release

Run:

```
gh release create vX.Y.Z "path/to/VANTAGE-X.Y.Z.zip" --title "VANTAGE: Milestone vX.Y.Z" --notes "description"
```

Release conventions:
- **Tag:** `vX.Y.Z` (with "v" prefix)
- **Title:** `VANTAGE: Milestone vX.Y.Z` — the title IS the version, NOT a description
- **Notes/body:** User-facing change description (can mirror ReleaseNotes.json highlights)

## Step 9: Post-Publish Verification

After the release is created:
1. Confirm the release exists: `gh release view vX.Y.Z`
2. Confirm the asset is attached and downloadable
3. Report back to the user with:
   - Commit hash (short)
   - Version published
   - GitHub release URL
   - Download URL

## Important Rules

- All file paths are relative to the repository root — NEVER use absolute paths
- NEVER proceed past Step 1 without explicit user confirmation of the version number — this is the ONE approval gate. Once the version is confirmed, run all remaining steps end-to-end without further prompts.
- If any step fails, STOP and report — do not attempt to continue or partially recover without user direction
- The publish script modifies the `bin/` output — do not run it unless the version bump has been completed
