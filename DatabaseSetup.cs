using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.IO;
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
                        FOREIGN KEY (UserID) REFERENCES Users(UserID),
                        UNIQUE(UserID, SettingName)
                    );

                    -- Activities table (NEW: NewVantage column names)
                    CREATE TABLE IF NOT EXISTS Activities (
                        ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
  
              -- Core Fields
       HexNO INTEGER DEFAULT 0,
                  ProjectID TEXT DEFAULT '',
   Description TEXT DEFAULT '',
   UniqueID TEXT UNIQUE NOT NULL,
        
             -- Area/Location
            Area TEXT DEFAULT '',
      SubArea TEXT DEFAULT '',
      System TEXT DEFAULT '',
             SystemNO TEXT DEFAULT '',
            
           -- Categories
        CompType TEXT DEFAULT '',
                   PhaseCategory TEXT DEFAULT '',
  ROCStep TEXT DEFAULT '',
           
      -- Assignments
            AssignedTo TEXT DEFAULT 'Unassigned',
             CreatedBy TEXT DEFAULT '',
 LastModifiedBy TEXT DEFAULT '',
          
      -- Progress (STORED AS 0-100 PERCENTAGE)
   PercentEntry REAL DEFAULT 0,
          Quantity REAL DEFAULT 0,
   EarnQtyEntry REAL DEFAULT 0,
       UOM TEXT DEFAULT '',
   
           -- Budgets & Hours
          BudgetMHs REAL DEFAULT 0,
      BudgetHoursGroup REAL DEFAULT 0,
       BudgetHoursROC REAL DEFAULT 0,
 BaseUnit REAL DEFAULT 0,
         EarnedMHsRoc INTEGER DEFAULT 0,
       
        -- ROC
         ROCID INTEGER DEFAULT 0,
       ROCPercent REAL DEFAULT 0,
           ROCBudgetQTY REAL DEFAULT 0,
 
      -- Drawings
 DwgNO TEXT DEFAULT '',
              RevNO TEXT DEFAULT '',
     SecondDwgNO TEXT DEFAULT '',
           ShtNO TEXT DEFAULT '',
   
     -- Tags/References
    TagNO TEXT DEFAULT '',
        WorkPackage TEXT DEFAULT '',
           PhaseCode TEXT DEFAULT '',
Service TEXT DEFAULT '',
  ShopField TEXT DEFAULT '',
         SchedActNO TEXT DEFAULT '',
      SecondActno TEXT DEFAULT '',
           
 -- Equipment/Line
        EqmtNO TEXT DEFAULT '',
     LineNO TEXT DEFAULT '',
         ChgOrdNO TEXT DEFAULT '',
     
     -- Materials
      MtrlSpec TEXT DEFAULT '',
 PipeGrade TEXT DEFAULT '',
   PaintCode TEXT DEFAULT '',
          InsulType TEXT DEFAULT '',
      HtTrace TEXT DEFAULT '',
              
        -- Pipe
     PipeSize1 REAL DEFAULT 0,
      PipeSize2 REAL DEFAULT 0,
        
   -- Auxiliary
              Aux1 TEXT DEFAULT '',
Aux2 TEXT DEFAULT '',
        Aux3 TEXT DEFAULT '',
       Estimator TEXT DEFAULT '',
    RFINO TEXT DEFAULT '',
               XRay REAL DEFAULT 0,
           
       -- Equipment Quantities
        EquivQTY TEXT DEFAULT '',
       EquivUOM TEXT DEFAULT '',
      
       -- Client Fields
           ClientEquivQty REAL DEFAULT 0,
    ClientBudget REAL DEFAULT 0,
ClientCustom3 REAL DEFAULT 0,
            ClientEquivEarnQTY TEXT DEFAULT '',
      
    -- Previous/History
 PrevEarnMHs REAL DEFAULT 0,
         PrevEarnQTY REAL DEFAULT 0,
       
   -- Schedule
             Start TEXT DEFAULT '',
               Finish TEXT DEFAULT '',
        
       -- Trigger
    DateTrigger INTEGER DEFAULT 0,
     
      -- Notes
  Notes TEXT DEFAULT '',
           
          -- User-Defined Fields (excluding special ones mapped above)
          UDF1 TEXT DEFAULT '',
      UDF2 TEXT DEFAULT '',
       UDF3 TEXT DEFAULT '',
         UDF4 TEXT DEFAULT '',
            UDF5 TEXT DEFAULT '',
 UDF6 TEXT DEFAULT '',
                UDF7 TEXT DEFAULT '',
       UDF8 TEXT DEFAULT '',
            UDF9 TEXT DEFAULT '',
         UDF10 TEXT DEFAULT '',
    -- UDF11 = AssignedTo
         -- UDF12 = LastModifiedBy
            -- UDF13 = CreatedBy
         UDF14 TEXT DEFAULT '',
     UDF15 TEXT DEFAULT '',
 UDF16 TEXT DEFAULT '',
      UDF17 TEXT DEFAULT '',
    UDF18 TEXT DEFAULT '',
 -- UDF19 = UniqueID
     UDF20 TEXT DEFAULT ''
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
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Seed ColumnMappings table from CSV data
        /// </summary>
        private static void SeedColumnMappings(SqliteConnection connection)
        {
     // Check if already seeded
            var checkCmd = connection.CreateCommand();
    checkCmd.CommandText = "SELECT COUNT(*) FROM ColumnMappings";
            var count = (long)checkCmd.ExecuteScalar();

       if (count > 0) return; // Already seeded

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

            // Seed all mappings from CSV
     var mappings = new[]
            {
       ("ActivityID", null, "ActivityID", "Integer", 0, 0, null, "Auto-increment primary key"),
        ("Area", "Tag_Area", "Tag_Area", "Text", 1, 0, null, null),
     ("AssignedTo", "UDFEleven", "UDF11", "Text", 1, 0, null, "Default: Unassigned"),
     ("Aux1", "Tag_Aux1", "Tag_Aux1", "Text", 1, 0, null, null),
    ("Aux2", "Tag_Aux2", "Tag_Aux2", "Text", 1, 0, null, null),
      ("Aux3", "Tag_Aux3", "Tag_Aux3", "Text", 1, 0, null, null),
         ("AzureUploadDate", null, "Timestamp", "DateTime", 0, 1, null, "When user submits to Azure"),
    ("BaseUnit", "Val_Base_Unit", "Val_Base_Unit", "Number", 1, 0, null, null),
    ("BudgetHoursGroup", "Val_BudgetedHours_Group", "Val_BudgetedHours_Group", "Number", 1, 0, null, null),
("BudgetHoursROC", "Val_BudgetedHours_ROC", "Val_BudgetedHours_ROC", "Number", 1, 0, null, null),
     ("BudgetMHs", "Val_BudgetedHours_Ind", "Val_BudgetedHours_Ind", "Number", 1, 0, null, null),
 ("ChgOrdNO", "Tag_CONo", "Tag_CONo", "Text", 1, 0, null, null),
   ("ClientBudget", "VAL_UDF_Two", "VAL_UDF_Two", "Number", 1, 0, null, null),
          ("ClientCustom3", "VAL_UDF_Three", "VAL_UDF_Three", "Number", 1, 0, null, null),
            ("ClientEquivEarnQTY", "VAL_Client_Earned_EQ-QTY", "VAL_Client_Earned_EQ-QTY", "Text", 1, 0, null, null),
            ("ClientEquivQty", "VAL_Client_EQ-QTY_BDG", "Val_Client_Eq_Qty_Bdg", "Number", 1, 0, null, null),
 ("CompType", "Catg_ComponentType", "Catg_ComponentType", "Text", 1, 0, null, null),
           ("CreatedBy", "UDFThirteen", "UDF13", "Text", 1, 0, null, null),
      ("DateTrigger", "Trg_DateTrigger", null, "Number", 1, 0, null, "Not synced to Azure"),
      ("Description", "Tag_Descriptions", "Tag_Descriptions", "Text", 1, 0, null, null),
            ("DwgNO", "Dwg_PrimeDrawingNO", "Dwg_PrimeDrawingNO", "Text", 1, 0, null, null),
      ("EarnedMHsRoc", "Val_EarnedHours_ROC", null, "Number", 1, 0, null, "Not synced"),
         ("EarnedQtyCalc", "Val_Earn_Qty", null, "Number", 0, 1, "PercentEntry / 100 * Quantity", "Calculated"),
      ("EarnMHsCalc", "Val_EarnedHours_Ind", "Val_EarnedHours_Ind", "Number", 0, 1, "PercentEntry / 100 * BudgetMHs", "Calculated"),
      ("EarnQtyEntry", "Val_EarnedQty", "Val_EarnedQty", "Number", 1, 0, null, null),
        ("EqmtNO", "Tag_EqmtNo", "Tag_EqmtNo", "Text", 1, 0, null, null),
("EquivQTY", "Val_EQ-QTY", "Val_EQ-QTY", "Text", 1, 0, null, null),
   ("EquivUOM", "Val_EQ_UOM", "Val_EQ_UOM", "Text", 1, 0, null, null),
                ("Estimator", "Tag_Estimator", "Tag_Estimator", "Text", 1, 0, null, null),
        ("Finish", "Sch_Finish", null, "DateTime", 1, 0, null, "Future: calculated"),
      ("HexNO", "HexNO", "HexNO", "Number", 1, 0, null, null),
     ("HtTrace", "Tag_Tracing", "Tag_Tracing", "Text", 1, 0, null, null),
         ("InsulType", "Tag_Insulation_Typ", "Tag_Insulation_Typ", "Text", 1, 0, null, null),
      ("LastModifiedBy", "UDFTwelve", "UDF12", "Text", 1, 0, null, null),
        ("LineNO", "Tag_LineNo", "Tag_LineNo", "Text", 1, 0, null, null),
    ("MtrlSpec", "Tag_Matl_Spec", "Tag_Matl_Spec", "Text", 1, 0, null, null),
         ("Notes", "Notes_Comments", "Notes_Comments", "LongText", 1, 0, null, null),
    ("PaintCode", "Tag_Paint_Code", "Tag_Paint_Code", "Text", 1, 0, null, null),
      ("PercentCompleteCalc", "Val_Percent_Earned", null, "Number", 0, 1, "PercentEntry", "Legacy alias"),
              ("PercentEntry", "Val_Perc_Complete", "Val_Perc_Complete", "Number", 1, 0, null, "Stored as 0-100"),
   ("PhaseCategory", "Catg_PhaseCategory", "Catg_PhaseCategory", "Text", 1, 0, null, null),
   ("PhaseCode", "Tag_Phase Code", "Tag_PhaseCode", "Text", 1, 0, null, null),
   ("PipeGrade", "Tag_Pipe_Grade", "Tag_Pipe_Grade", "Text", 1, 0, null, null),
  ("PipeSize1", "Val_Pipe_Size1", "Val_Pipe_Size1", "Number", 1, 0, null, null),
             ("PipeSize2", "Val_Pipe_Size2", "Val_Pipe_Size2", "Number", 1, 0, null, null),
  ("PrevEarnMHs", "Val_Prev_Earned_Hours", null, "Number", 1, 0, null, "Not synced"),
    ("PrevEarnQTY", "Val_Prev_Earned_Qty", null, "Number", 1, 0, null, "Not synced"),
     ("ProgDate", null, "Val_ProgDate", "DateTime", 0, 1, null, "Timestamp when submitted"),
             ("ProjectID", "Tag_ProjectID", "Tag_ProjectID", "Text", 1, 0, null, null),
     ("Quantity", "Val_Quantity", "Val_Quantity", "Number", 1, 0, null, null),
       ("RevNO", "Dwg_RevisionNo", "Dwg_RevisionNo", "Text", 1, 0, null, null),
        ("RFINO", "Tag_RFINo", "Tag_RFINo", "Text", 1, 0, null, null),
       ("ROCBudgetQTY", "Val_ROC_BudgetQty", "Val_ROC_BudgetQty", "Number", 1, 0, null, "Must equal Quantity on export"),
    ("ROCID", "Tag_ROC_ID", null, "Number", 1, 0, null, "Not synced"),
            ("ROCLookupID", "LookUP_ROC_ID", null, "Text", 0, 1, "ProjectID | CompType | PhaseCategory | ROCStep", "Concatenated"),
     ("ROCPercent", "Val_ROC_Perc", null, "Number", 1, 0, null, "Not synced"),
     ("ROCStep", "Catg_ROC_Step", "Catg_ROC_Step", "Text", 1, 0, null, null),
        ("SchedActNO", "Tag_Sch_ActNo", "Tag_Sch_ActNo", "Text", 1, 0, null, null),
       ("SecondActno", "Sch_Actno", "Sch_Actno", "Text", 1, 0, null, null),
    ("SecondDwgNO", "Dwg_SecondaryDrawingNO", "Dwg_SecondaryDrawingNO", "Text", 1, 0, null, null),
          ("Service", "Tag_Service", "Tag_Service", "Text", 1, 0, null, null),
       ("ShopField", "Tag_ShopField", "Tag_ShopField", "Text", 1, 0, null, null),
   ("ShtNO", "Dwg_ShtNo", "Dwg_ShtNo", "Text", 1, 0, null, null),
       ("Start", "Sch_Start", "Sch_Start", "DateTime", 1, 0, null, "Future: calculated"),
         ("Status", "Sch_Status", "Sch_Status", "Text", 0, 1, "PercentEntry == 0 ? 'Not Started' : PercentEntry >= 100 ? 'Complete' : 'In Progress'", "Calculated"),
  ("SubArea", "Tag_SubArea", "Tag_SubArea", "Text", 1, 0, null, null),
        ("System", "Tag_System", "Tag_System", "Text", 1, 0, null, null),
                ("SystemNO", "Tag_SystemNo", null, "Text", 1, 0, null, "Not synced"),
     ("TagNO", "Tag_TagNo", "Tag_TagNo", "Text", 1, 0, null, null),
       ("UDF1", "UDFOne", "UDF1", "Text", 1, 0, null, null),
 ("UDF10", "UDFTen", "UDF10", "Text", 1, 0, null, null),
           ("UDF14", "UDFFourteen", "UDF14", "Text", 1, 0, null, null),
     ("UDF15", "UDFFifteen", "UDF15", "Text", 1, 0, null, null),
   ("UDF16", "UDFSixteen", "UDF16", "Text", 1, 0, null, null),
        ("UDF17", "UDFSeventeen", "UDF17", "Text", 1, 0, null, null),
     ("UDF18", "UDFEighteen", "UDF18", "Text", 1, 0, null, null),
           ("UDF2", "UDFTwo", "UDF2", "Text", 1, 0, null, null),
    ("UDF20", "UDFTwenty", "UDF20", "Text", 1, 0, null, null),
    ("UDF3", "UDFThree", "UDF3", "Text", 1, 0, null, null),
   ("UDF4", "UDFFour", "UDF4", "Text", 1, 0, null, null),
      ("UDF5", "UDFFive", "UDF5", "Text", 1, 0, null, null),
           ("UDF6", "UDFSix", "UDF6", "Text", 1, 0, null, null),
                ("UDF7", "UDFSeven", "UDF7", "Text", 1, 0, null, null),
   ("UDF8", "UDFEight", "UDF8", "Text", 1, 0, null, null),
            ("UDF9", "UDFNine", "UDF9", "Text", 1, 0, null, null),
      ("UniqueID", "UDFNineteen", "UDF19", "Text", 0, 0, null, "Auto-generate if null on import"),
 ("UOM", "Val_UOM", "Val_UOM", "Text", 1, 0, null, null),
     ("UserID", null, "UserID", "Text", 0, 1, null, "Created on Azure upload"),
 ("WeekEndDate", "Val_TimeStamp", "Val_TimeStamp", "DateTime", 0, 1, null, "Set when submitted"),
       ("WorkPackage", "Tag_WorkPackage", "Tag_WorkPackage", "Text", 1, 0, null, null),
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
        }

        /// <summary>
        /// Add test users to the database (for development/testing)
        /// </summary>
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
       INSERT OR IGNORE INTO Users (Username, FullName, Email, PhoneNumber) 
    VALUES (@username, @fullName, @email, '')";
  command.Parameters.AddWithValue("@username", user.Username);
           command.Parameters.AddWithValue("@fullName", user.FullName);
            command.Parameters.AddWithValue("@email", user.Email);
          command.ExecuteNonQuery();
 }
        }
       catch (Exception ex)
     {
      // TODO: Add proper logging when logging system is implemented
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