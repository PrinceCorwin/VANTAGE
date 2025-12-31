# MILESTONE Project Plan and Status

**IMPORTANT NOTE FOR AI ASSISTANTS:** Do NOT use emojis (checkmarks, arrows, etc.) anywhere in this document. They cause encoding issues. Use text markers like [COMPLETE], [FIXED], [IN PROGRESS], etc. instead.

**Last Updated:** December 30, 2025
**Project:** MILESTONE - Construction Progress Tracking Application  
**Developer:** Steve  
**Framework:** WPF .NET 8, Syncfusion Controls (v31.2.x), SQLite Local Database, Azure SQL Server

---

## PROJECT OVERVIEW

MILESTONE is a production-quality WPF application replacing a legacy MS Access VBA system (OldVantage) for tracking construction activities on industrial projects (pharmaceutical, microchip, data center facilities). Each record represents field actions like welding, bolt-ups, or steel erection for major clients including Eli Lilly, Intel, Samsung, and TSMC.

**Architecture:**
- Local SQLite database for offline work and fast queries
- Azure SQL Server (mile-wip-server-stecor / MILESTONE-WIP-DB) as central authority
- Bidirectional sync with SyncVersion-based change tracking
- Multi-user collaboration with ownership controls
- Current scale: 4,800+ test records, target: 200k+ production records

**Development Philosophy:**
- Build features correctly over speed
- No patches or quick fixes - proper architectural solutions
- One change at a time with testing after each step
- No production deadline pressure - quality over timeline

**Current Status:** Progress Module and Schedule Module are READY FOR TESTING with employees.

---

## COMPLETED MODULES

### Progress Module [COMPLETE - READY FOR TESTING]

**Core Functionality:**
- **SfDataGrid Implementation:** 90+ columns with Syncfusion's built-in virtualization
- **Database Loading:** GetAllActivitiesAsync loads complete filtered dataset, Syncfusion handles UI virtualization for performance
- **Activity Model:** 78 editable fields + 5 calculated fields (EarnMHsCalc, Status, EarnedQtyCalc, PercentCompleteCalc, ROCLookupID) + 7 system-managed fields (AssignedTo, AzureUploadUtcDate, LocalDirty, ProgDate, UpdatedBy, WeekEndDate, SyncVersion)
- **Bidirectional Data Binding:** PropertyChanged notifications, PercentEntry sync with EarnQtyEntry

**Data Management:**
- **Excel Import/Export:** Compatible with OldVantage naming conventions
  - Replace Mode: Deletes all local, resets LastPulledSyncVersion, marks all LocalDirty=1
  - Combine Mode: Skips duplicates based on UniqueID
  - Auto-generated UniqueIDs: Format `i{timestamp}{sequence}{userSuffix}`
  - Default values: Quantity/BudgetMHs/ClientBudget = 0.001 if imported as 0
- **Cell Editing:** All 80+ editable columns supported with auto-save on CurrentCellEndEdit
- **Find & Replace:** Column-level permissions, security controls, LocalDirty flag tracking

**Edit Validation:**
- **PercentEntry Auto-Adjusts Dates:**
  - 0% -> clears SchStart and SchFinish
  - >0 and <100 -> sets SchStart if null, clears SchFinish
  - 100% -> sets both SchStart and SchFinish if null
- **SchStart Validation:**
  - Cannot set if PercentEntry = 0
  - Cannot be in the future (beyond DateTime.Today)
  - Cannot be after SchFinish (auto-adjusts SchFinish to match)
  - Cannot clear if PercentEntry > 0
- **SchFinish Validation:**
  - Cannot set if PercentEntry < 100
  - Cannot be in the future (beyond DateTime.Today)
  - Cannot be before SchStart
  - Cannot clear if PercentEntry = 100

**Assignment & Ownership:**
- **My Records Filter:** Shows only current user's assigned activities
- **Assign Button:** Allows user to assign selected records they own to self or other users
- **Context Menu:** Right-click row opens context menu with full functionality
- **Security:** Users can only edit/delete/reassign their own records (admins override)
- **Ownership Validation:** Verified at Azure before any modifications
- **Email Notifications:** Users receive email when records are assigned to them

