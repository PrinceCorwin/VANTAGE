using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    public static class ExcelImporter
    {
        // Legacy column name mapping (Excel hyphen or space names → SQLite underscore names)
        private static readonly Dictionary<string, string> LegacyColumnMap = new Dictionary<string, string>
        {
            { "Val_EQ-QTY", "Val_EQ_QTY" },
            { "VAL_Client_EQ-QTY_BDG", "Val_Client_EQ_QTY_BDG" },
            { "VAL_Client_Earned_EQ-QTY", "VAL_Client_Earned_EQ_QTY" },
            // Space → Underscore (ADD THIS)
            { "Tag_Phase Code", "Tag_Phase_Code" }
        };
        // Reverse mapping for export (Database → Excel with legacy names)
        private static readonly Dictionary<string, string> ReverseLegacyColumnMap = new Dictionary<string, string>
        {
            // Underscore → Hyphen (for Excel export)
            { "Val_EQ_QTY", "Val_EQ-QTY" },
            { "Val_Client_EQ_QTY_BDG", "VAL_Client_EQ-QTY_BDG" },
            { "VAL_Client_Earned_EQ_QTY", "VAL_Client_Earned_EQ-QTY" },
    
            // Underscore → Space (for Excel export)
            { "Tag_Phase_Code", "Tag_Phase Code" }
        };

        /// <summary>
        /// Get Excel column name from database column name (for export)
        /// </summary>
        public static string GetExcelColumnName(string dbColumnName)
        {
            return ReverseLegacyColumnMap.ContainsKey(dbColumnName)
                ? ReverseLegacyColumnMap[dbColumnName]
                : dbColumnName;
        }
        /// <summary>
        /// Import activities from Excel file
        /// </summary>
        /// <param name="filePath">Path to Excel file</param>
        /// <param name="replaceMode">True = replace all existing, False = combine/add</param>
        /// <returns>Number of records imported</returns>
        public static int ImportActivities(string filePath, bool replaceMode)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Excel file not found: {filePath}");
                }

                // Open Excel workbook
                using var workbook = new XLWorkbook(filePath);
                // Get Sheet1 by name (more reliable than index)
                var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Sheet1");

                if (worksheet == null)
                {
                    // Fallback: try first worksheet
                    worksheet = workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        throw new Exception("No worksheets found in Excel file");
                    }
                    System.Diagnostics.Debug.WriteLine($"⚠ Could not find 'Sheet1', using '{worksheet.Name}' instead");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"→ Using worksheet: {worksheet.Name}");
                }

                // Get header row to map columns
                var headerRow = worksheet.Row(1);
                var columnMap = BuildColumnMap(headerRow);

                System.Diagnostics.Debug.WriteLine($"→ Found {columnMap.Count} mapped columns in Excel");

                // Read all data rows
                // Read all data rows - use a more robust method
                var activities = new List<Dictionary<string, object>>();

                // Find the actual last row with data by checking a key column (UDFNineteen)
                int lastRow = 1;
                for (int rowNum = 2; rowNum <= 10000; rowNum++) // Check up to 10,000 rows
                {
                    var cell = worksheet.Cell(rowNum, 1); // Check first column
                    if (!cell.IsEmpty())
                    {
                        lastRow = rowNum;
                    }
                    else if (rowNum > lastRow + 100) // If 100 empty rows after last data, stop
                    {
                        break;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"→ Excel has data from row 1 (header) to row {lastRow}");
                System.Diagnostics.Debug.WriteLine($"→ Starting data read from row 2");

                for (int rowNum = 2; rowNum <= lastRow; rowNum++) // Start after header row
                {
                    var row = worksheet.Row(rowNum);

                    // Check if row has any data in mapped columns
                    bool hasAnyData = false;
                    foreach (var mapping in columnMap)
                    {
                        var cell = row.Cell(mapping.Key);
                        if (!cell.IsEmpty())
                        {
                            hasAnyData = true;
                            break;
                        }
                    }

                    if (!hasAnyData)
                    {
                        System.Diagnostics.Debug.WriteLine($"→ Skipping empty row {rowNum}");
                        continue;
                    }

                    var activityData = new Dictionary<string, object>();

                    foreach (var mapping in columnMap)
                    {
                        int excelColNum = mapping.Key;
                        string dbColumnName = mapping.Value;

                        var cell = row.Cell(excelColNum);
                        object value = GetCellValue(cell);

                        activityData[dbColumnName] = value;
                    }

                    activities.Add(activityData);
                    System.Diagnostics.Debug.WriteLine($"→ Read row {rowNum}");
                }

                System.Diagnostics.Debug.WriteLine($"→ Read {activities.Count} activities from Excel");

                // Import to database
                int imported = ImportToDatabase(activities, replaceMode);

                System.Diagnostics.Debug.WriteLine($"✓ Imported {imported} activities successfully!");
                return imported;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Excel import error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Build column mapping from Excel columns to database columns
        /// </summary>
        private static Dictionary<int, string> BuildColumnMap(IXLRow headerRow)
        {
            var columnMap = new Dictionary<int, string>();

            // Calculated fields to skip (app will recalculate these)
            var calculatedFields = new HashSet<string>
            {
                "Val_EarnedHours_Ind",
                "Val_Earn_Qty",
                "Val_Percent_Earned",
                "LookUP_ROC_ID",
                "VAL_Client_Earned_EQ_QTY"
            };

            for (int colNum = 1; colNum <= headerRow.LastCellUsed()?.Address.ColumnNumber; colNum++)
            {
                var cell = headerRow.Cell(colNum);
                string excelColumnName = cell.GetString().Trim();

                if (string.IsNullOrEmpty(excelColumnName))
                    continue;

                // Check if it's a legacy column name that needs mapping
                string dbColumnName = LegacyColumnMap.ContainsKey(excelColumnName)
                    ? LegacyColumnMap[excelColumnName]
                    : excelColumnName;

                // Validate against ColumnMapper (ensures column exists in our schema)
                if (!ColumnMapper.IsValidDbColumn(dbColumnName))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Unknown column in Excel: {excelColumnName} (mapped to {dbColumnName}) - skipping");
                    continue;
                }

                // Skip calculated fields - app will recalculate
                if (calculatedFields.Contains(dbColumnName))
                {
                    System.Diagnostics.Debug.WriteLine($"→ Skipping calculated field: {dbColumnName}");
                    continue;
                }

                columnMap[colNum] = dbColumnName;
            }

            return columnMap;
        }

        /// <summary>
        /// Get cell value with proper type conversion
        /// </summary>
        private static object GetCellValue(IXLCell cell)
        {
            if (cell.IsEmpty())
                return DBNull.Value;

            switch (cell.DataType)
            {
                case XLDataType.Number:
                    return cell.GetDouble();

                case XLDataType.DateTime:
                    return cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss");

                case XLDataType.Boolean:
                    return cell.GetBoolean() ? 1 : 0;

                case XLDataType.Text:
                default:
                    return cell.GetString();
            }
        }

        /// <summary>
        /// Import activities to SQLite database
        /// </summary>
        private static int ImportToDatabase(List<Dictionary<string, object>> activities, bool replaceMode)
        {
            using var connection = DatabaseSetup.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // If replace mode, delete all existing activities
                if (replaceMode)
                {
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Activities";
                    deleteCommand.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("→ Cleared existing activities (Replace mode)");
                }

                int imported = 0;

                foreach (var activityData in activities)
                {
                    // Check for required field: UDFNineteen (Unique Activity ID)
                    if (!activityData.ContainsKey("UDFNineteen") ||
                        activityData["UDFNineteen"] == DBNull.Value ||
                        string.IsNullOrWhiteSpace(activityData["UDFNineteen"]?.ToString()))
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Skipping activity with no UDFNineteen");
                        continue;
                    }

                    string activityID = activityData["UDFNineteen"].ToString();

                    // In combine mode, check if activity already exists
                    if (!replaceMode)
                    {
                        var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = "SELECT COUNT(*) FROM Activities WHERE UDFNineteen = @id";
                        checkCommand.Parameters.AddWithValue("@id", activityID);
                        var exists = (long)checkCommand.ExecuteScalar() > 0;

                        if (exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"→ Skipping duplicate: {activityID}");
                            continue;
                        }
                    }

                    // Build INSERT statement dynamically
                    var columns = new List<string>();
                    var parameters = new List<string>();

                    foreach (var kvp in activityData)
                    {
                        // Use safe column names (strip spaces and special chars for parameter names)
                        string safeParamName = kvp.Key.Replace(" ", "_").Replace("-", "_");
                        columns.Add($"[{kvp.Key}]");  // Wrap column name in brackets
                        parameters.Add($"@{safeParamName}");  // Use safe parameter name
                    }

                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = $@"
                        INSERT INTO Activities ({string.Join(", ", columns)})
                        VALUES ({string.Join(", ", parameters)})";

                    // Add parameters with safe names
                    foreach (var kvp in activityData)
                    {
                        string safeParamName = kvp.Key.Replace(" ", "_").Replace("-", "_");
                        insertCommand.Parameters.AddWithValue($"@{safeParamName}", kvp.Value);
                    }

                    insertCommand.ExecuteNonQuery();
                    imported++;
                }

                transaction.Commit();
                return imported;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"✗ Database import error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get legacy column mapping for display
        /// </summary>
        public static Dictionary<string, string> GetLegacyColumnMapping()
        {
            return new Dictionary<string, string>(LegacyColumnMap);
        }
    }
}