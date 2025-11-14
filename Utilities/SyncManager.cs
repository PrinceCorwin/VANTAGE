using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    public static class SyncManager
    {
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
        // Push LocalDirty records to Central and update their SyncVersion
        // Push LocalDirty records to Central and update their SyncVersion
        // Push LocalDirty records to Central and update their SyncVersion (optimized with batch operations)
        // Push LocalDirty records to Central and update their SyncVersion (optimized with batch operations)
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

                // Explicit list of columns that exist in Central Activities table (matches schema exactly)
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

                // Step 3: Handle UPDATES - verify ownership first
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

                    // Batch UPDATE
                    foreach (var record in validUpdates)
                    {
                        try
                        {
                            var setClauses = syncColumns.Select(col => $"{col} = @{col}");
                            var updateSql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE UniqueID = @UniqueID";

                            var updateCmd = centralConn.CreateCommand();
                            updateCmd.CommandText = updateSql;
                            updateCmd.Parameters.AddWithValue("@UniqueID", record.UniqueID);

                            foreach (var colName in syncColumns)
                            {
                                var prop = typeof(Activity).GetProperty(colName);
                                if (prop != null)
                                {
                                    var value = prop.GetValue(record);
                                    updateCmd.Parameters.AddWithValue($"@{colName}", value ?? DBNull.Value);
                                }
                                else
                                {
                                    updateCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                                }
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
                }

                // Step 4: Handle INSERTS
                var insertSuccessIds = new List<string>();
                if (toInsert.Count > 0)
                {
                    var insertColumns = new List<string> { "UniqueID" };
                    insertColumns.AddRange(syncColumns);
                    var insertParams = insertColumns.Select(c => "@" + c);

                    var insertSql = $"INSERT INTO Activities ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertParams)})";

                    foreach (var record in toInsert)
                    {
                        try
                        {
                            var insertCmd = centralConn.CreateCommand();
                            insertCmd.CommandText = insertSql;
                            insertCmd.Parameters.AddWithValue("@UniqueID", record.UniqueID);

                            foreach (var colName in syncColumns)
                            {
                                var prop = typeof(Activity).GetProperty(colName);
                                if (prop != null)
                                {
                                    var value = prop.GetValue(record);
                                    insertCmd.Parameters.AddWithValue($"@{colName}", value ?? DBNull.Value);
                                }
                                else
                                {
                                    insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                                }
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
                }

                // Step 5: Get all assigned SyncVersions in batch
                var allSuccessIds = updateSuccessIds.Concat(insertSuccessIds).ToList();
                if (allSuccessIds.Count > 0)
                {
                    var versionMap = new Dictionary<string, long>();
                    var getVersionsCmd = centralConn.CreateCommand();
                    getVersionsCmd.CommandText = $"SELECT UniqueID, SyncVersion, ActivityID FROM Activities WHERE UniqueID IN ({string.Join(",", allSuccessIds.Select((id, i) => $"@vid{i}"))})";
                    for (int i = 0; i < allSuccessIds.Count; i++)
                    {
                        getVersionsCmd.Parameters.AddWithValue($"@vid{i}", allSuccessIds[i]);
                    }

                    var activityIdMap = new Dictionary<string, int>();
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

                    // Step 6: Update local records - clear LocalDirty and set SyncVersion/ActivityID
                    foreach (var uniqueId in allSuccessIds)
                    {
                        if (versionMap.TryGetValue(uniqueId, out long syncVersion) && activityIdMap.TryGetValue(uniqueId, out int activityId))
                        {
                            var updateLocalCmd = localConn.CreateCommand();
                            updateLocalCmd.CommandText = @"
                        UPDATE Activities 
                        SET SyncVersion = @version, ActivityID = @actId, LocalDirty = 0 
                        WHERE UniqueID = @id";
                            updateLocalCmd.Parameters.AddWithValue("@version", syncVersion);
                            updateLocalCmd.Parameters.AddWithValue("@actId", activityId);
                            updateLocalCmd.Parameters.AddWithValue("@id", uniqueId);
                            updateLocalCmd.ExecuteNonQuery();
                            result.PushedRecords++;
                        }
                    }
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
            public int PushedRecords { get; set; }
            public int PulledRecords { get; set; }
            public int SkippedRecords { get; set; }
            public List<string> FailedRecords { get; set; } = new List<string>();
            public string ErrorMessage { get; set; }
        }
        // Pull records from Central that have changed since last sync
        // Pull records from Central that have changed since last sync
        // Pull records from Central that have changed since last sync
        public static async Task<SyncResult> PullRecordsAsync(string centralDbPath, List<string> selectedProjects)
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

                    // Query Central for ALL records newer than last sync
                    var pullCmd = centralConn.CreateCommand();
                    pullCmd.CommandText = @"
                SELECT * FROM Activities 
                WHERE ProjectID = @projectId 
                  AND SyncVersion > @lastVersion
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