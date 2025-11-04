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
                        IsAdmin INTEGER DEFAULT0,
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
                        Aux1 TEXT DEFAULT '',
                        Aux2 TEXT DEFAULT '',
                        Aux3 TEXT DEFAULT '',
                        BaseUnit REAL DEFAULT0,
                        BudgetMHs REAL DEFAULT0,
                        BudgetHoursGroup REAL DEFAULT0,
                        BudgetHoursROC REAL DEFAULT0,
                        ChgOrdNO TEXT DEFAULT '',
                        ClientBudget REAL DEFAULT0,
                        ClientCustom3 REAL DEFAULT0,
                        ClientEquivEarnQTY TEXT DEFAULT '',
                        ClientEquivQty REAL DEFAULT0,
                        CompType TEXT DEFAULT '',
                        CreatedBy TEXT DEFAULT '',
                        DateTrigger INTEGER DEFAULT0,
                        Description TEXT DEFAULT '',
                        DwgNO TEXT DEFAULT '',
                        EarnedMHsRoc REAL DEFAULT0,
                        EarnQtyEntry REAL DEFAULT0,
                        EqmtNO TEXT DEFAULT '',
                        EquivQTY TEXT DEFAULT '',
                        EquivUOM TEXT DEFAULT '',
                        Estimator TEXT DEFAULT '',
                        Finish TEXT DEFAULT '',
                        HexNO INTEGER DEFAULT0,
                        HtTrace TEXT DEFAULT '',
                        InsulType TEXT DEFAULT '',
                        LastModifiedBy TEXT DEFAULT '',
                        LineNO TEXT DEFAULT '',
                        MtrlSpec TEXT DEFAULT '',
                        Notes TEXT DEFAULT '',
                        PaintCode TEXT DEFAULT '',
                        PercentEntry REAL DEFAULT0,
                        PhaseCategory TEXT DEFAULT '',
                        PhaseCode TEXT DEFAULT '',
                        PipeGrade TEXT DEFAULT '',
                        PipeSize1 REAL DEFAULT0,
                        PipeSize2 REAL DEFAULT0,
                        PrevEarnMHs REAL DEFAULT0,
                        PrevEarnQTY REAL DEFAULT0,
                        ProjectID TEXT DEFAULT '',
                        Quantity REAL DEFAULT0,
                        RevNO TEXT DEFAULT '',
                        RFINO TEXT DEFAULT '',
                        ROCBudgetQTY REAL DEFAULT0,
                        ROCID REAL DEFAULT0,
                        ROCPercent REAL DEFAULT0,
                        ROCStep TEXT DEFAULT '',
                        SchedActNO TEXT DEFAULT '',
                        SchFinish TEXT DEFAULT '',
                        SchStart TEXT DEFAULT '',
                        SecondActno TEXT DEFAULT '',
                        SecondDwgNO TEXT DEFAULT '',
                        Service TEXT DEFAULT '',
                        ShopField TEXT DEFAULT '',
                        ShtNO TEXT DEFAULT '',
                        Start TEXT DEFAULT '',
                        Status TEXT DEFAULT '',
                        SubArea TEXT DEFAULT '',
                        System TEXT DEFAULT '',
                        SystemNO TEXT DEFAULT '',
                        TagNO TEXT DEFAULT '',
                        UDF1 TEXT DEFAULT '',
                        UDF10 TEXT DEFAULT '',
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
                        WorkPackage TEXT DEFAULT '',
                        XRay REAL DEFAULT0
                    );

                    -- ColumnMappings table (Master mappings for all external systems)
                    CREATE TABLE IF NOT EXISTS ColumnMappings (
                        MappingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        ColumnName TEXT NOT NULL,
                        OldVantageName TEXT,
                        AzureName TEXT,
                        DataType TEXT,
                        IsEditable INTEGER DEFAULT1,
                        IsCalculated INTEGER DEFAULT0,
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
        /// Seed ColumnMappings table from CSV data (hardcoded from ColumnNameComparisonForAiModel.csv)
        /// This data is embedded in code for deployment - no CSV file needed at runtime
        /// </summary>
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
   var mappings = new[]
            {
  ("Area", "Tag_Area", "Tag_Area", "Short Text", 1, 0, null, null),
         ("AssignedTo", "UDFEleven", "UDF11", "Short Text", 1, 0, null, null),
        ("Aux1", "Tag_Aux1", "Tag_Aux1", "Short Text", 1, 0, null, null),
             ("Aux2", "Tag_Aux2", "Tag_Aux2", "Short Text", 1, 0, null, null),
    ("Aux3", "Tag_Aux3", "Tag_Aux3", "Short Text", 1, 0, null, null),
    ("AzureUploadDate", null, "Timestamp", "Date/Time", 0, 0, null, "When user submits to Azure - official date used in Power BI dashboard"),
        ("BaseUnit", "Val_Base_Unit", "Val_Base_Unit", "Number", 1, 0, null, null),
        ("BudgetHoursGroup", "Val_BudgetedHours_Group", "Val_BudgetedHours_Group", "Number", 1, 0, null, null),
         ("BudgetHoursROC", "Val_BudgetedHours_ROC", "Val_BudgetedHours_ROC", "Number", 1, 0, null, null),
          ("BudgetMHs", "Val_BudgetedHours_Ind", "Val_BudgetedHours_Ind", "Number", 1, 0, null, null),
                ("ChgOrdNO", "Tag_CONo", "Tag_CONo", "Short Text", 1, 0, null, null),
    ("ClientBudget", "VAL_UDF_Two", "VAL_UDF_Two", "Number", 1, 0, null, null),
          ("ClientCustom3", "VAL_UDF_Three", "VAL_UDF_Three", "Number", 1, 0, null, null),
    ("ClientEquivEarnQTY", "VAL_Client_Earned_EQ-QTY", "VAL_Client_Earned_EQ-QTY", "Short Text", 1, 0, null, null),
              ("ClientEquivQty", "VAL_Client_EQ-QTY_BDG", "Val_Client_Eq_Qty_Bdg", "Number", 1, 0, null, null),
                ("CompType", "Catg_ComponentType", "Catg_ComponentType", "Short Text", 1, 0, null, null),
      ("CreatedBy", "UDFThirteen", "UDF13", "Short Text", 1, 0, null, null),
                ("DateTrigger", "Trg_DateTrigger", null, "Number", 1, 0, null, null),
       ("Description", "Tag_Descriptions", "Tag_Descriptions", "Short Text", 1, 0, null, null),
          ("DwgNO", "Dwg_PrimeDrawingNO", "Dwg_PrimeDrawingNO", "Short Text", 1, 0, null, null),
         ("EarnedMHsRoc", "Val_EarnedHours_ROC", null, "Number", 1, 0, null, null),
         ("EarnedQtyCalc", "Val_Earn_Qty", null, "Number", 0, 1, "PercentEntry", null),
  ("EarnMHsCalc", "Val_EarnedHours_Ind", "Val_EarnedHours_Ind", "Number", 0, 1, "PercentEntry*BudgetMHs", null),
       ("EarnQtyEntry", "Val_EarnedQty", "Val_EarnedQty", "Number", 1, 0, null, null),
("EqmtNO", "Tag_EqmtNo", "Tag_EqmtNo", "Short Text", 1, 0, null, null),
       ("EquivQTY", "Val_EQ-QTY", "Val_EQ-QTY", "Short Text", 1, 0, null, null),
    ("EquivUOM", "Val_EQ_UOM", "Val_EQ_UOM", "Short Text", 1, 0, null, null),
     ("Estimator", "Tag_Estimator", "Tag_Estimator", "Short Text", 1, 0, null, null),
   ("SchFinish", "Sch_Finish", null, "Date/Time", 1, 0, null, "will make calculated later when schedule module is developed"),
      ("HexNO", "HexNO", "HexNO", "Number", 1, 0, null, null),
 ("HtTrace", "Tag_Tracing", "Tag_Tracing", "Short Text", 1, 0, null, null),
        ("InsulType", "Tag_Insulation_Typ", "Tag_Insulation_Typ", "Short Text", 1, 0, null, null),
         ("LastModifiedBy", "UDFTwelve", "UDF12", "Short Text", 1, 0, null, null),
                ("LineNO", "Tag_LineNo", "Tag_LineNo", "Short Text", 1, 0, null, null),
           ("MtrlSpec", "Tag_Matl_Spec", "Tag_Matl_Spec", "Short Text", 1, 0, null, null),
      ("Notes", "Notes_Comments", "Notes_Comments", "Long Text", 1, 0, null, null),
         ("PaintCode", "Tag_Paint_Code", "Tag_Paint_Code", "Short Text", 1, 0, null, null),
      ("PercentCompleteCalc", "Val_Percent_Earned", null, "Number", 0, 1, "PercentEntry", null),
         ("PercentEntry", "Val_Perc_Complete", "Val_Perc_Complete", "Number", 1, 0, null, "format 0-1 in import, export. Format 0-100% in datagrid display"),
           ("PhaseCategory", "Catg_PhaseCategory", "Catg_PhaseCategory", "Short Text", 1, 0, null, null),
    ("PhaseCode", "Tag_Phase Code", "Tag_PhaseCode", "Short Text", 1, 0, null, null),
           ("PipeGrade", "Tag_Pipe_Grade", "Tag_Pipe_Grade", "Short Text", 1, 0, null, null),
                ("PipeSize1", "Val_Pipe_Size1", "Val_Pipe_Size1", "Number", 1, 0, null, null),
  ("PipeSize2", "Val_Pipe_Size2", "Val_Pipe_Size2", "Number", 1, 0, null, null),
     ("PrevEarnMHs", "Val_Prev_Earned_Hours", null, "Number", 1, 0, null, null),
           ("PrevEarnQTY", "Val_Prev_Earned_Qty", null, "Number", 1, 0, null, null),
("ProgDate", null, "Val_ProgDate", "Date/Time", 0, 1, null, "timestamp when user clicks to submit progress to local db"),
             ("ProjectID", "Tag_ProjectID", "Tag_ProjectID", "Short Text", 1, 0, null, null),
          ("Quantity", "Val_Quantity", "Val_Quantity", "Number", 1, 0, null, null),
             ("RevNO", "Dwg_RevisionNo", "Dwg_RevisionNo", "Short Text", 1, 0, null, null),
      ("RFINO", "Tag_RFINo", "Tag_RFINo", "Short Text", 1, 0, null, null),
       ("ROCBudgetQTY", "Val_ROC_BudgetQty", "Val_ROC_BudgetQty", "Number", 1, 0, null, "needs to be same as Quantity on export"),
     ("ROCID", "Tag_ROC_ID", null, "Number", 1, 0, null, null),
      ("ROCLookupID", "LookUP_ROC_ID", null, "Short Text", 0, 1, "ProjectID & \"|\" & CompType & \"|\" & PhaseCatagory & \"|\" & ROCStep", null),
    ("ROCPercent", "Val_ROC_Perc", null, "Number", 1, 0, null, null),
        ("ROCStep", "Catg_ROC_Step", "Catg_ROC_Step", "Short Text", 1, 0, null, null),
 ("SchedActNO", "Tag_Sch_ActNo", "Tag_Sch_ActNo", "Short Text", 1, 0, null, null),
  ("SecondActno", "Sch_Actno", "Sch_Actno", "Short Text", 1, 0, null, null),
 ("SecondDwgNO", "Dwg_SecondaryDrawingNO", "Dwg_SecondaryDrawingNO", "Short Text", 1, 0, null, null),
  ("Service", "Tag_Service", "Tag_Service", "Short Text", 1, 0, null, null),
            ("ShopField", "Tag_ShopField", "Tag_ShopField", "Short Text", 1, 0, null, null),
                ("ShtNO", "Dwg_ShtNo", "Dwg_ShtNo", "Short Text", 1, 0, null, null),
  ("SchStart", "Sch_Start", "Sch_Start", "Date/Time", 1, 0, null, "will make calculated later when schedule module is developed"),
      ("Status", "Sch_Status", "Sch_Status", "Short Text", 0, 1, "PercentEntry = 0: \"Not Started\", PercentEntry >0, <100: \"In Progress\", PercentEntry = 100: \"Complete\"", "Not Started, In Progress, Complete base on PercentEntry"),
    ("SubArea", "Tag_SubArea", "Tag_SubArea", "Short Text", 1, 0, null, null),
     ("System", "Tag_System", "Tag_System", "Short Text", 1, 0, null, null),
         ("SystemNO", "Tag_SystemNo", null, "Short Text", 1, 0, null, null),
                ("TagNO", "Tag_TagNo", "Tag_TagNo", "Short Text", 1, 0, null, null),
             ("UDF1", "UDFOne", "UDF1", "Short Text", 1, 0, null, null),
("UDF10", "UDFTen", "UDF10", "Short Text", 1, 0, null, null),
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
     ("UserID", null, "UserID", "Short Text", 0, 0, null, "created upon upload, sync to Azure (current username)"),
                ("WeekEndDate", "Val_TimeStamp", "Val_TimeStamp", "Date/Time", 0, 1, null, "Week Ending Date set when user submits to Local or Azure"),
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