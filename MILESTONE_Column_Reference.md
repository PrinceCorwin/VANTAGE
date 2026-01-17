# MILESTONE Column Reference

**Last Updated:** January 16, 2025  
**Purpose:** Comprehensive reference of all column groupings used throughout MILESTONE

---

## 1. EXPORTED TO EXCEL COLUMNS

These are the columns that appear in Excel exports, in this exact left-to-right order.  
**Source:** `ExcelExporter.cs` - `ExportColumnOrder` array  
**Total Count:** 86 columns  
**Format:** OldVantage Name = Milestone Column Name

```
HexNO = HexNO
Catg_ComponentType = CompType
Catg_PhaseCategory = PhaseCategory
Catg_ROC_Step = ROCStep
Dwg_PrimeDrawingNO = DwgNO
Dwg_RevisionNo = RevNO
Dwg_SecondaryDrawingNO = SecondDwgNO
Dwg_ShtNo = ShtNO
Notes_Comments = Notes
Sch_Actno = SecondActno
Sch_Start = SchStart
Sch_Finish = SchFinish
Sch_Status = Status
Tag_Aux1 = Aux1
Tag_Aux2 = Aux2
Tag_Aux3 = Aux3
Tag_Area = Area
Tag_CONo = ChgOrdNO
Tag_Descriptions = Description
Tag_EqmtNo = EqmtNO
Tag_Estimator = Estimator
Tag_Insulation_Typ = InsulType
Tag_LineNo = LineNO
Tag_Matl_Spec = MtrlSpec
Tag_Phase Code = PhaseCode
Tag_Paint_Code = PaintCode
Tag_Pipe_Grade = PipeGrade
Tag_ProjectID = ProjectID
Tag_RFINo = RFINO
Tag_Sch_ActNo = SchedActNO
Tag_Service = Service
Tag_ShopField = ShopField
Tag_SubArea = SubArea
Tag_System = PjtSystem
Tag_SystemNo = SystemNO
Tag_TagNo = TagNO
Tag_Tracing = HtTrace
Tag_WorkPackage = WorkPackage
Tag_XRAY = XRay
Trg_DateTrigger = DateTrigger
UDFOne = UDF1
UDFTwo = UDF2
UDFThree = UDF3
UDFFour = UDF4
UDFFive = UDF5
UDFSix = UDF6
UDFSeven = UDF7
UDFEight = UDF8
UDFNine = UDF9
UDFTen = UDF10
UDFEleven = AssignedTo
UDFTwelve = UDF12
UDFThirteen = CreatedBy
UDFFourteen = UDF14
UDFFifteen = UDF15
UDFSixteen = UDF16
UDFSeventeen = UDF17
UDFEighteen = RespParty
UDFNineteen = UniqueID
UDFTwenty = UDF20
Val_Base_Unit = BaseUnit
Val_BudgetedHours_Ind = BudgetMHs
Val_BudgetedHours_Group = BudgetHoursGroup
Val_BudgetedHours_ROC = BudgetHoursROC
Val_EarnedHours_ROC = EarnedMHsRoc
Val_EarnedHours_Ind = EarnMHsCalc
Val_EarnedQty = EarnQtyEntry
Val_Earn_Qty = EarnedQtyCalc
Val_EQ-QTY = EquivQTY
Val_EQ_UOM = EquivUOM
Val_Perc_Complete = PercentEntry
Val_Percent_Earned = PercentCompleteCalc
Val_Quantity = Quantity
Tag_ROC_ID = ROCID
LookUP_ROC_ID = ROCLookupID
Val_ROC_Perc = ROCPercent
Val_ROC_BudgetQty = ROCBudgetQTY
Val_Pipe_Size1 = PipeSize1
Val_Pipe_Size2 = PipeSize2
Val_Prev_Earned_Hours = PrevEarnMHs
Val_Prev_Earned_Qty = PrevEarnQTY
Val_TimeStamp = WeekEndDate
Val_UOM = UOM
VAL_Client_EQ-QTY_BDG = ClientEquivQty
VAL_UDF_Two = ClientBudget
VAL_UDF_Three = ClientCustom3
```

---

## 2. IMPORTED FROM EXCEL COLUMNS

These are the columns that are read during Excel import operations.  
**Source:** `ExcelImporter.cs` - `BuildColumnMap` method  
**Total Count:** 82 columns  
**Note:** All other Milestone columns are skipped either because they don't exist in the Excel file or they are calculated fields that the application computes automatically.  
**Format:** OldVantage Name = Milestone Column Name

