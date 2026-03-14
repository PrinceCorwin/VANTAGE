using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using VANTAGE.Utilities;

namespace VANTAGE.Data
{
    // Azure CRUD for VMS_ProjectRates (per-project named rate sets)
    public static class ProjectRateRepository
    {
        // Bulk import rates for a project+set using SqlBulkCopy for performance
        public static async Task ImportProjectRatesAsync(
            string projectId, string setName, List<ProjectRateItem> items, string username)
        {
            await Task.Run(() =>
            {
                using var conn = AzureDbManager.GetConnection();
                conn.Open();

                // Delete existing rates for this project+set
                using (var delCmd = conn.CreateCommand())
                {
                    delCmd.CommandText = "DELETE FROM VMS_ProjectRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                    delCmd.Parameters.AddWithValue("@ProjectID", projectId);
                    delCmd.Parameters.AddWithValue("@SetName", setName);
                    delCmd.ExecuteNonQuery();
                }

                if (items.Count == 0) return;

                // Build DataTable for SqlBulkCopy
                var table = new System.Data.DataTable();
                table.Columns.Add("ProjectID", typeof(string));
                table.Columns.Add("SetName", typeof(string));
                table.Columns.Add("Item", typeof(string));
                table.Columns.Add("Size", typeof(double));
                table.Columns.Add("SchClass", typeof(string));
                table.Columns.Add("Unit", typeof(string));
                table.Columns.Add("MH", typeof(double));
                table.Columns.Add("CreatedBy", typeof(string));
                table.Columns.Add("CreatedUtcDate", typeof(DateTime));
                table.Columns.Add("UpdatedBy", typeof(string));
                table.Columns.Add("UpdatedUtcDate", typeof(DateTime));

                var now = DateTime.UtcNow;
                foreach (var item in items)
                {
                    table.Rows.Add(
                        projectId,
                        setName,
                        item.Item,
                        item.Size,
                        string.IsNullOrEmpty(item.SchClass) ? (object)DBNull.Value : item.SchClass,
                        string.IsNullOrEmpty(item.Unit) ? "EA" : item.Unit,
                        item.MH,
                        username,
                        now,
                        username,
                        now
                    );
                }

                using var bulkCopy = new SqlBulkCopy(conn)
                {
                    DestinationTableName = "VMS_ProjectRates",
                    BulkCopyTimeout = 120
                };

                // Map columns explicitly (skip Id — it's identity)
                bulkCopy.ColumnMappings.Add("ProjectID", "ProjectID");
                bulkCopy.ColumnMappings.Add("SetName", "SetName");
                bulkCopy.ColumnMappings.Add("Item", "Item");
                bulkCopy.ColumnMappings.Add("Size", "Size");
                bulkCopy.ColumnMappings.Add("SchClass", "SchClass");
                bulkCopy.ColumnMappings.Add("Unit", "Unit");
                bulkCopy.ColumnMappings.Add("MH", "MH");
                bulkCopy.ColumnMappings.Add("CreatedBy", "CreatedBy");
                bulkCopy.ColumnMappings.Add("CreatedUtcDate", "CreatedUtcDate");
                bulkCopy.ColumnMappings.Add("UpdatedBy", "UpdatedBy");
                bulkCopy.ColumnMappings.Add("UpdatedUtcDate", "UpdatedUtcDate");

                bulkCopy.WriteToServer(table);
            });
        }

        // Delete an entire rate set
        public static async Task DeleteRateSetAsync(string projectId, string setName)
        {
            await Task.Run(() =>
            {
                using var conn = AzureDbManager.GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM VMS_ProjectRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                cmd.Parameters.AddWithValue("@ProjectID", projectId);
                cmd.Parameters.AddWithValue("@SetName", setName);
                cmd.ExecuteNonQuery();
            });
        }

