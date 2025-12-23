using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;
using VANTAGE.Utilities;
namespace VANTAGE.Repositories
{
    // Repository for local Schedule table operations
    public static class ScheduleRepository
    {
        // Get master grid rows with P6 data + MS rollups
        public static async Task<List<ScheduleMasterRow>> GetScheduleMasterRowsAsync(DateTime weekEndDate)
        {
            return await Task.Run(() =>
            {
                var masterRows = new List<ScheduleMasterRow>();

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    // Step 1: Get ProjectIDs for this schedule
                    var projectIds = GetProjectIDsForWeek(connection, weekEndDate);

                    if (projectIds.Count == 0)
                    {
                        AppLogger.Warning($"No ProjectIDs mapped for WeekEndDate {weekEndDate:yyyy-MM-dd}", "ScheduleRepository.GetScheduleMasterRowsAsync");
                        return masterRows;
                    }

                    // Step 2: Get distinct SchedActNOs from ProgressSnapshots (user's actual data)
                    var snapshotSchedActNOs = GetSchedActNOsFromSnapshots(weekEndDate, projectIds);

                    if (snapshotSchedActNOs.Count == 0)
                    {
                        AppLogger.Warning($"No SchedActNOs found in ProgressSnapshots for WeekEndDate {weekEndDate:yyyy-MM-dd}", "ScheduleRepository.GetScheduleMasterRowsAsync");
                        return masterRows;
                    }

                    // Step 3: Get Schedule rows ONLY for SchedActNOs that exist in snapshots
                    string schedActNoList = "'" + string.Join("','", snapshotSchedActNOs) + "'";

                    var scheduleCmd = connection.CreateCommand();
                    scheduleCmd.CommandText = $@"
                SELECT 
                    SchedActNO, WbsId, Description,
                    P6_PlannedStart, P6_PlannedFinish, P6_ActualStart, P6_ActualFinish,
                    P6_PercentComplete, P6_BudgetMHs,
                    ThreeWeekStart, ThreeWeekFinish, MissedStartReason, MissedFinishReason
                FROM Schedule
                WHERE WeekEndDate = @weekEndDate
                  AND SchedActNO IN ({schedActNoList})
                ORDER BY SchedActNO";
                    scheduleCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                    using var reader = scheduleCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var schedActNo = reader.GetString(0);

                        var masterRow = new ScheduleMasterRow
                        {
                            SchedActNO = schedActNo,
                            WbsId = reader.GetString(1),
                            Description = reader.GetString(2),
                            P6_PlannedStart = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                            P6_PlannedFinish = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                            P6_ActualStart = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                            P6_ActualFinish = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                            P6_PercentComplete = reader.GetDouble(7),
                            P6_BudgetMHs = reader.GetDouble(8),
                            ThreeWeekStart = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
                            ThreeWeekFinish = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
                            MissedStartReason = reader.IsDBNull(11) ? null : reader.GetString(11),
                            MissedFinishReason = reader.IsDBNull(12) ? null : reader.GetString(12),
                            WeekEndDate = weekEndDate
                        };

                        masterRows.Add(masterRow);
                    }

                    // Step 4: Calculate MS rollups
                    CalculateAllMSRollups(masterRows, weekEndDate, projectIds);

                    AppLogger.Info($"Loaded {masterRows.Count} schedule master rows for {weekEndDate:yyyy-MM-dd}",
                        "ScheduleRepository.GetScheduleMasterRowsAsync");

                    return masterRows;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.GetScheduleMasterRowsAsync");
                    throw;
                }
            });
        }
        // Get distinct SchedActNOs from ProgressSnapshots for the given week and projects
        private static HashSet<string> GetSchedActNOsFromSnapshots(DateTime weekEndDate, List<string> projectIds)
        {
            var schedActNOs = new HashSet<string>();

            try
            {
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                string projectIdList = "'" + string.Join("','", projectIds) + "'";

                var cmd = azureConn.CreateCommand();
                cmd.CommandText = $@"
            SELECT DISTINCT SchedActNO 
            FROM ProgressSnapshots 
            WHERE WeekEndDate = @weekEndDate 
              AND ProjectID IN ({projectIdList})
              AND SchedActNO IS NOT NULL 
              AND SchedActNO <> ''";
                cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    schedActNOs.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleRepository.GetSchedActNOsFromSnapshots");
            }

            return schedActNOs;
        }
        // Update editable fields in Schedule table (local SQLite)
        // Only updates: ThreeWeekStart, ThreeWeekFinish, MissedStartReason, MissedFinishReason
        public static async Task<bool> UpdateScheduleRowAsync(ScheduleMasterRow row, string username)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE Schedule 
                        SET ThreeWeekStart = @threeWeekStart,
                            ThreeWeekFinish = @threeWeekFinish,
                            MissedStartReason = @missedStartReason,
                            MissedFinishReason = @missedFinishReason,
                            UpdatedBy = @updatedBy,
                            UpdatedUtcDate = @updatedUtcDate
                        WHERE SchedActNO = @schedActNo 
                          AND WeekEndDate = @weekEndDate";

                    cmd.Parameters.AddWithValue("@threeWeekStart",
                        row.ThreeWeekStart?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@threeWeekFinish",
                        row.ThreeWeekFinish?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@missedStartReason",
                        row.MissedStartReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@missedFinishReason",
                        row.MissedFinishReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@updatedBy", username);
                    cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    cmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                    cmd.Parameters.AddWithValue("@weekEndDate", row.WeekEndDate.ToString("yyyy-MM-dd"));

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        AppLogger.Info($"Updated Schedule row: {row.SchedActNO}",
                            "ScheduleRepository.UpdateScheduleRowAsync", username);
                        return true;
                    }
                    else
                    {
                        AppLogger.Warning($"No rows updated for SchedActNO: {row.SchedActNO}, WeekEndDate: {row.WeekEndDate:yyyy-MM-dd}",
                            "ScheduleRepository.UpdateScheduleRowAsync");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.UpdateScheduleRowAsync");
                    return false;
                }
            });
        }
        // Save all editable fields for all rows in one transaction
        public static async Task<int> SaveAllScheduleRowsAsync(IEnumerable<ScheduleMasterRow> rows, string username)
        {
            return await Task.Run(() =>
            {
                int savedCount = 0;

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    using var transaction = connection.BeginTransaction();

                    var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                UPDATE Schedule 
                SET ThreeWeekStart = @threeWeekStart,
                    ThreeWeekFinish = @threeWeekFinish,
                    MissedStartReason = @missedStartReason,
                    MissedFinishReason = @missedFinishReason,
                    UpdatedBy = @updatedBy,
                    UpdatedUtcDate = @updatedUtcDate
                WHERE SchedActNO = @schedActNo 
                  AND WeekEndDate = @weekEndDate";

                    foreach (var row in rows)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@threeWeekStart",
                            row.ThreeWeekStart?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@threeWeekFinish",
                            row.ThreeWeekFinish?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@missedStartReason",
                            row.MissedStartReason ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@missedFinishReason",
                            row.MissedFinishReason ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@updatedBy", username);
                        cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        cmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                        cmd.Parameters.AddWithValue("@weekEndDate", row.WeekEndDate.ToString("yyyy-MM-dd"));

                        savedCount += cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    AppLogger.Info($"Batch saved {savedCount} schedule rows", "ScheduleRepository.SaveAllScheduleRowsAsync", username);
                    return savedCount;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.SaveAllScheduleRowsAsync");
                    throw;
                }
            });
        }
        // Calculate MS rollups for ALL rows in ONE Azure query
        private static void CalculateAllMSRollups(
            List<ScheduleMasterRow> masterRows,
            DateTime weekEndDate,
            List<string> projectIds)
        {
            try
            {
                if (masterRows.Count == 0) return;

                // Connect to Azure for ProgressSnapshots
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                var cmd = azureConn.CreateCommand();

                // Build ProjectID IN clause
                string projectIdList = "'" + string.Join("','", projectIds) + "'";

                // ONE query to calculate rollups for ALL SchedActNOs
                // Calculate weighted average directly in SQL (stays in 0-100 scale)
                cmd.CommandText = $@"
            SELECT 
                SchedActNO,
                MIN(SchStart) as MS_ActualStart,
                MAX(SchFinish) as MS_ActualFinish,
                CASE 
                    WHEN SUM(BudgetMHs) > 0 
                    THEN SUM(BudgetMHs * PercentEntry) / SUM(BudgetMHs)
                    ELSE 0 
                END as MS_PercentComplete,
                SUM(BudgetMHs) as MS_BudgetMHs
            FROM ProgressSnapshots
            WHERE WeekEndDate = @weekEndDate
              AND ProjectID IN ({projectIdList})
            GROUP BY SchedActNO";

                cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                // Create dictionary for fast lookup
                var rollupDict = new Dictionary<string, (DateTime? Start, DateTime? Finish, double Percent, double MHs)>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string schedActNo = reader.GetString(0);

                    // SchStart and SchFinish are stored as TEXT (VARCHAR) in Azure, not DATETIME
                    DateTime? msStart = null;
                    if (!reader.IsDBNull(1))
                    {
                        string startStr = reader.GetString(1);
                        if (DateTime.TryParse(startStr, out DateTime parsedStart))
                            msStart = parsedStart;
                    }

                    DateTime? msFinish = null;
                    if (!reader.IsDBNull(2))
                    {
                        string finishStr = reader.GetString(2);
                        if (DateTime.TryParse(finishStr, out DateTime parsedFinish))
                            msFinish = parsedFinish;
                    }

                    double msPercent = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                    double msBudgetMHs = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);

                    rollupDict[schedActNo] = (msStart, msFinish, msPercent, msBudgetMHs);
                }

                // Apply rollups to master rows
                foreach (var row in masterRows)
                {
                    if (rollupDict.TryGetValue(row.SchedActNO, out var rollup))
                    {
                        row.MS_ActualStart = rollup.Start;
                        row.MS_ActualFinish = rollup.Finish;
                        row.MS_PercentComplete = rollup.Percent;
                        row.MS_BudgetMHs = rollup.MHs;
                    }
                }

                AppLogger.Info($"Calculated MS rollups for {rollupDict.Count} SchedActNOs", "ScheduleRepository.CalculateAllMSRollups");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleRepository.CalculateAllMSRollups");
                // Don't throw - just leave MS values at defaults
            }
        }

        // Get ProjectIDs that this schedule covers
        private static List<string> GetProjectIDsForWeek(SqliteConnection connection, DateTime weekEndDate)
        {
            var projectIds = new List<string>();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ProjectID 
                FROM ScheduleProjectMappings 
                WHERE WeekEndDate = @weekEndDate";
            cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                projectIds.Add(reader.GetString(0));
            }

            return projectIds;
        }

        // Get distinct WeekEndDates available in Schedule table
        public static async Task<List<DateTime>> GetAvailableWeekEndDatesAsync()
        {
            return await Task.Run(() =>
            {
                var dates = new List<DateTime>();

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT WeekEndDate FROM Schedule ORDER BY WeekEndDate DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        dates.Add(DateTime.Parse(reader.GetString(0)));
                    }

                    return dates;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.GetAvailableWeekEndDatesAsync");
                    return dates;
                }
            });
        }
    }
}