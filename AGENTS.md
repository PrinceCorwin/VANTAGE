# VANTAGE: Milestone --- Codex Project Instructions

## Project Identity

-   Official name: **VANTAGE: Milestone**
-   Company name: **Summit Industrial** (never use any variation)
-   Legacy system: **OldVantage**
-   Never refer to the app as only "Milestone" in code, UI, or docs.

This is a production WPF application with live users. All changes must
be treated as production-safe.

------------------------------------------------------------------------

# Architecture Overview

WPF .NET 8 application replacing a legacy MS Access system.

Architecture:

-   Local SQLite (offline cache)
-   Azure SQL Server (source of truth)
-   Bidirectional Sync (SyncVersion-based)
-   MVVM pattern
-   Async/await throughout
-   Syncfusion UI (FluentDark / Light / Orchid themes)
-   Auto-update via GitHub Releases (manifest.json + ZIP)

Reference documentation: - Plans/Milestone_Project_plan.md -
Plans/Project_Status.md - Plans/Completed_Work.md

Also read when relevant:

-   Plans/Decisions.md for settled design decisions.
-   Plans/Security_Guidelines.md before touching security-sensitive paths.
-   Plans/MCAA-Takeoff/MCAA_Ratesheet_Plan.md and
    Plans/MCAA-Takeoff/MCAA_Key_Composition.md
    before MCAA rate/takeoff work.
-   Plans/claude-code-aws-deployment-guide.md before any AWS, Lambda,
    S3, ECR, or Step Functions work.
-   Plans/MCAA-Takeoff/vantage_handoff/01_master_takeoff_instructions.md before
    drawing-first weld takeoff work.

------------------------------------------------------------------------

# Sister Projects

## SkySkraper

External MCAA WebLEM scraping/ratesheet producer. It is Synology Drive
synced, not part of this Git repository.

-   Work PC: %USERPROFILE%\source\repos\PrinceCorwin\SkySkraper\SynologyDrive
-   Personal PC: C:\Users\steve\Projects\SkySkraper

Before work in that folder, read its own CLAUDE.md / local guidance.
Never suggest moving it into VANTAGE, copying its files here, adding it
to .gitignore, or git-tracking it. The canonical PRD for its VANTAGE
integration lives here in Plans/MCAA-Takeoff/MCAA_Ratesheet_Plan.md. SkySkraper
status in this repo is tracked in Plans/Project_Status.md and
Plans/Completed_Work.md. SkySkraper files prefixed cdx_ are Codex's
working journal; read them for context and do not modify them unless
the task explicitly targets Codex's SkySkraper work.

## VANTAGE-Plugins

External Git repo for plugin source and published plugin index.

-   Work PC: %USERPROFILE%\source\repos\PrinceCorwin\VANTAGE-Plugins
-   Personal PC: C:\Users\steve\Projects\VANTAGE-Plugins

Pull latest first there as well and read its own guidance before plugin
work. Releases are cut from that repo, not from the VANTAGE tree.

## REQit

External WPF material-expediting rebuild with its own Git repo and
guidance.

-   Work PC: %USERPROFILE%\source\repos\PrinceCorwin\REQit
-   Personal PC: C:\Users\steve\Projects\REQit

REQit shares the same Azure SQL server/database as VANTAGE
(summitpc.database.windows.net / projectcontrols). Schema changes on
projectcontrols can affect both apps. Do REQit work from a REQit-rooted
session after reading its own CLAUDE.md.

------------------------------------------------------------------------

# Workflow Routing

-   Commits/end-of-session docs: use the project finisher skill only
    when the user explicitly says "finisher" or "commit".
-   Releases: use the publisher workflow only when explicitly requested.
-   Never commit without explicit user instruction.
-   Never publish outside the release workflow.
-   Never modify CLAUDE.md.
-   Do not modify AGENTS.md unless Steve explicitly asks to update
    Codex project instructions.

# Development Principles

## 1. One Change at a Time

