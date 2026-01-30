# Find-Replace in Schedule Detail Grid - Failure Report

**Date:** January 29, 2026
**Status:** FAILED - Rolled back. Needs fresh approach.

## Goal

Add Find-Replace functionality to the Schedule detail grid (`sfScheduleDetail`) which operates on `ProgressSnapshot` objects stored in Azure SQL Server. Find-Replace already works perfectly in the Progress grid which operates on `Activity` objects stored in local SQLite.

## Key Difference Between Progress and Schedule Grids

| | Progress Grid | Schedule Grid |
|---|---|---|
| **Data model** | `Activity` | `ProgressSnapshot` |
| **Storage** | Local SQLite | Azure SQL Server |
| **Save method** | `ActivityRepository.UpdateActivityInDatabase()` | `ScheduleRepository.UpdateSnapshotAsync()` |
| **After save** | In-memory objects ARE the grid source; no reload needed | Must reload from Azure to see changes |
| **provider.SetValue** | Works for all property types | Does NOT work for `DateTime?` properties (SchStart, SchFinish) |
| **Date column types** | TEXT in SQLite | VARCHAR in Azure SQL |

**The Progress grid works because** `provider.SetValue` successfully updates the in-memory `Activity` object, which IS the grid's data source. The save to SQLite is just persistence. The grid immediately reflects changes.

**The Schedule grid fails because** `provider.SetValue` does NOT update `DateTime?` properties on `ProgressSnapshot`. The in-memory object retains the OLD value. Any save method that reads from the object writes the old value to Azure.

## What Was Attempted

### Attempt 1: Delegate pattern on FindReplaceDialog

Refactored `FindReplaceDialog.xaml.cs` with a second `SetTargetColumn` overload accepting three delegates:
- `ownershipFilter`: Check record ownership
- `afterReplaceAsync`: Per-record save callback
- `afterAllReplacesAsync`: Post-all callback

The dialog's `BtnReplaceAll_Click` would call `provider.SetValue` then invoke the delegate. The delegate would call `ScheduleRepository.UpdateSnapshotAsync(snapshot, username)`.

**Result:** Dialog reported success but nothing changed. `UpdateSnapshotAsync` reads from the snapshot object's properties, which were never actually updated by `provider.SetValue`.

### Attempt 2: Direct property assignment in delegate

Added explicit property assignment before calling `UpdateSnapshotAsync`:
```csharp
snapshot.SchFinish = newValue as DateTime?;
```

**Result:** `newValue as DateTime?` returns `null` because `newValue` is actually a **string** (not a boxed `DateTime`). Syncfusion's `provider.GetValue()` returns a string for TEXT-stored date columns, so `ConvertToPropertyType` returns a string, and `string as DateTime?` is always null in C#.

### Attempt 3: Robust parsing with pattern matching + TryParse

Replaced `as DateTime?` with:
```csharp
if (newValue is DateTime dtFinish) snapshot.SchFinish = dtFinish;
else if (DateTime.TryParse(newValue.ToString(), out DateTime parsed)) snapshot.SchFinish = parsed;
```

**Result:** User reported seeing changes momentarily during the dialog, then old values returned after the dialog closed. The post-dialog reload from Azure appeared to overwrite the changes. Unclear whether Azure was actually being updated.

### Attempt 4: Direct SQL update (bypass in-memory objects entirely)

Created `ScheduleRepository.UpdateSnapshotColumnsAsync()` that writes directly to Azure via parameterized SQL, bypassing the in-memory ProgressSnapshot object entirely:
```sql
UPDATE VMS_ProgressSnapshots
SET SchFinish = @p0, UpdatedBy = @updatedBy, UpdatedUtcDate = @updatedUtcDate
WHERE UniqueID = @uniqueId AND WeekEndDate = @weekEndDate
```

Dates written as `dt.ToString("M/d/yyyy")` to match P6-imported format.

**Result:** Detail grid appeared to update but master grid rollup did not. After manual refresh, old values returned. It's unclear whether the SQL UPDATE was actually persisting to Azure or if the reload was reading stale data.

### Attempt 5: Move all SQL AFTER dialog closes

Restructured so the delegate only COLLECTS matched records (no SQL, no async). All SQL UPDATEs execute sequentially after `dialog.ShowDialog()` returns, followed by reload.

**Result:** Same behavior. Detail grid showed changes but master grid didn't update. After refresh, old values returned.

### Attempt 6: Fix rollup SQL query

The rollup query uses `MIN(SchStart)` and `MAX(SchFinish)` on VARCHAR columns (text comparison). Changed to:
```sql
CONVERT(VARCHAR(10), MIN(TRY_CONVERT(DATE, SchStart, 101)), 101) as MS_ActualStart
```

Style 101 forces mm/dd/yyyy interpretation regardless of SQL Server locale.

