# VANTAGE Column Name Refactor - Design Document

## Executive Summary
Refactor the VANTAGE application to use a single, consistent set of column names throughout the database, models, and UI. Eliminate the complex translation layer between database columns and display names, moving all mapping logic to import/export operations only.

---

## Current State Analysis

### Problems
1. **Triple naming convention**: Database columns (e.g., `Tag_ProjectID`), Model properties (e.g., `ProjectID`), Display names (e.g., "Project ID")
2. **Complex translation layer**: `ColumnMapper` constantly translates between naming schemes
3. **Filter bugs**: Current issue where filter popups don't work correctly due to name translation failures
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
Display ("ProjectID")
 
Import/Export ONLY:
Excel/Azure ? [Mapping] ? Database (ProjectID)
Database (ProjectID) ? [Mapping] ? Excel/Azure
```

### Single Source of Truth: ColumnMappings Table
```sql
CREATE TABLE ColumnMappings (
    MappingID INTEGER PRIMARY KEY AUTOINCREMENT,
    ColumnName TEXT,     -- The ONE name used everywhere (was DefaultDisplayName)
  OldVantageName TEXT,       -- For Excel import/export
AzureName TEXT, -- For Azure sync
    DataType TEXT,
    IsEditable INTEGER DEFAULT 1,
    IsCalculated INTEGER DEFAULT 0,
    Notes TEXT
);
```

**Key Change**: `DbColumnName` is removed. `DefaultDisplayName` becomes `ColumnName` and is used everywhere except import/export.

---

## Phase-by-Phase Implementation Plan

### **PHASE 0: Preparation & Branch Setup** ?
**Goal**: Set up for the refactor without breaking current functionality

**Tasks**:
1. ? Create new Git branch: `refactor/unified-column-names`
2. Document current `ColumnMappings` data (export to CSV for reference)
3. Create backup of current database schema
4. Review all calculated columns in `Activity` model

**Calculated Columns Decision**:
- **Current calculated columns**: `Status`, `EarnMHsCalc`, `EarnedQtyCalc`, `PercentCompleteCalc`, `AssignedToUsername`, etc.
- **Recommendation**: Keep as C# properties, NOT database columns
  - **Pros**: Always accurate, no sync issues, simpler schema
  - **Cons**: Can't query/filter on them in SQL (but we can use CASE expressions)
  - **Verdict**: Use SQL CASE expressions in queries when needed (already doing this for Status)

**Deliverable**: Branch created, current state documented

---

### **PHASE 1: Database Schema Migration** ??
**Goal**: Rename all database columns to match `DefaultDisplayName` values

#### Step 1.1: Create Column Name Mapping
Query `ColumnMappings` to generate rename script:
```sql
-- Example mapping (will generate from actual ColumnMappings data)
Tag_ProjectID ? ProjectID
Val_Perc_Complete ? PercentEntry
UDFEleven ? AssignedTo
UDFNineteen ? UniqueID
```

#### Step 1.2: Create Migration Script
Since SQLite doesn't support `ALTER TABLE RENAME COLUMN` in older versions, we'll:
1. Create new `Activities` table with new column names
2. Copy data from old table
3. Drop old table
4. Rename new table

```sql
-- Migration script (simplified example)
CREATE TABLE Activities_New (
    ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
    HexNO INTEGER DEFAULT 0,
    CompType TEXT,
    PhaseCategory TEXT,
    ProjectID TEXT,
    PercentEntry REAL DEFAULT 0,
    AssignedTo TEXT,
    UniqueID TEXT UNIQUE NOT NULL,
    -- ... all other columns with new names
);

INSERT INTO Activities_New SELECT 
    ActivityID,
    HexNO,
    Catg_ComponentType,  -- old name
    Catg_PhaseCategory,
    Tag_ProjectID,
    Val_Perc_Complete,
    UDFEleven,
    UDFNineteen,
    -- ... all other columns
FROM Activities;

DROP TABLE Activities;
ALTER TABLE Activities_New RENAME TO Activities;

