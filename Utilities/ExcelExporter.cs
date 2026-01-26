using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace VANTAGE.Utilities
{
    // Format options for Excel import/export
    public enum ExportFormat
    {
        Legacy,     // OldVantage column names, percentages as 0-1 decimals
        NewVantage  // NewVantage/ColumnName, percentages as 0-100
    }

    // Export activities to Excel - supports both Legacy and NewVantage formats
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
            "Tag_LineNo",
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

        // NewVantage column order (database property names) for modern export format
        private static readonly string[] NewVantageColumnOrder = new[]
        {
            "HexNO",
            "CompType",
            "PhaseCategory",
            "ROCStep",
            "DwgNO",
            "RevNO",
            "SecondDwgNO",
            "ShtNO",
            "Notes",
            "SecondActno",
            "SchStart",
            "SchFinish",
            "Status",
            "Aux1",
            "Aux2",
            "Aux3",
            "Area",
            "ChgOrdNO",
            "Description",
            "EqmtNO",
            "Estimator",
            "InsulType",
            "LineNumber",
            "MtrlSpec",
            "PhaseCode",
            "PaintCode",
            "PipeGrade",
            "ProjectID",
            "RFINO",
            "SchedActNO",
            "Service",
            "ShopField",
            "SubArea",
            "PjtSystem",
            "SystemNO",
            "TagNO",
            "HtTrace",
            "WorkPackage",
            "XRay",
            "DateTrigger",
            "UDF1",
            "UDF2",
            "UDF3",
            "UDF4",
            "UDF5",
            "UDF6",
            "UDF7",
            "UDF8",
            "UDF9",
            "UDF10",
            "UDF11",
            "UDF12",
            "UDF13",
            "UDF14",
            "UDF15",
            "UDF16",
            "UDF17",
            "RespParty",
            "UniqueID",
            "UDF20",
            "BaseUnit",
            "BudgetMHs",
            "BudgetHoursGroup",
            "BudgetHoursROC",
            "EarnedMHsRoc",
            "EarnMHsCalc",
            "EarnedQtyCalc",
            "EarnQtyEntry",
            "EquivQTY",
            "EquivUOM",
            "PercentCompleteCalc",
            "PercentEntry",
            "Quantity",
            "ROCID",
            "ROCLookupID",
            "ROCPercent",
            "ROCBudgetQTY",
            "PipeSize1",
            "PipeSize2",
            "PrevEarnMHs",
            "PrevEarnQTY",
            "UpdatedUtcDate",
            "UOM",
            "ClientEquivQty",
            "ClientBudget",
            "ClientCustom3"
        };

        // Export all activities to Excel file (defaults to Legacy format for backward compatibility)
        public static void ExportActivities(string filePath, List<Models.Activity> activities)
        {
            ExportActivities(filePath, activities, ExportFormat.Legacy);
        }

        // Export all activities to Excel file with specified format
        public static void ExportActivities(string filePath, List<Models.Activity> activities, ExportFormat format)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                if (format == ExportFormat.NewVantage)
                {
                    // NewVantage format: use property names as headers, percentages as 0-100
                    int colIndex = 1;
                    var columnOrder = new List<(string PropertyName, int ExcelColumn)>();

                    foreach (var propertyName in NewVantageColumnOrder)
                    {
                        worksheet.Cell(1, colIndex).Value = propertyName;
                        columnOrder.Add((propertyName, colIndex));
                        colIndex++;
                    }

                    // Write data rows
                    int rowIndex = 2;
                    foreach (var activity in activities)
                    {
                        foreach (var col in columnOrder)
                        {
                            var value = GetActivityValueNewVantage(activity, col.PropertyName);
                            WriteCellValue(worksheet, rowIndex, col.ExcelColumn, value);
                        }
                        rowIndex++;
                    }
                }
                else
                {
                    // Legacy format: OldVantage column names, percentages as 0-1 decimals
                    var oldVantageToDbMapping = GetOldVantageToDbColumnMapping();

                    int colIndex = 1;
                    var columnOrder = new List<(string OldVantageName, string DbColumnName, int ExcelColumn)>();

                    foreach (var oldVantageName in ExportColumnOrder)
                    {
                        worksheet.Cell(1, colIndex).Value = oldVantageName;

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
                            WriteCellValue(worksheet, rowIndex, col.ExcelColumn, value);
                        }
                        rowIndex++;
                    }
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
            catch
            {
                throw;
            }
        }

        // Helper to write cell value with type handling
        private static void WriteCellValue(IXLWorksheet worksheet, int row, int col, object value)
        {
            if (value == null || value.ToString() == "")
            {
                worksheet.Cell(row, col).Value = "";
            }
            else if (value is double doubleValue)
            {
                worksheet.Cell(row, col).Value = doubleValue;
            }
            else if (value is int intValue)
            {
                worksheet.Cell(row, col).Value = intValue;
            }
            else if (value is DateTime dateValue)
            {
                worksheet.Cell(row, col).Value = dateValue;
            }
            else
            {
                worksheet.Cell(row, col).Value = value.ToString();
            }
        }

        // Export empty template (headers only) - defaults to Legacy format
        public static void ExportTemplate(string filePath)
        {
            ExportTemplate(filePath, ExportFormat.Legacy);
        }

        // Export empty template (headers only) with specified format
        public static void ExportTemplate(string filePath, ExportFormat format)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // Write headers only in exact order based on format
                int colIndex = 1;
                var columns = format == ExportFormat.NewVantage ? NewVantageColumnOrder : ExportColumnOrder;

                foreach (var columnName in columns)
                {
                    worksheet.Cell(1, colIndex).Value = columnName;
                    colIndex++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
            catch
            {
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

        // Get activity property value for NewVantage format (no percentage conversion)
        private static object GetActivityValueNewVantage(Models.Activity activity, string propertyName)
        {
            var property = typeof(Models.Activity).GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(activity);

                // Round all double values to 3 decimal places (but keep percentages as 0-100)
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