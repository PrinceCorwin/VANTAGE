# Schedule Module - Implementation Plan (REVISED)

**Last Updated:** December 30, 2025

## Overview
The Schedule Module enables comparison between P6 Primavera schedule data and MILESTONE progress tracking data, with Three-Week Lookahead (3WLA) planning capabilities. This eliminates double-entry work and provides discrepancy reporting between P6 and field progress.

## Business Context

### Problem Being Solved
Currently, field engineers track progress in MILESTONE but must separately maintain P6 schedule updates, leading to:
- Double-entry of start/finish dates
- Discrepancies between P6 and field reality
- Time wasted reconciling differences
- Delayed schedule updates

### Solution
Import P6 schedules weekly, compare against MILESTONE snapshots, identify discrepancies, allow field engineers to correct MILESTONE data or add explanatory notes, then export corrections back to P6 for scheduler import.

### User Workflow
1. **Weekly P6 Import:** Import latest P6 schedule (TASK sheet) for specific ProjectIDs
2. **Comparison View:** See P6 planned/actual dates vs MILESTONE rolled-up dates side-by-side
3. **Discrepancy Resolution:** 
   - Filter to items where P6 != MS
   - Select master row to see individual MILESTONE activities in detail grid
   - Edit individual activity dates/percents to correct MILESTONE data
   - Add MissedStartReason/MissedFinishReason for variances
4. **Three-Week Lookahead:** 
   - Filter to activities starting/finishing in next 3 weeks
   - Fill in ThreeWeekStart/ThreeWeekFinish dates for coordination
   - **3WLA dates are FORECASTS** - once actuals exist, no forecast needed
5. **Export to P6:** Export corrected dates/percents back to P6 format for scheduler

## Data Model

### Schedule Table (Local SQLite)
Stores P6 import data - one row per SchedActNO per WeekEndDate
```
SchedActNO (TEXT, PK)
WeekEndDate (TEXT, PK)
WbsId (TEXT)
Description (TEXT)
P6_PlannedStart (TEXT)
P6_PlannedFinish (TEXT)
P6_ActualStart (TEXT)
P6_ActualFinish (TEXT)
P6_PercentComplete (REAL)
P6_BudgetMHs (REAL)
MissedStartReason (TEXT) - user-editable variance explanation
MissedFinishReason (TEXT) - user-editable variance explanation
UpdatedBy (TEXT)
UpdatedUtcDate (TEXT)
```

**Note:** No ProjectID column in Schedule table. P6 files don't contain ProjectID.
**Important:** All date fields stored as TEXT (not DATETIME). P6 exports everything as text.
**CHANGE (12/28/2025):** ThreeWeekStart and ThreeWeekFinish columns REMOVED - now stored in ThreeWeekLookahead table.
**CHANGE (12/30/2025):** InMS column REMOVED - no longer used. Azure is source of truth for which activities have MS data.

### ThreeWeekLookahead Table (Local SQLite)
Stores user's 3WLA forecast dates - persists across P6 imports
```
SchedActNO (TEXT, PK)
ProjectID (TEXT, PK)
ThreeWeekStart (TEXT)
ThreeWeekFinish (TEXT)
```

**Purpose:** 3WLA dates are FORECASTS for when work will start/finish. They persist across P6 re-imports so users don't have to re-enter them weekly.

**Purge Logic (on P6 Import):**
1. Delete orphaned rows (SchedActNO not in current import for imported ProjectIDs)
2. Clear individual dates that are in the past of selected WeekEndDate
3. Delete rows where both dates become null

**ProjectID Handling:**
- Same SchedActNO across projects imported together = split ownership (shared 3WLA)
- Separate imports = separate 3WLA rows, no collision
- First ProjectID from ScheduleProjectMappings used as anchor for saves

### ScheduleProjectMappings Table (Local SQLite)
Maps which ProjectIDs are covered by each schedule import
```
WeekEndDate (TEXT, PK)
ProjectID (TEXT, PK)
```

**Usage:** When calculating MS rollups, query ProgressSnapshots WHERE ProjectID IN (SELECT ProjectID FROM ScheduleProjectMappings WHERE WeekEndDate = @date)

