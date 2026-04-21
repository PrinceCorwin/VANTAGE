# VANTAGE: Milestone - Construction Progress Tracking

WPF application for Summit Industrial replacing the legacy MS Access system ("OldVantage"). Tracks construction activities (welding, bolt-ups, steel erection) for industrial projects. Integrates with P6 Primavera for schedule import/export.

## Naming Conventions
- **Official name:** VANTAGE: Milestone (use this in UI, docs, README)
- **Casual references:** Vantage, VMS, newVantage — all refer to this application
- **Legacy system:** OldVantage — the predecessor MS Access/VBA application
- **Company name:** Summit Industrial — this is the ONLY correct name for the company
  - NEVER use "Summit Constructors", "Summit Industrial Constructors", or any other variation
  - Always use "Summit Industrial" wherever the company name appears in code, UI, docs, or installer
- Do NOT refer to the app as just "Milestone" or "MILESTONE" in new code, UI text, or documentation. Always refer to it as "VANTAGE: Milestone" or "Vantage" for short. The full name should be used in formal contexts (UI titles, documentation headers, README), while "Vantage" can be used in casual references (variable names, comments, informal docs). Avoid using "Milestone" alone to prevent confusion with the schedule module or generic milestones.

**See also:** `Project_Status.md` (todos, backlog), `Completed_Work.md` (changelog), `Milestone_Project_plan.md` (architecture), `Decisions.md` (design decisions)

## Tech Stack
- WPF .NET 8, Syncfusion 31.2.12 (FluentDark theme)
- SQLite (local) + Azure SQL Server (central sync)
- MVVM with async/await
- **App is LIVE with active users** - treat all changes as production changes
- Schema migrations must be backward-compatible (use SchemaMigrator for local DB changes)
- Local database contains user data - never suggest deleting it; migrations handle schema updates

## Workflow Skills
- **Commits / end-of-session docs:** Invoke `/finisher`. Handles Project_Status.md, Completed_Work.md (with monthly archiving), manual.html check, Decisions.md, plan file cleanup, commit, and push.
- **Releases:** Invoke `/publisher`. Handles version bump, ReleaseNotes.json, publish script, manifest.json, GitHub Release, and verification.
- NEVER commit or publish outside these skills.

## Development Approach
- Never modify the AGENTS.md file or add it to gitignore file.
- **All generated/written files must use CRLF line endings** — Visual Studio shows an annoying "Inconsistent Line Endings" dialog when files have mixed or LF-only endings. When writing files via PowerShell, use `[System.IO.File]::WriteAllText()` with CRLF-normalized content, not `Out-File`. When using the Write tool, ensure content uses `\r\n`.
- ONE change at a time, test before proceeding
- No quick fixes - proper architectural solutions
- Delete/refactor legacy code when no longer relevant
- After completing a feature: identify improvements, check for dead code, suggest refactoring
- Add brief intuitive tool tips to all created controls
- ALWAYS run `dotnet build` after code changes and fix any errors before reporting completion
- Do NOT add AI attribution comments in code

### Help Sidebar Maintenance
- When features are added, deleted, or modified, update `Help/manual.html` to keep documentation current
- Add new features to the appropriate section and update the Table of Contents if adding new sections
- Remove documentation for deleted features
- Update existing documentation when feature behavior changes
- Alert user to update or add screenshots if UI changes affect or would benefit from visual updates in the help manual
- (Note: `/finisher` handles this at commit time — proactive mid-session updates are welcome but not required)

## C# Code Conventions

### Comments
- Use ONLY `//` style comments
- NEVER use `/// <summary>` XML documentation
- Add brief but intuitive descriptive comments for added methods, complex logic, or non-obvious decisions

### Nullable Reference Types
```csharp
string? optionalParam = null;     // null is valid
string requiredValue;             // non-nullable when required
string _field = null!;            // set after construction
string _desc = string.Empty;      // optional, defaults empty
```

### Exception Handling
```csharp
catch { throw; }                              // rethrow
catch (Exception ex) { AppLogger.Error(ex, "Class.Method"); }  // handle + log
// NEVER: catch (Exception ex) { }  // unused ex
```

### Logging
```csharp
AppLogger.Error(ex, "ClassName.MethodName");
AppLogger.Info($"Action description", "ClassName.MethodName", App.CurrentUser!.Username);
```

Log these user actions with username parameter:
- AssignTo changes
- Sync operations
- Delete operations
- Bulk updates

## Database Conventions
- Dates: TEXT/VARCHAR (P6 exports text) - use string parsing, not DATETIME
- Percentages: 0-100 format (not 0-1 decimal)
- Sync: UniqueID matching, SyncVersion monotonic integers
- Azure PK: ActivityID (auto-increment); Local PK: UniqueID
- ExecuteScalar can return null: `Convert.ToInt64(cmd.ExecuteScalar() ?? 0)`

## Data Patterns