-- Recreate indexes with new column names
CREATE INDEX idx_project ON Activities(ProjectID);
CREATE INDEX idx_assigned_to ON Activities(AssignedTo);
-- ... other indexes
```

#### Step 1.3: Update ColumnMappings Table
```sql
-- Drop DbColumnName column
ALTER TABLE ColumnMappings DROP COLUMN DbColumnName;

-- Rename DefaultDisplayName to ColumnName (or just use it as-is)
-- Since SQLite doesn't support column rename easily, we'll just update docs
-- to refer to DefaultDisplayName as "ColumnName"

-- Update index
DROP INDEX IF EXISTS idx_db_column;
CREATE INDEX idx_column_name ON ColumnMappings(DefaultDisplayName);
```

#### Step 1.4: Migration Code
Add to `DatabaseSetup.cs`:
```csharp
public static void MigrateToUnifiedColumnNames()
{
    using var connection = GetConnection();
    connection.Open();
    
    // Check if migration already done
    var checkCmd = connection.CreateCommand();
    checkCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Activities') WHERE name='ProjectID'";
    var alreadyMigrated = (long)checkCmd.ExecuteScalar() > 0;
    
    if (alreadyMigrated) return;
    
    // Run migration script
    var migrationCmd = connection.CreateCommand();
    migrationCmd.CommandText = @"
      -- Full migration SQL here
    ";
    migrationCmd.ExecuteNonQuery();
}
```

**Deliverable**: Database schema updated, migration script tested

**Testing**:
- ? New database creates with correct column names
- ? Existing database migrates correctly
- ? All data preserved
- ? Indexes recreated

---

### **PHASE 2: Activity Model Refactor** ??
**Goal**: Update `Activity` model properties to match new database column names

#### Step 2.1: Update Property Names
```csharp
public class Activity : INotifyPropertyChanged
{
    // OLD: private string _projectID; public string ProjectID { ... }
    // NEW: Same! (property name already matches display name)
    
    // But fix the database mapping:
    // OLD: [Column("Tag_ProjectID")]
    // NEW: [Column("ProjectID")]  or remove attribute entirely
    
    public int ActivityID { get; set; }
    public int HexNO { get; set; }
    public string CompType { get; set; }
    public string PhaseCategory { get; set; }
    public string ProjectID { get; set; }  // was mapped from Tag_ProjectID
    
    private double _percentEntry;
    public double PercentEntry  // was mapped from Val_Perc_Complete
    {
 get => _percentEntry;
        set
        {
 _percentEntry = value;
       OnPropertyChanged(nameof(PercentEntry));
    OnPropertyChanged(nameof(EarnMHsCalc));  // trigger recalc
    OnPropertyChanged(nameof(Status));
        }
    }
    
    public string AssignedTo { get; set; }  // was UDFEleven
    public string UniqueID { get; set; }    // was UDFNineteen
    
    // Calculated properties stay the same
    public string Status => /* ... */;
    public double EarnMHsCalc => /* ... */;
}
```

#### Step 2.2: Handle Null/Blank/Empty Values
Add to `Activity` constructor or property setters:
```csharp
private string _assignedTo = "Unassigned";
public string AssignedTo
{
    get => string.IsNullOrWhiteSpace(_assignedTo) ? "Unassigned" : _assignedTo;
    set => _assignedTo = value;
}

private double _percentEntry;
public double PercentEntry
{
    get => _percentEntry;
    set => _percentEntry = value < 0 ? 0 : (value > 1 ? 1 : value);  // clamp 0-1
}
```

**Deliverable**: `Activity` model updated, compiles successfully

**Testing**:
- ? Model properties match database columns
- ? Calculated properties still work
- ? Null/empty handling works
- ? INotifyPropertyChanged still triggers correctly

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

#### Step 3.2: Update Calculated Column Queries
```csharp
// Status calculation (already using CASE expression):
string statusCase = @"
    CASE 
        WHEN PercentEntry IS NULL OR PercentEntry = 0 THEN 'Not Started' 
        WHEN PercentEntry >= 1.0 THEN 'Complete' 
        ELSE 'In Progress' 
    END";

