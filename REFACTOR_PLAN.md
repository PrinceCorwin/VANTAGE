# VANTAGE Column Name Refactor - Design Document

## Executive Summary
Refactor the VANTAGE application to use a single, consistent set of column names throughout the database, models, and UI. Eliminate the complex translation layer between database columns and display names, moving all mapping logic to import/export operations only.

**Branch**: `ColumnNameRefactor`

---

## CONFIRMED DECISIONS ?

### 1. Column Naming
- **Database & Code**: Use "NewVantage" names from CSV (e.g., `ProjectID`, `PercentEntry`, `AssignedTo`)
- **Excel Import/Export**: Translate to/from "OldVantage" names (e.g., `Tag_ProjectID`, `Val_Perc_Complete`, `UDFEleven`)
- **Azure Upload/Sync**: Translate to "Azure" names per CSV mapping
- **Fixed typo**: `PhaseCategory` (not `PhaseCatagory`)

### 2. PercentEntry Storage Format
**CRITICAL DECISION**: Store as **PERCENTAGE (0-100)** in database and UI

| Context | Value | Example |
|---------|-------|---------|
| **Database** | 0-100 | `75.5` |
| **UI Display** | 0-100 with "%" | `75.5%` |
| **C# Calculations** | Divide by 100 | `PercentEntry / 100 * BudgetMHs` |
| **Old Excel Import** | Multiply by 100 | `0.755` ? `75.5` |
| **Old Excel Export** | Divide by 100 | `75.5` ? `0.755` |
| **Azure Upload** | Divide by 100 | `75.5` ? `0.755` |

**Rationale**: Simpler for users - what they see (75%) is what's stored. Only convert when crossing system boundaries.

### 3. Calculated Columns
**DO NOT STORE** in database - Calculate in C# properties:

| Column | Formula | Notes |
|--------|---------|-------|
| `Status` | Based on PercentEntry | "Not Started", "In Progress", "Complete" |
| `EarnMHsCalc` | `PercentEntry / 100 * BudgetMHs` | Earned manhours |
| `EarnedQtyCalc` | `PercentEntry / 100 * Quantity` | Earned quantity |
| `PercentCompleteCalc` | Same as `PercentEntry` | Duplicate for legacy compatibility |
| `ROCLookupID` | Concatenated string | `ProjectID \|\| CompType \|\| ...` |
| `WeekEndDate` | Set on submit | Timestamp |
| `ProgDate` | Set on submit | Timestamp |
| `AzureUploadDate` | Set on upload | Timestamp |

### 4. UniqueID Auto-Generation
When importing records without `UDFNineteen` (UniqueID), generate using:

**Format**: `i{yyMMddHHmmss}{sequence}{username_last3}`

**Example**: `i2510300738271ano`, `i2510300738272ano`, etc.

```csharp
var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
var userSuffix = username.Length >= 3 
    ? username.Substring(username.Length - 3).ToLower() 
    : username.ToLower();
var sequence = 1; // Increment for each row in import batch

foreach (var activity in importedActivities.Where(a => string.IsNullOrEmpty(a.UniqueID)))
{
    activity.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
    sequence++;
}
```

