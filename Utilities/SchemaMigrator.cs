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
        public const int CurrentSchemaVersion = 11;

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
                case 8:
                    Migration_v8_AddScheduleThreeWeekColumns(connection);
                    break;
                case 9:
                    Migration_v9_CleanupStaleDates(connection);
                    break;
                case 10:
                    Migration_v10_CreateLocalProgressSnapshots(connection);
                    break;
                case 11:
                    Migration_v11_TrimLocalProgressSnapshots(connection);
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

        // v8: Add ThreeWeekStart/ThreeWeekFinish columns to Schedule table for 3WLA date persistence
        private static void Migration_v8_AddScheduleThreeWeekColumns(SqliteConnection connection)
        {
            var scheduleCols = GetTableColumns(connection, "Schedule");

            if (!scheduleCols.Contains("ThreeWeekStart"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Schedule ADD COLUMN ThreeWeekStart TEXT";
                cmd.ExecuteNonQuery();
            }

            if (!scheduleCols.Contains("ThreeWeekFinish"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Schedule ADD COLUMN ThreeWeekFinish TEXT";
                cmd.ExecuteNonQuery();
            }
        }
        // v9: Clear stale ActStart/ActFin dates that don't align with PercentEntry
        // 0% should have no dates, <100% should have no ActFin
        private static void Migration_v9_CleanupStaleDates(SqliteConnection connection)
        {
            // Clear both dates for 0% records
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE Activities SET ActStart = '', ActFin = '', LocalDirty = 1
                    WHERE PercentEntry = 0
                    AND (ActStart IS NOT NULL AND ActStart != ''
                         OR ActFin IS NOT NULL AND ActFin != '')";
                int cleared = cmd.ExecuteNonQuery();
                if (cleared > 0)
                    AppLogger.Info($"Cleared dates on {cleared} records with 0% progress",
                        "SchemaMigrator.Migration_v9");
            }

            // Clear ActFin for >0% but <100% records
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE Activities SET ActFin = '', LocalDirty = 1
                    WHERE PercentEntry > 0 AND PercentEntry < 100
                    AND ActFin IS NOT NULL AND ActFin != ''";
                int cleared = cmd.ExecuteNonQuery();
                if (cleared > 0)
                    AppLogger.Info($"Cleared ActFin on {cleared} records with <100% progress",
                        "SchemaMigrator.Migration_v9");
            }
        }

        // v10: Create local ProgressSnapshots table for the Schedule module
        // Mirrors Azure VMS_ProgressSnapshots; holds only the current user's snapshot rows
        // for the week matching the imported P6 file. Wiped/refilled on P6 import.
        // The one-time backfill from Azure runs post-login from App.xaml.cs (after CurrentUser is set).
        private static void Migration_v10_CreateLocalProgressSnapshots(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ProgressSnapshots (
                    UniqueID              TEXT NOT NULL,
                    WeekEndDate           TEXT NOT NULL,
                    Area                  TEXT NOT NULL DEFAULT '',
                    AssignedTo            TEXT NOT NULL DEFAULT '',
                    AzureUploadUtcDate    TEXT,
                    Aux1                  TEXT NOT NULL DEFAULT '',
                    Aux2                  TEXT NOT NULL DEFAULT '',
                    Aux3                  TEXT NOT NULL DEFAULT '',
                    BaseUnit              REAL NOT NULL DEFAULT 0,
                    BudgetHoursGroup      REAL NOT NULL DEFAULT 0,
                    BudgetHoursROC        REAL NOT NULL DEFAULT 0,
                    BudgetMHs             REAL NOT NULL DEFAULT 0,
                    ChgOrdNO              TEXT NOT NULL DEFAULT '',
                    ClientBudget          REAL NOT NULL DEFAULT 0,
                    ClientCustom3         REAL NOT NULL DEFAULT 0,
                    ClientEquivQty        REAL NOT NULL DEFAULT 0,
                    CompType              TEXT NOT NULL DEFAULT '',
                    CreatedBy             TEXT NOT NULL DEFAULT '',
                    DateTrigger           INTEGER NOT NULL DEFAULT 0,
                    Description           TEXT NOT NULL DEFAULT '',
                    DwgNO                 TEXT NOT NULL DEFAULT '',
                    EarnQtyEntry          REAL NOT NULL DEFAULT 0,
                    EarnedMHsRoc          REAL NOT NULL DEFAULT 0,
                    EqmtNO                TEXT NOT NULL DEFAULT '',
                    EquivQTY              TEXT NOT NULL DEFAULT '',
                    EquivUOM              TEXT NOT NULL DEFAULT '',
                    Estimator             TEXT NOT NULL DEFAULT '',
                    HexNO                 INTEGER NOT NULL DEFAULT 0,
                    HtTrace               TEXT NOT NULL DEFAULT '',
                    InsulType             TEXT NOT NULL DEFAULT '',
                    LineNumber            TEXT NOT NULL DEFAULT '',
                    MtrlSpec              TEXT NOT NULL DEFAULT '',
                    Notes                 TEXT NOT NULL DEFAULT '',
                    PaintCode             TEXT NOT NULL DEFAULT '',
                    PercentEntry          REAL NOT NULL DEFAULT 0,
                    PhaseCategory         TEXT NOT NULL DEFAULT '',
                    PhaseCode             TEXT NOT NULL DEFAULT '',
                    PipeGrade             TEXT NOT NULL DEFAULT '',
                    PipeSize1             REAL NOT NULL DEFAULT 0,
                    PipeSize2             REAL NOT NULL DEFAULT 0,
                    PrevEarnMHs           REAL NOT NULL DEFAULT 0,
                    PrevEarnQTY           REAL NOT NULL DEFAULT 0,
                    ProgDate              TEXT,
                    ProjectID             TEXT NOT NULL DEFAULT '',
                    Quantity              REAL NOT NULL DEFAULT 0,
                    RevNO                 TEXT NOT NULL DEFAULT '',
                    RFINO                 TEXT NOT NULL DEFAULT '',
                    ROCBudgetQTY          REAL NOT NULL DEFAULT 0,
                    ROCID                 REAL NOT NULL DEFAULT 0,
                    ROCPercent            REAL NOT NULL DEFAULT 0,
                    ROCStep               TEXT NOT NULL DEFAULT '',
                    SchedActNO            TEXT NOT NULL DEFAULT '',
                    ActFin                TEXT,
                    ActStart              TEXT,
                    SecondActno           TEXT NOT NULL DEFAULT '',
                    SecondDwgNO           TEXT NOT NULL DEFAULT '',
                    Service               TEXT NOT NULL DEFAULT '',
                    ShopField             TEXT NOT NULL DEFAULT '',
                    ShtNO                 TEXT NOT NULL DEFAULT '',
                    SubArea               TEXT NOT NULL DEFAULT '',
                    PjtSystem             TEXT NOT NULL DEFAULT '',
                    PjtSystemNo           TEXT NOT NULL DEFAULT '',
                    TagNO                 TEXT NOT NULL DEFAULT '',
                    UDF1                  TEXT NOT NULL DEFAULT '',
                    UDF2                  TEXT NOT NULL DEFAULT '',
                    UDF3                  TEXT NOT NULL DEFAULT '',
                    UDF4                  TEXT NOT NULL DEFAULT '',
                    UDF5                  TEXT NOT NULL DEFAULT '',
                    UDF6                  TEXT NOT NULL DEFAULT '',
                    UDF7                  TEXT NOT NULL DEFAULT '',
                    UDF8                  TEXT NOT NULL DEFAULT '',
                    UDF9                  TEXT NOT NULL DEFAULT '',
                    UDF10                 TEXT NOT NULL DEFAULT '',
                    UDF11                 TEXT NOT NULL DEFAULT '',
                    UDF12                 TEXT NOT NULL DEFAULT '',
                    UDF13                 TEXT NOT NULL DEFAULT '',
                    UDF14                 TEXT NOT NULL DEFAULT '',
                    UDF15                 TEXT NOT NULL DEFAULT '',
                    UDF16                 TEXT NOT NULL DEFAULT '',
                    UDF17                 TEXT NOT NULL DEFAULT '',
                    RespParty             TEXT NOT NULL DEFAULT '',
                    UDF20                 TEXT NOT NULL DEFAULT '',
                    UpdatedBy             TEXT NOT NULL DEFAULT '',
                    UpdatedUtcDate        TEXT,
                    UOM                   TEXT NOT NULL DEFAULT '',
                    WorkPackage           TEXT NOT NULL DEFAULT '',
                    XRay                  REAL NOT NULL DEFAULT 0,
                    ExportedBy            TEXT,
                    ExportedDate          TEXT,
                    PRIMARY KEY (UniqueID, WeekEndDate)
                );
                CREATE INDEX IF NOT EXISTS idx_progsnap_week_proj ON ProgressSnapshots(WeekEndDate, ProjectID, AssignedTo);
                CREATE INDEX IF NOT EXISTS idx_progsnap_schedactno ON ProgressSnapshots(SchedActNO);
            ";
            cmd.ExecuteNonQuery();
            AppLogger.Info("Created local ProgressSnapshots table and indexes", "SchemaMigrator.Migration_v10");
        }

        // v11: Trim local ProgressSnapshots from 89 columns to the 12 the Schedule module
        // actually reads. Massive perf win on the refill from Azure (~7x less data shipped).
        // Drops the table and recreates with the trimmed schema; the next P6 import or
        // post-login backfill will repopulate from Azure with only the 12 needed columns.
        // Azure VMS_ProgressSnapshots is unchanged and still has all 89 columns — the revert
        // flow in ManageSnapshotsDialog reads from Azure and continues to work.
        private static void Migration_v11_TrimLocalProgressSnapshots(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DROP TABLE IF EXISTS ProgressSnapshots;
                CREATE TABLE ProgressSnapshots (
                    UniqueID              TEXT NOT NULL,
                    WeekEndDate           TEXT NOT NULL,
                    SchedActNO            TEXT NOT NULL DEFAULT '',
                    Description           TEXT NOT NULL DEFAULT '',
                    PercentEntry          REAL NOT NULL DEFAULT 0,
                    BudgetMHs             REAL NOT NULL DEFAULT 0,
                    ActStart              TEXT,
                    ActFin                TEXT,
                    AssignedTo            TEXT NOT NULL DEFAULT '',
                    ProjectID             TEXT NOT NULL DEFAULT '',
                    UpdatedBy             TEXT NOT NULL DEFAULT '',
                    UpdatedUtcDate        TEXT,
                    PRIMARY KEY (UniqueID, WeekEndDate)
                );
                CREATE INDEX IF NOT EXISTS idx_progsnap_week_proj ON ProgressSnapshots(WeekEndDate, ProjectID, AssignedTo);
                CREATE INDEX IF NOT EXISTS idx_progsnap_schedactno ON ProgressSnapshots(SchedActNO);
            ";
            cmd.ExecuteNonQuery();
            AppLogger.Info("Trimmed local ProgressSnapshots to 12 columns", "SchemaMigrator.Migration_v11");
        }
    }
}