// Use in queries:
command.CommandText = $@"
    SELECT *, {statusCase} as Status 
  FROM Activities 
  WHERE ProjectID = @projectId";
```

#### Step 3.3: Simplify ColumnMapper
```csharp
public static class ColumnMapper
{
  // REMOVE all GetDbColumnName / GetPropertyName methods
  // KEEP only import/export mappings:
    
    public static string GetOldVantageName(string columnName)
    {
        // Query ColumnMappings.OldVantageName WHERE DefaultDisplayName = columnName
    }
    
    public static string GetAzureName(string columnName)
    {
 // Query ColumnMappings.AzureName WHERE DefaultDisplayName = columnName
    }
    
    public static string GetColumnNameFromOldVantage(string oldName)
    {
     // Query ColumnMappings.DefaultDisplayName WHERE OldVantageName = oldName
    }
    
    public static string GetColumnNameFromAzure(string azureName)
    {
        // Query ColumnMappings.DefaultDisplayName WHERE AzureName = azureName
    }
}
```

**Deliverable**: All SQL queries updated, ColumnMapper simplified

**Testing**:
- ? Data loads correctly
- ? Filtering works
- ? Sorting works
- ? Updates save correctly

---

### **PHASE 4: ViewModel & View Updates** ??
**Goal**: Simplify filter logic and DataGrid bindings

#### Step 4.1: Update FilterBuilder
```csharp
// OLD: BuildFilterCondition had to translate column names
// NEW: Use column names directly

public void AddTextSearch(string searchText)
{
    // Search across multiple columns - use actual column names
    var condition = $@"(
        ProjectID LIKE '%{Escape(searchText)}%' OR
        Description LIKE '%{Escape(searchText)}%' OR
  TagNO LIKE '%{Escape(searchText)}%' OR
        UniqueID LIKE '%{Escape(searchText)}%'
    )";
    AddCondition(condition);
}
```

#### Step 4.2: Simplify ProgressViewModel
```csharp
// REMOVE all ColumnMapper.GetDbColumnName() calls
// Use column names directly:

private string BuildFilterCondition(string columnName, string filterType, string filterValue)
{
    // No more translation needed!
    switch (filterType)
    {
        case "Contains":
   return $"{columnName} LIKE '%{EscapeSQL(filterValue)}%'";
  case "Equals":
  return $"{columnName} = '{EscapeSQL(filterValue)}'";
        // ...
    }
}
```

#### Step 4.3: Update DataGrid Column Bindings
```xml
<!-- OLD: Binding path might not match column name -->
<DataGridTextColumn Header="Project ID" Binding="{Binding ProjectID}" />

<!-- NEW: Same! (already correct, just verify all columns) -->
<DataGridTextColumn Header="Project ID" Binding="{Binding ProjectID}" />
```

#### Step 4.4: Fix ColumnFilterPopup
```csharp
// REMOVE GetDistinctColumnValuesForFilterAsync complexity
// Simplify to use column name directly:

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

**Deliverable**: Views and ViewModels simplified, filters working

**Testing**:
- ? DataGrid displays correctly
- ? Column filters show correct values
- ? Text search works
- ? All filters apply correctly
- ? Paging works

---

### **PHASE 5: Import/Export Updates** ??
**Goal**: Keep mapping logic for Excel and Azure, but only in import/export code

