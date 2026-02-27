# VANTAGE: Milestone - Project Architecture

## Overview
WPF application replacing legacy MS Access VBA system (OldVantage) for tracking construction activities on industrial projects. Each record represents field actions like welding, bolt-ups, or steel erection.

## Architecture

```
+------------------+          +-------------------+
|   Local SQLite   |  <--->   |  Azure SQL Server |
|  (offline work)  |   sync   | (central authority)|
+------------------+          +-------------------+
```

- **Local SQLite:** Fast queries, offline capability
- **Azure SQL:** Central authority, multi-user collaboration
- **Sync:** Bidirectional with SyncVersion-based change tracking
- **Publishing:** Self-contained exe with auto-update via GitHub Releases (manifest.json + ZIP)
- **Credentials:** AES-256 encrypted config file (appsettings.enc) loaded at runtime

## Data Model

### Activity (~87 properties)
Core record representing a construction activity.

**Key Fields:**
- `UniqueID` - Primary key for sync matching
- `ActivityID` - Azure auto-increment PK
- `ProjectID` - Links to Projects table
- `SchedActNO` - Links to P6 schedule activities
- `AssignedTo` - Record ownership
- `PercentEntry` - Completion percentage (0-100)
- `SchStart/SchFinish` - Actual start/finish dates
- `LocalDirty` - 1=needs sync, 0=clean
- `SyncVersion` - Monotonic version for sync
- `UDF1-UDF17, UDF20` - User-defined fields for project-specific data

**Calculated Fields:**
- `EarnMHsCalc`, `EarnedQtyCalc`, `PercentCompleteCalc`, `Status`, `ROCLookupID`

**Numeric Precision:**
- All double values enforced to 3 decimal places via `NumericHelper.RoundToPlaces()`
- Applied at: import, model setters, grid edit, export, database save

### ProgressSnapshots (Azure)
Frozen copy of Activity at weekly submission time.
- Composite PK: `UniqueID + WeekEndDate`
- Created by Submit Progress button
- Auto-purged after 4 weeks

### Reference Tables (Mirrored from Azure)
- `Projects` - Valid project IDs
- `Users` - User accounts with email
- `Admins` - Admin privileges (Azure only)
- `Managers` - Project managers
- `ColumnMappings` - Excel import/export mappings
- `Feedback` - Ideas/Bugs board entries

### Local Tables
- `Activities` - Primary data table (synced with Azure VMS_Activities)
- `Schedule` - P6 import data
- `ScheduleProjectMappings` - Links schedules to projects
- `ThreeWeekLookahead` - 3WLA forecast data
- `AppSettings` - Application configuration
- `UserSettings` - Per-user preferences (layouts, theme, filters)
- `FormTemplates` - Work package form templates
- `WPTemplates` - Work package templates
- `ProgressBookLayouts` - Saved progress book configurations

## Sync System

### Sync Flow
1. User edits record -> `LocalDirty=1`
2. User clicks Sync -> metadata validation -> connection check
3. Mirror reference tables from Azure
4. **Push:** LocalDirty=1 records to Azure (SqlBulkCopy)
5. **Pull:** Records where SyncVersion > LastPulledSyncVersion
6. Handle deletions (IsDeleted=1 in Azure -> delete locally)

### Conflict Resolution
- **Ownership:** Only record owner can modify; failed records force-pulled
- **Deletion:** Sync blocked if record deleted in Azure
- **Version:** SyncVersion always wins (Azure is authority)

## Module Overview

### Progress Module
- SfDataGrid with 90+ columns, virtualization for 200k+ records
- Excel import/export (OldVantage compatible)
- Cell editing with auto-save
- Assignment/ownership with email notifications
- Metadata validation (9 required fields)
- Soft delete (Azure IsDeleted flag)
- User-defined filters (multi-condition with AND/OR logic)
- Prorate BudgetMHs across filtered activities (proportional distribution)

### Schedule Module
- P6 Primavera import/export (uses current schedule dates, not baseline)
- Compare P6 schedule vs Vantage actuals
- Discrepancy filters: Actual Start, Actual Finish, MHs, % Complete (dropdown with clear option)
- MS Rollups: MIN(start), MAX(finish when all complete), weighted % average
- Three-Week Lookahead (3WLA) forecasting with required field indicators
- Missed Start/Finish reason tracking with auto-fill for early completions
- Schedule Change Log: tracks detail grid edits, view/apply changes to Activities
- Master/detail grid layout with Clear Filters button

### Analysis Module
- 4×2 resizable grid layout with independent row/column splitters
- Summary metrics grid with Group By dropdown, user filter, project multi-select
- Aggregated columns: BudgetMHs, EarnedMHs, Quantity, QtyEarned, % Complete
- Conditional cell coloring on % Complete (red/orange/yellow/green thresholds)
- All settings persist to UserSettings

### Progress Books Module
- PDF generation for field progress tracking sheets
- Layout builder: grouping (up to 10 levels), sorting, column selection
- Paper size and font size options, exclude completed filter
- AI Progress Scan: AWS Textract table extraction from scanned PDFs/images
- Scan results grid with filtering, contrast adjustment, batch apply to Activities

### Work Package Module
- PDF generation for construction work packages (replaces legacy MS Access VBA)
- Four template types: Cover (image), List (TOC), Form (checklist), Grid (empty rows)
- Token-based content binding ({WorkPackage}, {ProjectName}, {PrintedDate}, etc.)
- Customizable templates stored in local SQLite (FormTemplates, WPTemplates tables)
- Syncfusion.Pdf for PDF generation, merge individual forms into single package
- See `WorkPackage_Module_Plan.md` for details

### Admin System
- Azure Admins table (single source of truth)
- Manage users, projects, snapshots
- View/restore/purge deleted records

### Deleted Records Module
- View soft-deleted Activities (IsDeleted=1 in Azure)
- Restore deleted records back to active state
- Permanently purge records (admin only)

### Feedback Board
- Ideas and bug tracking system
- Azure-synced (VMS_Feedback table)
- Status workflow: Submitted → In Progress → Completed
- Admin controls for status management and deletion
- Auto-purge of deleted items after 30 days

### Procore Integration
- OAuth 2.0 authentication with token refresh
- API integration for project data sync
- See `Procore_Plan.md` for implementation details

### Help Sidebar
- WebView2-based HTML manual with virtual host mapping
- Collapsible sidebar panel, search functionality
- Context-aware (future: link UI elements to help sections)

### Theme System
- Three themes: Dark (default), Light, Orchid
- Theme Manager dialog (Settings > Theme...)
- StaticResource bindings, theme applied on restart
- Syncfusion SfSkinManager integration (FluentDark/FluentLight base themes)

## Performance Targets
- Sync 5k records: ~6 seconds
- Grid handles 200k+ records via virtualization
- MS rollup calculation: <3 seconds for 200 activities