**Result:** No improvement. Master grid still didn't update properly.

## Pre-existing Bug Found and Fixed

`ScheduleRepository.UpdateSnapshotAsync` at line 621 had:
```sql
UPDATE ProgressSnapshots  -- WRONG table name
```
Should be:
```sql
UPDATE VMS_ProgressSnapshots  -- Correct table name
```
Every SELECT in the file uses `VMS_ProgressSnapshots` but the UPDATE had the wrong table. **This bug affects single-cell edits in the Schedule detail grid too** (they silently fail to persist). This fix is still in the codebase.

## Date Format Issue

`UpdateSnapshotAsync` was writing dates as `"yyyy-MM-dd HH:mm:ss"` format while original P6-imported data uses `"M/d/yyyy"` format. Since the rollup query uses text-based MIN/MAX, mixed formats produce wrong results ("2025-06-27" sorts before "6/26/2025" in string comparison because '2' < '6'). Changed to write `"M/d/yyyy"` format. This fix is still in the codebase.

## Unresolved Questions

1. **Is `provider.GetValue()` returning a string or a boxed DateTime for DateTime? properties on ProgressSnapshot?** — Never definitively confirmed. Diagnostic logging showed the snapshot's property retained the old value after `provider.SetValue`, but the actual return type of `GetValue` was not logged.

2. **Is `UpdateSnapshotColumnsAsync` (direct SQL) actually persisting to Azure?** — The method returns true (ExecuteNonQuery > 0), and the detail grid appeared to show new values after reload. But after manual refresh/restart, old values appeared. This could mean: (a) SQL writes but in wrong format, (b) SQL writes but a different table/condition, (c) the detail grid update was from provider.SetValue display cache not Azure reload.

3. **What does "refresh" do?** — The full schedule refresh (user-initiated) reloads everything. If it calls the SQL rollup query and that query returns wrong results, the rollup is wrong. If it also reloads detail data, and Azure has the new values, detail should be correct. The fact that both revert suggests Azure may not have the new values.

4. **Azure SQL Server locale/dateformat setting** — `TRY_CONVERT(DATE, '6/27/2025')` without a style parameter uses the server's dateformat. If the server uses DMY order, '6/27/2025' fails (no 27th month). We added style 101 but this was never confirmed to fix the issue.

5. **Does the SchFinish/SchStart column in VMS_ProgressSnapshots Azure table actually use VARCHAR?** — Assumed based on code comments, but never verified against the actual Azure schema. If it's a DATETIME column, the string formatting approach is wrong.

## Recommended Next Steps

1. **Query the Azure table schema directly** to confirm column types for SchStart, SchFinish, WeekEndDate:
   ```sql
   SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_NAME = 'VMS_ProgressSnapshots'
   ```

2. **Add a read-back verification** after the SQL UPDATE: immediately SELECT the updated row and log the value. This definitively proves whether the write persisted.

3. **Check what `provider.GetValue` returns** by logging `newValue.GetType().Name` in the dialog. This confirms whether FindReplaceDialog's `ConvertToPropertyType` is creating a DateTime or a string.

4. **Consider a completely different approach**: Instead of using FindReplaceDialog's generic infrastructure, create a Schedule-specific dialog that:
   - Accepts find/replace text
   - Queries Azure directly for matching UniqueIDs
   - Executes batch UPDATE directly
   - Reloads the grids
   - No dependency on Syncfusion's property accessor at all

5. **Test single-cell edit persistence** in the Schedule detail grid. The pre-existing table name bug (`ProgressSnapshots` vs `VMS_ProgressSnapshots`) means single-cell edits may also have been silently failing. With the fix applied, test if single-cell edits now persist after app restart.

## Files Currently Modified (not rolled back)

| File | Change | Status |
|---|---|---|
| `Data/ScheduleRepository.cs` | Table name fix, date format fix, rollup TRY_CONVERT, new UpdateSnapshotColumnsAsync method | Keep table name fix + date format fix. Rollup/new method may need review. |
| `Dialogs/AdminSnapshotsDialog.xaml` | Added "Uploaded" column | Working - tested |
| `Dialogs/AdminSnapshotsDialog.xaml.cs` | Uploaded column logic (3-part key lookup) | Working - tested |
| `Services/ProgressBook/ProgressBookPdfGenerator.cs` | Simplified DrawEntryBox (removed box-in-box) | Working - tested |

## Files Rolled Back

| File | What was attempted |
|---|---|
| `Dialogs/FindReplaceDialog.xaml.cs` | Delegate pattern for generic usage |
| `Views/ScheduleView.xaml.cs` | ValidateAndAdjustSnapshot helper, context menu handler, find-replace click handler |
| `Views/ScheduleView.xaml` | HeaderContextMenu on sfScheduleDetail |
| `Utilities/SyncManager.cs` | Removed step-by-step sync log lines |
