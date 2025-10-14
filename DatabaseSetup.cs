using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace VANTAGE
{
    public class DatabaseSetup
    {
        public static string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VANTAGE",
            "VANTAGE_Local.db"
        );

        public static void InitializeDatabase()
        {
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(DbPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create/open database
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
                        PhoneNumber TEXT
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

                    -- Activities table (exact Azure schema match)
                    CREATE TABLE IF NOT EXISTS Activities (
                        ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                        HexNO INTEGER DEFAULT 0,
                        
                        -- Categories
                        Catg_ComponentType TEXT,
                        Catg_PhaseCategory TEXT,
                        Catg_ROC_Step TEXT,
                        
                        -- Drawings
                        Dwg_PrimeDrawingNO TEXT,
                        Dwg_RevisionNo TEXT,
                        Dwg_SecondaryDrawingNO TEXT,
                        Dwg_ShtNo TEXT,
                        
                        -- Notes
                        Notes_Comments TEXT,
                        
                        -- Schedule
                        Sch_Actno TEXT,
                        Sch_Start TEXT,
                        Sch_Finish TEXT,
                        Sch_Status TEXT,
                        
                        -- Tags
                        Tag_Aux1 TEXT,
                        Tag_Aux2 TEXT,
                        Tag_Aux3 TEXT,
                        Tag_Area TEXT,
                        Tag_CONo TEXT,
                        Tag_Descriptions TEXT,
                        Tag_EqmtNo TEXT,
                        Tag_Estimator TEXT,
                        Tag_Insulation_Typ TEXT,
                        Tag_LineNo TEXT,
                        Tag_Matl_Spec TEXT,
                        Tag_Phase_Code TEXT,
                        Tag_Paint_Code TEXT,
                        Tag_Pipe_Grade TEXT,
                        Tag_ProjectID TEXT,
                        Tag_RFINo TEXT,
                        Tag_Sch_ActNo TEXT,
                        Tag_Service TEXT,
                        Tag_ShopField TEXT,
                        Tag_SubArea TEXT,
                        Tag_System TEXT,
                        Tag_SystemNo TEXT,
                        Tag_TagNo TEXT,
                        Tag_Tracing TEXT,
                        Tag_WorkPackage TEXT,
                        Tag_XRAY REAL,
                        
                        -- Trigger
                        Trg_DateTrigger INTEGER,
                        
                        -- Custom Fields (UDF1-UDF20)
                        UDFOne TEXT,
                        UDFTwo TEXT,
                        UDFThree TEXT,
                        UDFFour TEXT,
                        UDFFive TEXT,
                        UDFSix TEXT,
                        UDFSeven INTEGER,
                        UDFEight TEXT,
                        UDFNine TEXT,
                        UDFTen TEXT,
                        UDFEleven TEXT,
                        UDFTwelve TEXT,
                        UDFThirteen TEXT,
                        UDFFourteen TEXT,
                        UDFFifteen TEXT,
                        UDFSixteen TEXT,
                        UDFSeventeen TEXT,
                        UDFEighteen TEXT,
                        UDFNineteen TEXT UNIQUE NOT NULL,
                        UDFTwenty TEXT,
                        
                        -- Values (user-editable)
                        Val_Base_Unit REAL DEFAULT 0,
                        Val_BudgetedHours_Ind REAL DEFAULT 0,
                        Val_BudgetedHours_Group REAL DEFAULT 0,
                        Val_BudgetedHours_ROC REAL DEFAULT 0,
                        Val_EarnedHours_ROC INTEGER DEFAULT 0,
                        Val_EarnedQty REAL DEFAULT 0,
                        Val_Perc_Complete REAL DEFAULT 0,
                        Val_Quantity REAL DEFAULT 0,
                        Val_UOM TEXT,
                        
                        -- Values (calculated)
                        Val_EarnedHours_Ind REAL DEFAULT 0,
                        Val_Earn_Qty REAL DEFAULT 0,
                        Val_Percent_Earned REAL DEFAULT 0,
                        
                        -- Equipment Quantity
                        Val_EQ_QTY REAL DEFAULT 0,
                        Val_EQ_UOM TEXT,
                        
                        -- ROC
                        Tag_ROC_ID INTEGER DEFAULT 0,
                        LookUP_ROC_ID TEXT,
                        Val_ROC_Perc REAL DEFAULT 0,
                        Val_ROC_BudgetQty REAL DEFAULT 0,
                        
                        -- Pipe
                        Val_Pipe_Size1 REAL DEFAULT 0,
                        Val_Pipe_Size2 REAL DEFAULT 0,
                        
                        -- Previous values
                        Val_Prev_Earned_Hours REAL DEFAULT 0,
                        Val_Prev_Earned_Qty REAL DEFAULT 0,
                        
                        -- Timestamps
                        Val_TimeStamp TEXT,
                        
                        -- Client values
                        VAL_Client_EQ_QTY_BDG REAL DEFAULT 0,
                        VAL_UDF_Two REAL DEFAULT 0,
                        VAL_UDF_Three REAL DEFAULT 0,
                        VAL_Client_Earned_EQ_QTY REAL DEFAULT 0
                    );

                    -- Indexes for performance
                    CREATE INDEX IF NOT EXISTS idx_tag_roc_id ON Activities(Tag_ROC_ID);
                    CREATE INDEX IF NOT EXISTS idx_tag_project ON Activities(Tag_ProjectID);
                    CREATE INDEX IF NOT EXISTS idx_tag_area ON Activities(Tag_Area);
                    CREATE INDEX IF NOT EXISTS idx_assigned_to ON Activities(UDFEleven);
                    CREATE INDEX IF NOT EXISTS idx_udf_nineteen ON Activities(UDFNineteen);
                ";

                command.ExecuteNonQuery();

                System.Diagnostics.Debug.WriteLine($"Database initialized at: {DbPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
                throw;
            }
        }

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={DbPath}");
        }
    }
}