Make a single coherent change. Ensure it builds cleanly before
proceeding.

## 2. Always Build After Code Changes

Run:

    dotnet build

Fix all errors before reporting completion.

## 3. No Quick Fixes

Prefer architectural solutions over patches. Refactor or delete obsolete
code rather than layering hacks.

## 4. Production Safety

-   Never suggest deleting the local database.
-   All schema changes must use SchemaMigrator.
-   Migrations must be backward-compatible.
-   The app is live with active users.
-   Never modify the Claude.md file in any way and never add it to the .gitignore file.
-   Never surface secrets, connection strings, API keys, or raw
    credential values in UI, logs, docs, or chat output.

------------------------------------------------------------------------

# Git Rules

-   NEVER commit without explicit user instruction.
-   When user says "commit":
    -   Stage ALL changes: git add -A
    -   Do not selectively stage files.
-   Do not add AI attribution in commit messages.
-   Do not auto-commit after making changes.
-   Always push after commit unless instructed otherwise.

Before committing:

1.  Update Plans/Project_Status.md
2.  Update Plans/Completed_Work.md
3.  Update Help/manual.html if user-visible behavior changed.

Do not update status docs until user confirms testing passed.

When user says "commit", stage all changes with git add -A. Do not
selectively stage files unless the user explicitly instructs otherwise.

## Line Endings (Required)

-   Use Windows line endings (CRLF) for repository text files.
-   Preserve existing line ending style when editing files.
-   Before finishing changes, normalize any touched file that has mixed line endings to consistent CRLF.

------------------------------------------------------------------------

# C# Code Conventions

## Comments

-   Use // only.
-   NEVER use XML documentation comments (///
    ```{=html}
    <summary>
    ```
    ).
-   Add brief explanatory comments for non-obvious logic.

## Nullable Reference Types

string? optionalValue = null; string requiredValue; string \_field =
null!;

## Exception Handling

Allowed:

catch { throw; }

catch (Exception ex) { AppLogger.Error(ex, "Class.Method"); }

Never swallow exceptions.

## Logging

