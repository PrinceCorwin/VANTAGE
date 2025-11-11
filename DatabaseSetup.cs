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
                    -- Users table
                    CREATE TABLE IF NOT EXISTS Users (
                        UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT UNIQUE NOT NULL,
                        FullName TEXT,
                        Email TEXT,
                        PhoneNumber TEXT,
                        IsAdmin INTEGER DEFAULT 0,
                        AdminToken TEXT
                    );

                    -- AppSettings table
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingName TEXT UNIQUE NOT NULL,
                        SettingValue TEXT,
                        DataType TEXT
                    );

                    -- UserSettings table
                    CREATE TABLE IF NOT EXISTS UserSettings (
                        UserSettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserID INTEGER NOT NULL,
                        SettingName TEXT NOT NULL,
                        SettingValue TEXT,
                        DataType TEXT,
                        LastModified DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (UserID) REFERENCES Users(UserID),
                        UNIQUE(UserID, SettingName)
                    );

                    -- Activities table (NEW: NewVantage column names)
                    CREATE TABLE IF NOT EXISTS Activities (
                        ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Area TEXT DEFAULT '',
                        AssignedTo TEXT DEFAULT 'Unassigned',
                        AzureUploadUtcDate TEXT,
                        Aux1 TEXT DEFAULT '',
                        Aux2 TEXT DEFAULT '',
                        Aux3 TEXT DEFAULT '',
                        BaseUnit REAL DEFAULT 0,
                        BudgetMHs REAL DEFAULT 0,
                        BudgetHoursGroup REAL DEFAULT 0,
                        BudgetHoursROC REAL DEFAULT 0,
                        ChgOrdNO TEXT DEFAULT '',
                        ClientBudget REAL DEFAULT 0,
                        ClientCustom3 REAL DEFAULT 0,
                        ClientEquivQty REAL DEFAULT 0,
                        CompType TEXT DEFAULT '',
                        CreatedBy TEXT DEFAULT '',
                        DateTrigger INTEGER DEFAULT 0,
                        Description TEXT DEFAULT '',
                        DwgNO TEXT DEFAULT '',
                        EarnedMHsRoc REAL DEFAULT 0,
                        EarnQtyEntry REAL DEFAULT 0,
                        EqmtNO TEXT DEFAULT '',
                        EquivQTY TEXT DEFAULT '',
                        EquivUOM TEXT DEFAULT '',
                        Estimator TEXT DEFAULT '',
                        HexNO INTEGER DEFAULT 0,
                        HtTrace TEXT DEFAULT '',
                        InsulType TEXT DEFAULT '',
                        LineNO TEXT DEFAULT '',
                        LocalDirty INTEGER DEFAULT 1,
                        MtrlSpec TEXT DEFAULT '',
                        Notes TEXT DEFAULT '',
                        PaintCode TEXT DEFAULT '',
                        PercentEntry REAL DEFAULT 0,
                        PhaseCategory TEXT DEFAULT '',
                        PhaseCode TEXT DEFAULT '',
                        PipeGrade TEXT DEFAULT '',
                        PipeSize1 REAL DEFAULT 0,
                        PipeSize2 REAL DEFAULT 0,
                        PrevEarnMHs REAL DEFAULT 0,
                        PrevEarnQTY REAL DEFAULT 0,
                        PjtSystem TEXT DEFAULT '',
                        ProgDate TEXT,
                        ProjectID TEXT DEFAULT '',
                        Quantity REAL DEFAULT 0,
                        RevNO TEXT DEFAULT '',
                        RFINO TEXT DEFAULT '',
                        ROCBudgetQTY REAL DEFAULT 0,
                        ROCID REAL DEFAULT 0,
                        ROCPercent REAL DEFAULT 0,
                        ROCStep TEXT DEFAULT '',
                        SchedActNO TEXT DEFAULT '',
                        SchFinish TEXT DEFAULT '',
                        SchStart TEXT DEFAULT '',
                        SecondActno TEXT DEFAULT '',
                        SecondDwgNO TEXT DEFAULT '',
                        Service TEXT DEFAULT '',
                        ShopField TEXT DEFAULT '',
                        ShtNO TEXT DEFAULT '',
                        SubArea TEXT DEFAULT '',
                        SystemNO TEXT DEFAULT '',
                        TagNO TEXT DEFAULT '',
                        UDF1 TEXT DEFAULT '',
                        UDF10 TEXT DEFAULT '',
                        UDF11 TEXT DEFAULT '',
                        UDF12 TEXT DEFAULT '',
                        UDF13 TEXT DEFAULT '',
                        UDF14 TEXT DEFAULT '',
                        UDF15 TEXT DEFAULT '',
                        UDF16 TEXT DEFAULT '',
                        UDF17 TEXT DEFAULT '',
                        UDF18 TEXT DEFAULT '',
                        UDF2 TEXT DEFAULT '',
                        UDF20 TEXT DEFAULT '',
                        UDF3 TEXT DEFAULT '',
                        UDF4 TEXT DEFAULT '',
                        UDF5 TEXT DEFAULT '',
                        UDF6 TEXT DEFAULT '',
                        UDF7 TEXT DEFAULT '',
                        UDF8 TEXT DEFAULT '',
                        UDF9 TEXT DEFAULT '',
                        UniqueID TEXT UNIQUE NOT NULL,
                        UOM TEXT DEFAULT '',
                        UpdatedBy TEXT DEFAULT '',
                        UpdatedUtcDate TEXT,
                        WeekEndDate TEXT,
                        WorkPackage TEXT DEFAULT '',
                        XRay REAL DEFAULT 0
                    );

                    -- ColumnMappings table (Master mappings for all external systems)
                    CREATE TABLE IF NOT EXISTS ColumnMappings (
                        MappingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        ColumnName TEXT NOT NULL,
                        OldVantageName TEXT,
                        AzureName TEXT,
                        DataType TEXT,
                        IsEditable INTEGER DEFAULT 1,
                        IsCalculated INTEGER DEFAULT 0,
                        CalcFormula TEXT,
                        Notes TEXT
                    );

                    -- Indexes for performance
                    CREATE INDEX IF NOT EXISTS idx_project ON Activities(ProjectID);
                    CREATE INDEX IF NOT EXISTS idx_area ON Activities(Area);
                    CREATE INDEX IF NOT EXISTS idx_assigned_to ON Activities(AssignedTo);
                    CREATE INDEX IF NOT EXISTS idx_unique_id ON Activities(UniqueID);
                    CREATE INDEX IF NOT EXISTS idx_roc_id ON Activities(ROCID);
                    CREATE INDEX IF NOT EXISTS idx_column_name ON ColumnMappings(ColumnName);
                ";

                command.ExecuteNonQuery();

                // Seed ColumnMappings table if empty
                SeedColumnMappings(connection);
                EnsureDeletedActivitiesTable();
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DatabaseSetup.InitializeDatabase");
                throw;
            }
        }

        // DatabaseSetup.cs (inside class)
        // DatabaseSetup.cs
        private static void EnsureDeletedActivitiesTable()
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                // 0) Activities must exist first
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

                // 1) Create the table if it doesn't exist (mirror columns only; no rows)
                using (var create = connection.CreateCommand())
                {
                    create.CommandText =
                        "CREATE TABLE IF NOT EXISTS Deleted_Activities AS SELECT * FROM Activities WHERE 0;";
                    create.ExecuteNonQuery();
                }

                // 2) Ensure audit columns exist (idempotent)
                EnsureColumn(connection, "Deleted_Activities", "DeletedDate", "TEXT NOT NULL");
                EnsureColumn(connection, "Deleted_Activities", "DeletedBy", "TEXT NULL");

                // 3) Helpful indexes
                using (var idx = connection.CreateCommand())
                {
                    idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Deleted_Activities_DeletedDate ON Deleted_Activities(DeletedDate);";
                    idx.ExecuteNonQuery();
                }
                using (var uidx = connection.CreateCommand())
                {
                    uidx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Deleted_Activities_ActivityID ON Deleted_Activities(ActivityID);";
                    uidx.ExecuteNonQuery();
                }

                VANTAGE.Utilities.AppLogger.Info("Deleted_Activities ensured.", "DB.EnsureDeletedActivitiesTable");
            }
            catch (Exception ex)
            {
                // Log the actual exception so we can see it in the log file
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




        /// Seed ColumnMappings table from CSV data (hardcoded from ColumnNameComparisonForAiModel.csv)
        /// This data is embedded in code for deployment - no CSV file needed at runtime

        private static void SeedColumnMappings(SqliteConnection connection)
        {
            // Check if already seeded
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM ColumnMappings";
            var count = (long)checkCmd.ExecuteScalar();

            if (count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"ColumnMappings already seeded with {count} entries");
                return; // Already seeded
            }

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO ColumnMappings (ColumnName, OldVantageName, AzureName, DataType, IsEditable, IsCalculated, CalcFormula, Notes)
            VALUES (@col, @old, @azure, @type, @edit, @calc, @formula, @notes)";

            // Add parameters
            insertCmd.Parameters.Add("@col", SqliteType.Text);
            insertCmd.Parameters.Add("@old", SqliteType.Text);
            insertCmd.Parameters.Add("@azure", SqliteType.Text);
            insertCmd.Parameters.Add("@type", SqliteType.Text);
            insertCmd.Parameters.Add("@edit", SqliteType.Integer);
            insertCmd.Parameters.Add("@calc", SqliteType.Integer);
            insertCmd.Parameters.Add("@formula", SqliteType.Text);
            insertCmd.Parameters.Add("@notes", SqliteType.Text);

            // Data from ColumnNameComparisonForAiModel.csv
            // Format: (NewVantage, OldVantage, Azure, DataType, IsEditable, IsCalculated, CalcFormula, Notes)
            (string, string, string, string, int, int, string, string)[] mappings = new (string, string, string, string, int, int, string, string)[]
            {
                ("Area", "Tag_Area", "Tag_Area", "Short Text", 1, 0, null, null),
                ("AssignedTo", "UDFEleven", "UDF11", "Short Text", 1, 0, null, null),
                ("Aux1", "Tag_Aux1", "Tag_Aux1", "Short Text", 1, 0, null, null),
                ("Aux2", "Tag_Aux2", "Tag_Aux2", "Short Text", 1, 0, null, null),
                ("Aux3", "Tag_Aux3", "Tag_Aux3", "Short Text", 1, 0, null, null),
                ("AzureUploadUtcDate", null, "Timestamp", "Date/Time", 0, 0, null, "When user submits to Azure - official date used in Power BI dashboard"),
                ("BaseUnit", "Val_Base_Unit", "Val_Base_Unit", "Number", 1, 0, null, null),
                ("BudgetHoursGroup", "Val_BudgetedHours_Group", "Val_BudgetedHours_Group", "Number", 1, 0, null, null),
                ("BudgetHoursROC", "Val_BudgetedHours_ROC", "Val_BudgetedHours_ROC", "Number", 1, 0, null, null),
                ("BudgetMHs", "Val_BudgetedHours_Ind", "Val_BudgetedHours_Ind", "Number", 1, 0, null, null),
                ("ChgOrdNO", "Tag_CONo", "Tag_CONo", "Short Text", 1, 0, null, null),
                ("ClientBudget", "VAL_UDF_Two", "VAL_UDF_Two", "Number", 1, 0, null, null),
                ("ClientCustom3", "VAL_UDF_Three", "VAL_UDF_Three", "Number", 1, 0, null, null),
                ("ClientEquivQty", "VAL_Client_EQ-QTY_BDG", "Val_Client_Eq_Qty_Bdg", "Number", 1, 0, null, null),
                ("CompType", "Catg_ComponentType", "Catg_ComponentType", "Short Text", 1, 0, null, null),
                ("CreatedBy", "UDFThirteen", "UDF13", "Short Text", 1, 0, null, null),
                ("DateTrigger", "Trg_DateTrigger", null, "Number", 1, 0, null, null),
                ("Description", "Tag_Descriptions", "Tag_Descriptions", "Short Text", 1, 0, null, null),
                ("DwgNO", "Dwg_PrimeDrawingNO", "Dwg_PrimeDrawingNO", "Short Text", 1, 0, null, null),
                ("EarnedMHsRoc", "Val_EarnedHours_ROC", null, "Number", 1, 0, null, null),
                ("EarnQtyEntry", "Val_EarnedQty", "Val_EarnedQty", "Number", 1, 0, null, null),
                ("EqmtNO", "Tag_EqmtNo", "Tag_EqmtNo", "Short Text", 1, 0, null, null),
                ("EquivQTY", "Val_EQ-QTY", "Val_EQ-QTY", "Short Text", 1, 0, null, null),
                ("EquivUOM", "Val_EQ_UOM", "Val_EQ_UOM", "Short Text", 1, 0, null, null),
                ("Estimator", "Tag_Estimator", "Tag_Estimator", "Short Text", 1, 0, null, null),
                ("SchFinish", "Sch_Finish", null, "Date/Time", 1, 0, null, "will make calculated later when schedule module is developed"),
                ("HexNO", "HexNO", "HexNO", "Number", 1, 0, null, null),
                ("HtTrace", "Tag_Tracing", "Tag_Tracing", "Short Text", 1, 0, null, null),
                ("InsulType", "Tag_Insulation_Typ", "Tag_Insulation_Typ", "Short Text", 1, 0, null, null),
                ("LineNO", "Tag_LineNo", "Tag_LineNo", "Short Text", 1, 0, null, null),
                ("MtrlSpec", "Tag_Matl_Spec", "Tag_Matl_Spec", "Short Text", 1, 0, null, null),
                ("Notes", "Notes_Comments", "Notes_Comments", "Long Text", 1, 0, null, null),
                ("PaintCode", "Tag_Paint_Code", "Tag_Paint_Code", "Short Text", 1, 0, null, null),
                ("PercentEntry", "Val_Perc_Complete", "Val_Perc_Complete", "Number", 1, 0, null, "format 0-1 in import, export. Format 0-100% in datagrid display"),
                ("PhaseCategory", "Catg_PhaseCategory", "Catg_PhaseCategory", "Short Text", 1, 0, null, null),
                ("PhaseCode", "Tag_Phase Code", "Tag_PhaseCode", "Short Text", 1, 0, null, null),
                ("PipeGrade", "Tag_Pipe_Grade", "Tag_Pipe_Grade", "Short Text", 1, 0, null, null),
                ("PipeSize1", "Val_Pipe_Size1", "Val_Pipe_Size1", "Number", 1, 0, null, null),
                ("PipeSize2", "Val_Pipe_Size2", "Val_Pipe_Size2", "Number", 1, 0, null, null),
                ("PrevEarnMHs", "Val_Prev_Earned_Hours", null, "Number", 1, 0, null, null),
                ("PrevEarnQTY", "Val_Prev_Earned_Qty", null, "Number", 1, 0, null, null),
                ("ProgDate", null, "Val_ProgDate", "Date/Time", 0, 0, null, "timestamp when user clicks to submit progress to local db"),
                ("ProjectID", "Tag_ProjectID", "Tag_ProjectID", "Short Text", 1, 0, null, null),
                ("Quantity", "Val_Quantity", "Val_Quantity", "Number", 1, 0, null, null),
                ("RevNO", "Dwg_RevisionNo", "Dwg_RevisionNo", "Short Text", 1, 0, null, null),
                ("RFINO", "Tag_RFINo", "Tag_RFINo", "Short Text", 1, 0, null, null),
                ("ROCBudgetQTY", "Val_ROC_BudgetQty", "Val_ROC_BudgetQty", "Number", 1, 0, null, "needs to be same as Quantity on export"),
                ("ROCID", "Tag_ROC_ID", null, "Number", 1, 0, null, null),
                ("ROCPercent", "Val_ROC_Perc", null, "Number", 1, 0, null, null),
                ("ROCStep", "Catg_ROC_Step", "Catg_ROC_Step", "Short Text", 1, 0, null, null),
                ("SchedActNO", "Tag_Sch_ActNo", "Tag_Sch_ActNo", "Short Text", 1, 0, null, null),
                ("SecondActno", "Sch_Actno", "Sch_Actno", "Short Text", 1, 0, null, null),
                ("SecondDwgNO", "Dwg_SecondaryDrawingNO", "Dwg_SecondaryDrawingNO", "Short Text", 1, 0, null, null),
                ("Service", "Tag_Service", "Tag_Service", "Short Text", 1, 0, null, null),
                ("ShopField", "Tag_ShopField", "Tag_ShopField", "Short Text", 1, 0, null, null),
                ("ShtNO", "Dwg_ShtNo", "Dwg_ShtNo", "Short Text", 1, 0, null, null),
                ("SchStart", "Sch_Start", "Sch_Start", "Date/Time", 1, 0, null, "will make calculated later when schedule module is developed"),
                ("SubArea", "Tag_SubArea", "Tag_SubArea", "Short Text", 1, 0, null, null),
                ("PjtSystem", "Tag_System", "Tag_System", "Short Text", 1, 0, null, null),
                ("SystemNO", "Tag_SystemNo", null, "Short Text", 1, 0, null, null),
                ("TagNO", "Tag_TagNo", "Tag_TagNo", "Short Text", 1, 0, null, null),
                ("UDF1", "UDFOne", "UDF1", "Short Text", 1, 0, null, null),
                ("UDF10", "UDFTen", "UDF10", "Short Text", 1, 0, null, null),
                ("UDF11", "UDFEleven", "UDF11", "Short Text", 1, 0, null, "Separate field - was mapped to AssignedTo before refactor"),
                ("UDF12", "UDFTwelve", "UDF12", "Short Text", 1, 0, null, "Separate field - was mapped to UpdatedBy before refactor"),
                ("UDF13", "UDFThirteen", "UDF13", "Short Text", 1, 0, null, "Separate field - was mapped to CreatedBy before refactor"),
                ("UDF14", "UDFFourteen", "UDF14", "Short Text", 1, 0, null, null),
                ("UDF15", "UDFFifteen", "UDF15", "Short Text", 1, 0, null, null),
                ("UDF16", "UDFSixteen", "UDF16", "Short Text", 1, 0, null, null),
                ("UDF17", "UDFSeventeen", "UDF17", "Short Text", 1, 0, null, null),
                ("UDF18", "UDFEighteen", "UDF18", "Short Text", 1, 0, null, null),
                ("UDF2", "UDFTwo", "UDF2", "Short Text", 1, 0, null, null),
                ("UDF20", "UDFTwenty", "UDF20", "Short Text", 1, 0, null, null),
                ("UDF3", "UDFThree", "UDF3", "Short Text", 1, 0, null, null),
                ("UDF4", "UDFFour", "UDF4", "Short Text", 1, 0, null, null),
                ("UDF5", "UDFFive", "UDF5", "Short Text", 1, 0, null, null),
                ("UDF6", "UDFSix", "UDF6", "Short Text", 1, 0, null, null),
                ("UDF7", "UDFSeven", "UDF7", "Short Text", 1, 0, null, null),
                ("UDF8", "UDFEight", "UDF8", "Short Text", 1, 0, null, null),
                ("UDF9", "UDFNine", "UDF9", "Short Text", 1, 0, null, null),
                ("UniqueID", "UDFNineteen", "UDF19", "Short Text", 0, 0, null, "If null on import, calculated using \"i\" & base time at time of import (yymmddhhnnss) & iterated integer from 1 to n (number of records import without UDFNineteen value) & last three characters of current username. Example: i2510300738271ano, i2510300738272ano, etc"),
                ("UOM", "Val_UOM", "Val_UOM", "Short Text", 1, 0, null, null),
                ("UpdatedBy", null, "UpdatedBy", "Short Text", 0, 0, null, "Username who last modified this record"),
                ("UpdatedUtcDate", null, "UpdatedUtcDate", "Date/Time", 0, 0, null, "UTC timestamp of last modification"),
                ("LocalDirty", null, "LocalDirty", "Number", 0, 0, null, "Sync flag: 0=synced with central, 1=needs sync"),
                ("UserID", null, "UserID", "Short Text", 0, 0, null, "created upon upload, sync to Azure (current username)"),
                ("WeekEndDate", "Val_TimeStamp", "Val_TimeStamp", "Date/Time", 0, 0, null, "Week Ending Date set when user submits to Local or Azure"),
                ("WorkPackage", "Tag_WorkPackage", "Tag_WorkPackage", "Short Text", 1, 0, null, null),
                ("XRay", "Tag_XRAY", "Tag_XRAY", "Number", 1, 0, null, null)
            };

            foreach (var mapping in mappings)
            {
                insertCmd.Parameters["@col"].Value = mapping.Item1;
                insertCmd.Parameters["@old"].Value = (object)mapping.Item2 ?? DBNull.Value;
                insertCmd.Parameters["@azure"].Value = (object)mapping.Item3 ?? DBNull.Value;
                insertCmd.Parameters["@type"].Value = mapping.Item4;
                insertCmd.Parameters["@edit"].Value = mapping.Item5;
                insertCmd.Parameters["@calc"].Value = mapping.Item6;
                insertCmd.Parameters["@formula"].Value = (object)mapping.Item7 ?? DBNull.Value;
                insertCmd.Parameters["@notes"].Value = (object)mapping.Item8 ?? DBNull.Value;

                insertCmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine($"✓ Seeded {mappings.Length} column mappings");
        }

        public static void SeedTestUsers()
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                var testUsers = new[]
                {
                    new { Username = "John.Smith", FullName = "John Smith", Email = "john.smith@company.com" },
                    new { Username = "Jane.Doe", FullName = "Jane Doe", Email = "jane.doe@company.com" },
                    new { Username = "Mike.Johnson", FullName = "Mike Johnson", Email = "mike.johnson@company.com" },
                    new { Username = "Sarah.Williams", FullName = "Sarah Williams", Email = "sarah.williams@company.com" }
                };

                foreach (var user in testUsers)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                    INSERT OR IGNORE INTO Users (Username, FullName, Email, PhoneNumber, IsAdmin)
                    VALUES (@username, @fullName, @email, '', 0)";
                    command.Parameters.AddWithValue("@username", user.Username);
                    command.Parameters.AddWithValue("@fullName", user.FullName);
                    command.Parameters.AddWithValue("@email", user.Email);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
                MessageBox.Show($"Error seeding test users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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