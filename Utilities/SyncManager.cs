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
        private static object? GetActivityValue(Activity activity, string columnName)
        {
            return columnName switch
            {
                "Area" => activity.Area,
                "AssignedTo" => activity.AssignedTo,
                "AzureUploadUtcDate" => activity.AzureUploadUtcDate?.ToString("yyyy-MM-dd HH:mm:ss"),
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
                "LineNumber" => activity.LineNumber,
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
                "ActFin" => activity.ActFin,
                "ActStart" => activity.ActStart,
                "SecondActno" => activity.SecondActno,
                "SecondDwgNO" => activity.SecondDwgNO,
                "Service" => activity.Service,
                "ShopField" => activity.ShopField,
                "ShtNO" => activity.ShtNO,
                "SubArea" => activity.SubArea,
                "PjtSystem" => activity.PjtSystem,
                "PjtSystemNo" => activity.PjtSystemNo,
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
                "RespParty" => activity.RespParty,
                "UDF20" => activity.UDF20,
                "UpdatedBy" => activity.UpdatedBy,
                "UpdatedUtcDate" => activity.UpdatedUtcDate?.ToString("yyyy-MM-dd HH:mm:ss"),
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

                // Columns to sync to Azure (AzureUploadUtcDate excluded - pull-only field set by admin)
                var syncColumns = new List<string>
                {
                    "Area", "AssignedTo", "Aux1", "Aux2", "Aux3",
                    "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "BudgetMHs",
                    "ChgOrdNO", "ClientBudget", "ClientCustom3", "ClientEquivQty",
                    "CompType", "CreatedBy", "DateTrigger", "Description", "DwgNO",
                    "EarnQtyEntry", "EarnedMHsRoc", "EqmtNO", "EquivQTY", "EquivUOM",
                    "Estimator", "HexNO", "HtTrace", "InsulType", "LineNumber",
                    "MtrlSpec", "Notes", "PaintCode", "PercentEntry", "PhaseCategory",
                    "PhaseCode", "PipeGrade", "PipeSize1", "PipeSize2",
                    "PrevEarnMHs", "PrevEarnQTY", "ProgDate", "ProjectID",
                    "Quantity", "RevNO", "RFINO", "ROCBudgetQTY", "ROCID",
                    "ROCPercent", "ROCStep", "SchedActNO", "ActFin", "ActStart",
                    "SecondActno", "SecondDwgNO", "Service", "ShopField", "ShtNO",
                    "SubArea", "PjtSystem", "PjtSystemNo", "TagNO",
                    "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7", "UDF8", "UDF9", "UDF10",
                    "UDF11", "UDF12", "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "RespParty", "UDF20",
                    "UpdatedBy", "UpdatedUtcDate", "UOM", "WeekEndDate", "WorkPackage", "XRay"
                };

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // ============================================================
                // STEP 1: Create temp table and populate with all dirty UniqueIDs
                // ============================================================
                var createTempCmd = azureConn.CreateCommand();
                createTempCmd.CommandText = @"
                    IF OBJECT_ID('tempdb..#SyncBatch') IS NOT NULL DROP TABLE #SyncBatch;
                    CREATE TABLE #SyncBatch (UniqueID NVARCHAR(100) PRIMARY KEY)";
                createTempCmd.ExecuteNonQuery();

                // Bulk insert UniqueIDs to temp table using DataTable
                var uniqueIdTable = new DataTable();
                uniqueIdTable.Columns.Add("UniqueID", typeof(string));
                foreach (var record in dirtyRecords)
                {
                    uniqueIdTable.Rows.Add(record.UniqueID);
                }

                using (var bulkCopy = new SqlBulkCopy(azureConn))
                {
                    bulkCopy.DestinationTableName = "#SyncBatch";
                    bulkCopy.WriteToServer(uniqueIdTable);
                }

                AppLogger.Info($"Step 1 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Populated temp table with {dirtyRecords.Count} UniqueIDs", "SyncManager.PushRecords");

                // ============================================================
                // STEP 2: Find which UniqueIDs already exist in Azure (using JOIN)
                // ============================================================
                var existingIds = new HashSet<string>();
                var checkCmd = azureConn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT a.[UniqueID] 
                    FROM VMS_Activities a 
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

                AppLogger.Info($"Step 2 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Records to insert: {toInsert.Count}, Records to update: {toUpdate.Count}", "SyncManager.PushRecords");

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
                        FROM VMS_Activities a 
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

                        if (ownershipMap.TryGetValue(r.UniqueID, out string? owner) && owner == r.AssignedTo)
                        {
                            return true;
                        }
                        result.FailedRecords.Add($"UniqueID {r.UniqueID}: No longer assigned to you");
                        return false;
                    }).ToList();

                    // Bulk update using staging table approach
                    if (validUpdates.Count > 0)
                    {
                        // Create staging table for updates
                        var createStagingCmd = azureConn.CreateCommand();
                        createStagingCmd.CommandText = @"
                            IF OBJECT_ID('tempdb..#UpdateStaging') IS NOT NULL DROP TABLE #UpdateStaging;
                            CREATE TABLE #UpdateStaging (
                                UniqueID NVARCHAR(100) PRIMARY KEY,
                                " + string.Join(",\n                        ", syncColumns.Select(c => $"[{c}] NVARCHAR(MAX)")) + @"
                            )";
                        createStagingCmd.ExecuteNonQuery();

                        // Build DataTable for updates
                        var updateTable = new DataTable();
                        updateTable.Columns.Add("UniqueID", typeof(string));
                        foreach (var colName in syncColumns)
                        {
                            updateTable.Columns.Add(colName, typeof(string));
                        }

                        foreach (var record in validUpdates)
                        {
                            var row = updateTable.NewRow();
                            row["UniqueID"] = record.UniqueID;
                            foreach (var colName in syncColumns)
                            {
                                var value = GetActivityValue(record, colName);
                                row[colName] = value?.ToString() ?? (object)DBNull.Value;
                            }
                            updateTable.Rows.Add(row);
                        }

                        // Bulk insert to staging table
                        using (var bulkCopy = new SqlBulkCopy(azureConn))
                        {
                            bulkCopy.DestinationTableName = "#UpdateStaging";
                            bulkCopy.BulkCopyTimeout = 120;
                            bulkCopy.WriteToServer(updateTable);
                        }

                        // Single UPDATE statement using JOIN
                        var updateFromStagingCmd = azureConn.CreateCommand();
                        updateFromStagingCmd.CommandTimeout = 120;
                        var setClause = string.Join(", ", syncColumns.Select(c => $"a.[{c}] = s.[{c}]"));
                        updateFromStagingCmd.CommandText = $@"
                    UPDATE a
                    SET {setClause}
                    FROM VMS_Activities a
                    INNER JOIN #UpdateStaging s ON a.[UniqueID] = s.[UniqueID]";
                        int updatedCount = updateFromStagingCmd.ExecuteNonQuery();

                        updateSuccessIds.AddRange(validUpdates.Select(r => r.UniqueID));
                        AppLogger.Info($"Step 3 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Bulk updated {updatedCount} records", "SyncManager.PushRecords");
                    }
                }

                // ============================================================
                // STEP 4: Handle INSERTS with SqlBulkCopy (trigger disabled)
                // ============================================================
                var insertSuccessIds = new List<string>();

                if (toInsert.Count > 0)
                {
                    // Disable the trigger for bulk insert performance
                    var disableTriggerCmd = azureConn.CreateCommand();
                    disableTriggerCmd.CommandText = "DISABLE TRIGGER TR_VMS_Activities_SyncVersion ON VMS_Activities";
                    disableTriggerCmd.ExecuteNonQuery();

                    try
                    {
                        // Build DataTable matching Azure Activities schema
                        var dataTable = new DataTable();
                        dataTable.Columns.Add("UniqueID", typeof(string));
                        dataTable.Columns.Add("IsDeleted", typeof(bool));
                        dataTable.Columns.Add("SyncVersion", typeof(long));

                        foreach (var colName in syncColumns)
                        {
                            var colType = GetColumnType(colName);
                            dataTable.Columns.Add(colName, colType);
                        }

                        // Reserve SyncVersion range atomically
                        long startVersion;
                        var reserveCmd = azureConn.CreateCommand();
                        reserveCmd.CommandText = @"
                    UPDATE VMS_GlobalSyncVersion
                    SET CurrentVersion = CurrentVersion + @count
                    OUTPUT INSERTED.CurrentVersion - @count + 1";
                        reserveCmd.Parameters.AddWithValue("@count", toInsert.Count);
                        startVersion = Convert.ToInt64(reserveCmd.ExecuteScalar());

                        AppLogger.Info($"Reserved SyncVersion range {startVersion} to {startVersion + toInsert.Count - 1}", "SyncManager.PushRecords");

                        // Populate DataTable with records and assigned SyncVersions
                        long currentVersion = startVersion;
                        foreach (var record in toInsert)
                        {
                            var row = dataTable.NewRow();
                            row["UniqueID"] = record.UniqueID;
                            row["IsDeleted"] = false;
                            row["SyncVersion"] = currentVersion++;

                            foreach (var colName in syncColumns)
                            {
                                var value = GetActivityValue(record, colName);
                                row[colName] = value ?? DBNull.Value;
                            }

                            dataTable.Rows.Add(row);
                        }

                        // Bulk insert
                        using (var bulkCopy = new SqlBulkCopy(azureConn))
                        {
                            bulkCopy.DestinationTableName = "VMS_Activities";
                            bulkCopy.BatchSize = 5000;
                            bulkCopy.BulkCopyTimeout = 120;

                            // Map columns explicitly
                            bulkCopy.ColumnMappings.Add("UniqueID", "UniqueID");
                            bulkCopy.ColumnMappings.Add("IsDeleted", "IsDeleted");
                            bulkCopy.ColumnMappings.Add("SyncVersion", "SyncVersion");
                            foreach (var colName in syncColumns)
                            {
                                bulkCopy.ColumnMappings.Add(colName, colName);
                            }

                            bulkCopy.WriteToServer(dataTable);
                        }

                        insertSuccessIds.AddRange(toInsert.Select(r => r.UniqueID));
                        AppLogger.Info($"Step 4 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Bulk inserted {toInsert.Count} records", "SyncManager.PushRecords");
                    }
                    finally
                    {
                        // Re-enable the trigger
                        var enableTriggerCmd = azureConn.CreateCommand();
                        enableTriggerCmd.CommandText = "ENABLE TRIGGER TR_VMS_Activities_SyncVersion ON VMS_Activities";
                        enableTriggerCmd.ExecuteNonQuery();
                    }
                }

                // ============================================================
                // STEP 5: Get SyncVersions and ActivityIDs (using JOIN)
                // ============================================================
                var allSuccessIds = updateSuccessIds.Concat(insertSuccessIds).ToList();

                if (allSuccessIds.Count > 0)
                {
                    // Rebuild temp table with only successful IDs using bulk copy
                    var clearTempCmd = azureConn.CreateCommand();
                    clearTempCmd.CommandText = "DELETE FROM #SyncBatch";
                    clearTempCmd.ExecuteNonQuery();

                    var successIdTable = new DataTable();
                    successIdTable.Columns.Add("UniqueID", typeof(string));
                    foreach (var uid in allSuccessIds)
                    {
                        successIdTable.Rows.Add(uid);
                    }

                    using (var bulkCopy = new SqlBulkCopy(azureConn))
                    {
                        bulkCopy.DestinationTableName = "#SyncBatch";
                        bulkCopy.WriteToServer(successIdTable);
                    }

                    // Get versions using JOIN
                    var versionMap = new Dictionary<string, long>();
                    var activityIdMap = new Dictionary<string, int>();

                    var getVersionsCmd = azureConn.CreateCommand();
                    getVersionsCmd.CommandText = @"
                SELECT a.[UniqueID], a.[SyncVersion], a.[ActivityID] 
                FROM VMS_Activities a 
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

                    AppLogger.Info($"Step 5 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Retrieved versions for {versionMap.Count} records", "SyncManager.PushRecords");

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

                    AppLogger.Info($"Step 6 complete ({stopwatch.Elapsed.TotalSeconds:F1}s): Updated local records", "SyncManager.PushRecords");

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

                        var failedIdTable = new DataTable();
                        failedIdTable.Columns.Add("UniqueID", typeof(string));
                        foreach (var uid in failedUniqueIds)
                        {
                            failedIdTable.Rows.Add(uid);
                        }

                        using (var bulkCopy = new SqlBulkCopy(azureConn))
                        {
                            bulkCopy.DestinationTableName = "#SyncBatch";
                            bulkCopy.WriteToServer(failedIdTable);
                        }

                        // Force-pull using JOIN
                        var forcePullCmd = azureConn.CreateCommand();
                        forcePullCmd.CommandText = @"
                    SELECT a.* FROM VMS_Activities a 
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

                stopwatch.Stop();
                AppLogger.Info($"Push completed in {stopwatch.Elapsed.TotalSeconds:F1}s: {result.PushedRecords} pushed ({result.InsertedRecords} inserted, {result.UpdatedRecords} updated), {result.FailedRecords.Count} failed", "SyncManager.PushRecords");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "SyncManager.PushRecords");
            }

            return result;
        }

        // Helper method to get column type for DataTable
        private static Type GetColumnType(string columnName)
        {
            // Numeric columns
            var numericColumns = new HashSet<string>
            {
                "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "BudgetMHs",
                "ClientBudget", "ClientCustom3", "ClientEquivQty", "DateTrigger",
                "EarnQtyEntry", "EarnedMHsRoc", "HexNO", "PercentEntry",
                "PipeSize1", "PipeSize2", "PrevEarnMHs", "PrevEarnQTY",
                "Quantity", "ROCBudgetQTY", "ROCID", "ROCPercent", "XRay"
            };

            if (numericColumns.Contains(columnName))
            {
                return typeof(double);
            }

            return typeof(string);
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
            public string? ErrorMessage { get; set; }
            public List<string> PushedUniqueIds { get; set; } = new List<string>();
        }

        // Pull records from Azure that have changed since last sync
        public static async Task<SyncResult> PullRecordsAsync(List<string> selectedProjects, string? ownerFilter = null)
        {
            // Note: excludeUniqueIds parameter kept for backward compatibility but ignored
            var result = new SyncResult();

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using var azureConn = AzureDbManager.GetConnection();
                using var localConn = DatabaseSetup.GetConnection();

                await Task.Run(() => azureConn.Open());
                localConn.Open();

                foreach (var projectId in selectedProjects)
                {
                    long lastPulledVersion = Convert.ToInt64(
                        SettingsManager.GetAppSetting($"LastPulledSyncVersion_{projectId}", "0")
                    );

                    var pullCmd = azureConn.CreateCommand();
                    pullCmd.CommandTimeout = 120;

                    // Build query with optional owner filter
                    var sql = @"
                            SELECT * FROM VMS_Activities
                            WHERE [ProjectID] = @projectId
                              AND [SyncVersion] > @lastVersion";

                    if (!string.IsNullOrEmpty(ownerFilter))
                    {
                        sql += " AND [AssignedTo] = @ownerFilter";
                    }

                    sql += " ORDER BY [SyncVersion]";

                    pullCmd.CommandText = sql;
                    pullCmd.Parameters.AddWithValue("@projectId", projectId);
                    pullCmd.Parameters.AddWithValue("@lastVersion", lastPulledVersion);

                    if (!string.IsNullOrEmpty(ownerFilter))
                    {
                        pullCmd.Parameters.AddWithValue("@ownerFilter", ownerFilter);
                    }

                    // Read all records into memory first
                    var recordsToPull = new List<Dictionary<string, object>>();
                    var recordsToDelete = new List<string>();
                    long maxVersionPulled = lastPulledVersion;

                    using (var reader = await Task.Run(() => pullCmd.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            string uniqueId = reader.GetString(reader.GetOrdinal("UniqueID"));
                            long syncVersion = reader.GetInt64(reader.GetOrdinal("SyncVersion"));
                            int isDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")) ? 1 : 0;

                            if (syncVersion > maxVersionPulled)
                            {
                                maxVersionPulled = syncVersion;
                            }

                            if (isDeleted == 1)
                            {
                                recordsToDelete.Add(uniqueId);
                                continue;
                            }

                            // Store record data
                            var recordData = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (colName == "IsDeleted") continue;
                                recordData[colName] = reader.GetValue(i);
                            }
                            recordsToPull.Add(recordData);
                        }
                    }

                    AppLogger.Info($"Project {projectId}: {recordsToPull.Count} to pull, {recordsToDelete.Count} to delete", "SyncManager.PullRecords");

                    // Bulk delete records marked as deleted in Azure
                    if (recordsToDelete.Count > 0)
                    {
                        using var deleteTransaction = localConn.BeginTransaction();
                        var deleteCmd = localConn.CreateCommand();
                        deleteCmd.Transaction = deleteTransaction;
                        deleteCmd.CommandText = "DELETE FROM Activities WHERE UniqueID = @uid";
                        deleteCmd.Parameters.Add("@uid", SqliteType.Text);

                        foreach (var uid in recordsToDelete)
                        {
                            deleteCmd.Parameters["@uid"].Value = uid;
                            deleteCmd.ExecuteNonQuery();
                        }

                        deleteTransaction.Commit();
                        AppLogger.Info($"Deleted {recordsToDelete.Count} records from local (marked deleted in Azure)", "SyncManager.PullRecords");
                    }

                    // Bulk insert/update pulled records
                    if (recordsToPull.Count > 0)
                    {
                        // Get column names from first record
                        var colNames = recordsToPull[0].Keys.ToList();
                        colNames.Add("LocalDirty");

                        var insertSql = $@"
                    INSERT OR REPLACE INTO Activities ({string.Join(", ", colNames)}) 
                    VALUES ({string.Join(", ", colNames.Select(c => "@" + c))})";

                        using var insertTransaction = localConn.BeginTransaction();
                        var insertCmd = localConn.CreateCommand();
                        insertCmd.Transaction = insertTransaction;
                        insertCmd.CommandText = insertSql;

                        // Add parameters once
                        foreach (var colName in colNames)
                        {
                            if (colName == "LocalDirty")
                                insertCmd.Parameters.Add("@LocalDirty", SqliteType.Integer);
                            else
                                insertCmd.Parameters.Add("@" + colName, SqliteType.Text);
                        }

                        foreach (var record in recordsToPull)
                        {
                            foreach (var colName in record.Keys)
                            {
                                insertCmd.Parameters["@" + colName].Value = record[colName] ?? DBNull.Value;
                            }
                            insertCmd.Parameters["@LocalDirty"].Value = 0;
                            insertCmd.ExecuteNonQuery();
                        }

                        insertTransaction.Commit();
                        result.PulledRecords += recordsToPull.Count;
                    }

                    // Update LastPulledSyncVersion to Azure's max for this project
                    var maxVersionCmd = azureConn.CreateCommand();
                    maxVersionCmd.CommandText = "SELECT ISNULL(MAX(SyncVersion), 0) FROM VMS_Activities WHERE [ProjectID] = @projectId";
                    maxVersionCmd.Parameters.AddWithValue("@projectId", projectId);
                    long azureMaxVersion = Convert.ToInt64(maxVersionCmd.ExecuteScalar());

                    SettingsManager.SetAppSetting(
                        $"LastPulledSyncVersion_{projectId}",
                        azureMaxVersion.ToString(),
                        "int"
                    );
                }

                stopwatch.Stop();
                var filterInfo = string.IsNullOrEmpty(ownerFilter) ? "" : $" (filtered to {ownerFilter})";
                AppLogger.Info($"Pull completed in {stopwatch.Elapsed.TotalSeconds:F1}s: {result.PulledRecords} pulled{filterInfo}", "SyncManager.PullRecords");
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