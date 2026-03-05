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

------------------------------------------------------------------------

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

------------------------------------------------------------------------

# Database Rules

-   Dates stored as TEXT (never DATETIME)
-   Percentages stored 0--100
-   Azure is source of truth
-   LocalDirty = 1 marks for push
-   Always set UpdatedBy, UpdatedUtcDate, and LocalDirty after edits

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

------------------------------------------------------------------------

# Performance Rules

-   No Debug.WriteLine in loops
-   Use bulk operations for large datasets
-   Use prepared statements for repeated queries
-   Use SqlBulkCopy for sync
-   Avoid per-record DB calls when batch possible

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
