# Azure Migration Execution Plan

**Date:** January 26, 2026
**Branch:** `azure-company-migration` (from main)
**Status:** Ready for Execution

---

## Overview

Migrate MILESTONE from personal Azure subscription to company Azure (`summitpc.database.windows.net`/`projectcontrols`). This involves:
1. Creating VMS_ prefixed tables on company Azure
2. Migrating reference data (Users, Projects, Admins, ColumnMappings, Managers)
3. Updating all C# code to use VMS_ table names
4. Simplifying Credentials.cs to remove personal Azure

---

## Pre-Migration Checklist

- [ ] Create new branch `azure-company-migration` from main
- [ ] Verify company Azure connection works (csurles credentials in Credentials.cs)
- [ ] Backup current Credentials.cs locally (not in git)

---

## Phase 1: Create VMS_ Tables on Company Azure

**Execute via Visual Studio SQL Query tool connected to `summitpc.database.windows.net`/`projectcontrols`**

Tables to create (12 total):

| # | Table | Primary Key | Notes |
|---|-------|-------------|-------|
| 1 | VMS_GlobalSyncVersion | Id (single row) | Seed with (1, 0) |
| 2 | VMS_Users | UserID (identity) | Reference table |
| 3 | VMS_Admins | AdminID (identity) | Reference table |
| 4 | VMS_Projects | ProjectID (nvarchar) | Reference table |
| 5 | VMS_Managers | ManagerID (identity) | Reference table |
| 6 | VMS_ColumnMappings | MappingID (identity) | Reference table |
| 7 | VMS_Feedback | Id (identity) | Empty initially |
| 8 | VMS_InMilestoneNotInP6 | SchedActNO | Empty initially |
| 9 | VMS_InP6NotInMilestone | SchedActNO | Empty initially |
| 10 | VMS_Schedule | (SchedActNO, WeekEndDate) | Empty initially |
| 11 | VMS_ProgressSnapshots | (UniqueID, WeekEndDate) | Empty initially |
| 12 | VMS_Activities | ActivityID (identity) | Empty initially, has trigger |

**Verification Query:**
```sql
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'VMS_%'
ORDER BY TABLE_NAME;
-- Should return 12 rows
```

---

## Phase 2: Data Migration

Migrate data from personal Azure to company Azure for 5 tables.

**Migration approach:** SELECT from personal Azure, INSERT into company Azure via scripts.

### 2.1 VMS_Users
```sql
-- Run against company Azure after extracting from personal Azure
INSERT INTO VMS_Users (Username, FullName, Email)
VALUES
  -- (data from personal Azure Users table)
;
```

### 2.2 VMS_Projects
```sql
INSERT INTO VMS_Projects (ProjectID, Description, ClientName, ClientStreetAddress, ClientCity, ClientState, ClientZipCode, ProjectStreetAddress, ProjectCity, ProjectState, ProjectZipCode, ProjectManager, SiteManager, OM, SM, EN, PM, Phone, Fax)
VALUES
  -- (data from personal Azure Projects table)
;
```

### 2.3 VMS_Admins
```sql
INSERT INTO VMS_Admins (Username, FullName, DateAdded)
VALUES
  -- (data from personal Azure Admins table)
;
```

### 2.4 VMS_ColumnMappings
```sql
INSERT INTO VMS_ColumnMappings (ColumnName, OldVantageName, AzureName, DataType, IsEditable, IsCalculated, CalcFormula, Notes)
VALUES
  -- (data from personal Azure ColumnMappings table)
;
```

### 2.5 VMS_Managers
```sql
INSERT INTO VMS_Managers (FullName, Position, Company, Email, ProjectsAssigned, IsActive)
VALUES
  -- (data from personal Azure Managers table)
;
```

**Verification:** SELECT COUNT(*) from each table to confirm row counts match personal Azure.

---

## Phase 3: Code Updates

### 3.1 Files to Modify (20 files)

| File | Tables Referenced | Complexity |
|------|-------------------|------------|
| **AzureDbManager.cs** | Admins, Users, Activities, GlobalSyncVersion | High |
| **SyncManager.cs** | Activities, GlobalSyncVersion | High |
| **DatabaseSetup.cs** | Users, Projects, ColumnMappings, Managers, Feedback | Medium |
| **ActivityRepository.cs** | Activities, Users | High |
| **ScheduleRepository.cs** | ProgressSnapshots, Schedule | Medium |
| **ProgressView.xaml.cs** | Activities, ProgressSnapshots, Users | High |
| **DeletedRecordsView.xaml.cs** | Activities | Low |
| **WorkPackageView.xaml.cs** | Projects, Users | Low |
| **MainWindow.xaml.cs** | Users, Admins, Activities, Schedule | High |
| **AdminUsersDialog.xaml.cs** | Users | Low |
| **AdminProjectsDialog.xaml.cs** | Projects | Low |
| **FeedbackDialog.xaml.cs** | Feedback | Low |
| **AdminSnapshotsDialog.xaml.cs** | ProgressSnapshots | Low |
| **ManageSnapshotsDialog.xaml.cs** | ProgressSnapshots | Low |
| **ColumnMapper.cs** | ColumnMappings | Low |
| **ProjectCache.cs** | Projects | Low |
| **ScheduleExcelImporter.cs** | Schedule | Medium |
| **TokenResolver.cs** | Users | Low |
| **ScheduleDiagnostic.cs** | ProgressSnapshots, Schedule | Low |
| **App.xaml.cs** | Users | Low |