#### Step 5.1: Excel Import
```csharp
public class ExcelImporter
{
    public List<Activity> ImportFromExcel(string filePath)
    {
 var activities = new List<Activity>();
   
        // Read Excel file
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        
        for (int row = 2; row <= worksheet.Dimension.Rows; row++)
        {
var activity = new Activity();
            
     // Map OLD column names to NEW
  for (int col = 1; col <= worksheet.Dimension.Columns; col++)
       {
       string excelHeader = worksheet.Cells[1, col].Text;
      string cellValue = worksheet.Cells[row, col].Text;
    
     // Get our column name from old vantage name
          string columnName = ColumnMapper.GetColumnNameFromOldVantage(excelHeader);
           
    // Set property by name (reflection or switch statement)
         SetPropertyValue(activity, columnName, cellValue);
    }
   
            // Handle nulls/blanks for calculated fields
            if (string.IsNullOrWhiteSpace(activity.AssignedTo))
   activity.AssignedTo = "Unassigned";
    
       activities.Add(activity);
   }
        
        return activities;
    }
}
```

#### Step 5.2: Excel Export
```csharp
public class ExcelExporter
{
    public void ExportToExcel(List<Activity> activities, string filePath)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Activities");
        
  // Get all column mappings
        var mappings = GetColumnMappings();
        
        // Write headers (OLD Vantage names for compatibility)
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
  worksheet.Cells[row, col].Value = value;
     col++;
          }
     row++;
        }
        
        package.SaveAs(new FileInfo(filePath));
    }
}
```

#### Step 5.3: Azure Sync
```csharp
public class AzureSyncService
{
    public async Task UploadToAzure(Activity activity)
    {
        var azureData = new Dictionary<string, object>();
   
        // Map our column names to Azure column names
        var mappings = GetColumnMappings();
  foreach (var mapping in mappings)
      {
  var value = GetPropertyValue(activity, mapping.ColumnName);
     azureData[mapping.AzureName] = value;
      }
        
        // Send to Azure
        await _azureClient.UpsertAsync("Activities", azureData);
    }
}
```

**Deliverable**: Import/export working with column name translation

**Testing**:
- ? Import from old Excel format works
- ? Export to old Excel format works
- ? Azure sync works
- ? Calculated fields handle nulls correctly

---

### **PHASE 6: Cleanup & Testing** ??
**Goal**: Remove old code, comprehensive testing

#### Step 6.1: Remove Deprecated Code
- Delete old `ColumnMapper.GetDbColumnName()` methods
- Remove `DbColumnName` references from codebase
- Clean up comments referring to old column names
- Update XML documentation

#### Step 6.2: Update Documentation
- README.md: Explain new column name strategy
- Database schema diagram
- Import/export guide

#### Step 6.3: Comprehensive Testing
```
Test Suite:
- ? Create new database (fresh install)
- ? Migrate existing database
- ? CRUD operations (Create, Read, Update, Delete)
- ? All filter types (text, list, date, number)
- ? Sorting all columns
- ? Paging
- ? Search
- ? Import Excel (old format)
- ? Export Excel (old format)
- ? Azure sync (if applicable)
- ? Calculated columns update correctly
- ? Null/blank handling
- ? Performance (no regression)
```

**Deliverable**: Clean, tested, documented codebase

---

## Migration Strategy for Existing Users

### Option 1: Automatic Migration on Startup
```csharp
// In App.xaml.cs OnStartup:
DatabaseSetup.InitializeDatabase();
DatabaseSetup.MigrateToUnifiedColumnNames();  // Runs only if needed
```

### Option 2: Require New Database
- Provide export tool to save current data to Excel
- User creates fresh database
- Import from Excel with new column names

**Recommendation**: Option 1 (automatic migration)

---

## Calculated Columns Strategy - DECISION NEEDED

### Current Calculated Columns:
1. **Status** - `NotStarted`, `InProgress`, `Complete` (based on `PercentEntry`)
2. **EarnMHsCalc** - `PercentEntry * BudgetMHs`
3. **EarnedQtyCalc** - `(EarnQtyEntry / Quantity) * 100` as percentage
4. **PercentCompleteCalc** - Same as `PercentEntry` but displayed as 0-100%
5. **AssignedToUsername** - Fallback to "Unassigned" if null

### Recommendation: Keep as C# Properties, Use SQL CASE When Needed