```
HexNO = HexNO
Catg_ComponentType = CompType
Catg_PhaseCategory = PhaseCategory
Catg_ROC_Step = ROCStep
Dwg_PrimeDrawingNO = DwgNO
Dwg_RevisionNo = RevNO
Dwg_SecondaryDrawingNO = SecondDwgNO
Dwg_ShtNo = ShtNO
Notes_Comments = Notes
Sch_Actno = SecondActno
Sch_Start = SchStart
Sch_Finish = SchFinish
Sch_Status = Status
Tag_Aux1 = Aux1
Tag_Aux2 = Aux2
Tag_Aux3 = Aux3
Tag_Area = Area
Tag_CONo = ChgOrdNO
Tag_Descriptions = Description
Tag_EqmtNo = EqmtNO
Tag_Estimator = Estimator
Tag_Insulation_Typ = InsulType
Tag_LineNo = LineNO
Tag_Matl_Spec = MtrlSpec
Tag_Phase Code = PhaseCode
Tag_Paint_Code = PaintCode
Tag_Pipe_Grade = PipeGrade
Tag_ProjectID = ProjectID
Tag_RFINo = RFINO
Tag_Sch_ActNo = SchedActNO
Tag_Service = Service
Tag_ShopField = ShopField
Tag_SubArea = SubArea
Tag_System = PjtSystem
Tag_SystemNo = SystemNO
Tag_TagNo = TagNO
Tag_Tracing = HtTrace
Tag_WorkPackage = WorkPackage
Tag_XRAY = XRay
Trg_DateTrigger = DateTrigger
UDFOne = UDF1
UDFTwo = UDF2
UDFThree = UDF3
UDFFour = UDF4
UDFFive = UDF5
UDFSix = UDF6
UDFSeven = UDF7
UDFEight = UDF8
UDFNine = UDF9
UDFTen = UDF10
UDFEleven = AssignedTo
UDFTwelve = UDF12
UDFThirteen = CreatedBy
UDFFourteen = UDF14
UDFFifteen = UDF15
UDFSixteen = UDF16
UDFSeventeen = UDF17
UDFEighteen = RespParty
UDFNineteen = UniqueID
UDFTwenty = UDF20
Val_Base_Unit = BaseUnit
Val_BudgetedHours_Ind = BudgetMHs
Val_BudgetedHours_Group = BudgetHoursGroup
Val_BudgetedHours_ROC = BudgetHoursROC
Val_EarnedHours_ROC = EarnedMHsRoc
Val_EarnedQty = EarnQtyEntry
Val_EQ-QTY = EquivQTY
Val_EQ_UOM = EquivUOM
Val_Perc_Complete = PercentEntry
Val_Quantity = Quantity
Tag_ROC_ID = ROCID
Val_ROC_Perc = ROCPercent
Val_ROC_BudgetQty = ROCBudgetQTY
Val_Pipe_Size1 = PipeSize1
Val_Pipe_Size2 = PipeSize2
Val_Prev_Earned_Hours = PrevEarnMHs
Val_Prev_Earned_Qty = PrevEarnQTY
Val_TimeStamp = WeekEndDate
Val_UOM = UOM
VAL_Client_EQ-QTY_BDG = ClientEquivQty
VAL_UDF_Two = ClientBudget
VAL_UDF_Three = ClientCustom3
```

---

## 3. DATABASE COLUMNS

These are all columns in the Activities table (both Local and Central databases).  
**Source:** `DatabaseSetup.cs` - Activities table CREATE statement  
**Total Count:** 91 columns  
**Note:** These use NewVantage naming convention (PascalCase), which also matches C# property names.

```
UniqueID
ActivityID
Area
AssignedTo
AzureUploadUtcDate
Aux1
Aux2
Aux3
BaseUnit
BudgetHoursGroup
BudgetHoursROC
BudgetMHs
ChgOrdNO
ClientBudget
ClientCustom3
ClientEquivEarnQTY
ClientEquivQty
CompType
CreatedBy
DateTrigger
Description
DwgNO
EarnQtyEntry
EarnedMHsRoc
EqmtNO
EquivQTY
EquivUOM
Estimator
HexNO
HtTrace
InsulType
LineNO
LocalDirty
MtrlSpec
Notes
PaintCode
PercentEntry
PhaseCategory
PhaseCode
PipeGrade
PipeSize1
PipeSize2
PrevEarnMHs
PrevEarnQTY
ProgDate
ProjectID
Quantity
RevNO
RFINO
ROCBudgetQTY
ROCID
ROCPercent
ROCStep
SchedActNO
SchFinish
SchStart
SecondActno
SecondDwgNO
Service
ShopField
ShtNO
SubArea
PjtSystem
SystemNO
TagNO
UDF1
UDF2
UDF3
UDF4
UDF5
UDF6
UDF7
UDF8
UDF9
UDF10
UDF11
UDF12
UDF13
UDF14
UDF15
UDF16
UDF17
RespParty
UDF20
UpdatedBy
UpdatedUtcDate
UOM
WeekEndDate
WorkPackage
XRay
SyncVersion
```

