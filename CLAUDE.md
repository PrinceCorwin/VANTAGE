# MILESTONE - Construction Progress Tracking

WPF application for Summit Constructors replacing legacy MS Access system. Tracks construction activities (welding, bolt-ups, steel erection) for industrial projects. Integrates with P6 Primavera for schedule import/export.

**See also:** `Project_Status.md` (current status, backlog), `Milestone_Project_plan.md` (architecture), `Schedule_Module_plan.md` (schedule module details)

## Tech Stack
- WPF .NET 8, Syncfusion 31.2.12 (FluentDark theme)
- SQLite (local) + Azure SQL Server (central sync)
- MVVM with async/await

## Development Approach
- ONE change at a time, test before proceeding
- No quick fixes - proper architectural solutions
- Delete/refactor legacy code when no longer relevant

## C# Code Conventions

### Comments
- Use ONLY `//` style comments
- NEVER use `/// <summary>` XML documentation

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

## Database Conventions
- Dates: TEXT/VARCHAR (P6 exports text) - use string parsing, not DATETIME
- Percentages: 0-100 format (not 0-1 decimal)
- Sync: UniqueID matching, SyncVersion monotonic integers
- Azure PK: ActivityID (auto-increment); Local PK: UniqueID

## Performance Rules
- NO Debug.WriteLine in loops
- Bulk operations for large datasets
- SqlBulkCopy for sync

## Key Files
| File | Purpose |
|------|---------|
| `AzureDbManager.cs` | Azure connection, utilities |
| `SyncManager.cs` | Push/Pull sync logic |
| `DatabaseSetup.cs` | SQLite init, MirrorTablesFromAzure |
| `ScheduleRepository.cs` | Schedule data access |
| `ProgressView.xaml.cs` | Progress module UI |
| `Credentials.cs` | Connection strings (gitignored) |

## Communication Preferences
- Be direct, skip pleasantries
- State what to do and why
- Challenge assumptions if there's a better approach
