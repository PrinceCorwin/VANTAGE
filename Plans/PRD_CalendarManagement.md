# Product Requirements Document: Calendar Management

## Document Info
- **Version:** 2.0
- **Last Updated:** February 2026
- **Status:** Ready for Implementation
- **Changes from V1:** Restructured XER parser into general-purpose XerFileReader + XerCalendarExtractor. Added future roadmap for XER-based schedule import. Added per-activity calendar architecture for remaining duration accuracy.

---

## Table of Contents
1. [Overview](#overview)
2. [Database Schema](#database-schema)
3. [XER File Parsing Architecture](#xer-file-parsing-architecture)
4. [Calendar Manager Dialog](#calendar-manager-dialog)
5. [Calendar Import Flow](#calendar-import-flow)
6. [Interactive Calendar Window](#interactive-calendar-window)
7. [Schedule Module Integration](#schedule-module-integration)
8. [File Structure](#file-structure)
9. [Implementation Phases](#implementation-phases)
10. [Future: XER Schedule Import](#future-xer-schedule-import)

---

## Overview

### Purpose
Enable MILESTONE users to import P6 Primavera project calendars from XER files, visually verify and manage work/non-work days, and use calendar data to calculate remaining durations for 3WLA schedule entries and P6 exports.

The XER parsing infrastructure is designed as a general-purpose reader so it can be extended to import schedule data (activities, relationships) from XER files in the future, providing an alternative to the current Excel-based P6 import.

### Goals
1. Import P6 calendars from XER files with proven recursive descent parser
2. Provide visual calendar interface for verification and manual adjustments
3. Calculate remaining work day durations between WE date or 3WLA Start and 3WLA Finish
4. Include remaining duration in P6 exports
5. Track non-work day categories (Holiday, Weather, Safety) for future AI analysis
6. Establish a reusable XER file reader that can be extended for future schedule data import

### Problem Statement
Currently, MILESTONE has no awareness of the project work calendar. When users enter 3WLA finish dates in the Schedule module, there is no way to calculate how many actual work days remain. The scheduler using P6 operates on a calendar that accounts for holidays, weather days, and RDOs (Regular Days Off), but this data never reaches MILESTONE. This creates a disconnect between MILESTONE's 3WLA forecasting and P6's schedule calculations.

Additionally, the current P6 schedule import relies on an Excel file exported from P6. This introduces a dependency on the scheduler's P6 layout configuration — if columns are missing or renamed, the import silently loses data. XER files use fixed database field names that never change across P6 configurations, making them a more reliable data source. The XER file already exists in the current workflow (schedulers export it regularly), so no new export step is required.

### User Workflow
```
1. Scheduler exports XER from P6 (already done for schedule imports)
2. User opens Tools → Manage Calendars
3. User clicks Import, selects project, browses to XER file
4. Parser presents available calendars from XER, user selects one
5. Calendar imports into local database
6. User clicks Modify to open interactive calendar for visual verification
7. User optionally relabels exception days (NonWork → Holiday, Weather, Safety)
8. Schedule module now calculates remaining duration using imported calendar
9. Remaining duration is included in P6 exports
```

---

## Database Schema

### SQLite Local Tables

Both tables are created during database initialization alongside existing tables.

#### Calendars Table
```sql
CREATE TABLE IF NOT EXISTS Calendars (
    CalendarId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId TEXT NOT NULL,
    P6CalendarId TEXT,
    CalendarName TEXT NOT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    HoursPerDay REAL NOT NULL DEFAULT 10,
    HoursPerWeek REAL NOT NULL DEFAULT 60,
    SunWork INTEGER NOT NULL DEFAULT 0,
    MonWork INTEGER NOT NULL DEFAULT 1,
    TueWork INTEGER NOT NULL DEFAULT 1,
    WedWork INTEGER NOT NULL DEFAULT 1,
    ThuWork INTEGER NOT NULL DEFAULT 1,
    FriWork INTEGER NOT NULL DEFAULT 1,
    SatWork INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NOT NULL DEFAULT '',
    CreatedUtcDate TEXT NOT NULL,
    UpdatedBy TEXT,
    UpdatedUtcDate TEXT
);
```

**Column Notes:**
- `ProjectId` — FK to VMS_Projects.ProjectID (NVARCHAR(50) in Azure, TEXT in SQLite)
- `P6CalendarId` — Original clndr_id from XER file. Used to detect duplicates on re-import. Nullable for manually created calendars. Also used by future per-activity calendar lookup (TASK table's `clndr_id` references this value).
- `IsDefault` — 1 = default calendar for this project. Only one calendar per project should be default. Used by Schedule module as fallback when no per-activity calendar is available.
- `HoursPerDay` / `HoursPerWeek` — Imported from XER (`day_hr_cnt`, `week_hr_cnt`). Not used in V1 calculations but preserved for future AI analysis (crew sizing, production rates).
- `SunWork` through `SatWork` — 0 = off, 1 = work. Defines the weekly baseline pattern. Exceptions override individual dates.

#### CalendarExceptions Table
```sql
CREATE TABLE IF NOT EXISTS CalendarExceptions (
    ExceptionId INTEGER PRIMARY KEY AUTOINCREMENT,
    CalendarId INTEGER NOT NULL,
    ExceptionDate TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'NonWork',
    CreatedBy TEXT NOT NULL DEFAULT '',
    CreatedUtcDate TEXT NOT NULL,
    FOREIGN KEY (CalendarId) REFERENCES Calendars(CalendarId) ON DELETE CASCADE
);
```

**Column Notes:**
- `ExceptionDate` — ISO format "YYYY-MM-DD". Always a deviation from the weekly pattern.
- `Status` — One of: `"NonWork"`, `"Holiday"`, `"Weather"`, `"Safety"`, `"Work"`. See Status Values below.
- `ON DELETE CASCADE` — Deleting a calendar automatically removes all its exceptions.

**Status Values:**

| Status | Meaning | Color (UI) | Example |
|--------|---------|------------|---------|
| `NonWork` | Generic non-work day (default from XER import) | Red | RDO, unclassified day off |
| `Holiday` | Company/federal holiday | Orange | Thanksgiving, Christmas, July 4th |
| `Weather` | Forecasted or actual weather day | Blue | Rain day, hurricane day |
| `Safety` | Safety stand-down | Yellow | Safety stand-down day |
| `Work` | Off-day made into a work day | Green | Sunday turned into a work day |

**How exceptions interact with the weekly pattern:**
- If a date has NO exception row → use the weekly pattern (`SunWork`..`SatWork`)
- If a date HAS an exception with Status = `"Work"` → it IS a work day regardless of weekly pattern (e.g., Sunday made work)
- If a date HAS an exception with any other Status → it is NOT a work day regardless of weekly pattern (e.g., Monday holiday)

### Azure SQL Tables

Follow existing VMS_ naming convention. These tables sync alongside existing tables.

#### VMS_Calendars
```sql
CREATE TABLE VMS_Calendars (
    CalendarId INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ProjectId NVARCHAR(50) NOT NULL,
    P6CalendarId NVARCHAR(50) NULL,
    CalendarName NVARCHAR(255) NOT NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    HoursPerDay FLOAT NOT NULL DEFAULT 10,
    HoursPerWeek FLOAT NOT NULL DEFAULT 60,
    SunWork BIT NOT NULL DEFAULT 0,
    MonWork BIT NOT NULL DEFAULT 1,
    TueWork BIT NOT NULL DEFAULT 1,
    WedWork BIT NOT NULL DEFAULT 1,
    ThuWork BIT NOT NULL DEFAULT 1,
    FriWork BIT NOT NULL DEFAULT 1,
    SatWork BIT NOT NULL DEFAULT 1,
    CreatedBy NVARCHAR(255) NOT NULL DEFAULT N'',
    CreatedUtcDate NVARCHAR(50) NOT NULL,
    UpdatedBy NVARCHAR(255) NULL,
    UpdatedUtcDate NVARCHAR(50) NULL
);
GO
```

#### VMS_CalendarExceptions
```sql
CREATE TABLE VMS_CalendarExceptions (
    ExceptionId INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CalendarId INT NOT NULL,
    ExceptionDate NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT N'NonWork',
    CreatedBy NVARCHAR(255) NOT NULL DEFAULT N'',
    CreatedUtcDate NVARCHAR(50) NOT NULL,
    FOREIGN KEY (CalendarId) REFERENCES VMS_Calendars(CalendarId) ON DELETE CASCADE
);
GO
```

**Sync Notes:**
- Calendar data changes infrequently (weekly at most after schedule updates). Sync strategy can be simple full-replace per project rather than incremental SyncVersion tracking.
- On sync upload: push local Calendars and CalendarExceptions for the current project to Azure, replacing existing rows for that ProjectId.
- On sync download: pull Calendars and CalendarExceptions for the current project from Azure, replacing local rows for that ProjectId.
- This avoids the complexity of conflict resolution since calendar data should be managed by one person (the field engineer or scheduler).

---

## XER File Parsing Architecture

### Design Rationale

XER files are plain text, pipe-delimited exports of the P6 database. They can contain up to 170 tables (typically <20 in practice), all sharing the same `%T/%F/%R` row structure. Rather than building a calendar-specific parser that hardcodes knowledge of the XER format, we split parsing into two layers:

1. **XerFileReader** — General-purpose reader that handles encoding, `%T/%F/%R` structure, and produces raw table data. Accepts a filter for which tables to extract, skipping `%R` rows for unwanted tables to avoid unnecessary memory overhead.
2. **XerCalendarExtractor** — Calendar-specific logic that takes the CALENDAR table output from `XerFileReader` and parses the proprietary `clndr_data` format using recursive descent.

This separation means future features (XER-based schedule import, relationship extraction) reuse `XerFileReader` without modifying it.

### XER File Format Overview

XER files are encoded in **latin-1** (not UTF-8). Structure:
- `%T` lines define table names
- `%F` lines define field names for the current table
- `%R` lines are data rows for the current table

Delimiter: **tab character** (`\t`) separates fields on `%F` and `%R` lines.

### XerFileReader

**Location:** `Services/Xer/XerFileReader.cs`

**Public API:**
```
// Parse specified tables from an XER file
// Returns: dictionary keyed by table name, value is list of row dictionaries (field name → value)
Dictionary<string, List<Dictionary<string, string>>> Parse(string filePath, params string[] tableNames)
```

**Parsing Logic:**
1. Read XER file with `latin-1` encoding (`Encoding.GetEncoding("iso-8859-1")`)
2. Iterate lines:
   - `%T` line → extract table name. Check if it's in the requested `tableNames` array. Set `currentTable` and `isActiveTable` flag.
   - `%F` line → if `isActiveTable`, split by tab to get field names for `currentTable`
   - `%R` line → if `isActiveTable`, split by tab, create `Dictionary<string, string>` mapping field names to values, add to result list for `currentTable`
   - All other lines (`%E`, header line, etc.) → skip
3. If `tableNames` is empty, parse all tables (for diagnostic/future use, but not expected in normal operation)
4. Return the dictionary of parsed tables

**Example usage:**
```csharp
// Calendar import - only parse CALENDAR table
var tables = XerFileReader.Parse(filePath, "CALENDAR");
var calendarRows = tables.GetValueOrDefault("CALENDAR") ?? new List<Dictionary<string, string>>();

// Future schedule import - parse TASK and TASKPRED
var tables = XerFileReader.Parse(filePath, "TASK", "TASKPRED");
```

**Performance:** For a table not in the filter, the reader skips `%R` lines (no string splitting, no dictionary creation). Only the line-reading loop advances. This means parsing CALENDAR from a file with 10,000 TASK rows costs essentially nothing for those TASK rows.

### XerCalendarExtractor

**Location:** `Services/Xer/XerCalendarExtractor.cs`

**Public API:**
```
// Extract calendar data from parsed CALENDAR table rows
List<XerCalendar> Extract(List<Dictionary<string, string>> calendarRows)
```

**This class handles everything calendar-specific:**
- Reading `clndr_id`, `clndr_name`, `clndr_type`, `proj_id`, `day_hr_cnt`, `week_hr_cnt` from row dictionaries
- Parsing `clndr_data` using recursive descent (DaysOfWeek + Exceptions)
- OLE date conversion
- `\x7f` DEL character handling in `clndr_data`

### clndr_data Format

The `clndr_data` field contains a nested parenthetical structure. Key sections:

**DaysOfWeek** — weekly work pattern:
```
(0||DaysOfWeek()(
    (0||1()())                              ← Day 1 (Sunday): empty children = OFF
    (0||2()(                                ← Day 2 (Monday): has children = WORK
        (0||0(s|07:00|f|17:00)())))         ← Shift: start 07:00, finish 17:00
    ...
    (0||7()(                                ← Day 7 (Saturday): WORK
        (0||0(s|07:00|f|17:00)())))
))
```
- Days numbered 1 (Sunday) through 7 (Saturday)
- No children = day off
- Children with `s|HH:MM|f|HH:MM` = work day with those hours

**Exceptions** — individual date overrides:
```
(0||Exceptions()(
    (0||0(d|45988)())                       ← Non-work exception (empty children)
    (0||125(d|46054)(                       ← Work exception (has shift children)
        (0||0(s|07:00|f|17:00)())))
))
```
- `d|NNNNN` = OLE Automation date (days since 1899-12-30)
- Empty children `()` = non-work day
- Children with shift data = work day (modified hours or off-day made work)

**Note:** `\x7f` (DEL) characters appear as whitespace in the raw data. The parser must handle these.

### Recursive Descent Parser

**Use a recursive descent parser, NOT regex.** Regex was proven unreliable in testing — it missed exceptions where off-days were turned into work days because the shift children had nested structure that regex couldn't capture.

**Parser Logic (inside XerCalendarExtractor):**
```
ParseNode():
    consume '('
    read name until '(' or ')'
    if next is '(':  // params
        consume '('
        read params until ')'
        consume ')'
    if next is '(':  // children
        consume '('
        while next != ')':
            if next is '(': children.add(ParseNode())
            else: advance
        consume ')'
    consume ')'
    return Node(name, params, children)
```

**Critical: Skip whitespace (spaces, tabs, newlines, `\x7f` DEL characters) when peeking for the next structural character.**

### OLE Date Conversion
```
OLE Automation Date = number of days since December 30, 1899
Example: 46054 = DateTime(1899, 12, 30) + 46054 days = 2026-02-01
```

### Output Models

```
class XerCalendar
{
    string ClndrId           // P6 calendar ID
    string CalendarName      // Display name
    string CalendarType      // "CA_Base" or "CA_Project"
    string? ProjectId        // P6 project ID (null for global)
    double HoursPerDay       // From day_hr_cnt
    double HoursPerWeek      // From week_hr_cnt
    bool[] WorkDays          // Index 0=Sun, 1=Mon...6=Sat. true=work
    List<XerException> Exceptions
}

class XerException
{
    DateTime Date            // Converted from OLE date
    bool IsWorkDay           // true = modified/added work, false = non-work
}
```

### Validated Test Results

The parser was validated against a live P6 XER export (project 5724, "Fluor Near Site" calendar ID 9633):

- **Weekly pattern:** Sunday OFF, Monday–Saturday 07:00–17:00 (10 hrs/day)
- **Total exceptions found:** 177
- **Non-work exceptions:** 173 (holidays, RDOs, weather days, shutdowns)
- **Work exceptions:** 4:
  - 2025-11-23 (Sunday) → made work day 07:00–17:00
  - 2026-02-01 (Sunday) → made work day 07:00–17:00
  - 2026-03-30 (Monday) → modified hours 08:00–16:00
  - 2026-03-31 (Tuesday) → modified hours 08:00–16:00
- **All results confirmed accurate** against P6 calendar dialog

---

## Calendar Manager Dialog

### Access
**Tools menu → Manage Calendars**

### Layout
```
┌─────────────────────────────────────────────────────────────────┐
│ Manage Calendars                                           [X]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Project │ Calendar Name       │ Hrs/Day │ Hrs/Week │ Def │  │
│  │─────────│─────────────────────│─────────│──────────│─────│  │
│  │ 24-005  │ Fluor Near Site     │ 10      │ 60       │ ✓   │  │
│  │ 24-005  │ Fluor OSM           │ 10      │ 50       │     │  │
│  │ 24-012  │ Client Calendar     │ 8       │ 40       │ ✓   │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│  [Import]    [Modify]    [Delete]    [Set Default]    [Close]   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Components
- **SfDataGrid** — Read-only list of all calendars in local database
- **Columns:** ProjectId, CalendarName, HoursPerDay, HoursPerWeek, IsDefault (checkbox)
- **Selection:** Single row selection
- **Sorting:** By ProjectId, then CalendarName

### Buttons

| Button | Enabled When | Action |
|--------|-------------|--------|
| Import | Always | Opens import flow (see Calendar Import Flow) |
| Modify | Row selected | Opens Interactive Calendar Window for selected calendar |
| Delete | Row selected | Confirmation dialog → deletes calendar + all exceptions (CASCADE) |
| Set Default | Row selected, not already default | Sets selected calendar as default for its project, clears other defaults for same project |
| Close | Always | Closes dialog |

### Delete Confirmation
```
"Delete calendar 'Fluor Near Site' for project 24-005?
This will remove the calendar and all [X] exception dates.
This cannot be undone."

[Delete]  [Cancel]
```

---

## Calendar Import Flow

### Step 1: Select Project
Dialog or combo box to select which ProjectId to import the calendar for. Populated from VMS_Projects in local database.

### Step 2: Browse to XER File
Standard file browser dialog filtered to `*.xer` files.

### Step 3: Parse and Present Calendars

`XerFileReader.Parse(filePath, "CALENDAR")` reads the file, then `XerCalendarExtractor.Extract()` parses the CALENDAR rows:

```
┌─────────────────────────────────────────────────────────────┐
│ Select Calendar(s) to Import                           [X]  │
├─────────────────────────────────────────────────────────────┤
│ XER File: C:\Projects\schedule_update.xer                   │
│ Target Project: 24-005                                      │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ ☐ │ P6 ID │ Name                    │ Type    │ Proj  │  │
│  │───│───────│─────────────────────────│─────────│───────│  │
│  │ ☑ │ 9633  │ Fluor Near Site         │ Project │ 5724  │  │
│  │ ☐ │ 9179  │ Fluor Near Site         │ Global  │       │  │
│  │ ☐ │ 6785  │ Fluor Near Site 60/60/50│ Global  │       │  │
│  │ ☐ │ 5100  │ Fluor Fab/Engineering   │ Global  │       │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                             │
│  [Import Selected]                              [Cancel]    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

- **Checkbox selection** — User can select one or more calendars
- **Type column** — "Project" or "Global" (from `clndr_type`)
- **Proj column** — P6 project ID if project-level calendar

### Step 4: Duplicate Detection

Before importing, check if a calendar with the same `P6CalendarId` + `ProjectId` already exists in the local database.

**If duplicate found, show warning:**
```
"A calendar named 'Fluor Near Site' (P6 ID: 9633) already exists
for project 24-005.

Overwriting will replace the weekly pattern and all exception dates.
Any custom labels you've applied to exception dates (Holiday, Weather,
Safety) will be reset to NonWork.

[Overwrite]  [Cancel]"
```

- **Overwrite** — Delete existing calendar + exceptions, import fresh
- **Cancel** — Skip this calendar (continue with others if multiple selected)

### Step 5: Import Execution

For each selected calendar:
1. If overwriting: delete existing calendar row (CASCADE deletes exceptions)
2. Insert row into `Calendars` table:
   - `ProjectId` = user-selected project
   - `P6CalendarId` = from XER
   - `CalendarName` = from XER
   - `HoursPerDay` / `HoursPerWeek` = from XER
   - `SunWork`..`SatWork` = from parsed `DaysOfWeek` section
   - `CreatedBy` = `App.CurrentUser.Username`
   - `CreatedUtcDate` = `DateTime.UtcNow` in ISO format
3. If this is the first calendar for the project, set `IsDefault = 1`
4. Insert rows into `CalendarExceptions` for each parsed exception:
   - Non-work exceptions → `Status = "NonWork"`
   - Work exceptions (off-day made work) → `Status = "Work"`
   - `CreatedBy` = `App.CurrentUser.Username`
   - `CreatedUtcDate` = `DateTime.UtcNow` in ISO format

### Logging
```csharp
AppLogger.Info($"Imported calendar '{calendarName}' (P6 ID: {p6Id}) for project {projectId} with {exceptionCount} exceptions", "CalendarImportService.Import", App.CurrentUser!.Username);
```

---

## Interactive Calendar Window

### Access
Calendar Manager → select calendar → click **Modify**

### Window Properties
- **Detached window** (not modal — user can interact with main app while open)
- **Title:** "{CalendarName} — {ProjectId}"
- **Resizable**, reasonable default size (~800x600)
- **Can have multiple calendar windows open simultaneously** (different calendars)

### Layout
```
┌─────────────────────────────────────────────────────────────────┐
│ Fluor Near Site — 24-005                                   [X]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│       [<]         February 2026          [>]                    │
│                                                                 │
│  ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┐            │
│  │ Mon  │ Tue  │ Wed  │ Thu  │ Fri  │ Sat  │ Sun  │            │
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────┤            │
│  │      │      │      │      │      │      │  1   │            │
│  │      │      │      │      │      │      │ OFF  │            │
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────┤            │
│  │  2   │  3   │  4   │  5   │  6   │  7   │  8   │            │
│  │ WORK │ WORK │ WORK │ WORK │ WORK │ WORK │ OFF  │            │
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────┤            │
│  │  9   │  10  │  11  │  12  │  13  │  14  │  15  │            │
│  │ WORK │ WORK │ WORK │ WORK │ WORK │ WORK │ OFF  │            │
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────┤            │
│  │  16  │  17  │  18  │  19  │  20  │  21  │  22  │            │
│  │ WORK │ WORK │ WORK │ WORK │ WORK │ WORK │ OFF  │            │
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────┤            │
│  │  23  │  24  │  25  │  26  │  27  │  28  │      │            │
│  │ WORK │ WORK │ WORK │ NW   │ WORK │ WORK │      │            │
│  └──────┴──────┴──────┴──────┴──────┴──────┴──────┘            │
│                                                                 │
│  Legend:  ■ Work  ■ NonWork  ■ Holiday  ■ Weather  ■ Safety    │
│                                                                 │
│  Click a day to cycle its status.                               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Rendering Logic

For each day cell in the displayed month:

1. Check if an exception exists for that date in `CalendarExceptions`
2. If exception exists → use the exception's Status for color and label
3. If no exception → check weekly pattern (`SunWork`..`SatWork`) for that day of week
   - If work day (1) → green "WORK"
   - If off day (0) → gray/default "OFF"

### Color Scheme

Colors should work within the FluentDark theme:

| Status | Background | Label |
|--------|-----------|-------|
| Work (pattern) | Dark green (#2E7D32 or similar) | WORK |
| Off (pattern) | Default/neutral | OFF |
| NonWork (exception) | Red (#C62828 or similar) | NW |
| Holiday (exception) | Orange (#EF6C00 or similar) | HOL |
| Weather (exception) | Blue (#1565C0 or similar) | WX |
| Safety (exception) | Yellow (#F9A825 or similar) | SAF |
| Work (exception) | Bright green (#43A047 or similar) | WORK* |

**Note:** "WORK*" or a different shade distinguishes an exception-work day (like a Sunday made work) from a normal pattern work day. The asterisk or distinct shade makes it visible during verification.

### Click Interaction

**Left-click a day cell** cycles through statuses:

**If the day is a WORK day by weekly pattern (no exception):**
- Click 1: Add exception → NonWork
- Click 2: Change exception → Holiday
- Click 3: Change exception → Weather
- Click 4: Change exception → Safety
- Click 5: Remove exception → back to WORK (pattern)

**If the day is an OFF day by weekly pattern (no exception):**
- Click 1: Add exception → Work
- Click 2: Remove exception → back to OFF (pattern)

**If the day already has an exception:**
- Cycle continues from current status through the appropriate cycle above

### Persistence

- Each click immediately saves to the local SQLite database (insert, update, or delete in `CalendarExceptions`)
- No Save button needed — changes are live
- `UpdatedBy` and `UpdatedUtcDate` on the parent `Calendars` row should be updated when any exception is modified

### Syncfusion Component Options

**Option 1: SfScheduler in MonthView**
- Built-in month navigation, day cell templates, appointment-like styling
- May require workarounds for the simple "click to cycle" interaction
- Consider if the built-in features justify the complexity

**Option 2: Custom Grid (UniformGrid or WrapPanel)**
- Full control over rendering and click behavior
- Simpler to implement exactly as designed
- No Syncfusion dependencies for this specific UI

**Recommendation:** Evaluate SfScheduler first. If cell click customization is too constrained, fall back to a custom WPF grid. The calendar UI is simple enough that a custom approach may be cleaner.

---

## Schedule Module Integration

### Remaining Duration Calculation

**CalendarService** provides a utility method:

```
int CountWorkDays(int calendarId, DateTime startDate, DateTime endDate)
```

This method:
1. Loads the calendar's weekly pattern
2. Loads all exceptions for the calendar within the date range
3. Iterates day by day from startDate to endDate (exclusive of startDate, inclusive of endDate — or vice versa, to be confirmed during implementation)
4. For each day: checks exception first, then weekly pattern
5. Returns count of work days

### Calendar Resolution Per Activity

Remaining duration should use the most specific calendar available:

1. **Per-activity calendar** (future — requires XER schedule import): If the Schedule row has a `P6CalendarId` value, look up the Calendars table where `P6CalendarId` matches and `ProjectId` matches. Use that calendar.
2. **Project default calendar** (V1): If no per-activity calendar, use the default calendar for the project (`IsDefault = 1`).
3. **No calendar**: If no calendar exists for the project, remaining duration is not calculated (field left blank or shows "N/A"). Do not fall back to simple calendar day math — that would be misleading.

This two-tier lookup is forward-compatible. V1 always hits tier 2 (project default). When XER schedule import is added, activities with a `P6CalendarId` in the Schedule table will hit tier 1 automatically.

### Where Remaining Duration is Calculated

In the **Schedule module** when displaying or exporting 3WLA data:

**For in-progress activities** (P6_ActualStart exists, no P6_ActualFinish):
```
RemainingDuration = CountWorkDays(resolvedCalendarId, weekEndDate, threeWeekFinish)
```

**For not-started activities** (both ThreeWeekStart and ThreeWeekFinish are in the future):
```
RemainingDuration = CountWorkDays(resolvedCalendarId, threeWeekStart, threeWeekFinish)
```

Where `resolvedCalendarId` follows the resolution order above.

**If no calendar exists for the project:** remaining duration is not calculated (field left blank or shows "N/A"). Do not fall back to simple calendar day math — that would be misleading.

### P6 Export Integration

When the Schedule module exports data for P6 import, include the calculated remaining duration as a column. The P6 scheduler can use this value to update activity remaining durations in the schedule, which feeds into the scheduling engine's forward/backward pass.

### CalendarService Class

**Location:** `Services/Calendar/CalendarService.cs`

**Public API:**
```
// Count work days between two dates using a specific calendar
int CountWorkDays(int calendarId, DateTime startDate, DateTime endDate)

// Get the default calendar for a project (returns null if none exists)
Calendar? GetDefaultCalendar(string projectId)

// Get a calendar by its P6 calendar ID for a specific project (returns null if not found)
Calendar? GetCalendarByP6Id(string p6CalendarId, string projectId)

// Resolve the correct calendar for a schedule activity:
// 1. Per-activity P6CalendarId → 2. Project default → 3. null
Calendar? ResolveCalendar(string? p6CalendarId, string projectId)

// Check if a specific date is a work day
bool IsWorkDay(int calendarId, DateTime date)
```

**Caching:** The calendar weekly pattern and exceptions should be cached in memory after first load since they rarely change. Cache should invalidate when the Interactive Calendar Window modifies exceptions or when a new import occurs.

---

## File Structure

```
VANTAGE/
│
├── Models/
│   └── Calendar/
│       ├── Calendar.cs                      // Calendar entity model
│       ├── CalendarException.cs             // Exception entity model
│       └── XerCalendar.cs                   // Extractor output model (XerCalendar + XerException)
│
├── Services/
│   ├── Xer/
│   │   ├── XerFileReader.cs                 // General-purpose XER parser (%T/%F/%R)
│   │   └── XerCalendarExtractor.cs          // Calendar-specific: clndr_data recursive descent
│   │
│   └── Calendar/
│       ├── CalendarRepository.cs            // CRUD for Calendars and CalendarExceptions (SQLite)
│       └── CalendarService.cs               // Work day counting, calendar resolution, caching
│
├── ViewModels/
│   ├── CalendarManagerViewModel.cs          // Calendar Manager dialog logic
│   ├── CalendarImportViewModel.cs           // Import flow (project selection, file browse, calendar pick, duplicate detection)
│   └── InteractiveCalendarViewModel.cs      // Interactive calendar window logic (month nav, click handling)
│
└── Views/
    ├── Dialogs/
    │   ├── CalendarManagerDialog.xaml        // Manage Calendars dialog
    │   └── CalendarImportDialog.xaml         // Import flow dialog(s)
    └── CalendarWindow.xaml                   // Detached interactive calendar window
```

---

## Implementation Phases

### Phase 1: Database Schema & Models
**Files:** Models/Calendar/*, database initialization code
**Tasks:**
1. Create Calendar, CalendarException, XerCalendar, XerException model classes
2. Add Calendars and CalendarExceptions table creation to SQLite database initialization
3. Prepare Azure SQL creation script (VMS_Calendars, VMS_CalendarExceptions)

### Phase 2: XER Parsing
**Files:** Services/Xer/XerFileReader.cs, Services/Xer/XerCalendarExtractor.cs

#### Phase 2a: XerFileReader (General-Purpose)
**Tasks:**
1. Implement latin-1 encoded file reading
2. Implement `%T/%F/%R` line detection and table name extraction
3. Implement table name filter (`params string[] tableNames`) — skip `%R` lines for non-requested tables
4. Build `Dictionary<string, List<Dictionary<string, string>>>` output
5. Handle edge cases: empty fields, trailing delimiters, `%E` end markers

#### Phase 2b: XerCalendarExtractor (Calendar-Specific)
**Tasks:**
1. Map CALENDAR table field names to XerCalendar properties (`clndr_id`, `clndr_name`, `clndr_type`, `proj_id`, `day_hr_cnt`, `week_hr_cnt`)
2. Implement recursive descent parser for `clndr_data`
3. Implement OLE date conversion
4. Implement DaysOfWeek extraction (weekly work pattern)
5. Implement Exceptions extraction (non-work and work exceptions)
6. Handle `\x7f` DEL characters in `clndr_data`
7. Return `List<XerCalendar>` with fully parsed data
8. Test against known XER file to validate all 177 exceptions parse correctly

### Phase 3: Calendar Repository
**Files:** Services/Calendar/CalendarRepository.cs
**Tasks:**
1. Implement InsertCalendar (returns new CalendarId)
2. Implement InsertExceptions (bulk insert for all exceptions of a calendar)
3. Implement GetCalendarsForProject (returns list filtered by ProjectId)
4. Implement GetAllCalendars (for Calendar Manager grid)
5. Implement GetExceptionsForCalendar (returns all exceptions for a CalendarId)
6. Implement GetExceptionsInRange (date range query for work day counting)
7. Implement DeleteCalendar (CASCADE handles exceptions)
8. Implement SetDefault (set one, clear others for same project)
9. Implement UpsertException (for interactive calendar clicks — insert/update/delete)
10. Implement DeleteExceptionByDate (for removing an exception when cycling back to pattern)
11. Implement FindByP6Id (for duplicate detection: P6CalendarId + ProjectId)

### Phase 4: Calendar Manager Dialog
**Files:** ViewModels/CalendarManagerViewModel.cs, Views/Dialogs/CalendarManagerDialog.xaml
**Tasks:**
1. Create dialog with SfDataGrid showing all calendars
2. Wire up Import button to import flow
3. Wire up Modify button to open Interactive Calendar Window
4. Wire up Delete button with confirmation dialog
5. Wire up Set Default button
6. Wire up Close button
7. Add "Manage Calendars" menu item to Tools menu in MainWindow

### Phase 5: Calendar Import Flow
**Files:** ViewModels/CalendarImportViewModel.cs, Views/Dialogs/CalendarImportDialog.xaml
**Tasks:**
1. Project selection combo box (populated from local VMS_Projects)
2. File browse button with .xer filter
3. Call `XerFileReader.Parse(filePath, "CALENDAR")` then `XerCalendarExtractor.Extract()` on file selection
4. Populate calendar selection grid from extractor output
5. Checkbox selection for multiple calendars
6. Duplicate detection on import (check P6CalendarId + ProjectId)
7. Overwrite warning dialog with custom label reset notice
8. Execute import: insert calendar + bulk insert exceptions
9. Auto-set IsDefault if first calendar for project
10. Refresh Calendar Manager grid after import
11. Logging for import operations

### Phase 6: Interactive Calendar Window
**Files:** ViewModels/InteractiveCalendarViewModel.cs, Views/CalendarWindow.xaml
**Tasks:**
1. Create detached Window (not modal)
2. Month navigation (< > arrows, month/year display)
3. Render monthly grid with day cells
4. Color-code each day based on exception then weekly pattern
5. Click handler to cycle status (insert/update/delete exceptions)
6. Immediate persistence on click
7. Legend display
8. Support multiple windows open simultaneously

### Phase 7: Schedule Module Integration
**Files:** Services/Calendar/CalendarService.cs, Schedule module modifications
**Tasks:**
1. Implement CountWorkDays method
2. Implement GetDefaultCalendar method
3. Implement GetCalendarByP6Id method
4. Implement ResolveCalendar method (per-activity → project default → null)
5. Implement IsWorkDay method
6. Add caching layer for calendar data
7. Integrate remaining duration calculation into Schedule view (using project default for V1)
8. Add RemainingDuration column to P6 export
9. Handle missing calendar gracefully (blank/N/A)

### Phase 8: Azure Sync
**Files:** Sync-related code (SyncManager or equivalent)
**Tasks:**
1. Run VMS_Calendars and VMS_CalendarExceptions creation scripts on Azure
2. Add calendar sync logic (upload local → Azure, download Azure → local)
3. Simple full-replace strategy per ProjectId (not incremental)
4. Test sync round-trip

---

## Future: XER Schedule Import

This section documents the planned extension of `XerFileReader` to support importing schedule data directly from XER files, providing an alternative to the current Excel-based P6 import (`ScheduleExcelImporter`).

### Timeline
Not scheduled. Implement after calendar features are stable and field-tested. The existing Excel import remains fully functional and will not be removed.

### Transition Strategy
Both import paths will coexist:
- **File → Import From P6 File** — Existing Excel import (unchanged)
- **File → Import From P6 XER** — New XER import (to be added)

During the transition period, users can try both methods with the same week's data to verify they produce identical results. Once XER import is validated through real-world use, it can become the recommended path. The Excel import stays as a fallback.

### What XER Provides That Excel Does Not

| Data | XER Source | Benefit |
|------|-----------|---------|
| Per-activity calendar ID | TASK.`clndr_id` | Accurate remaining duration per activity instead of one project-wide calendar |
| Activity relationships | TASKPRED table | Foundation for AI schedule conflict detection, critical path analysis |
| Total float | TASK.`total_float_hr_cnt` | AI risk flagging — zero/negative float = critical path |
| WBS hierarchy | PROJWBS table | Structured breakdown instead of flat WBS code string |
| Resource assignments | TASKRSRC table | AI manpower analysis, production rate flagging |
| Constraints | TASK.`cstr_type` / `cstr_date` | Schedule constraint awareness |
| Remaining duration | TASK.`remain_drtn_hr_cnt` | P6-calculated remaining duration for comparison |

### TASK Table Field Mapping to Existing Schedule Model

These are the fields currently imported via Excel, mapped to their XER equivalents:

| XER TASK Field | Schedule Model Property | Notes |
|---------------|------------------------|-------|
| `task_code` | SchedActNO | Activity ID |
| `task_name` | Description | Activity name |
| `wbs_id` (FK → PROJWBS) | WbsId | Need to resolve to WBS code via PROJWBS table |
| `target_start_date` | P6_Start | Planned start |
| `target_end_date` | P6_Finish | Planned finish |
| `act_start_date` | P6_ActualStart | Actual start |
| `act_end_date` | P6_ActualFinish | Actual finish |
| `phys_complete_pct` | P6_PercentComplete | Physical % complete |
| `target_work_qty` | P6_BudgetMHs | Budgeted labor hours |
| `status_code` | (read but not stored) | Same as current behavior |
| `clndr_id` | P6CalendarId **(new column)** | Links to Calendars.P6CalendarId |

### Schedule Table Modification (When XER Import is Implemented)

Add one column to the Schedule table:
```sql
-- SQLite
ALTER TABLE Schedule ADD COLUMN P6CalendarId TEXT;

-- Azure
ALTER TABLE VMS_Schedule ADD P6CalendarId NVARCHAR(50) NULL;
```

This stores the P6 calendar ID assigned to each activity. When present, `CalendarService.ResolveCalendar()` uses it to find the correct calendar for that specific activity's remaining duration calculation. When null (Excel-imported rows), falls back to the project default calendar.

### TASKPRED Table (Relationships)

The TASKPRED table contains activity-to-activity relationships:

| XER Field | Purpose |
|-----------|---------|
| `task_id` | Successor activity (FK → TASK) |
| `pred_task_id` | Predecessor activity (FK → TASK) |
| `pred_type` | Relationship type: PR_FS, PR_FF, PR_SS, PR_SF |
| `lag_hr_cnt` | Lag/lead time in hours |
| `float_path` | Float path information |

This data has no current home in MILESTONE's database. A new `ScheduleRelationships` table would be created when this feature is implemented. Relationships are the foundation for AI-powered schedule conflict detection — without them, AI can only compare dates. With them, AI can trace logic chains and predict cascade delays.

### Implementation Approach (When Ready)

1. Create `Services/Xer/XerScheduleExtractor.cs` — takes TASK (and optionally PROJWBS, TASKPRED) tables from `XerFileReader` output, maps to existing `Schedule` model
2. Add `P6CalendarId` column to Schedule table
3. Add new menu item "Import From P6 XER" alongside existing "Import From P6 File"
4. New extractor feeds the same `Schedule` model → same `ImportToDatabase` logic → same Schedule module views
5. Validate side-by-side with Excel import before recommending as primary path

---

## Technical Notes

### Syncfusion Theme Compliance
All UI must use the existing FluentDark theme. Use `SfDataGrid` for the Calendar Manager list. For the interactive calendar, evaluate `SfScheduler` MonthView first — if cell customization is too limited, build a custom WPF grid using theme colors from `DarkTheme.xaml`.

### Error Handling
- XER parse failures: catch and display user-friendly message ("Could not parse calendar data from XER file. The file may be corrupt or in an unsupported format.")
- Empty XER (no calendars): inform user ("No calendars found in the selected XER file.")
- Database errors: log with AppLogger.Error, show generic error to user

### Logging Requirements
```csharp
// Import
AppLogger.Info($"Imported calendar '{name}' (P6: {p6Id}) for project {projectId}: {count} exceptions", "CalendarImportService.Import", App.CurrentUser!.Username);

// Overwrite
AppLogger.Info($"Overwrote calendar '{name}' for project {projectId}", "CalendarImportService.Import", App.CurrentUser!.Username);

// Delete
AppLogger.Info($"Deleted calendar '{name}' (ID: {id}) for project {projectId}", "CalendarManagerViewModel.Delete", App.CurrentUser!.Username);

// Exception modified via interactive calendar
AppLogger.Info($"Calendar exception updated: {date} → {status} for '{calendarName}'", "InteractiveCalendarViewModel.CycleStatus", App.CurrentUser!.Username);

// Set default
AppLogger.Info($"Set default calendar for project {projectId}: '{name}'", "CalendarManagerViewModel.SetDefault", App.CurrentUser!.Username);
```

### Nullable Reference Type Conventions
Follow project standards:
- `string?` for optional fields (P6CalendarId, UpdatedBy, UpdatedUtcDate)
- `string` for required fields (ProjectId, CalendarName, Status)
- `Calendar?` for methods that might not find a result (GetDefaultCalendar, GetCalendarByP6Id, ResolveCalendar)
- `= null!` for properties set during initialization/import
- `?? defaultValue` for ExecuteScalar results

### Performance Considerations
- CalendarExceptions for a single calendar: typically 100-200 rows. No performance concern for queries.
- CountWorkDays iterates day-by-day. For a 3-week lookahead this is ~21 iterations maximum — trivial.
- Cache calendar + exceptions in memory for the active project to avoid repeated DB queries during Schedule grid rendering with many activities.
- XerFileReader skips `%R` lines for non-requested tables, so parsing CALENDAR from a large XER with thousands of TASK rows has minimal overhead.

---

## Deferred to V2

### AI Auto-Labeling of Exception Types
Use Claude API to automatically label exceptions imported from XER:
- Federal/company holidays are identifiable by date (Thanksgiving = 4th Thursday of November, Christmas = Dec 25, etc.)
- Weather days could be identified by pattern (scattered throughout calendar, often on Saturdays)
- Safety stand-downs are harder to auto-detect but could be flagged for user review
- User would review AI suggestions before applying