**Filtering & Search:**
- **Percent Filter Buttons:** Mutually exclusive (Complete, Not Complete, Not Started)
- **Column Filters:** Stack with My Records and percent filters
- **Server-side Filtering:** All filters applied at SQL level for performance
- **Clear Filters:** Clears both Syncfusion grid-level and ViewModel-level filters

**Metadata Validation:**
- **Required Fields (9):** WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, UDF18
- **ProjectID Validation:** Validates against Projects table using ProjectCache
- **Error Count Button:** Shows count, filters to error rows
- **Sync Blocking:** Prevents sync/reassign if metadata errors exist

**Deletion System:**
- **Soft Delete in Azure:** IsDeleted flag, SyncVersion increments
- **Hard Delete in Local:** Records removed completely
- **Sync Propagation:** Other users pull deletions via SyncVersion tracking

**Context Menu Functions:**
- Delete (soft delete to Azure)
- Export Selected to Excel
- Copy Row(s) - Visible Columns
- Copy Row(s) - All Columns
- Duplicate Row(s)

**Submit Progress:**
- **Date Validation:** Blocks submission if any SchStart or SchFinish dates are after selected WeekEndDate
- **Auto-Purge:** Automatically deletes ProgressSnapshots older than 4 weeks from today
- **Conflict Detection:** Warns if records already submitted by other users

### Schedule Module [COMPLETE - READY FOR TESTING]

**Core Functionality:**
- **P6 Import:** Import weekly P6 schedule TASK sheet with WeekEndDate and ProjectID selection
- **Comparison View:** Side-by-side P6 vs MILESTONE data in master grid
- **MS Rollups:** Calculated from ProgressSnapshots (MIN start, MAX finish when all complete, weighted % avg)
- **Detail Grid:** Edit individual ProgressSnapshot records for selected SchedActNO
- **3WLA Planning:** Three-Week Lookahead forecast dates that persist across imports
- **P6 Export:** Export corrected dates/percents back to P6-compatible format

**User Filtering:**
- All ProgressSnapshot queries filter by AssignedTo = current user
- Users only see their own data in Schedule module
- Fixed in: ScheduleRepository.GetMSRollupsFromAzure, ScheduleRepository.GetSnapshotsBySchedActNOAsync

**Data Loading Architecture [REFACTORED 12/30/2025]:**
- Azure is source of truth for which activities have MS data
- GetMSRollupsFromAzure() queries Azure FIRST
- Only Schedule rows with matching Azure data are loaded
- Eliminates stale InMS flag problem entirely
- InMS column removed from use (Azure query determines activity selection at runtime)

**UI Components:**
- **Master Grid:** P6 data + MS rollups, editable MissedReasons and 3WLA dates
- **Detail Grid:** Individual ProgressSnapshot editing with real-time save
- **Filter Buttons:** Missed Start, Missed Finish, 3WLA, Actuals, Required Fields
- **WeekEndDate Display:** Label showing current loaded week (not dropdown)
- **SAVE Button:** Highlights red when dirty, batch saves master grid edits

**Detail Grid Enhancements:**
- Column persistence (width, order, visibility)
- Column dragging enabled
- Column filters enabled

**Reports:**
- **3WLA Report:** Single consolidated Excel tab with 20 columns
- **Mismatch Flags:** NotInP6, NotInMS, Actual_Mismatch, MH_Mismatch columns
- **Color Coding:** Header colors by section, red fill for True flags