**Reasoning**:
1. **Always accurate**: No risk of stale data
2. **Simpler schema**: Fewer columns to maintain
3. **No sync issues**: Don't need triggers or batch updates
4. **Queryable when needed**: Use SQL CASE expressions in WHERE clauses

**Example**:
```csharp
// In Activity model:
public string Status
{
    get
    {
        if (PercentEntry == null || PercentEntry == 0) return "Not Started";
        if (PercentEntry >= 1.0) return "Complete";
        return "In Progress";
    }
}

// In SQL queries when filtering:
string statusCase = @"
    CASE 
   WHEN PercentEntry IS NULL OR PercentEntry = 0 THEN 'Not Started'
        WHEN PercentEntry >= 1.0 THEN 'Complete'
ELSE 'In Progress'
    END";
    
command.CommandText = $@"
    SELECT *, {statusCase} as Status
    FROM Activities
    WHERE {statusCase} = @statusFilter";
```

**Alternative**: Store in database and update via triggers
- **Pros**: Slightly faster queries
- **Cons**: Complexity, potential for stale data, more disk space

**Decision**: PENDING - Keep as C# properties + SQL CASE expressions

---

## Null/Blank/Empty Value Handling Strategy

### Import Behavior for Blank Values:
1. **Calculated fields (Status, EarnMHsCalc, etc.)**: Skip setting them, let C# calculate on-the-fly
2. **User-editable fields**: Set to default values:
   - Text: `""` (empty string)
   - Numbers: `0` or `null` (depending on field)
   - AssignedTo: `"Unassigned"`
3. **Required fields (UniqueID)**: Throw validation error if blank

### Database Constraints:
```sql
CREATE TABLE Activities (
    UniqueID TEXT NOT NULL,  -- Required
    AssignedTo TEXT DEFAULT 'Unassigned',
    PercentEntry REAL DEFAULT 0,
  -- ... other columns
);
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Data loss during migration | Low | High | Backup database, test migration extensively |
| Breaking existing features | Medium | High | Comprehensive testing, phase-by-phase approach |
| Performance regression | Low | Medium | Benchmark before/after |
| User confusion | Low | Low | No UI changes |
| Import/export breaks | Medium | Medium | Test with real Excel files |

---

## Timeline Estimate

| Phase | Estimated Time | Risk Level |
|-------|----------------|------------|
| Phase 0: Prep | 2 hours | Low |
| Phase 1: Database | 4 hours | Medium |
| Phase 2: Model | 3 hours | Low |
| Phase 3: Repository | 4 hours | Medium |
| Phase 4: View/ViewModel | 3 hours | Low |
| Phase 5: Import/Export | 4 hours | Medium |
| Phase 6: Testing | 4 hours | Low |
| **Total** | **24 hours** | |

---

## Questions for Review

1. **Calculated columns**: Approve C# properties + SQL CASE strategy? ? APPROVED
2. **Column naming**: Keep current `DefaultDisplayName` values exactly as-is? ? APPROVED
3. **Null handling**: For imports - skip calculated fields, use defaults for editables, error on required? ? APPROVED
4. **Migration timing**: Auto-migrate on startup? ? APPROVED
5. **Testing priority**: Which features are most critical to test thoroughly? PENDING
6. **Ready to proceed?**: Create branch and start Phase 0? PENDING

---

## Current Column Mapping Reference

See `Utilities\ColumnMapper.cs` for complete current mapping.

**Example mappings that will change**:
- `Tag_ProjectID` ? becomes just `ProjectID` everywhere
- `Val_Perc_Complete` ? becomes just `PercentEntry` everywhere
- `UDFEleven` ? becomes just `AssignedTo` everywhere
- `UDFNineteen` ? becomes just `UniqueID` everywhere

---

## Next Steps

1. Review and approve this plan
2. Create branch: `refactor/unified-column-names`
3. Begin Phase 0: Export current ColumnMappings, document calculated columns
4. Proceed phase-by-phase with testing at each step

---

**Document Version**: 1.0  
**Date**: 2024  
**Status**: DRAFT - Awaiting approval  
