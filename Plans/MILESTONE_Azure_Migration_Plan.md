# MILESTONE Azure Migration: Personal to Company

**Date:** January 16, 2026  
**Status:** Planning Complete, Ready for Execution

---

## Summary

This document details the plan to migrate MILESTONE's Azure SQL database from Steve's personal Azure subscription to the company Azure server (Summit Industrial). This migration will enable employee testing and eventual production deployment.

---

## Completed Work

### 1. Company Azure Connection Verified

Successfully connected to the company Azure SQL server using Visual Studio's SQL query tool.

**Connection Details:**
- Server: `summitpc.database.windows.net`
- Database: `projectcontrols`
- Login: `csurles`

### 2. Permission Test Passed

Verified CREATE TABLE permissions by successfully creating and dropping a test table:

```sql
CREATE TABLE dbo.TestTable (
    Id INT PRIMARY KEY,
    Name NVARCHAR(50)
)
-- Success

DROP TABLE dbo.TestTable
-- Success
```

### 3. Credentials File Updated

Updated `Credentials.cs` to support both personal (development) and company (production) Azure connections with a toggle flag:

```csharp
// Set to true to use company Azure, false for personal development
public const bool UseCompanyAzure = false;

// Helper properties dynamically return active credentials
public static string AzureServer => UseCompanyAzure ? AzureCompanyServer : AzurePersonalServer;
public static string AzureDatabase => UseCompanyAzure ? AzureCompanyDatabase : AzurePersonalDatabase;
public static string AzureUserId => UseCompanyAzure ? AzureCompanyUserId : AzurePersonalUserId;
public static string AzurePassword => UseCompanyAzure ? AzureCompanyPassword : AzurePersonalPassword;
```

### 4. Schema Export Completed

Exported complete schema from personal Azure (12 tables) to JSON for analysis.

---

## Migration Plan

### Table Naming Convention

All MILESTONE tables will use the `VMS_` prefix to differentiate from existing legacy OldVantage tables on the company server.

| Personal Azure | Company Azure |
|----------------|---------------|
| Activities | VMS_Activities |
| Admins | VMS_Admins |
| ColumnMappings | VMS_ColumnMappings |
| Feedback | VMS_Feedback |
| GlobalSyncVersion | VMS_GlobalSyncVersion |
| InMilestoneNotInP6 | VMS_InMilestoneNotInP6 |
| InP6NotInMilestone | VMS_InP6NotInMilestone |
| Managers | VMS_Managers |
| ProgressSnapshots | VMS_ProgressSnapshots |
| Projects | VMS_Projects |
| Schedule | VMS_Schedule |
| Users | VMS_Users |

**Trigger:** `TR_VMS_Activities_SyncVersion`

---

## Execution Steps

### Phase 1: Create Tables on Company Azure

1. Open Visual Studio
2. Connect to company Azure (`summitpc.database.windows.net` / `projectcontrols`)
3. Open new query window
4. Paste and execute the SQL script below
5. Verify all 12 tables created with verification query

### Phase 2: Update C# Code (DEFERRED)

> **Status:** Deferred until ready to switch to company Azure. Personal Azure continues working with current table names.

After tables are created, update all Azure table references in the codebase:

**Files to modify:**
- `AzureDbManager.cs` - Connection checks, admin lookups
- `SyncManager.cs` - Push/pull operations, temp tables
- `DatabaseSetup.cs` - MirrorTablesFromAzure table name array
- `ProgressView.xaml.cs` - Delete operations

**Change pattern:**
- `Activities` → `VMS_Activities`
- `Users` → `VMS_Users`
- `Projects` → `VMS_Projects`
- etc.

### Phase 3: Test with Company Azure (DEFERRED)

> **Status:** Deferred until Phase 2 is complete.

1. Set `UseCompanyAzure = true` in Credentials.cs
2. Build and run application
3. Test connection on startup
4. Test sync operations
5. Verify data integrity

### Phase 4: Data Migration (Optional)

If existing test data needs to be migrated from personal Azure to company Azure, a separate data migration script will be created.

---

## Rollback Strategy

