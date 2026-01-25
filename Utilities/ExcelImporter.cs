using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    
    // Import activities from Excel files - auto-detects Legacy vs NewVantage format
    // Legacy: OldVantage column names (UDFNineteen, Val_Perc_Complete), percent as 0-1 decimal
    // NewVantage: Column names match property names (UniqueID, PercentEntry), percent as 0-100

    public static class ExcelImporter
    {
        // Detect file format from header row column names
        // Returns Legacy if OldVantage columns found, NewVantage if NewVantage columns found
        private static ExportFormat DetectFormat(IXLRow headerRow)
        {
            var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int colNum = 1; colNum <= headerRow.LastCellUsed()?.Address.ColumnNumber; colNum++)
            {
                var cell = headerRow.Cell(colNum);
                string header = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    headers.Add(header);
            }

            // Signature columns unique to each format
            bool hasLegacyColumns = headers.Contains("UDFNineteen") || headers.Contains("Val_Perc_Complete");
            bool hasNewVantageColumns = headers.Contains("UniqueID") || headers.Contains("PercentEntry");

            if (hasLegacyColumns && !hasNewVantageColumns)
                return ExportFormat.Legacy;

            if (hasNewVantageColumns && !hasLegacyColumns)
                return ExportFormat.NewVantage;

            if (hasLegacyColumns && hasNewVantageColumns)
                throw new InvalidOperationException(
                    "The Excel file contains both Legacy and NewVantage column names.\n\n" +
                    "This file format is not supported. Please use a file exported from either " +
                    "OldVantage or Milestone, but not a mix of both.");

            // Fallback: check for other distinguishing columns
            if (headers.Contains("Tag_ProjectID"))
                return ExportFormat.Legacy;
            if (headers.Contains("ProjectID"))
                return ExportFormat.NewVantage;

            throw new InvalidOperationException(
                "Unable to determine the Excel file format.\n\n" +
                "Expected either:\n" +
                "• Legacy format with columns like 'UDFNineteen', 'Val_Perc_Complete'\n" +
                "• NewVantage format with columns like 'UniqueID', 'PercentEntry'\n\n" +
                "Please ensure the file is a valid activity export.");
        }

        // Build column mapping: Excel column number → NewVantage property name

        private static Dictionary<int, string> BuildColumnMap(IXLRow headerRow, ExportFormat format)
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
                string header = cell.GetString().Trim();

                if (string.IsNullOrEmpty(header))
                    continue;

                // For NewVantage format, headers are already property names
                // For Legacy format, translate OldVantage to NewVantage
                string propertyName = format == ExportFormat.NewVantage
                    ? header
                    : ColumnMapper.GetColumnNameFromOldVantage(header);

                // Skip calculated fields
                if (calculatedFields.Contains(propertyName))
                {
                    continue;
                }

                columnMap[colNum] = propertyName;
            }

            return columnMap;
        }


        // Read activities from Excel worksheet

        private static List<Activity> ReadActivitiesFromExcel(IXLWorksheet worksheet, Dictionary<int, string> columnMap, ExportFormat format)
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
                activity.BeginInit();  // Start batch initialization - suppress calculations

                // Set property values from Excel
                foreach (var mapping in columnMap)
                {
                    int excelColNum = mapping.Key;
                    string propertyName = mapping.Value;

                    var cell = row.Cell(excelColNum);
                    SetPropertyValue(activity, propertyName, cell, format);
                }

                // Handle UniqueID auto-generation if missing
                if (string.IsNullOrWhiteSpace(activity.UniqueID))
                {
                    activity.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
                    sequence++;
                }

                // Apply defaults for zero/empty numeric values
                if (activity.Quantity == 0) activity.Quantity = 0.001;
                if (activity.BudgetMHs == 0) activity.BudgetMHs = 0.001;
                if (activity.ClientBudget == 0) activity.ClientBudget = 0.001;

                // Always clear WeekEndDate on import
                activity.WeekEndDate = null;

                // Clean up date/percent inconsistencies
                var today = DateTime.Today;

                // Rule 1: PercentEntry = 0 → clear both dates
                if (activity.PercentEntry == 0)
                {
                    activity.SchStart = null;
                    activity.SchFinish = null;
                }
                else
                {
                    // Rule 2: PercentEntry > 0 but no SchStart → set to today
                    if (activity.SchStart == null)
                    {
                        activity.SchStart = today;
                    }
                    // Rule 3: SchStart in future → clamp to today
                    else if (activity.SchStart > today)
                    {
                        activity.SchStart = today;
                    }

                    // Rule 4: PercentEntry < 100 → clear SchFinish
                    if (activity.PercentEntry < 100)
                    {
                        activity.SchFinish = null;
                    }
                    else // PercentEntry = 100
                    {
                        // Rule 5: PercentEntry = 100 but no SchFinish → set to today
                        if (activity.SchFinish == null)
                        {
                            activity.SchFinish = today;
                        }
                        // Rule 6: SchFinish in future → clamp to today
                        else if (activity.SchFinish > today)
                        {
                            activity.SchFinish = today;
                        }
                    }
                }

                activity.ActivityID = 0;

                activity.EndInit();  // End batch initialization - trigger calculations once

                activities.Add(activity);
            }

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

        // Set Activity property value from Excel cell

        private static void SetPropertyValue(Activity activity, string propertyName, IXLCell cell, ExportFormat format)
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
                // Special handling for PercentEntry based on detected format
                if (propertyName == "PercentEntry")
                {
                    double value = cell.GetDouble();
                    double percentValue;

                    if (format == ExportFormat.NewVantage)
                    {
                        // NewVantage: values are already 0-100, use as-is
                        percentValue = value;
                    }
                    else
                    {
                        // Legacy: ALWAYS convert 0-1 decimal to 0-100 percentage
                        // Format is determined by column names, not value ranges
                        percentValue = value * 100.0;
                    }
                    activity.PercentEntry = NumericHelper.RoundToPlaces(percentValue);
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
                    double value = cell.GetDouble();
                    property.SetValue(activity, NumericHelper.RoundToPlaces(value));
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
            catch
            {
                // Set default value on error
                if (property.PropertyType == typeof(string))
                    property.SetValue(activity, "");
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
                    property.SetValue(activity, 0);
            }
        }


        /// Import activities to database using NewVantage column names

        private static int ImportToDatabase(List<Activity> activities, bool replaceMode, IProgress<(int current, int total, string message)>? progress = null)
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

                // Create command once (outside the loop)
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
                EqmtNO, LineNumber, ChgOrdNO,
                MtrlSpec, PipeGrade, PaintCode, InsulType, HtTrace,
                Aux1, Aux2, Aux3, Estimator, RFINO, XRay,
                EquivQTY, EquivUOM,
                ClientEquivQty, ClientBudget, ClientCustom3,
                PrevEarnMHs, PrevEarnQTY,
                SchStart, SchFinish, DateTrigger, Notes,
                UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                UDF11, UDF12, UDF13,
                UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
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
                @EqmtNO, @LineNumber, @ChgOrdNO,
                @MtrlSpec, @PipeGrade, @PaintCode, @InsulType, @HtTrace,
                @Aux1, @Aux2, @Aux3, @Estimator, @RFINO, @XRay,
                @EquivQTY, @EquivUOM,
                @ClientEquivQty, @ClientBudget, @ClientCustom3,
                @PrevEarnMHs, @PrevEarnQTY,
                @SchStart, @SchFinish, @DateTrigger, @Notes,
                @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9, @UDF10,
                @UDF11, @UDF12, @UDF13,
                @UDF14, @UDF15, @UDF16, @UDF17, @RespParty, @UDF20,
                @PipeSize1, @PipeSize2,
                @UpdatedUtcDate, @LocalDirty
            )";

                // Add parameters once
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
                command.Parameters.Add("@LineNumber", SqliteType.Text);
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
                command.Parameters.Add("@RespParty", SqliteType.Text);
                command.Parameters.Add("@UDF20", SqliteType.Text);
                command.Parameters.Add("@PipeSize1", SqliteType.Real);
                command.Parameters.Add("@PipeSize2", SqliteType.Real);
                command.Parameters.Add("@UpdatedUtcDate", SqliteType.Text);
                command.Parameters.Add("@LocalDirty", SqliteType.Integer);

                // Prepare statement (compile SQL once)
                command.Prepare();

                // Create duplicate check command once (for Combine mode)
                SqliteCommand? checkCommand = null;
                if (!replaceMode)
                {
                    checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = "SELECT COUNT(*) FROM Activities WHERE UniqueID = @id";
                    checkCommand.Parameters.Add("@id", SqliteType.Text);
                    checkCommand.Prepare();
                }

                // Loop through activities
                foreach (var activity in activities)
                {
                    // In combine mode, check if activity already exists
                    if (!replaceMode && checkCommand != null)
                    {
                        checkCommand.Parameters["@id"].Value = activity.UniqueID;
                        var exists = Convert.ToInt64(checkCommand.ExecuteScalar() ?? 0) > 0;

                        if (exists)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    // Set parameter values (reuse same command)
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
                    command.Parameters["@LineNumber"].Value = activity.LineNumber ?? "";
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
                    command.Parameters["@RespParty"].Value = activity.RespParty ?? "";
                    command.Parameters["@UDF20"].Value = activity.UDF20 ?? "";
                    command.Parameters["@PipeSize1"].Value = activity.PipeSize1;
                    command.Parameters["@PipeSize2"].Value = activity.PipeSize2;
                    command.Parameters["@UpdatedUtcDate"].Value = DateTime.UtcNow.ToString("o");
                    command.Parameters["@LocalDirty"].Value = 1;  // Mark as dirty - needs sync

                    // Execute (uses prepared statement)
                    command.ExecuteNonQuery();
                    imported++;

                    // Progress indicator
                    if (imported % 1000 == 0)
                    {
                        progress?.Report((imported, activities.Count, $"Importing records..."));
                    }
                }

                transaction.Commit();

                // Dispose duplicate check command if created
                checkCommand?.Dispose();

                // AFTER commit, reset sync versions for replaced ProjectIDs
                if (distinctProjectIds != null && distinctProjectIds.Any())
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

                return imported;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        // Validate that all UniqueID values in the list are unique
        // Throws exception with duplicate values if validation fails
        private static void ValidateNoDuplicateUniqueIDs(List<Activity> activities)
        {
            var duplicates = activities
                .Where(a => !string.IsNullOrWhiteSpace(a.UniqueID))
                .GroupBy(a => a.UniqueID)
                .Where(g => g.Count() > 1)
                .Select(g => new { UniqueID = g.Key, Count = g.Count() })
                .ToList();

            if (duplicates.Any())
            {
                var duplicateList = string.Join("\n", duplicates.Take(10).Select(d => $"  • {d.UniqueID} ({d.Count} occurrences)"));

                string message = $"The Excel file contains duplicate UDFNineteen values.\n\n" +
                                 $"Duplicates found:\n{duplicateList}";

                if (duplicates.Count > 10)
                    message += $"\n  ... and {duplicates.Count - 10} more";

                message += "\n\nPlease fix these duplicates in Excel and try again.";

                throw new InvalidOperationException(message);
            }
        }

        // Import activities from Excel file with auto-detected format
        public static async Task<int> ImportActivitiesAsync(string filePath, bool replaceMode, IProgress<(int current, int total, string message)>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report((0, 0, "Opening Excel file..."));

                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"Excel file not found: {filePath}");

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

                        if (worksheet == null)
                        {
                            throw new InvalidOperationException("No worksheets found in the Excel file.");
                        }

                        progress?.Report((0, 0, "Detecting file format..."));
                        var headerRow = worksheet.Row(1);

                        // Auto-detect format from column names
                        var format = DetectFormat(headerRow);
                        progress?.Report((0, 0, $"Importing {format} format..."));
                        var columnMap = BuildColumnMap(headerRow, format);

                        progress?.Report((0, 0, "Reading Excel data..."));
                        var activities = ReadActivitiesFromExcel(worksheet, columnMap, format);

                        ValidateNoDuplicateUniqueIDs(activities);

                        progress?.Report((0, activities.Count, "Importing to database..."));
                        int imported = ImportToDatabase(activities, replaceMode, progress);

                        progress?.Report((imported, imported, "Import complete!"));
                        return imported;
                    }
                }
                catch
                {
                    throw;
                }
            });
        }
    }
}