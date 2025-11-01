# VANTAGE Column Name Refactor - Design Document

## Executive Summary
Refactor the VANTAGE application to use a single, consistent set of column names throughout the database, models, and UI. Eliminate the complex translation layer between database columns and display names, moving all mapping logic to import/export operations only.

**Branch**: `ColumnNameRefactor`

**Status**: ? **PHASES 1-3 COMPLETE** | Database, Model, and UI now use NewVantage names consistently

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

## ? COMPLETED PHASES

### **PHASE 1: Database Schema Creation** ? COMPLETE
**Goal**: Create new Activities table with NewVantage column names

#### Completed Tasks:
? **DatabaseSetup.cs** - Created Activities table with NewVantage column names
? **ColumnMappings Table** - Hardcoded all 86 mappings from CSV (no runtime CSV file needed)
? **Removed CSV from deployment** - CSV kept in repo for documentation only
? **All column names use NewVantage format** (e.g., `SecondActno`, `PercentEntry`, `ProjectID`)

#### Key Changes:
```sql
CREATE TABLE Activities (
    ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectID TEXT,        -- NewVantage name (was Tag_ProjectID)
    PercentEntry REAL,-- 0-100 format (was Val_Perc_Complete)
    SecondActno TEXT,    -- NewVantage name (was Sch_Actno)
    AssignedTo TEXT,      -- NewVantage name (was UDFEleven)
    ...
);

CREATE TABLE ColumnMappings (
    ColumnName TEXT,     -- NewVantage name
    OldVantageName TEXT,      -- For Excel import/export
 AzureName TEXT,           -- For Azure sync
    ...
);
```

**Result**: Database uses NewVantage names exclusively. 86 column mappings hardcoded in `SeedColumnMappings()` method.

---

### **PHASE 2: Activity Model Refactor** ? COMPLETE
**Goal**: Update `Activity` model properties to match NewVantage column names

#### Completed Tasks:
? **Fixed property names** to match database columns exactly
? **Updated PercentEntry** to store as 0-100 instead of 0-1
? **Added helper methods** for Excel import/export conversion
? **Fixed all calculated properties** to work with 0-100 format
? **Renamed `OldActno`** to `SecondActno` to match database

#### Key Changes:
```csharp
// OLD:
public string OldActno { get; set; }
public double PercentEntry { get; set; } // 0-1 format

// NEW:
public string SecondActno { get; set; }  // Matches DB column name
public double PercentEntry { get; set; } // 0-100 format

// NEW: Helper methods for Excel conversion
public void SetPercentFromDecimal(double decimalValue)
{
    PercentEntry = decimalValue * 100; // 0.755 ? 75.5
}

public double GetPercentAsDecimal()
{
    return PercentEntry / 100; // 75.5 ? 0.755
}

// NEW: Status calculation using 0-100
public string Status
{
    get
    {
        if (PercentEntry == 0) return "Not Started";
        if (PercentEntry >= 100) return "Complete";
      return "In Progress";
    }
}
```

**Result**: All Activity properties now match database column names. PercentEntry stored as 0-100 throughout.

---

### **PHASE 3: UI/DataGrid Updates** ? COMPLETE
**Goal**: Update all DataGrid column headers to match NewVantage names

#### Completed Tasks:
? **Updated all column headers** in ProgressView.xaml to use exact database names
? **Removed alternate display names** (e.g., "Drawing No" ? "DwgNO")
? **Fixed bindings** to match property names
? **Updated ActivityRepository** to use correct property names

#### Key Changes:
```xaml
<!-- OLD: -->
<DataGridTextColumn Header="Drawing No" Binding="{Binding DwgNO}" />
<DataGridTextColumn Header="% Complete" Binding="{Binding PercentEntry_Display}" />
<DataGridTextColumn Header="OldActno" Binding="{Binding OldActno}" />
<DataGridTextColumn Header="Val UDF3" Binding="{Binding ClientCustom3}" />

<!-- NEW: -->
<DataGridTextColumn Header="DwgNO" Binding="{Binding DwgNO}" />
<DataGridTextColumn Header="PercentEntry" Binding="{Binding PercentEntry_Display}" />
<DataGridTextColumn Header="SecondActno" Binding="{Binding SecondActno}" />
<DataGridTextColumn Header="ClientCustom3" Binding="{Binding ClientCustom3}" />
```

**Result**: ALL DataGrid headers now match database column names exactly. No more translation needed for UI.

---

### **PHASE 4: ColumnMapper Simplification** ? COMPLETE (Already Done)
**Goal**: Keep ONLY import/export mapping, remove all database mapping

#### Current State:
? **ColumnMapper** already simplified - only handles Excel/Azure translation
? **Database queries** use column names directly (no `GetDbColumnName()` calls)
? **LoadMappingsFromDatabase()** reads from ColumnMappings table
? **Import/Export methods** use ColumnMapper for OldVantage ? NewVantage translation

