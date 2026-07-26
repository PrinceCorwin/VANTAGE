using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VANTAGE.Utilities;

namespace VANTAGE.Data
{
    // Shared cloud library of Project Dashboard report layouts, stored in dbo.VMS_ReportLayouts on the
    // central projectcontrols database. NOTE: projectcontrols is shared with REQit — this table is
    // additive and REQit-neutral (no existing schema is touched). The table is created/managed on the
    // server side (created manually 2026-07-25); the app never runs DDL. Schema for reference:
    //   LayoutId UNIQUEIDENTIFIER PK, Name NVARCHAR(120), Author NVARCHAR(120), SchemaVersion INT,
    //   LayoutJson NVARCHAR(MAX), CreatedUtc DATETIME2, UpdatedUtc DATETIME2
    // Publish pushes a copy; Import pulls a copy down into the local list (no live link). Deletion is admin-only.
    public static class AzureReportLayoutRepository
    {
        public sealed class CloudLayout
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public DateTime UpdatedUtc { get; set; }
        }

        // Publish (upsert by Name + Author): re-publishing your own named layout updates that copy
        // rather than creating duplicates. Different authors can share the same layout name.
        public static async Task PublishAsync(string name, string author, int schemaVersion, string layoutJson)
        {
            using var conn = AzureDbManager.GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
MERGE dbo.VMS_ReportLayouts AS t
USING (SELECT @name AS Name, @author AS Author) AS s
ON (t.Name = s.Name AND t.Author = s.Author)
WHEN MATCHED THEN
    UPDATE SET LayoutJson = @json, SchemaVersion = @ver, UpdatedUtc = @now
WHEN NOT MATCHED THEN
    INSERT (LayoutId, Name, Author, SchemaVersion, LayoutJson, CreatedUtc, UpdatedUtc)
    VALUES (NEWID(), @name, @author, @ver, @json, @now, @now);";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@author", author);
            cmd.Parameters.AddWithValue("@ver", schemaVersion);
            cmd.Parameters.AddWithValue("@json", layoutJson);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<List<CloudLayout>> GetListAsync()
        {
            var list = new List<CloudLayout>();
            using var conn = AzureDbManager.GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT LayoutId, Name, Author, UpdatedUtc FROM dbo.VMS_ReportLayouts ORDER BY Name";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new CloudLayout
                {
                    Id = r.GetGuid(0),
                    Name = r.GetString(1),
                    Author = r.GetString(2),
                    UpdatedUtc = r.GetDateTime(3)
                });
            }
            return list;
        }

        public static async Task<string?> GetJsonAsync(Guid id)
        {
            using var conn = AzureDbManager.GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT LayoutJson FROM dbo.VMS_ReportLayouts WHERE LayoutId = @id";
            cmd.Parameters.AddWithValue("@id", id);
            var o = await cmd.ExecuteScalarAsync();
            return o as string;
        }

        // Admin-only (caller gates on AzureDbManager.IsUserAdmin).
        public static async Task DeleteAsync(Guid id)
        {
            using var conn = AzureDbManager.GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM dbo.VMS_ReportLayouts WHERE LayoutId = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
