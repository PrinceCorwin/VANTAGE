using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    public static class SyncManager
    {
        // Fast property accessor without reflection - returns value for given column name
        private static object GetActivityValue(Activity activity, string columnName)
        {
            return columnName switch
            {
                "Area" => activity.Area,
                "AssignedTo" => activity.AssignedTo,
                "AzureUploadUtcDate" => activity.AzureUploadUtcDate,
                "Aux1" => activity.Aux1,
                "Aux2" => activity.Aux2,
                "Aux3" => activity.Aux3,
                "BaseUnit" => activity.BaseUnit,
                "BudgetHoursGroup" => activity.BudgetHoursGroup,
                "BudgetHoursROC" => activity.BudgetHoursROC,
                "BudgetMHs" => activity.BudgetMHs,
                "ChgOrdNO" => activity.ChgOrdNO,
                "ClientBudget" => activity.ClientBudget,
                "ClientCustom3" => activity.ClientCustom3,
                "ClientEquivQty" => activity.ClientEquivQty,
                "CompType" => activity.CompType,
                "CreatedBy" => activity.CreatedBy,
                "DateTrigger" => activity.DateTrigger,
                "Description" => activity.Description,
                "DwgNO" => activity.DwgNO,
                "EarnQtyEntry" => activity.EarnQtyEntry,
                "EarnedMHsRoc" => activity.EarnedMHsRoc,
                "EqmtNO" => activity.EqmtNO,
                "EquivQTY" => activity.EquivQTY,
                "EquivUOM" => activity.EquivUOM,
                "Estimator" => activity.Estimator,
                "HexNO" => activity.HexNO,
                "HtTrace" => activity.HtTrace,
                "InsulType" => activity.InsulType,
                "LineNO" => activity.LineNO,
                "MtrlSpec" => activity.MtrlSpec,
                "Notes" => activity.Notes,
                "PaintCode" => activity.PaintCode,
                "PercentEntry" => activity.PercentEntry,
                "PhaseCategory" => activity.PhaseCategory,
                "PhaseCode" => activity.PhaseCode,
                "PipeGrade" => activity.PipeGrade,
                "PipeSize1" => activity.PipeSize1,
                "PipeSize2" => activity.PipeSize2,
                "PrevEarnMHs" => activity.PrevEarnMHs,
                "PrevEarnQTY" => activity.PrevEarnQTY,
                "ProgDate" => activity.ProgDate,
                "ProjectID" => activity.ProjectID,
                "Quantity" => activity.Quantity,
                "RevNO" => activity.RevNO,
                "RFINO" => activity.RFINO,
                "ROCBudgetQTY" => activity.ROCBudgetQTY,
                "ROCID" => activity.ROCID,
                "ROCPercent" => activity.ROCPercent,
                "ROCStep" => activity.ROCStep,
                "SchedActNO" => activity.SchedActNO,
                "SchFinish" => activity.SchFinish,
                "SchStart" => activity.SchStart,
                "SecondActno" => activity.SecondActno,
                "SecondDwgNO" => activity.SecondDwgNO,
                "Service" => activity.Service,
                "ShopField" => activity.ShopField,
                "ShtNO" => activity.ShtNO,
                "SubArea" => activity.SubArea,
                "PjtSystem" => activity.PjtSystem,
                "SystemNO" => activity.SystemNO,
                "TagNO" => activity.TagNO,
                "UDF1" => activity.UDF1,
                "UDF2" => activity.UDF2,
                "UDF3" => activity.UDF3,
                "UDF4" => activity.UDF4,
                "UDF5" => activity.UDF5,
                "UDF6" => activity.UDF6,
                "UDF7" => activity.UDF7,
                "UDF8" => activity.UDF8,
                "UDF9" => activity.UDF9,
                "UDF10" => activity.UDF10,
                "UDF11" => activity.UDF11,
                "UDF12" => activity.UDF12,
                "UDF13" => activity.UDF13,
                "UDF14" => activity.UDF14,
                "UDF15" => activity.UDF15,
                "UDF16" => activity.UDF16,
                "UDF17" => activity.UDF17,
                "UDF18" => activity.UDF18,
                "UDF20" => activity.UDF20,
                "UpdatedBy" => activity.UpdatedBy,
                "UpdatedUtcDate" => activity.UpdatedUtcDate,
                "UOM" => activity.UOM,
                "WeekEndDate" => activity.WeekEndDate,
                "WorkPackage" => activity.WorkPackage,
                "XRay" => activity.XRay,
                _ => null
            };
        }
        // Check if Central database is accessible
        public static bool CheckCentralConnection(string centralDbPath, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(centralDbPath))
                {
                    errorMessage = "Central database path not configured.";
                    return false;
                }

                if (!System.IO.File.Exists(centralDbPath))
                {
                    errorMessage = "Central database file not found.";
                    return false;
                }

                // Try to open connection
                using var connection = new SqliteConnection($"Data Source={centralDbPath}");
                connection.Open();

                // Verify critical tables exist
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Activities'";
                var result = cmd.ExecuteScalar();

                if (result == null)
                {
                    errorMessage = "Central database is missing Activities table.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Cannot connect to Central database: {ex.Message}";
                AppLogger.Error(ex, "SyncManager.CheckCentralConnection");
                return false;
            }
        }

        // Push LocalDirty records to Central and update their SyncVersion (optimized with transactions and batch operations)
        public static async Task<SyncResult> PushRecordsAsync(string centralDbPath, List<string> selectedProjects)
        {
            var result = new SyncResult();

            try
            {
                // Get LocalDirty records from repository
                var dirtyRecords = await Data.ActivityRepository.GetDirtyActivitiesAsync(selectedProjects);
                result.TotalRecordsToPush = dirtyRecords.Count;

                if (dirtyRecords.Count == 0)
                {
                    return result;
                }

                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                using var localConn = DatabaseSetup.GetConnection();

                centralConn.Open();
                localConn.Open();

                // Explicit list of columns that exist in Central Activities table
                var syncColumns = new List<string>
        {
            "Area", "AssignedTo", "AzureUploadUtcDate", "Aux1", "Aux2", "Aux3",
            "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "BudgetMHs",
            "ChgOrdNO", "ClientBudget", "ClientCustom3", "ClientEquivQty",
            "CompType", "CreatedBy", "DateTrigger", "Description", "DwgNO",
            "EarnQtyEntry", "EarnedMHsRoc", "EqmtNO", "EquivQTY", "EquivUOM",
            "Estimator", "HexNO", "HtTrace", "InsulType", "LineNO",
            "MtrlSpec", "Notes", "PaintCode", "PercentEntry", "PhaseCategory",
            "PhaseCode", "PipeGrade", "PipeSize1", "PipeSize2",
            "PrevEarnMHs", "PrevEarnQTY", "ProgDate", "ProjectID",
            "Quantity", "RevNO", "RFINO", "ROCBudgetQTY", "ROCID",
            "ROCPercent", "ROCStep", "SchedActNO", "SchFinish", "SchStart",
            "SecondActno", "SecondDwgNO", "Service", "ShopField", "ShtNO",
            "SubArea", "PjtSystem", "SystemNO", "TagNO",
            "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7", "UDF8", "UDF9", "UDF10",
            "UDF11", "UDF12", "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "UDF18", "UDF20",
            "UpdatedBy", "UpdatedUtcDate", "UOM", "WeekEndDate", "WorkPackage", "XRay"
        };

                // Step 1: Check which UniqueIDs exist at Central (one query)
                var dirtyUniqueIds = dirtyRecords.Select(r => r.UniqueID).ToList();
                var existingIds = new HashSet<string>();

                var checkCmd = centralConn.CreateCommand();
                checkCmd.CommandText = $"SELECT UniqueID FROM Activities WHERE UniqueID IN ({string.Join(",", dirtyUniqueIds.Select((id, i) => $"@id{i}"))})";
                for (int i = 0; i < dirtyUniqueIds.Count; i++)
                {
                    checkCmd.Parameters.AddWithValue($"@id{i}", dirtyUniqueIds[i]);
                }

                using (var reader = checkCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingIds.Add(reader.GetString(0));
                    }
                }

                // Step 2: Split into INSERT and UPDATE lists
                var toInsert = dirtyRecords.Where(r => !existingIds.Contains(r.UniqueID)).ToList();
                var toUpdate = dirtyRecords.Where(r => existingIds.Contains(r.UniqueID)).ToList();

                // Step 3: Handle UPDATES with transaction
                var updateSuccessIds = new List<string>();
                if (toUpdate.Count > 0)
                {
                    // Verify ownership for all records to update
                    var ownerCheckCmd = centralConn.CreateCommand();
                    ownerCheckCmd.CommandText = $"SELECT UniqueID, AssignedTo FROM Activities WHERE UniqueID IN ({string.Join(",", toUpdate.Select((r, i) => $"@uid{i}"))})";
                    for (int i = 0; i < toUpdate.Count; i++)
                    {
                        ownerCheckCmd.Parameters.AddWithValue($"@uid{i}", toUpdate[i].UniqueID);
                    }

                    var ownershipMap = new Dictionary<string, string>();
                    using (var ownerReader = ownerCheckCmd.ExecuteReader())
                    {
                        while (ownerReader.Read())
                        {
                            ownershipMap[ownerReader.GetString(0)] = ownerReader.GetString(1);
                        }
                    }

                    // Filter out records not owned by current user
                    var validUpdates = toUpdate.Where(r =>
                    {
                        if (ownershipMap.TryGetValue(r.UniqueID, out string owner) && owner == r.AssignedTo)
                        {
                            return true;
                        }
                        result.FailedRecords.Add($"UniqueID {r.UniqueID}: No longer assigned to you");
                        return false;
                    }).ToList();

                    // Batch UPDATE with transaction and prepared statement
                    if (validUpdates.Count > 0)
                    {
                        using var transaction = centralConn.BeginTransaction();

                        var setClauses = syncColumns.Select(col => $"{col} = @{col}");
                        var updateSql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE UniqueID = @UniqueID";

                        using var updateCmd = centralConn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = updateSql;

                        // Add parameters once
                        updateCmd.Parameters.Add("@UniqueID", SqliteType.Text);
                        foreach (var colName in syncColumns)
                        {
                            updateCmd.Parameters.Add($"@{colName}", SqliteType.Text);
                        }

                        // Prepare statement (compile once)
                        updateCmd.Prepare();

                        foreach (var record in validUpdates)
                        {
                            try
                            {
                                updateCmd.Parameters["@UniqueID"].Value = record.UniqueID;

                                foreach (var colName in syncColumns)
                                {
                                    var value = GetActivityValue(record, colName);
                                    updateCmd.Parameters[$"@{colName}"].Value = value ?? DBNull.Value;
                                }

                                updateCmd.ExecuteNonQuery();
                                updateSuccessIds.Add(record.UniqueID);
                            }
                            catch (Exception ex)
                            {
                                result.FailedRecords.Add($"UniqueID {record.UniqueID}: {ex.Message}");
                                AppLogger.Error(ex, $"SyncManager.PushRecords UPDATE - UniqueID {record.UniqueID}");
                            }
                        }

                        transaction.Commit();
                    }
                }

                // Step 4: Handle INSERTS with transaction and prepared statement
                var insertSuccessIds = new List<string>();
                if (toInsert.Count > 0)
                {
                    using var transaction = centralConn.BeginTransaction();

                    var insertColumns = new List<string> { "UniqueID" };
                    insertColumns.AddRange(syncColumns);
                    var insertParams = insertColumns.Select(c => "@" + c);

                    var insertSql = $"INSERT INTO Activities ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertParams)})";

                    using var insertCmd = centralConn.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = insertSql;

                    // Add parameters once
                    insertCmd.Parameters.Add("@UniqueID", SqliteType.Text);
                    foreach (var colName in syncColumns)
                    {
                        insertCmd.Parameters.Add($"@{colName}", SqliteType.Text);
                    }

                    // Prepare statement (compile once)
                    insertCmd.Prepare();

                    foreach (var record in toInsert)
                    {
                        try
                        {
                            insertCmd.Parameters["@UniqueID"].Value = record.UniqueID;

                            foreach (var colName in syncColumns)
                            {
                                var value = GetActivityValue(record, colName);
                                insertCmd.Parameters[$"@{colName}"].Value = value ?? DBNull.Value;
                            }

                            insertCmd.ExecuteNonQuery();
                            insertSuccessIds.Add(record.UniqueID);
                        }
                        catch (Exception ex)
                        {
                            result.FailedRecords.Add($"UniqueID {record.UniqueID}: {ex.Message}");
                            AppLogger.Error(ex, $"SyncManager.PushRecords INSERT - UniqueID {record.UniqueID}");
                        }
                    }

                    transaction.Commit();
                }

                // Step 5: Get all assigned SyncVersions and ActivityIDs in ONE batch query
                var allSuccessIds = updateSuccessIds.Concat(insertSuccessIds).ToList();
                if (allSuccessIds.Count > 0)
                {
                    var versionMap = new Dictionary<string, long>();
                    var activityIdMap = new Dictionary<string, int>();

                    var getVersionsCmd = centralConn.CreateCommand();
                    getVersionsCmd.CommandText = $"SELECT UniqueID, SyncVersion, ActivityID FROM Activities WHERE UniqueID IN ({string.Join(",", allSuccessIds.Select((id, i) => $"@vid{i}"))})";
                    for (int i = 0; i < allSuccessIds.Count; i++)
                    {
                        getVersionsCmd.Parameters.AddWithValue($"@vid{i}", allSuccessIds[i]);
                    }

                    using (var versionReader = getVersionsCmd.ExecuteReader())
                    {
                        while (versionReader.Read())
                        {
                            string uid = versionReader.GetString(0);
                            long syncVer = versionReader.GetInt64(1);
                            int actId = versionReader.GetInt32(2);
                            versionMap[uid] = syncVer;
                            activityIdMap[uid] = actId;
                        }
                    }

                    // Step 6: Update local records with transaction and prepared statement
                    using var localTransaction = localConn.BeginTransaction();

                    var updateLocalCmd = localConn.CreateCommand();
                    updateLocalCmd.Transaction = localTransaction;
                    updateLocalCmd.CommandText = @"
                UPDATE Activities 
                SET SyncVersion = @version, ActivityID = @actId, LocalDirty = 0 
                WHERE UniqueID = @id";

                    updateLocalCmd.Parameters.Add("@version", SqliteType.Integer);
                    updateLocalCmd.Parameters.Add("@actId", SqliteType.Integer);
                    updateLocalCmd.Parameters.Add("@id", SqliteType.Text);
                    updateLocalCmd.Prepare();

                    foreach (var uniqueId in allSuccessIds)
                    {
                        if (versionMap.TryGetValue(uniqueId, out long syncVersion) && activityIdMap.TryGetValue(uniqueId, out int activityId))
                        {
                            updateLocalCmd.Parameters["@version"].Value = syncVersion;
                            updateLocalCmd.Parameters["@actId"].Value = activityId;
                            updateLocalCmd.Parameters["@id"].Value = uniqueId;
                            updateLocalCmd.ExecuteNonQuery();
                        }
                    }

                    localTransaction.Commit();
                    // Set counts
                    result.InsertedRecords = insertSuccessIds.Count;
                    result.UpdatedRecords = updateSuccessIds.Count;
                    result.PushedRecords = result.InsertedRecords + result.UpdatedRecords;  // ADD THIS LINE
                    result.PushedUniqueIds.AddRange(allSuccessIds);  // KEEP THIS LINE
                                                                     // Update LastPulledSyncVersion to avoid re-pulling pushed records
                    var maxSyncVersion = versionMap.Values.Any() ? versionMap.Values.Max() : 0;
                    foreach (var projectId in selectedProjects)
                    {
                        var currentMax = Convert.ToInt64(SettingsManager.GetAppSetting($"LastPulledSyncVersion_{projectId}", "0"));
                        if (maxSyncVersion > currentMax)
                        {
                            SettingsManager.SetAppSetting(
                                $"LastPulledSyncVersion_{projectId}",
                                maxSyncVersion.ToString(),
                                "int"
                            );
                        }
                    }
                }
                // Update statistics after bulk operations for optimal query performance
                if (result.InsertedRecords > 100)
                {
                    var analyzeCmd = centralConn.CreateCommand();
                    analyzeCmd.CommandText = "ANALYZE Activities";
                    analyzeCmd.ExecuteNonQuery();
                }
                AppLogger.Info($"Push completed: {result.PushedRecords} pushed, {result.FailedRecords.Count} failed", "SyncManager.PushRecords");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "SyncManager.PushRecords");
            }

            return result;
        }
        // Result class for sync operations
        public class SyncResult
        {
            public int TotalRecordsToPush { get; set; }
            public int PushedRecords { get; set; }  // KEEP THIS
            public int InsertedRecords { get; set; }  // ADD THIS
            public int UpdatedRecords { get; set; }
            public int PulledRecords { get; set; }
            public int SkippedRecords { get; set; }
            public List<string> FailedRecords { get; set; } = new List<string>();
            public string ErrorMessage { get; set; }
            public List<string> PushedUniqueIds { get; set; } = new List<string>();
        }
        // Pull records from Central that have changed since last sync
        // Pull records from Central that have changed since last sync
        // Pull records from Central that have changed since last sync
        public static async Task<SyncResult> PullRecordsAsync(string centralDbPath, List<string> selectedProjects, List<string> excludeUniqueIds = null)
        {
            var result = new SyncResult();

            try
            {
                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                using var localConn = DatabaseSetup.GetConnection();

                centralConn.Open();
                localConn.Open();

                foreach (var projectId in selectedProjects)
                {
                    // Get last pulled version for this project
                    long lastPulledVersion = Convert.ToInt64(
                        SettingsManager.GetAppSetting($"LastPulledSyncVersion_{projectId}", "0")
                    );


                    // Query Central for records newer than last sync (excluding just-pushed records)
                    var pullCmd = centralConn.CreateCommand();
                    string excludeClause = "";
                    if (excludeUniqueIds != null && excludeUniqueIds.Any())
                    {
                        var excludeParams = string.Join(",", excludeUniqueIds.Select((id, i) => $"@exclude{i}"));
                        excludeClause = $" AND UniqueID NOT IN ({excludeParams})";
                        for (int i = 0; i < excludeUniqueIds.Count; i++)
                        {
                            pullCmd.Parameters.AddWithValue($"@exclude{i}", excludeUniqueIds[i]);
                        }
                    }

                    pullCmd.CommandText = $@"
                        SELECT * FROM Activities 
                        WHERE ProjectID = @projectId 
                          AND SyncVersion > @lastVersion
                          {excludeClause}
                        ORDER BY SyncVersion";

                    pullCmd.Parameters.AddWithValue("@projectId", projectId);
                    pullCmd.Parameters.AddWithValue("@lastVersion", lastPulledVersion);

                    long maxVersionPulled = lastPulledVersion;

                    using var reader = pullCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        try
                        {
                            string uniqueId = reader.GetString(reader.GetOrdinal("UniqueID"));
                            long syncVersion = reader.GetInt64(reader.GetOrdinal("SyncVersion"));

                            // Build INSERT OR REPLACE dynamically (works with UniqueID as primary key)
                            var columnNames = new List<string>();
                            var paramNames = new List<string>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                columnNames.Add(colName);
                                paramNames.Add($"@{colName}");
                            }

                            // Add LocalDirty = 0 for local insert
                            columnNames.Add("LocalDirty");
                            paramNames.Add("@LocalDirty");

                            var insertSql = $@"
                        INSERT OR REPLACE INTO Activities ({string.Join(", ", columnNames)}) 
                        VALUES ({string.Join(", ", paramNames)})";

                            var insertCmd = localConn.CreateCommand();
                            insertCmd.CommandText = insertSql;

                            // Add all parameters from Central
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                var value = reader.GetValue(i);
                                insertCmd.Parameters.AddWithValue($"@{colName}", value ?? DBNull.Value);
                            }

                            // Set LocalDirty = 0 for pulled records
                            insertCmd.Parameters.AddWithValue("@LocalDirty", 0);

                            insertCmd.ExecuteNonQuery();
                            result.PulledRecords++;

                            if (syncVersion > maxVersionPulled)
                            {
                                maxVersionPulled = syncVersion;
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error(ex, $"SyncManager.PullRecords - Error pulling record");
                        }
                    }

                    // Update LastPulledSyncVersion for this project
                    if (maxVersionPulled > lastPulledVersion)
                    {
                        SettingsManager.SetAppSetting(
                            $"LastPulledSyncVersion_{projectId}",
                            maxVersionPulled.ToString(),
                            "int"
                        );
                    }
                }

                AppLogger.Info($"Pull completed: {result.PulledRecords} pulled", "SyncManager.PullRecords");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "SyncManager.PullRecords");
            }

            return result;
        }

    }
}