### ProgressSnapshots Table (Azure SQL - Existing)
Already exists - created when user clicks Submit Progress
```
SnapshotID (PK)
UniqueID
SchedActNO - links to Schedule table
WeekEndDate (VARCHAR) - stored as text "2025-12-14"
ProjectID
AssignedTo (VARCHAR) - owner of the snapshot
SchStart (VARCHAR) - stored as text with time "2025-12-14 00:00:00"
SchFinish (VARCHAR) - stored as text with time "2025-12-14 00:00:00"
PercentEntry (REAL) - stored as 0-100 (not 0-1 decimal)
BudgetMHs (REAL)
... all Activity fields frozen at time of snapshot
```

**MS Rollup Calculation:**
```sql
SELECT 
    SchedActNO,
    MIN(SchStart) as MS_ActualStart,
    CASE 
        WHEN COUNT(*) = COUNT(SchFinish) 
        THEN MAX(SchFinish)
        ELSE NULL 
    END as MS_ActualFinish,
    SUM(BudgetMHs * PercentEntry) / SUM(BudgetMHs) as MS_PercentComplete,
    SUM(BudgetMHs) as MS_BudgetMHs
FROM ProgressSnapshots
WHERE WeekEndDate = @weekEndDate
  AND ProjectID IN (SELECT ProjectID FROM ScheduleProjectMappings WHERE WeekEndDate = @weekEndDate)
  AND AssignedTo = @username
GROUP BY SchedActNO
```

**Critical Notes:**
- Dates stored as VARCHAR (not DATETIME) - use GetString() then DateTime.TryParse()
- PercentEntry stored as 0-100 (not 0-1 decimal)
- Weighted average calculation keeps 0-100 scale throughout
- **MS_ActualFinish only returns value when ALL activities have SchFinish** (prevents partial completion showing as complete)
- **CHANGE (12/29/2025):** AssignedTo filter added - users only see their own snapshots

### ScheduleMasterRow (ViewModel)
Combines P6 data + MS rollups for grid display
```csharp
// P6 Data (from Schedule table)
SchedActNO, WbsId, Description
P6_PlannedStart, P6_PlannedFinish
P6_ActualStart, P6_ActualFinish
P6_PercentComplete, P6_BudgetMHs

// MS Rollups (calculated from ProgressSnapshots)
// These have backing fields + PropertyChanged notifications
MS_ActualStart, MS_ActualFinish
MS_PercentComplete, MS_BudgetMHs

// Computed Properties for Filtering
HasStartVariance (compares P6_ActualStart vs MS_ActualStart using .Date)
HasFinishVariance (compares P6_ActualFinish vs MS_ActualFinish using .Date)

// Required Field Indicators (for conditional formatting)
IsMissedStartReasonRequired (HasStartVariance AND MissedStartReason empty)
IsMissedFinishReasonRequired (HasFinishVariance AND MissedFinishReason empty)
IsThreeWeekStartRequired (P6_PlannedStart within 21 days AND ThreeWeekStart null AND MS_ActualStart null)
IsThreeWeekFinishRequired (P6_PlannedFinish within 21 days AND ThreeWeekFinish null AND MS_ActualFinish null)
IsThreeWeekStartEditable (MS_ActualStart is null - can't forecast what already happened)
IsThreeWeekFinishEditable (MS_ActualFinish is null - can't forecast what already happened)

// Editable Fields (with INotifyPropertyChanged)
MissedStartReason, MissedFinishReason
ThreeWeekStart, ThreeWeekFinish (from ThreeWeekLookahead table)

// Child Collection
ObservableCollection<ProgressSnapshot> DetailActivities
```

