using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    // Exception thrown when a schema migration fails
    public class MigrationException : Exception
    {
        public int FailedVersion { get; }

        public MigrationException(int version, Exception inner)
            : base($"Schema migration to v{version} failed", inner)
        {
            FailedVersion = version;
        }
    }

    // Manages database schema migrations - versioned, sequential, idempotent
    public static class SchemaMigrator
    {
        private const string SchemaVersionKey = "SchemaVersion";

        // Increment this when adding new migrations
        public const int CurrentSchemaVersion = 7;

        // Runs all pending migrations sequentially
        // progressCallback is invoked with status messages for UI updates
        public static void RunMigrations(SqliteConnection connection, Action<string>? progressCallback = null)
        {
            int currentVersion = GetSchemaVersion(connection);

            // Check if schema is newer than app (user rolled back to older version)
            if (currentVersion > CurrentSchemaVersion)
            {
                AppLogger.Warning(
                    $"Database schema (v{currentVersion}) is newer than app expects (v{CurrentSchemaVersion}). " +
                    "This may cause issues if the database has incompatible changes.",
                    "SchemaMigrator.RunMigrations");
                return;
            }

            if (currentVersion >= CurrentSchemaVersion)
            {
                AppLogger.Info($"Schema is current (v{currentVersion})", "SchemaMigrator.RunMigrations");
                return;
            }

            AppLogger.Info($"Schema upgrade needed: v{currentVersion} -> v{CurrentSchemaVersion}", "SchemaMigrator.RunMigrations");

            // Run migrations sequentially
            while (currentVersion < CurrentSchemaVersion)
            {
                int targetVersion = currentVersion + 1;
                progressCallback?.Invoke($"Upgrading database schema (v{targetVersion})...");

                try
                {
                    RunMigration(connection, targetVersion);
                    SetSchemaVersion(connection, targetVersion);
                    currentVersion = targetVersion;
                    AppLogger.Info($"Migration v{targetVersion} completed successfully", "SchemaMigrator.RunMigrations");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"SchemaMigrator.Migration_v{targetVersion}");
                    throw new MigrationException(targetVersion, ex);
                }
            }
        }

        // Dispatches to the appropriate migration method
        private static void RunMigration(SqliteConnection connection, int version)
        {
            switch (version)
            {
                case 1:
                    Migration_v1_ThreeWeekLookaheadColumns(connection);
                    break;
                case 2:
                    Migration_v2_RenameActivityDateColumns(connection);
                    break;
                case 3:
                    Migration_v3_UpdateColumnMappingsData(connection);
                    break;
                case 4:
                    Migration_v4_RemoveSystemNOColumn(connection);
                    break;
                case 5:
                    Migration_v5_AddPlanDateColumns(connection);
                    break;
                case 6:
                    Migration_v6_AddFeedbackNotesColumn(connection);
                    break;
                case 7:
                    Migration_v7_AddScheduleUDFColumns(connection);
                    break;
                default:
                    throw new ArgumentException($"Unknown migration version: {version}");
            }
        }

        // Get current schema version from AppSettings (0 if not set)
        private static int GetSchemaVersion(SqliteConnection connection)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT SettingValue FROM AppSettings WHERE SettingName = @name";
                cmd.Parameters.AddWithValue("@name", SchemaVersionKey);

                var result = cmd.ExecuteScalar();
                if (result != null && int.TryParse(result.ToString(), out int version))
                {
                    return version;
                }
            }
            catch
            {
                // Table might not exist yet or other error - treat as version 0
            }

            return 0;
        }

        // Set schema version in AppSettings
        private static void SetSchemaVersion(SqliteConnection connection, int version)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AppSettings (SettingName, SettingValue, DataType)
                VALUES (@name, @value, 'int')
                ON CONFLICT(SettingName)
                DO UPDATE SET SettingValue = @value, DataType = 'int'";
            cmd.Parameters.AddWithValue("@name", SchemaVersionKey);
            cmd.Parameters.AddWithValue("@value", version.ToString());
            cmd.ExecuteNonQuery();
        }

        // Helper to get existing columns for a table
        private static HashSet<string> GetTableColumns(SqliteConnection connection, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1)); // Column name is at index 1
            }
            return columns;
        }

        // ============================================================================
        // MIGRATIONS
        // When adding a new migration:
        // 1. Increment CurrentSchemaVersion
        // 2. Add case to RunMigration switch
        // 3. Implement Migration_vN_Description method
        // 4. Ensure migration is idempotent (checks state before making changes)
        // ============================================================================

        // v1: Add columns to ThreeWeekLookahead table
        private static void Migration_v1_ThreeWeekLookaheadColumns(SqliteConnection connection)
        {
            var existingColumns = GetTableColumns(connection, "ThreeWeekLookahead");

            var newColumns = new (string Name, string Type)[]
            {
                ("MissedStartReason", "TEXT"),
                ("MissedFinishReason", "TEXT"),
                ("P6_Start", "TEXT"),
                ("P6_Finish", "TEXT"),
                ("MS_Start", "TEXT"),
                ("MS_Finish", "TEXT")
            };

            foreach (var (columnName, columnType) in newColumns)
            {
                if (!existingColumns.Contains(columnName))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE ThreeWeekLookahead ADD COLUMN {columnName} {columnType}";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // v2: Rename SchStart/SchFinish to ActStart/ActFin in Activities table
        private static void Migration_v2_RenameActivityDateColumns(SqliteConnection connection)
        {
            var existingColumns = GetTableColumns(connection, "Activities");

            // Rename SchStart -> ActStart if old column exists and new doesn't
            if (existingColumns.Contains("SchStart") && !existingColumns.Contains("ActStart"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Activities RENAME COLUMN SchStart TO ActStart";
                cmd.ExecuteNonQuery();
            }

            // Rename SchFinish -> ActFin if old column exists and new doesn't
            if (existingColumns.Contains("SchFinish") && !existingColumns.Contains("ActFin"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Activities RENAME COLUMN SchFinish TO ActFin";
                cmd.ExecuteNonQuery();
            }
        }

        // v3: Update ColumnMappings data to use new property names
        private static void Migration_v3_UpdateColumnMappingsData(SqliteConnection connection)
        {
            // Update Sch_Start mapping to point to ActStart instead of SchStart
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE ColumnMappings
                    SET ColumnName = 'ActStart'
                    WHERE OldVantageName = 'Sch_Start' AND ColumnName = 'SchStart'";
                cmd.ExecuteNonQuery();
            }

            // Update Sch_Finish mapping to point to ActFin instead of SchFinish
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE ColumnMappings
                    SET ColumnName = 'ActFin'
                    WHERE OldVantageName = 'Sch_Finish' AND ColumnName = 'SchFinish'";
                cmd.ExecuteNonQuery();
            }
        }
        // v4: Remove orphaned SystemNO column, keep PjtSystemNo as canonical field
        private static void Migration_v4_RemoveSystemNOColumn(SqliteConnection connection)
        {
            var activitiesCols = GetTableColumns(connection, "Activities");

            // Copy SystemNO data into PjtSystemNo where PjtSystemNo is empty
            if (activitiesCols.Contains("SystemNO") && activitiesCols.Contains("PjtSystemNo"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Activities SET PjtSystemNo = SystemNO
                    WHERE (PjtSystemNo IS NULL OR PjtSystemNo = '')
                    AND SystemNO IS NOT NULL AND SystemNO != ''";
                cmd.ExecuteNonQuery();
            }

            // Drop SystemNO from Activities
            if (activitiesCols.Contains("SystemNO"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Activities DROP COLUMN SystemNO";
                cmd.ExecuteNonQuery();
            }

            // Same for VMS_ProgressSnapshots if it exists
            var snapshotCols = GetTableColumns(connection, "VMS_ProgressSnapshots");
            if (snapshotCols.Contains("SystemNO") && snapshotCols.Contains("PjtSystemNo"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE VMS_ProgressSnapshots SET PjtSystemNo = SystemNO
                    WHERE (PjtSystemNo IS NULL OR PjtSystemNo = '')
                    AND SystemNO IS NOT NULL AND SystemNO != ''";
                cmd.ExecuteNonQuery();
            }

            if (snapshotCols.Contains("SystemNO"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE VMS_ProgressSnapshots DROP COLUMN SystemNO";
                cmd.ExecuteNonQuery();
            }
        }

        // v5: Add PlanStart and PlanFin columns to Activities table
        private static void Migration_v5_AddPlanDateColumns(SqliteConnection connection)
        {
            var activitiesCols = GetTableColumns(connection, "Activities");

            if (!activitiesCols.Contains("PlanStart"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Activities ADD COLUMN PlanStart TEXT";
                cmd.ExecuteNonQuery();
            }

            if (!activitiesCols.Contains("PlanFin"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Activities ADD COLUMN PlanFin TEXT";
                cmd.ExecuteNonQuery();
            }
        }

        // v6: Add Notes column to Feedback table for admin notes
        private static void Migration_v6_AddFeedbackNotesColumn(SqliteConnection connection)
        {
            var feedbackCols = GetTableColumns(connection, "Feedback");

            if (!feedbackCols.Contains("Notes"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Feedback ADD COLUMN Notes TEXT";
                cmd.ExecuteNonQuery();
            }
        }

        // v7: Add SchedUDF1-5 columns to Schedule table for custom P6 UDF mapping
        private static void Migration_v7_AddScheduleUDFColumns(SqliteConnection connection)
        {
            var scheduleCols = GetTableColumns(connection, "Schedule");

            for (int i = 1; i <= 5; i++)
            {
                string colName = $"SchedUDF{i}";
                if (!scheduleCols.Contains(colName))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE Schedule ADD COLUMN {colName} TEXT NOT NULL DEFAULT ''";
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
