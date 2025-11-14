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
        public static string DbPath { get; private set; }
        // Add new user to Central database
        public static void AddUserToCentral(string centralDbPath, int userId, string username, string fullName, string email, string phoneNumber, bool isAdmin, string adminToken)
        {
            try
            {
                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                centralConn.Open();

                var cmd = centralConn.CreateCommand();
                cmd.CommandText = @"
            INSERT INTO Users (UserID, Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken)
            VALUES (@userId, @username, @fullName, @email, @phone, @isAdmin, @token)
            ON CONFLICT(UserID) DO UPDATE SET
                Username = @username,
                FullName = @fullName,
                Email = @email,
                PhoneNumber = @phone,
                IsAdmin = @isAdmin,
                AdminToken = @token";

                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@fullName", fullName ?? "");
                cmd.Parameters.AddWithValue("@email", email ?? "");
                cmd.Parameters.AddWithValue("@phone", phoneNumber ?? "");
                cmd.Parameters.AddWithValue("@isAdmin", isAdmin ? 1 : 0);
                cmd.Parameters.AddWithValue("@token", adminToken ?? "");

                cmd.ExecuteNonQuery();

                VANTAGE.Utilities.AppLogger.Info($"Added/updated user {username} in Central database", "DatabaseSetup.AddUserToCentral");
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DatabaseSetup.AddUserToCentral");
                throw;
            }
        }
        // Mirror reference tables from Central database to Local database
        public static void MirrorTablesFromCentral(string centralDbPath)
        {
            try
            {
                using var centralConn = new SqliteConnection($"Data Source={centralDbPath}");
                using var localConn = GetConnection();

                centralConn.Open();
                localConn.Open();

                // Tables to mirror schema + data (metadata)
                string[] metadataTables = { "Users", "Projects", "ColumnMappings", "Managers" };

                // Activities and Deleted_Activities are created manually with local schema

                // Mirror metadata tables (schema + data)
                foreach (string tableName in metadataTables)
                {
                    CopyTableSchema(centralConn, localConn, tableName);
                    CopyTableData(centralConn, localConn, tableName);
                }

                VANTAGE.Utilities.AppLogger.Info($"Mirrored tables from Central database", "DatabaseSetup.MirrorTablesFromCentral");
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DatabaseSetup.MirrorTablesFromCentral");
                throw;
            }
        }
        private static void EnsureDeletedActivitiesTable()
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                // Check if Activities table exists first
                using (var chkAct = connection.CreateCommand())
                {
                    chkAct.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='Activities';";
                    var activitiesExists = chkAct.ExecuteScalar() != null;
                    if (!activitiesExists)
                    {
                        VANTAGE.Utilities.AppLogger.Warning("Activities table not found; skipping Deleted_Activities creation.", "DB.EnsureDeletedActivitiesTable");
                        return;
                    }
                }

                VANTAGE.Utilities.AppLogger.Info("Ensuring Deleted_Activities…", "DB.EnsureDeletedActivitiesTable");

                // Create the table if it doesn't exist (mirror columns only; no rows)
                using (var create = connection.CreateCommand())
                {
                    create.CommandText = "CREATE TABLE IF NOT EXISTS Deleted_Activities AS SELECT * FROM Activities WHERE 0;";
                    create.ExecuteNonQuery();
                }

                // Ensure audit columns exist (idempotent)
                EnsureColumn(connection, "Deleted_Activities", "DeletedDate", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "Deleted_Activities", "DeletedBy", "TEXT NULL");

                // Helpful indexes
                using (var idx = connection.CreateCommand())
                {
                    idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Deleted_Activities_DeletedDate ON Deleted_Activities(DeletedDate);";
                    idx.ExecuteNonQuery();
                }
                using (var uidx = connection.CreateCommand())
                {
                    uidx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Deleted_Activities_UniqueID ON Deleted_Activities(UniqueID);";
                    uidx.ExecuteNonQuery();
                }

                VANTAGE.Utilities.AppLogger.Info("Deleted_Activities ensured.", "DB.EnsureDeletedActivitiesTable");
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DB.EnsureDeletedActivitiesTable");
            }

            static void EnsureColumn(Microsoft.Data.Sqlite.SqliteConnection conn, string table, string name, string def)
            {
                using var info = conn.CreateCommand();
                info.CommandText = $"PRAGMA table_info('{table}');";
                using var rd = info.ExecuteReader();
                bool has = false;
                while (rd.Read())
                    if (string.Equals(rd["name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
                if (!has)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {name} {def};";
                    alter.ExecuteNonQuery();
                }
            }
        }
        // Copy table schema from Central to Local
        private static void CopyTableSchema(SqliteConnection centralConn, SqliteConnection localConn, string tableName)
        {
            // Get the CREATE TABLE statement from Central
            var schemaCmd = centralConn.CreateCommand();
            schemaCmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=@tableName";
            schemaCmd.Parameters.AddWithValue("@tableName", tableName);

            string createTableSql = schemaCmd.ExecuteScalar()?.ToString();

            if (string.IsNullOrEmpty(createTableSql))
            {
                throw new Exception($"Table {tableName} not found in Central database");
            }

            // Replace CREATE TABLE with CREATE TABLE IF NOT EXISTS
            createTableSql = createTableSql.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");

            // Execute on local database
            var createCmd = localConn.CreateCommand();
            createCmd.CommandText = createTableSql;
            createCmd.ExecuteNonQuery();
        }

        // Copy table data from Central to Local
        private static void CopyTableData(SqliteConnection centralConn, SqliteConnection localConn, string tableName)
        {
            // Clear existing data in local table
            var deleteCmd = localConn.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM {tableName}";
            deleteCmd.ExecuteNonQuery();

            // Get all data from Central
            var selectCmd = centralConn.CreateCommand();
            selectCmd.CommandText = $"SELECT * FROM {tableName}";

            using var reader = selectCmd.ExecuteReader();

            // Build column list
            var columnNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            string columns = string.Join(", ", columnNames);
            string parameters = string.Join(", ", columnNames.Select(c => "@" + c));
            string insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

            // Copy each row
            while (reader.Read())
            {
                var insertCmd = localConn.CreateCommand();
                insertCmd.CommandText = insertSql;

                for (int i = 0; i < columnNames.Count; i++)
                {
                    insertCmd.Parameters.AddWithValue("@" + columnNames[i], reader[i] ?? DBNull.Value);
                }

                insertCmd.ExecuteNonQuery();
            }
        }
        // Validate that the selected file is a valid Central database
        public static bool ValidateCentralDatabase(string centralDbPath, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if file exists
            if (!File.Exists(centralDbPath))
            {
                errorMessage = "The selected file does not exist.";
                return false;
            }

            try
            {
                // Try to open as SQLite database
                using var connection = new SqliteConnection($"Data Source={centralDbPath}");
                connection.Open();

                // Check for required tables
                string[] requiredTables = { "Users", "Projects", "ColumnMappings", "Managers" };

                foreach (string tableName in requiredTables)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName";
                    command.Parameters.AddWithValue("@tableName", tableName);

                    var result = command.ExecuteScalar();
                    if (result == null)
                    {
                        errorMessage = $"Central database is missing required table: {tableName}";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to validate Central database: {ex.Message}";
                return false;
            }
        }
        public static void InitializeDatabase()
        {
            try
            {
                // Step 1: Determine database path
                DbPath = GetOrSetDatabasePath();

                // Step 2: Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(DbPath);
                if (!Directory.Exists(directory))
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
                LineNO                TEXT NOT NULL DEFAULT '',
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
        ";

                command.ExecuteNonQuery();

                // Create Deleted_Activities mirroring Activities structure
                EnsureDeletedActivitiesTable();
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

        private static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                string testFile = Path.Combine(dirPath, "test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string PromptUserForDatabaseLocation()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Choose Database Location",
                FileName = "VANTAGE_Local.db",
                DefaultExt = ".db",
                Filter = "Database files (*.db)|*.db"
            };

            if (dialog.ShowDialog() == true)
            {
                string chosenPath = dialog.FileName;
                SettingsManager.SetAppSetting("DatabasePath", chosenPath);
                return chosenPath;
            }

            // User cancelled - use fallback in current directory
            string fallback = Path.Combine(Directory.GetCurrentDirectory(), "VANTAGE", "VANTAGE_Local.db");
            SettingsManager.SetAppSetting("DatabasePath", fallback);
            return fallback;
        }
    }
}