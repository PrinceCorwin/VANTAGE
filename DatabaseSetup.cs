using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using VANTAGE.Utilities;

namespace VANTAGE
{
    public class DatabaseSetup
    {
        public static string DbPath { get; private set; } = null!;

        public static void MirrorTablesFromAzure()
        {
            try
            {
                using var azureConn = AzureDbManager.GetConnection();
                using var localConn = GetConnection();
                azureConn.Open();
                localConn.Open();

                // Tables to mirror from Azure
                string[] metadataTables = { "Users", "Projects", "ColumnMappings", "Managers" };

                foreach (string tableName in metadataTables)
                {
                    CopyTableDataFromAzure(azureConn, localConn, tableName);
                }

                AppLogger.Info("Mirrored tables from Azure database", "DatabaseSetup.MirrorTablesFromAzure");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseSetup.MirrorTablesFromAzure");
                throw;
            }
        }

        // Copy table data from Azure SQL to Local SQLite
        private static void CopyTableDataFromAzure(SqlConnection azureConn, SqliteConnection localConn, string tableName)
        {
            // Clear existing data in local table
            var deleteCmd = localConn.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM {tableName}";
            deleteCmd.ExecuteNonQuery();

            // Get all data from Azure
            var selectCmd = azureConn.CreateCommand();
            selectCmd.CommandText = $"SELECT * FROM {tableName}";

            using var reader = selectCmd.ExecuteReader();

            // Build column list from the reader
            var columnNames = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            string columns = string.Join(", ", columnNames);
            string parameters = string.Join(", ", columnNames.Select(c => "@" + c));
            string insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

            // Copy each row
            int rowCount = 0;
            while (reader.Read())
            {
                var insertCmd = localConn.CreateCommand();
                insertCmd.CommandText = insertSql;

                for (int i = 0; i < columnNames.Count; i++)
                {
                    var value = reader[i];
                    // Convert boolean to int for SQLite compatibility
                    if (value is bool boolValue)
                    {
                        value = boolValue ? 1 : 0;
                        AppLogger.Info($"Converted bool {boolValue} to {value} for column {columnNames[i]}", "CopyTableDataFromAzure");
                    }
                    insertCmd.Parameters.AddWithValue("@" + columnNames[i], value ?? DBNull.Value);
                }

                insertCmd.ExecuteNonQuery();
                rowCount++;
            }

            AppLogger.Info($"Copied {rowCount} rows to {tableName}", "DatabaseSetup.CopyTableDataFromAzure");
        }
        public static void InitializeDatabase()
        {
            try
            {
                // Step 1: Determine database path
                DbPath = GetOrSetDatabasePath();

                // Step 2: Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(DbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Step 3: Create/open database
                using var connection = new SqliteConnection($"Data Source={DbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            -- AppSettings table (local only)
            CREATE TABLE IF NOT EXISTS AppSettings (
                SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                SettingName TEXT UNIQUE NOT NULL,
                SettingValue TEXT,
                DataType TEXT
            );

            -- UserSettings table (local only)
            CREATE TABLE IF NOT EXISTS UserSettings (
                UserSettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                UserID INTEGER NOT NULL,
                SettingName TEXT NOT NULL,
                SettingValue TEXT,
                DataType TEXT,
                LastModified DATETIME DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(UserID, SettingName)
            );
            -- Schedule table (local only - P6 import data)
            CREATE TABLE IF NOT EXISTS Schedule (
                SchedActNO            TEXT NOT NULL,
                WeekEndDate           TEXT NOT NULL,
                ProjectID             TEXT NOT NULL DEFAULT '',
                WbsId                 TEXT NOT NULL DEFAULT '',
                Description           TEXT NOT NULL DEFAULT '',
                P6_PlannedStart       TEXT,
                P6_PlannedFinish      TEXT,
                P6_ActualStart        TEXT,
                P6_ActualFinish       TEXT,
                P6_PercentComplete    REAL NOT NULL DEFAULT 0,
                P6_BudgetMHs          REAL NOT NULL DEFAULT 0,
                ThreeWeekStart        TEXT,
                ThreeWeekFinish       TEXT,
                MissedStartReason     TEXT,
                MissedFinishReason    TEXT,
                UpdatedBy             TEXT NOT NULL DEFAULT '',
                UpdatedUtcDate        TEXT NOT NULL,
                PRIMARY KEY (SchedActNO, WeekEndDate)
            );

            -- Users table (mirrored from Azure)
            CREATE TABLE IF NOT EXISTS Users (
                UserID INTEGER PRIMARY KEY,
                Username TEXT NOT NULL,
                FullName TEXT,
                Email TEXT

            );

            -- Projects table (mirrored from Azure)
            CREATE TABLE IF NOT EXISTS Projects (
                ProjectID TEXT PRIMARY KEY NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                ClientName TEXT NOT NULL DEFAULT '',
                ClientStreetAddress TEXT NOT NULL DEFAULT '',
                ClientCity TEXT NOT NULL DEFAULT '',
                ClientState TEXT NOT NULL DEFAULT '',
                ClientZipCode TEXT NOT NULL DEFAULT '',
                ProjectStreetAddress TEXT NOT NULL DEFAULT '',
                ProjectCity TEXT NOT NULL DEFAULT '',
                ProjectState TEXT NOT NULL DEFAULT '',
                ProjectZipCode TEXT NOT NULL DEFAULT '',
                ProjectManager TEXT NOT NULL DEFAULT '',
                SiteManager TEXT NOT NULL DEFAULT '',
                OM INTEGER NOT NULL DEFAULT 0,
                SM INTEGER NOT NULL DEFAULT 0,
                EN INTEGER NOT NULL DEFAULT 0,
                PM INTEGER NOT NULL DEFAULT 0
            );

            -- ColumnMappings table (mirrored from Azure)
            CREATE TABLE IF NOT EXISTS ColumnMappings (
                MappingID INTEGER PRIMARY KEY,
                ColumnName TEXT NOT NULL,
                OldVantageName TEXT,
                AzureName TEXT,
                DataType TEXT,
                IsEditable INTEGER DEFAULT 0,
                IsCalculated INTEGER DEFAULT 0,
                CalcFormula TEXT,
                Notes TEXT
            );

            -- Managers table (mirrored from Azure)
            CREATE TABLE IF NOT EXISTS Managers (
                ManagerID INTEGER PRIMARY KEY,
                FullName TEXT NOT NULL DEFAULT '',
                Position TEXT NOT NULL DEFAULT '',
                Company TEXT NOT NULL DEFAULT '',
                Email TEXT NOT NULL DEFAULT '',
                ProjectsAssigned TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Activities table (local schema with UniqueID as primary key)
            CREATE TABLE IF NOT EXISTS Activities (
                UniqueID              TEXT PRIMARY KEY NOT NULL,
                ActivityID            INTEGER NOT NULL DEFAULT 0,
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
                LocalDirty            INTEGER NOT NULL DEFAULT 0,
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
                SchFinish             TEXT,
                SchStart              TEXT,
                SecondActno           TEXT NOT NULL DEFAULT '',
                SecondDwgNO           TEXT NOT NULL DEFAULT '',
                Service               TEXT NOT NULL DEFAULT '',
                ShopField             TEXT NOT NULL DEFAULT '',
                ShtNO                 TEXT NOT NULL DEFAULT '',
                SubArea               TEXT NOT NULL DEFAULT '',
                PjtSystem             TEXT NOT NULL DEFAULT '',
                SystemNO              TEXT NOT NULL DEFAULT '',
                TagNO                 TEXT NOT NULL DEFAULT '',
                UDF1                  TEXT NOT NULL DEFAULT '',
                UDF10                 TEXT NOT NULL DEFAULT '',
                UDF11                 TEXT NOT NULL DEFAULT '',
                UDF12                 TEXT NOT NULL DEFAULT '',
                UDF13                 TEXT NOT NULL DEFAULT '',
                UDF14                 TEXT NOT NULL DEFAULT '',
                UDF15                 TEXT NOT NULL DEFAULT '',
                UDF16                 TEXT NOT NULL DEFAULT '',
                UDF17                 TEXT NOT NULL DEFAULT '',
                UDF18                 TEXT NOT NULL DEFAULT '',
                UDF2                  TEXT NOT NULL DEFAULT '',
                UDF20                 TEXT NOT NULL DEFAULT '',
                UDF3                  TEXT NOT NULL DEFAULT '',
                UDF4                  TEXT NOT NULL DEFAULT '',
                UDF5                  TEXT NOT NULL DEFAULT '',
                UDF6                  TEXT NOT NULL DEFAULT '',
                UDF7                  TEXT NOT NULL DEFAULT '',
                UDF8                  TEXT NOT NULL DEFAULT '',
                UDF9                  TEXT NOT NULL DEFAULT '',
                UpdatedBy             TEXT NOT NULL DEFAULT '',
                UpdatedUtcDate        TEXT NOT NULL,
                UOM                   TEXT NOT NULL DEFAULT '',
                WeekEndDate           TEXT,
                WorkPackage           TEXT NOT NULL DEFAULT '',
                XRay                  REAL NOT NULL DEFAULT 0,
                SyncVersion           INTEGER NOT NULL DEFAULT 0
            );

            -- Indexes for performance
            CREATE INDEX IF NOT EXISTS idx_project ON Activities(ProjectID);
            CREATE INDEX IF NOT EXISTS idx_area ON Activities(Area);
            CREATE INDEX IF NOT EXISTS idx_assigned_to ON Activities(AssignedTo);
            CREATE INDEX IF NOT EXISTS idx_unique_id ON Activities(UniqueID);
            CREATE INDEX IF NOT EXISTS idx_roc_id ON Activities(ROCID);
            CREATE INDEX IF NOT EXISTS idx_column_name ON ColumnMappings(ColumnName);
            CREATE INDEX IF NOT EXISTS idx_schedule_weekenddate ON Schedule(WeekEndDate);
            CREATE INDEX IF NOT EXISTS idx_schedule_projectid ON Schedule(ProjectID);
        ";

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DatabaseSetup.InitializeDatabase");
                throw;
            }
        }

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={DbPath}");
        }

        private static string GetOrSetDatabasePath()
        {
            // ALWAYS use LocalApplicationData - ONE LOCATION ONLY
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VANTAGE",
                "VANTAGE_Local.db"
            );

            DbPath = defaultPath;
            return defaultPath;
        }
    }
}