### 3.2 Table Name Mappings

| Original | VMS Prefix |
|----------|------------|
| `Activities` | `VMS_Activities` |
| `Admins` | `VMS_Admins` |
| `ColumnMappings` | `VMS_ColumnMappings` |
| `Feedback` | `VMS_Feedback` |
| `GlobalSyncVersion` | `VMS_GlobalSyncVersion` |
| `InMilestoneNotInP6` | `VMS_InMilestoneNotInP6` |
| `InP6NotInMilestone` | `VMS_InP6NotInMilestone` |
| `Managers` | `VMS_Managers` |
| `ProgressSnapshots` | `VMS_ProgressSnapshots` |
| `Projects` | `VMS_Projects` |
| `Schedule` | `VMS_Schedule` |
| `Users` | `VMS_Users` |

### 3.3 Trigger Reference Update

In SyncManager.cs, update trigger name:
- `TR_Activities_SyncVersion` -> `TR_VMS_Activities_SyncVersion`

### 3.4 Credentials.cs Simplification

**Before:**
```csharp
public const bool UseCompanyAzure = false;

// Personal Azure
public const string AzurePersonalServer = "...";
public const string AzurePersonalDatabase = "...";
// ... more personal credentials

// Company Azure
public const string AzureCompanyServer = "summitpc.database.windows.net";
// ... more company credentials

// Dynamic helpers
public static string AzureServer => UseCompanyAzure ? AzureCompanyServer : AzurePersonalServer;
// ... more helpers
```

**After:**
```csharp
// Company Azure (Summit Industrial)
public const string AzureServer = "summitpc.database.windows.net";
public const string AzureDatabase = "projectcontrols";
public const string AzureUserId = "csurles";
public const string AzurePassword = "..."; // existing password
```

Remove:
- `UseCompanyAzure` toggle
- All `AzurePersonal*` constants
- All `AzureCompany*` constants
- All dynamic helper properties (just use constants directly)

---

## Phase 4: Testing Checklist

### 4.1 Connection Tests
- [ ] App starts and connects to company Azure
- [ ] Splash screen shows "Syncing Reference Tables..." successfully
- [ ] No connection errors in log

### 4.2 Reference Table Tests
- [ ] Users list populates in Admin > Manage Users dialog
- [ ] Projects list populates in Admin > Manage Projects dialog
- [ ] Column mappings load correctly (check Import/Export functionality)
- [ ] Managers data accessible (Work Package module)
- [ ] Feedback board shows/accepts new items

### 4.3 Core Functionality Tests
- [ ] Sync (Push) works - create activity, mark dirty, sync
- [ ] Sync (Pull) works - verify GlobalSyncVersion increments
- [ ] Admin toggle works (add/remove admin)
- [ ] Activities CRUD operations work
- [ ] Progress snapshots can be saved

### 4.4 Schedule Module Tests
- [ ] P6 Import works
- [ ] Schedule data displays
- [ ] Snapshot comparisons work

---

## Phase 5: Cleanup

After successful testing:
1. Delete `Plans/MILESTONE_Azure_Migration_Plan.md` (superseded by this plan)
2. Update `Plans/Project_Status.md`
3. Update `Plans/Completed_Work.md`
4. Merge `azure-company-migration` branch to main
5. Optionally: Delete personal Azure database tables (not required)

---

## Rollback Strategy

If migration fails:
1. Switch back to main branch
2. Personal Azure continues working
3. Investigate and fix issues on migration branch
4. Re-attempt migration

---

## Execution Order

1. **Phase 1** - Create tables (manual SQL in VS)
2. **Phase 2** - Migrate data (manual SQL in VS)
3. **Phase 3** - Code updates (Claude Code on migration branch)
4. **Phase 4** - Testing (user in Visual Studio)
5. **Phase 5** - Cleanup and merge

---

## Notes

- Personal Azure remains untouched during migration - safe rollback
- No production users - no downtime concerns
- Local SQLite db can be deleted and re-synced after migration
- VMS_ prefix chosen to distinguish from existing legacy OldVantage tables on company server
