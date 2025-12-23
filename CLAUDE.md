# MILESTONE - Construction Progress Tracking Application

## Project Overview
WPF construction progress tracking application replacing a legacy MS Access VBA system for Summit Constructors. Manages industrial construction activities for pharmaceutical, microchip, and data center projects. Each database record represents specific field actions (welding, bolt-ups, steel erection). Handles datasets from thousands to 200k+ records.

Integrates with P6 Primavera scheduling software for schedule data import/export.

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

## Architecture Notes
- Progress Module: Complete (Excel import/export, filtering, sorting, pagination, sync)
- Schedule Module: Active development (P6 import, comparison grids, edit capabilities)
- Future: AI integration for schedule conflict detection, material expediting, anomaly detection

## Communication Preferences
- Be direct, skip pleasantries
- State what to do and why
- If code is inefficient or wrong, say so clearly
- After completing features, identify 2-3 areas for improvement
- Challenge assumptions if there's a better approach
