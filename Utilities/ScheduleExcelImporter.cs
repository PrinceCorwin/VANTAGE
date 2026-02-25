using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Utilities
{
    // Import P6 schedule data from Excel TASK sheet
    // Handles 2 header rows (row 1 = field names, row 2 = descriptions)
    // Data starts at row 3
    public static class ScheduleExcelImporter
    {
        // Primary headers (row 1) - technical field names that vary by P6 configuration
        private static readonly Dictionary<string, string> PrimaryHeaderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "task_code", "SchedActNO" },
            { "wbs_id", "WbsId" },
            { "task_name", "Description" },
            { "start_date", "P6_Start" },
            { "target_start_date", "P6_Start" },
            { "end_date", "P6_Finish" },
            { "target_end_date", "P6_Finish" },
            { "act_start_date", "P6_ActualStart" },
            { "act_end_date", "P6_ActualFinish" },
            { "complete_pct", "P6_PercentComplete" },
            { "target_work_qty", "P6_BudgetMHs" },
            { "status_code", "StatusCode" }
        };

        // Secondary headers (row 2) - display names, more consistent across P6 exports
        // Normalized by stripping (*) prefix and unit suffixes like (h), (%), (d)
        private static readonly Dictionary<string, string> SecondaryHeaderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Activity ID", "SchedActNO" },
            { "WBS Code", "WbsId" },
            { "Activity Name", "Description" },
            { "Start", "P6_Start" },
            { "Finish", "P6_Finish" },
            { "Actual Start", "P6_ActualStart" },
            { "Actual Finish", "P6_ActualFinish" },
            { "Activity % Complete", "P6_PercentComplete" },
            { "Budgeted Labor Units", "P6_BudgetMHs" },
            { "Activity Status", "StatusCode" }
        };

        // Import P6 schedule file
        public static async Task<int> ImportFromP6Async(
            string filePath, 
            DateTime weekEndDate, 
            List<string> projectIds,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("Opening P6 Excel file...");

                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"P6 file not found: {filePath}");

                    IXLWorkbook workbook;
                    try
                    {
                        workbook = new XLWorkbook(filePath);
                    }
                    catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020))
                    {
                        throw new InvalidOperationException(
                            "The Excel file is currently open in another program.\n\n" +
                            "Please close the file and try again.", ex);
                    }

                    using (workbook)
                    {
                        // Find TASK sheet
                        var worksheet = workbook.Worksheets.FirstOrDefault(ws => 
                            ws.Name.Equals("TASK", StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidOperationException("TASK sheet not found in P6 file.");

                        progress?.Report("Analyzing P6 structure...");

                        // Try secondary headers (row 2) first, fall back to primary (row 1)
                        var columnMap = BuildP6ColumnMap(worksheet);

                        progress?.Report("Reading P6 data...");
                        var scheduleRows = ReadScheduleFromP6(worksheet, columnMap, weekEndDate);

                        progress?.Report($"Importing {scheduleRows.Count} schedule activities...");

                        // Clear existing data for this WeekEndDate and insert new data
                        int imported = ImportToDatabase(scheduleRows, projectIds, weekEndDate);

                        AppLogger.Info($"Imported {imported} P6 activities for {weekEndDate:yyyy-MM-dd}, Projects: {string.Join(",", projectIds)}", 
                            "ScheduleExcelImporter.ImportFromP6Async", 
                            App.CurrentUser?.Username);

                        progress?.Report($"Import complete - {imported} activities");
                        return imported;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleExcelImporter.ImportFromP6Async");
                    throw;
                }
            });
        }

        // Build column mapping - try secondary headers (row 2) first, fall back to primary (row 1)
        private static Dictionary<int, string> BuildP6ColumnMap(IXLWorksheet worksheet)
        {
            var primaryRow = worksheet.Row(1);
            var secondaryRow = worksheet.Row(2);
            int lastCol = primaryRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

            // Try secondary headers first (more consistent across P6 exports)
            var columnMap = MapFromRow(secondaryRow, lastCol, SecondaryHeaderMap, normalizeHeader: true);

            // Fall back to primary headers if secondary didn't find enough
            if (columnMap.Count < 9)
                columnMap = MapFromRow(primaryRow, lastCol, PrimaryHeaderMap, normalizeHeader: false);

            if (columnMap.Count < 9)
                throw new InvalidOperationException($"P6 file is missing required columns. Found {columnMap.Count} of 10 expected columns.");

            // Add UDF column mappings based on user configuration
            AddUDFColumnMappings(columnMap, primaryRow, secondaryRow, lastCol);

            return columnMap;
        }

        // Add UDF columns to the column map based on user's mapping configuration
        private static void AddUDFColumnMappings(
            Dictionary<int, string> columnMap,
            IXLRow primaryRow,
            IXLRow secondaryRow,
            int lastCol)
        {
            var udfConfig = SettingsManager.GetScheduleUDFMappings();

            foreach (var mapping in udfConfig.Mappings.Where(m => m.IsEnabled))
            {
                if (string.IsNullOrWhiteSpace(mapping.PrimaryHeader) && string.IsNullOrWhiteSpace(mapping.SecondaryHeader))
                    continue;

                // Try to find column by primary header first (row 1, exact match)
                for (int colNum = 1; colNum <= lastCol; colNum++)
                {
                    if (columnMap.ContainsKey(colNum))
                        continue;

                    string primaryText = primaryRow.Cell(colNum).GetString().Trim();
                    string secondaryText = NormalizeSecondaryHeader(secondaryRow.Cell(colNum).GetString().Trim());

                    // Match primary header (exact)
                    if (!string.IsNullOrWhiteSpace(mapping.PrimaryHeader) &&
                        primaryText.Equals(mapping.PrimaryHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        columnMap[colNum] = mapping.TargetColumn;
                        break;
                    }

                    // Match secondary header (normalized)
                    if (!string.IsNullOrWhiteSpace(mapping.SecondaryHeader) &&
                        secondaryText.Equals(mapping.SecondaryHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        columnMap[colNum] = mapping.TargetColumn;
                        break;
                    }
                }
            }
        }

        // Map columns from a header row using the provided lookup dictionary
        private static Dictionary<int, string> MapFromRow(
            IXLRow row,
            int lastCol,
            Dictionary<string, string> headerMap,
            bool normalizeHeader)
        {
            var columnMap = new Dictionary<int, string>();

            for (int colNum = 1; colNum <= lastCol; colNum++)
            {
                string headerText = row.Cell(colNum).GetString().Trim();
                if (string.IsNullOrEmpty(headerText))
                    continue;

                if (normalizeHeader)
                    headerText = NormalizeSecondaryHeader(headerText);

                if (headerMap.TryGetValue(headerText, out string? fieldName))
                    columnMap[colNum] = fieldName;
            }

            return columnMap;
        }

        // Strip (*) prefix and unit/format suffixes from secondary headers
        // Example: "(*)Budgeted Labor Units(h)" -> "Budgeted Labor Units"
        private static string NormalizeSecondaryHeader(string header)
        {
            if (header.StartsWith("(*)"))
                header = header.Substring(3);

            // Remove trailing parenthetical suffixes like (h), (%), (d)
            int parenIndex = header.LastIndexOf('(');
            if (parenIndex > 0 && header.EndsWith(")"))
                header = header.Substring(0, parenIndex);

            return header.Trim();
        }

        // Read schedule data from P6 worksheet (data starts row 3)
        private static List<Schedule> ReadScheduleFromP6(
            IXLWorksheet worksheet, 
            Dictionary<int, string> columnMap,
            DateTime weekEndDate)
        {
            var scheduleRows = new List<Schedule>();
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 3;

            // Data starts at row 3 (row 1 = headers, row 2 = descriptions)
            for (int rowNum = 3; rowNum <= lastRow; rowNum++)
            {
                var row = worksheet.Row(rowNum);

                // Check if row has any data
                if (!RowHasData(row, columnMap))
                    continue;

                var schedule = new Schedule
                {
                    WeekEndDate = weekEndDate,
                    UpdatedBy = App.CurrentUser?.Username ?? "Unknown",
                    UpdatedUtcDate = DateTime.UtcNow
                };

                // Set property values from P6 Excel
                foreach (var mapping in columnMap)
                {
                    int excelColNum = mapping.Key;
                    string fieldName = mapping.Value;

                    var cell = row.Cell(excelColNum);
                    SetScheduleProperty(schedule, fieldName, cell);
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(schedule.SchedActNO))
                    continue; // Skip rows without SchedActNO

                scheduleRows.Add(schedule);
            }

            return scheduleRows;
        }

        // Check if row has any data in mapped columns
        private static bool RowHasData(IXLRow row, Dictionary<int, string> columnMap)
        {
            foreach (var mapping in columnMap)
            {
                var cell = row.Cell(mapping.Key);
                if (!cell.IsEmpty())
                    return true;
            }
            return false;
        }

        // Set Schedule property value from P6 Excel cell
        private static void SetScheduleProperty(Schedule schedule, string fieldName, IXLCell cell)
        {
            if (cell.IsEmpty())
                return;

            try
            {
                switch (fieldName)
                {
                    case "SchedActNO":
                        schedule.SchedActNO = cell.GetString();
                        break;

                    case "WbsId":
                        schedule.WbsId = cell.GetString();
                        break;

                    case "Description":
                        schedule.Description = cell.GetString();
                        break;

                    case "P6_Start":
                        if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime)
                            schedule.P6_Start = cell.GetDateTime();
                        else if (DateTime.TryParse(cell.GetString(), out var dt1))
                            schedule.P6_Start = dt1;
                        break;

                    case "P6_Finish":
                        if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime)
                            schedule.P6_Finish = cell.GetDateTime();
                        else if (DateTime.TryParse(cell.GetString(), out var dt2))
                            schedule.P6_Finish = dt2;
                        break;

                    case "P6_ActualStart":
                        if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime)
                            schedule.P6_ActualStart = cell.GetDateTime();
                        else if (DateTime.TryParse(cell.GetString(), out var dt3))
                            schedule.P6_ActualStart = dt3;
                        break;

                    case "P6_ActualFinish":
                        if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime)
                            schedule.P6_ActualFinish = cell.GetDateTime();
                        else if (DateTime.TryParse(cell.GetString(), out var dt4))
                            schedule.P6_ActualFinish = dt4;
                        break;

                    case "P6_PercentComplete":
                        double pct = 0;
                        if (cell.DataType == ClosedXML.Excel.XLDataType.Number)
                            pct = cell.GetDouble();
                        else if (double.TryParse(cell.GetString(), out var pctText))
                            pct = pctText;

                        // P6 exports as 0-1 decimal, convert to 0-100 percentage
                        schedule.P6_PercentComplete = pct <= 1.0 ? pct * 100.0 : pct;
                        break;

                    case "P6_BudgetMHs":
                        if (cell.DataType == ClosedXML.Excel.XLDataType.Number)
                            schedule.P6_BudgetMHs = cell.GetDouble();
                        else if (double.TryParse(cell.GetString(), out var mhs))
                            schedule.P6_BudgetMHs = mhs;
                        else
                            schedule.P6_BudgetMHs = 0;
                        break;

                    // User-mapped UDF columns from P6
                    case "SchedUDF1":
                        schedule.SchedUDF1 = cell.GetString().Trim();
                        break;
                    case "SchedUDF2":
                        schedule.SchedUDF2 = cell.GetString().Trim();
                        break;
                    case "SchedUDF3":
                        schedule.SchedUDF3 = cell.GetString().Trim();
                        break;
                    case "SchedUDF4":
                        schedule.SchedUDF4 = cell.GetString().Trim();
                        break;
                    case "SchedUDF5":
                        schedule.SchedUDF5 = cell.GetString().Trim();
                        break;

                    case "StatusCode":
                        // Read but don't store - we calculate this at export time
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error setting {fieldName} from cell: {ex.Message}", "ScheduleExcelImporter.SetScheduleProperty");
            }
        }

        // Import schedule rows to database with transaction
        private static int ImportToDatabase(List<Schedule> scheduleRows, List<string> projectIds, DateTime weekEndDate)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Clear ALL existing Schedule data
                var deleteScheduleCmd = connection.CreateCommand();
                deleteScheduleCmd.CommandText = "DELETE FROM Schedule";
                deleteScheduleCmd.ExecuteNonQuery();

                // Clear ALL existing ScheduleProjectMappings
                var deleteMappingsCmd = connection.CreateCommand();
                deleteMappingsCmd.CommandText = "DELETE FROM ScheduleProjectMappings";
                deleteMappingsCmd.ExecuteNonQuery();

                // Clear schedule change log (old entries reference activities that no longer exist)
                ScheduleChangeLogger.ClearAll();

                // Insert Schedule rows (InMS always 0 - column is obsolete but kept for schema compatibility)
                var insertScheduleCmd = connection.CreateCommand();
                insertScheduleCmd.CommandText = @"
                INSERT INTO Schedule (
                    SchedActNO, WeekEndDate, WbsId, Description,
                    P6_Start, P6_Finish, P6_ActualStart, P6_ActualFinish,
                    P6_PercentComplete, P6_BudgetMHs,
                    MissedStartReason, MissedFinishReason,
                    SchedUDF1, SchedUDF2, SchedUDF3, SchedUDF4, SchedUDF5,
                    UpdatedBy, UpdatedUtcDate
                ) VALUES (
                    @schedActNo, @weekEndDate, @wbsId, @description,
                    @plannedStart, @plannedFinish, @actualStart, @actualFinish,
                    @percentComplete, @budgetMHs,
                    @missedStartReason, @missedFinishReason,
                    @schedUDF1, @schedUDF2, @schedUDF3, @schedUDF4, @schedUDF5,
                    @updatedBy, @updatedUtc
                )";

                foreach (var schedule in scheduleRows)
                {
                    insertScheduleCmd.Parameters.Clear();
                    insertScheduleCmd.Parameters.AddWithValue("@schedActNo", schedule.SchedActNO);
                    insertScheduleCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                    insertScheduleCmd.Parameters.AddWithValue("@wbsId", schedule.WbsId);
                    insertScheduleCmd.Parameters.AddWithValue("@description", schedule.Description);
                    insertScheduleCmd.Parameters.AddWithValue("@plannedStart", schedule.P6_Start?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@plannedFinish", schedule.P6_Finish?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@actualStart", schedule.P6_ActualStart?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@actualFinish", schedule.P6_ActualFinish?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@percentComplete", schedule.P6_PercentComplete);
                    insertScheduleCmd.Parameters.AddWithValue("@budgetMHs", schedule.P6_BudgetMHs);
                    insertScheduleCmd.Parameters.AddWithValue("@missedStartReason", schedule.MissedStartReason ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@missedFinishReason", schedule.MissedFinishReason ?? (object)DBNull.Value);
                    insertScheduleCmd.Parameters.AddWithValue("@schedUDF1", schedule.SchedUDF1);
                    insertScheduleCmd.Parameters.AddWithValue("@schedUDF2", schedule.SchedUDF2);
                    insertScheduleCmd.Parameters.AddWithValue("@schedUDF3", schedule.SchedUDF3);
                    insertScheduleCmd.Parameters.AddWithValue("@schedUDF4", schedule.SchedUDF4);
                    insertScheduleCmd.Parameters.AddWithValue("@schedUDF5", schedule.SchedUDF5);
                    insertScheduleCmd.Parameters.AddWithValue("@updatedBy", schedule.UpdatedBy);
                    insertScheduleCmd.Parameters.AddWithValue("@updatedUtc", schedule.UpdatedUtcDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                    insertScheduleCmd.ExecuteNonQuery();
                }

                // Insert ScheduleProjectMappings
                var insertMappingCmd = connection.CreateCommand();
                insertMappingCmd.CommandText = @"
            INSERT INTO ScheduleProjectMappings (WeekEndDate, ProjectID)
            VALUES (@weekEndDate, @projectId)";

                foreach (var projectId in projectIds)
                {
                    insertMappingCmd.Parameters.Clear();
                    insertMappingCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                    insertMappingCmd.Parameters.AddWithValue("@projectId", projectId);
                    insertMappingCmd.ExecuteNonQuery();
                }

                transaction.Commit();

                AppLogger.Info($"Imported {scheduleRows.Count} P6 activities for {weekEndDate:yyyy-MM-dd}",
                    "ScheduleExcelImporter.ImportToDatabase",
                    App.CurrentUser?.Username);

                return scheduleRows.Count;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
