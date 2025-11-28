using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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

        // Check if Azure database is accessible (delegates to AzureDbManager)
        public static bool CheckAzureConnection(out string errorMessage)
        {
            return AzureDbManager.CheckConnection(out errorMessage);
        }

        // Legacy method signature for compatibility - now checks Azure instead of file path
        [Obsolete("Use CheckAzureConnection() instead. This method ignores the centralDbPath parameter.")]
        public static bool CheckCentralConnection(string centralDbPath, out string errorMessage)
        {
            return AzureDbManager.CheckConnection(out errorMessage);
        }

        // Push LocalDirty records to Azure using temp table approach for scalability
        public static async Task<SyncResult> PushRecordsAsync(List<string> selectedProjects)
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

                AppLogger.Info($"Starting push of {dirtyRecords.Count} records to Azure", "SyncManager.PushRecords");

                using var azureConn = AzureDbManager.GetConnection();
                using var localConn = DatabaseSetup.GetConnection();

                azureConn.Open();
                localConn.Open();

                // Columns to sync to Azure
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

                // ============================================================
                // STEP 1: Create temp table and populate with all dirty UniqueIDs
                // ============================================================
                var createTempCmd = azureConn.CreateCommand();
                createTempCmd.CommandText = @"
                    IF OBJECT_ID('tempdb..#SyncBatch') IS NOT NULL DROP TABLE #SyncBatch;
                    CREATE TABLE #SyncBatch (UniqueID NVARCHAR(100) PRIMARY KEY)";
                createTempCmd.ExecuteNonQuery();

                // Insert all UniqueIDs using parameterized batch
                using (var transaction = azureConn.BeginTransaction())
                {
                    var insertTempCmd = azureConn.CreateCommand();
                    insertTempCmd.Transaction = transaction;
                    insertTempCmd.CommandText = "INSERT INTO #SyncBatch (UniqueID) VALUES (@uid)";
                    var uidParam = insertTempCmd.Parameters.Add("@uid", SqlDbType.NVarChar, 100);

                    foreach (var record in dirtyRecords)
                    {
                        uidParam.Value = record.UniqueID;
                        insertTempCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                AppLogger.Info($"Populated temp table with {dirtyRecords.Count} UniqueIDs", "SyncManager.PushRecords");

                // ============================================================
                // STEP 2: Find which UniqueIDs already exist in Azure (using JOIN)
                // ============================================================
                var existingIds = new HashSet<string>();
                var checkCmd = azureConn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT a.[UniqueID] 
                    FROM Activities a 
                    INNER JOIN #SyncBatch s ON a.[UniqueID] = s.[UniqueID]";

                using (var reader = checkCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingIds.Add(reader.GetString(0));
                    }
                }

                // Split into INSERT and UPDATE lists
                var toInsert = dirtyRecords.Where(r => !existingIds.Contains(r.UniqueID)).ToList();
                var toUpdate = dirtyRecords.Where(r => existingIds.Contains(r.UniqueID)).ToList();

                AppLogger.Info($"Records to insert: {toInsert.Count}, Records to update: {toUpdate.Count}", "SyncManager.PushRecords");

                // ============================================================
                // STEP 3: Handle UPDATES - verify ownership first (using JOIN)
                // ============================================================
                var updateSuccessIds = new List<string>();

                if (toUpdate.Count > 0)
                {
                    // Get ownership and deletion status using JOIN
                    var ownershipMap = new Dictionary<string, string>();
                    var deletionMap = new Dictionary<string, int>();

                    var ownerCheckCmd = azureConn.CreateCommand();
                    ownerCheckCmd.CommandText = @"
                        SELECT a.[UniqueID], a.[AssignedTo], a.[IsDeleted] 
                        FROM Activities a 
                        INNER JOIN #SyncBatch s ON a.[UniqueID] = s.[UniqueID]";

                    using (var ownerReader = ownerCheckCmd.ExecuteReader())
                    {
                        while (ownerReader.Read())
                        {
                            ownershipMap[ownerReader.GetString(0)] = ownerReader.GetString(1);
                            deletionMap[ownerReader.GetString(0)] = ownerReader.GetBoolean(2) ? 1 : 0;
                        }
                    }

                    // Filter out records not owned by current user OR deleted in Azure
                    var validUpdates = toUpdate.Where(r =>
                    {
                        if (deletionMap.TryGetValue(r.UniqueID, out int isDeleted) && isDeleted == 1)
                        {
                            result.FailedRecords.Add($"UniqueID {r.UniqueID}: Record was deleted, please pull to sync");
                            return false;
                        }

                        if (ownershipMap.TryGetValue(r.UniqueID, out string owner) && owner == r.AssignedTo)
                        {
                            return true;
                        }
                        result.FailedRecords.Add($"UniqueID {r.UniqueID}: No longer assigned to you");
                        return false;
                    }).ToList();

                    // Perform updates with parameterized command
                    if (validUpdates.Count > 0)
                    {
                        using var transaction = azureConn.BeginTransaction();

                        var setClauses = syncColumns.Select(col => $"[{col}] = @{col}");
                        var updateSql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE [UniqueID] = @UniqueID";

                        using var updateCmd = azureConn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = updateSql;

                        updateCmd.Parameters.Add("@UniqueID", SqlDbType.NVarChar, 100);
                        foreach (var colName in syncColumns)
                        {
                            updateCmd.Parameters.Add($"@{colName}", SqlDbType.NVarChar);
                        }

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

                                int rowsAffected = updateCmd.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    updateSuccessIds.Add(record.UniqueID);
                                }
                                else
                                {
                                    result.FailedRecords.Add($"UniqueID {record.UniqueID}: No rows updated");
                                }   
                                
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

                // ============================================================
                // STEP 4: Handle INSERTS with parameterized command
                // ============================================================
                var insertSuccessIds = new List<string>();

                if (toInsert.Count > 0)
                {
                    using var transaction = azureConn.BeginTransaction();

                    var insertColumns = new List<string> { "UniqueID", "IsDeleted" };
                    insertColumns.AddRange(syncColumns);
                    var insertParams = insertColumns.Select(c => "@" + c);

                    var insertSql = $"INSERT INTO Activities ({string.Join(", ", insertColumns.Select(c => $"[{c}]"))}) VALUES ({string.Join(", ", insertParams)})";

                    using var insertCmd = azureConn.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = insertSql;

                    insertCmd.Parameters.Add("@UniqueID", SqlDbType.NVarChar, 100);
                    insertCmd.Parameters.Add("@IsDeleted", SqlDbType.Int);
                    foreach (var colName in syncColumns)
                    {
                        insertCmd.Parameters.Add($"@{colName}", SqlDbType.NVarChar);
                    }

                    foreach (var record in toInsert)
                    {
                        try
                        {
                            insertCmd.Parameters["@UniqueID"].Value = record.UniqueID;
                            insertCmd.Parameters["@IsDeleted"].Value = 0;

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

                // ============================================================
                // STEP 5: Get SyncVersions and ActivityIDs (using JOIN)
                // ============================================================
                var allSuccessIds = updateSuccessIds.Concat(insertSuccessIds).ToList();

                if (allSuccessIds.Count > 0)
                {
                    // Rebuild temp table with only successful IDs
                    var rebuildTempCmd = azureConn.CreateCommand();
                    rebuildTempCmd.CommandText = "DELETE FROM #SyncBatch";
                    rebuildTempCmd.ExecuteNonQuery();

                    using (var tempTransaction = azureConn.BeginTransaction())
                    {
                        var insertTempCmd = azureConn.CreateCommand();
                        insertTempCmd.Transaction = tempTransaction;
                        insertTempCmd.CommandText = "INSERT INTO #SyncBatch (UniqueID) VALUES (@uid)";
                        var uidParam = insertTempCmd.Parameters.Add("@uid", SqlDbType.NVarChar, 100);

                        foreach (var uid in allSuccessIds)
                        {
                            uidParam.Value = uid;
                            insertTempCmd.ExecuteNonQuery();
                        }

                        tempTransaction.Commit();
                    }

                    // Get versions using JOIN
                    var versionMap = new Dictionary<string, long>();
                    var activityIdMap = new Dictionary<string, int>();

                    var getVersionsCmd = azureConn.CreateCommand();
                    getVersionsCmd.CommandText = @"
                        SELECT a.[UniqueID], a.[SyncVersion], a.[ActivityID] 
                        FROM Activities a 
                        INNER JOIN #SyncBatch s ON a.[UniqueID] = s.[UniqueID]";

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

                    // ============================================================
                    // STEP 6: Update local records with SyncVersion and clear LocalDirty
                    // ============================================================
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

                    // Set result counts
                    result.InsertedRecords = insertSuccessIds.Count;
                    result.UpdatedRecords = updateSuccessIds.Count;
                    result.PushedRecords = result.InsertedRecords + result.UpdatedRecords;
                    result.PushedUniqueIds.AddRange(allSuccessIds);
                }

                // ============================================================
                // STEP 7: Force-pull any ownership-conflicted records
                // ============================================================
                if (result.FailedRecords.Any())
                {
                    var failedUniqueIds = result.FailedRecords
                        .Where(f => f.Contains("No longer assigned to you"))
                        .Select(f =>
                        {
                            var parts = f.Split(':');
                            if (parts.Length >= 1)
                            {
                                return parts[0].Replace("UniqueID", "").Trim();
                            }
                            return null;
                        })
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();

                    if (failedUniqueIds.Any())
                    {
                        // Rebuild temp table with failed IDs
                        var clearTempCmd = azureConn.CreateCommand();
                        clearTempCmd.CommandText = "DELETE FROM #SyncBatch";
                        clearTempCmd.ExecuteNonQuery();

                        using (var tempTransaction = azureConn.BeginTransaction())
                        {
                            var insertTempCmd = azureConn.CreateCommand();
                            insertTempCmd.Transaction = tempTransaction;
                            insertTempCmd.CommandText = "INSERT INTO #SyncBatch (UniqueID) VALUES (@uid)";
                            var uidParam = insertTempCmd.Parameters.Add("@uid", SqlDbType.NVarChar, 100);

                            foreach (var uid in failedUniqueIds)
                            {
                                uidParam.Value = uid;
                                insertTempCmd.ExecuteNonQuery();
                            }

                            tempTransaction.Commit();
                        }

                        // Force-pull using JOIN
                        var forcePullCmd = azureConn.CreateCommand();
                        forcePullCmd.CommandText = @"
                            SELECT a.* FROM Activities a 
                            INNER JOIN #SyncBatch s ON a.[UniqueID] = s.[UniqueID]";

                        using var reader = forcePullCmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var columnNames = new List<string>();
                            var paramNames = new List<string>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (colName == "IsDeleted") continue;
                                columnNames.Add(colName);
                                paramNames.Add($"@{colName}");
                            }

                            columnNames.Add("LocalDirty");
                            paramNames.Add("@LocalDirty");

                            // Use INSERT OR REPLACE for SQLite local database
                            var insertSql = $"INSERT OR REPLACE INTO Activities ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
                            var insertCmd = localConn.CreateCommand();
                            insertCmd.CommandText = insertSql;

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (colName == "IsDeleted") continue;
                                insertCmd.Parameters.AddWithValue($"@{colName}", reader.GetValue(i) ?? DBNull.Value);
                            }

                            insertCmd.Parameters.AddWithValue("@LocalDirty", 0);
                            insertCmd.ExecuteNonQuery();
                        }

                        AppLogger.Info($"Force-pulled {failedUniqueIds.Count} ownership-conflicted records from Azure", "SyncManager.PushRecords");
                    }
                }

                // Temp table automatically dropped when connection closes

                AppLogger.Info($"Push completed: {result.PushedRecords} pushed ({result.InsertedRecords} inserted, {result.UpdatedRecords} updated), {result.FailedRecords.Count} failed", "SyncManager.PushRecords");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "SyncManager.PushRecords");
            }

            return result;
        }

        // Legacy method signature for compatibility
        [Obsolete("Use PushRecordsAsync(List<string> selectedProjects) instead. The centralDbPath parameter is ignored.")]
        public static async Task<SyncResult> PushRecordsAsync(string centralDbPath, List<string> selectedProjects)
        {
            return await PushRecordsAsync(selectedProjects);
        }

        // Result class for sync operations
        public class SyncResult
        {
            public int TotalRecordsToPush { get; set; }
            public int PushedRecords { get; set; }
            public int InsertedRecords { get; set; }
            public int UpdatedRecords { get; set; }
            public int PulledRecords { get; set; }
            public int SkippedRecords { get; set; }
            public List<string> FailedRecords { get; set; } = new List<string>();
            public string ErrorMessage { get; set; }
            public List<string> PushedUniqueIds { get; set; } = new List<string>();
        }

        // Pull records from Azure that have changed since last sync
        public static async Task<SyncResult> PullRecordsAsync(List<string> selectedProjects, List<string> excludeUniqueIds = null)
        {
            var result = new SyncResult();

            try
            {
                using var azureConn = AzureDbManager.GetConnection();
                using var localConn = DatabaseSetup.GetConnection();

                azureConn.Open();
                localConn.Open();

                // If we have UniqueIDs to exclude, create temp table
                if (excludeUniqueIds != null && excludeUniqueIds.Any())
                {
                    var createTempCmd = azureConn.CreateCommand();
                    createTempCmd.CommandText = @"
                        IF OBJECT_ID('tempdb..#ExcludeBatch') IS NOT NULL DROP TABLE #ExcludeBatch;
                        CREATE TABLE #ExcludeBatch (UniqueID NVARCHAR(100) PRIMARY KEY)";
                    createTempCmd.ExecuteNonQuery();

                    using (var tempTransaction = azureConn.BeginTransaction())
                    {
                        var insertTempCmd = azureConn.CreateCommand();
                        insertTempCmd.Transaction = tempTransaction;
                        insertTempCmd.CommandText = "INSERT INTO #ExcludeBatch (UniqueID) VALUES (@uid)";
                        var uidParam = insertTempCmd.Parameters.Add("@uid", SqlDbType.NVarChar, 100);

                        foreach (var uid in excludeUniqueIds)
                        {
                            uidParam.Value = uid;
                            insertTempCmd.ExecuteNonQuery();
                        }

                        tempTransaction.Commit();
                    }
                }

                foreach (var projectId in selectedProjects)
                {
                    long lastPulledVersion = Convert.ToInt64(
                        SettingsManager.GetAppSetting($"LastPulledSyncVersion_{projectId}", "0")
                    );

                    var pullCmd = azureConn.CreateCommand();

                    // Use LEFT JOIN with NULL check to exclude pushed records
                    if (excludeUniqueIds != null && excludeUniqueIds.Any())
                    {
                        pullCmd.CommandText = @"
                            SELECT a.* FROM Activities a
                            LEFT JOIN #ExcludeBatch e ON a.[UniqueID] = e.[UniqueID]
                            WHERE a.[ProjectID] = @projectId 
                              AND a.[SyncVersion] > @lastVersion
                              AND e.[UniqueID] IS NULL
                            ORDER BY a.[SyncVersion]";
                    }
                    else
                    {
                        pullCmd.CommandText = @"
                            SELECT * FROM Activities 
                            WHERE [ProjectID] = @projectId 
                              AND [SyncVersion] > @lastVersion
                            ORDER BY [SyncVersion]";
                    }

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
                            int isDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")) ? 1 : 0;

                            if (isDeleted == 1)
                            {
                                var deleteCmd = localConn.CreateCommand();
                                deleteCmd.CommandText = "DELETE FROM Activities WHERE UniqueID = @uid";
                                deleteCmd.Parameters.AddWithValue("@uid", uniqueId);
                                deleteCmd.ExecuteNonQuery();

                                if (syncVersion > maxVersionPulled)
                                {
                                    maxVersionPulled = syncVersion;
                                }

                                continue;
                            }

                            var columnNames = new List<string>();
                            var paramNames = new List<string>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (colName == "IsDeleted") continue;

                                columnNames.Add(colName);
                                paramNames.Add($"@{colName}");
                            }

                            columnNames.Add("LocalDirty");
                            paramNames.Add("@LocalDirty");

                            var insertSql = $@"
                                INSERT OR REPLACE INTO Activities ({string.Join(", ", columnNames)}) 
                                VALUES ({string.Join(", ", paramNames)})";

                            var insertCmd = localConn.CreateCommand();
                            insertCmd.CommandText = insertSql;

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (colName == "IsDeleted") continue;

                                var value = reader.GetValue(i);
                                insertCmd.Parameters.AddWithValue($"@{colName}", value ?? DBNull.Value);
                            }

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
                    reader.Close();
                    // Update LastPulledSyncVersion
                    var maxVersionCmd = azureConn.CreateCommand();
                    maxVersionCmd.CommandText = "SELECT ISNULL(MAX(SyncVersion), 0) FROM Activities WHERE ProjectID = @projectId";
                    maxVersionCmd.Parameters.AddWithValue("@projectId", projectId);
                    long azureMaxVersion = Convert.ToInt64(maxVersionCmd.ExecuteScalar());

                    SettingsManager.SetAppSetting(
                        $"LastPulledSyncVersion_{projectId}",
                        azureMaxVersion.ToString(),
                        "int"
                    );
                }

                // Temp table automatically dropped when connection closes

                AppLogger.Info($"Pull completed: {result.PulledRecords} pulled", "SyncManager.PullRecords");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "SyncManager.PullRecords");
            }

            return result;
        }

        // Legacy method signature for compatibility
        [Obsolete("Use PullRecordsAsync(List<string> selectedProjects, List<string> excludeUniqueIds) instead. The centralDbPath parameter is ignored.")]
        public static async Task<SyncResult> PullRecordsAsync(string centralDbPath, List<string> selectedProjects, List<string> excludeUniqueIds = null)
        {
            return await PullRecordsAsync(selectedProjects, excludeUniqueIds);
        }
    }
}