---

## 4. GRID COLUMNS

These are all columns defined in the ProgressView SfDataGrid.  
**Source:** `ProgressView.xaml` - GridTextColumn and GridNumericColumn definitions  
**Total Count:** 95 columns (after removing ClientEquivEarnQTY ghost column)  
**Note:** Includes all 90 database columns PLUS 5 calculated fields (EarnMHsCalc, Status, EarnedQtyCalc, PercentCompleteCalc, ROCLookupID). Many columns are hidden by default (IsHidden="True").

```
ActivityID
Area
AssignedTo
Aux1
Aux2
Aux3
AzureUploadUtcDate
BaseUnit
BudgetHoursGroup
BudgetHoursROC
BudgetMHs
ChgOrdNO
ClientBudget
ClientCustom3
ClientEquivQty
CompType
CreatedBy
DateTrigger
Description
DwgNO
EarnMHsCalc
EarnQtyEntry
EarnedMHsRoc
EarnedQtyCalc
EqmtNO
EquivQTY
EquivUOM
Estimator
HexNO
HtTrace
InsulType
LineNO
LocalDirty
MtrlSpec
Notes
PaintCode
PercentCompleteCalc
PercentEntry
PhaseCategory
PhaseCode
PipeGrade
PipeSize1
PipeSize2
PjtSystem
PrevEarnMHs
PrevEarnQTY
ProgDate
ProjectID
Quantity
RFINO
ROCBudgetQTY
ROCID
ROCLookupID
ROCPercent
ROCStep
RevNO
SchFinish
SchStart
SchedActNO
SecondActno
SecondDwgNO
Service
ShopField
ShtNO
Status
SubArea
SyncVersion
SystemNO
TagNO
UDF1
UDF2
UDF3
UDF4
UDF5
UDF6
UDF7
UDF8
UDF9
UDF10
UDF11
UDF12
UDF13
UDF14
UDF15
UDF16
UDF17
RespParty
UDF20
UOM
UniqueID
UpdatedBy
UpdatedUtcDate
WeekEndDate
WorkPackage
XRay
```

---

## 5. CALCULATED FIELDS

**Total Count:** 5 fields  
**Description:** Properties computed by the application, not stored in database or imported from Excel  
**Source:** `Activity.cs` model - properties with calculation logic  
**Note:** These fields are read-only and automatically recalculated when their dependent properties change

- **Status** - Calculated based on PercentEntry value (Not Started, In Progress, Complete)
- **EarnMHsCalc** - Calculated: BudgetMHs Ã— (PercentEntry / 100)
- **EarnedQtyCalc** - Calculated: Quantity Ã— (PercentEntry / 100)
- **PercentCompleteCalc** - Same as PercentEntry (provided for compatibility/display)
- **ROCLookupID** - Calculated lookup combining ROCID and other ROC-related fields

---

## 6. ACTION-BASED FIELDS (System-Managed)

**Total Count:** 12 fields  
**Description:** Fields automatically set by system operations, not directly editable by users  
**Source:** Activity model and database triggers

- **UniqueID** - Generated on import/creation, never changes, used for sync matching
- **ActivityID** - Auto-assigned by Central database on first sync (0 until synced)
- **AssignedTo** - Set when user assigns records to themselves or others
- **LocalDirty** - Set to 1 on any edit, cleared to 0 after successful sync
- **UpdatedUtcDate** - Set to current UTC timestamp on any edit
- **UpdatedBy** - Set to current username on any edit
- **CreatedDate** - Set to current timestamp when record is created
- **CreatedBy** - Set to username when record is created
- **WeekEndDate** - Calculated from ProgDate
- **ProgDate** - Calculated from UpdatedUtcDate
- **AzureUploadUtcDate** - Set when record is uploaded to Azure (future feature)
- **SyncVersion** - Auto-incremented by Central database trigger on INSERT/UPDATE

---

## 7. UPLOADED TO AZURE COLUMNS

These are the columns that will be uploaded to Azure SQL database using Azure naming convention.  
**Source:** Azure SQL schema mappings  
**Total Count:** 79 columns (78 from Milestone mappings + 1 generated field: UserID)  
**Note:** Most Azure names match OldVantage names, with a few exceptions. ClientEquivEarnQTY and UserID are special cases.

**Milestone Column â†’ Azure Column Name:**