**Deprecated Methods** (marked for removal in future):
- `GetDbColumnName()` - Database columns now match property names
- `GetPropertyName()` - No translation needed anymore
- `IsValidDbColumn()` - Use ColumnMappings table queries instead
- `GetAllDbColumnNames()` - Use ColumnMappings table queries instead

**Result**: ColumnMapper now ONLY used for Excel/Azure translation. No internal database translation.

---

## ?? REMAINING PHASES

### **PHASE 5: Excel Import/Export** ? IN PROGRESS
**Goal**: Ensure Excel import/export works with column name translation

#### Tasks:
? **ExcelImporter.cs** - Already updated to use ColumnMapper for OldVantage ? NewVantage
? **PercentEntry conversion** - Already converts 0-1 to 0-100 on import
?? **ExcelExporter.cs** - Needs update to export with OldVantage names
?? **Test import/export round-trip** - Verify data preservation

---

### **PHASE 6: Repository & ViewModel Updates** ? IN PROGRESS  
**Goal**: Remove any remaining ColumnMapper.GetDbColumnName() calls

#### Tasks:
? **ActivityRepository.cs** - Already uses NewVantage column names directly
? **FilterBuilder.cs** - Already uses column names directly
?? **Verify all SQL queries** - Ensure no old column names remain
?? **Test all filters** - Status, AssignedTo, calculated fields

---

### **PHASE 7: Testing & Cleanup** ?? PENDING
**Goal**: Comprehensive testing and code cleanup

#### Tasks:
- [ ] Test database creation (fresh install)
- [ ] Test CRUD operations
- [ ] Test all filter types (text, list, calculated)
- [ ] Test sorting all columns
- [ ] Test Excel import (old format) ? verify PercentEntry conversion
- [ ] Test Excel export (old format) ? verify PercentEntry conversion
- [ ] Test UniqueID auto-generation
- [ ] Performance testing (no regression)
- [ ] Remove deprecated ColumnMapper methods
- [ ] Update XML documentation
- [ ] Clean up debug logging

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

## Architecture Summary

### Current State (After Phases 1-4)

```
???????????????????????????????????????????????????????????
? CSV File (Documents/ColumnNameComparisonForAiModel.csv)?
? ? Kept in repo for DOCUMENTATION/REFERENCE only         ?
? ? NOT included in deployment      ?
? ? NOT read at runtime        ?
???????????????????????????????????????????????????????????
           ?
        ? (Used to create hardcoded mappings)
 ?
???????????????????????????????????????????????????????????
? DatabaseSetup.SeedColumnMappings() - HARDCODED METHOD   ?
? ? 86 mappings embedded in C# code     ?
? ? Compile-time verified           ?
? ? Version controlled         ?
? ? No file I/O at runtime ?
???????????????????????????????????????????????????????????
 ?
                 ? (Called during InitializeDatabase)
???????????????????????????????????????????????????????????
? ColumnMappings Table (SQLite Database)      ?
? - Populated on first run          ?
? - Single source of truth for translations       ?
???????????????????????????????????????????????????????????
   ?
   ? (ColumnMapper.LoadMappingsFromDatabase)
???????????????????????????????????????????????????????????
? In-Memory Cache (Dictionary)          ?
? - Used ONLY for Excel/Azure translation              ?
? - NOT used for internal database queries?
???????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????
? Database Tables   ?
? ? Use NewVantage column names (ProjectID, PercentEntry) ?
???????????????????????????????????????????????????????????
       ?
              ? (Direct property mapping - no translation)
???????????????????????????????????????????????????????????
? C# Models (Activity)    ?
? ? Properties match database columns exactly             ?
? ? PercentEntry stored as 0-100        ?
???????????????????????????????????????????????????????????
     ?
 ? (Direct binding - no translation)
???????????????????????????????????????????????????????????
? DataGrid UI    ?
? ? Headers match property/column names      ?
? ? All use NewVantage names         ?
???????????????????????????????????????????????????????????

Translation ONLY happens at system boundaries:
???????????????????????????????????????????????????????????
? Excel Import/Export         ?
? OldVantage (Tag_ProjectID) ? NewVantage (ProjectID)    ?
? PercentEntry: 0-1 ? 0-100 conversion            ?
???????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????
? Azure Upload/Sync      ?
? NewVantage (ProjectID) ? Azure (Tag_ProjectID)         ?
? PercentEntry: 0-100 ? 0-1 conversion            ?
???????????????????????????????????????????????????????????
```

---

## Success Criteria

? **Database**: Single set of column names (NewVantage)
? **Code**: No ColumnMapper translations except import/export
? **PercentEntry**: Displays as 0-100%, stores as 0-100, converts correctly
? **Import/Export**: Excel round-trip preserves data (needs testing)
?? **Azure Sync**: Uploads with correct format (pending)
?? **Performance**: No noticeable slowdown (pending testing)
?? **Tests**: All passing (pending)

---

**Document Version**: 3.0  
**Last Updated**: 2025-01-30  
**Status**: ? PHASES 1-4 COMPLETE | Database, Model, UI use NewVantage names consistently