### 5. Null/Blank Handling
- **Text fields**: Store as `""` (empty string)
- **Number fields**: Store as `0`
- **Special**: `AssignedTo` defaults to `"Unassigned"`
- **Required fields**: `UniqueID` must not be null (error on import if missing and can't generate)

### 6. Migration Strategy
**NO MIGRATION** - Fresh start approach:
1. Users export data from OldVantage to Excel
2. Users import Excel into NewVantage (with column name translation)
3. Database created fresh with new schema

---

## Complete Column List from CSV

### Stored in Database (IsEditable=1 or System Fields)

| NewVantage Name | OldVantage Name | Azure Name | Data Type | Notes |
|-----------------|-----------------|------------|-----------|-------|
| ActivityID | (auto-increment) | ActivityID | Integer | Primary key |
| Area | Tag_Area | Tag_Area | Text | |
| AssignedTo | UDFEleven | UDF11 | Text | Default: "Unassigned" |
| Aux1 | Tag_Aux1 | Tag_Aux1 | Text | |
| Aux2 | Tag_Aux2 | Tag_Aux2 | Text | |
| Aux3 | Tag_Aux3 | Tag_Aux3 | Text | |
| BaseUnit | Val_Base_Unit | Val_Base_Unit | Number | |
| BudgetHoursGroup | Val_BudgetedHours_Group | Val_BudgetedHours_Group | Number | |
| BudgetHoursROC | Val_BudgetedHours_ROC | Val_BudgetedHours_ROC | Number | |
| BudgetMHs | Val_BudgetedHours_Ind | Val_BudgetedHours_Ind | Number | |
| ChgOrdNO | Tag_CONo | Tag_CONo | Text | |
| ClientBudget | VAL_UDF_Two | VAL_UDF_Two | Number | |
| ClientCustom3 | VAL_UDF_Three | VAL_UDF_Three | Number | |
| ClientEquivEarnQTY | VAL_Client_Earned_EQ-QTY | VAL_Client_Earned_EQ-QTY | Text | |
| ClientEquivQty | VAL_Client_EQ-QTY_BDG | Val_Client_Eq_Qty_Bdg | Number | |
| CompType | Catg_ComponentType | Catg_ComponentType | Text | |
| CreatedBy | UDFThirteen | UDF13 | Text | |
| DateTrigger | Trg_DateTrigger | (not synced) | Number | |
| Description | Tag_Descriptions | Tag_Descriptions | Text | |
| DwgNO | Dwg_PrimeDrawingNO | Dwg_PrimeDrawingNO | Text | |
| EarnedMHsRoc | Val_EarnedHours_ROC | (not synced) | Number | |
| EarnQtyEntry | Val_EarnedQty | Val_EarnedQty | Number | |
| EqmtNO | Tag_EqmtNo | Tag_EqmtNo | Text | |
| EquivQTY | Val_EQ-QTY | Val_EQ-QTY | Text | |
| EquivUOM | Val_EQ_UOM | Val_EQ_UOM | Text | |
| Estimator | Tag_Estimator | Tag_Estimator | Text | |
| Finish | Sch_Finish | (not synced) | DateTime | Future: calculated |
| HexNO | HexNO | HexNO | Number | |
| HtTrace | Tag_Tracing | Tag_Tracing | Text | |
| InsulType | Tag_Insulation_Typ | Tag_Insulation_Typ | Text | |
| LastModifiedBy | UDFTwelve | UDF12 | Text | |
| LineNO | Tag_LineNo | Tag_LineNo | Text | |
| MtrlSpec | Tag_Matl_Spec | Tag_Matl_Spec | Text | |
| Notes | Notes_Comments | Notes_Comments | LongText | |
| PaintCode | Tag_Paint_Code | Tag_Paint_Code | Text | |
| PercentEntry | Val_Perc_Complete | Val_Perc_Complete | Number | **0-100 format** |
| PhaseCategory | Catg_PhaseCategory | Catg_PhaseCategory | Text | Fixed typo |
| PhaseCode | Tag_Phase Code | Tag_PhaseCode | Text | |
| PipeGrade | Tag_Pipe_Grade | Tag_Pipe_Grade | Text | |
| PipeSize1 | Val_Pipe_Size1 | Val_Pipe_Size1 | Number | |
| PipeSize2 | Val_Pipe_Size2 | Val_Pipe_Size2 | Number | |
| PrevEarnMHs | Val_Prev_Earned_Hours | (not synced) | Number | |
| PrevEarnQTY | Val_Prev_Earned_Qty | (not synced) | Number | |
| ProjectID | Tag_ProjectID | Tag_ProjectID | Text | |
| Quantity | Val_Quantity | Val_Quantity | Number | |
| RevNO | Dwg_RevisionNo | Dwg_RevisionNo | Text | |
| RFINO | Tag_RFINo | Tag_RFINo | Text | |
| ROCBudgetQTY | Val_ROC_BudgetQty | Val_ROC_BudgetQty | Number | Must equal Quantity on export |
| ROCID | Tag_ROC_ID | (not synced) | Number | |
| ROCPercent | Val_ROC_Perc | (not synced) | Number | |
| ROCStep | Catg_ROC_Step | Catg_ROC_Step | Text | |
| SchedActNO | Tag_Sch_ActNo | Tag_Sch_ActNo | Text | |
| SecondActno | Sch_Actno | Sch_Actno | Text | |
| SecondDwgNO | Dwg_SecondaryDrawingNO | Dwg_SecondaryDrawingNO | Text | |
| Service | Tag_Service | Tag_Service | Text | |
| ShopField | Tag_ShopField | Tag_ShopField | Text | |
| ShtNO | Dwg_ShtNo | Dwg_ShtNo | Text | |
| Start | Sch_Start | Sch_Start | DateTime | Future: calculated |
| SubArea | Tag_SubArea | Tag_SubArea | Text | |
| System | Tag_System | Tag_System | Text | |
| SystemNO | Tag_SystemNo | (not synced) | Text | |
| TagNO | Tag_TagNo | Tag_TagNo | Text | |
| UDF1-UDF20 | UDFOne-UDFTwenty | UDF1-UDF20 | Text | (except 11,12,13,19) |
| UniqueID | UDFNineteen | UDF19 | Text | Auto-generate if null |
| UOM | Val_UOM | Val_UOM | Text | |
| WorkPackage | Tag_WorkPackage | Tag_WorkPackage | Text | |
| XRay | Tag_XRAY | Tag_XRAY | Number | |

### NOT Stored - Calculated Properties Only

| NewVantage Name | Formula | Notes |
|-----------------|---------|-------|
| Status | `PercentEntry == 0 ? "Not Started" : PercentEntry >= 100 ? "Complete" : "In Progress"` | |
| EarnMHsCalc | `PercentEntry / 100 * BudgetMHs` | |
| EarnedQtyCalc | `PercentEntry / 100 * Quantity` | |
| PercentCompleteCalc | `PercentEntry` | Legacy alias |
| ROCLookupID | `$"{ProjectID}\|{CompType}\|{PhaseCategory}\|{ROCStep}"` | |
| WeekEndDate | Set on submit to local DB | |
| ProgDate | Set on submit to local DB | |
| AzureUploadDate | Set on Azure upload | |
| UserID | Created on Azure upload (username) | |

---

## Current State Analysis

### Problems
1. **Triple naming convention**: Database columns (e.g., `Tag_ProjectID`), Model properties (e.g., `ProjectID`), Display names
2. **Complex translation layer**: `ColumnMapper` constantly translates between naming schemes
3. **Filter bugs**: Filter popups don't work correctly due to name translation failures
4. **Maintenance burden**: Every feature touching data requires mapping logic
5. **Error-prone**: Easy to use wrong column name in queries or bindings

### Current Column Flow
```
Database (Tag_ProjectID) 
    ? [ColumnMapper.GetPropertyName]
Model (ProjectID) 
    ? [DataGrid Binding]
Display ("Project ID")
    ? [ColumnMapper.GetDbColumnName] 
Database Query
```

---

## Proposed Architecture

### New Column Flow
```
Database (ProjectID) 
    ? [Direct Property]
Model (ProjectID) 
    ? [Direct Binding]
Display ("ProjectID" or "Project ID")
 
Import/Export ONLY:
Excel/Azure ? [Mapping] ? Database (ProjectID)
Database (ProjectID) ? [Mapping] ? Excel/Azure
```

### Single Source of Truth: ColumnMappings Table
```sql
CREATE TABLE ColumnMappings (
    MappingID INTEGER PRIMARY KEY AUTOINCREMENT,
    ColumnName TEXT,           -- NewVantage name (e.g., "ProjectID")
    OldVantageName TEXT,     -- For Excel import/export (e.g., "Tag_ProjectID")
AzureName TEXT,            -- For Azure sync (e.g., "Tag_ProjectID")
    DataType TEXT,
    IsEditable INTEGER DEFAULT 1,
    IsCalculated INTEGER DEFAULT 0,
    CalcFormula TEXT,          -- Formula for calculated fields
    Notes TEXT
);
```

**Seed from CSV**: Use `Documents\ColumnNameComparisonForAiModel.csv` to populate this table.

---

## Phase-by-Phase Implementation Plan

### **PHASE 0: Preparation & Branch Setup** ?
**Goal**: Set up for the refactor without breaking current functionality

**Tasks**:
1. ? Create new Git branch: `ColumnNameRefactor`
2. ? Document current state (REFACTOR_PLAN.md)
3. ? Confirm all decisions with user
4. Export current ColumnMappings table (if exists)
5. Backup current Activity model and ColumnMapper

**Deliverable**: Branch created, plan documented, ready to code

**Action**: Create branch manually in Visual Studio: 
- Team Explorer ? Branches ? New Branch ? Name: `ColumnNameRefactor`

---

### **PHASE 1: Database Schema Creation** ??
**Goal**: Create new Activities table with NewVantage column names

#### Step 1.1: Generate CREATE TABLE SQL

Based on CSV, create Activities table with NewVantage names:

```sql
CREATE TABLE Activities (
    ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- Core Fields
    HexNO INTEGER DEFAULT 0,
    ProjectID TEXT,
    Description TEXT,
    UniqueID TEXT UNIQUE NOT NULL,
    
    -- Area/Location
    Area TEXT,
    SubArea TEXT,
  System TEXT,
    SystemNO TEXT,
    
    -- Categories
    CompType TEXT,
    PhaseCategory TEXT,    -- Fixed typo from PhaseCatagory
    ROCStep TEXT,
    
    -- Assignments
    AssignedTo TEXT DEFAULT 'Unassigned',
    CreatedBy TEXT,
    LastModifiedBy TEXT,
    
    -- Progress (STORED AS 0-100 PERCENTAGE)
    PercentEntry REAL DEFAULT 0,     -- 0-100 format
    Quantity REAL DEFAULT 0,
    EarnQtyEntry REAL DEFAULT 0,
  UOM TEXT,
    
    -- Budgets & Hours
    BudgetMHs REAL DEFAULT 0,
    BudgetHoursGroup REAL DEFAULT 0,
    BudgetHoursROC REAL DEFAULT 0,
    BaseUnit REAL DEFAULT 0,
    EarnedMHsRoc INTEGER DEFAULT 0,
    
    -- ROC
    ROCID INTEGER DEFAULT 0,
    ROCPercent REAL DEFAULT 0,
    ROCBudgetQTY REAL DEFAULT 0,
    
    -- Drawings
    DwgNO TEXT,
  RevNO TEXT,
    SecondDwgNO TEXT,
    ShtNO TEXT,
    
    -- Tags/References
    TagNO TEXT,
    WorkPackage TEXT,
    PhaseCode TEXT,
    Service TEXT,
    ShopField TEXT,
    SchedActNO TEXT,
 SecondActno TEXT,
    
    -- Equipment/Line
    EqmtNO TEXT,
    LineNO TEXT,
    ChgOrdNO TEXT,
    
  -- Materials
    MtrlSpec TEXT,
    PipeGrade TEXT,
    PaintCode TEXT,
    InsulType TEXT,
    HtTrace TEXT,
    
    -- Pipe
    PipeSize1 REAL DEFAULT 0,
    PipeSize2 REAL DEFAULT 0,
    
    -- Auxiliary
    Aux1 TEXT,
    Aux2 TEXT,
    Aux3 TEXT,
    Estimator TEXT,
    RFINO TEXT,
    XRay REAL DEFAULT 0,
    
    -- Equipment Quantities
    EquivQTY TEXT,
    EquivUOM TEXT,
    
    -- Client Fields
    ClientEquivQty REAL DEFAULT 0,
    ClientBudget REAL DEFAULT 0,
    ClientCustom3 REAL DEFAULT 0,
    ClientEquivEarnQTY TEXT,
    
    -- Previous/History
    PrevEarnMHs REAL DEFAULT 0,
    PrevEarnQTY REAL DEFAULT 0,
    
    -- Schedule
    Start TEXT,
  Finish TEXT,
    
    -- Trigger
    DateTrigger INTEGER,
    
    -- Notes
    Notes TEXT,
    
    -- User-Defined Fields (20 total, excluding special ones)
    UDF1 TEXT,
    UDF2 TEXT,
  UDF3 TEXT,
    UDF4 TEXT,
    UDF5 TEXT,
    UDF6 TEXT,
    UDF7 TEXT,
    UDF8 TEXT,
    UDF9 TEXT,
    UDF10 TEXT,
    -- UDF11 = AssignedTo
    -- UDF12 = LastModifiedBy
    -- UDF13 = CreatedBy
    UDF14 TEXT,
    UDF15 TEXT,
    UDF16 TEXT,
    UDF17 TEXT,
    UDF18 TEXT,
    -- UDF19 = UniqueID
    UDF20 TEXT
);

-- Indexes
CREATE INDEX idx_project ON Activities(ProjectID);
CREATE INDEX idx_area ON Activities(Area);
CREATE INDEX idx_assigned_to ON Activities(AssignedTo);
CREATE INDEX idx_unique_id ON Activities(UniqueID);
CREATE INDEX idx_roc_id ON Activities(ROCID);
```

#### Step 1.2: Seed ColumnMappings Table

```sql
-- Insert all mappings from CSV
INSERT INTO ColumnMappings (ColumnName, OldVantageName, AzureName, DataType, IsEditable, IsCalculated, CalcFormula, Notes)
VALUES
-- (generated from CSV - 90+ rows)
('Area', 'Tag_Area', 'Tag_Area', 'Text', 1, 0, NULL, NULL),
('AssignedTo', 'UDFEleven', 'UDF11', 'Text', 1, 0, NULL, NULL),
-- ... etc
('Status', 'Sch_Status', 'Sch_Status', 'Text', 0, 1, 'PercentEntry == 0 ? "Not Started" : PercentEntry >= 100 ? "Complete" : "In Progress"', 'Calculated from PercentEntry'),
-- ... etc
```

**Deliverable**: Database schema ready, ColumnMappings seeded

**Testing**:
- ? Create new database succeeds
- ? All columns present with correct data types
- ? Indexes created
- ? ColumnMappings populated

---

### **PHASE 2: Activity Model Refactor** ??
**Goal**: Update `Activity` model properties to match NewVantage column names

#### Step 2.1: Update Property Names & Types

```csharp
public class Activity : INotifyPropertyChanged
{
    public int ActivityID { get; set; }
    public int HexNO { get; set; }
    
    // Core
    public string ProjectID { get; set; }
    public string Description { get; set; }
    public string UniqueID { get; set; }
    
    // PercentEntry: STORED AS 0-100
    private double _percentEntry;
    public double PercentEntry  
    {
        get => _percentEntry;
        set
        {
        // Clamp to 0-100 range
            _percentEntry = Math.Max(0, Math.Min(100, value));
            OnPropertyChanged(nameof(PercentEntry));
          
            // Trigger calculated properties
 OnPropertyChanged(nameof(EarnMHsCalc));
   OnPropertyChanged(nameof(EarnedQtyCalc));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(PercentCompleteCalc));
    }
    }
    
    // Display property with % suffix
    public string PercentEntry_Display => $"{PercentEntry:F1}%";
    
    // Calculated properties
    public string Status
    {
        get
        {
            if (PercentEntry == 0) return "Not Started";
        if (PercentEntry >= 100) return "Complete";
            return "In Progress";
        }
    }
    
    public double EarnMHsCalc => (PercentEntry / 100) * BudgetMHs;
    
    public double EarnedQtyCalc => (PercentEntry / 100) * Quantity;
    
    public double PercentCompleteCalc => PercentEntry;  // Alias
    
    public string ROCLookupID => $"{ProjectID}|{CompType}|{PhaseCategory}|{ROCStep}";
    
    // All other properties...
    public string Area { get; set; }
    public string AssignedTo { get; set; } = "Unassigned";
    public double BudgetMHs { get; set; }
    public double Quantity { get; set; }
    // ... 80+ more properties matching NewVantage names
}
```

#### Step 2.2: Add Helper Methods

```csharp
// In Activity class:
public void SetPercentFromDecimal(double decimal Value)
{
    PercentEntry = decimalValue * 100;  // For imports: 0.755 ? 75.5
}

public double GetPercentAsDecimal()
{
    return PercentEntry / 100;  // For exports: 75.5 ? 0.755
}
```

**Deliverable**: Activity model updated, compiles successfully

**Testing**:
- ? Model properties match database columns
- ? Calculated properties work correctly
- ? PercentEntry calculations correct
- ? INotifyPropertyChanged triggers

---

### **PHASE 3: Repository Refactor** ??
**Goal**: Update all SQL queries to use new column names

#### Step 3.1: Update ActivityRepository Queries

```csharp
// OLD:
command.CommandText = @"
    SELECT Tag_ProjectID, Val_Perc_Complete, UDFEleven 
    FROM Activities 
    WHERE Tag_ProjectID = @projectId";

// NEW:
command.CommandText = @"
    SELECT ProjectID, PercentEntry, AssignedTo 
    FROM Activities 
    WHERE ProjectID = @projectId";
```

#### Step 3.2: Update INSERT/UPDATE Statements

```csharp
command.CommandText = @"
    INSERT INTO Activities (
  ProjectID, Description, PercentEntry, AssignedTo, BudgetMHs, Quantity, ...
    ) VALUES (
        @ProjectID, @Description, @PercentEntry, @AssignedTo, @BudgetMHs, @Quantity, ...
    )";

// No more ColumnMapper.GetDbColumnName() needed!
```

#### Step 3.3: Update Calculated Column Queries (for filtering)

```csharp
// Status filter using CASE expression:
string statusCase = @"
    CASE 
        WHEN PercentEntry = 0 THEN 'Not Started'
        WHEN PercentEntry >= 100 THEN 'Complete'
        ELSE 'In Progress'
    END";

// EarnMHsCalc filter:
string earnMHsCalc = "PercentEntry / 100 * BudgetMHs";

// Use in WHERE clauses:
command.CommandText = $@"
    SELECT *
    FROM Activities
  WHERE {statusCase} = @statusFilter
    AND {earnMHsCalc} >= @minEarnedHours";
```

**Deliverable**: All SQL queries updated

**Testing**:
- ? Data loads correctly
- ? Filtering works (including calculated columns)
- ? Sorting works
- ? Updates save correctly
- ? No ColumnMapper.GetDbColumnName() calls in repository

---

### **PHASE 4: Simplify ColumnMapper** ??
**Goal**: Keep ONLY import/export mapping, remove all database mapping

#### Step 4.1: New Simplified ColumnMapper

```csharp
public static class ColumnMapper
{
 private static Dictionary<string, (string OldVantage, string Azure)> _mappings;
    
    static ColumnMapper()
 {
        LoadMappingsFromDatabase();
  }
 
    private static void LoadMappingsFromDatabase()
    {
  _mappings = new Dictionary<string, (string, string)>();
        
        using var connection = DatabaseSetup.GetConnection();
        connection.Open();
        
   var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT ColumnName, OldVantageName, AzureName FROM ColumnMappings";
        
        using var reader = cmd.ExecuteReader();
      while (reader.Read())
 {
         string colName = reader.GetString(0);
            string oldName = reader.IsDBNull(1) ? null : reader.GetString(1);
      string azureName = reader.IsDBNull(2) ? null : reader.GetString(2);
      
            _mappings[colName] = (oldName, azureName);
        }
}
    
    // For Excel export: ProjectID ? Tag_ProjectID
    public static string GetOldVantageName(string newVantageName)
    {
        return _mappings.TryGetValue(newVantageName, out var tuple) 
            ? tuple.OldVantage 
            : newVantageName;
    }
    
    // For Azure upload: ProjectID ? Tag_ProjectID
    public static string GetAzureName(string newVantageName)
    {
        return _mappings.TryGetValue(newVantageName, out var tuple) 
       ? tuple.Azure 
     : newVantageName;
    }

    // For Excel import: Tag_ProjectID ? ProjectID
    public static string GetColumnNameFromOldVantage(string oldVantageName)
    {
        return _mappings.FirstOrDefault(kvp => kvp.Value.OldVantage == oldVantageName).Key 
 ?? oldVantageName;
    }
    
    // For Azure sync: Tag_ProjectID ? ProjectID
    public static string GetColumnNameFromAzure(string azureName)
    {
      return _mappings.FirstOrDefault(kvp => kvp.Value.Azure == azureName).Key 
            ?? azureName;
 }
    
    // REMOVED: GetDbColumnName(), GetPropertyName()
    // Database columns now match property names!
}
```

**Deliverable**: ColumnMapper simplified to 4 methods

**Testing**:
- ? GetOldVantageName("ProjectID") returns "Tag_ProjectID"
- ? GetAzureName("ProjectID") returns "Tag_ProjectID"
- ? GetColumnNameFromOldVantage("Tag_ProjectID") returns "ProjectID"
- ? Mappings load from database correctly

---

### **PHASE 5: ViewModel & View Updates** ??
**Goal**: Remove all ColumnMapper.GetDbColumnName() calls

#### Step 5.1: Update FilterBuilder

```csharp
// OLD:
var dbCol = ColumnMapper.GetDbColumnName(columnName);
var condition = $"{dbCol} LIKE '%{searchText}%'

// NEW:
var condition = $"{columnName} LIKE '%{searchText}%'";
```

#### Step 5.2: Update ProgressViewModel

```csharp
// OLD:
public async Task ApplyFilter(string columnName, string filterType, string filterValue)
{
    var dbColName = ColumnMapper.GetDbColumnName(columnName);
    // ...
}

// NEW:
public async Task ApplyFilter(string columnName, string filterType, string filterValue)
{
    // columnName IS the database column name!
  // ...
}
```

#### Step 5.3: Fix ColumnFilterPopup

```csharp
// Simplified to use column names directly:
public static async Task<List<string>> GetDistinctValuesAsync(
    string columnName, 
    string whereClause = "")
{
    var cmd = connection.CreateCommand();
    cmd.CommandText = $@"
        SELECT DISTINCT {columnName} 
        FROM Activities 
        {whereClause} 
        ORDER BY {columnName}";
 // ...
}
```

**Deliverable**: No more ColumnMapper.GetDbColumnName() in UI layer

**Testing**:
- ? DataGrid displays correctly
- ? Column filters work
- ? Text search works
- ? All filter types work
- ? Paging works
- ? Sorting works

---

### **PHASE 6: Import/Export Implementation** ??
**Goal**: Handle Excel import/export with column name translation

#### Step 6.1: Excel Import

```csharp
public class ExcelImporter
{
    public List<Activity> ImportFromExcel(string filePath)
    {
        var activities = new List<Activity>();
        int sequence = 1;
 var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
        var userSuffix = App.CurrentUser.Username.Length >= 3 
            ? App.CurrentUser.Username.Substring(App.CurrentUser.Username.Length - 3).ToLower()
       : App.CurrentUser.Username.ToLower();
        
  using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        
        // Build column index map
    var columnMap = new Dictionary<int, string>();
        for (int col = 1; col <= worksheet.Dimension.Columns; col++)
    {
            string oldVantageHeader = worksheet.Cells[1, col].Text;
            string newVantageName = ColumnMapper.GetColumnNameFromOldVantage(oldVantageHeader);
      columnMap[col] = newVantageName;
        }
        
        // Import rows
 for (int row = 2; row <= worksheet.Dimension.Rows; row++)
   {
         var activity = new Activity();
        
     foreach (var kvp in columnMap)
            {
          int col = kvp.Key;
   string propertyName = kvp.Value;
             string cellValue = worksheet.Cells[row, col].Text;
        
    // Set property value
          SetPropertyValue(activity, propertyName, cellValue);
   }
          
     // Handle UniqueID generation
            if (string.IsNullOrEmpty(activity.UniqueID))
   {
     activity.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
     sequence++;
  }
  
            // Handle defaults
            if (string.IsNullOrWhiteSpace(activity.AssignedTo))
              activity.AssignedTo = "Unassigned";
       
        activities.Add(activity);
        }

        return activities;
    }
  
    private void SetPropertyValue(Activity activity, string propertyName, string cellValue)
    {
   var prop = typeof(Activity).GetProperty(propertyName);
        if (prop == null || !prop.CanWrite) return;
        
 if (string.IsNullOrWhiteSpace(cellValue))
    {
    // Set defaults for empty values
  if (prop.PropertyType == typeof(string))
         prop.SetValue(activity, "");
     else if (prop.PropertyType == typeof(double) || prop.PropertyType == typeof(int))
              prop.SetValue(activity, 0);
            return;
    }
        
        // Special handling for PercentEntry: convert 0-1 to 0-100
        if (propertyName == "PercentEntry" && double.TryParse(cellValue, out var percentDecimal))
     {
          activity.SetPercentFromDecimal(percentDecimal);  // 0.755 ? 75.5
            return;
}
  
     // Regular property setting
      if (prop.PropertyType == typeof(string))
  prop.SetValue(activity, cellValue);
        else if (prop.PropertyType == typeof(double) && double.TryParse(cellValue, out var dbl))
prop.SetValue(activity, dbl);
        else if (prop.PropertyType == typeof(int) && int.TryParse(cellValue, out var i))
            prop.SetValue(activity, i);
        // ... other types
    }
}
```

#### Step 6.2: Excel Export

```csharp
public class ExcelExporter
{
    public void ExportToExcel(List<Activity> activities, string filePath)
 {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Activities");
        
        // Get column mappings (exclude calculated fields)
      var mappings = GetExportableColumns();
        
        // Write headers (OldVantage names)
     int col = 1;
        foreach (var mapping in mappings)
     {
          worksheet.Cells[1, col].Value = mapping.OldVantageName;
      col++;
     }
    
        // Write data
        int row = 2;
        foreach (var activity in activities)
   {
        col = 1;
            foreach (var mapping in mappings)
            {
        var value = GetPropertyValue(activity, mapping.ColumnName);
        
    // Special handling for PercentEntry: convert 0-100 to 0-1
  if (mapping.ColumnName == "PercentEntry" && value is double percent)
              {
        value = percent / 100;  // 75.5 ? 0.755
  }
         
     // Special handling for ROCBudgetQTY: must equal Quantity
       if (mapping.ColumnName == "ROCBudgetQTY")
             {
    value = activity.Quantity;
            }
   
         worksheet.Cells[row, col].Value = value;
            col++;
            }
            row++;
  }
        
 package.SaveAs(new FileInfo(filePath));
    }
    
    private List<ColumnMapping> GetExportableColumns()
    {
        using var connection = DatabaseSetup.GetConnection();
    connection.Open();
        
   var cmd = connection.CreateCommand();
        cmd.CommandText = @"
  SELECT ColumnName, OldVantageName 
            FROM ColumnMappings 
       WHERE IsCalculated = 0 
   AND OldVantageName IS NOT NULL 
      AND OldVantageName != 'None. Will not be included in import or export'
     ORDER BY ColumnName";
  
   // ... read mappings
    }
}
```

#### Step 6.3: Azure Sync

```csharp
public class AzureSyncService
{
    public async Task UploadToAzure(Activity activity)
    {
  var azureData = new Dictionary<string, object>();
        
      // Get all syncable columns
        var mappings = GetAzureSyncableColumns();
        
        foreach (var mapping in mappings)
        {
    var value = GetPropertyValue(activity, mapping.ColumnName);
       
         // Special handling for PercentEntry: convert 0-100 to 0-1
            if (mapping.ColumnName == "PercentEntry" && value is double percent)
            {
    value = percent / 100;  // 75.5 ? 0.755
    }
            
   azureData[mapping.AzureName] = value;
  }
        
        // Add system fields
    azureData["UserID"] = App.CurrentUser.Username;
        azureData["Timestamp"] = DateTime.Now;
     
        await _azureClient.UpsertAsync("Activities", azureData);
    }
}
```

**Deliverable**: Import/export working with proper translations

**Testing**:
- ? Import from old Excel format works
- ? PercentEntry converts correctly (0.755 ? 75.5)
- ? UniqueID auto-generates if missing
- ? Export to old Excel format works
- ? PercentEntry converts correctly (75.5 ? 0.755)
- ? ROCBudgetQTY equals Quantity on export
- ? Azure sync works
- ? Calculated fields NOT exported

---

### **PHASE 7: Cleanup & Final Testing** ??
**Goal**: Remove old code, comprehensive testing

#### Step 7.1: Remove Deprecated Code
- ? Delete old `GetDbColumnName()` and `GetPropertyName()` methods
- ? Remove any remaining `ColumnMapper` calls in code
- ? Clean up comments referring to old column names
- ? Update XML documentation

#### Step 7.2: Update DatabaseSetup.cs

```csharp
// Remove old Activities table creation
// Add new Activities table with NewVantage names
// Add ColumnMappings seeding from CSV
```

#### Step 7.3: Comprehensive Testing

**Test Suite**:
- ? Create new database (fresh install)
- ? CRUD operations (Create, Read, Update, Delete)
- ? All filter types (text, list)
- ? Sorting all columns
- ? Paging
- ? Search
- ? Import Excel (old format) ? verify PercentEntry conversion
- ? Export Excel (old format) ? verify PercentEntry conversion
- ? Azure sync ? verify PercentEntry conversion
- ? Calculated columns display correctly
- ? Calculated columns filter correctly
- ? Null/blank handling
- ? UniqueID auto-generation
- ? Performance (no regression)

**Deliverable**: Clean, tested, documented codebase

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| PercentEntry conversion bugs | Medium | High | Extensive testing, clear documentation |
| Breaking existing features | Medium | High | Phase-by-phase testing |
| Performance regression | Low | Medium | Benchmark before/after |
| Import/export format errors | Medium | High | Test with real Excel files |
| UniqueID collisions | Low | Medium | Use timestamp + sequence + username |

---

## Timeline Estimate

| Phase | Estimated Time | Complexity |
|-------|----------------|------------|
| Phase 0: Prep | 1 hour | Low |
| Phase 1: Database | 3 hours | Medium |
| Phase 2: Model | 2 hours | Low |
| Phase 3: Repository | 3 hours | Medium |
| Phase 4: ColumnMapper | 2 hours | Low |
| Phase 5: Views/ViewModels | 2 hours | Low |
| Phase 6: Import/Export | 4 hours | High |
| Phase 7: Testing | 4 hours | Medium |
| **Total** | **21 hours** | |

---

## Success Criteria

? **Database**: Single set of column names (NewVantage)
? **Code**: No ColumnMapper translations except import/export
? **Filters**: All working correctly
? **PercentEntry**: Displays as 0-100%, stores as 0-100, converts correctly
? **Import/Export**: Excel round-trip preserves data
? **Azure Sync**: Uploads with correct format
? **Performance**: No noticeable slowdown
? **Tests**: All passing

---

**Document Version**: 2.0  
**Last Updated**: 2024-01-30  
**Status**: READY TO IMPLEMENT - Awaiting Phase 0 completion