**Important 3WLA Logic:**
- 3WLA dates are FORECASTS for when work will start/finish
- Once actuals exist (MS_ActualStart/Finish populated), no forecast needed
- IsThreeWeekRequired returns false if actual already exists
- IsThreeWeekEditable returns false if actual exists (can't forecast the past)

## Implementation Phases

### Phase 1: Database Foundation [COMPLETE]
**Status:** Complete
- Created Schedule table (local SQLite only)
- Created ScheduleProjectMappings table (local SQLite)
- **ADDED (12/28/2025):** Created ThreeWeekLookahead table (local SQLite)
- **REMOVED (12/30/2025):** InMS column no longer used - Azure is source of truth
- Models: Schedule.cs, ScheduleProjectMapping.cs

### Phase 2: Progress Module Enhancements [COMPLETE]
**Status:** Complete
- Created ProgressSnapshots table (Azure)
- Implemented auto-date logic (SchStart/SchFinish based on PercentEntry)
- Implemented ownership validation
- Created SnapshotHelper for Submit Progress workflow
- Submit Progress creates snapshots for SchedActNO coverage
- **FIXED:** Duplicate key handling - checks for conflicts before insert, skips already-submitted records
- **FIXED:** User-friendly error messages for PRIMARY KEY violations

### Phase 3: Basic UI [COMPLETE]
**Status:** Complete
- Created ScheduleView.xaml with split-panel layout (master + detail grids)
- Created ScheduleViewModel with filter properties
- Toolbar with filter buttons, import/export, refresh, save
- GridSplitter for user-adjustable heights
- Column state persistence for both grids

### Phase 4: P6 Import [COMPLETE]
**Status:** Complete
- P6ImportDialog with WeekEndDate picker and ProjectID selection
- ScheduleExcelImporter reads P6 TASK sheet
- Maps P6 columns to Schedule table fields
- Converts percentages from 0-1 to 0-100 scale
- Clears Schedule and ScheduleProjectMappings tables before insert (no versioning)
- ThreeWeekLookahead purge logic implemented
- **REMOVED (12/30/2025):** InMS flag logic removed - no longer needed

### Phase 5: MS Rollups & Display [COMPLETE]
**Status:** Complete
- **REFACTORED (12/30/2025):** Azure query runs FIRST to determine which activities to show
- GetMSRollupsFromAzure() returns dictionary keyed by SchedActNO
- Schedule rows loaded only for SchedActNOs that have MS data
- Master grid displays P6 data + MS rollups side-by-side
- Detail grid shows individual ProgressSnapshots for selected SchedActNO
- Real-time save on detail grid edit (Azure only - not local Activities)
- MS rollup recalculates after detail edit

### Phase 6: Filters & Conditional Formatting [COMPLETE]
**Status:** Complete
- Missed Start filter (HasStartVariance = true)
- Missed Finish filter (HasFinishVariance = true)
- 3WLA filter (IsThreeWeekStartRequired OR IsThreeWeekFinishRequired)
- Required Fields filter (IsMissedStartReasonRequired OR IsMissedFinishReasonRequired)
- Actuals Discrepancies filter (MS actuals != P6 actuals)
- XAML conditional formatting:
  - Red background for required reasons/3WLA dates
  - Yellow background for date variances and MH mismatches
- In-memory filtering (ObservableCollection rebuild, instant response)

### Phase 7: Export & Reporting [COMPLETE]
**Status:** Complete
- P6ExportDialog with time pickers for start/finish times
- ScheduleExcelExporter creates P6-compatible XLSX
- 3WLA Report with 20 columns, color-coded headers, mismatch flags
- Mismatch columns: NotInP6, NotInMS, Actual_Mismatch, MH_Mismatch
- Red fill for True values in mismatch columns

### Phase 8: Polish & Bug Fixes [COMPLETE]
**Status:** Complete (12/30/2025)

1. **UpdatedUtcDate Format Standardization**
   - Fixed SyncManager.GetActivityValue to format DateTime as "yyyy-MM-dd HH:mm:ss"
   - Azure cleanup SQL executed to standardize existing data

2. **User Filtering in Schedule Module**
   - Added AssignedTo filter to ScheduleRepository.GetMSRollupsFromAzure
   - Added AssignedTo filter to ScheduleRepository.GetSnapshotsBySchedActNOAsync
   - Users only see their own snapshots in Schedule module

3. **Progress Module Edit Validation**
   - PercentEntry auto-adjusts SchStart/SchFinish dates
   - SchStart validation (can't be future, can't be after finish, etc.)
   - SchFinish validation (can't set if < 100%, can't be before start, etc.)

4. **ProgressSnapshots Auto-Purge**
   - Deletes ALL snapshots with WeekEndDate > 4 weeks old
   - Runs automatically during Submit Progress
   - Prevents unbounded table growth

5. **WeekEndDate UI Simplification**
   - Changed from dropdown to label (only one WeekEndDate after import)
   - Shows "Week Ending: MM-dd-yyyy" or "(No schedule loaded)"

6. **Clear 3WLA When Actuals Are Set**
   - When detail grid edit creates MS_ActualStart -> clear ThreeWeekStart
   - When detail grid edit creates MS_ActualFinish -> clear ThreeWeekFinish

7. **Detail Grid Column Persistence**
   - Added SaveDetailColumnState() and LoadDetailColumnState() methods
   - Saves column width, order, visibility to UserSettings
   - Added AllowDraggingColumns="True" to detail grid
   - Changed AllowFiltering="False" to AllowFiltering="True"

8. **InMS Column Refactoring [ADDED 12/30/2025]**
   - Removed InMS flag entirely - was becoming stale when snapshots deleted
   - Azure is now source of truth for which activities have MS data
   - GetScheduleMasterRowsAsync queries Azure FIRST, then loads matching Schedule rows
   - GetP6NotInMSAsync refactored to query Azure at runtime
   - GetSchedActNOsFromSnapshots removed from ScheduleExcelImporter
   - Eliminates ghost rows problem permanently

## Completed Items

All schedule module phases are COMPLETE. The module is READY FOR TESTING.

**Features Delivered:**
- P6 Import with WeekEndDate and ProjectID selection
- Master/detail grid view with MS rollups
- Editable MissedReasons and 3WLA dates
- Multiple filter options (Missed Start/Finish, 3WLA, Required Fields, Actuals)
- Conditional formatting for variances and required fields
- P6 Export with time selection
- 3WLA Report generation
- Column state persistence for both grids
- User-scoped data (see only your own snapshots)

## Technical Notes

### Architecture Decisions

1. **MS Rollups: Calculate On-The-Fly (Not Stored)**
   - Always reflects current snapshot values
   - No data staleness issues
   - Batched Azure query for performance

2. **Schedule Table: Local SQLite Only**
   - P6 data is reference data, not user-generated
   - No sync needed to Azure
   - Fast local queries for display

3. **ThreeWeekLookahead: Local SQLite Only**
   - 3WLA dates are user forecasts, not actuals
   - Persists across P6 imports
   - Auto-purges stale dates on import
   - No Azure sync needed (exported to P6 for scheduler)

4. **3WLA Dates Are FORECASTS**
   - 3WLA dates are NOT mirrored from actuals
   - Once MS_ActualStart exists, ThreeWeekStart not required (work already started)
   - Once MS_ActualFinish exists, ThreeWeekFinish not required (work already finished)
   - User enters forecasts for coordination with scheduler
   - Actuals come from MIN(SchStart)/MAX(SchFinish) of children

5. **ScheduleProjectMappings: Normalized Table (Not JSON)**
   - Clear schema, easy to query
   - Handles multi-project schedules cleanly

6. **Import Always Clears Schedule Table**
   - No need to keep multiple imports
   - Simplifies logic (DELETE then INSERT)
   - ThreeWeekLookahead persists separately (purge logic handles cleanup)

7. **Master Grid: Read-Only for MS Columns**
   - Forces users to fix source data (ProgressSnapshots/Activities)
   - Prevents band-aid fixes that recur next week

8. **In-Memory Filtering (Not ICollectionView)**
   - Syncfusion SfDataGrid doesn't respect ICollectionView.Filter
   - Manual ObservableCollection rebuild provides instant filtering
   - Load once per week change, filter clicks are instant
   - Better UX than database queries on every filter click

9. **SAVE Button vs Real-Time Saving (Master Grid)**
   - Real-time cell edit saving had timing issues with Syncfusion grid
   - Batch save approach more reliable and predictable
   - User explicitly controls when changes are persisted
   - SAVE button highlights red when dirty to prevent data loss

10. **Real-Time Saving (Detail Grid)**
    - Detail edits affect Azure (ProgressSnapshots) + local (Activities) + MS rollups
    - Immediate feedback required for rollup recalculation
    - Different pattern than master grid is intentional
    - UpdateSnapshotAsync only updates Azure snapshots, NOT local Activities

11. **Split-Panel Layout (Not DetailsViewDefinition)**
    - Simpler implementation than Syncfusion nested grids
    - Better for single-focus workflow
    - GridSplitter allows user-controlled sizing
    - Persisted heights improve UX

12. **Azure as Source of Truth for Activity Selection [CHANGED 12/30/2025]**
    - Query Azure for rollups FIRST to determine which SchedActNOs to display
    - Only load Schedule rows that have matching Azure snapshot data
    - Eliminates stale InMS flag problem entirely
    - GetP6NotInMSAsync calculates at runtime (not from stored flag)

13. **Dialog Theming with SfSkinManager**
    - Standard WPF dialogs don't inherit Syncfusion FluentDark automatically
    - Must add SfSkinManager.Theme to Window declaration
    - Required for P6ImportDialog, P6ExportDialog, and similar custom dialogs

14. **User-Scoped Data**
    - All ProgressSnapshot queries filter by AssignedTo = current user
    - Users only see their own data in Schedule module
    - Prevents confusion from seeing other users' progress

15. **Detail Grid Column Persistence**
    - Separate settings key from master grid (DetailGridPrefsKey)
    - Same pattern as master grid (schema hash, width, order, visibility)
    - AllowDraggingColumns and AllowFiltering enabled

### Critical Data Type Handling

**All Dates are TEXT/VARCHAR:**
- P6 exports everything as text (dates, numbers, percentages)
- Schedule table stores dates as TEXT: "2025-12-14"
- ProgressSnapshots stores dates as VARCHAR: "2025-12-14 00:00:00"
- **NEVER use reader.GetDateTime()** - always use GetString() then DateTime.TryParse()
- Use .Date property when comparing dates to strip time component

**Percentage Handling:**
- P6 exports percentages as 0-1 decimal (50% = 0.5)
- ScheduleExcelImporter converts to 0-100 scale during import
- PercentEntry stored as 0-100 everywhere (Schedule, ProgressSnapshots, Activity)
- MS rollup calculation keeps 0-100 scale: SUM(BudgetMHs * PercentEntry) / SUM(BudgetMHs)
- Export keeps 0-100 scale (P6 expects whole numbers)

**UpdatedUtcDate Format:**
- Standardized to "yyyy-MM-dd HH:mm:ss" throughout system
- SyncManager.GetActivityValue explicitly formats DateTime values
- Azure cleanup SQL executed to standardize existing data

### Split Ownership Handling
SchedActNO can exist in multiple ProjectIDs (e.g., fab yard + job site). This is CORRECT, not a violation.
- Example: "Fab pipe spools" in ProjectID 24.004 (Crosby) AND 24.005 (Indiana)
- ScheduleProjectMappings tracks which ProjectIDs the schedule covers
- MS rollups aggregate across ALL mapped ProjectIDs
- ThreeWeekLookahead uses first ProjectID as anchor, but queries across all imported ProjectIDs

Split ownership violation = same SchedActNO + same ProjectID split between multiple users.

### Performance Considerations
- Batched Azure query: ONE query calculates ALL MS rollups (GROUP BY SchedActNO)
- Load time: <3 seconds for 200 activities with Azure query
- In-memory filtering: Instant response (no database queries during filter clicks)
- Detail grid lazy-loads only when row selected
- Column state persistence prevents re-setup each time
- Splitter state persistence improves UX

## Future Enhancements (Shelved)

1. **Find-Replace in Schedule Detail Grid**
2. **Copy/Paste in Schedule Detail Grid**

## Future AI Integration

**AI Error Message Interpreter:**
- Analyze exceptions and return user-friendly messages
- Context-aware suggestions for resolution
- Falls back to raw message if AI unavailable or times out
- Implementation: AIErrorHelper class with GetFriendlyMessageAsync method

**AI Schedule Analysis (Future):**
- Flag potential problems: manpower, low production, material expediting, scheduling conflicts
- Pattern recognition across historical data
- Proactive alerts for schedule risks

## Status

**Schedule Module:** COMPLETE - READY FOR TESTING
**Progress Module:** COMPLETE - READY FOR TESTING

All planned features implemented. Testing can begin.