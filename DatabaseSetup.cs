using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using VANTAGE.Models;
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

                // Tables to mirror from Azure (Azure name -> Local name)
                var tableMappings = new Dictionary<string, string>
                {
                    { "VMS_Users", "Users" },
                    { "VMS_Projects", "Projects" },
                    { "VMS_ColumnMappings", "ColumnMappings" },
                    { "VMS_Managers", "Managers" },
                    { "VMS_Feedback", "Feedback" }
                };

                foreach (var mapping in tableMappings)
                {
                    CopyTableDataFromAzure(azureConn, localConn, mapping.Key, mapping.Value);
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
        private static void CopyTableDataFromAzure(SqlConnection azureConn, SqliteConnection localConn, string azureTableName, string localTableName)
        {
            // Clear existing data in local table
            var deleteCmd = localConn.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM {localTableName}";
            deleteCmd.ExecuteNonQuery();

            // Get all data from Azure
            var selectCmd = azureConn.CreateCommand();
            selectCmd.CommandText = $"SELECT * FROM {azureTableName}";

            using var reader = selectCmd.ExecuteReader();

            // Build column list from the reader
            var columnNames = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            string columns = string.Join(", ", columnNames);
            string parameters = string.Join(", ", columnNames.Select(c => "@" + c));
            string insertSql = $"INSERT INTO {localTableName} ({columns}) VALUES ({parameters})";

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
                    }
                    insertCmd.Parameters.AddWithValue("@" + columnNames[i], value ?? DBNull.Value);
                }

                insertCmd.ExecuteNonQuery();
                rowCount++;
            }

            AppLogger.Info($"Copied {rowCount} rows from {azureTableName} to {localTableName}", "DatabaseSetup.CopyTableDataFromAzure");
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

            -- UserSettings table (local only - single user per machine)
            CREATE TABLE IF NOT EXISTS UserSettings (
                UserSettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                SettingName TEXT NOT NULL UNIQUE,
                SettingValue TEXT,
                DataType TEXT,
                LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- Schedule table (local only - P6 import data)
            CREATE TABLE IF NOT EXISTS Schedule (
                SchedActNO            TEXT NOT NULL,
                WeekEndDate           TEXT NOT NULL,
                WbsId                 TEXT NOT NULL DEFAULT '',
                Description           TEXT NOT NULL DEFAULT '',
                P6_Start       TEXT,
                P6_Finish      TEXT,
                P6_ActualStart        TEXT,
                P6_ActualFinish       TEXT,
                P6_PercentComplete    REAL NOT NULL DEFAULT 0,
                P6_BudgetMHs          REAL NOT NULL DEFAULT 0,
                MissedStartReason     TEXT,
                MissedFinishReason    TEXT,
                UpdatedBy             TEXT NOT NULL DEFAULT '',
                UpdatedUtcDate        TEXT NOT NULL,
                PRIMARY KEY (SchedActNO, WeekEndDate)
            );

            -- ScheduleProjectMappings table (local only - which projects does this schedule cover)
            CREATE TABLE IF NOT EXISTS ScheduleProjectMappings (
                WeekEndDate           TEXT NOT NULL,
                ProjectID             TEXT NOT NULL,
                PRIMARY KEY (WeekEndDate, ProjectID)
            );

            -- ThreeWeekLookahead table (local only - user forecasts that persist across P6 imports)
            CREATE TABLE IF NOT EXISTS ThreeWeekLookahead (
                SchedActNO            TEXT NOT NULL,
                ProjectID             TEXT NOT NULL,
                ThreeWeekStart        TEXT,
                ThreeWeekFinish       TEXT,
                PRIMARY KEY (SchedActNO, ProjectID)
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
                Phone TEXT NOT NULL DEFAULT '',
                Fax TEXT NOT NULL DEFAULT '',
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

            -- Feedback table (mirrored from Azure)
            CREATE TABLE IF NOT EXISTS Feedback (
                Id INTEGER PRIMARY KEY,
                Type TEXT NOT NULL DEFAULT 'Idea',
                Title TEXT NOT NULL DEFAULT '',
                Description TEXT,
                Status TEXT NOT NULL DEFAULT 'New',
                CreatedBy TEXT NOT NULL DEFAULT '',
                CreatedUtcDate TEXT NOT NULL,
                UpdatedBy TEXT,
                UpdatedUtcDate TEXT,
                IsDeleted INTEGER NOT NULL DEFAULT 0
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
                PjtSystemNo           TEXT NOT NULL DEFAULT '',
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
                RespParty             TEXT NOT NULL DEFAULT '',
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
            -- FormTemplates table (local only - Work Package form definitions)
            CREATE TABLE IF NOT EXISTS FormTemplates (
                TemplateID TEXT PRIMARY KEY,
                TemplateName TEXT NOT NULL,
                TemplateType TEXT NOT NULL,
                StructureJson TEXT NOT NULL,
                IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                CreatedBy TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );

            -- WPTemplates table (local only - Work Package template definitions)
            CREATE TABLE IF NOT EXISTS WPTemplates (
                WPTemplateID TEXT PRIMARY KEY,
                WPTemplateName TEXT NOT NULL,
                FormsJson TEXT NOT NULL,
                DefaultSettings TEXT NOT NULL,
                IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                CreatedBy TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );

            -- ProgressBookLayouts table (local only - Progress Book layout definitions)
            CREATE TABLE IF NOT EXISTS ProgressBookLayouts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ProjectId TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL,
                ConfigurationJson TEXT NOT NULL
            );

            -- Indexes for performance
            CREATE INDEX IF NOT EXISTS idx_project ON Activities(ProjectID);
            CREATE INDEX IF NOT EXISTS idx_area ON Activities(Area);
            CREATE INDEX IF NOT EXISTS idx_assigned_to ON Activities(AssignedTo);
            CREATE INDEX IF NOT EXISTS idx_unique_id ON Activities(UniqueID);
            CREATE INDEX IF NOT EXISTS idx_roc_id ON Activities(ROCID);
            CREATE INDEX IF NOT EXISTS idx_column_name ON ColumnMappings(ColumnName);
            CREATE INDEX IF NOT EXISTS idx_schedule_weekenddate ON Schedule(WeekEndDate);
            CREATE INDEX IF NOT EXISTS idx_schedprojmap_weekenddate ON ScheduleProjectMappings(WeekEndDate);
            CREATE INDEX IF NOT EXISTS idx_3wla_projectid ON ThreeWeekLookahead(ProjectID);
            CREATE INDEX IF NOT EXISTS idx_feedback_type ON Feedback(Type);
            CREATE INDEX IF NOT EXISTS idx_feedback_status ON Feedback(Status);
            CREATE INDEX IF NOT EXISTS idx_formtemplate_name ON FormTemplates(TemplateName);
            CREATE INDEX IF NOT EXISTS idx_formtemplate_type ON FormTemplates(TemplateType);
            CREATE INDEX IF NOT EXISTS idx_wptemplate_name ON WPTemplates(WPTemplateName);
            CREATE INDEX IF NOT EXISTS idx_pblayout_projectid ON ProgressBookLayouts(ProjectId);
            CREATE INDEX IF NOT EXISTS idx_pblayout_name ON ProgressBookLayouts(Name);
        ";

                command.ExecuteNonQuery();

                // Seed built-in templates if not present
                SeedBuiltInTemplates(connection);
            }
            catch (Exception ex)
            {
                VANTAGE.Utilities.AppLogger.Error(ex, "DatabaseSetup.InitializeDatabase");
                throw;
            }
        }

        // Seed built-in form templates and WP templates
        private static void SeedBuiltInTemplates(SqliteConnection connection)
        {
            try
            {
                // Check if built-in templates already exist
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM FormTemplates WHERE IsBuiltIn = 1";
                var count = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0);
                if (count > 0)
                {
                    return; // Already seeded
                }

                var createdUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var createdBy = "System";

                // Built-in Form Template IDs (stable for references)
                const string coverSheetId = "builtin-cover-sheet";
                const string tocId = "builtin-toc";
                const string checklistId = "builtin-checklist";
                const string punchlistId = "builtin-punchlist";
                const string signoffId = "builtin-signoff";
                const string dwgLogId = "builtin-dwg-log";
                const string drawingsId = "builtin-drawings";

                // 1. Cover Sheet (Cover type)
                var coverStructure = new CoverStructure
                {
                    Title = "COVER SHEET",
                    ImagePath = null, // null = use default images/CoverPic.png
                    ImageWidthPercent = 80,
                    FooterText = null
                };
                InsertFormTemplate(connection, coverSheetId, "Cover Sheet - Template", TemplateTypes.Cover,
                    JsonSerializer.Serialize(coverStructure), createdBy, createdUtc);

                // 2. TOC (List type)
                var tocStructure = new ListStructure
                {
                    Title = "TABLE OF CONTENTS",
                    Items = new List<string>
                    {
                        "WP DOC EXPIRATION DATE: {ExpirationDate}",
                        "PRINTED: {PrintedDate}",
                        "WP NAME: {WPName}",
                        "SCHEDULE ACTIVITY NO: {SchedActNO}",
                        "PHASE CODE: {PhaseCode}",
                        "",
                        "1    WP Coversheet",
                        "2    WP Checklist",
                        "3    Drawing Log",
                        "4    Drawings",
                        "5    Punchlist Form",
                        "6    WP Walkdown and Acceptance Form"
                    },
                    FooterText = null
                };
                InsertFormTemplate(connection, tocId, "Table of Contents - Template", TemplateTypes.List,
                    JsonSerializer.Serialize(tocStructure), createdBy, createdUtc);

                // 3. Checklist (Form type)
                var checklistStructure = new FormStructure
                {
                    Title = "CHECKLIST",
                    Columns = new List<TemplateColumn>
                    {
                        new TemplateColumn { Name = "ITEM", WidthPercent = 50 },
                        new TemplateColumn { Name = "DATE", WidthPercent = 12 },
                        new TemplateColumn { Name = "SIGN", WidthPercent = 15 },
                        new TemplateColumn { Name = "COMMENTS", WidthPercent = 23 }
                    },
                    RowHeightIncreasePercent = 0,
                    Sections = new List<SectionDefinition>
                    {
                        new SectionDefinition
                        {
                            Name = "6 WEEK ASSEMBLY",
                            Items = new List<string>
                            {
                                "Documents Assembled",
                                "Estimate/Quantities Verified",
                                "Material Takeoff Complete",
                                "Work Package Assembled",
                                "Work Package Final Review",
                                "Work Package Assigned to Craft Superintendent"
                            }
                        },
                        new SectionDefinition
                        {
                            Name = "3 WEEK REVIEW",
                            Items = new List<string>
                            {
                                "Documents Reviewed",
                                "Materials Confirmed Onsite",
                                "RFIs Closed and Incorporated",
                                "Final Document Revisions Confirmed",
                                "Work Package Issued to Construction"
                            }
                        },
                        new SectionDefinition
                        {
                            Name = "CONSTRUCTION COMPLETE SIGN-OFFS",
                            Items = new List<string>
                            {
                                "Work Package Walked Down and Verified Complete",
                                "Quality Signoffs Complete",
                                "Engineering Signoffs Complete"
                            }
                        }
                    },
                    FooterText = null
                };
                InsertFormTemplate(connection, checklistId, "Checklist - Template", TemplateTypes.Form,
                    JsonSerializer.Serialize(checklistStructure), createdBy, createdUtc);

                // 4. Punchlist (Grid type) - smaller base font to fit many columns
                var punchlistStructure = new GridStructure
                {
                    Title = "PUNCHLIST",
                    Columns = new List<TemplateColumn>
                    {
                        new TemplateColumn { Name = "PL NO", WidthPercent = 6 },
                        new TemplateColumn { Name = "TAG/LINE/CABLE", WidthPercent = 12 },
                        new TemplateColumn { Name = "RAISED BY/ON", WidthPercent = 10 },
                        new TemplateColumn { Name = "PL ITEM DESCRIPTION", WidthPercent = 30 },
                        new TemplateColumn { Name = "RP", WidthPercent = 5 },
                        new TemplateColumn { Name = "DUE BY", WidthPercent = 9 },
                        new TemplateColumn { Name = "CL DATE", WidthPercent = 7 },
                        new TemplateColumn { Name = "CL BY", WidthPercent = 7 },
                        new TemplateColumn { Name = "VR DATE", WidthPercent = 7 },
                        new TemplateColumn { Name = "VR BY", WidthPercent = 7 }
                    },
                    RowCount = 22,
                    RowHeightIncreasePercent = 0,
                    BaseHeaderFontSize = 6.3f,  // 30% smaller than default 9pt
                    FooterText = null
                };
                InsertFormTemplate(connection, punchlistId, "Punchlist - Template", TemplateTypes.Grid,
                    JsonSerializer.Serialize(punchlistStructure), createdBy, createdUtc);

                // 5. Signoff Sheet (Form type)
                var signoffStructure = new FormStructure
                {
                    Title = "SIGN-OFF SHEET",
                    Columns = new List<TemplateColumn>
                    {
                        new TemplateColumn { Name = "ITEM", WidthPercent = 50 },
                        new TemplateColumn { Name = "DATE", WidthPercent = 12 },
                        new TemplateColumn { Name = "SIGN", WidthPercent = 15 },
                        new TemplateColumn { Name = "COMMENTS", WidthPercent = 23 }
                    },
                    RowHeightIncreasePercent = 0,
                    Sections = new List<SectionDefinition>
                    {
                        new SectionDefinition
                        {
                            Name = "GENERAL INFORMATION",
                            Items = new List<string>
                            {
                                "Date Completed",
                                "Date of Walkdown",
                                "Punchlist Generated Date",
                                "Punchlist Completed Date"
                            }
                        },
                        new SectionDefinition
                        {
                            Name = "SIGN-OFFS",
                            Items = new List<string>
                            {
                                "General Foreman",
                                "Superintendent",
                                "Engineering (see note below)***",
                                "Quality Control",
                                "Package Completed Accepted"
                            }
                        },
                        new SectionDefinition
                        {
                            Name = "ADDITIONAL NOTES",
                            Items = new List<string>()
                        }
                    },
                    FooterText = "***As part of the sign-off all drawings used in the package will be manually checked against the current revisions to ensure that the latest revisions were utilized for construction. The Field Engineer and/or Project Engineer will physically walk down the scope of this work to ensure that all components have been installed and documented properly. The Project Engineer will confirm all MTRs and COCs were compiled and all Heat Numbers transfered to the material as well as to the Heat Mapped/Weld Mapped Drawings"
                };
                InsertFormTemplate(connection, signoffId, "Signoff Sheet - Template", TemplateTypes.Form,
                    JsonSerializer.Serialize(signoffStructure), createdBy, createdUtc);

                // 6. DWG Log (Grid type - for tracking drawings)
                var dwgLogStructure = new GridStructure
                {
                    Title = "DRAWING LOG",
                    Columns = new List<TemplateColumn>
                    {
                        new TemplateColumn { Name = "DWG NO", WidthPercent = 20 },
                        new TemplateColumn { Name = "DESCRIPTION", WidthPercent = 40 },
                        new TemplateColumn { Name = "REV", WidthPercent = 10 },
                        new TemplateColumn { Name = "DATE", WidthPercent = 15 },
                        new TemplateColumn { Name = "NOTES", WidthPercent = 15 }
                    },
                    RowCount = 20,
                    RowHeightIncreasePercent = 0,
                    FooterText = null
                };
                InsertFormTemplate(connection, dwgLogId, "Drawing Log - Template", TemplateTypes.Grid,
                    JsonSerializer.Serialize(dwgLogStructure), createdBy, createdUtc);

                // 7. Drawings (Drawings type - displays drawing images from local folder)
                var drawingsStructure = new DrawingsStructure
                {
                    Title = "DRAWINGS",
                    Source = "Local",
                    FolderPath = null, // User must configure folder path
                    FileExtensions = "*.pdf,*.png,*.jpg,*.jpeg,*.tif,*.tiff",
                    ImagesPerPage = 1,
                    ShowCaptions = true,
                    FooterText = null
                };
                InsertFormTemplate(connection, drawingsId, "Drawings - Placeholder", TemplateTypes.Drawings,
                    JsonSerializer.Serialize(drawingsStructure), createdBy, createdUtc);

                // Built-in WP Template: Summit Standard WP
                var wpFormsJson = JsonSerializer.Serialize(new List<FormReference>
                {
                    new FormReference { FormTemplateId = coverSheetId },
                    new FormReference { FormTemplateId = tocId },
                    new FormReference { FormTemplateId = checklistId },
                    new FormReference { FormTemplateId = dwgLogId },
                    new FormReference { FormTemplateId = drawingsId },
                    new FormReference { FormTemplateId = punchlistId },
                    new FormReference { FormTemplateId = signoffId }
                });
                var wpSettings = JsonSerializer.Serialize(new WPTemplateSettings { ExpirationDays = 14 });

                var wpCmd = connection.CreateCommand();
                wpCmd.CommandText = @"
                    INSERT INTO WPTemplates (WPTemplateID, WPTemplateName, FormsJson, DefaultSettings,
                                             IsBuiltIn, CreatedBy, CreatedUtc)
                    VALUES (@wpTemplateId, @wpTemplateName, @formsJson, @defaultSettings,
                            1, @createdBy, @createdUtc)";
                wpCmd.Parameters.AddWithValue("@wpTemplateId", "builtin-summit-standard");
                wpCmd.Parameters.AddWithValue("@wpTemplateName", "Summit Standard WP");
                wpCmd.Parameters.AddWithValue("@formsJson", wpFormsJson);
                wpCmd.Parameters.AddWithValue("@defaultSettings", wpSettings);
                wpCmd.Parameters.AddWithValue("@createdBy", createdBy);
                wpCmd.Parameters.AddWithValue("@createdUtc", createdUtc);
                wpCmd.ExecuteNonQuery();

                AppLogger.Info("Seeded built-in Work Package templates", "DatabaseSetup.SeedBuiltInTemplates");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseSetup.SeedBuiltInTemplates");
            }
        }

        // Helper to insert a form template
        private static void InsertFormTemplate(SqliteConnection connection, string templateId, string name,
            string type, string structureJson, string createdBy, string createdUtc)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FormTemplates (TemplateID, TemplateName, TemplateType, StructureJson,
                                           IsBuiltIn, CreatedBy, CreatedUtc)
                VALUES (@templateId, @templateName, @templateType, @structureJson,
                        1, @createdBy, @createdUtc)";
            cmd.Parameters.AddWithValue("@templateId", templateId);
            cmd.Parameters.AddWithValue("@templateName", name);
            cmd.Parameters.AddWithValue("@templateType", type);
            cmd.Parameters.AddWithValue("@structureJson", structureJson);
            cmd.Parameters.AddWithValue("@createdBy", createdBy);
            cmd.Parameters.AddWithValue("@createdUtc", createdUtc);
            cmd.ExecuteNonQuery();
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

        // Ensure indexes exist on Azure tables for query performance
        public static void EnsureAzureIndexes()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                AppLogger.Info("Checking Azure indexes...", "DatabaseSetup.EnsureAzureIndexes");

                using var conn = AzureDbManager.GetConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 0; // index creation on large tables can take time
                cmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_ProgressLog_Delete_Lookup'
                                   AND object_id = OBJECT_ID('VANTAGE_global_ProgressLog'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_ProgressLog_Delete_Lookup
                        ON VANTAGE_global_ProgressLog (Tag_ProjectID, UDF18, [Timestamp], Val_TimeStamp);
                        SELECT 1; -- index was created
                    END
                    ELSE
                        SELECT 0; -- index already existed
                ";
                var result = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                sw.Stop();

                string elapsed = sw.Elapsed.TotalSeconds < 60
                    ? $"{sw.Elapsed.TotalSeconds:F1}s"
                    : $"{sw.Elapsed.TotalMinutes:F1}m";

                if (result == 1)
                    AppLogger.Info($"Created IX_ProgressLog_Delete_Lookup index in {elapsed}", "DatabaseSetup.EnsureAzureIndexes");
                else
                    AppLogger.Info($"Azure indexes verified ({elapsed})", "DatabaseSetup.EnsureAzureIndexes");
            }
            catch (Exception ex)
            {
                // Non-fatal - app works without the index, just slower deletes
                AppLogger.Error(ex, "DatabaseSetup.EnsureAzureIndexes");
            }
        }
    }
}