using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    
    /// Import activities from Excel files with OldVantage column names
    /// Translates to NewVantage column names for database storage
    
    public static class ExcelImporter
    {
        
        /// Import activities from Excel file
        
        /// <param name="filePath">Path to Excel file with OldVantage column names</param>
        /// <param name="replaceMode">True = replace all existing, False = combine/add</param>
        /// <returns>Number of records imported</returns>
        public static int ImportActivities(string filePath, bool replaceMode)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Excel file not found: {filePath}");
                }

                // Open Excel workbook
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Sheet1")
                    ?? workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    throw new Exception("No worksheets found in Excel file");
                }

                // Get header row to map columns (OldVantage names → NewVantage names)
                var headerRow = worksheet.Row(1);
                var columnMap = BuildColumnMap(headerRow);

                // Read all Activity objects from Excel
                var activities = ReadActivitiesFromExcel(worksheet, columnMap);

                // Import to database
                int imported = ImportToDatabase(activities, replaceMode);

                System.Diagnostics.Debug.WriteLine($"✓ Imported {imported} activities from {filePath}");
                return imported;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Import error: {ex.Message}");
                throw;
            }
        }

        
        /// Build column mapping: Excel column number → NewVantage property name
        /// Translates OldVantage column headers to NewVantage property names
        
        private static Dictionary<int, string> BuildColumnMap(IXLRow headerRow)
        {
            var columnMap = new Dictionary<int, string>();

            // Calculated fields to skip (app will recalculate these)
            var calculatedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Status",
                "EarnMHsCalc",
                "EarnedQtyCalc",
                "PercentCompleteCalc",
                "ROCLookupID",
                "ClientEquivEarnQTY",
                "WeekEndDate",
                "ProgDate"
            };

            for (int colNum = 1; colNum <= headerRow.LastCellUsed()?.Address.ColumnNumber; colNum++)
            {
                var cell = headerRow.Cell(colNum);
                string oldVantageHeader = cell.GetString().Trim();

                if (string.IsNullOrEmpty(oldVantageHeader))
                    continue;

                string newVantageName = ColumnMapper.GetColumnNameFromOldVantage(oldVantageHeader);

                // DEBUG: Log UDF mappings specifically
                if (oldVantageHeader.StartsWith("UDF", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[GENERIC] Excel[{oldVantageHeader}] → Property[{newVantageName}]");
                }

                // Skip calculated fields
                if (calculatedFields.Contains(newVantageName))
                {
                    System.Diagnostics.Debug.WriteLine($"  Skipping calculated field: {oldVantageHeader} → {newVantageName}");
                    continue;
                }

                columnMap[colNum] = newVantageName;
            }

            System.Diagnostics.Debug.WriteLine($"✓ Mapped {columnMap.Count} columns from Excel");
            return columnMap;
        }

        
        /// Read activities from Excel worksheet
        
        private static List<Activity> ReadActivitiesFromExcel(IXLWorksheet worksheet, Dictionary<int, string> columnMap)
        {
            var activities = new List<Activity>();

            // Find last row with data
            int lastRow = FindLastDataRow(worksheet);

            // UniqueID generation settings
            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            var userSuffix = App.CurrentUser?.Username?.Length >= 3
                ? App.CurrentUser.Username.Substring(App.CurrentUser.Username.Length - 3).ToLower()
                : "usr";
            int sequence = 1;

            for (int rowNum = 2, rowCount = 0; rowNum <= lastRow; rowNum++, rowCount++)
            {
                var row = worksheet.Row(rowNum);

                // Check if row has any data
                if (!RowHasData(row, columnMap))
                    continue;

                var activity = new Activity();

                // Set property values from Excel
                foreach (var mapping in columnMap)
                {
                    int excelColNum = mapping.Key;
                    string propertyName = mapping.Value;

                    var cell = row.Cell(excelColNum);
                    SetPropertyValue(activity, propertyName, cell);
                }

                // Handle UniqueID auto-generation if missing
                if (string.IsNullOrWhiteSpace(activity.UniqueID))
                {
                    activity.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
                    sequence++;
                    System.Diagnostics.Debug.WriteLine($"  Generated UniqueID: {activity.UniqueID}");
                }

                // Handle AssignedTo default
                if (string.IsNullOrWhiteSpace(activity.AssignedTo))
                {
                    activity.AssignedTo = "Unassigned";
                }

                activities.Add(activity);

                // Limit to 1000 activities to avoid high memory usage
                if (activities.Count >= 1000)
                {
                    System.Diagnostics.Debug.WriteLine($"  Read {activities.Count} activities, pausing for memory");
                    System.Threading.Thread.Sleep(1000);
                }
            }

            System.Diagnostics.Debug.WriteLine($"✓ Read {activities.Count} activities from Excel");
            return activities;
        }

        
        /// Find the last row with actual data
        
        private static int FindLastDataRow(IXLWorksheet worksheet)
        {
            int lastRow = 1;
            for (int rowNum = 2; rowNum <= 100000; rowNum++)
            {
                var cell = worksheet.Cell(rowNum, 1);
                if (!cell.IsEmpty())
                {
                    lastRow = rowNum;
                }
                else if (rowNum > lastRow + 100) // 100 empty rows = end of data
                {
                    break;
                }
            }
            return lastRow;
        }

        
        /// Check if row has any data in mapped columns
        
        private static bool RowHasData(IXLRow row, Dictionary<int, string> columnMap)
        {
            foreach (var mapping in columnMap)
            {
                var cell = row.Cell(mapping.Key);
                if (!cell.IsEmpty())
                    return true;
            }
            return false;
        }

        
        /// Set Activity property value from Excel cell
        
        private static void SetPropertyValue(Activity activity, string propertyName, IXLCell cell)
        {
            var property = typeof(Activity).GetProperty(propertyName);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            if (cell.IsEmpty())
            {
                // Set defaults for empty cells
                if (property.PropertyType == typeof(string))
                    property.SetValue(activity, "");
                else if (property.PropertyType == typeof(double))
                    property.SetValue(activity, 0.0);
                else if (property.PropertyType == typeof(int))
                    property.SetValue(activity, 0);
                return;
            }

            try
            {
                // Special handling for PercentEntry: convert 0-1 decimal to 0-100 percentage
                if (propertyName == "PercentEntry")
                {
                    double value = cell.GetDouble();

                    // OLD EXCEL FORMAT: Values are 0-1 (e.g., 0.755 = 75.5%)
                    // Convert to 0-100 format for storage
                    if (value >= 0 && value <= 1.0)
                    {
                        activity.PercentEntry = value * 100.0; // 0.755 → 75.5
                    }
                    // If value is > 1, assume it's already a percentage (0-100)
                    else if (value > 1 && value <= 100)
                    {
                        activity.PercentEntry = value;
                    }
                    else
                    {
                        // Out of range, set to 0
                        activity.PercentEntry = 0;
                    }
                    return;
                }

                // NEW: Handle SchStart and SchFinish separately
                if (propertyName == "SchStart" || propertyName == "SchFinish")
                {
                    if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime)
                        property.SetValue(activity, cell.GetDateTime());
                    else if (DateTime.TryParse(cell.GetString(), out var dt))
                        property.SetValue(activity, dt);
                    else
                        property.SetValue(activity, null);
                    return;
                }

                // DEBUG: Log UDF assignments
                if (propertyName.StartsWith("UDF", StringComparison.OrdinalIgnoreCase))
                {
                    string cellValue = cell.GetString();
                    System.Diagnostics.Debug.WriteLine($"[GENERIC] SETTING {propertyName} = \"{cellValue}\"");
                }

                // Regular property setting based on type
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(activity, cell.GetString());
                }
                else if (property.PropertyType == typeof(double))
                {
                    property.SetValue(activity, cell.GetDouble());
                }
                else if (property.PropertyType == typeof(int))
                {
                    property.SetValue(activity, (int)cell.GetDouble());
                }
                else if (property.PropertyType == typeof(DateTime))
                {
                    property.SetValue(activity, cell.GetDateTime());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  Warning: Failed to set {propertyName} = {cell.Value}: {ex.Message}");
                // Set default value on error
                if (property.PropertyType == typeof(string))
                    property.SetValue(activity, "");
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
                    property.SetValue(activity, 0);
            }
        }

        
        /// Import activities to database using NewVantage column names
        
        private static int ImportToDatabase(List<Activity> activities, bool replaceMode)
        {
            using var connection = DatabaseSetup.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // If replace mode, delete all existing activities
                if (replaceMode)
                {
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Activities";
                    int deleted = deleteCommand.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"✓ Deleted {deleted} existing activities (replace mode)");
                }

                int imported = 0;
                int skipped = 0;

                foreach (var activity in activities)
                {
                    // Validate UniqueID
                    if (string.IsNullOrWhiteSpace(activity.UniqueID))
                    {
                        System.Diagnostics.Debug.WriteLine($"  ✗ Skipping activity: missing UniqueID");
                        skipped++;
                        continue;
                    }

                    // In combine mode, check if activity already exists
                    if (!replaceMode)
                    {
                        var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = "SELECT COUNT(*) FROM Activities WHERE UniqueID = @id";
                        checkCommand.Parameters.AddWithValue("@id", activity.UniqueID);
                        var exists = (long)checkCommand.ExecuteScalar() > 0;

                        if (exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Skipping duplicate: {activity.UniqueID}");
                            skipped++;
                            continue;
                        }
                    }

                    // Insert activity using NewVantage column names
                    InsertActivity(connection, activity);
                    imported++;
                }

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine($"✓ Import complete: {imported} imported, {skipped} skipped");
                return imported;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"✗ Import failed, transaction rolled back: {ex.Message}");
                throw;
            }
        }

        
        /// Insert a single activity into the database
        
        private static void InsertActivity(SqliteConnection connection, Activity activity)
        {
            var command = connection.CreateCommand();

            // DEBUG: Log UDF values before INSERT
            System.Diagnostics.Debug.WriteLine($"📝 INSERT Activity: UniqueID={activity.UniqueID}, UDF1=\"{activity.UDF1}\", UDF2=\"{activity.UDF2}\"");

            // Build INSERT with NewVantage column names
            command.CommandText = @"
       INSERT INTO Activities (
        HexNO, ProjectID, Description, UniqueID,
      Area, SubArea, System, SystemNO,
   CompType, PhaseCategory, ROCStep,
         AssignedTo, CreatedBy, LastModifiedBy,
              PercentEntry, Quantity, EarnQtyEntry, UOM,
           BudgetMHs, BudgetHoursGroup, BudgetHoursROC, BaseUnit, EarnedMHsRoc,
     ROCID, ROCPercent, ROCBudgetQTY,
       DwgNO, RevNO, SecondDwgNO, ShtNO,
              TagNO, WorkPackage, PhaseCode, Service, ShopField, SchedActNO, SecondActno,
       EqmtNO, LineNO, ChgOrdNO,
 MtrlSpec, PipeGrade, PaintCode, InsulType, HtTrace,
   Aux1, Aux2, Aux3, Estimator, RFINO, XRay,
    EquivQTY, EquivUOM,
       ClientEquivQty, ClientBudget, ClientCustom3,
       PrevEarnMHs, PrevEarnQTY,
     SchStart, SchFinish, DateTrigger, Notes,
    UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
    UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
          PipeSize1, PipeSize2
     ) VALUES (
    @HexNO, @ProjectID, @Description, @UniqueID,
    @Area, @SubArea, @System, @SystemNO,
  @CompType, @PhaseCategory, @ROCStep,
                    @AssignedTo, @CreatedBy, @LastModifiedBy,
                    @PercentEntry, @Quantity, @EarnQtyEntry, @UOM,
    @BudgetMHs, @BudgetHoursGroup, @BudgetHoursROC, @BaseUnit, @EarnedMHsRoc,
        @ROCID, @ROCPercent, @ROCBudgetQTY,
     @DwgNO, @RevNO, @SecondDwgNO, @ShtNO,
         @TagNO, @WorkPackage, @PhaseCode, @Service, @ShopField, @SchedActNO, @SecondActno,
         @EqmtNO, @LineNO, @ChgOrdNO,
 @MtrlSpec, @PipeGrade, @PaintCode, @InsulType, @HtTrace,
 @Aux1, @Aux2, @Aux3, @Estimator, @RFINO, @XRay,
    @EquivQTY, @EquivUOM,
     @ClientEquivQty, @ClientBudget, @ClientCustom3,
       @PrevEarnMHs, @PrevEarnQTY,
 @SchStart, @SchFinish, @DateTrigger, @Notes,
         @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9, @UDF10,
         @UDF14, @UDF15, @UDF16, @UDF17, @UDF18, @UDF20,
     @PipeSize1, @PipeSize2
      )";

            // Add all parameters
            command.Parameters.AddWithValue("@HexNO", activity.HexNO);
            command.Parameters.AddWithValue("@ProjectID", activity.ProjectID ?? "");
            command.Parameters.AddWithValue("@Description", activity.Description ?? "");
            command.Parameters.AddWithValue("@UniqueID", activity.UniqueID);
            command.Parameters.AddWithValue("@Area", activity.Area ?? "");
            command.Parameters.AddWithValue("@SubArea", activity.SubArea ?? "");
            command.Parameters.AddWithValue("@System", activity.System ?? "");
            command.Parameters.AddWithValue("@SystemNO", activity.SystemNO ?? "");
            command.Parameters.AddWithValue("@CompType", activity.CompType ?? "");
            command.Parameters.AddWithValue("@PhaseCategory", activity.PhaseCategory ?? "");
            command.Parameters.AddWithValue("@ROCStep", activity.ROCStep ?? "");
            command.Parameters.AddWithValue("@AssignedTo", activity.AssignedTo ?? "Unassigned");
            command.Parameters.AddWithValue("@CreatedBy", activity.CreatedBy ?? "");
            command.Parameters.AddWithValue("@LastModifiedBy", activity.LastModifiedBy ?? "");
            command.Parameters.AddWithValue("@PercentEntry", activity.PercentEntry); // Already 0-100
            command.Parameters.AddWithValue("@Quantity", activity.Quantity);
            command.Parameters.AddWithValue("@EarnQtyEntry", activity.EarnQtyEntry);
            command.Parameters.AddWithValue("@UOM", activity.UOM ?? "");
            command.Parameters.AddWithValue("@BudgetMHs", activity.BudgetMHs);
            command.Parameters.AddWithValue("@BudgetHoursGroup", activity.BudgetHoursGroup);
            command.Parameters.AddWithValue("@BudgetHoursROC", activity.BudgetHoursROC);
            command.Parameters.AddWithValue("@BaseUnit", activity.BaseUnit);
            command.Parameters.AddWithValue("@EarnedMHsRoc", activity.EarnedMHsRoc);
            command.Parameters.AddWithValue("@ROCID", activity.ROCID);
            command.Parameters.AddWithValue("@ROCPercent", activity.ROCPercent);
            command.Parameters.AddWithValue("@ROCBudgetQTY", activity.ROCBudgetQTY);
            command.Parameters.AddWithValue("@DwgNO", activity.DwgNO ?? "");
            command.Parameters.AddWithValue("@RevNO", activity.RevNO ?? "");
            command.Parameters.AddWithValue("@SecondDwgNO", activity.SecondDwgNO ?? "");
            command.Parameters.AddWithValue("@ShtNO", activity.ShtNO ?? "");
            command.Parameters.AddWithValue("@TagNO", activity.TagNO ?? "");
            command.Parameters.AddWithValue("@WorkPackage", activity.WorkPackage ?? "");
            command.Parameters.AddWithValue("@PhaseCode", activity.PhaseCode ?? "");
            command.Parameters.AddWithValue("@Service", activity.Service ?? "");
            command.Parameters.AddWithValue("@ShopField", activity.ShopField ?? "");
            command.Parameters.AddWithValue("@SchedActNO", activity.SchedActNO ?? "");
            command.Parameters.AddWithValue("@SecondActno", activity.SecondActno ?? "");
            command.Parameters.AddWithValue("@EqmtNO", activity.EqmtNO ?? "");
            command.Parameters.AddWithValue("@LineNO", activity.LineNO ?? "");
            command.Parameters.AddWithValue("@ChgOrdNO", activity.ChgOrdNO ?? "");
            command.Parameters.AddWithValue("@MtrlSpec", activity.MtrlSpec ?? "");
            command.Parameters.AddWithValue("@PipeGrade", activity.PipeGrade ?? "");
            command.Parameters.AddWithValue("@PaintCode", activity.PaintCode ?? "");
            command.Parameters.AddWithValue("@InsulType", activity.InsulType ?? "");
            command.Parameters.AddWithValue("@HtTrace", activity.HtTrace ?? "");
            command.Parameters.AddWithValue("@Aux1", activity.Aux1 ?? "");
            command.Parameters.AddWithValue("@Aux2", activity.Aux2 ?? "");
            command.Parameters.AddWithValue("@Aux3", activity.Aux3 ?? "");
            command.Parameters.AddWithValue("@Estimator", activity.Estimator ?? "");
            command.Parameters.AddWithValue("@RFINO", activity.RFINO ?? "");
            command.Parameters.AddWithValue("@XRay", activity.XRay);
            command.Parameters.AddWithValue("@EquivQTY", activity.EquivQTY);
            command.Parameters.AddWithValue("@EquivUOM", activity.EquivUOM ?? "");
            command.Parameters.AddWithValue("@ClientEquivQty", activity.ClientEquivQty);
            command.Parameters.AddWithValue("@ClientBudget", activity.ClientBudget);
            command.Parameters.AddWithValue("@ClientCustom3", activity.ClientCustom3);
            command.Parameters.AddWithValue("@PrevEarnMHs", activity.PrevEarnMHs);
            command.Parameters.AddWithValue("@PrevEarnQTY", activity.PrevEarnQTY);
            command.Parameters.AddWithValue("@SchStart", activity.SchStart?.ToString("yyyy-MM-dd") ?? "");
            command.Parameters.AddWithValue("@SchFinish", activity.SchFinish?.ToString("yyyy-MM-dd") ?? "");
            command.Parameters.AddWithValue("@DateTrigger", activity.DateTrigger);
            command.Parameters.AddWithValue("@Notes", activity.Notes ?? "");
            command.Parameters.AddWithValue("@UDF1", activity.UDF1 ?? "");
            command.Parameters.AddWithValue("@UDF2", activity.UDF2 ?? "");
            command.Parameters.AddWithValue("@UDF3", activity.UDF3 ?? "");
            command.Parameters.AddWithValue("@UDF4", activity.UDF4 ?? "");
            command.Parameters.AddWithValue("@UDF5", activity.UDF5 ?? "");
            command.Parameters.AddWithValue("@UDF6", activity.UDF6 ?? "");
            command.Parameters.AddWithValue("@UDF7", activity.UDF7);
            command.Parameters.AddWithValue("@UDF8", activity.UDF8 ?? "");
            command.Parameters.AddWithValue("@UDF9", activity.UDF9 ?? "");
            command.Parameters.AddWithValue("@UDF10", activity.UDF10 ?? "");
            command.Parameters.AddWithValue("@UDF14", activity.UDF14 ?? "");
            command.Parameters.AddWithValue("@UDF15", activity.UDF15 ?? "");
            command.Parameters.AddWithValue("@UDF16", activity.UDF16 ?? "");
            command.Parameters.AddWithValue("@UDF17", activity.UDF17 ?? "");
            command.Parameters.AddWithValue("@UDF18", activity.UDF18 ?? "");
            command.Parameters.AddWithValue("@UDF20", activity.UDF20 ?? "");
            command.Parameters.AddWithValue("@PipeSize1", activity.PipeSize1);
            command.Parameters.AddWithValue("@PipeSize2", activity.PipeSize2);

            command.ExecuteNonQuery();
        }
    }
}