        // Build a lookup cache for a specific project+set, keyed same way as RateSheetService
        // Key format: "ITEM-SIZE:SCHCLASS" or "ITEM-SIZE" (no rating)
        public static async Task<Dictionary<string, (double MH, string Unit)>> BuildLookupCacheAsync(
            string projectId, string setName)
        {
            return await Task.Run(() =>
            {
                var cache = new Dictionary<string, (double MH, string Unit)>(StringComparer.OrdinalIgnoreCase);
                using var conn = AzureDbManager.GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Item, Size, SchClass, Unit, MH
                    FROM VMS_ProjectRates
                    WHERE ProjectID = @ProjectID AND SetName = @SetName";
                cmd.Parameters.AddWithValue("@ProjectID", projectId);
                cmd.Parameters.AddWithValue("@SetName", setName);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string item = reader.GetString(0);
                    double size = reader.GetDouble(1);
                    string? schClass = reader.IsDBNull(2) ? null : reader.GetString(2);
                    string unit = reader.GetString(3);
                    double mh = reader.GetDouble(4);

                    string sizeStr = size.ToString("0.###");
                    string key = string.IsNullOrWhiteSpace(schClass)
                        ? $"{item}-{sizeStr}"
                        : $"{item}-{sizeStr}:{schClass}";

                    cache[key] = (mh, unit);
                }
                return cache;
            });
        }

        // Get all rate sets with metadata for the management dialog
        // Returns empty list if table doesn't exist yet
        public static async Task<List<ProjectRateSetInfo>> GetRateSetsAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<ProjectRateSetInfo>();
                try
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT ProjectID, SetName, COUNT(*) as RateCount,
                               MIN(CreatedBy) as CreatedBy, MIN(CreatedUtcDate) as CreatedDate,
                               MAX(UpdatedBy) as UpdatedBy, MAX(UpdatedUtcDate) as UpdatedDate
                        FROM VMS_ProjectRates
                        GROUP BY ProjectID, SetName
                        ORDER BY ProjectID, SetName";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new ProjectRateSetInfo
                        {
                            ProjectID = reader.GetString(0),
                            SetName = reader.GetString(1),
                            RowCount = reader.GetInt32(2),
                            CreatedBy = reader.GetString(3),
                            CreatedDate = reader.GetDateTime(4),
                            UpdatedBy = reader.GetString(5),
                            UpdatedDate = reader.GetDateTime(6)
                        });
                    }
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // Table doesn't exist yet
                }
                return list;
            });
        }

        // Get distinct (ProjectID, SetName) pairs for dropdowns
        // Returns empty list if table doesn't exist yet
        public static async Task<List<(string ProjectID, string SetName)>> GetProjectRateSetsAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<(string, string)>();
                try
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT DISTINCT ProjectID, SetName
                        FROM VMS_ProjectRates
                        ORDER BY ProjectID, SetName";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        list.Add((reader.GetString(0), reader.GetString(1)));
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // Table doesn't exist yet
                }
                return list;
            });
        }

        // Get distinct (ProjectID, SetName) pairs from VMS_ROCRates for dropdown
        // Returns empty list if table doesn't exist yet
        public static async Task<List<(string ProjectID, string SetName)>> GetROCSetsAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<(string, string)>();
                try
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT ProjectID, SetName FROM VMS_ROCRates ORDER BY ProjectID, SetName";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        list.Add((reader.GetString(0), reader.GetString(1)));
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // Table doesn't exist yet
                }
                return list;
            });
        }
    }

    // Summary info for a project rate set
    public class ProjectRateSetInfo
    {
        public string ProjectID { get; set; } = "";
        public string SetName { get; set; } = "";
        public int RowCount { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string UpdatedBy { get; set; } = "";
        public DateTime UpdatedDate { get; set; }
    }

    // Project rate item for data transfer
    public class ProjectRateItem
    {
        public int Id { get; set; }
        public string Item { get; set; } = "";
        public double Size { get; set; }
        public string SchClass { get; set; } = "";
        public string Unit { get; set; } = "EA";
        public double MH { get; set; }
    }
}
