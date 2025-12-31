# MILESTONE - Construction Progress Tracking Application

## Project Overview
WPF construction progress tracking application replacing a legacy MS Access VBA system (OldVantage) for Summit Constructors. Manages industrial construction activities for pharmaceutical, microchip, and data center projects (clients: Eli Lilly, Intel, Samsung, TSMC). Each database record represents specific field actions (welding, bolt-ups, steel erection). Current scale: 4,800+ test records, target: 200k+ production records.

Integrates with P6 Primavera scheduling software for schedule data import/export.

**Current Status:** Progress Module and Schedule Module are READY FOR TESTING.

## Tech Stack
- WPF .NET 8
- Syncfusion 31.2.12 (FluentDark theme)
- SQLite (local storage)
- Azure SQL Server (central sync - mile-wip-server-stecor / MILESTONE-WIP-DB)
- MVVM architecture with async/await patterns

## Development Approach
- Make ONE change at a time, test thoroughly before proceeding
- No quick fixes or patches - do it right the first time
- Delete/refactor legacy code that is no longer relevant
- Prefer complete architectural solutions over workarounds
- No production deadline pressure - quality over timeline

## C# Code Conventions

### Comments
- Use ONLY // style comments
- NEVER use /// <summary> XML documentation tags

### Nullable Reference Types
```csharp
// Use string? when null is valid/expected
string? optionalParam = null;

// Use string (non-nullable) when required
string requiredValue;

// For fields set after construction but before use
string _importedValue = null!;

// For optional fields defaulting to empty
string _description = string.Empty;

// NEVER do this - it's dishonest
// string foo = null;  // WRONG
```

### Database Operations
```csharp
// ExecuteScalar can return null - always handle
var count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);

// Method parameters with null default must be nullable
void Query(string? whereClause = null)

// Methods that may not find result return nullable
User? GetUser(string username)
```

### Exception Handling
```csharp
// If catching to rethrow
catch { throw; }

// If catching to handle - ALWAYS log
catch (Exception ex)
{
    AppLogger.Error(ex, "ClassName.MethodName");
}

// NEVER declare unused ex variables
// catch (Exception ex) { } // WRONG
```

### Logging Requirements
```csharp
// Errors
AppLogger.Error(ex, "ClassName.MethodName");
AppLogger.Error("message", "ClassName.MethodName");

// User actions - include username parameter
AppLogger.Info($"Assigned {count} records to {username}", "ClassName.MethodName", App.CurrentUser!.Username);
AppLogger.Info($"Sync completed: {uploaded} up, {downloaded} down", "SyncManager.Sync", App.CurrentUser!.Username);
AppLogger.Info($"Deleted {count} records", "ClassName.MethodName", App.CurrentUser!.Username);
```

### INotifyPropertyChanged
```csharp
public event PropertyChangedEventHandler? PropertyChanged;
protected void OnPropertyChanged(string? propertyName = null)
```

## Database Conventions
- All dates stored as TEXT/VARCHAR (P6 exports as text) - requires string parsing, not DATETIME casting
- Percentages stored as 0-100 format (not 0-1 decimal)
- Azure uses ActivityID as auto-incrementing PK; local uses UniqueID
- Sync matches on UniqueID
- SyncVersion uses monotonically increasing integers (avoids clock drift)

## Performance Rules
- NO Debug.WriteLine in tight loops - severe performance impact
- Use bulk operations and prepared statements for large datasets
- SqlBulkCopy with optimized triggers for sync operations

## Module Status

### Progress Module [COMPLETE]
- SfDataGrid with 90+ columns and built-in virtualization
- Activity Model: 78 editable + 5 calculated + 7 system-managed fields
- Excel Import/Export compatible with OldVantage naming
- Cell editing with auto-save, Find & Replace with permissions
- Assignment & ownership with email notifications
- Metadata validation (9 required fields, ProjectID validation)
- Soft delete (Azure IsDeleted flag) with hard delete locally

### Schedule Module [COMPLETE]
- P6 Import: Weekly TASK sheet with WeekEndDate/ProjectID selection
- Comparison View: P6 vs MILESTONE rollups side-by-side
- MS Rollups: Calculated from ProgressSnapshots (MIN start, MAX finish when all complete, weighted % avg)
- Detail Grid: Edit individual ProgressSnapshots with real-time save
- 3WLA Planning: Three-Week Lookahead forecast dates that persist across imports
- P6 Export: Export corrected dates/percents back to P6-compatible format
- User-scoped: All queries filter by AssignedTo = current user

### Sync System [COMPLETE]
- Bidirectional sync with SyncVersion-based change tracking
- Push: SqlBulkCopy with disabled trigger (~3s for 4,800 records)
- Pull: Bulk transaction to SQLite (~3s for 4,800 records)
- Conflict resolution with ownership validation

### Admin System [COMPLETE]
- Azure Admins table (single source of truth)
- Admin dialogs: Users, Projects, Snapshots management
- DeletedRecordsView: View, restore, purge deleted records

## Key Files
- `AzureDbManager.cs` - Azure SQL connection and utilities
- `Credentials.cs` - Connection string, email credentials (gitignored)
- `SyncManager.cs` - Push/Pull logic with bulk operations
- `DatabaseSetup.cs` - Local SQLite initialization, MirrorTablesFromAzure
- `App.xaml.cs` - Startup, user authorization, admin check
- `ScheduleRepository.cs` - Schedule data access layer
- `ScheduleExcelImporter.cs` / `ScheduleExcelExporter.cs` - P6 import/export
- `ProgressView.xaml.cs` - Progress module UI and edit validation
- `ProjectCache.cs` - Valid ProjectID cache for validation
- `EmailService.cs` - Azure Communication Services email sending

## Action Items - Remaining
1. Find-Replace in Schedule Detail Grid
2. Copy/Paste in Schedule Detail Grid
3. Idea Board / Bug Report feature
4. Export/Import UserSettings (PC migration support)
5. Export Logs with email option

## Future AI Integration
- Schedule conflict detection
- Material expediting alerts
- Anomaly detection in production data
- AI-powered error message interpretation

## Communication Preferences
- Be direct, skip pleasantries
- State what to do and why
- If code is inefficient or wrong, say so clearly
- After completing features, identify 2-3 areas for improvement
- Challenge assumptions if there's a better approach