If issues occur after switching to company Azure:

1. Set `UseCompanyAzure = false` in Credentials.cs
2. Rebuild and redeploy
3. Users continue working against personal Azure while issues are resolved

---

## Future Feature: Legacy Azure Table Save

Admin feature to upload weekly progress snapshots to the company's existing legacy table for reporting/integration with other systems.

**Target Table:** `dbo_VANTAGE_global_ProgressLog` (existing legacy table on company Azure)

**Feature Requirements:**
- Admin dialog to select snapshots by Project and WeekEndDate
- Allows incremental uploads as users complete their weekly snapshots
- UPSERT (MERGE) behavior - allows re-uploads and corrections
- Schema mapping TBD when feature is implemented (legacy table has existing format)

**Use Case:**
Admin monitors which projects have completed their weekly snapshots, then uploads them to the legacy table either individually or in batches. By week's end, all projects' snapshots for the current WeekEndDate are saved to the legacy system.

---

## SQL Script: Create VMS_ Tables

Save this as `VMS_Azure_Tables_Create.sql` and execute against company Azure:

```sql
-- ============================================================================
-- MILESTONE (VMS) Azure SQL Database Schema
-- Run this script against the company Azure database (projectcontrols)
-- ============================================================================

-- ============================================================================
-- TABLE 1: VMS_GlobalSyncVersion
-- ============================================================================
CREATE TABLE VMS_GlobalSyncVersion (
    Id INT NOT NULL DEFAULT 1,
    CurrentVersion BIGINT NOT NULL DEFAULT 0
);
GO

INSERT INTO VMS_GlobalSyncVersion (Id, CurrentVersion) VALUES (1, 0);
GO

-- ============================================================================
-- TABLE 2: VMS_Users
-- ============================================================================
CREATE TABLE VMS_Users (
    UserID INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(255) NULL,
    Email NVARCHAR(255) NULL
);
GO

-- ============================================================================
-- TABLE 3: VMS_Admins
-- ============================================================================
CREATE TABLE VMS_Admins (
    AdminID INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(255) NOT NULL,
    DateAdded DATETIME NULL DEFAULT GETUTCDATE()
);
GO

-- ============================================================================
-- TABLE 4: VMS_Projects
-- ============================================================================
CREATE TABLE VMS_Projects (
    ProjectID NVARCHAR(50) NOT NULL PRIMARY KEY,
    Description NVARCHAR(MAX) NOT NULL DEFAULT N'',
    ClientName NVARCHAR(255) NOT NULL DEFAULT N'',
    ClientStreetAddress NVARCHAR(255) NOT NULL DEFAULT N'',
    ClientCity NVARCHAR(100) NOT NULL DEFAULT N'',
    ClientState NVARCHAR(50) NOT NULL DEFAULT N'',
    ClientZipCode NVARCHAR(50) NOT NULL DEFAULT N'',
    ProjectStreetAddress NVARCHAR(255) NOT NULL DEFAULT N'',
    ProjectCity NVARCHAR(100) NOT NULL DEFAULT N'',
    ProjectState NVARCHAR(50) NOT NULL DEFAULT N'',
    ProjectZipCode NVARCHAR(50) NOT NULL DEFAULT N'',
    ProjectManager NVARCHAR(255) NOT NULL DEFAULT N'Unknown',
    SiteManager NVARCHAR(255) NOT NULL DEFAULT N'Unknown',
    OM BIT NOT NULL DEFAULT 1,
    SM BIT NOT NULL DEFAULT 1,
    EN BIT NOT NULL DEFAULT 1,
    PM BIT NOT NULL DEFAULT 1,
    Phone NVARCHAR(50) NOT NULL DEFAULT '',
    Fax NVARCHAR(50) NOT NULL DEFAULT ''
);
GO

-- ============================================================================
-- TABLE 5: VMS_Managers
-- ============================================================================
CREATE TABLE VMS_Managers (
    ManagerID INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(255) NOT NULL,
    Position NVARCHAR(100) NOT NULL DEFAULT N'Unknown',
    Company NVARCHAR(100) NOT NULL DEFAULT N'Summit',
    Email NVARCHAR(255) NOT NULL DEFAULT N'',
    ProjectsAssigned NVARCHAR(MAX) NULL DEFAULT N'[]',
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- ============================================================================
-- TABLE 6: VMS_ColumnMappings
-- ============================================================================
CREATE TABLE VMS_ColumnMappings (
    MappingID INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ColumnName NVARCHAR(255) NOT NULL,
    OldVantageName NVARCHAR(255) NULL,
    AzureName NVARCHAR(255) NULL,
    DataType NVARCHAR(50) NULL,
    IsEditable BIT NULL DEFAULT 1,
    IsCalculated BIT NULL DEFAULT 0,
    CalcFormula NVARCHAR(MAX) NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

-- ============================================================================
-- TABLE 7: VMS_Feedback
-- ============================================================================
CREATE TABLE VMS_Feedback (
    Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Type NVARCHAR(20) NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'New',
    CreatedBy NVARCHAR(100) NOT NULL,
    CreatedUtcDate DATETIME2 NOT NULL,
    UpdatedBy NVARCHAR(100) NULL,
    UpdatedUtcDate DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);
GO

-- ============================================================================
-- TABLE 8: VMS_InMilestoneNotInP6
-- ============================================================================
CREATE TABLE VMS_InMilestoneNotInP6 (
    SchedActNO NVARCHAR(50) NOT NULL PRIMARY KEY,
    Placeholder BIT NULL DEFAULT 1
);
GO

-- ============================================================================
-- TABLE 9: VMS_InP6NotInMilestone
-- ============================================================================
CREATE TABLE VMS_InP6NotInMilestone (
    SchedActNO NVARCHAR(50) NOT NULL PRIMARY KEY,
    Placeholder BIT NULL DEFAULT 1
);
GO

-- ============================================================================
-- TABLE 10: VMS_Schedule
-- ============================================================================
CREATE TABLE VMS_Schedule (
    SchedActNO NVARCHAR(255) NOT NULL,
    WeekEndDate NVARCHAR(50) NOT NULL,
    ProjectID NVARCHAR(255) NOT NULL DEFAULT '',
    WbsId NVARCHAR(255) NOT NULL DEFAULT '',
    Description NVARCHAR(MAX) NOT NULL DEFAULT '',
    P6_PlannedStart NVARCHAR(50) NULL,
    P6_PlannedFinish NVARCHAR(50) NULL,
    P6_ActualStart NVARCHAR(50) NULL,
    P6_ActualFinish NVARCHAR(50) NULL,
    P6_PercentComplete FLOAT NOT NULL DEFAULT 0,
    P6_BudgetMHs FLOAT NOT NULL DEFAULT 0,
    ThreeWeekStart NVARCHAR(50) NULL,
    ThreeWeekFinish NVARCHAR(50) NULL,
    MissedStartReason NVARCHAR(500) NULL,
    MissedFinishReason NVARCHAR(500) NULL,
    UpdatedBy NVARCHAR(255) NOT NULL DEFAULT '',
    UpdatedUtcDate NVARCHAR(50) NOT NULL,
    PRIMARY KEY (SchedActNO, WeekEndDate)
);
GO

-- ============================================================================
-- TABLE 11: VMS_ProgressSnapshots
-- ============================================================================
CREATE TABLE VMS_ProgressSnapshots (
    UniqueID NVARCHAR(100) NOT NULL,
    WeekEndDate NVARCHAR(50) NOT NULL,
    Area NVARCHAR(255) NOT NULL DEFAULT '',
    AssignedTo NVARCHAR(255) NOT NULL DEFAULT '',
    AzureUploadUtcDate NVARCHAR(50) NULL,
    Aux1 NVARCHAR(255) NOT NULL DEFAULT '',
    Aux2 NVARCHAR(255) NOT NULL DEFAULT '',
    Aux3 NVARCHAR(255) NOT NULL DEFAULT '',
    BaseUnit FLOAT NOT NULL DEFAULT 0,
    BudgetHoursGroup FLOAT NOT NULL DEFAULT 0,
    BudgetHoursROC FLOAT NOT NULL DEFAULT 0,
    BudgetMHs FLOAT NOT NULL DEFAULT 0,
    ChgOrdNO NVARCHAR(255) NOT NULL DEFAULT '',
    ClientBudget FLOAT NOT NULL DEFAULT 0,
    ClientCustom3 FLOAT NOT NULL DEFAULT 0,
    ClientEquivQty FLOAT NOT NULL DEFAULT 0,
    CompType NVARCHAR(255) NOT NULL DEFAULT '',
    CreatedBy NVARCHAR(255) NOT NULL DEFAULT '',
    DateTrigger INT NOT NULL DEFAULT 0,
    Description NVARCHAR(MAX) NOT NULL DEFAULT '',
    DwgNO NVARCHAR(255) NOT NULL DEFAULT '',
    EarnQtyEntry FLOAT NOT NULL DEFAULT 0,
    EarnedMHsRoc FLOAT NOT NULL DEFAULT 0,
    EqmtNO NVARCHAR(255) NOT NULL DEFAULT '',
    EquivQTY NVARCHAR(255) NOT NULL DEFAULT '',
    EquivUOM NVARCHAR(255) NOT NULL DEFAULT '',
    Estimator NVARCHAR(255) NOT NULL DEFAULT '',
    HexNO INT NOT NULL DEFAULT 0,
    HtTrace NVARCHAR(255) NOT NULL DEFAULT '',
    InsulType NVARCHAR(255) NOT NULL DEFAULT '',
    LineNumber NVARCHAR(255) NOT NULL DEFAULT '',
    MtrlSpec NVARCHAR(255) NOT NULL DEFAULT '',
    Notes NVARCHAR(MAX) NOT NULL DEFAULT '',
    PaintCode NVARCHAR(255) NOT NULL DEFAULT '',
    PercentEntry FLOAT NOT NULL DEFAULT 0,
    PhaseCategory NVARCHAR(255) NOT NULL DEFAULT '',
    PhaseCode NVARCHAR(255) NOT NULL DEFAULT '',
    PipeGrade NVARCHAR(255) NOT NULL DEFAULT '',
    PipeSize1 FLOAT NOT NULL DEFAULT 0,
    PipeSize2 FLOAT NOT NULL DEFAULT 0,
    PrevEarnMHs FLOAT NOT NULL DEFAULT 0,
    PrevEarnQTY FLOAT NOT NULL DEFAULT 0,
    ProgDate NVARCHAR(50) NULL,
    ProjectID NVARCHAR(255) NOT NULL DEFAULT '',
    Quantity FLOAT NOT NULL DEFAULT 0,
    RevNO NVARCHAR(255) NOT NULL DEFAULT '',
    RFINO NVARCHAR(255) NOT NULL DEFAULT '',
    ROCBudgetQTY FLOAT NOT NULL DEFAULT 0,
    ROCID FLOAT NOT NULL DEFAULT 0,
    ROCPercent FLOAT NOT NULL DEFAULT 0,
    ROCStep NVARCHAR(255) NOT NULL DEFAULT '',
    SchedActNO NVARCHAR(255) NOT NULL DEFAULT '',
    SchFinish NVARCHAR(50) NULL,
    SchStart NVARCHAR(50) NULL,
    SecondActno NVARCHAR(255) NOT NULL DEFAULT '',
    SecondDwgNO NVARCHAR(255) NOT NULL DEFAULT '',
    Service NVARCHAR(255) NOT NULL DEFAULT '',
    ShopField NVARCHAR(255) NOT NULL DEFAULT '',
    ShtNO NVARCHAR(255) NOT NULL DEFAULT '',
    SubArea NVARCHAR(255) NOT NULL DEFAULT '',
    PjtSystem NVARCHAR(255) NOT NULL DEFAULT '',
    SystemNO NVARCHAR(255) NOT NULL DEFAULT '',
    TagNO NVARCHAR(255) NOT NULL DEFAULT '',
    UDF1 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF2 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF3 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF4 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF5 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF6 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF7 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF8 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF9 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF10 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF11 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF12 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF13 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF14 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF15 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF16 NVARCHAR(255) NOT NULL DEFAULT '',
    UDF17 NVARCHAR(255) NOT NULL DEFAULT '',
    RespParty NVARCHAR(255) NOT NULL DEFAULT '',
    UDF20 NVARCHAR(255) NOT NULL DEFAULT '',
    UpdatedBy NVARCHAR(255) NOT NULL DEFAULT '',
    UpdatedUtcDate NVARCHAR(50) NOT NULL,
    UOM NVARCHAR(255) NOT NULL DEFAULT '',
    WorkPackage NVARCHAR(255) NOT NULL DEFAULT '',
    XRay FLOAT NOT NULL DEFAULT 0,
    ExportedBy NVARCHAR(255) NULL,
    ExportedDate DATETIME NULL,
    PRIMARY KEY (UniqueID, WeekEndDate)
);
GO

-- ============================================================================
-- TABLE 12: VMS_Activities (Main table - largest)
-- ============================================================================
CREATE TABLE VMS_Activities (
    ActivityID INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UniqueID NVARCHAR(255) NOT NULL UNIQUE,
    IsDeleted BIT NOT NULL DEFAULT 0,
    SyncVersion BIGINT NOT NULL DEFAULT 0,
    Area NVARCHAR(50) NOT NULL DEFAULT N'',
    AssignedTo NVARCHAR(255) NOT NULL DEFAULT N'',
    Aux1 NVARCHAR(50) NOT NULL DEFAULT N'',
    Aux2 NVARCHAR(50) NOT NULL DEFAULT N'',
    Aux3 NVARCHAR(50) NOT NULL DEFAULT N'',
    AzureUploadUtcDate NVARCHAR(50) NULL,
    BaseUnit FLOAT NOT NULL DEFAULT 0,
    BudgetHoursGroup FLOAT NOT NULL DEFAULT 0,
    BudgetHoursROC FLOAT NOT NULL DEFAULT 0,
    BudgetMHs FLOAT NOT NULL DEFAULT 0,
    ChgOrdNO NVARCHAR(50) NOT NULL DEFAULT N'',
    ClientBudget FLOAT NOT NULL DEFAULT 0,
    ClientCustom3 FLOAT NOT NULL DEFAULT 0,
    ClientEquivQty FLOAT NOT NULL DEFAULT 0,
    CompType NVARCHAR(50) NOT NULL DEFAULT N'',
    CreatedBy NVARCHAR(255) NOT NULL DEFAULT N'',
    DateTrigger INT NOT NULL DEFAULT 0,
    Description NVARCHAR(MAX) NOT NULL DEFAULT N'',
    DwgNO NVARCHAR(255) NOT NULL DEFAULT N'',
    EarnQtyEntry FLOAT NOT NULL DEFAULT 0,
    EarnedMHsRoc FLOAT NOT NULL DEFAULT 0,
    EqmtNO NVARCHAR(50) NOT NULL DEFAULT N'',
    EquivQTY NVARCHAR(50) NOT NULL DEFAULT N'',
    EquivUOM NVARCHAR(50) NOT NULL DEFAULT N'',
    Estimator NVARCHAR(255) NOT NULL DEFAULT N'',
    HexNO INT NOT NULL DEFAULT 0,
    HtTrace NVARCHAR(50) NOT NULL DEFAULT N'',
    InsulType NVARCHAR(50) NOT NULL DEFAULT N'',
    LineNumber NVARCHAR(255) NOT NULL DEFAULT N'',
    MtrlSpec NVARCHAR(255) NOT NULL DEFAULT N'',
    Notes NVARCHAR(MAX) NOT NULL DEFAULT N'',
    PaintCode NVARCHAR(50) NOT NULL DEFAULT N'',
    PercentEntry FLOAT NOT NULL DEFAULT 0,
    PhaseCategory NVARCHAR(50) NOT NULL DEFAULT N'',
    PhaseCode NVARCHAR(50) NOT NULL DEFAULT N'',
    PipeGrade NVARCHAR(255) NULL DEFAULT N'',
    PipeSize1 FLOAT NOT NULL DEFAULT 0,
    PipeSize2 FLOAT NOT NULL DEFAULT 0,
    PjtSystem NVARCHAR(50) NOT NULL DEFAULT N'',
    PrevEarnMHs FLOAT NOT NULL DEFAULT 0,
    PrevEarnQTY FLOAT NOT NULL DEFAULT 0,
    ProgDate NVARCHAR(50) NULL,
    ProjectID NVARCHAR(50) NOT NULL DEFAULT N'',
    Quantity FLOAT NOT NULL DEFAULT 0,
    RevNO NVARCHAR(50) NOT NULL DEFAULT N'',
    RFINO NVARCHAR(50) NOT NULL DEFAULT N'',
    ROCBudgetQTY FLOAT NOT NULL DEFAULT 0,
    ROCID FLOAT NOT NULL DEFAULT 0,
    ROCPercent FLOAT NOT NULL DEFAULT 0,
    ROCStep NVARCHAR(50) NOT NULL DEFAULT N'',
    SchedActNO NVARCHAR(50) NOT NULL DEFAULT N'',
    SchFinish NVARCHAR(50) NULL,
    SchStart NVARCHAR(50) NULL,
    SecondActno NVARCHAR(50) NOT NULL DEFAULT N'',
    SecondDwgNO NVARCHAR(255) NOT NULL DEFAULT N'',
    Service NVARCHAR(50) NOT NULL DEFAULT N'',
    ShopField NVARCHAR(50) NOT NULL DEFAULT N'',
    ShtNO NVARCHAR(50) NOT NULL DEFAULT N'',
    SubArea NVARCHAR(50) NOT NULL DEFAULT N'',
    SystemNO NVARCHAR(50) NOT NULL DEFAULT N'',
    TagNO NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF1 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF2 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF3 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF4 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF5 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF6 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF7 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF8 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF9 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF10 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF11 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF12 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF13 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF14 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF15 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF16 NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF17 NVARCHAR(255) NOT NULL DEFAULT N'',
    RespParty NVARCHAR(255) NOT NULL DEFAULT N'',
    UDF20 NVARCHAR(255) NOT NULL DEFAULT N'',
    UOM NVARCHAR(50) NOT NULL DEFAULT N'',
    UpdatedBy NVARCHAR(255) NOT NULL DEFAULT N'',
    UpdatedUtcDate NVARCHAR(50) NOT NULL,
    WeekEndDate NVARCHAR(50) NULL,
    WorkPackage NVARCHAR(255) NOT NULL DEFAULT N'',
    XRay FLOAT NOT NULL DEFAULT 0
);
GO

-- ============================================================================
-- TRIGGER: TR_VMS_Activities_SyncVersion
-- Auto-increments SyncVersion on INSERT/UPDATE
-- ============================================================================
CREATE TRIGGER TR_VMS_Activities_SyncVersion
ON VMS_Activities
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get next version(s) from GlobalSyncVersion
    DECLARE @rowCount INT = (SELECT COUNT(*) FROM inserted);
    DECLARE @startVersion BIGINT;
    
    UPDATE VMS_GlobalSyncVersion 
    SET CurrentVersion = CurrentVersion + @rowCount,
        @startVersion = CurrentVersion + 1;
    
    -- Update SyncVersion for affected rows
    ;WITH NumberedRows AS (
        SELECT UniqueID, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS RowNum
        FROM inserted
    )
    UPDATE a
    SET SyncVersion = @startVersion + nr.RowNum
    FROM VMS_Activities a
    INNER JOIN NumberedRows nr ON a.UniqueID = nr.UniqueID;
END;
GO

-- ============================================================================
-- VERIFICATION: List all VMS_ tables created
-- ============================================================================
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE 'VMS_%' 
ORDER BY TABLE_NAME;
GO
```

---

## Notes

- The `dbo.` prefix is the default schema and does not need to be included in table names
- The `VMS_` prefix distinguishes MILESTONE tables from existing OldVantage tables
- Personal Azure remains available for development by setting `UseCompanyAzure = false`
- No data migration is required initially - tables will start empty for fresh employee testing
