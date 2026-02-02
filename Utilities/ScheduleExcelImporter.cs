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
        // P6 column names to MILESTONE field mapping
        private static readonly Dictionary<string, string> P6ColumnMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "task_code", "SchedActNO" },
            { "wbs_id", "WbsId" },
            { "task_name", "Description" },
            { "start_date", "P6_Start" },
            { "end_date", "P6_Finish" },
            { "act_start_date", "P6_ActualStart" },
            { "act_end_date", "P6_ActualFinish" },
            { "complete_pct", "P6_PercentComplete" },
            { "target_work_qty", "P6_BudgetMHs" },
            { "status_code", "StatusCode" } // Read but not stored
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

                        // Row 1 = field names, Row 2 = descriptions, Data starts Row 3
                        var headerRow = worksheet.Row(1);
                        var columnMap = BuildP6ColumnMap(headerRow);

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

        // Build column mapping from P6 header row (row 1)
        private static Dictionary<int, string> BuildP6ColumnMap(IXLRow headerRow)
        {
            var columnMap = new Dictionary<int, string>();

            for (int colNum = 1; colNum <= headerRow.LastCellUsed()?.Address.ColumnNumber; colNum++)
            {
                var cell = headerRow.Cell(colNum);
                string p6ColumnName = cell.GetString().Trim();

                if (string.IsNullOrEmpty(p6ColumnName))
                    continue;

                // Check if this P6 column is one we care about
                if (P6ColumnMap.TryGetValue(p6ColumnName, out string? milestoneField))
                {
                    columnMap[colNum] = milestoneField;
                }
            }

            if (columnMap.Count < 9) // We need at least 9 columns (status_code is optional)
            {
                throw new InvalidOperationException($"P6 file is missing required columns. Found {columnMap.Count} of 10 expected columns.");
            }

            return columnMap;
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
                // Build list of imported SchedActNOs for purge logic
                var importedActNos = scheduleRows.Select(s => s.SchedActNO).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Purge ThreeWeekLookahead for SchedActNOs NOT in this import (for imported ProjectIDs)
                string projectIdList = "'" + string.Join("','", projectIds) + "'";
                string actNoList = "'" + string.Join("','", importedActNos) + "'";

                var purgeOrphanCmd = connection.CreateCommand();
                purgeOrphanCmd.CommandText = $@"
            DELETE FROM ThreeWeekLookahead 
            WHERE ProjectID IN ({projectIdList})
              AND SchedActNO NOT IN ({actNoList})";
                int orphansPurged = purgeOrphanCmd.ExecuteNonQuery();

                if (orphansPurged > 0)
                {
                    AppLogger.Info($"Purged {orphansPurged} orphaned ThreeWeekLookahead rows",
                        "ScheduleExcelImporter.ImportToDatabase",
                        App.CurrentUser?.Username);
                }

                // Clear stale 3WLA dates (dates in the past of selected WeekEndDate)
                var clearStaleStartCmd = connection.CreateCommand();
                clearStaleStartCmd.CommandText = @"
            UPDATE ThreeWeekLookahead 
            SET ThreeWeekStart = NULL
            WHERE ThreeWeekStart IS NOT NULL AND ThreeWeekStart < @weekEndDate";
                clearStaleStartCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                int clearedStarts = clearStaleStartCmd.ExecuteNonQuery();

                var clearStaleFinishCmd = connection.CreateCommand();
                clearStaleFinishCmd.CommandText = @"
            UPDATE ThreeWeekLookahead 
            SET ThreeWeekFinish = NULL
            WHERE ThreeWeekFinish IS NOT NULL AND ThreeWeekFinish < @weekEndDate";
                clearStaleFinishCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                int clearedFinishes = clearStaleFinishCmd.ExecuteNonQuery();

                if (clearedStarts > 0 || clearedFinishes > 0)
                {
                    AppLogger.Info($"Cleared stale 3WLA dates: {clearedStarts} starts, {clearedFinishes} finishes",
                        "ScheduleExcelImporter.ImportToDatabase",
                        App.CurrentUser?.Username);
                }

                // Delete rows where both dates are now NULL (no longer useful)
                var deleteEmptyCmd = connection.CreateCommand();
                deleteEmptyCmd.CommandText = @"
            DELETE FROM ThreeWeekLookahead 
            WHERE ThreeWeekStart IS NULL AND ThreeWeekFinish IS NULL";
                deleteEmptyCmd.ExecuteNonQuery();

                // Clear ALL existing Schedule data
                var deleteScheduleCmd = connection.CreateCommand();
                deleteScheduleCmd.CommandText = "DELETE FROM Schedule";
                deleteScheduleCmd.ExecuteNonQuery();

                // Clear ALL existing ScheduleProjectMappings
                var deleteMappingsCmd = connection.CreateCommand();
                deleteMappingsCmd.CommandText = "DELETE FROM ScheduleProjectMappings";
                deleteMappingsCmd.ExecuteNonQuery();

                // Insert Schedule rows (InMS always 0 - column is obsolete but kept for schema compatibility)
                var insertScheduleCmd = connection.CreateCommand();
                insertScheduleCmd.CommandText = @"
                INSERT INTO Schedule (
                    SchedActNO, WeekEndDate, WbsId, Description,
                    P6_Start, P6_Finish, P6_ActualStart, P6_ActualFinish,
                    P6_PercentComplete, P6_BudgetMHs,
                    MissedStartReason, MissedFinishReason,
                    UpdatedBy, UpdatedUtcDate
                ) VALUES (
                    @schedActNo, @weekEndDate, @wbsId, @description,
                    @plannedStart, @plannedFinish, @actualStart, @actualFinish,
                    @percentComplete, @budgetMHs,
                    @missedStartReason, @missedFinishReason,
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
