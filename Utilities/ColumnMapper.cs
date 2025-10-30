using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Maps database column names to clean C# property names
    /// Single source of truth for all column name translations
    /// </summary>
    public static class ColumnMapper
    {
        // Database Column Name → Clean Property Name
        private static readonly Dictionary<string, string> _dbToProperty = new Dictionary<string, string>
        {
            // IDs
            ["ActivityID"] = "ActivityID",
            ["HexNO"] = "HexNO",

            // Categories
            ["Catg_ComponentType"] = "CompType",
            ["Catg_PhaseCategory"] = "PhaseCategory",
            ["Catg_ROC_Step"] = "ROCStep",

            // Drawings
            ["Dwg_PrimeDrawingNO"] = "DwgNO",
            ["Dwg_RevisionNo"] = "RevNO",
            ["Dwg_SecondaryDrawingNO"] = "SecondDwgNO",
            ["Dwg_ShtNo"] = "ShtNO",

            // Notes
            ["Notes_Comments"] = "Notes",

            // Schedule
            ["Sch_Actno"] = "OldActno",
            ["Sch_Start"] = "Start",
            ["Sch_Finish"] = "Finish",
            ["Sch_Status"] = "Status",

            // Tags - Core Fields
            ["Tag_TagNo"] = "TagNO",
            ["Tag_Descriptions"] = "Description",
            ["Tag_Area"] = "Area",
            ["Tag_SubArea"] = "SubArea",
            ["Tag_System"] = "System",
            ["Tag_SystemNo"] = "SystemNO",
            ["Tag_ProjectID"] = "ProjectID",
            ["Tag_WorkPackage"] = "WorkPackage",
            ["Tag_Phase_Code"] = "PhaseCode",
            ["Tag_Service"] = "Service",
            ["Tag_ShopField"] = "ShopField",

            // Tags - Equipment/Line
            ["Tag_EqmtNo"] = "EqmtNO",
            ["Tag_LineNo"] = "LineNO",
            ["Tag_CONo"] = "ChgOrdNO",

            // Tags - Material Specs
            ["Tag_Matl_Spec"] = "MtrlSpec",
            ["Tag_Pipe_Grade"] = "PipeGrade",
            ["Tag_Paint_Code"] = "PaintCode",
            ["Tag_Insulation_Typ"] = "InsulType",
            ["Tag_Tracing"] = "HtTrace",

            // Tags - Auxiliary
            ["Tag_Aux1"] = "Aux1",
            ["Tag_Aux2"] = "Aux2",
            ["Tag_Aux3"] = "Aux3",
            ["Tag_Estimator"] = "Estimator",
            ["Tag_RFINo"] = "RFINO",
            ["Tag_Sch_ActNo"] = "SchedActNO",
            ["Tag_XRAY"] = "XRay",

            // Trigger
            ["Trg_DateTrigger"] = "DateTrigger",

            // User-Defined Fields
            ["UDFOne"] = "UDFOne",
            ["UDFTwo"] = "UDFTwo",
            ["UDFThree"] = "UDFThree",
            ["UDFFour"] = "UDFFour",
            ["UDFFive"] = "UDFFive",
            ["UDFSix"] = "UDFSix",
            ["UDFSeven"] = "UDFSeven",
            ["UDFEight"] = "UDFEight",
            ["UDFNine"] = "UDFNine",
            ["UDFTen"] = "UDFTen",
            ["UDFEleven"] = "AssignedTo",           // Special: User assignment
            ["UDFTwelve"] = "LastModifiedBy",       // Special: Last modifier
            ["UDFThirteen"] = "CreatedBy",          // Special: Creator
            ["UDFFourteen"] = "UDFFourteen",
            ["UDFFifteen"] = "UDFFifteen",
            ["UDFSixteen"] = "UDFSixteen",
            ["UDFSeventeen"] = "UDFSeventeen",
            ["UDFEighteen"] = "UDFEighteen",
            ["UDFNineteen"] = "UniqueID",   // Special: Read-only ID
            ["UDFTwenty"] = "UDFTwenty",

            // Values - Budgeted
            ["Val_Base_Unit"] = "BaseUnit",
            ["Val_BudgetedHours_Ind"] = "BudgetMHs",
            ["Val_BudgetedHours_Group"] = "BudgetHoursGroup",
            ["Val_BudgetedHours_ROC"] = "BudgetHoursROC",

            // Values - Earned/Progress
            ["Val_EarnedQty"] = "EarnQtyEntry",
            ["Val_Perc_Complete"] = "PercentEntry",
            ["Val_Quantity"] = "Quantity",
            ["Val_UOM"] = "UOM",

            // Values - Calculated
            ["Val_EarnedHours_Ind"] = "EarnMHsCalc",
            ["Val_EarnedHours_ROC"] = "EarnedMHsRoc",
            ["Val_Earn_Qty"] = "EarnedQtyCalc",
            ["Val_Percent_Earned"] = "PercentCompleteCalc",

            // Values - Equipment
            ["Val_EQ_QTY"] = "EquivQTY",
            ["Val_EQ_UOM"] = "EquivUOM",

            // Values - ROC
            ["Tag_ROC_ID"] = "ROCID",
            ["LookUP_ROC_ID"] = "ROCLookupID",
            ["Val_ROC_Perc"] = "ROCPercent",
            ["Val_ROC_BudgetQty"] = "ROCBudgetQTY",

            // Values - Pipe
            ["Val_Pipe_Size1"] = "PipeSize1",
            ["Val_Pipe_Size2"] = "PipeSize2",

            // Values - Previous/History
            ["Val_Prev_Earned_Hours"] = "PrevEarnMHs",
            ["Val_Prev_Earned_Qty"] = "PrevEarnQTY",
            ["Val_TimeStamp"] = "WeekEndDate",

            // Values - Client
            ["VAL_Client_EQ_QTY_BDG"] = "ClientEquivQty",
            ["VAL_UDF_Two"] = "ClientBudget",
            ["VAL_UDF_Three"] = "ClientCustom3",
            ["VAL_Client_Earned_EQ_QTY"] = "ClientEquivEarnQTY"
        };

        // Reverse mapping: Clean Property Name → Database Column Name
        private static Dictionary<string, string> _propertyToDb;

        static ColumnMapper()
        {
            // Build reverse mapping
            _propertyToDb = new Dictionary<string, string>();
            foreach (var kvp in _dbToProperty)
            {
                _propertyToDb[kvp.Value] = kvp.Key;
            }
        }
        /// <summary>
        /// Check if a database column name is valid
        /// </summary>
        public static bool IsValidDbColumn(string dbColumnName)
        {
            return _dbToProperty.ContainsKey(dbColumnName);
        }

        /// <summary>
        /// Check if a property name is valid
        /// </summary>
        public static bool IsValidProperty(string propertyName)
        {
            return _propertyToDb.ContainsKey(propertyName);
        }

        /// <summary>
        /// Get a mapping dictionary: Database Column Name → Property Name
        /// </summary>
        public static Dictionary<string, string> GetDbToPropertyMap()
        {
            return new Dictionary<string, string>(_dbToProperty);
        }

        /// <summary>
        /// Get a mapping dictionary: Property Name → Database Column Name
        /// </summary>
        public static Dictionary<string, string> GetPropertyToDbMap()
        {
            return new Dictionary<string, string>(_propertyToDb);
        }
        /// <summary>
        /// Get clean property name from database column name
        /// </summary>
        public static string GetPropertyName(string dbColumnName)
        {
            return _dbToProperty.TryGetValue(dbColumnName, out var propName) ? propName : dbColumnName;
        }

        /// <summary>
        /// Get database column name from clean property name
        /// </summary>
        public static string GetDbColumnName(string propertyName)
        {
            return _propertyToDb.TryGetValue(propertyName, out var dbName) ? dbName : propertyName;
        }

        /// <summary>
        /// Get all database column names
        /// </summary>
        public static IEnumerable<string> GetAllDbColumnNames()
        {
            return _dbToProperty.Keys;
        }

        /// <summary>
        /// Get all clean property names
        /// </summary>
        public static IEnumerable<string> GetAllPropertyNames()
        {
            return _dbToProperty.Values;
        }
    }
}