# Progress_Log Azure Upload - Implementation Plan

**Status:** Planning in progress - awaiting answers to remaining questions

---

## Feature Overview

Admin dialog to upload project snapshots from `VMS_ProgressSnapshots` to company Azure table `dbo_VANTAGE_global_ProgressLog` for historical tracking by WeekEndDate.

---

## Requirements Confirmed

| Requirement | Decision |
|-------------|----------|
| Access Control | **Admin only** (use `AzureDbManager.IsUserAdmin()`) |
| Duplicate Handling | **Error on duplicates** - warn user and prevent upload if records already exist |
| UI Pattern | Follow existing admin dialogs (AdminProjectsDialog, AdminSnapshotsDialog) |

---

## Questions Remaining

### 1. Target Server Location
**Is `dbo_VANTAGE_global_ProgressLog` on the same Azure server as VMS_* tables, or a different company server (like summitpc)?**

- **If same server:** Use existing `AzureDbManager.GetConnection()`
- **If different server:** Need new connection string in `Credentials.cs` and new connection helper method

### 2. Target Table Schema
**What columns does `dbo_VANTAGE_global_ProgressLog` have?**

Options:
- A) Identical to `VMS_ProgressSnapshots` (83 columns) - direct copy
- B) Subset of columns - need mapping
- C) Different column names - need explicit mapping
- D) Table doesn't exist yet - need to create it

### 3. Selection Scope
**How should users select what to upload?**

Options:
- A) By Project + WeekEndDate range (filter then multi-select weeks)
- B) By individual snapshot groups (like AdminSnapshotsDialog)
- C) Upload all un-exported snapshots for a project
- D) Single week at a time only

### 4. Upload Tracking
**Should we track what's been uploaded?**

The `VMS_ProgressSnapshots` table already has `ExportedBy` and `ExportedDate` columns (currently unused). Options:
- A) Use these fields to mark uploaded records
- B) Don't track - allow manual re-runs (with duplicate error protection)

---

## What We Learned From Exploration

### Snapshot System Architecture

**Source Table:** `VMS_ProgressSnapshots` (Azure)
- **Primary Key:** Composite (UniqueID + WeekEndDate)
- **Columns:** 83 total (full Activity snapshot + metadata)
- **Key Fields:** UniqueID, WeekEndDate, ProjectID, AssignedTo, ExportedBy, ExportedDate
- **Auto-Purge:** Snapshots >28 days deleted during submission (important: upload before purge!)

**Snapshot Creation Flow:**
1. User clicks "Submit" in Progress view
2. Current Activities copied to VMS_ProgressSnapshots
3. WeekEndDate set to submission week

### Existing Admin Dialog Patterns

**File locations:**
- `Dialogs/AdminProjectsDialog.xaml.cs` (~490 lines) - CRUD pattern
- `Dialogs/AdminUsersDialog.xaml.cs` (~368 lines) - similar pattern
- `Dialogs/AdminSnapshotsDialog.xaml.cs` - snapshot management, groups by user/project/week

**Common Pattern:**
```csharp
// 1. Load async on dialog open
private async void Dialog_Loaded(object sender, RoutedEventArgs e)
{
    await LoadDataAsync();
}

// 2. Loading panel for UX feedback
pnlLoading.Visibility = Visibility.Visible;

// 3. Task.Run for database operations
var data = await Task.Run(() => { /* Azure query */ });

// 4. ObservableCollection for ListView binding
lvItems.ItemsSource = new ObservableCollection<T>(data);

// 5. Log admin actions with username
AppLogger.Info($"Action", "Class.Method", App.CurrentUser?.Username);
```

### Bulk Upload Patterns (from SyncManager.cs)

**SqlBulkCopy configuration:**
```csharp
using var bulkCopy = new SqlBulkCopy(connection)
{
    DestinationTableName = "TableName",
    BatchSize = 5000,
    BulkCopyTimeout = 120
};
bulkCopy.ColumnMappings.Add("SourceCol", "DestCol");
bulkCopy.WriteToServer(dataTable);
```

**For duplicate checking before upload:**
```csharp
// Create temp table with IDs to check
// JOIN against target table to find existing
// Return list of duplicates to user
```

---

## Preliminary Implementation Plan

### Files to Create
1. `Dialogs/ProgressLogUploadDialog.xaml` - UI
2. `Dialogs/ProgressLogUploadDialog.xaml.cs` - Logic

### Files to Modify
1. `MainWindow.xaml.cs` - Add menu item under Admin menu
2. `MainWindow.xaml` - Add menu item XAML
3. Possibly `Credentials.cs` - If different server, add connection string
4. Possibly `AzureDbManager.cs` - If different server, add connection method

### UI Design (Preliminary)

```
┌─────────────────────────────────────────────────────────┐
│ Upload Progress Log to Company Database                  │
├─────────────────────────────────────────────────────────┤
│ Project: [Dropdown - all projects]                       │
│                                                          │
│ Available Snapshots:                                     │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ ☐ Week ending 01/25/2026 (45 records)              │ │
│ │ ☐ Week ending 01/18/2026 (42 records) [Uploaded]   │ │
│ │ ☐ Week ending 01/11/2026 (38 records)              │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                          │
│ Selected: 0 week(s) (0 records)                          │
│                                                          │
│ [Select All] [Select Un-uploaded] [Clear]                │
│                                                          │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Upload status/results appear here                   │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                          │
│              [Upload Selected]  [Close]                  │
└─────────────────────────────────────────────────────────┘
```

### Core Logic Flow

1. **Load:** Query VMS_ProgressSnapshots grouped by WeekEndDate for selected project
2. **Check Duplicates:** Before upload, query target table for existing UniqueID+WeekEndDate
3. **Error if Duplicates:** Show which records already exist, prevent upload
4. **Upload:** SqlBulkCopy to target table with column mappings
5. **Track (if desired):** Update ExportedBy/ExportedDate in VMS_ProgressSnapshots
6. **Log:** AppLogger.Info with admin username

---

## Important Notes

- **28-day auto-purge:** Snapshots older than 28 days are deleted during submission. Uploads should happen before this window closes.
- **ExportedBy/ExportedDate fields:** Already exist in VMS_ProgressSnapshots but are currently unused (set to NULL). Can be repurposed for tracking uploads.