### User Ownership
- Activities have `AssignedTo` field - users only edit their own records
- Admins bypass ownership checks: `AzureDbManager.IsUserAdmin(username)`
- Always verify ownership before edits/deletes

### Sync System
- `LocalDirty = 1` marks records for push to Azure
- Azure is source of truth; local SQLite is cache
- Always set after edits:
```csharp
activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
activity.UpdatedUtcDate = DateTime.UtcNow;
activity.LocalDirty = 1;
await ActivityRepository.UpdateActivityInDatabase(activity);
```

### Azure Connection Check
```csharp
if (!AzureDbManager.CheckConnection(out string errorMessage))
{
    MessageBox.Show($"Cannot connect to Azure database:\n\n{errorMessage}",
        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
    return;
}
```

## Loading Indicators
- For any operation that may take noticeable time, use the Syncfusion `SfBusyIndicator` with `DualRing` animation and `AccentColor` foreground — this is the standard loading spinner pattern throughout the app (see `SyncDialog.xaml` for reference)

## Syncfusion Notes
- SfDataGrid: Use `sfGrid.View.Filter` for custom filtering (not ICollectionView.Filter)
- FluentDark theme requires SfSkinManager on dialogs
- Column persistence: width, order, visibility stored in UserSettings
- Virtualization is automatic - don't implement manual
- CommonMark: blank line required before lists and after headers

## Edit Validation Rules
- See `Utilities/ActivityValidator.cs` — single source of truth for Activity date/percent rules.
- Metadata errors (9 required fields) block sync operations.
- Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, RespParty.

## Performance Rules
- NO Debug.WriteLine in loops
- Bulk operations for large datasets
- SqlBulkCopy for sync
- Prepared statements for repeated queries

## Testing
- Real-world datasets are **100,000+ rows**; write every bulk/grid/DB path with that in mind, not with small-test assumptions
- Test after EVERY change before proceeding
- Multi-user scenarios require Azure connection
- Don't assume database is clean - may have test data
- NEVER run the app from Claude Code - it leaves background processes that must be manually killed
- User will run the app from Visual Studio for all testing - wait for their feedback before proceeding

## Avoid
- XML doc comments (`/// <summary>`)
- Debug.WriteLine in loops
- Quick patches - proper solutions only
- DATETIME types for dates - always TEXT
- Assuming Azure tables match local schema exactly (Azure has IsDeleted, Admins table)
- **Modifying `Credentials.cs`** - this file is gitignored and shared across branches; only modify when explicitly instructed

## Key Files
| File | Purpose |
|------|---------|
| `AzureDbManager.cs` | Azure connection, utilities, admin check |
| `SyncManager.cs` | Push/Pull sync logic |
| `DatabaseSetup.cs` | SQLite init, MirrorTablesFromAzure |
| `ActivityRepository.cs` | Activity CRUD operations |
| `ScheduleRepository.cs` | Schedule data access |
| `NumericHelper.cs` | 3 decimal place rounding for all double values |
| `ActivityValidator.cs` | Single source of truth for Activity date/percent edit rules |
| `UserSettingsRegistry.cs` | Central registry of user-facing UserSettings keys (labels, defaults, groupings) — `/finisher` syncs against this |
| `LongRunningOps.cs` | App-close guard counter used by Submit Week, snapshot delete, ProgressLog upload, etc. |
| `ProgressView.xaml.cs` | Progress module UI, edit validation |
| `ScheduleView.xaml.cs` | Schedule module UI |
| `ScheduleExcelImporter.cs` | P6 import logic |
| `ScheduleExcelExporter.cs` | P6 export logic |
| `ProjectCache.cs` | Valid ProjectID validation cache |
| `EmailService.cs` | Azure Communication Services |
| `FeedbackDialog.xaml.cs` | Feedback Board (Ideas/Bugs) with Azure sync |
| `ManageFiltersDialog.xaml.cs` | User-defined filters dialog |
| `ManageLayoutsDialog.xaml.cs` | Named grid layouts dialog |
| `ManageSnapshotsDialog.xaml.cs` | User-facing Delete/Revert snapshot weeks |
| `AdminSnapshotsDialog.xaml.cs` | Admin snapshot ops — delete all, upload to ProgressLog |
| `ProrateDialog.xaml.cs` | Prorate BudgetMHs across filtered activities |
| `Credentials.cs` | Connection strings (gitignored) |
| `WorkPackageView.xaml.cs` | Work Package module UI |
| `TemplateRepository.cs` | Form/WP template CRUD |
| `WorkPackageGenerator.cs` | PDF generation orchestrator |
| `TokenResolver.cs` | Token replacement for PDF content |

## Communication Preferences
- Be direct, skip pleasantries
- State what to do and why
- Challenge assumptions if there's a better approach
- Present code one block at a time, wait for confirmation
- When in plan mode, do NOT call ExitPlanMode until user explicitly agrees to the plan
