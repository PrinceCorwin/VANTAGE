# MILESTONE - Project Architecture

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

## Data Model

### Activity (78 editable + 12 system fields)
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

**Calculated Fields:**
- `EarnMHsCalc`, `EarnedQtyCalc`, `PercentCompleteCalc`, `Status`, `ROCLookupID`

### ProgressSnapshots (Azure)
Frozen copy of Activity at weekly submission time.
- Composite PK: `UniqueID + WeekEndDate`
- Created by Submit Progress button
- Auto-purged after 4 weeks

### Key Reference Tables
- `Projects` - Valid project IDs
- `Users` - User accounts with email
- `Admins` - Admin privileges (Azure only)
- `ColumnMappings` - Excel import/export mappings

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

### Schedule Module
- P6 Primavera import/export
- Compare P6 schedule vs MILESTONE actuals
- MS Rollups: MIN(start), MAX(finish when all complete), weighted % average
- Three-Week Lookahead (3WLA) forecasting
- Master/detail grid layout

### Admin System
- Azure Admins table (single source of truth)
- Manage users, projects, snapshots
- View/restore/purge deleted records

## Performance Targets
- Sync 5k records: ~6 seconds
- Grid handles 200k+ records via virtualization
- MS rollup calculation: <3 seconds for 200 activities