**Technical Notes:**
- ALL dates stored as TEXT/VARCHAR (not DATETIME) - P6 exports everything as text
- PercentEntry stored as 0-100 throughout system (not 0-1 decimal)
- Syncfusion SfDataGrid requires manual ObservableCollection filtering (doesn't support ICollectionView.Filter)
- Use GetString() + DateTime.TryParse() for all date reading (never GetDateTime())
- Dialogs require SfSkinManager for FluentDark theming

**Performance:**
- MS rollup calculation: <3 seconds for 200+ activities
- Filter operations: Instant (in-memory)
- Load time: <3 seconds total (P6 data + MS rollups)

See Schedule_Module_Implementation_Plan_REVISED.md for detailed plan and phase breakdown.

### Sync System [COMPLETE]

**Architecture:**
- **Azure SQL Server:** Central authority (mile-wip-server-stecor / MILESTONE-WIP-DB)
- **Local SQLite:** Offline work capability, fast queries
- **UniqueID-based Matching:** Primary key for sync operations
- **SyncVersion Tracking:** Azure trigger auto-increments on INSERT/UPDATE
- **LocalDirty Flag:** Marks edited records for push (1=dirty, 0=clean)
- **LastPulledSyncVersion:** Stored per-project in AppSettings table

**UpdatedUtcDate Format:**
- Standardized to "yyyy-MM-dd HH:mm:ss" throughout system
- SyncManager.GetActivityValue explicitly formats DateTime values
- Azure cleanup SQL executed to standardize existing data in Activities and ProgressSnapshots

**Sync Flow:**
1. User edits - sets LocalDirty=1
2. User clicks Sync - checks metadata errors - checks Azure connection - opens project selection dialog
3. MirrorTablesFromAzure (sync reference tables: Projects, Users, ColumnMappings, Managers)
4. **PushRecordsAsync:**
   - Query LocalDirty=1 records
   - Disable SyncVersion trigger for bulk performance
   - Reserve SyncVersion range atomically
   - SqlBulkCopy inserts (with pre-assigned SyncVersions)
   - Staging table + UPDATE...FROM for bulk updates
   - Re-enable trigger
   - Update Local with new SyncVersion/ActivityID, clear LocalDirty
5. **PullRecordsAsync:**
   - Query Azure WHERE SyncVersion > LastPulledSyncVersion
   - Read all records to memory
   - Bulk INSERT OR REPLACE to Local in transaction
   - If IsDeleted=1 then DELETE from Local
   - Update LastPulledSyncVersion = MAX(SyncVersion) from Azure

**Performance:**
- Push: ~3 seconds for 4,802 records (SqlBulkCopy with disabled trigger)
- Pull: ~3 seconds for 4,802 records (bulk transaction)
- Total sync: ~6 seconds for 4,802 records
- Reference table mirror: <1 second

**Conflict Resolution:**
- **Ownership Conflicts:** Failed records force-pulled from Azure
- **Deletion Conflicts:** Sync blocked if record deleted in Azure
- **Error Reporting:** Detailed failure messages with UniqueIDs

**Startup Connection Handling:**
- **Connection Check:** Tests Azure connectivity before MirrorTablesFromAzure
- **ConnectionRetryDialog:** Custom dialog with RETRY and WORK OFFLINE buttons
- **Graceful Offline Mode:** Users can work locally when Azure is unavailable
- **Online Operations Protected:** Sync, Assign, and Delete buttons have connection checks with user-friendly error messages

### Admin System [COMPLETE]

**Architecture:**
- **Azure Admins Table:** Single source of truth for admin status
  - AdminID (INT IDENTITY PRIMARY KEY)
  - Username (NVARCHAR UNIQUE)
  - FullName (NVARCHAR)
  - DateAdded (DATETIME DEFAULT GETUTCDATE)
- **No Local Admin Storage:** Cannot be tampered with
- **Real-time Azure Check:** AzureDbManager.IsUserAdmin(username) called at startup
- **Offline = No Admin:** Security by design

**Admin Features:**
- Toggle admin status for users (INSERT/DELETE on Admins table)
- DeletedRecordsView: View, restore, purge deleted records
- **Admin button visibility:** Hidden (Visibility.Collapsed) for non-admins instead of greyed out

**Admin Dialogs [ADDED 12/30/2025]:**
- **AdminSnapshotsDialog:** View/delete snapshots grouped by User+Project+WeekEndDate
- **AdminUsersDialog:** Add/edit/delete users with CRUD form
- **AdminProjectsDialog:** Add/edit/delete projects with full field form

### DeletedRecordsView [COMPLETE]

**Admin Tool Features:**
- **View Deleted Records:** Query Azure WHERE IsDeleted=1
- **Project Filter:** Multi-select checkboxes for projects
- **Selection:** Multi-row selection for batch operations
- **Restore:** Sets IsDeleted=0, increments SyncVersion
- **Hard Delete:** Permanently removes from Azure
- **Export:** Export deleted records to Excel

### Tools Menu [COMPLETE]

**User Tools:**
- **Delete My Snapshots:** User can delete their own ProgressSnapshots by project/week
- **Clear Local Activities:** Delete all local Activities, reset sync state [ADDED 12/30/2025]
- **Clear Local Schedule:** Delete Schedule, ScheduleProjectMappings, ThreeWeekLookahead tables [ADDED 12/30/2025]

### Test Menu [COMPLETE]

**Dev-Only Tools:**
- **Clear Azure Activities:** Delete ALL Activities from Azure (requires typing "DELETE") [ADDED 12/30/2025]
  - Resets local sync state (LastPulledSyncVersion)
  - Sets LocalDirty=1 on all local records for re-sync
  - Refreshes ProgressView grid

---

## CODING CONVENTIONS

**Nullable Reference Types:**
- Use `string?` when null is valid/expected
- Use `string` (non-nullable) when value is required
- Use `= null!` for fields guaranteed to be set after construction
- Use `= string.Empty` for optional fields defaulting to empty
- Never use `string foo = null` for non-nullable types

**Database Operations:**
- ExecuteScalar() can return null - handle with `?? defaultValue`
- Method parameters with `= null` default must be nullable type
- Methods that may not find result must return nullable type

**Exception Handling:**
- Never declare unused `ex` variables in catch blocks
- If catching to rethrow: use bare `catch { throw; }`
- If catching to handle: log with `AppLogger.Error(ex, "ClassName.MethodName")`
- All catch blocks must either log or have clear reason not to

**Logging:**
- Log all errors with `AppLogger.Error(ex, "ClassName.MethodName")`
- Log significant user actions with `AppLogger.Info()`:
  - AssignTo changes, Sync operations, Delete operations, Bulk updates
- Include username parameter for user-initiated actions
- Context parameter format: "ClassName.MethodName"

**INotifyPropertyChanged:**
- Event must be nullable: `public event PropertyChangedEventHandler? PropertyChanged;`
- OnPropertyChanged parameter must be nullable

**XAML Formatting:**
- Never put XAML in artifact files
- Always write XAML in chat response using markdown code blocks

**Code Quality:**
- Include input validation and error handling
- Add appropriate AppLogger calls
- Identify opportunities for optimization or refactoring
- Write clean, self-documenting code with clear variable names

**Approach:**
- Make one change at a time
- Test after each change before proceeding
- Delete or refactor legacy code when no longer relevant
- Challenge assumptions and propose better approaches

---

## COMMON TASKS REFERENCE

**Query Azure Database:**
```csharp
using var conn = AzureDbManager.GetConnection();
conn.Open();
// Use SqlCommand with parameterized queries
```

**Check Azure Connection:**
```csharp
if (!AzureDbManager.CheckConnection(out string errorMessage))
{
    MessageBox.Show(
        $"Cannot connect to Azure database:\n\n{errorMessage}\n\n" +
        "Please ensure you have network access and try again.",
        "Connection Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    return;
}
```

**Check Admin Status:**
```csharp
bool isAdmin = AzureDbManager.IsUserAdmin(username);
```

**Update Activity in Database:**
```csharp
activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
activity.UpdatedUtcDate = DateTime.UtcNow;
activity.LocalDirty = 1;
await ActivityRepository.UpdateActivityInDatabase(activity);
```

**Format DateTime for Database:**
```csharp
// Always use this format for UpdatedUtcDate
DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
```

**Apply Syncfusion FluentDark to Dialog:**
```xml
xmlns:skinManager="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
skinManager:SfSkinManager.Theme="{skinManager:SkinManagerExtension ThemeName=FluentDark}"
```

**Send Email Notification:**
```csharp
await EmailService.SendAssignmentNotificationAsync(
    recipientEmail,
    recipientName,
    assignedByUsername,
    recordCount,
    projectIds);
```

**Mirror Tables from Azure (Refresh Local Reference Data):**
```csharp
DatabaseSetup.MirrorTablesFromAzure();
ProjectCache.Reload(); // If Projects changed
```

---

## PERFORMANCE METRICS

- **Sync (4,802 records):** ~6 seconds total (push + pull)
- **Push:** ~3 seconds (SqlBulkCopy with disabled trigger)
- **Pull:** ~3 seconds (bulk transaction to SQLite)
- **Grid virtualization:** Handles 200k+ records
- **Reference table mirror:** <1 second
- **MS rollup calculation:** <3 seconds for 200+ activities

---

## TESTING NOTES

**Test Datasets:**
- **13-row dataset:** Quick validation testing
- **4,802-row dataset:** Performance and stress testing

**Test Scenarios Validated:**
- Import then Edit then Sync then Pull (all columns preserve values correctly)
- Multi-user ownership conflicts (proper blocking/force-pull)
- Deletion propagation across users
- Metadata validation blocking
- ProjectID validation against Projects table
- Percent filter combinations
- Find & Replace with permissions
- Startup with Azure offline (retry/work offline dialog)
- Work offline mode with sync/assign/delete protection
- Admin toggle via Azure Admins table
- Bulk sync performance with SqlBulkCopy
- P6 Import/Export cycle
- Schedule filters and conditional formatting
- Detail grid edit with MS rollup recalculation
- Progress module edit validation (dates, percents)
- Submit progress date validation
- ProgressSnapshots auto-purge
- Delete user snapshots via Tools menu
- Email notification on assignment
- Clear Local Activities/Schedule (Tools menu)
- Clear Azure Activities (Test menu)
- Admin Edit Snapshots/Users/Projects dialogs

---

## NOTES FOR NEW CHAT SESSIONS

**Always Check:**
1. Review this document for current project state
2. Check Git commit history for recent changes
3. Verify database schema hasn't changed
4. Confirm user's current working branch

**Key Files:**
- `AzureDbManager.cs` - Azure SQL connection and utilities
- `Credentials.cs` - Azure connection string, email credentials (gitignored)
- `SyncManager.cs` - Push/Pull logic with bulk operations
- `DatabaseSetup.cs` - Local SQLite initialization, MirrorTablesFromAzure
- `App.xaml.cs` - Startup, user authorization, admin check, ProjectCache init
- `ScheduleExcelExporter.cs` - P6 export logic
- `ScheduleExcelImporter.cs` - P6 import logic
- `ScheduleRepository.cs` - Schedule data access layer (refactored 12/30/2025)
- `ProgressView.xaml.cs` - Progress module UI and edit validation
- `ProjectCache.cs` - Valid ProjectID cache for validation
- `EmailService.cs` - Azure Communication Services email sending
- `AdminSnapshotsDialog.xaml.cs` - Admin snapshot management
- `AdminUsersDialog.xaml.cs` - Admin user CRUD
- `AdminProjectsDialog.xaml.cs` - Admin project CRUD

**Recent Changes (December 30, 2025):**
- Refactored ScheduleRepository - Azure is now source of truth for activity selection
- Removed InMS column usage (GetMSRollupsFromAzure determines activities at runtime)
- Added Clear Local Activities (Tools menu)
- Added Clear Local Schedule (Tools menu)
- Added Clear Azure Activities (Test menu)
- Added AdminSnapshotsDialog (Admin menu)
- Added AdminUsersDialog (Admin menu)
- Added AdminProjectsDialog (Admin menu)
- Fixed Delete My Snapshots to refresh Schedule grid

**Earlier Changes (December 29, 2025):**
- Added ProjectID validation against Projects table (ProjectCache)
- Added Delete My Snapshots tool (DeleteSnapshotsDialog)
- Added detail grid column persistence
- Added email notifications on assignment (Azure Communication Services)
- Fixed UpdatedUtcDate format inconsistency
- Fixed Schedule module user filtering
- Added ProgressSnapshots auto-purge
- Added Progress module edit validation
- Added Submit progress date validation

**Don't Assume:**
- Database is in clean state (might have test data)
- All features in this doc are still implemented exactly as described
- Azure tables match local schema exactly (Azure has IsDeleted, Admins table)

---

## ACTION ITEMS - REMAINING

**High Priority (Next Session):**
1. **Find-Replace in Schedule Detail Grid** - Same pattern as Progress module
2. **Copy/Paste in Schedule Detail Grid** - Same pattern as Progress module
3. **Create AI Implementation Ideas document** - Extract AI ideas into standalone doc
4. **Close out Schedule Module Implementation Plan** - Rewrite as COMPLETE

**User Features:**
5. **Idea Board / Bug Report** - Feature request/feedback system
6. **Export/Import UserSettings** - PC migration support
7. **Export Logs with email option** - Log attachment support

---

**END OF DOCUMENT**