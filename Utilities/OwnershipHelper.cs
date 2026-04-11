using System.Collections.Generic;
using System.Threading.Tasks;
using VANTAGE.Data;

namespace VANTAGE.Utilities
{
    public class SplitOwnershipIssue
    {
        public string SchedActNO { get; set; } = null!;
        public string AssignedTo { get; set; } = null!;
        public int RecordCount { get; set; }
    }

    public static class OwnershipHelper
    {
        // Query Azure for all SchedActNOs in a project that have multiple owners
        public static async Task<List<SplitOwnershipIssue>> CheckSplitOwnershipAsync(string projectId)
        {
            return await Task.Run(() =>
            {
                var issues = new List<SplitOwnershipIssue>();

                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                // Find all SchedActNOs with more than one distinct owner, then return each owner's count
                var cmd = azureConn.CreateCommand();
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"
                    SELECT a.SchedActNO, a.AssignedTo, COUNT(*) AS RecordCount
                    FROM VMS_Activities a
                    INNER JOIN (
                        SELECT SchedActNO
                        FROM VMS_Activities
                        WHERE ProjectID = @projectId
                          AND IsDeleted = 0
                          AND SchedActNO IS NOT NULL
                          AND SchedActNO != ''
                          AND AssignedTo IS NOT NULL
                          AND AssignedTo != ''
                        GROUP BY SchedActNO
                        HAVING COUNT(DISTINCT AssignedTo) > 1
                    ) split ON a.SchedActNO = split.SchedActNO
                    WHERE a.ProjectID = @projectId
                      AND a.IsDeleted = 0
                      AND a.AssignedTo IS NOT NULL
                      AND a.AssignedTo != ''
                    GROUP BY a.SchedActNO, a.AssignedTo
                    ORDER BY a.SchedActNO, a.AssignedTo";
                cmd.Parameters.AddWithValue("@projectId", projectId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    issues.Add(new SplitOwnershipIssue
                    {
                        SchedActNO = reader.GetString(0),
                        AssignedTo = reader.GetString(1),
                        RecordCount = reader.GetInt32(2)
                    });
                }

                return issues;
            });
        }
    }
}
