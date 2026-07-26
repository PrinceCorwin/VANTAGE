using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VANTAGE.Utilities;

namespace VANTAGE.Data
{
    // Shared cloud library of Project Dashboard report layouts, stored in dbo.VMS_ReportLayouts on the
    // central projectcontrols database. NOTE: projectcontrols is shared with REQit — this table is
    // additive and REQit-neutral (a brand-new table; no existing schema is touched). Publish pushes a
    // copy; Import pulls a copy down into the local list (no live link). Deletion is admin-only.
    public static class AzureReportLayoutRepository
    {
        public sealed class CloudLayout
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public DateTime UpdatedUtc { get; set; }
        }

        // Idempotent create — runs on first Publish/List. If the app account lacks DDL rights the
        // caller surfaces a friendly error and the table can be created manually with the same SQL.
        private const string EnsureSql = @"
IF OBJECT_ID('dbo.VMS_ReportLayouts','U') IS NULL
CREATE TABLE dbo.VMS_ReportLayouts (
    LayoutId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name          NVARCHAR(120)    NOT NULL,
    Author        NVARCHAR(120)    NOT NULL,
    SchemaVersion INT              NOT NULL,
    LayoutJson    NVARCHAR(MAX)    NOT NULL,
    CreatedUtc    DATETIME2        NOT NULL,
    UpdatedUtc    DATETIME2        NOT NULL
);";

        public static async Task EnsureTableAsync()
        {
            using var conn = AzureDbManager.GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = EnsureSql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Publish (upsert by Name + Author): re-publishing your own named layout updates that copy
        // rather than creating duplicates. Different authors can share the same layout name.
        public static async Task PublishAsync(string name, string author, int schemaVersion, string layoutJson)
        {
            await EnsureTableAsync();
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
            await EnsureTableAsync();
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