```
App.CurrentUser.Username = UserID
Area = Tag_Area
Aux1 = Tag_Aux1
Aux2 = Tag_Aux2
Aux3 = Tag_Aux3
AzureUploadUtcDate = Timestamp
BaseUnit = Val_Base_Unit
BudgetHoursGroup = Val_BudgetedHours_Group
BudgetHoursROC = Val_BudgetedHours_ROC
BudgetMHs = Val_BudgetedHours_Ind
ChgOrdNO = Tag_CONo
ClientBudget = VAL_UDF_Two
ClientCustom3 = VAL_UDF_Three
ClientEquivEarnQTY = VAL_Client_Earned_EQ-QTY
ClientEquivQty = Val_Client_Eq_Qty_Bdg
CompType = Catg_ComponentType
Description = Tag_Descriptions
DwgNO = Dwg_PrimeDrawingNO
EarnMHsCalc = Val_EarnedHours_Ind
EarnQtyEntry = Val_EarnedQty
EqmtNO = Tag_EqmtNo
EquivQTY = Val_EQ-QTY
EquivUOM = Val_EQ_UOM
Estimator = Tag_Estimator
HexNO = HexNO
HtTrace = Tag_Tracing
InsulType = Tag_Insulation_Typ
LineNO = Tag_LineNo
MtrlSpec = Tag_Matl_Spec
Notes = Notes_Comments
PaintCode = Tag_Paint_Code
PercentEntry = Val_Perc_Complete
PhaseCategory = Catg_PhaseCategory
PhaseCode = Tag_PhaseCode
PipeGrade = Tag_Pipe_Grade
PipeSize1 = Val_Pipe_Size1
PipeSize2 = Val_Pipe_Size2
PjtSystem = Tag_System
ProgDate = Val_ProgDate
ProjectID = Tag_ProjectID
Quantity = Val_Quantity
RFINO = Tag_RFINo
ROCBudgetQTY = Val_ROC_BudgetQty
ROCStep = Catg_ROC_Step
RevNO = Dwg_RevisionNo
SchFinish = Sch_Finish
SchStart = Sch_Start
SchedActNO = Tag_Sch_ActNo
SecondActno = Sch_Actno
SecondDwgNO = Dwg_SecondaryDrawingNO
Service = Tag_Service
ShopField = Tag_ShopField
ShtNO = Dwg_ShtNo
Status = Sch_Status
SubArea = Tag_SubArea
SystemNO = Tag_SystemNo
TagNO = Tag_TagNo
UDF1 = UDF1
UDF2 = UDF2
UDF3 = UDF3
UDF4 = UDF4
UDF5 = UDF5
UDF6 = UDF6
UDF7 = UDF7
UDF8 = UDF8
UDF9 = UDF9
UDF10 = UDF10
UDF11 = UDF11
UDF12 = UDF12
UDF13 = UDF13
UDF14 = UDF14
UDF15 = UDF15
UDF16 = UDF16
UDF17 = UDF17
RespParty = RespParty
UDF20 = UDF20
UOM = Val_UOM
UniqueID = UDF19
WeekEndDate = Val_TimeStamp
WorkPackage = Tag_WorkPackage
XRay = Tag_XRAY
```

**Special Fields:**
- **ClientEquivEarnQTY** - Ghost column in database (DEFAULT 0), not in grid. Will be auto-populated with 0 during Azure upload. Maps to `VAL_Client_Earned_EQ-QTY` in Azure.
- **UserID** - Not in database or grid. Created during upload and set to current username. Maps to `UserID` in Azure.

**Key Differences from OldVantage Names:**
- `PjtSystem` (Milestone) â†’ `Tag_System` (Azure) - NOT `Tag_SystemNo`
- `SystemNO` (Milestone) â†’ `Tag_SystemNo` (Azure)
- `UniqueID` (Milestone) â†’ `UDF19` (Azure) - Stored in UDF field on Azure side
- `AzureUploadUtcDate` (Milestone) â†’ `Timestamp` (Azure)
- Several calculated fields are uploaded: `EarnMHsCalc`, `Status`, `ProgDate`, `WeekEndDate`

---

## NOTES

**Column Name Conventions:**
- **OldVantage Names:** Used in Excel import/export (e.g., "Tag_ProjectID", "Catg_ComponentType")
- **NewVantage/Database Names:** Used in database and C# code (e.g., "ProjectID", "CompType")
- **C# Property Names:** PascalCase matching database names (e.g., activity.ProjectID)

**Mapping Table:**
- ColumnMappings table in database stores the relationship between OldVantage â†” NewVantage names
- Used by ColumnMapper utility class for translation during import/export

---

**END OF DOCUMENT**