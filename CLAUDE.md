# MILESTONE - Construction Progress Tracking

WPF application for Summit Constructors replacing legacy MS Access system. Tracks construction activities (welding, bolt-ups, steel erection) for industrial projects. Integrates with P6 Primavera for schedule import/export.

**See also:** `Project_Status.md` (current status, backlog), `Milestone_Project_plan.md` (architecture), `Schedule_Module_plan.md` (schedule module details), `WorkPackage_Module_Plan.md` (work package module)

## Tech Stack
- WPF .NET 8, Syncfusion 31.2.12 (FluentDark theme)
- SQLite (local) + Azure SQL Server (central sync)
- MVVM with async/await
- Project is a work in progress - architecture and conventions may evolve.
- Local database can be deleted and re-synced from Azure at any time.
- Don't reject refactoring opportunities for fear user downtime. There are no production users yet.

## Development Approach
- ONE change at a time, test before proceeding
- No quick fixes - proper architectural solutions
- Delete/refactor legacy code when no longer relevant
- After completing a feature: identify improvements, check for dead code, suggest refactoring
- Add brief intuitive tool tips to all created controls
- ALWAYS run `dotnet build` after code changes and fix any errors before reporting completion

## Git Commits
- ALWAYS get user permission before committing - user needs to test changes first
- Do NOT add "Generated with Claude" or "Co-Authored-By: Claude" to commit messages
- Do NOT add AI attribution comments in code
- Write clear, concise commit messages describing the change

### MANDATORY Pre-Commit Checklist
**STOP. Before running `git commit`, you MUST complete these steps IN ORDER:**
1. Update Project_Status.md - Add completed work to "Recent Completions", mark backlog items as [x] complete
2. Update any other relevant plan docs if the work relates to a specific feature plan
3. ONLY THEN proceed with git add and git commit

**This is NOT optional. Failure to update status docs before committing is a workflow violation.**

## C# Code Conventions

### Comments
- Use ONLY `//` style comments
- NEVER use `/// <summary>` XML documentation
- Add brief but intuitive desriptive comments for added methods, complex logic, or non-obvious decisions

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

## Syncfusion Notes
- SfDataGrid: Use `sfGrid.View.Filter` for custom filtering (not ICollectionView.Filter)
- FluentDark theme requires SfSkinManager on dialogs
- Column persistence: width, order, visibility stored in UserSettings
- Virtualization is automatic - don't implement manual
- CommonMark: blank line required before lists and after headers

## Edit Validation Rules
- PercentEntry changes auto-adjust SchStart/SchFinish dates
- SchStart: cannot be future, cannot be after SchFinish, cannot clear if PercentEntry > 0
- SchFinish: requires PercentEntry = 100, cannot be before SchStart
- Metadata errors (9 required fields) block sync operations
- Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, UDF18

## Performance Rules
- NO Debug.WriteLine in loops
- Bulk operations for large datasets
- SqlBulkCopy for sync
- Prepared statements for repeated queries

## Testing
- Test datasets: 13-row (quick validation), 4,802-row (stress/performance)
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

## Key Files
| File | Purpose |
|------|---------|
| `AzureDbManager.cs` | Azure connection, utilities, admin check |
| `SyncManager.cs` | Push/Pull sync logic |
| `DatabaseSetup.cs` | SQLite init, MirrorTablesFromAzure |
| `ActivityRepository.cs` | Activity CRUD operations |
| `ScheduleRepository.cs` | Schedule data access |
| `NumericHelper.cs` | 3 decimal place rounding for all double values |
| `ProgressView.xaml.cs` | Progress module UI, edit validation |
| `ScheduleView.xaml.cs` | Schedule module UI |
| `ScheduleExcelImporter.cs` | P6 import logic |
| `ScheduleExcelExporter.cs` | P6 export logic |
| `ProjectCache.cs` | Valid ProjectID validation cache |
| `EmailService.cs` | Azure Communication Services |
| `FeedbackDialog.cs` | Feedback Board (Ideas/Bugs) with Azure sync |
| `ManageFiltersDialog.cs` | User-defined filters dialog |
| `ManageLayoutsDialog.cs` | Named grid layouts dialog |
| `ManageSnapshotsDialog.cs` | Delete/Revert snapshot weeks |
| `ProrateDialog.cs` | Prorate BudgetMHs across filtered activities |
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