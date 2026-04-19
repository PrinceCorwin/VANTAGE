using System;
using System.Collections.Generic;
using System.Linq;
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

                    // Step 2: Query local ProgressSnapshots for MS rollups FIRST - this determines which activities to show
                    var rollupDict = GetMSRollups(weekEndDate, projectIds);
                    if (rollupDict.Count == 0)
                    {
                        AppLogger.Info($"No ProgressSnapshots found for WeekEndDate {weekEndDate:yyyy-MM-dd}", "ScheduleRepository.GetScheduleMasterRowsAsync");
                        return masterRows;
                    }

                    // Step 3: Get Schedule rows only for SchedActNOs that have MS data
                    string schedActNoList = "'" + string.Join("','", rollupDict.Keys) + "'";

                    var scheduleCmd = connection.CreateCommand();
                    scheduleCmd.CommandText = $@"
                SELECT SchedActNO, WbsId, Description,
                    P6_Start, P6_Finish, P6_ActualStart, P6_ActualFinish,
                    P6_PercentComplete, P6_BudgetMHs,
                    MissedStartReason, MissedFinishReason,
                    SchedUDF1, SchedUDF2, SchedUDF3, SchedUDF4, SchedUDF5,
                    ThreeWeekStart, ThreeWeekFinish
                FROM Schedule
                WHERE WeekEndDate = @weekEndDate
                  AND SchedActNO IN ({schedActNoList})
                ORDER BY SchedActNO";
                    scheduleCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                    var schedActNOs = new List<string>();

                    using (var reader = scheduleCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var schedActNo = reader.GetString(0);
                            schedActNOs.Add(schedActNo);

                            // Parse 3WLA dates from Schedule table
                            string threeWeekStartStr = reader.IsDBNull(16) ? "" : reader.GetString(16);
                            string threeWeekFinishStr = reader.IsDBNull(17) ? "" : reader.GetString(17);

                            var masterRow = new ScheduleMasterRow
                            {
                                SchedActNO = schedActNo,
                                WbsId = reader.GetString(1),
                                Description = reader.GetString(2),
                                P6_Start = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                                P6_Finish = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                                P6_ActualStart = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                                P6_ActualFinish = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                                P6_PercentComplete = reader.GetDouble(7),
                                P6_BudgetMHs = reader.GetDouble(8),
                                MissedStartReason = reader.IsDBNull(9) ? null : reader.GetString(9),
                                MissedFinishReason = reader.IsDBNull(10) ? null : reader.GetString(10),
                                SchedUDF1 = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                                SchedUDF2 = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                                SchedUDF3 = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                                SchedUDF4 = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                                SchedUDF5 = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                                ThreeWeekStart = string.IsNullOrWhiteSpace(threeWeekStartStr) ? null : DateTime.Parse(threeWeekStartStr),
                                ThreeWeekFinish = string.IsNullOrWhiteSpace(threeWeekFinishStr) ? null : DateTime.Parse(threeWeekFinishStr),
                                WeekEndDate = weekEndDate
                            };

                            // Apply MS rollups from dictionary
                            if (rollupDict.TryGetValue(schedActNo, out var rollup))
                            {
                                masterRow.V_Start = rollup.Start;
                                masterRow.V_Finish = rollup.Finish;
                                masterRow.MS_PercentComplete = rollup.Percent;
                                masterRow.MS_BudgetMHs = rollup.MHs;
                            }

                            masterRows.Add(masterRow);
                        }
                    }

                    if (masterRows.Count == 0)
                    {
                        AppLogger.Warning($"No Schedule rows found for SchedActNOs with MS data for WeekEndDate {weekEndDate:yyyy-MM-dd}", "ScheduleRepository.GetScheduleMasterRowsAsync");
                        return masterRows;
                    }

                    // Step 4: Apply default MissedReasons based on MS rollups (only if fields are empty)
                    foreach (var row in masterRows)
                    {
                        // Default MissedStartReason to "Started Early" if MS started before P6 schedule date
                        if (string.IsNullOrEmpty(row.MissedStartReason) &&
                            row.V_Start != null &&
                            row.P6_Start != null &&
                            row.V_Start.Value.Date < row.P6_Start.Value.Date)
                        {
                            row.MissedStartReason = "Started Early";
                        }

                        // Default MissedFinishReason to "Finished Early" if MS finished before P6 schedule date
                        if (string.IsNullOrEmpty(row.MissedFinishReason) &&
                            row.V_Finish != null &&
                            row.P6_Finish != null &&
                            row.V_Finish.Value.Date < row.P6_Finish.Value.Date)
                        {
                            row.MissedFinishReason = "Finished Early";
                        }
                    }

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
        private static Dictionary<string, (DateTime? Start, DateTime? Finish, double Percent, double MHs)> GetMSRollups(
    DateTime weekEndDate,
    List<string> projectIds)
        {
            var rollupDict = new Dictionary<string, (DateTime? Start, DateTime? Finish, double Percent, double MHs)>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                connection.Open();

                var cmd = connection.CreateCommand();

                // Build ProjectID IN clause
                string projectIdList = "'" + string.Join("','", projectIds) + "'";

                // ONE query to calculate rollups for ALL SchedActNOs from the local mirror.
                // Calculate weighted average directly in SQL (stays in 0-100 scale).
                // V_Start = min ActStart (NULLIF handles empty strings stored instead of NULL).
                // V_Finish = max ActFin only if ALL activities are 100% complete.
                cmd.CommandText = $@"
            SELECT
                SchedActNO,
                MIN(NULLIF(ActStart, '')) as V_Start,
                CASE
                    WHEN MIN(PercentEntry) = 100
                    THEN MAX(NULLIF(ActFin, ''))
                    ELSE NULL
                END as V_Finish,
                CASE
                    WHEN SUM(BudgetMHs) > 0
                    THEN SUM(BudgetMHs * PercentEntry) / SUM(BudgetMHs)
                    ELSE 0
                END as MS_PercentComplete,
                SUM(BudgetMHs) as MS_BudgetMHs
            FROM ProgressSnapshots
            WHERE WeekEndDate = @weekEndDate
              AND ProjectID IN ({projectIdList})
              AND AssignedTo = @username
            GROUP BY SchedActNO";

                cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@username", App.CurrentUser?.Username ?? "");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string schedActNo = reader.GetString(0);

                    // ActStart and ActFin are stored as TEXT
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

                AppLogger.Info($"Retrieved MS rollups for {rollupDict.Count} SchedActNOs from local mirror", "ScheduleRepository.GetMSRollups");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleRepository.GetMSRollups");
                // Return empty dict - don't throw, let caller handle empty result
            }

            return rollupDict;
        }
        public static async Task<List<(string SchedActNO, string Description, string WbsId)>> GetP6NotInMSAsync(DateTime weekEndDate)
        {
            return await Task.Run(() =>
            {
                var results = new List<(string SchedActNO, string Description, string WbsId)>();

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    // Get ProjectIDs for this week
                    var projectIds = GetProjectIDsForWeek(connection, weekEndDate);
                    if (projectIds.Count == 0)
                        return results;

                    // Get all SchedActNOs that have MS data (from local mirror)
                    var msSchedActNOs = new HashSet<string>();
                    {
                        string projectIdList = "'" + string.Join("','", projectIds) + "'";

                        var msCmd = connection.CreateCommand();
                        msCmd.CommandText = $@"
                    SELECT DISTINCT SchedActNO
                    FROM ProgressSnapshots
                    WHERE WeekEndDate = @weekEndDate
                      AND ProjectID IN ({projectIdList})
                      AND AssignedTo = @username";
                        msCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                        msCmd.Parameters.AddWithValue("@username", App.CurrentUser?.Username ?? "");

                        using var msReader = msCmd.ExecuteReader();
                        while (msReader.Read())
                        {
                            msSchedActNOs.Add(msReader.GetString(0));
                        }
                    }

                    // Get all P6 SchedActNOs and filter out ones that have MS data
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                SELECT SchedActNO, Description, WbsId
                FROM Schedule
                WHERE WeekEndDate = @weekEndDate
                ORDER BY SchedActNO";
                    cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string schedActNo = reader.GetString(0);

                        // Only include if NOT in MS data
                        if (!msSchedActNOs.Contains(schedActNo))
                        {
                            results.Add((
                                schedActNo,
                                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                            ));
                        }
                    }

                    AppLogger.Info($"Found {results.Count} P6 activities not in MS for {weekEndDate:yyyy-MM-dd}",
                        "ScheduleRepository.GetP6NotInMSAsync");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.GetP6NotInMSAsync");
                }

                return results;
            });
        }

        // Get "In MS, Not in P6" SchedActNOs for NotIn report
        public static async Task<List<string>> GetMSNotInP6Async(DateTime weekEndDate)
        {
            return await Task.Run(() =>
            {
                var results = new List<string>();

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    // Get ProjectIDs for this week
                    var projectIds = GetProjectIDsForWeek(connection, weekEndDate);
                    if (projectIds.Count == 0)
                        return results;

                    // Get all SchedActNOs from Schedule for this week
                    var p6ActNOs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var schedCmd = connection.CreateCommand();
                    schedCmd.CommandText = @"
                SELECT SchedActNO FROM Schedule WHERE WeekEndDate = @weekEndDate";
                    schedCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                    using (var reader = schedCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            p6ActNOs.Add(reader.GetString(0));
                        }
                    }

                    // Query local ProgressSnapshots for distinct SchedActNOs
                    // Note: local mirror only contains current user's rows, so this naturally filters
                    // to current-user MS data — a behavior change from the previous all-users Azure query.
                    string projectIdList = "'" + string.Join("','", projectIds) + "'";

                    var msCmd = connection.CreateCommand();
                    msCmd.CommandText = $@"
                SELECT DISTINCT SchedActNO
                FROM ProgressSnapshots
                WHERE WeekEndDate = @weekEndDate
                  AND ProjectID IN ({projectIdList})
                  AND SchedActNO IS NOT NULL
                  AND SchedActNO <> ''";
                    msCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                    using var msReader = msCmd.ExecuteReader();
                    while (msReader.Read())
                    {
                        string actNo = msReader.GetString(0);
                        if (!p6ActNOs.Contains(actNo))
                        {
                            results.Add(actNo);
                        }
                    }

                    results.Sort();

                    AppLogger.Info($"Found {results.Count} MS activities not in P6 for {weekEndDate:yyyy-MM-dd}",
                        "ScheduleRepository.GetMSNotInP6Async");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.GetMSNotInP6Async");
                }

                return results;
            });
        }

        // Gets the first ProjectID from ScheduleProjectMappings for a given WeekEndDate
        public static string? GetFirstProjectIDForWeek(DateTime weekEndDate)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
            SELECT ProjectID FROM ScheduleProjectMappings 
            WHERE WeekEndDate = @weekEndDate 
            LIMIT 1";
                cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));

                return cmd.ExecuteScalar() as string;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleRepository.GetFirstProjectIDForWeek");
                return null;
            }
        }
        // May need to be deleted. Get distinct SchedActNOs from ProgressSnapshots for the given week and projects
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
            FROM VMS_ProgressSnapshots
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
        // Update ProgressSnapshot in Azure AND corresponding Activity in local SQLite
        // Replaces UpdateSnapshotAndActivityAsync - only updates Azure snapshot, NOT local Activity
        // Reason: Editing historical snapshots shouldn't overwrite current Activity progress
        public static async Task<bool> UpdateSnapshotAsync(ProgressSnapshot snapshot, string username)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var azureCmd = azureConn.CreateCommand();
                    azureCmd.CommandText = @"
                UPDATE VMS_ProgressSnapshots
                SET PercentEntry = @percentEntry,
                    BudgetMHs = @budgetMHs,
                    ActStart = @schStart,
                    ActFin = @schFinish,
                    UpdatedBy = @updatedBy,
                    UpdatedUtcDate = @updatedUtcDate
                WHERE UniqueID = @uniqueId
                  AND WeekEndDate = @weekEndDate";

                    azureCmd.Parameters.AddWithValue("@percentEntry", snapshot.PercentEntry);
                    azureCmd.Parameters.AddWithValue("@budgetMHs", snapshot.BudgetMHs);
                    azureCmd.Parameters.AddWithValue("@schStart",
                        snapshot.ActStart?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    azureCmd.Parameters.AddWithValue("@schFinish",
                        snapshot.ActFin?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    azureCmd.Parameters.AddWithValue("@updatedBy", username);
                    azureCmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    azureCmd.Parameters.AddWithValue("@uniqueId", snapshot.UniqueID);
                    azureCmd.Parameters.AddWithValue("@weekEndDate", snapshot.WeekEndDate.ToString("yyyy-MM-dd"));

                    int azureRows = azureCmd.ExecuteNonQuery();

                    if (azureRows == 0)
                    {
                        AppLogger.Warning($"No Azure snapshot updated for UniqueID: {snapshot.UniqueID}",
                            "ScheduleRepository.UpdateSnapshotAsync");
                        return false;
                    }

                    AppLogger.Info($"Updated snapshot: {snapshot.UniqueID}",
                        "ScheduleRepository.UpdateSnapshotAsync", username);

                    // Mirror the same UPDATE to local ProgressSnapshots so the Schedule grid
                    // reflects the edit immediately on the next read. Best-effort: if local
                    // fails, the Azure write is still authoritative and local will self-heal
                    // on the next P6 import. Date format must match the Azure write above so
                    // that any subsequent rollup MIN/MAX comparisons stay consistent.
                    try
                    {
                        using var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                        localConn.Open();

                        var localCmd = localConn.CreateCommand();
                        localCmd.CommandText = @"
                            UPDATE ProgressSnapshots
                            SET PercentEntry = @percentEntry,
                                BudgetMHs = @budgetMHs,
                                ActStart = @schStart,
                                ActFin = @schFinish,
                                UpdatedBy = @updatedBy,
                                UpdatedUtcDate = @updatedUtcDate
                            WHERE UniqueID = @uniqueId
                              AND WeekEndDate = @weekEndDate";
                        localCmd.Parameters.AddWithValue("@percentEntry", snapshot.PercentEntry);
                        localCmd.Parameters.AddWithValue("@budgetMHs", snapshot.BudgetMHs);
                        localCmd.Parameters.AddWithValue("@schStart",
                            snapshot.ActStart?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                        localCmd.Parameters.AddWithValue("@schFinish",
                            snapshot.ActFin?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                        localCmd.Parameters.AddWithValue("@updatedBy", username);
                        localCmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        localCmd.Parameters.AddWithValue("@uniqueId", snapshot.UniqueID);
                        localCmd.Parameters.AddWithValue("@weekEndDate", snapshot.WeekEndDate.ToString("yyyy-MM-dd"));
                        localCmd.ExecuteNonQuery();
                    }
                    catch (Exception localEx)
                    {
                        AppLogger.Warning(
                            $"Local snapshot mirror update failed (Azure write succeeded): {localEx.Message}",
                            "ScheduleRepository.UpdateSnapshotAsync");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.UpdateSnapshotAsync");
                    return false;
                }
            });
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
        // Save a single Schedule row + its scoped Activities.PlanStart/PlanFin bounds update.
        // Called by the dynamic-save flow on every master grid cell commit.
        public static async Task<bool> SaveScheduleRowAsync(ScheduleMasterRow row, string username)
        {
            if (row == null)
                return false;

            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();
                    using var transaction = connection.BeginTransaction();

                    string? projectId = GetFirstProjectIDForWeek(row.WeekEndDate);

                    // Update Schedule table with lookahead dates and MissedReasons
                    using (var scheduleCmd = connection.CreateCommand())
                    {
                        scheduleCmd.Transaction = transaction;
                        scheduleCmd.CommandText = @"
                            UPDATE Schedule
                            SET ThreeWeekStart = @threeWeekStart,
                                ThreeWeekFinish = @threeWeekFinish,
                                MissedStartReason = @missedStartReason,
                                MissedFinishReason = @missedFinishReason,
                                UpdatedBy = @updatedBy,
                                UpdatedUtcDate = @updatedUtcDate
                            WHERE SchedActNO = @schedActNo
                              AND WeekEndDate = @weekEndDate";
                        scheduleCmd.Parameters.AddWithValue("@threeWeekStart",
                            row.ThreeWeekStart?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                        scheduleCmd.Parameters.AddWithValue("@threeWeekFinish",
                            row.ThreeWeekFinish?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                        scheduleCmd.Parameters.AddWithValue("@missedStartReason",
                            row.MissedStartReason ?? (object)DBNull.Value);
                        scheduleCmd.Parameters.AddWithValue("@missedFinishReason",
                            row.MissedFinishReason ?? (object)DBNull.Value);
                        scheduleCmd.Parameters.AddWithValue("@updatedBy", username);
                        scheduleCmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        scheduleCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                        scheduleCmd.Parameters.AddWithValue("@weekEndDate", row.WeekEndDate.ToString("yyyy-MM-dd"));
                        scheduleCmd.ExecuteNonQuery();
                    }

                    // Activities bounds updates — only applicable when we have a ProjectID and a lookahead date
                    if (!string.IsNullOrEmpty(projectId))
                    {
                        // PlanStart: update if NULL or earlier than ThreeWeekStart
                        if (row.ThreeWeekStart.HasValue)
                        {
                            string planStartStr = row.ThreeWeekStart.Value.ToString("yyyy-MM-dd");

                            using var exactMatchCmd = connection.CreateCommand();
                            exactMatchCmd.Transaction = transaction;
                            exactMatchCmd.CommandText = @"
                                SELECT COUNT(*) FROM Activities
                                WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                  AND PlanStart = @planStart";
                            exactMatchCmd.Parameters.AddWithValue("@planStart", planStartStr);
                            exactMatchCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                            exactMatchCmd.Parameters.AddWithValue("@projectId", projectId);
                            long exactCount = Convert.ToInt64(exactMatchCmd.ExecuteScalar() ?? 0);

                            if (exactCount == 0)
                            {
                                using var planStartCmd = connection.CreateCommand();
                                planStartCmd.Transaction = transaction;
                                planStartCmd.CommandText = @"
                                    UPDATE Activities
                                    SET PlanStart = @planStart
                                    WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                      AND (PlanStart IS NULL OR PlanStart < @planStart)";
                                planStartCmd.Parameters.AddWithValue("@planStart", planStartStr);
                                planStartCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                                planStartCmd.Parameters.AddWithValue("@projectId", projectId);
                                int rowsAffected = planStartCmd.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    using var fallbackCmd = connection.CreateCommand();
                                    fallbackCmd.Transaction = transaction;
                                    fallbackCmd.CommandText = @"
                                        UPDATE Activities
                                        SET PlanStart = @planStart
                                        WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                          AND PlanStart = (
                                            SELECT MIN(PlanStart) FROM Activities
                                            WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                              AND PlanStart > @planStart
                                          )";
                                    fallbackCmd.Parameters.AddWithValue("@planStart", planStartStr);
                                    fallbackCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                                    fallbackCmd.Parameters.AddWithValue("@projectId", projectId);
                                    fallbackCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // PlanFin: update if NULL or later than ThreeWeekFinish
                        if (row.ThreeWeekFinish.HasValue)
                        {
                            string planFinStr = row.ThreeWeekFinish.Value.ToString("yyyy-MM-dd");

                            using var exactMatchCmd = connection.CreateCommand();
                            exactMatchCmd.Transaction = transaction;
                            exactMatchCmd.CommandText = @"
                                SELECT COUNT(*) FROM Activities
                                WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                  AND PlanFin = @planFin";
                            exactMatchCmd.Parameters.AddWithValue("@planFin", planFinStr);
                            exactMatchCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                            exactMatchCmd.Parameters.AddWithValue("@projectId", projectId);
                            long exactCount = Convert.ToInt64(exactMatchCmd.ExecuteScalar() ?? 0);

                            if (exactCount == 0)
                            {
                                using var planFinCmd = connection.CreateCommand();
                                planFinCmd.Transaction = transaction;
                                planFinCmd.CommandText = @"
                                    UPDATE Activities
                                    SET PlanFin = @planFin
                                    WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                      AND (PlanFin IS NULL OR PlanFin > @planFin)";
                                planFinCmd.Parameters.AddWithValue("@planFin", planFinStr);
                                planFinCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                                planFinCmd.Parameters.AddWithValue("@projectId", projectId);
                                int rowsAffected = planFinCmd.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    using var fallbackCmd = connection.CreateCommand();
                                    fallbackCmd.Transaction = transaction;
                                    fallbackCmd.CommandText = @"
                                        UPDATE Activities
                                        SET PlanFin = @planFin
                                        WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                          AND PlanFin = (
                                            SELECT MAX(PlanFin) FROM Activities
                                            WHERE SchedActNO = @schedActNo AND ProjectID = @projectId
                                              AND PlanFin < @planFin
                                          )";
                                    fallbackCmd.Parameters.AddWithValue("@planFin", planFinStr);
                                    fallbackCmd.Parameters.AddWithValue("@schedActNo", row.SchedActNO);
                                    fallbackCmd.Parameters.AddWithValue("@projectId", projectId);
                                    fallbackCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.SaveScheduleRowAsync");
                    return false;
                }
            });
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


        // Get ProgressSnapshots for a specific SchedActNO (for detail grid) — reads local mirror
        public static async Task<List<ProgressSnapshot>> GetSnapshotsBySchedActNOAsync(string schedActNO, DateTime weekEndDate)
        {
            return await Task.Run(() =>
            {
                var snapshots = new List<ProgressSnapshot>();

                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var projectIds = GetProjectIDsForWeek(connection, weekEndDate);
                    if (projectIds.Count == 0)
                    {
                        AppLogger.Warning($"No ProjectIDs mapped for WeekEndDate {weekEndDate:yyyy-MM-dd}",
                            "ScheduleRepository.GetSnapshotsBySchedActNOAsync");
                        return snapshots;
                    }

                    string projectIdList = "'" + string.Join("','", projectIds) + "'";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"
                            SELECT
                                UniqueID, WeekEndDate, SchedActNO, Description,
                                PercentEntry, BudgetMHs, ActStart, ActFin,
                                AssignedTo, ProjectID, UpdatedBy, UpdatedUtcDate
                            FROM ProgressSnapshots
                            WHERE SchedActNO = @schedActNO
                              AND WeekEndDate = @weekEndDate
                              AND ProjectID IN ({projectIdList})
                              AND AssignedTo = @username
                            ORDER BY UniqueID";

                    cmd.Parameters.AddWithValue("@schedActNO", schedActNO);
                    cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser?.Username ?? "");

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var snapshot = new ProgressSnapshot
                        {
                            UniqueID = reader.GetString(0),
                            WeekEndDate = DateTime.Parse(reader.GetString(1)),
                            SchedActNO = reader.GetString(2),
                            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            PercentEntry = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                            BudgetMHs = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                            AssignedTo = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                            ProjectID = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                            UpdatedBy = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                            UpdatedUtcDate = reader.IsDBNull(11) ? DateTime.MinValue : DateTime.Parse(reader.GetString(11))
                        };

                        // ActStart and ActFin are stored as TEXT
                        if (!reader.IsDBNull(6))
                        {
                            string startStr = reader.GetString(6);
                            if (DateTime.TryParse(startStr, out DateTime parsedStart))
                                snapshot.ActStart = parsedStart;
                        }

                        if (!reader.IsDBNull(7))
                        {
                            string finishStr = reader.GetString(7);
                            if (DateTime.TryParse(finishStr, out DateTime parsedFinish))
                                snapshot.ActFin = parsedFinish;
                        }

                        snapshots.Add(snapshot);
                    }

                    AppLogger.Info($"Loaded {snapshots.Count} snapshots for SchedActNO {schedActNO}",
                        "ScheduleRepository.GetSnapshotsBySchedActNOAsync");

                    return snapshots;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.GetSnapshotsBySchedActNOAsync");
                    return snapshots;
                }
            });
        }
        // Column list for the local ProgressSnapshots mirror table — must stay in sync with
        // the CREATE TABLE in DatabaseSetup.cs / SchemaMigrator.Migration_v11. Used by the
        // refill helpers to bulk-copy current-user snapshot rows from Azure into local SQLite.
        // TRIMMED to the 12 columns the Schedule module actually reads. Azure
        // VMS_ProgressSnapshots still has all 89 columns and is unchanged.
        private const string ProgressSnapshotsColumns =
            "UniqueID, WeekEndDate, SchedActNO, Description, PercentEntry, BudgetMHs, " +
            "ActStart, ActFin, AssignedTo, ProjectID, UpdatedBy, UpdatedUtcDate";

        private const int ProgressSnapshotsColumnCount = 12;

        // Wipes the local ProgressSnapshots table and refills it with the given user's snapshot
        // rows for the given week from Azure. Called by the P6 import flow and by Submit Week
        // (when the submitted week matches what's currently mirrored locally).
        // Returns row count on success, -1 on failure. Best-effort — caller should not block on it.
        public static async Task<int> RefillLocalSnapshotsForWeekAsync(DateTime weekEndDate, string username)
        {
            return await Task.Run(() =>
            {
                var totalSw = System.Diagnostics.Stopwatch.StartNew();
                long azureMs = 0, deleteMs = 0, dropIdxMs = 0, insertMs = 0, createIdxMs = 0, commitMs = 0;
                try
                {
                    // Step 1: Read all 89 columns from Azure for this user/week
                    var stepSw = System.Diagnostics.Stopwatch.StartNew();
                    var azureRows = new List<object?[]>();
                    using (var azureConn = AzureDbManager.GetConnection())
                    {
                        azureConn.Open();
                        var cmd = azureConn.CreateCommand();
                        cmd.CommandTimeout = 0;
                        cmd.CommandText = $@"
                            SELECT {ProgressSnapshotsColumns}
                            FROM VMS_ProgressSnapshots
                            WHERE WeekEndDate = @weekEndDate
                              AND AssignedTo = @username";
                        cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@username", username);

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var arr = new object?[ProgressSnapshotsColumnCount];
                            for (int i = 0; i < ProgressSnapshotsColumnCount; i++)
                            {
                                arr[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            azureRows.Add(arr);
                        }
                    }
                    azureMs = stepSw.ElapsedMilliseconds;

                    // Step 2: Wipe + bulk insert into local SQLite in a single transaction
                    using var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    localConn.Open();

                    // Per-connection PRAGMAs to speed up bulk write. These only affect this
                    // connection (and only for its lifetime), so they don't impact other queries.
                    using (var pragmaCmd = localConn.CreateCommand())
                    {
                        pragmaCmd.CommandText = @"
                            PRAGMA synchronous = OFF;
                            PRAGMA temp_store = MEMORY;
                            PRAGMA cache_size = -200000;";
                        pragmaCmd.ExecuteNonQuery();
                    }

                    using var tx = localConn.BeginTransaction();

                    // Wipe existing rows
                    stepSw.Restart();
                    using (var deleteCmd = localConn.CreateCommand())
                    {
                        deleteCmd.Transaction = tx;
                        deleteCmd.CommandText = "DELETE FROM ProgressSnapshots";
                        deleteCmd.ExecuteNonQuery();
                    }
                    deleteMs = stepSw.ElapsedMilliseconds;

                    // Drop indexes before bulk insert. Maintaining indexes during a bulk load
                    // is dramatically slower than dropping them, inserting, and rebuilding.
                    stepSw.Restart();
                    using (var dropIdxCmd = localConn.CreateCommand())
                    {
                        dropIdxCmd.Transaction = tx;
                        dropIdxCmd.CommandText = @"
                            DROP INDEX IF EXISTS idx_progsnap_week_proj;
                            DROP INDEX IF EXISTS idx_progsnap_schedactno;";
                        dropIdxCmd.ExecuteNonQuery();
                    }
                    dropIdxMs = stepSw.ElapsedMilliseconds;

                    // Bulk insert using a prepared statement, executed once per row.
                    // For SQLite in-process, this is consistently the fastest pattern when
                    // wrapped in a transaction with indexes dropped: SQLite parses+plans the
                    // statement once and just rebinds parameters per row. The big speedup
                    // vs the original implementation comes from the dropped indexes (above)
                    // and the PRAGMAs (synchronous=OFF, etc.) — not from batching SQL.
                    stepSw.Restart();
                    if (azureRows.Count > 0)
                    {
                        var paramPlaceholders = string.Join(", ",
                            Enumerable.Range(0, ProgressSnapshotsColumnCount).Select(i => "@p" + i));

                        using var insertCmd = localConn.CreateCommand();
                        insertCmd.Transaction = tx;
                        insertCmd.CommandText =
                            $"INSERT INTO ProgressSnapshots ({ProgressSnapshotsColumns}) VALUES ({paramPlaceholders})";

                        for (int i = 0; i < ProgressSnapshotsColumnCount; i++)
                        {
                            insertCmd.Parameters.Add(new SqliteParameter("@p" + i, DBNull.Value));
                        }
                        insertCmd.Prepare();

                        foreach (var row in azureRows)
                        {
                            for (int i = 0; i < ProgressSnapshotsColumnCount; i++)
                            {
                                insertCmd.Parameters[i].Value = row[i] ?? DBNull.Value;
                            }
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                    insertMs = stepSw.ElapsedMilliseconds;

                    // Recreate indexes after bulk insert
                    stepSw.Restart();
                    using (var createIdxCmd = localConn.CreateCommand())
                    {
                        createIdxCmd.Transaction = tx;
                        createIdxCmd.CommandText = @"
                            CREATE INDEX IF NOT EXISTS idx_progsnap_week_proj ON ProgressSnapshots(WeekEndDate, ProjectID, AssignedTo);
                            CREATE INDEX IF NOT EXISTS idx_progsnap_schedactno ON ProgressSnapshots(SchedActNO);";
                        createIdxCmd.ExecuteNonQuery();
                    }
                    createIdxMs = stepSw.ElapsedMilliseconds;

                    stepSw.Restart();
                    tx.Commit();
                    commitMs = stepSw.ElapsedMilliseconds;

                    totalSw.Stop();
                    AppLogger.Info(
                        $"Refilled local ProgressSnapshots: {azureRows.Count} rows for {weekEndDate:yyyy-MM-dd} in {totalSw.ElapsedMilliseconds}ms " +
                        $"[azure={azureMs}ms delete={deleteMs}ms dropIdx={dropIdxMs}ms insert={insertMs}ms createIdx={createIdxMs}ms commit={commitMs}ms]",
                        "ScheduleRepository.RefillLocalSnapshotsForWeekAsync", username);
                    return azureRows.Count;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ScheduleRepository.RefillLocalSnapshotsForWeekAsync");
                    return -1;
                }
            });
        }


        // One-time post-login backfill: if the local ProgressSnapshots table is empty AND the
        // local Schedule table has an imported P6 file, pull the matching week's snapshot for
        // the current user from Azure. Lets existing users get instant Schedule performance
        // after upgrading without having to re-import their P6 file.
        // Best-effort — silent no-op if Azure unreachable or any other failure.
        public static async Task BackfillLocalSnapshotsIfNeededAsync(string username)
        {
            await Task.Run(() =>
            {
                try
                {
                    DateTime? scheduleWeek = null;
                    using (var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}"))
                    {
                        localConn.Open();

                        // If local mirror already has data, nothing to do
                        var countCmd = localConn.CreateCommand();
                        countCmd.CommandText = "SELECT COUNT(*) FROM ProgressSnapshots";
                        long existing = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
                        if (existing > 0) return;

                        // Find the imported P6 week (if any)
                        var weekCmd = localConn.CreateCommand();
                        weekCmd.CommandText = "SELECT WeekEndDate FROM Schedule LIMIT 1";
                        var weekResult = weekCmd.ExecuteScalar();
                        if (weekResult == null || weekResult == DBNull.Value) return;

                        if (DateTime.TryParse(weekResult.ToString(), out DateTime parsedWeek))
                        {
                            scheduleWeek = parsedWeek;
                        }
                    }

                    if (scheduleWeek == null) return;

                    AppLogger.Info(
                        $"Backfilling local ProgressSnapshots for week {scheduleWeek:yyyy-MM-dd}",
                        "ScheduleRepository.BackfillLocalSnapshotsIfNeededAsync", username);

                    // Reuse the refill helper (synchronous wait is fine — we're already on a Task.Run)
                    RefillLocalSnapshotsForWeekAsync(scheduleWeek.Value, username).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    // Best-effort — never fail startup over this
                    AppLogger.Warning(
                        $"Local snapshot backfill failed (will retry on next P6 import): {ex.Message}",
                        "ScheduleRepository.BackfillLocalSnapshotsIfNeededAsync");
                }
            });
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
