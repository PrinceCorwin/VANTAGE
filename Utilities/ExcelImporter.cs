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
                activity.ActivityID = 0;
                activities.Add(activity);
            }

            System.Diagnostics.Debug.WriteLine($"✓ Read {activities.Count} activities from Excel");
            return activities;
        }

        
        /// Find the last row with actual data
        
        private static int FindLastDataRow(IXLWorksheet worksheet)
        {
            return worksheet.LastRowUsed()?.RowNumber() ?? 1;
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

        private static int ImportToDatabase(List<Activity> activities, bool replaceMode, IProgress<(int current, int total, string message)> progress = null)
        {
            using var connection = DatabaseSetup.GetConnection();
            connection.Open();
            // Get distinct ProjectIDs BEFORE database operations (in-memory, fast)
            var distinctProjectIds = replaceMode
                ? activities.Select(a => a.ProjectID).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList()
                : null;
            using var transaction = connection.BeginTransaction();

            try
            {
                // If replace mode, delete all existing activities
                if (replaceMode)
                {
                    progress?.Report((0, activities.Count, "Clearing existing data..."));
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Activities";
                    int deleted = deleteCommand.ExecuteNonQuery();

                }

                int imported = 0;
                int skipped = 0;

                // ✅ CREATE COMMAND ONCE (OUTSIDE THE LOOP)
                using var command = connection.CreateCommand();
                command.CommandText = @"
            INSERT INTO Activities (
                HexNO, ProjectID, Description, UniqueID,
                Area, SubArea, PjtSystem, SystemNO,
                CompType, PhaseCategory, ROCStep,
                AssignedTo, CreatedBy, UpdatedBy,
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
                UDF11, UDF12, UDF13,
                UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                PipeSize1, PipeSize2,
                UpdatedUtcDate, LocalDirty
            ) VALUES (
                @HexNO, @ProjectID, @Description, @UniqueID,
                @Area, @SubArea, @PjtSystem, @SystemNO,
                @CompType, @PhaseCategory, @ROCStep,
                @AssignedTo, @CreatedBy, @UpdatedBy,
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
                @UDF11, @UDF12, @UDF13,
                @UDF14, @UDF15, @UDF16, @UDF17, @UDF18, @UDF20,
                @PipeSize1, @PipeSize2,
                @UpdatedUtcDate, @LocalDirty
            )";

                // ✅ ADD PARAMETERS ONCE
                command.Parameters.Add("@HexNO", SqliteType.Integer);
                command.Parameters.Add("@ProjectID", SqliteType.Text);
                command.Parameters.Add("@Description", SqliteType.Text);
                command.Parameters.Add("@UniqueID", SqliteType.Text);
                command.Parameters.Add("@Area", SqliteType.Text);
                command.Parameters.Add("@SubArea", SqliteType.Text);
                command.Parameters.Add("@PjtSystem", SqliteType.Text);
                command.Parameters.Add("@SystemNO", SqliteType.Text);
                command.Parameters.Add("@CompType", SqliteType.Text);
                command.Parameters.Add("@PhaseCategory", SqliteType.Text);
                command.Parameters.Add("@ROCStep", SqliteType.Text);
                command.Parameters.Add("@AssignedTo", SqliteType.Text);
                command.Parameters.Add("@CreatedBy", SqliteType.Text);
                command.Parameters.Add("@UpdatedBy", SqliteType.Text);
                command.Parameters.Add("@PercentEntry", SqliteType.Real);
                command.Parameters.Add("@Quantity", SqliteType.Real);
                command.Parameters.Add("@EarnQtyEntry", SqliteType.Real);
                command.Parameters.Add("@UOM", SqliteType.Text);
                command.Parameters.Add("@BudgetMHs", SqliteType.Real);
                command.Parameters.Add("@BudgetHoursGroup", SqliteType.Real);
                command.Parameters.Add("@BudgetHoursROC", SqliteType.Real);
                command.Parameters.Add("@BaseUnit", SqliteType.Real);
                command.Parameters.Add("@EarnedMHsRoc", SqliteType.Real);
                command.Parameters.Add("@ROCID", SqliteType.Integer);
                command.Parameters.Add("@ROCPercent", SqliteType.Real);
                command.Parameters.Add("@ROCBudgetQTY", SqliteType.Real);
                command.Parameters.Add("@DwgNO", SqliteType.Text);
                command.Parameters.Add("@RevNO", SqliteType.Text);
                command.Parameters.Add("@SecondDwgNO", SqliteType.Text);
                command.Parameters.Add("@ShtNO", SqliteType.Text);
                command.Parameters.Add("@TagNO", SqliteType.Text);
                command.Parameters.Add("@WorkPackage", SqliteType.Text);
                command.Parameters.Add("@PhaseCode", SqliteType.Text);
                command.Parameters.Add("@Service", SqliteType.Text);
                command.Parameters.Add("@ShopField", SqliteType.Text);
                command.Parameters.Add("@SchedActNO", SqliteType.Text);
                command.Parameters.Add("@SecondActno", SqliteType.Text);
                command.Parameters.Add("@EqmtNO", SqliteType.Text);
                command.Parameters.Add("@LineNO", SqliteType.Text);
                command.Parameters.Add("@ChgOrdNO", SqliteType.Text);
                command.Parameters.Add("@MtrlSpec", SqliteType.Text);
                command.Parameters.Add("@PipeGrade", SqliteType.Text);
                command.Parameters.Add("@PaintCode", SqliteType.Text);
                command.Parameters.Add("@InsulType", SqliteType.Text);
                command.Parameters.Add("@HtTrace", SqliteType.Text);
                command.Parameters.Add("@Aux1", SqliteType.Text);
                command.Parameters.Add("@Aux2", SqliteType.Text);
                command.Parameters.Add("@Aux3", SqliteType.Text);
                command.Parameters.Add("@Estimator", SqliteType.Text);
                command.Parameters.Add("@RFINO", SqliteType.Text);
                command.Parameters.Add("@XRay", SqliteType.Integer);
                command.Parameters.Add("@EquivQTY", SqliteType.Text);
                command.Parameters.Add("@EquivUOM", SqliteType.Text);
                command.Parameters.Add("@ClientEquivQty", SqliteType.Real);
                command.Parameters.Add("@ClientBudget", SqliteType.Real);
                command.Parameters.Add("@ClientCustom3", SqliteType.Real);
                command.Parameters.Add("@PrevEarnMHs", SqliteType.Real);
                command.Parameters.Add("@PrevEarnQTY", SqliteType.Real);
                command.Parameters.Add("@SchStart", SqliteType.Text);
                command.Parameters.Add("@SchFinish", SqliteType.Text);
                command.Parameters.Add("@DateTrigger", SqliteType.Integer);
                command.Parameters.Add("@Notes", SqliteType.Text);
                command.Parameters.Add("@UDF1", SqliteType.Text);
                command.Parameters.Add("@UDF2", SqliteType.Text);
                command.Parameters.Add("@UDF3", SqliteType.Text);
                command.Parameters.Add("@UDF4", SqliteType.Text);
                command.Parameters.Add("@UDF5", SqliteType.Text);
                command.Parameters.Add("@UDF6", SqliteType.Text);
                command.Parameters.Add("@UDF7", SqliteType.Text);
                command.Parameters.Add("@UDF8", SqliteType.Text);
                command.Parameters.Add("@UDF9", SqliteType.Text);
                command.Parameters.Add("@UDF10", SqliteType.Text);
                command.Parameters.Add("@UDF11", SqliteType.Text);
                command.Parameters.Add("@UDF12", SqliteType.Text);
                command.Parameters.Add("@UDF13", SqliteType.Text);
                command.Parameters.Add("@UDF14", SqliteType.Text);
                command.Parameters.Add("@UDF15", SqliteType.Text);
                command.Parameters.Add("@UDF16", SqliteType.Text);
                command.Parameters.Add("@UDF17", SqliteType.Text);
                command.Parameters.Add("@UDF18", SqliteType.Text);
                command.Parameters.Add("@UDF20", SqliteType.Text);
                command.Parameters.Add("@PipeSize1", SqliteType.Real);
                command.Parameters.Add("@PipeSize2", SqliteType.Real);
                command.Parameters.Add("@UpdatedUtcDate", SqliteType.Text);
                command.Parameters.Add("@LocalDirty", SqliteType.Integer);

                // ✅ PREPARE STATEMENT (COMPILE SQL ONCE)
                command.Prepare();

                // ✅ NOW LOOP THROUGH ACTIVITIES
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

                    // ✅ SET PARAMETER VALUES (REUSE SAME COMMAND)
                    command.Parameters["@HexNO"].Value = activity.HexNO;
                    command.Parameters["@ProjectID"].Value = activity.ProjectID ?? "";
                    command.Parameters["@Description"].Value = activity.Description ?? "";
                    command.Parameters["@UniqueID"].Value = activity.UniqueID;
                    command.Parameters["@Area"].Value = activity.Area ?? "";
                    command.Parameters["@SubArea"].Value = activity.SubArea ?? "";
                    command.Parameters["@PjtSystem"].Value = activity.PjtSystem ?? "";
                    command.Parameters["@SystemNO"].Value = activity.SystemNO ?? "";
                    command.Parameters["@CompType"].Value = activity.CompType ?? "";
                    command.Parameters["@PhaseCategory"].Value = activity.PhaseCategory ?? "";
                    command.Parameters["@ROCStep"].Value = activity.ROCStep ?? "";
                    string importingUser = App.CurrentUser?.Username ?? Environment.UserName;
                    command.Parameters["@AssignedTo"].Value = string.IsNullOrWhiteSpace(activity.AssignedTo)
                        ? importingUser
                        : activity.AssignedTo;
                    string currentUser = App.CurrentUser?.Username ?? Environment.UserName;
                    command.Parameters["@CreatedBy"].Value = currentUser;
                    command.Parameters["@UpdatedBy"].Value = currentUser;
                    command.Parameters["@PercentEntry"].Value = activity.PercentEntry;
                    command.Parameters["@Quantity"].Value = activity.Quantity;
                    command.Parameters["@EarnQtyEntry"].Value = activity.EarnQtyEntry;
                    command.Parameters["@UOM"].Value = activity.UOM ?? "";
                    command.Parameters["@BudgetMHs"].Value = activity.BudgetMHs;
                    command.Parameters["@BudgetHoursGroup"].Value = activity.BudgetHoursGroup;
                    command.Parameters["@BudgetHoursROC"].Value = activity.BudgetHoursROC;
                    command.Parameters["@BaseUnit"].Value = activity.BaseUnit;
                    command.Parameters["@EarnedMHsRoc"].Value = activity.EarnedMHsRoc;
                    command.Parameters["@ROCID"].Value = activity.ROCID;
                    command.Parameters["@ROCPercent"].Value = activity.ROCPercent;
                    command.Parameters["@ROCBudgetQTY"].Value = activity.ROCBudgetQTY;
                    command.Parameters["@DwgNO"].Value = activity.DwgNO ?? "";
                    command.Parameters["@RevNO"].Value = activity.RevNO ?? "";
                    command.Parameters["@SecondDwgNO"].Value = activity.SecondDwgNO ?? "";
                    command.Parameters["@ShtNO"].Value = activity.ShtNO ?? "";
                    command.Parameters["@TagNO"].Value = activity.TagNO ?? "";
                    command.Parameters["@WorkPackage"].Value = activity.WorkPackage ?? "";
                    command.Parameters["@PhaseCode"].Value = activity.PhaseCode ?? "";
                    command.Parameters["@Service"].Value = activity.Service ?? "";
                    command.Parameters["@ShopField"].Value = activity.ShopField ?? "";
                    command.Parameters["@SchedActNO"].Value = activity.SchedActNO ?? "";
                    command.Parameters["@SecondActno"].Value = activity.SecondActno ?? "";
                    command.Parameters["@EqmtNO"].Value = activity.EqmtNO ?? "";
                    command.Parameters["@LineNO"].Value = activity.LineNO ?? "";
                    command.Parameters["@ChgOrdNO"].Value = activity.ChgOrdNO ?? "";
                    command.Parameters["@MtrlSpec"].Value = activity.MtrlSpec ?? "";
                    command.Parameters["@PipeGrade"].Value = activity.PipeGrade ?? "";
                    command.Parameters["@PaintCode"].Value = activity.PaintCode ?? "";
                    command.Parameters["@InsulType"].Value = activity.InsulType ?? "";
                    command.Parameters["@HtTrace"].Value = activity.HtTrace ?? "";
                    command.Parameters["@Aux1"].Value = activity.Aux1 ?? "";
                    command.Parameters["@Aux2"].Value = activity.Aux2 ?? "";
                    command.Parameters["@Aux3"].Value = activity.Aux3 ?? "";
                    command.Parameters["@Estimator"].Value = activity.Estimator ?? "";
                    command.Parameters["@RFINO"].Value = activity.RFINO ?? "";
                    command.Parameters["@XRay"].Value = activity.XRay;
                    command.Parameters["@EquivQTY"].Value = activity.EquivQTY;
                    command.Parameters["@EquivUOM"].Value = activity.EquivUOM ?? "";
                    command.Parameters["@ClientEquivQty"].Value = activity.ClientEquivQty;
                    command.Parameters["@ClientBudget"].Value = activity.ClientBudget;
                    command.Parameters["@ClientCustom3"].Value = activity.ClientCustom3;
                    command.Parameters["@PrevEarnMHs"].Value = activity.PrevEarnMHs;
                    command.Parameters["@PrevEarnQTY"].Value = activity.PrevEarnQTY;
                    command.Parameters["@SchStart"].Value = activity.SchStart?.ToString("yyyy-MM-dd") ?? "";
                    command.Parameters["@SchFinish"].Value = activity.SchFinish?.ToString("yyyy-MM-dd") ?? "";
                    command.Parameters["@DateTrigger"].Value = activity.DateTrigger;
                    command.Parameters["@Notes"].Value = activity.Notes ?? "";
                    command.Parameters["@UDF1"].Value = activity.UDF1 ?? "";
                    command.Parameters["@UDF2"].Value = activity.UDF2 ?? "";
                    command.Parameters["@UDF3"].Value = activity.UDF3 ?? "";
                    command.Parameters["@UDF4"].Value = activity.UDF4 ?? "";
                    command.Parameters["@UDF5"].Value = activity.UDF5 ?? "";
                    command.Parameters["@UDF6"].Value = activity.UDF6 ?? "";
                    command.Parameters["@UDF7"].Value = activity.UDF7;
                    command.Parameters["@UDF8"].Value = activity.UDF8 ?? "";
                    command.Parameters["@UDF9"].Value = activity.UDF9 ?? "";
                    command.Parameters["@UDF10"].Value = activity.UDF10 ?? "";
                    command.Parameters["@UDF11"].Value = activity.UDF11 ?? "";
                    command.Parameters["@UDF12"].Value = activity.UDF12 ?? "";
                    command.Parameters["@UDF13"].Value = activity.UDF13 ?? "";
                    command.Parameters["@UDF14"].Value = activity.UDF14 ?? "";
                    command.Parameters["@UDF15"].Value = activity.UDF15 ?? "";
                    command.Parameters["@UDF16"].Value = activity.UDF16 ?? "";
                    command.Parameters["@UDF17"].Value = activity.UDF17 ?? "";
                    command.Parameters["@UDF18"].Value = activity.UDF18 ?? "";
                    command.Parameters["@UDF20"].Value = activity.UDF20 ?? "";
                    command.Parameters["@PipeSize1"].Value = activity.PipeSize1;
                    command.Parameters["@PipeSize2"].Value = activity.PipeSize2;
                    command.Parameters["@UpdatedUtcDate"].Value = DateTime.UtcNow.ToString("o");
                    command.Parameters["@LocalDirty"].Value = 1;  // Mark as dirty - needs sync

                    // ✅ EXECUTE (USES PREPARED STATEMENT)
                    System.Diagnostics.Debug.WriteLine($"LocalDirty value being inserted: {command.Parameters["@LocalDirty"].Value}");
                    command.ExecuteNonQuery();
                    imported++;

                    // Progress indicator
                    if (imported % 1000 == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Inserted {imported} records...");
                        progress?.Report((imported, activities.Count, $"Importing records..."));
                    }
                }

                transaction.Commit();
                // AFTER commit, reset sync versions for replaced ProjectIDs
                if (distinctProjectIds != null && distinctProjectIds.Any())
                {
                    foreach (var projectId in distinctProjectIds)
                    {
                        var deleteCmd = connection.CreateCommand();
                        deleteCmd.CommandText = "DELETE FROM AppSettings WHERE SettingName = @settingName";
                        deleteCmd.Parameters.AddWithValue("@settingName", $"LastPulledSyncVersion_{projectId}");
                        deleteCmd.ExecuteNonQuery();
                    }

                    AppLogger.Info($"Reset sync versions for {distinctProjectIds.Count} projects after replace import", "ExcelImporter");
                }
                else
                {
                    AppLogger.Warning("distinctProjectIds is null or empty - no sync versions reset", "ExcelImporter");
                }
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


        /// Import activities from Excel file with progress reporting

        /// <param name="filePath">Path to Excel file with OldVantage column names</param>
        /// <param name="replaceMode">True = replace all existing, False = combine/add</param>
        /// <param name="progress">Optional progress callback (current, total, message)</param>
        /// <returns>Number of records imported</returns>
        public static async Task<int> ImportActivitiesAsync(string filePath, bool replaceMode, IProgress<(int current, int total, string message)> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Starting import...");
                    progress?.Report((0, 0, "Opening Excel file..."));

                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"Excel file not found: {filePath}");

                    System.Diagnostics.Debug.WriteLine("Opening workbook...");

                    IXLWorkbook workbook;
                    try
                    {
                        workbook = new XLWorkbook(filePath);
                    }
                    catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020))
                    {
                        throw new InvalidOperationException(
                            "The Excel file is currently open in another program.\n\n" +
                            "Please close the file and try again.", ex);
                    }

                    using (workbook)
                    {
                        var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Sheet1")
                            ?? workbook.Worksheets.FirstOrDefault();

                        System.Diagnostics.Debug.WriteLine("Building column map...");
                        progress?.Report((0, 0, "Analyzing Excel structure..."));
                        var headerRow = worksheet.Row(1);
                        var columnMap = BuildColumnMap(headerRow);

                        System.Diagnostics.Debug.WriteLine("Reading activities from Excel...");
                        progress?.Report((0, 0, "Reading Excel data..."));
                        var activities = ReadActivitiesFromExcel(worksheet, columnMap);
                        System.Diagnostics.Debug.WriteLine($"Finished reading {activities.Count} activities");

                        System.Diagnostics.Debug.WriteLine("Importing to database...");
                        progress?.Report((0, activities.Count, "Importing to database..."));
                        int imported = ImportToDatabase(activities, replaceMode, progress);
                        System.Diagnostics.Debug.WriteLine($"Finished importing {imported} records");

                        progress?.Report((imported, imported, "Import complete!"));
                        return imported;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Import error: {ex.Message}");
                    throw;
                }
            });
        }

        // Keep the synchronous version for backward compatibility
        public static int ImportActivities(string filePath, bool replaceMode)
        {
        return ImportActivitiesAsync(filePath, replaceMode).GetAwaiter().GetResult();
        }

    }
}