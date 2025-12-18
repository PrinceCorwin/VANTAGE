using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using VANTAGE.Data;

namespace VANTAGE.Utilities
{
    public class SplitOwnershipIssue
    {
        public string SchedActNO { get; set; } = null!;
        public string AzureOwner { get; set; } = null!;
        public int LocalCount { get; set; }
    }

    public static class OwnershipHelper
    {
        // Check for split ownership issues between local and Azure
        public static async Task<List<SplitOwnershipIssue>> CheckSplitOwnershipAsync(
            string projectId,
            string currentUser)
        {
            return await Task.Run(() =>
            {
                var issues = new List<SplitOwnershipIssue>();

                // Get local SchedActNOs for current user and selected project
                var localSchedActNOs = new Dictionary<string, int>();
                using (var localConn = DatabaseSetup.GetConnection())
                {
                    localConn.Open();
                    var localCmd = localConn.CreateCommand();
                    localCmd.CommandText = @"
                        SELECT SchedActNO, COUNT(*) as Cnt
                        FROM Activities
                        WHERE AssignedTo = @username
                          AND ProjectID = @projectId
                          AND SchedActNO IS NOT NULL
                          AND SchedActNO != ''
                        GROUP BY SchedActNO";
                    localCmd.Parameters.AddWithValue("@username", currentUser);
                    localCmd.Parameters.AddWithValue("@projectId", projectId);

                    using var reader = localCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        localSchedActNOs[reader.GetString(0)] = reader.GetInt32(1);
                    }
                }

                if (!localSchedActNOs.Any())
                    return issues;

                // Query Azure for same SchedActNOs owned by different users
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                // Create temp table for bulk lookup
                var createTempCmd = azureConn.CreateCommand();
                createTempCmd.CommandText = "CREATE TABLE #SchedCheck (SchedActNO NVARCHAR(100) PRIMARY KEY)";
                createTempCmd.ExecuteNonQuery();

                using (var bulkCopy = new SqlBulkCopy(azureConn))
                {
                    bulkCopy.DestinationTableName = "#SchedCheck";

                    var dt = new DataTable();
                    dt.Columns.Add("SchedActNO", typeof(string));
                    foreach (var schedActNO in localSchedActNOs.Keys)
                    {
                        dt.Rows.Add(schedActNO);
                    }
                    bulkCopy.WriteToServer(dt);
                }

                // Find SchedActNOs owned by someone else on Azure
                var checkCmd = azureConn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT DISTINCT s.SchedActNO, a.AssignedTo
                    FROM #SchedCheck s
                    INNER JOIN Activities a ON a.SchedActNO = s.SchedActNO
                    WHERE a.ProjectID = @projectId
                      AND a.AssignedTo != @username
                      AND a.AssignedTo IS NOT NULL
                      AND a.AssignedTo != ''
                      AND a.IsDeleted = 0";
                checkCmd.Parameters.AddWithValue("@projectId", projectId);
                checkCmd.Parameters.AddWithValue("@username", currentUser);

                using var azureReader = checkCmd.ExecuteReader();
                while (azureReader.Read())
                {
                    var schedActNO = azureReader.GetString(0);
                    var azureOwner = azureReader.GetString(1);
                    var localCount = localSchedActNOs.GetValueOrDefault(schedActNO, 0);
                    issues.Add(new SplitOwnershipIssue
                    {
                        SchedActNO = schedActNO,
                        AzureOwner = azureOwner,
                        LocalCount = localCount
                    });
                }

                return issues;
            });
        }

