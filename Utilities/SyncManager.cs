using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
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

                if (!IsNetworkAvailable())
                {
                    errorMessage = "No network connection detected.\n\nPlease check your internet/network connection and try again.";
                    return false;
                }

                if (centralDbPath.Length >= 2 && centralDbPath[1] == ':')
                {
                    try
                    {
                        DriveInfo drive = new DriveInfo(centralDbPath.Substring(0, 2));

                        if (drive.DriveType == DriveType.Network)
                        {
                            string directory = Path.GetDirectoryName(centralDbPath);

                            try
                            {
                                var files = Directory.GetFiles(directory, "*.db");

                                if (files.Length == 0)
                                {
                                    errorMessage = $"No database files found in network directory.\n\nPath: {directory}";
                                    return false;
                                }
                            }
                            catch (IOException)
                            {
                                errorMessage = $"Network drive is not accessible.\n\nThe network connection may be offline or the path is invalid.";
                                return false;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                errorMessage = "Access denied to network drive.\n\nCheck your permissions.";
                                return false;
                            }
                        }
                    }
                    catch (Exception driveEx)
                    {
                        errorMessage = $"Cannot access drive: {driveEx.Message}";
                        return false;
                    }
                }

                if (!File.Exists(centralDbPath))
                {
                    errorMessage = "Central database file not found.";
                    return false;
                }

                using var connection = new SqliteConnection($"Data Source={centralDbPath}");
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Activities'";
                var result = cmd.ExecuteScalar();

                if (result == null)
                {
                    errorMessage = "Central database is missing Activities table.";
                    return false;
                }

                try
                {
                    var timestamp = DateTime.UtcNow.Ticks;
                    var writeTestCmd = connection.CreateCommand();
                    writeTestCmd.CommandText = $@"
                        CREATE TEMP TABLE ConnectionTest_{timestamp} (TestTime INTEGER);
                        INSERT INTO ConnectionTest_{timestamp} (TestTime) VALUES (@time);
                        DROP TABLE ConnectionTest_{timestamp};";
                    writeTestCmd.Parameters.AddWithValue("@time", timestamp);
                    writeTestCmd.ExecuteNonQuery();
                }
                catch (SqliteException writeEx)
                {
                    errorMessage = $"Cannot write to Central database.\n\nThe database may be read-only, locked, or cached.\n\nError: {writeEx.Message}";
                    return false;
                }

                return true;
            }
            catch (IOException ioEx)
            {
                errorMessage = $"Network or file access error: {ioEx.Message}\n\nThe Central database may be on a disconnected network drive.";
                AppLogger.Error(ioEx, "SyncManager.CheckCentralConnection");
                return false;
            }
            catch (SqliteException sqlEx)
            {
                errorMessage = $"Database connection error: {sqlEx.Message}\n\nThe database file may be locked, corrupted, or offline.";
                AppLogger.Error(sqlEx, "SyncManager.CheckCentralConnection");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Cannot connect to Central database: {ex.Message}";
                AppLogger.Error(ex, "SyncManager.CheckCentralConnection");
                return false;
            }
        }

        // Helper method: Check if network connectivity is available
        private static bool IsNetworkAvailable()
        {
            try
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    return false;
                }

                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        var ipProps = ni.GetIPProperties();

                        foreach (var addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                byte[] bytes = addr.Address.GetAddressBytes();
                                if (!(bytes[0] == 169 && bytes[1] == 254))
                                {
                                    return true;
                                }
                            }
                            else if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                if (!addr.Address.IsIPv6LinkLocal)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Push LocalDirty records to Central using temp table approach for scalability.
        /// No parameter limits - works with any dataset size.
        /// </summary>
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

                AppLogger.Info($"Starting push of {dirtyRecords.Count} records using temp table approach", "SyncManager.PushRecords");

                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                using var localConn = DatabaseSetup.GetConnection();

                centralConn.Open();
                localConn.Open();

                // Columns to sync to Central
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
                var createTempCmd = centralConn.CreateCommand();
                createTempCmd.CommandText = "CREATE TEMP TABLE IF NOT EXISTS SyncBatch (UniqueID TEXT PRIMARY KEY)";
                createTempCmd.ExecuteNonQuery();

                // Clear any previous data
                var clearTempCmd = centralConn.CreateCommand();
                clearTempCmd.CommandText = "DELETE FROM SyncBatch";
                clearTempCmd.ExecuteNonQuery();

                // Insert all UniqueIDs using prepared statement (very fast)
                using (var tempTransaction = centralConn.BeginTransaction())
                {
                    var insertTempCmd = centralConn.CreateCommand();
                    insertTempCmd.Transaction = tempTransaction;
                    insertTempCmd.CommandText = "INSERT OR IGNORE INTO SyncBatch (UniqueID) VALUES (@uid)";
                    insertTempCmd.Parameters.Add("@uid", SqliteType.Text);
                    insertTempCmd.Prepare();

                    foreach (var record in dirtyRecords)
                    {
                        insertTempCmd.Parameters["@uid"].Value = record.UniqueID;
                        insertTempCmd.ExecuteNonQuery();
                    }

                    tempTransaction.Commit();
                }

                AppLogger.Info($"Populated temp table with {dirtyRecords.Count} UniqueIDs", "SyncManager.PushRecords");

                // ============================================================
                // STEP 2: Find which UniqueIDs already exist in Central (using JOIN)
                // ============================================================
                var existingIds = new HashSet<string>();
                var checkCmd = centralConn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT a.UniqueID 
                    FROM Activities a 
                    INNER JOIN SyncBatch s ON a.UniqueID = s.UniqueID";

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
                    // Get ownership and deletion status using JOIN (no parameter limit)
                    var ownershipMap = new Dictionary<string, string>();
                    var deletionMap = new Dictionary<string, int>();

                    var ownerCheckCmd = centralConn.CreateCommand();
                    ownerCheckCmd.CommandText = @"
                        SELECT a.UniqueID, a.AssignedTo, a.IsDeleted 
                        FROM Activities a 
                        INNER JOIN SyncBatch s ON a.UniqueID = s.UniqueID";

                    using (var ownerReader = ownerCheckCmd.ExecuteReader())
                    {
                        while (ownerReader.Read())
                        {
                            ownershipMap[ownerReader.GetString(0)] = ownerReader.GetString(1);
                            deletionMap[ownerReader.GetString(0)] = ownerReader.GetInt32(2);
                        }
                    }

                    // Filter out records not owned by current user OR deleted in Central
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

                    // Perform updates with prepared statement
                    if (validUpdates.Count > 0)
                    {
                        using var transaction = centralConn.BeginTransaction();

                        var setClauses = syncColumns.Select(col => $"{col} = @{col}");
                        var updateSql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE UniqueID = @UniqueID";

                        using var updateCmd = centralConn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = updateSql;

                        updateCmd.Parameters.Add("@UniqueID", SqliteType.Text);
                        foreach (var colName in syncColumns)
                        {
                            updateCmd.Parameters.Add($"@{colName}", SqliteType.Text);
                        }
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

                // ============================================================
                // STEP 4: Handle INSERTS with prepared statement
                // ============================================================
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

                    insertCmd.Parameters.Add("@UniqueID", SqliteType.Text);
                    foreach (var colName in syncColumns)
                    {
                        insertCmd.Parameters.Add($"@{colName}", SqliteType.Text);
                    }
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

                // ============================================================
                // STEP 5: Get SyncVersions and ActivityIDs (using JOIN)
                // ============================================================
                var allSuccessIds = updateSuccessIds.Concat(insertSuccessIds).ToList();

                if (allSuccessIds.Count > 0)
                {
                    // Update temp table to only contain successful IDs
                    var clearTempCmd2 = centralConn.CreateCommand();
                    clearTempCmd2.CommandText = "DELETE FROM SyncBatch";
                    clearTempCmd2.ExecuteNonQuery();

                    using (var tempTransaction = centralConn.BeginTransaction())
                    {
                        var insertTempCmd = centralConn.CreateCommand();
                        insertTempCmd.Transaction = tempTransaction;
                        insertTempCmd.CommandText = "INSERT INTO SyncBatch (UniqueID) VALUES (@uid)";
                        insertTempCmd.Parameters.Add("@uid", SqliteType.Text);
                        insertTempCmd.Prepare();

                        foreach (var uid in allSuccessIds)
                        {
                            insertTempCmd.Parameters["@uid"].Value = uid;
                            insertTempCmd.ExecuteNonQuery();
                        }

                        tempTransaction.Commit();
                    }

                    // Get versions using JOIN
                    var versionMap = new Dictionary<string, long>();
                    var activityIdMap = new Dictionary<string, int>();

                    var getVersionsCmd = centralConn.CreateCommand();
                    getVersionsCmd.CommandText = @"
                        SELECT a.UniqueID, a.SyncVersion, a.ActivityID 
                        FROM Activities a 
                        INNER JOIN SyncBatch s ON a.UniqueID = s.UniqueID";

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

                    // Set result counts
                    result.InsertedRecords = insertSuccessIds.Count;
                    result.UpdatedRecords = updateSuccessIds.Count;
                    result.PushedRecords = result.InsertedRecords + result.UpdatedRecords;
                    result.PushedUniqueIds.AddRange(allSuccessIds);
                }

                // Update statistics after bulk operations
                if (result.InsertedRecords > 100)
                {
                    var analyzeCmd = centralConn.CreateCommand();
                    analyzeCmd.CommandText = "ANALYZE Activities";
                    analyzeCmd.ExecuteNonQuery();
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
                        // Populate temp table with failed IDs
                        var clearTempCmd3 = centralConn.CreateCommand();
                        clearTempCmd3.CommandText = "DELETE FROM SyncBatch";
                        clearTempCmd3.ExecuteNonQuery();

                        using (var tempTransaction = centralConn.BeginTransaction())
                        {
                            var insertTempCmd = centralConn.CreateCommand();
                            insertTempCmd.Transaction = tempTransaction;
                            insertTempCmd.CommandText = "INSERT INTO SyncBatch (UniqueID) VALUES (@uid)";
                            insertTempCmd.Parameters.Add("@uid", SqliteType.Text);
                            insertTempCmd.Prepare();

                            foreach (var uid in failedUniqueIds)
                            {
                                insertTempCmd.Parameters["@uid"].Value = uid;
                                insertTempCmd.ExecuteNonQuery();
                            }

                            tempTransaction.Commit();
                        }

                        // Force-pull using JOIN
                        var forcePullCmd = centralConn.CreateCommand();
                        forcePullCmd.CommandText = @"
                            SELECT a.* FROM Activities a 
                            INNER JOIN SyncBatch s ON a.UniqueID = s.UniqueID";

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

                        AppLogger.Info($"Force-pulled {failedUniqueIds.Count} ownership-conflicted records from Central", "SyncManager.PushRecords");
                    }
                }

                // Clean up temp table
                var dropTempCmd = centralConn.CreateCommand();
                dropTempCmd.CommandText = "DROP TABLE IF EXISTS SyncBatch";
                dropTempCmd.ExecuteNonQuery();

                AppLogger.Info($"Push completed: {result.PushedRecords} pushed ({result.InsertedRecords} inserted, {result.UpdatedRecords} updated), {result.FailedRecords.Count} failed", "SyncManager.PushRecords");
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
            public int InsertedRecords { get; set; }
            public int UpdatedRecords { get; set; }
            public int PulledRecords { get; set; }
            public int SkippedRecords { get; set; }
            public List<string> FailedRecords { get; set; } = new List<string>();
            public string ErrorMessage { get; set; }
            public List<string> PushedUniqueIds { get; set; } = new List<string>();
        }

        /// <summary>
        /// Pull records from Central that have changed since last sync.
        /// Uses temp table approach for large exclude lists.
        /// </summary>
        public static async Task<SyncResult> PullRecordsAsync(string centralDbPath, List<string> selectedProjects, List<string> excludeUniqueIds = null)
        {
            var result = new SyncResult();

            try
            {
                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                using var localConn = DatabaseSetup.GetConnection();

                centralConn.Open();
                localConn.Open();

                // If we have UniqueIDs to exclude, use temp table approach
                if (excludeUniqueIds != null && excludeUniqueIds.Any())
                {
                    var createTempCmd = centralConn.CreateCommand();
                    createTempCmd.CommandText = "CREATE TEMP TABLE IF NOT EXISTS ExcludeBatch (UniqueID TEXT PRIMARY KEY)";
                    createTempCmd.ExecuteNonQuery();

                    var clearTempCmd = centralConn.CreateCommand();
                    clearTempCmd.CommandText = "DELETE FROM ExcludeBatch";
                    clearTempCmd.ExecuteNonQuery();

                    using (var tempTransaction = centralConn.BeginTransaction())
                    {
                        var insertTempCmd = centralConn.CreateCommand();
                        insertTempCmd.Transaction = tempTransaction;
                        insertTempCmd.CommandText = "INSERT OR IGNORE INTO ExcludeBatch (UniqueID) VALUES (@uid)";
                        insertTempCmd.Parameters.Add("@uid", SqliteType.Text);
                        insertTempCmd.Prepare();

                        foreach (var uid in excludeUniqueIds)
                        {
                            insertTempCmd.Parameters["@uid"].Value = uid;
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

                    var pullCmd = centralConn.CreateCommand();

                    // Use LEFT JOIN with NULL check to exclude pushed records
                    if (excludeUniqueIds != null && excludeUniqueIds.Any())
                    {
                        pullCmd.CommandText = @"
                            SELECT a.* FROM Activities a
                            LEFT JOIN ExcludeBatch e ON a.UniqueID = e.UniqueID
                            WHERE a.ProjectID = @projectId 
                              AND a.SyncVersion > @lastVersion
                              AND e.UniqueID IS NULL
                            ORDER BY a.SyncVersion";
                    }
                    else
                    {
                        pullCmd.CommandText = @"
                            SELECT * FROM Activities 
                            WHERE ProjectID = @projectId 
                              AND SyncVersion > @lastVersion
                            ORDER BY SyncVersion";
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
                            int isDeleted = reader.GetInt32(reader.GetOrdinal("IsDeleted"));

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

                    // Update LastPulledSyncVersion
                    var maxVersionCmd = centralConn.CreateCommand();
                    maxVersionCmd.CommandText = "SELECT COALESCE(MAX(SyncVersion), 0) FROM Activities WHERE ProjectID = @projectId";
                    maxVersionCmd.Parameters.AddWithValue("@projectId", projectId);
                    long centralMaxVersion = Convert.ToInt64(maxVersionCmd.ExecuteScalar());

                    SettingsManager.SetAppSetting(
                        $"LastPulledSyncVersion_{projectId}",
                        centralMaxVersion.ToString(),
                        "int"
                    );
                }

                // Clean up temp table
                if (excludeUniqueIds != null && excludeUniqueIds.Any())
                {
                    var dropTempCmd = centralConn.CreateCommand();
                    dropTempCmd.CommandText = "DROP TABLE IF EXISTS ExcludeBatch";
                    dropTempCmd.ExecuteNonQuery();
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