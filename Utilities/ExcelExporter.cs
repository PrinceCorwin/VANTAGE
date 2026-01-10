using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace VANTAGE.Utilities
{
    // Export activities to Excel using OldVantage column names in exact order
    public static class ExcelExporter
    {
        // EXACT column order as specified - DO NOT REORDER
        private static readonly string[] ExportColumnOrder = new[]
        {
            "HexNO",
            "Catg_ComponentType",
            "Catg_PhaseCategory",
            "Catg_ROC_Step",
            "Dwg_PrimeDrawingNO",
            "Dwg_RevisionNo",
            "Dwg_SecondaryDrawingNO",
            "Dwg_ShtNo",
            "Notes_Comments",
            "Sch_Actno",
            "Sch_Start",
            "Sch_Finish",
            "Sch_Status",
            "Tag_Aux1",
            "Tag_Aux2",
            "Tag_Aux3",
            "Tag_Area",
            "Tag_CONo",
            "Tag_Descriptions",
            "Tag_EqmtNo",
            "Tag_Estimator",
            "Tag_Insulation_Typ",
            "Tag_LineNumber",
            "Tag_Matl_Spec",
            "Tag_Phase Code",
            "Tag_Paint_Code",
            "Tag_Pipe_Grade",
            "Tag_ProjectID",
            "Tag_RFINo",
            "Tag_Sch_ActNo",
            "Tag_Service",
            "Tag_ShopField",
            "Tag_SubArea",
            "Tag_System",
            "Tag_SystemNo",
            "Tag_TagNo",
            "Tag_Tracing",
            "Tag_WorkPackage",
            "Tag_XRAY",
            "Trg_DateTrigger",
            "UDFOne",
            "UDFTwo",
            "UDFThree",
            "UDFFour",
            "UDFFive",
            "UDFSix",
            "UDFSeven",
            "UDFEight",
            "UDFNine",
            "UDFTen",
            "UDFEleven",
            "UDFTwelve",
            "UDFThirteen",
            "UDFFourteen",
            "UDFFifteen",
            "UDFSixteen",
            "UDFSeventeen",
            "UDFEighteen",
            "UDFNineteen",
            "UDFTwenty",
            "Val_Base_Unit",
            "Val_BudgetedHours_Ind",
            "Val_BudgetedHours_Group",
            "Val_BudgetedHours_ROC",
            "Val_EarnedHours_ROC",
            "Val_EarnedHours_Ind",
            "Val_EarnedQty",
            "Val_Earn_Qty",
            "Val_EQ-QTY",
            "Val_EQ_UOM",
            "Val_Perc_Complete",
            "Val_Percent_Earned",
            "Val_Quantity",
            "Tag_ROC_ID",
            "LookUP_ROC_ID",
            "Val_ROC_Perc",
            "Val_ROC_BudgetQty",
            "Val_Pipe_Size1",
            "Val_Pipe_Size2",
            "Val_Prev_Earned_Hours",
            "Val_Prev_Earned_Qty",
            "Val_TimeStamp",
            "Val_UOM",
            "VAL_Client_EQ-QTY_BDG",
            "VAL_UDF_Two",
            "VAL_UDF_Three"
        };

        // Export all activities to Excel file
        public static void ExportActivities(string filePath, List<Models.Activity> activities)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // Get column mappings from database (OldVantage -> DbColumnName)
                var oldVantageToDbMapping = GetOldVantageToDbColumnMapping();

                // Write headers in exact order
                int colIndex = 1;
                var columnOrder = new List<(string OldVantageName, string DbColumnName, int ExcelColumn)>();

                foreach (var oldVantageName in ExportColumnOrder)
                {
                    worksheet.Cell(1, colIndex).Value = oldVantageName;

                    // Find the database column name for this OldVantage name
                    if (oldVantageToDbMapping.TryGetValue(oldVantageName, out string? dbColumnName) && dbColumnName != null)
                    {
                        columnOrder.Add((oldVantageName, dbColumnName, colIndex));
                    }

                    colIndex++;
                }

                // Write data rows
                int rowIndex = 2;
                foreach (var activity in activities)
                {
                    foreach (var col in columnOrder)
                    {
                        var value = GetActivityValue(activity, col.DbColumnName);

                        // Handle different value types for ClosedXML
                        if (value == null || value.ToString() == "")
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = "";
                        }
                        else if (value is double doubleValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = doubleValue;
                        }
                        else if (value is int intValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = intValue;
                        }
                        else if (value is DateTime dateValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = dateValue;
                        }
                        else
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = value.ToString();
                        }
                    }
                    rowIndex++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                System.Diagnostics.Debug.WriteLine($"✓ Exported {activities.Count} activities to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Export error: {ex.Message}");
                throw;
            }
        }

        // Export empty template (headers only)
        public static void ExportTemplate(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // Write headers only in exact order
                int colIndex = 1;
                foreach (var oldVantageName in ExportColumnOrder)
                {
                    worksheet.Cell(1, colIndex).Value = oldVantageName;
                    colIndex++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                System.Diagnostics.Debug.WriteLine($"✓ Exported template to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Template export error: {ex.Message}");
                throw;
            }
        }

        // Get mapping of OldVantageName -> DbColumnName from database
        private static Dictionary<string, string> GetOldVantageToDbColumnMapping()
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var connection = DatabaseSetup.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ColumnName, OldVantageName 
                FROM ColumnMappings 
                WHERE OldVantageName IS NOT NULL";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    string dbColumnName = reader.GetString(0);
                    string oldVantageName = reader.GetString(1);
                    mapping[oldVantageName] = dbColumnName;
                }
            }

            return mapping;
        }

        // Get activity property value by database column name
        private static object GetActivityValue(Models.Activity activity, string dbColumnName)
        {
            // Database column names now match property names directly
            string propertyName = dbColumnName;

            // Use reflection to get value
            var property = typeof(Models.Activity).GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(activity);

                // Convert percentage fields from 0-100 to 0-1 decimal format for OldVantage
                if (propertyName == "PercentEntry" ||
                    propertyName == "PercentCompleteCalc" ||
                    propertyName == "EarnedQtyCalc")
                {
                    if (value is double percentValue)
                    {
                        return NumericHelper.RoundToPlaces(percentValue / 100.0);
                    }
                }

                // Handle _Display properties (convert back to 0-1)
                if (propertyName.EndsWith("_Display"))
                {
                    if (value is double displayValue)
                    {
                        return NumericHelper.RoundToPlaces(displayValue / 100.0);
                    }
                }

                // Round all other double values to 3 decimal places
                if (value is double doubleValue)
                {
                    return NumericHelper.RoundToPlaces(doubleValue);
                }

                return value ?? "";
            }

            return "";
        }
    }
}