using System;
using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Seeds the ColumnMappings table with default mappings from Excel structure
    /// </summary>
    public static class ColumnMappingsSeeder
    {
        /// <summary>
        /// Seed ColumnMappings table if empty
        /// </summary>
        public static void SeedIfEmpty()
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // Check if table is already seeded
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM ColumnMappings";
                var count = (long)checkCommand.ExecuteScalar();

                if (count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"→ ColumnMappings already seeded ({count} rows)");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("→ Seeding ColumnMappings table...");

                var mappings = GetColumnMappings();

                using var transaction = connection.BeginTransaction();

                foreach (var mapping in mappings)
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = @"
                    INSERT INTO ColumnMappings 
                    (DbColumnName, OldVantageName, DefaultDisplayName, AzureName, DataType, IsEditable, IsCalculated, Notes)
                    VALUES (@db, @old, @display, @azure, @type, @editable, @calculated, @notes)";

                    // ALL columns can be NULL
                    insertCommand.Parameters.AddWithValue("@db",
                        string.IsNullOrEmpty(mapping.DbColumnName) ? DBNull.Value : (object)mapping.DbColumnName);
                    insertCommand.Parameters.AddWithValue("@old",
                        string.IsNullOrEmpty(mapping.OldVantageName) ? DBNull.Value : (object)mapping.OldVantageName);
                    insertCommand.Parameters.AddWithValue("@display",
                        string.IsNullOrEmpty(mapping.DefaultDisplayName) ? DBNull.Value : (object)mapping.DefaultDisplayName);
                    insertCommand.Parameters.AddWithValue("@azure",
                        string.IsNullOrEmpty(mapping.AzureName) ? DBNull.Value : (object)mapping.AzureName);
                    insertCommand.Parameters.AddWithValue("@type",
                        string.IsNullOrEmpty(mapping.DataType) ? DBNull.Value : (object)mapping.DataType);
                    insertCommand.Parameters.AddWithValue("@notes",
                        string.IsNullOrEmpty(mapping.Notes) ? DBNull.Value : (object)mapping.Notes);

                    insertCommand.Parameters.AddWithValue("@editable", mapping.IsEditable ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("@calculated", mapping.IsCalculated ? 1 : 0);

                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine($"✓ Seeded {mappings.Count} column mappings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error seeding ColumnMappings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all column mappings (from Excel structure)
        /// </summary>
        private static List<ColumnMappingDefinition> GetColumnMappings()
        {
            return new List<ColumnMappingDefinition>
            {
                // IDs
                new ColumnMappingDefinition("ActivityID", null, "ActivityID", null, "int", false, false, "Primary key"),
                new ColumnMappingDefinition("HexNO", "HexNO", "HexNO", "HexNO", "int", true, false, null),
                
                // Categories
                new ColumnMappingDefinition("Catg_ComponentType", "Catg_ComponentType", "CompType", "Catg_ComponentType", "string", true, false, null),
                new ColumnMappingDefinition("Catg_PhaseCategory", "Catg_PhaseCategory", "PhaseCatagory", "Catg_PhaseCategory", "string", true, false, null),
                new ColumnMappingDefinition("Catg_ROC_Step", "Catg_ROC_Step", "ROCStep", "Catg_ROC_Step", "string", true, false, null),
                
                // Drawings
                new ColumnMappingDefinition("Dwg_PrimeDrawingNO", "Dwg_PrimeDrawingNO", "DwgNO", "Dwg_PrimeDrawingNO", "string", true, false, null),
                new ColumnMappingDefinition("Dwg_RevisionNo", "Dwg_RevisionNo", "RevNO", "Dwg_RevisionNo", "string", true, false, null),
                new ColumnMappingDefinition("Dwg_SecondaryDrawingNO", "Dwg_SecondaryDrawingNO", "SecondDwgNO", "Dwg_SecondaryDrawingNO", "string", true, false, null),
                new ColumnMappingDefinition("Dwg_ShtNo", "Dwg_ShtNo", "ShtNO", "Dwg_ShtNo", "string", true, false, null),
                
                // Notes
                new ColumnMappingDefinition("Notes_Comments", "Notes_Comments", "Notes", "Notes_Comments", "string", true, false, null),
                
                // Schedule
                new ColumnMappingDefinition("Sch_Actno", "Sch_Actno", "OldActno", "Sch_Actno", "string", true, false, null),
                new ColumnMappingDefinition("Sch_Start", "Sch_Start", "Start", "Sch_Start", "datetime", true, false, null),
                new ColumnMappingDefinition("Sch_Finish", "Sch_Finish", "Finish", null, "datetime", true, false, null),
                new ColumnMappingDefinition("Sch_Status", "Sch_Status", "Status", "Sch_Status", "string", false, true, "Calculated from percent complete"),
                // Tags - Core
                new ColumnMappingDefinition("Tag_TagNo", "Tag_TagNo", "TagNO", "Tag_TagNo", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Descriptions", "Tag_Descriptions", "Description", "Tag_Descriptions", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Area", "Tag_Area", "Area", "Tag_Area", "string", true, false, null),
                new ColumnMappingDefinition("Tag_SubArea", "Tag_SubArea", "SubArea", "Tag_SubArea", "string", true, false, null),
                new ColumnMappingDefinition("Tag_System", "Tag_System", "System", "Tag_System", "string", true, false, null),
                new ColumnMappingDefinition("Tag_SystemNo", "Tag_SystemNo", "SystemNO", null, "string", true, false, null),
                new ColumnMappingDefinition("Tag_ProjectID", "Tag_ProjectID", "ProjectID", "Tag_ProjectID", "string", true, false, null),
                new ColumnMappingDefinition("Tag_WorkPackage", "Tag_WorkPackage", "WorkPackage", "Tag_WorkPackage", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Phase_Code", "Tag_Phase Code", "PhaseCode", "Tag_PhaseCode", "string", true, false, null), // Space in OldVantage
                new ColumnMappingDefinition("Tag_Service", "Tag_Service", "Service", "Tag_Service", "string", true, false, null),
                new ColumnMappingDefinition("Tag_ShopField", "Tag_ShopField", "ShopField", "Tag_ShopField", "string", true, false, null),
                
                // Tags - Equipment/Line
                new ColumnMappingDefinition("Tag_EqmtNo", "Tag_EqmtNo", "EqmtNO", "Tag_EqmtNo", "string", true, false, null),
                new ColumnMappingDefinition("Tag_LineNo", "Tag_LineNo", "LineNO", "Tag_LineNo", "string", true, false, null),
                new ColumnMappingDefinition("Tag_CONo", "Tag_CONo", "ChgOrdNO", "Tag_CONo", "string", true, false, null),
                
                // Tags - Material
                new ColumnMappingDefinition("Tag_Matl_Spec", "Tag_Matl_Spec", "MtrlSpec", "Tag_Matl_Spec", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Pipe_Grade", "Tag_Pipe_Grade", "PipeGrade", "Tag_Pipe_Grade", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Paint_Code", "Tag_Paint_Code", "PaintCode", "Tag_Paint_Code", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Insulation_Typ", "Tag_Insulation_Typ", "InsulType", "Tag_Insulation_Typ", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Tracing", "Tag_Tracing", "HtTrace", "Tag_Tracing", "string", true, false, null),
                
                // Tags - Auxiliary
                new ColumnMappingDefinition("Tag_Aux1", "Tag_Aux1", "Aux1", "Tag_Aux1", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Aux2", "Tag_Aux2", "Aux2", "Tag_Aux2", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Aux3", "Tag_Aux3", "Aux3", "Tag_Aux3", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Estimator", "Tag_Estimator", "Estimator", "Tag_Estimator", "string", true, false, null),
                new ColumnMappingDefinition("Tag_RFINo", "Tag_RFINo", "RFINO", "Tag_RFINo", "string", true, false, null),
                new ColumnMappingDefinition("Tag_Sch_ActNo", "Tag_Sch_ActNo", "SchedActNO", "Tag_Sch_ActNo", "string", true, false, null),
                new ColumnMappingDefinition("Tag_XRAY", "Tag_XRAY", "XRay", "Tag_XRAY", "double", true, false, null),
                
                // Trigger
                new ColumnMappingDefinition("Trg_DateTrigger", "Trg_DateTrigger", "DateTrigger", null, "int", true, false, null),
                // UDFs
                new ColumnMappingDefinition("UDFOne", "UDFOne", "UDF1", "UDF1", "string", true, false, null),
                new ColumnMappingDefinition("UDFTwo", "UDFTwo", "UDF2", "UDF2", "string", true, false, null),
                new ColumnMappingDefinition("UDFThree", "UDFThree", "UDF3", "UDF3", "string", true, false, null),
                new ColumnMappingDefinition("UDFFour", "UDFFour", "UDF4", "UDF4", "string", true, false, null),
                new ColumnMappingDefinition("UDFFive", "UDFFive", "UDF5", "UDF5", "string", true, false, null),
                new ColumnMappingDefinition("UDFSix", "UDFSix", "UDF6", "UDF6", "string", true, false, null),
                new ColumnMappingDefinition("UDFSeven", "UDFSeven", "UDF7", "UDF7", "int", true, false, null),
                new ColumnMappingDefinition("UDFEight", "UDFEight", "UDF8", "UDF8", "string", true, false, null),
                new ColumnMappingDefinition("UDFNine", "UDFNine", "UDF9", "UDF9", "string", true, false, null),
                new ColumnMappingDefinition("UDFTen", "UDFTen", "UDF10", "UDF10", "string", true, false, null),
                new ColumnMappingDefinition("UDFEleven", "UDFEleven", "AssignedTo", "UDF11", "string", true, false, "Assigned user"),
                new ColumnMappingDefinition("UDFTwelve", "UDFTwelve", "LastModifiedBy", "UDF12", "string", false, false, "Auto-updated on save"),
                new ColumnMappingDefinition("UDFThirteen", "UDFThirteen", "CreatedBy", "UDF13", "string", false, false, "Set on creation"),
                new ColumnMappingDefinition("UDFFourteen", "UDFFourteen", "UDF14", "UDF14", "string", true, false, null),
                new ColumnMappingDefinition("UDFFifteen", "UDFFifteen", "UDF15", "UDF15", "string", true, false, null),
                new ColumnMappingDefinition("UDFSixteen", "UDFSixteen", "UDF16", "UDF16", "string", true, false, null),
                new ColumnMappingDefinition("UDFSeventeen", "UDFSeventeen", "UDF17", "UDF17", "string", true, false, null),
                new ColumnMappingDefinition("UDFEighteen", "UDFEighteen", "UDF18", "UDF18", "string", true, false, null),
                new ColumnMappingDefinition("UDFNineteen", "UDFNineteen", "UniqueID", "UDF19", "string", false, false, "Unique activity identifier - READ ONLY"),
                new ColumnMappingDefinition("UDFTwenty", "UDFTwenty", "UDF20", "UDF20", "string", true, false, null),
                // Values - Budgeted
                new ColumnMappingDefinition("Val_Base_Unit", "Val_Base_Unit", "BaseUnit", "Val_Base_Unit", "double", true, false, null),
                new ColumnMappingDefinition("Val_BudgetedHours_Ind", "Val_BudgetedHours_Ind", "BudgetMHs", "Val_BudgetedHours_Ind", "double", true, false, null),
                new ColumnMappingDefinition("Val_BudgetedHours_Group", "Val_BudgetedHours_Group", "BudgetHoursGroup", "Val_BudgetedHours_Group", "double", true, false, null),
                new ColumnMappingDefinition("Val_BudgetedHours_ROC", "Val_BudgetedHours_ROC", "BudgetHoursROC", "Val_BudgetedHours_ROC", "double", true, false, null),
                
                // Values - Progress (User Editable)
                new ColumnMappingDefinition("Val_EarnedQty", "Val_EarnedQty", "EarnQtyEntry", "Val_EarnedQty", "double", true, false, "User enters quantity earned"),
                new ColumnMappingDefinition("Val_Perc_Complete", "Val_Perc_Complete", "PercentEntry", "Val_Perc_Complete", "double", true, false, "User enters percent complete"),
                new ColumnMappingDefinition("Val_Quantity", "Val_Quantity", "Quantity", "Val_Quantity", "double", true, false, null),
                new ColumnMappingDefinition("Val_UOM", "Val_UOM", "UOM", "Val_UOM", "string", true, false, null),
                
                // Values - Calculated
                new ColumnMappingDefinition("Val_EarnedHours_Ind", "Val_EarnedHours_Ind", "EarnMHsCalc", "Val_EarnedHours_Ind", "double", false, true, "Calculated earned hours"),
                new ColumnMappingDefinition("Val_EarnedHours_ROC", "Val_EarnedHours_ROC", "EarnedMHsRoc", null, "int", false, true, "Calculated ROC earned hours"),
                new ColumnMappingDefinition("Val_Earn_Qty", "Val_Earn_Qty", "EarnedQtyCalc", null, "double", false, true, "Calculated earned quantity"),
                new ColumnMappingDefinition("Val_Percent_Earned", "Val_Percent_Earned", "PercentCompleteCalc", null, "double", false, true, "Calculated percent complete"),
                
                // Values - Equipment
                new ColumnMappingDefinition("Val_EQ_QTY", "Val_EQ-QTY", "EquivQTY", "Val_EQ-QTY", "double", true, false, null), // Hyphen in OldVantage/Azure
                new ColumnMappingDefinition("Val_EQ_UOM", "Val_EQ_UOM", "EquivUOM", "Val_EQ_UOM", "string", true, false, null),
                
                // Values - ROC
                new ColumnMappingDefinition("Tag_ROC_ID", "Tag_ROC_ID", "ROCID", null, "int", true, false, null),
                new ColumnMappingDefinition("LookUP_ROC_ID", "LookUP_ROC_ID", "ROCLookupID", null, "string", false, true, "Calculated lookup key"),
                new ColumnMappingDefinition("Val_ROC_Perc", "Val_ROC_Perc", "ROCPercent", null, "double", true, false, null),
                new ColumnMappingDefinition("Val_ROC_BudgetQty", "Val_ROC_BudgetQty", "ROCBudgetQTY", "Val_ROC_BudgetQty", "double", true, false, null),
                
                // Values - Pipe
                new ColumnMappingDefinition("Val_Pipe_Size1", "Val_Pipe_Size1", "PipeSize1", "Val_Pipe_Size1", "double", true, false, null),
                new ColumnMappingDefinition("Val_Pipe_Size2", "Val_Pipe_Size2", "PipeSize2", "Val_Pipe_Size2", "double", true, false, null),
                
                // Values - History
                new ColumnMappingDefinition("Val_Prev_Earned_Hours", "Val_Prev_Earned_Hours", "PrevEarnMHs", null, "double", true, false, null),
                new ColumnMappingDefinition("Val_Prev_Earned_Qty", "Val_Prev_Earned_Qty", "PrevEarnQTY", null, "double", true, false, null),
                new ColumnMappingDefinition("Val_TimeStamp", "Val_TimeStamp", "WeekEndDate", "Val_TimeStamp", "datetime", false, false, "Timestamp when progress submitted"),
                
                // Values - Client
                new ColumnMappingDefinition("Val_Client_EQ_QTY_BDG", "VAL_Client_EQ-QTY_BDG", "ClientEquivQty", "Val_Client_Eq_Qty_Bdg", "double", true, false, null), // Hyphen in OldVantage/Azure
                new ColumnMappingDefinition("Val_UDF_Two", "Val_UDF_Two", "ClientBudget", "Val_UDF_Two", "double", true, false, null),
                new ColumnMappingDefinition("Val_UDF_Three", "Val_UDF_Three", "ClientCustom3", "Val_UDF_Three", "double", true, false, null),
                new ColumnMappingDefinition("VAL_Client_Earned_EQ_QTY", "VAL_Client_Earned_EQ-QTY", "ClientEquivEarnQTY", "VAL_Client_Earned_EQ-QTY", "double", false, true, "Calculated client earned qty"), // Hyphen in OldVantage/Azure
                
                // New Columns (NewVantage only)
                new ColumnMappingDefinition("AzureUploadDate", null, "AzureUploadDate", "Timestamp", "datetime", false, false, "When user submits to Azure - official date used in Power BI"),
                new ColumnMappingDefinition("Val_ProgDate", null, "ProgDate", "Val_ProgDate", "datetime", false, false, "Timestamp when user clicks to submit progress to local db"),
                new ColumnMappingDefinition(null, null, null, "UserID", "strign", false, true, "Populated when upload to Azure with getUserName")

            };
        }

        // Helper class for column mapping definition
        private class ColumnMappingDefinition
        {
            public string DbColumnName { get; }
            public string OldVantageName { get; }
            public string DefaultDisplayName { get; }
            public string AzureName { get; }
            public string DataType { get; }
            public bool IsEditable { get; }
            public bool IsCalculated { get; }
            public string Notes { get; }

            public ColumnMappingDefinition(string dbColumnName, string oldVantageName, string defaultDisplayName,
                string azureName, string dataType, bool isEditable, bool isCalculated, string notes)
            {
                DbColumnName = dbColumnName;
                OldVantageName = oldVantageName;
                DefaultDisplayName = defaultDisplayName;
                AzureName = azureName;
                DataType = dataType;
                IsEditable = isEditable;
                IsCalculated = isCalculated;
                Notes = notes;
            }
        }
    }
}