        // Resolve split ownership by reassigning local records to Azure owners
        public static async Task<int> ResolveSplitOwnershipAsync(
            string projectId,
            string currentUser,
            List<SplitOwnershipIssue> issues,
            IProgress<string>? progress = null)
        {
            return await Task.Run(async () =>
            {
                int count = 0;

                // Get the UniqueIDs of records to reassign (from local)
                var recordsToReassign = new List<(string UniqueID, string NewOwner)>();

                using (var localConn = DatabaseSetup.GetConnection())
                {
                    localConn.Open();

                    foreach (var issue in issues)
                    {
                        var selectCmd = localConn.CreateCommand();
                        selectCmd.CommandText = @"
                            SELECT UniqueID
                            FROM Activities
                            WHERE AssignedTo = @currentUser
                              AND ProjectID = @projectId
                              AND SchedActNO = @schedActNO";
                        selectCmd.Parameters.AddWithValue("@currentUser", currentUser);
                        selectCmd.Parameters.AddWithValue("@projectId", projectId);
                        selectCmd.Parameters.AddWithValue("@schedActNO", issue.SchedActNO);

                        using var reader = selectCmd.ExecuteReader();
                        while (reader.Read())
                        {
                            recordsToReassign.Add((reader.GetString(0), issue.AzureOwner));
                        }
                    }
                }

                if (!recordsToReassign.Any())
                    return 0;

                progress?.Report("Clearing Azure records...");

                // Group by new owner for bulk operations
                var byOwner = recordsToReassign.GroupBy(r => r.NewOwner);

                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                foreach (var ownerGroup in byOwner)
                {
                    var uniqueIds = ownerGroup.Select(r => r.UniqueID).ToList();

                    // Create temp table for this batch
                    var createTempCmd = azureConn.CreateCommand();
                    createTempCmd.CommandText = "CREATE TABLE #ReassignBatch (UniqueID NVARCHAR(50) PRIMARY KEY)";
                    createTempCmd.ExecuteNonQuery();

                    using (var bulkCopy = new SqlBulkCopy(azureConn))
                    {
                        bulkCopy.DestinationTableName = "#ReassignBatch";

                        var dt = new DataTable();
                        dt.Columns.Add("UniqueID", typeof(string));
                        foreach (var id in uniqueIds)
                        {
                            dt.Rows.Add(id);
                        }
                        bulkCopy.WriteToServer(dt);
                    }

                    // Delete existing records on Azure for these UniqueIDs
                    var deleteCmd = azureConn.CreateCommand();
                    deleteCmd.CommandText = @"
                        DELETE FROM Activities 
                        WHERE UniqueID IN (SELECT UniqueID FROM #ReassignBatch)";
                    deleteCmd.ExecuteNonQuery();

                    // Drop temp table
                    var dropCmd = azureConn.CreateCommand();
                    dropCmd.CommandText = "DROP TABLE #ReassignBatch";
                    dropCmd.ExecuteNonQuery();
                }

                azureConn.Close();

                progress?.Report("Updating local records...");

                // Update local records with new owner
                using (var localConn = DatabaseSetup.GetConnection())
                {
                    localConn.Open();

                    foreach (var issue in issues)
                    {
                        var updateCmd = localConn.CreateCommand();
                        updateCmd.CommandText = @"
                            UPDATE Activities
                            SET AssignedTo = @newOwner,
                                UpdatedBy = @updatedBy,
                                UpdatedUtcDate = @updatedDate,
                                LocalDirty = 1
                            WHERE AssignedTo = @currentUser
                              AND ProjectID = @projectId
                              AND SchedActNO = @schedActNO";
                        updateCmd.Parameters.AddWithValue("@newOwner", issue.AzureOwner);
                        updateCmd.Parameters.AddWithValue("@updatedBy", currentUser);
                        updateCmd.Parameters.AddWithValue("@updatedDate", DateTime.UtcNow.ToString("o"));
                        updateCmd.Parameters.AddWithValue("@currentUser", currentUser);
                        updateCmd.Parameters.AddWithValue("@projectId", projectId);
                        updateCmd.Parameters.AddWithValue("@schedActNO", issue.SchedActNO);

                        count += updateCmd.ExecuteNonQuery();
                    }
                }

                progress?.Report("Syncing reassigned records...");

                // Push the reassigned records
                await SyncManager.PushRecordsAsync(new List<string> { projectId });

                AppLogger.Info($"Resolved split ownership: {count} records reassigned",
                    "OwnershipHelper.ResolveSplitOwnershipAsync", currentUser);

                return count;
            });
        }

        // Build a user-friendly message for split ownership issues
        public static string BuildSplitOwnershipMessage(List<SplitOwnershipIssue> issues)
        {
            var byOwner = issues
                .GroupBy(x => x.AzureOwner)
                .Select(g => new
                {
                    Owner = g.Key,
                    Count = g.Sum(x => x.LocalCount),
                    SchedActNOs = g.Select(x => x.SchedActNO).ToList()
                })
                .ToList();

            var message = "Split ownership detected. The following SchedActNOs are owned by other users on Azure:\n\n";

            foreach (var group in byOwner.Take(5))
            {
                var actNOList = string.Join(", ", group.SchedActNOs.Take(5));
                if (group.SchedActNOs.Count > 5)
                    actNOList += $" (+{group.SchedActNOs.Count - 5} more)";

                message += $"• {group.Owner}: {group.Count} records\n  SchedActNOs: {actNOList}\n\n";
            }

            if (byOwner.Count > 5)
                message += $"... and {byOwner.Count - 5} more users\n\n";

            var totalRecords = byOwner.Sum(g => g.Count);
            message += $"Would you like to reassign your {totalRecords} record(s) to their respective owners and continue?\n\n" +
                       "Click YES to reassign and continue.\n" +
                       "Click NO to cancel and review manually.";

            return message;
        }
    }
}