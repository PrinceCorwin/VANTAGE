# Schedule Module - Architecture

## Business Context

### Problem
Field engineers track progress in MILESTONE but must separately maintain P6 schedule updates, causing:
- Double-entry of start/finish dates
- Discrepancies between P6 and field reality
- Time wasted reconciling differences

### Solution
Import P6 schedules weekly, compare against MILESTONE snapshots, identify discrepancies, allow corrections and explanatory notes, then export back to P6.

### User Workflow
1. **Import:** Weekly P6 TASK sheet with WeekEndDate and ProjectID selection
2. **Compare:** P6 planned/actual dates vs MILESTONE rolled-up dates
3. **Resolve:** Edit activities in detail grid, add MissedReasons for variances
4. **Forecast:** Fill in 3WLA dates for activities starting in next 3 weeks
5. **Export:** Send corrected dates/percents back to P6

## Data Model

### Schedule Table (Local SQLite)
P6 import data - one row per SchedActNO per WeekEndDate
```
SchedActNO (TEXT, PK)
WeekEndDate (TEXT, PK)
WbsId, Description
P6_PlannedStart, P6_PlannedFinish (TEXT)
P6_ActualStart, P6_ActualFinish (TEXT)
P6_PercentComplete, P6_BudgetMHs (REAL)
MissedStartReason, MissedFinishReason (TEXT) - user-editable
```

### ThreeWeekLookahead Table (Local SQLite)
User's 3WLA forecast dates - persists across P6 imports
```
SchedActNO (TEXT, PK)
ProjectID (TEXT, PK)
ThreeWeekStart, ThreeWeekFinish (TEXT)
```

### ScheduleProjectMappings Table (Local SQLite)
Maps which ProjectIDs are covered by each schedule import
```
WeekEndDate (TEXT, PK)
ProjectID (TEXT, PK)
```

### MS Rollup Calculation
```sql
SELECT
    SchedActNO,
    MIN(SchStart) as MS_ActualStart,
    CASE WHEN COUNT(*) = COUNT(SchFinish)
         THEN MAX(SchFinish) ELSE NULL END as MS_ActualFinish,
    SUM(BudgetMHs * PercentEntry) / SUM(BudgetMHs) as MS_PercentComplete
FROM ProgressSnapshots
WHERE WeekEndDate = @weekEndDate
  AND ProjectID IN (SELECT ProjectID FROM ScheduleProjectMappings WHERE WeekEndDate = @weekEndDate)
  AND AssignedTo = @username
GROUP BY SchedActNO
```

**Key:** MS_ActualFinish only returns value when ALL activities have SchFinish.

## Architecture Decisions

1. **MS Rollups: Calculate On-The-Fly** - Always fresh, no staleness
2. **Schedule Table: Local Only** - P6 data is reference, no sync needed
3. **3WLA: Local Only** - Forecasts persist across imports, auto-purge stale dates
4. **Azure Source of Truth** - Query Azure FIRST to determine which SchedActNOs to display
5. **Master Grid: MS Columns Read-Only** - Force users to fix source data
6. **In-Memory Filtering** - Syncfusion doesn't support ICollectionView.Filter
7. **SAVE Button (Master)** - Batch save; real-time had timing issues
8. **Real-Time Save (Detail)** - Immediate feedback for rollup recalculation
9. **User-Scoped Data** - All queries filter by AssignedTo = current user

## Critical Data Handling

**Dates:** ALL stored as TEXT/VARCHAR
- P6 exports text, not datetime
- NEVER use GetDateTime() - use GetString() + DateTime.TryParse()
- Compare with .Date to strip time component

**Percentages:** Stored as 0-100 (not 0-1)
- P6 exports 0-1 decimal
- ScheduleExcelImporter converts during import
- Keep 0-100 throughout system

**Split Ownership:** SchedActNO can exist in multiple ProjectIDs
- Example: "Fab pipe spools" in both 24.004 (Crosby) AND 24.005 (Indiana)
- MS rollups aggregate across ALL mapped ProjectIDs
- This is correct, not a violation
