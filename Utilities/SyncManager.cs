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

                // Get Activity properties to build UPDATE dynamically (exclude UniqueID and LocalDirty)
                var properties = typeof(Activity).GetProperties()
                    .Where(p => p.Name != "UniqueID" && p.Name != "LocalDirty" && p.Name != "ActivityID" && p.CanWrite)
                    .ToList();

                // Push each record
                foreach (var record in dirtyRecords)
                {
                    try
                    {
                        // Verify ownership at Central
                        var checkOwnerCmd = centralConn.CreateCommand();
                        checkOwnerCmd.CommandText = "SELECT AssignedTo FROM Activities WHERE UniqueID = @id";
                        checkOwnerCmd.Parameters.AddWithValue("@id", record.UniqueID);
                        var centralOwner = checkOwnerCmd.ExecuteScalar()?.ToString();

                        if (centralOwner != null && centralOwner != record.AssignedTo)
                        {
                            result.FailedRecords.Add($"UniqueID {record.UniqueID}: No longer assigned to you");
                            continue;
                        }

                        // Build UPDATE statement dynamically
                        var setClauses = properties.Select(p => $"{p.Name} = @{p.Name}");
                        var updateSql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE UniqueID = @UniqueID";

                        var updateCmd = centralConn.CreateCommand();
                        updateCmd.CommandText = updateSql;

                        // Add parameters dynamically
                        updateCmd.Parameters.AddWithValue("@UniqueID", record.UniqueID);
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(record);
                            updateCmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
                        }

                        updateCmd.ExecuteNonQuery();

                        // Get assigned SyncVersion from Central
                        var getVersionCmd = centralConn.CreateCommand();
                        getVersionCmd.CommandText = "SELECT SyncVersion FROM Activities WHERE UniqueID = @id";
                        getVersionCmd.Parameters.AddWithValue("@id", record.UniqueID);
                        var versionResult = getVersionCmd.ExecuteScalar();

                        if (versionResult == null)
                        {
                            result.FailedRecords.Add($"UniqueID {record.UniqueID}: Failed to get SyncVersion");
                            continue;
                        }

                        long assignedVersion = Convert.ToInt64(versionResult);

                        // Update local record with new SyncVersion and clear LocalDirty
                        var updateLocalCmd = localConn.CreateCommand();
                        updateLocalCmd.CommandText = @"
                    UPDATE Activities 
                    SET SyncVersion = @version, LocalDirty = 0 
                    WHERE UniqueID = @id";
                        updateLocalCmd.Parameters.AddWithValue("@version", assignedVersion);
                        updateLocalCmd.Parameters.AddWithValue("@id", record.UniqueID);
                        updateLocalCmd.ExecuteNonQuery();

                        result.PushedRecords++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedRecords.Add($"UniqueID {record.UniqueID}: {ex.Message}");
                        AppLogger.Error(ex, $"SyncManager.PushRecords - UniqueID {record.UniqueID}");
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