AppLogger.Error(ex, "ClassName.MethodName"); AppLogger.Info("Action
description", "ClassName.MethodName", App.CurrentUser!.Username);

Log these user actions: - AssignTo changes - Sync operations - Delete
operations - Bulk updates

## User-Facing Dialogs

Use AppMessageBox.Show(...) from VANTAGE.Utilities instead of
MessageBox.Show(...) directly. The wrapper keeps dialogs in front of the
active themed window after long-running awaits.

------------------------------------------------------------------------

# Database Rules

-   Dates stored as TEXT (never DATETIME)
-   Percentages stored 0--100
-   Azure is source of truth
-   LocalDirty = 1 marks for push
-   Always set UpdatedBy, UpdatedUtcDate, and LocalDirty after edits
-   All user-influenced SQL values must be parameterized.
-   Dynamic column/table names must go through an allowlist.

Never assume Azure schema exactly matches SQLite. Never modify
Credentials.cs unless explicitly instructed.

------------------------------------------------------------------------

# Sync Rules

Flow:

1.  LocalDirty records pushed via SqlBulkCopy
2.  Pull records where SyncVersion \> last pulled
3.  Azure IsDeleted propagates to local delete

Conflict rules:

-   Ownership enforced
-   SyncVersion wins
-   Azure authority

------------------------------------------------------------------------

# UI & Syncfusion Rules

-   Use sfGrid.View.Filter (not ICollectionView)
-   Virtualization is automatic --- do not implement manual
    virtualization
-   Use SfSkinManager for themed dialogs
-   Column persistence stored in UserSettings
-   Use theme resources --- no hard-coded hex colors
-   Use the standard Syncfusion SfBusyIndicator with DualRing animation
    and AccentColor foreground for noticeable operations.

## Progress View Toolbar State Sync

When modifying Views/ProgressView.* paths that change selection,
filtering, grid data, or Activities, verify the bottom toolbar and
summary UI are refreshed. Direct SelectedItems mutation and detached
selection handlers do not refresh these automatically.

Refresh as needed:

-   Filtered/total/selected count: UpdateRecordCount()
-   Metadata-error badge: CalculateMetadataErrorCount()
-   Multi-cell Count/Sum/Avg: UpdateSelectionStats(...)
-   Project summary rollup: UpdateSummaryPanel() or DebouncedUpdateSummary()

Common triggers are Select All, delete/duplicate/add blank row, bulk
operations, filter/sort changes, sync push/pull, ROC split application,
and ActNO ownership reassignment.

## Validation Rules

-   Utilities/ActivityValidator.cs is the source of truth for Activity
    date and percent rules.
-   ActivityRequiredMetadata defines the required metadata fields and
    should drive import, sync-gate, reassign, and user-facing messages.
-   GetAllViolations(activity) is the canonical batch-validation helper.
-   Project-exists validation stays at call sites that own a valid
    Projects cache.
-   New editable text fields should set MaxLength to match the backing
    column width.

------------------------------------------------------------------------

# Performance Rules

-   No Debug.WriteLine in loops
-   Use bulk operations for large datasets
-   Use prepared statements for repeated queries
-   Use SqlBulkCopy for sync
-   Avoid per-record DB calls when batch possible
-   Real project datasets can exceed 100,000 rows. Design grid, sync,
    export, and DB paths for that scale.

------------------------------------------------------------------------

# Testing Protocol

-   User runs app from Visual Studio.
-   Never attempt to launch app from Codex.
-   After code change:
    -   Run dotnet build
    -   Wait for user validation
-   Do not mark feature complete until user confirms testing passed.

Test datasets: - 13-row quick validation - 4,802-row stress test

------------------------------------------------------------------------

# AWS and AI Takeoff

Before any AWS, Lambda, S3, ECR, Step Functions, prompt deployment, or
takeoff-production debugging, read:

    Plans/claude-code-aws-deployment-guide.md

Key rules:

-   Verify every AWS change with a direct CLI check. For Lambda deploys,
    compare old/new SHA256. For S3 uploads, use head-object and compare
    ContentLength/LastModified/ETag.
-   Run AWS commands one at a time in PowerShell and inspect output before
    the next command.
-   Do not assume file locations. Resolve the current machine's
    %USERPROFILE% and NAS prefix first.
-   Do not propose root causes for production takeoff failures without
    evidence from S3 batch contents, failure JSON, extraction JSON, or
    CloudWatch logs.

## Current AI Takeoff Source Locations

The deployed/working AI Takeoff files live under the NAS-synced
Conversion folder:

    %USERPROFILE%\Documents\<prefix>\SynologyDrive\Conversion\

The prefix is WorkFromNAS on the work PC and SummitFiles on the personal
PC. Only the SynologyDrive\Conversion tail is common across machines.
Files named lambda_function.py exist in multiple folders; confirm which
one before editing.

Summit pipeline is frozen unless explicitly targeted:

-   summit-takeoff-poc\extraction_prompt.txt
-   summit-takeoff-poc\lambda_function.py
-   aggregate-deploy\lambda_function.py
-   summit-takeoff-poc\CompRefTable.xlsx
-   summit-takeoff-poc\MatRefTable.xlsx

MCAA pipeline is active development:

-   mcaa-takeoff-poc\extraction_prompt.txt
-   mcaa-takeoff-poc\lambda_function.py
-   mcaa-aggregate-deploy\lambda_function.py
-   Future per-property ref sheets are still being designed.

For MCAA, input-side extraction behavior must stay functionally
identical to Summit so existing drawn-box configs continue to work.
Only output-side schema, extracted properties, and vocabularies diverge.

## MCAA Rate-Key Contract

Plans/MCAA-Takeoff/MCAA_Key_Composition.md is canonical. It supersedes
older ordering language in Plans/MCAA-Takeoff/MCAA_Ratesheet_Plan.md
and scratch notes.

Current key rules:

-   Segment order: NewComp, Reducing, NewMaterial, Merged_Props,
    pressure_rating, class_rating, schedule, weight_class, length,
    connection_type, size_1 through size_7.
-   connection_qty is retained for validation/reference but is not in
    the lookup key.
-   connection_type sits immediately before the sizes.
-   No sorting of sizes or connection tokens. Stored column order is
    canonical.
-   Merged_Props is the only sorted segment.
-   length must carry uppercase FT or IN units.
-   C# key composition must be byte-identical to the SkySkraper producer.

Before MCAA producer or consumer work, read the current MCAA section in
Plans/Project_Status.md, Plans/MCAA-Takeoff/MCAA_Ratesheet_Plan.md, and
Plans/MCAA-Takeoff/MCAA_Key_Composition.md.

------------------------------------------------------------------------

# Drawing-First Weld Takeoff

For the AWS AI Take-Off Agent / future drawing-reading workflow, the
current authoritative handoff is:

    Plans/MCAA-Takeoff/vantage_handoff/01_master_takeoff_instructions.md

Plans/codex_takeoff contains earlier ChatGPT handoff material and is
useful context, but Plans/MCAA-Takeoff/vantage_handoff is newer and
takes precedence where the two disagree.

The reference drawings in the handoff are regression fixtures only.
Production MCAA-Claude and MCAA-Codex takeoffs must preserve the existing
Summit workflow: the user selects an arbitrary group of drawing files in
VANTAGE, uploads that batch, and the selected backend analyzes those files.
Never hard-code the reference drawings or restrict production processing
to a static drawing set.

Core rules:

-   Count physical connection symbols/nodes from the isometric drawing
    first. Do not derive weld quantity by multiplying BOM fitting ends.
-   Do not rely on weld numbers; they are a late QC artifact and may be
    absent.
-   Produce a connection-level audit before rollup totals.
-   Each counted connection should carry type, size, material, evidence,
    confidence, and ideally coordinates/bounding boxes for future UI
    review.
-   Use high-DPI crops for symbol classification. Whole-sheet views can
    collapse socket-weld ticks into plain dots.
-   BW is a plain weld dot. SW/socket weld is a weld dot with short
    socket tick marks. BU is bolt-up and excluded unless requested.
-   Drawing symbols locate/classify joints, but BOM validates fitting
    identity, actual size, end prep, material, and reductions.
-   Count fitting-to-fitting joints once.
-   Count pipe-to-pipe welds only where a weld symbol appears on a
    straight run with no fitting/branch at that point.
-   Do not invent a pipe-to-pipe weld at a branch node; that dot is the
    branch fitting's header weld.
-   For olet/pipet branch fittings, header-side weld size is normally
    the branch size, not the header size.
-   Swage default is BW large end / SW small end when the symbol is
    unclear; a clearly legible drawn symbol wins.
-   Add stock-length welds after drawn-node counting: SS 20 ft, CS
    40 ft. Added SW stock-length joints also need same-size/material
    couplings.
-   Round-one scope is BW/SW count by type, size, and material. Shop vs
    field, bolt-up, grooved, and threaded counts are deferred unless the
    user explicitly asks.

Reference regression drawings and symbol crops live under
Plans/MCAA-Takeoff/vantage_handoff/.

------------------------------------------------------------------------

# Help Sidebar Maintenance

If any user-visible change occurs:

-   Update Help/manual.html
-   Update Table of Contents if needed
-   Remove outdated sections
-   Add tooltips to new controls

------------------------------------------------------------------------

# Communication Expectations

-   Be direct.
-   State what will change and why.
-   Present one logical change at a time.
-   Wait for confirmation before large refactors.
-   Challenge architectural decisions if necessary.
-   Do not announce internal skill names, tool names, or workflow mechanics
    unless they materially affect Steve. When a higher-level instruction
    requires disclosure, describe the user-facing action briefly and
    naturally without boilerplate.
