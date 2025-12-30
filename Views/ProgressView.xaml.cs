using Microsoft.Data.Sqlite;
using MILESTONE.Views;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.Windows.Tools.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private const int ColumnUniqueValueDisplayLimit = 1000; // configurable
        private Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn> _columnMap = new Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn>();
        private ProgressViewModel _viewModel;
        // one key per grid/view
        private const string GridPrefsKey = "ProgressGrid.PreferencesJson";
        private ProgressViewModel ViewModel
        {
            get
            {
                if (DataContext is ProgressViewModel vm)
                    return vm;

                AppLogger.Error(
                    "ProgressView.DataContext was not a ProgressViewModel. " +
                    $"Actual type: {(DataContext?.GetType().FullName ?? "null")}"
                );

                throw new InvalidOperationException(
                    "ProgressView requires ProgressViewModel as its DataContext before using ViewModel."
                );
            }
        }
        private object? _originalCellValue;
        private Dictionary<string, PropertyInfo?> _propertyCache = new Dictionary<string, PropertyInfo?>();
        private async void DeleteSelectedActivities_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = sfActivities.SelectedItems?.Cast<Activity>().ToList();
                if (selected == null || selected.Count == 0)
                {
                    MessageBox.Show("Please select one or more records to delete.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var currentUser = App.CurrentUser;
                bool isAdmin = currentUser?.IsAdmin ?? false;

                // Check connection to Azure
                if (!AzureDbManager.CheckConnection(out string connectionError))
                {
                    MessageBox.Show($"Deletion requires connection to Azure database.\n\n{connectionError}\n\nPlease try again when connected.",
                        "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verify ownership in Azure for each record
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                var ownedRecords = new List<Activity>();
                var deniedRecords = new List<string>();

                foreach (var activity in selected)
                {
                    var checkCmd = azureConn.CreateCommand();
                    checkCmd.CommandText = "SELECT AssignedTo FROM Activities WHERE UniqueID = @id";
                    checkCmd.Parameters.AddWithValue("@id", activity.UniqueID);
                    var azureOwner = checkCmd.ExecuteScalar()?.ToString();

                    if (isAdmin || string.Equals(azureOwner, currentUser?.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        ownedRecords.Add(activity);
                    }
                    else
                    {
                        deniedRecords.Add(activity.UniqueID);
                    }
                }

                if (deniedRecords.Any())
                {
                    MessageBox.Show(
                        $"{deniedRecords.Count} record(s) could not be deleted - you do not own them.\n\n" +
                        $"First few: {string.Join(", ", deniedRecords.Take(3))}",
                        "Permission Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    if (!ownedRecords.Any())
                        return;
                }

                // Confirm deletion
                string preview = string.Join(", ", ownedRecords.Take(5).Select(a => a.UniqueID));
                if (ownedRecords.Count > 5) preview += ", …";

                var confirm = MessageBox.Show(
                    $"Delete {ownedRecords.Count} record(s)?\n\nFirst few: {preview}\n\nThis action cannot be undone.",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    azureConn.Close();
                    return;
                }

                // Delete from local
                using var localConn = DatabaseSetup.GetConnection();
                localConn.Open();

                var uniqueIds = ownedRecords.Select(a => a.UniqueID).ToList();
                string uniqueIdList = string.Join(",", uniqueIds.Select(id => $"'{id}'"));

                var deleteLocalCmd = localConn.CreateCommand();
                deleteLocalCmd.CommandText = $"DELETE FROM Activities WHERE UniqueID IN ({uniqueIdList})";
                int localDeleted = deleteLocalCmd.ExecuteNonQuery();

                // Set IsDeleted=1 in Azure (SyncVersion auto-increments via trigger)
                var deleteAzureCmd = azureConn.CreateCommand();
                deleteAzureCmd.CommandText = $@"
                        UPDATE Activities 
                        SET IsDeleted = 1, 
                            UpdatedBy = @user, 
                            UpdatedUtcDate = @date 
                        WHERE UniqueID IN ({uniqueIdList})";
                deleteAzureCmd.Parameters.AddWithValue("@user", currentUser?.Username ?? "Unknown");
                deleteAzureCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                int azureDeleted = deleteAzureCmd.ExecuteNonQuery();

                azureConn.Close();

                // Refresh grid and totals
                if (ViewModel != null)
                {
                    await ViewModel.RefreshAsync();
                    await ViewModel.UpdateTotalsAsync();
                }

                AppLogger.Info(
                    $"User deleted {localDeleted} activities (IsDeleted=1 set in Azure for {azureDeleted} records).",
                    "ProgressView.DeleteSelectedActivities_Click",
                    currentUser?.Username ?? "UnknownUser"
                );

                MessageBox.Show(
                    $"{localDeleted} record(s) deleted successfully.",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.DeleteSelectedActivities_Click");
                MessageBox.Show($"Delete failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnMetadataErrors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all existing filters first
                await _viewModel.ClearAllFiltersAsync();

                // Apply metadata error filter - records missing required fields
                var errorFilter = @"(
                        WorkPackage IS NULL OR WorkPackage = '' OR
                        PhaseCode IS NULL OR PhaseCode = '' OR
                        CompType IS NULL OR CompType = '' OR
                        PhaseCategory IS NULL OR PhaseCategory = '' OR
                        ProjectID IS NULL OR ProjectID = '' OR
                        NOT EXISTS (SELECT 1 FROM Projects p WHERE p.ProjectID = Activities.ProjectID) OR
                        SchedActNO IS NULL OR SchedActNO = '' OR
                        Description IS NULL OR Description = '' OR
                        ROCStep IS NULL OR ROCStep = '' OR
                        UDF18 IS NULL OR UDF18 = ''
                    )";

                await _viewModel.ApplyFilter("MetadataErrors", "IN", errorFilter);

                UpdateRecordCount();
                UpdateSummaryPanel();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.BtnMetadataErrors_Click");
                MessageBox.Show($"Error filtering metadata errors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task CalculateMetadataErrorCount()
        {
            try
            {
                await Task.Run(() =>
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                SELECT COUNT(*) FROM Activities a
                WHERE a.AssignedTo = @currentUser
                  AND (
                    a.WorkPackage IS NULL OR a.WorkPackage = '' OR
                    a.PhaseCode IS NULL OR a.PhaseCode = '' OR
                    a.CompType IS NULL OR a.CompType = '' OR
                    a.PhaseCategory IS NULL OR a.PhaseCategory = '' OR
                    a.ProjectID IS NULL OR a.ProjectID = '' OR
                    NOT EXISTS (SELECT 1 FROM Projects p WHERE p.ProjectID = a.ProjectID) OR
                    a.SchedActNO IS NULL OR a.SchedActNO = '' OR
                    a.Description IS NULL OR a.Description = '' OR
                    a.ROCStep IS NULL OR a.ROCStep = '' OR
                    a.UDF18 IS NULL OR a.UDF18 = ''
                  )";
                    cmd.Parameters.AddWithValue("@currentUser", App.CurrentUser?.Username ?? "");

                    var count = Convert.ToInt32(cmd.ExecuteScalar());

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _viewModel.MetadataErrorCount = count;
                    });
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.CalculateMetadataErrorCount");
            }
        }
        // Add these two methods to ProgressView.xaml.cs (in the context menu handlers region)

        // Copy (Visible Columns) - copies only visible columns manually
        private void MenuCopyVisibleColumns_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if records are selected
                var selected = sfActivities.SelectedItems?.Cast<Activity>().ToList();
                if (selected == null || selected.Count == 0)
                {
                    MessageBox.Show("Please select one or more records to copy.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get visible columns from grid (in display order)
                var visibleColumns = sfActivities.Columns
                    .Where(c => !c.IsHidden && c.Width > 0)
                    .Select((col, index) => new { Column = col, Index = index })
                    .OrderBy(x => x.Index)
                    .Select(x => x.Column)
                    .ToList();

                var sb = new StringBuilder();

                // Row 1: Column headers (only visible columns)
                sb.AppendLine(string.Join("\t", visibleColumns.Select(c => c.HeaderText)));

                // Rows 2+: Data rows (only visible columns)
                foreach (var activity in selected)
                {
                    var values = visibleColumns.Select(col =>
                    {
                        var propertyName = col.MappingName;
                        var property = typeof(Activity).GetProperty(propertyName);

                        if (property == null)
                            return string.Empty;

                        var value = property.GetValue(activity);

                        // Handle null values
                        if (value == null)
                            return string.Empty;

                        // Format dates
                        if (value is DateTime dt)
                            return dt.ToString("yyyy-MM-dd HH:mm:ss");

                        // Convert to string
                        return value.ToString();
                    });

                    sb.AppendLine(string.Join("\t", values));
                }

                // Copy to clipboard
                Clipboard.SetText(sb.ToString());

                AppLogger.Info($"Copied {selected.Count} record(s) - {visibleColumns.Count} visible columns",
                    "Copy Visible", App.CurrentUser?.Username ?? "Unknown");

                // Silent success (clipboard populated)
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Copy Visible Columns", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Copy failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Copy (All Columns) - copies all Activity properties using reflection
        private void MenuCopyAllColumns_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if records are selected
                var selected = sfActivities.SelectedItems?.Cast<Activity>().ToList();
                if (selected == null || selected.Count == 0)
                {
                    MessageBox.Show("Please select one or more records to copy.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get all properties from Activity model using reflection
                var activityType = typeof(Activity);
                var properties = activityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead) // Only readable properties
                    .OrderBy(p => p.Name)   // Alphabetical order
                    .ToList();

                var sb = new StringBuilder();

                // Build tab-delimited output
                // Row 1: Column headers
                sb.AppendLine(string.Join("\t", properties.Select(p => p.Name)));

                // Rows 2+: Data rows
                foreach (var activity in selected)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(activity);

                        // Handle null values
                        if (value == null)
                            return string.Empty;

                        // Format dates
                        if (value is DateTime dt)
                            return dt.ToString("yyyy-MM-dd HH:mm:ss");

                        // Convert to string
                        return value.ToString();
                    });

                    sb.AppendLine(string.Join("\t", values));
                }

                // Copy to clipboard
                Clipboard.SetText(sb.ToString());

                AppLogger.Info($"Copied {selected.Count} record(s) - all columns ({properties.Count} properties)",
                    "Copy All", App.CurrentUser?.Username ?? "Unknown");

                // Optional: Show toast/status instead of MessageBox for better UX
                // For now, silent success (clipboard populated)
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Copy All Columns", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Copy failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuDuplicateRows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = sfActivities.SelectedItems?.Cast<Activity>().ToList();
                if (selected == null || selected.Count == 0)
                {
                    MessageBox.Show("Please select one or more records to duplicate.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"This will create {selected.Count} duplicate record(s).\n\nContinue?",
                    "Confirm Duplication",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
                var userSuffix = App.CurrentUser?.Username?.Length >= 3
                    ? App.CurrentUser.Username.Substring(App.CurrentUser.Username.Length - 3).ToLower()
                    : "usr";
                int sequence = 1;

                var currentUser = App.CurrentUser?.Username ?? "Unknown";
                int successCount = 0;

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    foreach (var original in selected)
                    {
                        var duplicate = new Activity
                        {
                            UniqueID = $"i{timestamp}{sequence}{userSuffix}",
                            ActivityID = 0,
                            AssignedTo = currentUser,
                            LocalDirty = 1,
                            CreatedBy = currentUser,
                            UpdatedBy = currentUser,
                            UpdatedUtcDate = DateTime.UtcNow,
                            SyncVersion = 0,
                            WeekEndDate = null,
                            ProgDate = null,
                            AzureUploadUtcDate = null,
                            SchStart = null,
                            SchFinish = null,
                            Area = original.Area,
                            Aux1 = original.Aux1,
                            Aux2 = original.Aux2,
                            Aux3 = original.Aux3,
                            BaseUnit = original.BaseUnit,
                            BudgetHoursGroup = original.BudgetHoursGroup,
                            BudgetHoursROC = original.BudgetHoursROC,
                            BudgetMHs = original.BudgetMHs,
                            ChgOrdNO = original.ChgOrdNO,
                            ClientBudget = original.ClientBudget,
                            ClientCustom3 = original.ClientCustom3,
                            ClientEquivQty = original.ClientEquivQty,
                            CompType = original.CompType,
                            DateTrigger = original.DateTrigger,
                            Description = original.Description,
                            DwgNO = original.DwgNO,
                            EarnQtyEntry = original.EarnQtyEntry,
                            EarnedMHsRoc = original.EarnedMHsRoc,
                            EqmtNO = original.EqmtNO,
                            EquivQTY = original.EquivQTY,
                            EquivUOM = original.EquivUOM,
                            Estimator = original.Estimator,
                            HexNO = original.HexNO,
                            HtTrace = original.HtTrace,
                            InsulType = original.InsulType,
                            LineNumber = original.LineNumber,
                            MtrlSpec = original.MtrlSpec,
                            Notes = original.Notes,
                            PaintCode = original.PaintCode,
                            PercentEntry = original.PercentEntry,
                            PhaseCategory = original.PhaseCategory,
                            PhaseCode = original.PhaseCode,
                            PipeGrade = original.PipeGrade,
                            PipeSize1 = original.PipeSize1,
                            PipeSize2 = original.PipeSize2,
                            PrevEarnMHs = original.PrevEarnMHs,
                            PrevEarnQTY = original.PrevEarnQTY,
                            ProjectID = original.ProjectID,
                            Quantity = original.Quantity,
                            RevNO = original.RevNO,
                            RFINO = original.RFINO,
                            ROCBudgetQTY = original.ROCBudgetQTY,
                            ROCID = original.ROCID,
                            ROCPercent = original.ROCPercent,
                            ROCStep = original.ROCStep,
                            SchedActNO = original.SchedActNO,
                            SecondActno = original.SecondActno,
                            SecondDwgNO = original.SecondDwgNO,
                            Service = original.Service,
                            ShopField = original.ShopField,
                            ShtNO = original.ShtNO,
                            SubArea = original.SubArea,
                            PjtSystem = original.PjtSystem,
                            SystemNO = original.SystemNO,
                            TagNO = original.TagNO,
                            UDF1 = original.UDF1,
                            UDF2 = original.UDF2,
                            UDF3 = original.UDF3,
                            UDF4 = original.UDF4,
                            UDF5 = original.UDF5,
                            UDF6 = original.UDF6,
                            UDF7 = original.UDF7,
                            UDF8 = original.UDF8,
                            UDF9 = original.UDF9,
                            UDF10 = original.UDF10,
                            UDF11 = original.UDF11,
                            UDF12 = original.UDF12,
                            UDF13 = original.UDF13,
                            UDF14 = original.UDF14,
                            UDF15 = original.UDF15,
                            UDF16 = original.UDF16,
                            UDF17 = original.UDF17,
                            UDF18 = original.UDF18,
                            UDF20 = original.UDF20,
                            UOM = original.UOM,
                            WorkPackage = original.WorkPackage,
                            XRay = original.XRay
                        };

                        var insertCmd = connection.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = @"
                    INSERT INTO Activities (
                        UniqueID, ActivityID, Area, AssignedTo, AzureUploadUtcDate, Aux1, Aux2, Aux3,
                        BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs, ChgOrdNO, ClientBudget,
                        ClientCustom3, ClientEquivQty, CompType, CreatedBy, DateTrigger, Description,
                        DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO, EquivQTY, EquivUOM, Estimator,
                        HexNO, HtTrace, InsulType, LineNumber, LocalDirty, MtrlSpec, Notes, PaintCode,
                        PercentEntry, PhaseCategory, PhaseCode, PipeGrade, PipeSize1, PipeSize2,
                        PrevEarnMHs, PrevEarnQTY, ProgDate, ProjectID, Quantity, RevNO, RFINO,
                        ROCBudgetQTY, ROCID, ROCPercent, ROCStep, SchedActNO, SchFinish, SchStart,
                        SecondActno, SecondDwgNO, Service, ShopField, ShtNO, SubArea, PjtSystem,
                        SystemNO, TagNO, UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9,
                        UDF10, UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                        UpdatedBy, UpdatedUtcDate, UOM, WeekEndDate, WorkPackage, XRay, SyncVersion
                    ) VALUES (
                        @UniqueID, @ActivityID, @Area, @AssignedTo, @AzureUploadUtcDate, @Aux1, @Aux2, @Aux3,
                        @BaseUnit, @BudgetHoursGroup, @BudgetHoursROC, @BudgetMHs, @ChgOrdNO, @ClientBudget,
                        @ClientCustom3, @ClientEquivQty, @CompType, @CreatedBy, @DateTrigger, @Description,
                        @DwgNO, @EarnQtyEntry, @EarnedMHsRoc, @EqmtNO, @EquivQTY, @EquivUOM, @Estimator,
                        @HexNO, @HtTrace, @InsulType, @LineNumber, @LocalDirty, @MtrlSpec, @Notes, @PaintCode,
                        @PercentEntry, @PhaseCategory, @PhaseCode, @PipeGrade, @PipeSize1, @PipeSize2,
                        @PrevEarnMHs, @PrevEarnQTY, @ProgDate, @ProjectID, @Quantity, @RevNO, @RFINO,
                        @ROCBudgetQTY, @ROCID, @ROCPercent, @ROCStep, @SchedActNO, @SchFinish, @SchStart,
                        @SecondActno, @SecondDwgNO, @Service, @ShopField, @ShtNO, @SubArea, @PjtSystem,
                        @SystemNO, @TagNO, @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9,
                        @UDF10, @UDF11, @UDF12, @UDF13, @UDF14, @UDF15, @UDF16, @UDF17, @UDF18, @UDF20,
                        @UpdatedBy, @UpdatedUtcDate, @UOM, @WeekEndDate, @WorkPackage, @XRay, @SyncVersion
                    )";

                        insertCmd.Parameters.AddWithValue("@UniqueID", duplicate.UniqueID);
                        insertCmd.Parameters.AddWithValue("@ActivityID", duplicate.ActivityID);
                        insertCmd.Parameters.AddWithValue("@Area", duplicate.Area ?? "");
                        insertCmd.Parameters.AddWithValue("@AssignedTo", duplicate.AssignedTo ?? "");
                        insertCmd.Parameters.AddWithValue("@AzureUploadUtcDate", duplicate.AzureUploadUtcDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@Aux1", duplicate.Aux1 ?? "");
                        insertCmd.Parameters.AddWithValue("@Aux2", duplicate.Aux2 ?? "");
                        insertCmd.Parameters.AddWithValue("@Aux3", duplicate.Aux3 ?? "");
                        insertCmd.Parameters.AddWithValue("@BaseUnit", duplicate.BaseUnit);
                        insertCmd.Parameters.AddWithValue("@BudgetHoursGroup", duplicate.BudgetHoursGroup);
                        insertCmd.Parameters.AddWithValue("@BudgetHoursROC", duplicate.BudgetHoursROC);
                        insertCmd.Parameters.AddWithValue("@BudgetMHs", duplicate.BudgetMHs);
                        insertCmd.Parameters.AddWithValue("@ChgOrdNO", duplicate.ChgOrdNO ?? "");
                        insertCmd.Parameters.AddWithValue("@ClientBudget", duplicate.ClientBudget);
                        insertCmd.Parameters.AddWithValue("@ClientCustom3", duplicate.ClientCustom3);
                        insertCmd.Parameters.AddWithValue("@ClientEquivQty", duplicate.ClientEquivQty);
                        insertCmd.Parameters.AddWithValue("@CompType", duplicate.CompType ?? "");
                        insertCmd.Parameters.AddWithValue("@CreatedBy", duplicate.CreatedBy ?? "");
                        insertCmd.Parameters.AddWithValue("@DateTrigger", duplicate.DateTrigger);
                        insertCmd.Parameters.AddWithValue("@Description", duplicate.Description ?? "");
                        insertCmd.Parameters.AddWithValue("@DwgNO", duplicate.DwgNO ?? "");
                        insertCmd.Parameters.AddWithValue("@EarnQtyEntry", duplicate.EarnQtyEntry);
                        insertCmd.Parameters.AddWithValue("@EarnedMHsRoc", duplicate.EarnedMHsRoc);
                        insertCmd.Parameters.AddWithValue("@EqmtNO", duplicate.EqmtNO ?? "");
                        insertCmd.Parameters.AddWithValue("@EquivQTY", duplicate.EquivQTY);
                        insertCmd.Parameters.AddWithValue("@EquivUOM", duplicate.EquivUOM ?? "");
                        insertCmd.Parameters.AddWithValue("@Estimator", duplicate.Estimator ?? "");
                        insertCmd.Parameters.AddWithValue("@HexNO", duplicate.HexNO);
                        insertCmd.Parameters.AddWithValue("@HtTrace", duplicate.HtTrace ?? "");
                        insertCmd.Parameters.AddWithValue("@InsulType", duplicate.InsulType ?? "");
                        insertCmd.Parameters.AddWithValue("@LineNumber", duplicate.LineNumber ?? "");
                        insertCmd.Parameters.AddWithValue("@LocalDirty", duplicate.LocalDirty);
                        insertCmd.Parameters.AddWithValue("@MtrlSpec", duplicate.MtrlSpec ?? "");
                        insertCmd.Parameters.AddWithValue("@Notes", duplicate.Notes ?? "");
                        insertCmd.Parameters.AddWithValue("@PaintCode", duplicate.PaintCode ?? "");
                        insertCmd.Parameters.AddWithValue("@PercentEntry", duplicate.PercentEntry);
                        insertCmd.Parameters.AddWithValue("@PhaseCategory", duplicate.PhaseCategory ?? "");
                        insertCmd.Parameters.AddWithValue("@PhaseCode", duplicate.PhaseCode ?? "");
                        insertCmd.Parameters.AddWithValue("@PipeGrade", duplicate.PipeGrade ?? "");
                        insertCmd.Parameters.AddWithValue("@PipeSize1", duplicate.PipeSize1);
                        insertCmd.Parameters.AddWithValue("@PipeSize2", duplicate.PipeSize2);
                        insertCmd.Parameters.AddWithValue("@PrevEarnMHs", duplicate.PrevEarnMHs);
                        insertCmd.Parameters.AddWithValue("@PrevEarnQTY", duplicate.PrevEarnQTY);
                        insertCmd.Parameters.AddWithValue("@ProgDate", duplicate.ProgDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@ProjectID", duplicate.ProjectID ?? "");
                        insertCmd.Parameters.AddWithValue("@Quantity", duplicate.Quantity);
                        insertCmd.Parameters.AddWithValue("@RevNO", duplicate.RevNO ?? "");
                        insertCmd.Parameters.AddWithValue("@RFINO", duplicate.RFINO ?? "");
                        insertCmd.Parameters.AddWithValue("@ROCBudgetQTY", duplicate.ROCBudgetQTY);
                        insertCmd.Parameters.AddWithValue("@ROCID", duplicate.ROCID);
                        insertCmd.Parameters.AddWithValue("@ROCPercent", duplicate.ROCPercent);
                        insertCmd.Parameters.AddWithValue("@ROCStep", duplicate.ROCStep ?? "");
                        insertCmd.Parameters.AddWithValue("@SchedActNO", duplicate.SchedActNO ?? "");
                        insertCmd.Parameters.AddWithValue("@SchFinish", duplicate.SchFinish?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@SchStart", duplicate.SchStart?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@SecondActno", duplicate.SecondActno ?? "");
                        insertCmd.Parameters.AddWithValue("@SecondDwgNO", duplicate.SecondDwgNO ?? "");
                        insertCmd.Parameters.AddWithValue("@Service", duplicate.Service ?? "");
                        insertCmd.Parameters.AddWithValue("@ShopField", duplicate.ShopField ?? "");
                        insertCmd.Parameters.AddWithValue("@ShtNO", duplicate.ShtNO ?? "");
                        insertCmd.Parameters.AddWithValue("@SubArea", duplicate.SubArea ?? "");
                        insertCmd.Parameters.AddWithValue("@PjtSystem", duplicate.PjtSystem ?? "");
                        insertCmd.Parameters.AddWithValue("@SystemNO", duplicate.SystemNO ?? "");
                        insertCmd.Parameters.AddWithValue("@TagNO", duplicate.TagNO ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF1", duplicate.UDF1 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF2", duplicate.UDF2 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF3", duplicate.UDF3 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF4", duplicate.UDF4 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF5", duplicate.UDF5 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF6", duplicate.UDF6 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF7", duplicate.UDF7);
                        insertCmd.Parameters.AddWithValue("@UDF8", duplicate.UDF8 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF9", duplicate.UDF9 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF10", duplicate.UDF10 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF11", duplicate.UDF11 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF12", duplicate.UDF12 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF13", duplicate.UDF13 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF14", duplicate.UDF14 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF15", duplicate.UDF15 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF16", duplicate.UDF16 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF17", duplicate.UDF17 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF18", duplicate.UDF18 ?? "");
                        insertCmd.Parameters.AddWithValue("@UDF20", duplicate.UDF20 ?? "");
                        insertCmd.Parameters.AddWithValue("@UpdatedBy", duplicate.UpdatedBy ?? "");
                        insertCmd.Parameters.AddWithValue("@UpdatedUtcDate", duplicate.UpdatedUtcDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"));
                        insertCmd.Parameters.AddWithValue("@UOM", duplicate.UOM ?? "");
                        insertCmd.Parameters.AddWithValue("@WeekEndDate", duplicate.WeekEndDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@WorkPackage", duplicate.WorkPackage ?? "");
                        insertCmd.Parameters.AddWithValue("@XRay", duplicate.XRay);
                        insertCmd.Parameters.AddWithValue("@SyncVersion", duplicate.SyncVersion);

                        insertCmd.ExecuteNonQuery();
                        successCount++;
                        sequence++;
                    }

                    transaction.Commit();

                    AppLogger.Info($"Duplicated {successCount} record(s)", "Duplicate Rows", currentUser);

                    await _viewModel.RefreshAsync();
                    UpdateRecordCount();

                    MessageBox.Show($"Successfully duplicated {successCount} record(s).\n\nThe new records are assigned to you and marked for sync.",
                        "Duplication Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    AppLogger.Error(ex, "Duplicate Rows", currentUser);
                    MessageBox.Show($"Duplication failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Duplicate Rows", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Duplication failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuExportSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected activities from the grid
                var selectedRecords = sfActivities.SelectedItems.Cast<Activity>().ToList();

                if (selectedRecords.Count == 0)
                {
                    MessageBox.Show(
                        "No records selected. Please select one or more rows to export.",
                        "Export Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Call export helper
                await ExportHelper.ExportSelectedActivitiesAsync(
                    Window.GetWindow(this),
                    selectedRecords);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Export Selected Click", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlaceHolder1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("coming soon!",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Placeholder2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("coming soon!",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public class GridPreferences
        {
            public int Version { get; set; } = 1;
            public string SchemaHash { get; set; } = "";
            public List<GridColumnPref> Columns { get; set; } = new();
        }

        public class GridColumnPref
        {
            public string Name { get; set; } = "";
            public int OrderIndex { get; set; }
            public double Width { get; set; }
            public bool IsHidden { get; set; }
        }

        // ---- Helpers ----
        private static string ComputeSchemaHash(Syncfusion.UI.Xaml.Grid.SfDataGrid grid)
        {
            using var sha = SHA256.Create();
            var names = string.Join("|", grid.Columns.Select(c => c.MappingName).OrderBy(n => n));
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(names)));
        }



        public ProgressView()
        {
            InitializeComponent();
            // Apply FluentDark theme to grid
            Syncfusion.SfSkinManager.SfSkinManager.SetTheme(sfActivities, new Syncfusion.SfSkinManager.Theme("FluentDark"));
            // Hook into Syncfusion's filter changed event
            sfActivities.FilterChanged += SfActivities_FilterChanged;
            sfActivities.CurrentCellBeginEdit += SfActivities_CurrentCellBeginEdit;
            sfActivities.GridCopyContent += SfActivities_GridCopyContent;
            sfActivities.GridPasteContent += SfActivities_GridPasteContent;
            // VM
            _viewModel = new ProgressViewModel();
            this.DataContext = _viewModel;
            sfActivities.ItemsSource = _viewModel.ActivitiesView;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            //InitializeColumnVisibility();
            // InitializeColumnTooltips();
            UpdateRecordCount();

            // Your data-population logic
            this.Loaded += OnViewLoaded;

            // Custom button values
            LoadCustomPercentButtons();

            // IMPORTANT: load layout AFTER the view is loaded and columns are realized
            this.Loaded += (_, __) =>
            {
                sfActivities.Opacity = 0; // Hide grid during loading to prevent flicker
                // Let layout/render complete, then apply prefs
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadColumnState();
                    sfActivities.Opacity = 1; // Show grid after state is loaded
                }),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            };

            // Save when view closes
            this.Unloaded += (_, __) => SaveColumnState();

            sfActivities.QueryColumnDragging += (s, e) =>
            {
                if (e.Reason == Syncfusion.UI.Xaml.Grid.QueryColumnDraggingReason.Dropped)
                {
                    // Column was just dropped - save immediately
                    Dispatcher.BeginInvoke(new Action(() => SaveColumnState()),
                        System.Windows.Threading.DispatcherPriority.Background);
                }
            };
            // *** NEW: create timer here so it is always non-null ***
            _resizeSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _resizeSaveTimer.Tick += ResizeSaveTimer_Tick;
            SetupColumnResizeSave();
        }
        private void ResizeSaveTimer_Tick(object? sender, EventArgs e)
        {
            _resizeSaveTimer.Stop();
            SaveColumnState();
        }
        // Handle paste to ensure cell enters edit mode and value persists
        private void SfActivities_GridPasteContent(object? sender, GridCopyPasteEventArgs e)
        {
            // Get the current cell
            var currentCell = sfActivities.SelectionController.CurrentCellManager.CurrentCell;
            if (currentCell == null)
                return;

            // Check permission to edit this record
            var activity = sfActivities.SelectedItem as Activity;
            if (activity == null)
                return;

            bool canEdit = string.Equals(activity.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);
            if (!canEdit)
            {
                e.Handled = true; // Block paste for records user doesn't own
                return;
            }

            // If not in edit mode, enter edit mode so paste triggers normal edit flow
            if (!currentCell.IsEditing)
            {
                sfActivities.SelectionController.CurrentCellManager.BeginEdit();
            }
        }

        // Handle copy to ensure only cell value is copied when in single-cell context
        private void SfActivities_GridCopyContent(object? sender, GridCopyPasteEventArgs e)
        {
            // Get the current cell
            var currentCell = sfActivities.SelectionController.CurrentCellManager.CurrentCell;
            if (currentCell == null)
                return;

            // If not in edit mode, enter edit mode so copy gets just the cell value
            if (!currentCell.IsEditing)
            {
                sfActivities.SelectionController.CurrentCellManager.BeginEdit();
            }
        }
        private void SfActivities_FilterChanged(object? sender, Syncfusion.UI.Xaml.Grid.GridFilterEventArgs e)

        {
            // Update FilteredCount based on Syncfusion's filtered records
            if (sfActivities.View != null)
            {
                var filteredCount = sfActivities.View.Records.Count;
                _viewModel.FilteredCount = filteredCount;
                System.Diagnostics.Debug.WriteLine($">>> Syncfusion filter changed: {filteredCount} records");
                UpdateRecordCount(); // Ensure UI label updates
                UpdateSummaryPanel(); // <-- update summary panel on filter change
            }
        }
        private readonly DispatcherTimer _resizeSaveTimer;


        private void SetupColumnResizeSave()
        {
            // Hook into column width changes
            foreach (var column in sfActivities.Columns)
            {
                var descriptor = DependencyPropertyDescriptor
                    .FromProperty(Syncfusion.UI.Xaml.Grid.GridColumn.WidthProperty,
                                  typeof(Syncfusion.UI.Xaml.Grid.GridColumn));

                descriptor?.AddValueChanged(column, (sender, args) =>
                {
                    _resizeSaveTimer.Stop();
                    _resizeSaveTimer.Start();
                });
            }
        }


        // === PERCENT BUTTON HANDLERS ===

        /// Load custom percent values from user settings on form load
        private void LoadCustomPercentButtons()
        {
            // Load button 1
            string value1 = SettingsManager.GetUserSetting(App.CurrentUserID, "CustomPercentButton1");
            if (!string.IsNullOrEmpty(value1) && int.TryParse(value1, out int percent1))
            {
                btnSetPercent100.Content = $"{percent1}%";
            }

            // Load button 2
            string value2 = SettingsManager.GetUserSetting(App.CurrentUserID, "CustomPercentButton2");
            if (!string.IsNullOrEmpty(value2) && int.TryParse(value2, out int percent2))
            {
                btnSetPercent0.Content = $"{percent2}%";
            }
        }
        /// Save current column configuration to user settings
        private void SaveColumnState()
        {
            try
            {
                if (sfActivities?.Columns == null || sfActivities.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("SaveColumnState: no columns to save.");
                    return;
                }

                var prefs = new GridPreferences
                {
                    Version = 1,
                    SchemaHash = ComputeSchemaHash(sfActivities),
                    Columns = sfActivities.Columns
                        .Select(c => new GridColumnPref
                        {
                            Name = c.MappingName,
                            OrderIndex = sfActivities.Columns.IndexOf(c),
                            Width = c.Width,
                            IsHidden = c.IsHidden
                        })
                        .ToList()
                };

                var json = JsonSerializer.Serialize(prefs);
                SettingsManager.SetUserSetting(App.CurrentUserID, GridPrefsKey, json, "json");
                System.Diagnostics.Debug.WriteLine($"SaveColumnState: saved {prefs.Columns.Count} cols, hash={prefs.SchemaHash}, key={GridPrefsKey}\n{json}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveColumnState error: {ex}");
            }
        }

        private void LoadColumnState()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LoadColumnState START ===");

                if (sfActivities?.Columns == null || App.CurrentUserID <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("LoadColumnState SKIPPED: Grid or UserID invalid");
                    return;
                }

                var raw = SettingsManager.GetUserSetting(App.CurrentUserID, GridPrefsKey);
                System.Diagnostics.Debug.WriteLine($"Raw JSON from DB: {(string.IsNullOrWhiteSpace(raw) ? "NULL/EMPTY" : "EXISTS")}");

                if (string.IsNullOrWhiteSpace(raw))
                {
                    System.Diagnostics.Debug.WriteLine("No grid prefs found; using XAML defaults.");
                    return;
                }

                GridPreferences? prefs = null;
                try { prefs = JsonSerializer.Deserialize<GridPreferences>(raw); }
                catch { System.Diagnostics.Debug.WriteLine("LoadColumnState: invalid JSON (using XAML defaults)."); }

                if (prefs == null)
                {
                    System.Diagnostics.Debug.WriteLine("LoadColumnState: invalid JSON (using XAML defaults).");
                    return;
                }

                var currentHash = ComputeSchemaHash(sfActivities);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                {
                    System.Diagnostics.Debug.WriteLine($"LoadColumnState: schema mismatch (saved {prefs.SchemaHash} vs current {currentHash}). Defaults kept.");
                    return;
                }

                var byName = sfActivities.Columns.ToDictionary(c => c.MappingName, c => c);

                //1) Visibility first
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.IsHidden = p.IsHidden;

                //2) Order (move columns to target positions)
                var orderedPrefs = prefs.Columns.OrderBy(x => x.OrderIndex).ToList();
                for (int target = 0; target < orderedPrefs.Count; target++)
                {
                    var p = orderedPrefs[target];
                    if (!byName.TryGetValue(p.Name, out var col)) continue;
                    int cur = sfActivities.Columns.IndexOf(col);
                    if (cur != target && cur >= 0)
                    {
                        sfActivities.Columns.RemoveAt(cur);
                        sfActivities.Columns.Insert(target, col);
                    }
                }

                //3) Width last (guard against tiny widths)
                const double MinWidth = 40.0;
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.Width = Math.Max(MinWidth, p.Width);

                sfActivities.UpdateLayout(); // Force layout update
/*                InitializeColumnVisibility();*/ // Sync sidebar checkboxes
                System.Diagnostics.Debug.WriteLine($"LoadColumnState: applied {prefs.Columns.Count} cols, key={GridPrefsKey}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadColumnState error: {ex}");
            }
        }





        /// Left-click: Set selected records to button's percent value

        private async void BtnSetPercent_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                MessageBox.Show(
                    "Button source was not recognized.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                AppLogger.Warning(
                    "BtnSetPercent_Click called with non-Button sender.",
                    "ProgressView.BtnSetPercent_Click",
                    App.CurrentUser?.Username ?? "UnknownUser");

                return;
            }

            // Safely get content as string (never null)
            string buttonContent = button.Content?.ToString() ?? string.Empty;

            // Expect content like "0%", "50%", "100%"
            if (!int.TryParse(buttonContent.TrimEnd('%'), out int percent))
            {
                MessageBox.Show(
                    "Invalid percent value.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                AppLogger.Warning(
                    $"BtnSetPercent_Click invalid button content: '{buttonContent}'",
                    "ProgressView.BtnSetPercent_Click",
                    App.CurrentUser?.Username ?? "UnknownUser");

                return;
            }

            await SetSelectedRecordsPercent(percent);
        }



        /// Context menu: Reset button to default value

        private void MenuItem_ResetPercent_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem?.Tag == null)
                return;

            // Safe cast
            var contextMenu = menuItem.Parent as ContextMenu;
            var button = contextMenu?.PlacementTarget as Button;
            if (button == null)
                return;

            // This was your warning:
            // int defaultValue = int.Parse(menuItem.Tag.ToString());

            if (!int.TryParse(menuItem.Tag?.ToString() ?? "", out int defaultValue))
            {
                MessageBox.Show("Invalid default value.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Safe parse tagParts
            var tagParts = (button.Tag?.ToString() ?? "").Split('|');
            if (tagParts.Length != 3)
                return;

            string settingKey = tagParts[1];

            // Update button
            button.Content = $"{defaultValue}%";

            SettingsManager.SetUserSetting(App.CurrentUserID,
                settingKey,
                defaultValue.ToString(),
                "int");

            MessageBox.Show($"Button reset to {defaultValue}%", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }



        /// Context menu: Set custom percent value

        private void MenuItem_CustomPercent_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null)
                return;

            var contextMenu = menuItem.Parent as ContextMenu;
            var button = contextMenu?.PlacementTarget as Button;
            if (button == null)
                return;

            var tagParts = (button.Tag?.ToString() ?? "").Split('|');
            if (tagParts.Length != 3)
            {
                MessageBox.Show("Button configuration error.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string buttonName = tagParts[0];
            string settingKey = tagParts[1];

            string rawValue = button.Content?.ToString()?.TrimEnd('%') ?? "";
            int.TryParse(rawValue, out int currentPercent);

            var dialog = new CustomPercentDialog(currentPercent)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                int newPercent = dialog.PercentValue;

                button.Content = $"{newPercent}%";

                SettingsManager.SetUserSetting(
                    App.CurrentUserID,
                    settingKey,
                    newPercent.ToString(),
                    "int"
                );

                MessageBox.Show($"{buttonName} updated to {newPercent}%",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }


        // Keep your existing SetSelectedRecordsPercent helper method
        private async Task SetSelectedRecordsPercent(int percent)
        {
            var selectedActivities = sfActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Filter to only records the current user can edit
                var editableActivities = selectedActivities.Where(a =>
                   string.Equals(a.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase)
                       ).ToList();

                if (!editableActivities.Any())
                {
                    // User selected only other users' records - silently do nothing
                    return;
                }

                int successCount = 0;
                foreach (var activity in editableActivities)
                {
                    activity.PercentEntry = percent;

                    // Update ALL tracking fields on the in-memory object
                    activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                    activity.UpdatedUtcDate = DateTime.UtcNow;
                    activity.LocalDirty = 1;

                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success) successCount++;
                }

                // Refresh grid to show updated values
                sfActivities.View.Refresh();
                UpdateSummaryPanel();

                // Only show message if records were actually updated
                if (successCount > 0)
                {
                    MessageBox.Show($"Set {successCount} record(s) to {percent}%.",
                   "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)

        {
            // Reset progress bar when loading starts
            if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                if (_viewModel.IsLoading)
                {
                    // Reset to 0 when loading starts
                    loadingProgressBar.Value = 0;
                }
            }

            // Update record count when filtered or total counts change
            if (e.PropertyName == nameof(_viewModel.TotalRecordCount) ||
                e.PropertyName == nameof(_viewModel.FilteredCount))
            {
                UpdateRecordCount();

                // Only animate if we're currently loading
                if (_viewModel.IsLoading)
                {
                    AnimateProgressBar();
                }
            }

            // Update summary totals when they change
            if (e.PropertyName == nameof(_viewModel.BudgetedMHs) ||
                e.PropertyName == nameof(_viewModel.EarnedMHs) ||
                e.PropertyName == nameof(_viewModel.PercentComplete))
            {
                // Summary panel bindings will update automatically
            }
        }

        private void AnimateProgressBar()
        {
            // Very slow animation - grows steadily but will disappear when IsLoading becomes false
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = _viewModel.TotalRecordCount,
                Duration = TimeSpan.FromSeconds(5), // Very slow - 15 seconds to complete
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                }
            };

            loadingProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
        }
        // Update summary totals based on currently visible (filtered) records
        private async void UpdateSummaryPanel()
        {
            // Get the records to calculate from based on current filter state
            List<Activity> recordsToSum;

            // Check if there's active filtering by comparing counts
            bool hasActiveFilter = false;
            if (sfActivities?.View?.Records != null && _viewModel?.Activities != null)
            {
                hasActiveFilter = (sfActivities.View.Records.Count != _viewModel.Activities.Count);
            }

            if (hasActiveFilter && sfActivities?.View?.Records != null)
            {
                System.Diagnostics.Debug.WriteLine($"About to loop through {sfActivities.View.Records.Count} records");
                // Filter is active - extract filtered activities
                recordsToSum = new List<Activity>();

                foreach (var record in sfActivities.View.Records)
                {
                    // Syncfusion wraps records in RecordEntry - extract the Data property
                    var recordType = record.GetType();
                    var dataProperty = recordType.GetProperty("Data");
                    if (dataProperty != null)
                    {
                        var activity = dataProperty.GetValue(record) as Activity;
                        if (activity != null)
                        {
                            recordsToSum.Add(activity);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"UpdateSummaryPanel: Extracted {recordsToSum.Count} filtered activities");
            }
            else if (_viewModel?.Activities != null && _viewModel.Activities.Count > 0)
            {
                // No filter or filter not yet applied - use all records
                recordsToSum = _viewModel.Activities.ToList();
            }
            else
            {
                // No records at all
                recordsToSum = new List<Activity>();
            }
            // Debug: Check what we're actually summing
            System.Diagnostics.Debug.WriteLine($"UpdateSummaryPanel: About to sum {recordsToSum.Count} activities");
            if (recordsToSum.Any())
            {
                double testBudget = recordsToSum.Sum(a => a.BudgetMHs);
                double testEarned = recordsToSum.Sum(a => a.EarnMHsCalc);
                System.Diagnostics.Debug.WriteLine($"  Test sum: Budget={testBudget:N2}, Earned={testEarned:N2}");
            }
            System.Diagnostics.Debug.WriteLine($"=== UpdateSummaryPanel ===");
            System.Diagnostics.Debug.WriteLine($"recordsToSum.Count = {recordsToSum.Count}");
            if (recordsToSum.Any())
            {
                var first = recordsToSum[0];
                System.Diagnostics.Debug.WriteLine($"First record: PercentEntry={first.PercentEntry}, BudgetMHs={first.BudgetMHs}, EarnMHsCalc={first.EarnMHsCalc}");

                double testBudget = recordsToSum.Sum(a => a.BudgetMHs);
                double testEarned = recordsToSum.Sum(a => a.EarnMHsCalc);
                System.Diagnostics.Debug.WriteLine($"Sum: Budget={testBudget:N2}, Earned={testEarned:N2}");
            }
            // Call ViewModel method to update bound properties
            if (_viewModel != null)
            {
                await _viewModel.UpdateTotalsAsync(recordsToSum);
            }
        }

        /// Auto-save when user finishes editing a cell

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {


            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdateSummaryPanel(); 
            await CalculateMetadataErrorCount(); 
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }


        /// Extract property name from column (tries binding path first, then header text)

        private string GetColumnPropertyName(Syncfusion.UI.Xaml.Grid.GridColumn column)
        {
            // Syncfusion columns use MappingName for the property binding
            if (!string.IsNullOrEmpty(column.MappingName))
            {
                string propertyName = column.MappingName;

                // Strip _Display suffix if present (for display wrapper properties)
                if (propertyName.EndsWith("_Display"))
                {
                    propertyName = propertyName.Replace("_Display", "");
                }

                return propertyName;
            }

            // Fallback to HeaderText if MappingName is empty
            if (!string.IsNullOrEmpty(column.HeaderText))
            {
                return column.HeaderText;
            }

            return "Unknown";
        }

        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            string? columnName = checkBox.Content?.ToString();
            if (string.IsNullOrEmpty(columnName) || !_columnMap.ContainsKey(columnName))
                return;

            var column = _columnMap[columnName];

            // Syncfusion uses IsHidden (true = hidden, false = visible)
            column.IsHidden = !(checkBox.IsChecked == true);

            // Force layout update
            sfActivities.UpdateLayout();
            SaveColumnState();  // Save when visibility changes
        }
        private void UpdateRecordCount()
        {
            if (txtFilteredCount == null) return;

            var filteredCount = _viewModel.FilteredCount;
            var totalCount = _viewModel.TotalRecordCount;

            // Show filtered count
            if (filteredCount == totalCount)
            {
                txtFilteredCount.Text = $"{filteredCount:N0} records";
            }
            else
            {
                txtFilteredCount.Text = $"{filteredCount:N0} of {totalCount:N0} records";
            }
        }





        // === FILTER EVENT HANDLERS ===

        // Add this method to ProgressView.xaml.cs

        private void btnChangedRows_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter based on whether predicates already exist
            bool filterActive = sfActivities.Columns["LocalDirty"].FilterPredicates.Count > 0;

            if (!filterActive)
            {
                // Apply "Changed Rows" filter (LocalDirty = 1)
                sfActivities.Columns["LocalDirty"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 1,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals - active
                btnChangedRows.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Clear this filter
                sfActivities.Columns["LocalDirty"].FilterPredicates.Clear();

                // Update button visuals - inactive
                btnChangedRows.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnFilterUser3_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }
        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Step 1: Check Azure connection with retry option
                bool isConnected = false;
                while (!isConnected)
                {
                    var connectDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking Azure connection...");
                    connectDialog.Show();

                    var (connected, connError) = await Task.Run(() =>
                    {
                        bool result = AzureDbManager.CheckConnection(out string err);
                        return (result, err);
                    });

                    connectDialog.Close();

                    if (connected)
                    {
                        isConnected = true;
                    }
                    else
                    {
                        var retryResult = MessageBox.Show(
                            $"Submit Progress requires connection to Azure.\n\n{connError}\n\nWould you like to retry?",
                            "Connection Failed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (retryResult != MessageBoxResult.Yes)
                            return;
                    }
                }

                // Step 2: Get distinct ProjectIDs for current user's activities
                var userProjects = new List<string>();
                using (var localConn = DatabaseSetup.GetConnection())
                {
                    localConn.Open();
                    var cmd = localConn.CreateCommand();
                    cmd.CommandText = @"
                SELECT DISTINCT ProjectID 
                FROM Activities 
                WHERE AssignedTo = @username 
                  AND ProjectID IS NOT NULL 
                  AND ProjectID != ''";
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        userProjects.Add(reader.GetString(0));
                    }
                }

                if (!userProjects.Any())
                {
                    MessageBox.Show(
                        "You have no activities assigned to submit.",
                        "No Activities",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Step 3: Select project (if multiple)
                string selectedProject;
                if (userProjects.Count == 1)
                {
                    selectedProject = userProjects[0];
                }
                else
                {
                    var projectDialog = new Window
                    {
                        Title = "Select Project",
                        Width = 300,
                        Height = 165,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E"))
                    };

                    var projectCombo = new ComboBox
                    {
                        ItemsSource = userProjects,
                        SelectedIndex = 0,
                        Margin = new Thickness(10),
                        Height = 30
                    };

                    var okBtn = new Button { Content = "OK", Width = 80, Height = 30, Margin = new Thickness(5), IsDefault = true };
                    var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(5), IsCancel = true };

                    var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    btnPanel.Children.Add(okBtn);
                    btnPanel.Children.Add(cancelBtn);

                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock { Text = "Select project to submit:", Margin = new Thickness(10), Foreground = Brushes.White });
                    stack.Children.Add(projectCombo);
                    stack.Children.Add(btnPanel);

                    projectDialog.Content = stack;

                    bool? dialogResult = false;
                    okBtn.Click += (s, args) => { dialogResult = true; projectDialog.Close(); };

                    if (projectDialog.ShowDialog() != true && dialogResult != true)
                        return;

                    selectedProject = projectCombo.SelectedItem as string ?? userProjects[0];
                }

                // Step 4: Date picker dialog (default to previous Sunday)
                var today = DateTime.Today;
                var daysSinceSunday = (int)today.DayOfWeek;
                var previousSunday = today.AddDays(-daysSinceSunday);

                var dateDialog = new Window
                {
                    Title = "Select Week Ending Date",
                    Width = 320,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E"))
                };

                var datePicker = new DatePicker
                {
                    SelectedDate = previousSunday,
                    Margin = new Thickness(10),
                    Height = 30
                };

                var dateOkBtn = new Button { Content = "OK", Width = 80, Height = 30, Margin = new Thickness(5), IsDefault = true };
                var dateCancelBtn = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(5), IsCancel = true };

                var dateBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                dateBtnPanel.Children.Add(dateOkBtn);
                dateBtnPanel.Children.Add(dateCancelBtn);

                var dateStack = new StackPanel();
                dateStack.Children.Add(new TextBlock { Text = "Select week ending date:", Margin = new Thickness(10), Foreground = Brushes.White });
                dateStack.Children.Add(datePicker);
                dateStack.Children.Add(dateBtnPanel);

                dateDialog.Content = dateStack;

                bool? dateDialogResult = false;
                dateOkBtn.Click += (s, args) => { dateDialogResult = true; dateDialog.Close(); };

                if (dateDialog.ShowDialog() != true && dateDialogResult != true)
                    return;

                if (!datePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Please select a valid date.", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedWeekEndDate = datePicker.SelectedDate.Value;
                var weekEndDateStr = selectedWeekEndDate.ToString("yyyy-MM-dd");

                // Check for dates that are after the selected WeekEndDate
                var futureStartDates = _viewModel.Activities
                    .Where(a => a.AssignedTo == App.CurrentUser?.Username &&
                                a.ProjectID == selectedProject &&
                                a.SchStart != null &&
                                a.SchStart.Value.Date > selectedWeekEndDate.Date)
                    .ToList();

                var futureFinishDates = _viewModel.Activities
                    .Where(a => a.AssignedTo == App.CurrentUser?.Username &&
                                a.ProjectID == selectedProject &&
                                a.SchFinish != null &&
                                a.SchFinish.Value.Date > selectedWeekEndDate.Date)
                    .ToList();

                if (futureStartDates.Any() || futureFinishDates.Any())
                {
                    int totalCount = futureStartDates.Count + futureFinishDates.Count;
                    string message = $"Cannot submit progress for week ending {selectedWeekEndDate:MM/dd/yyyy}.\n\n";

                    if (futureStartDates.Any())
                        message += $"• {futureStartDates.Count} activity(s) have Start dates after the selected week\n";
                    if (futureFinishDates.Any())
                        message += $"• {futureFinishDates.Count} activity(s) have Finish dates after the selected week\n";

                    message += "\nPlease fix these dates before submitting, or select a later week end date.";

                    MessageBox.Show(message, "Invalid Dates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Step 5: Check for split SchedActNO ownership
                var splitOwnershipIssues = await OwnershipHelper.CheckSplitOwnershipAsync(
                    selectedProject, App.CurrentUser!.Username);

                if (splitOwnershipIssues.Any())
                {
                    var message = OwnershipHelper.BuildSplitOwnershipMessage(splitOwnershipIssues);
                    var result = MessageBox.Show(message, "Split Ownership Detected", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;

                    var reassignDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Reassigning records...");
                    reassignDialog.Show();

                    try
                    {
                        var reassignProgress = new Progress<string>(status => reassignDialog.UpdateStatus(status));
                        var totalReassigned = await OwnershipHelper.ResolveSplitOwnershipAsync(
                            selectedProject, App.CurrentUser!.Username, splitOwnershipIssues, reassignProgress);

                        reassignDialog.Close();

                        MessageBox.Show(
                            $"Reassigned {totalReassigned} records to their respective owners.\n\nContinuing with submit...",
                            "Records Reassigned",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        reassignDialog.Close();
                        AppLogger.Error(ex, "ProgressView.BtnSubmit_Click.Reassign");
                        MessageBox.Show($"Error reassigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Step 6: Check for existing snapshots on Azure
                var existingExportedBy = await Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var checkCmd = azureConn.CreateCommand();
                    checkCmd.CommandText = @"
                SELECT TOP 1 ExportedBy 
                FROM ProgressSnapshots 
                WHERE AssignedTo = @username 
                  AND ProjectID = @projectId 
                  AND WeekEndDate = @weekEndDate";
                    checkCmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);
                    checkCmd.Parameters.AddWithValue("@projectId", selectedProject);
                    checkCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);

                    return checkCmd.ExecuteScalar();
                });

                bool needsDelete = false;

                if (existingExportedBy != null && existingExportedBy != DBNull.Value)
                {
                    string exportedByUser = existingExportedBy.ToString()!;

                    if (!exportedByUser.Equals(App.CurrentUser!.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            $"Progress for week ending {weekEndDateStr} has already been exported by {exportedByUser}.\n\n" +
                            "You cannot overwrite snapshots exported by another user.",
                            "Already Exported",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    var overwriteResult = MessageBox.Show(
                        $"You already exported progress for week ending {weekEndDateStr}.\n\n" +
                        "Do you want to overwrite with current progress?\n\n" +
                        "Note: This will clear the export lock.",
                        "Overwrite Exported Snapshots?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (overwriteResult != MessageBoxResult.Yes)
                        return;

                    needsDelete = true;
                }
                else
                {
                    var existingCount = await Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var countCmd = azureConn.CreateCommand();
                        countCmd.CommandText = @"
                    SELECT COUNT(*) 
                    FROM ProgressSnapshots 
                    WHERE AssignedTo = @username 
                      AND ProjectID = @projectId 
                      AND WeekEndDate = @weekEndDate";
                        countCmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);
                        countCmd.Parameters.AddWithValue("@projectId", selectedProject);
                        countCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);

                        return Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                    });

                    if (existingCount > 0)
                    {
                        var overwriteResult = MessageBox.Show(
                            $"You already have {existingCount} snapshots for week ending {weekEndDateStr}.\n\n" +
                            "Do you want to overwrite them with current progress?",
                            "Overwrite Existing?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (overwriteResult != MessageBoxResult.Yes)
                            return;

                        needsDelete = true;
                    }
                }

                // Step 7: Show busy dialog - user is committed
                var busyDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Starting...");
                busyDialog.Show();

                var progress = new Progress<string>(status => busyDialog.UpdateStatus(status));
                var currentUser = App.CurrentUser!.Username;
                var progDateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var ownerWindow = Window.GetWindow(this);

                try
                {
                    var (success, submitError, snapshotCount, skippedCount) = await Task.Run(async () =>
                    {
                        var reporter = (IProgress<string>)progress;

                        // Step 8: Force sync to ensure Azure has latest local changes
                        reporter.Report("Syncing data...");
                        var pushResult = await SyncManager.PushRecordsAsync(new List<string> { selectedProject });
                        if (!string.IsNullOrEmpty(pushResult.ErrorMessage))
                            return (false, $"Sync failed during push:\n\n{pushResult.ErrorMessage}", 0, 0);

                        var pullResult = await SyncManager.PullRecordsAsync(new List<string> { selectedProject });
                        if (!string.IsNullOrEmpty(pullResult.ErrorMessage))
                            return (false, $"Sync failed during pull:\n\n{pullResult.ErrorMessage}", 0, 0);

                        // Step 9: Delete existing snapshots if needed
                        if (needsDelete)
                        {
                            reporter.Report("Deleting existing snapshots...");

                            using var deleteConn = AzureDbManager.GetConnection();
                            deleteConn.Open();

                            var deleteCmd = deleteConn.CreateCommand();
                            deleteCmd.CommandText = @"
                        DELETE FROM ProgressSnapshots 
                        WHERE AssignedTo = @username 
                          AND ProjectID = @projectId 
                          AND WeekEndDate = @weekEndDate";
                            deleteCmd.Parameters.AddWithValue("@username", currentUser);
                            deleteCmd.Parameters.AddWithValue("@projectId", selectedProject);
                            deleteCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);
                            var deletedCount = deleteCmd.ExecuteNonQuery();

                            AppLogger.Info($"Deleted {deletedCount} existing snapshots for overwrite", "ProgressView.BtnSubmit_Click", currentUser);
                        }

                        // Step 10: Check for records already submitted by other users
                        reporter.Report("Checking for conflicts...");

                        int skipped = 0;
                        using (var azureConn = AzureDbManager.GetConnection())
                        {
                            azureConn.Open();

                            var conflictCheck = azureConn.CreateCommand();
                            conflictCheck.CommandText = @"
                        SELECT ps.AssignedTo as OriginalSubmitter, COUNT(*) as RecordCount
                        FROM Activities a
                        INNER JOIN ProgressSnapshots ps ON ps.UniqueID = a.UniqueID AND ps.WeekEndDate = @weekEndDate
                        WHERE a.AssignedTo = @username 
                          AND a.ProjectID = @projectId
                          AND a.IsDeleted = 0
                          AND ps.AssignedTo <> @username
                        GROUP BY ps.AssignedTo";
                            conflictCheck.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);
                            conflictCheck.Parameters.AddWithValue("@username", currentUser);
                            conflictCheck.Parameters.AddWithValue("@projectId", selectedProject);

                            var conflicts = new List<(string User, int Count)>();
                            using (var conflictReader = conflictCheck.ExecuteReader())
                            {
                                while (conflictReader.Read())
                                {
                                    conflicts.Add((conflictReader.GetString(0), conflictReader.GetInt32(1)));
                                }
                            }

                            if (conflicts.Any())
                            {
                                skipped = conflicts.Sum(c => c.Count);
                                var conflictDetails = string.Join("\n", conflicts.Select(c => $"  • {c.Count} records by {c.User}"));

                                bool proceed = false;
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    busyDialog.Hide();  // Hide the busy dialog

                                    var result = MessageBox.Show(
                                        ownerWindow,
                                        $"{skipped} of your assigned records were already submitted for this week by other users:\n\n" +
                                        $"{conflictDetails}\n\n" +
                                        $"These records will be SKIPPED. Do you want to submit the remaining records?\n\n" +
                                        $"(You may need to coordinate with the users above if this is unexpected.)",
                                        "Records Already Submitted",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Warning);
                                    proceed = (result == MessageBoxResult.Yes);

                                    if (proceed)
                                        busyDialog.Show();  // Show it again only if continuing
                                });

                                if (!proceed)
                                    return (false, "Cancelled by user", 0, 0);
                            }

                            // Step 11: Copy Activities from Azure to ProgressSnapshots (skip existing)
                            reporter.Report("Creating snapshots...");

                            var insertCmd = azureConn.CreateCommand();
                            insertCmd.CommandText = @"
                        INSERT INTO ProgressSnapshots (
                            UniqueID, WeekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                            Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                            ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                            DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                            EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                            MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                            PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                            ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                            ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                            Service, ShopField, ShtNO, SubArea, PjtSystem, SystemNO, TagNO,
                            UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                            UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                            UpdatedBy, UpdatedUtcDate, UOM, WorkPackage, XRay, ExportedBy, ExportedDate
                        )
                        SELECT 
                            UniqueID, @weekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                            Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                            ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                            DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                            EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                            MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                            PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                            ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                            ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                            Service, ShopField, ShtNO, SubArea, PjtSystem, SystemNO, TagNO,
                            UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                            UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                            UpdatedBy, UpdatedUtcDate, UOM, WorkPackage, XRay, NULL, NULL
                        FROM Activities a
                        WHERE AssignedTo = @username 
                          AND ProjectID = @projectId
                          AND IsDeleted = 0
                          AND NOT EXISTS (
                              SELECT 1 FROM ProgressSnapshots ps 
                              WHERE ps.UniqueID = a.UniqueID 
                                AND ps.WeekEndDate = @weekEndDate
                          )";
                            insertCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);
                            insertCmd.Parameters.AddWithValue("@username", currentUser);
                            insertCmd.Parameters.AddWithValue("@projectId", selectedProject);

                            int snapshots = insertCmd.ExecuteNonQuery();

                            AppLogger.Info($"Created {snapshots} snapshots for week ending {weekEndDateStr} ({skipped} skipped)", "ProgressView.BtnSubmit_Click", currentUser);
                            // Step 13: Purge old snapshots (older than 4 weeks from today)
                            reporter.Report("Cleaning up old snapshots...");

                            var purgeCmd = azureConn.CreateCommand();
                            purgeCmd.CommandText = @"
                                DELETE FROM ProgressSnapshots 
                                WHERE WeekEndDate < @cutoffDate";
                            purgeCmd.Parameters.AddWithValue("@cutoffDate", DateTime.Now.AddDays(-28).ToString("yyyy-MM-dd"));

                            int purgedCount = purgeCmd.ExecuteNonQuery();

                            if (purgedCount > 0)
                            {
                                AppLogger.Info($"Purged {purgedCount} old snapshots (before {DateTime.Now.AddDays(-28):yyyy-MM-dd})",
                                    "ProgressView.BtnSubmit_Click", currentUser);
                            }
                            // Step 12: Update local Activities.WeekEndDate and ProgDate
                            reporter.Report("Updating local records...");

                            using (var localConn = DatabaseSetup.GetConnection())
                            {
                                localConn.Open();
                                var updateLocalCmd = localConn.CreateCommand();
                                updateLocalCmd.CommandText = @"
                            UPDATE Activities 
                            SET WeekEndDate = @weekEndDate,
                                ProgDate = @progDate,
                                LocalDirty = 1
                            WHERE AssignedTo = @username 
                              AND ProjectID = @projectId";
                                updateLocalCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);
                                updateLocalCmd.Parameters.AddWithValue("@progDate", progDateStr);
                                updateLocalCmd.Parameters.AddWithValue("@username", currentUser);
                                updateLocalCmd.Parameters.AddWithValue("@projectId", selectedProject);
                                updateLocalCmd.ExecuteNonQuery();
                            }

                            // Step 13: Push WeekEndDate changes to Azure
                            reporter.Report("Syncing changes...");
                            await SyncManager.PushRecordsAsync(new List<string> { selectedProject });

                            return (true, string.Empty, snapshots, skipped);
                        }
                    });

                    if (!success)
                    {
                        busyDialog.Close();
                        if (submitError != "Cancelled by user")
                        {
                            MessageBox.Show(submitError, "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return;
                    }

                    // Step 14: Refresh grid
                    busyDialog.UpdateStatus("Refreshing...");
                    await RefreshData();

                    busyDialog.Close();

                    if (skippedCount > 0)
                    {
                        MessageBox.Show(
                            $"Submitted {snapshotCount} activities for week ending {weekEndDateStr}.\n\n" +
                            $"{skippedCount} records were skipped because they were already submitted by another user.",
                            "Progress Submitted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Successfully submitted {snapshotCount} activities for week ending {weekEndDateStr}.",
                            "Progress Submitted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch
                {
                    busyDialog.Close();
                    throw;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.BtnSubmit_Click", App.CurrentUser?.Username);

                string userMessage;
                if (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("duplicate key"))
                {
                    userMessage = "Some records have already been submitted for this week by another user.\n\n" +
                                  "This can happen when records were reassigned after another user submitted.\n\n" +
                                  "Please sync your data and try again, or contact your administrator if the problem persists.";
                }
                else
                {
                    userMessage = ex.Message;
                }

                MessageBox.Show(
                    $"Error submitting progress:\n\n{userMessage}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check Azure connection with retry option
                bool isConnected = false;
                while (!isConnected)
                {
                    var connectDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking Azure connection...");
                    connectDialog.Show();

                    var (connected, connError) = await Task.Run(() =>
                    {
                        bool result = AzureDbManager.CheckConnection(out string err);
                        return (result, err);
                    });

                    connectDialog.Close();

                    if (connected)
                    {
                        isConnected = true;
                    }
                    else
                    {
                        var retryResult = MessageBox.Show(
                            $"Cannot sync - Azure database unavailable:\n\n{connError}\n\nWould you like to retry?",
                            "Connection Failed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (retryResult != MessageBoxResult.Yes)
                            return;
                    }
                }

                // Check metadata errors
                await CalculateMetadataErrorCount();
                if (_viewModel.MetadataErrorCount > 0)
                {
                    MessageBox.Show(
                        $"Cannot sync. You have {_viewModel.MetadataErrorCount} record(s) with missing required metadata.\n\n" +
                        "Click 'Metadata Errors' button to view and fix these records.\n\n" +
                        "Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, UDF18",
                        "Metadata Errors",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Check for split ownership across all user's projects
                var userProjects = new List<string>();
                using (var localConn = DatabaseSetup.GetConnection())
                {
                    localConn.Open();
                    var cmd = localConn.CreateCommand();
                    cmd.CommandText = @"
                SELECT DISTINCT ProjectID 
                FROM Activities 
                WHERE AssignedTo = @username 
                  AND ProjectID IS NOT NULL 
                  AND ProjectID != ''";
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        userProjects.Add(reader.GetString(0));
                    }
                }

                var allSplitIssues = new List<(string ProjectID, List<SplitOwnershipIssue> Issues)>();

                if (userProjects.Any())
                {
                    var checkDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking ownership...");
                    checkDialog.Show();

                    foreach (var projectId in userProjects)
                    {
                        var issues = await OwnershipHelper.CheckSplitOwnershipAsync(projectId, App.CurrentUser!.Username);
                        if (issues.Any())
                        {
                            allSplitIssues.Add((projectId, issues));
                        }
                    }

                    checkDialog.Close();
                }

                if (allSplitIssues.Any())
                {
                    // Flatten all issues for the message
                    var flatIssues = allSplitIssues.SelectMany(x => x.Issues).ToList();
                    var message = OwnershipHelper.BuildSplitOwnershipMessage(flatIssues);

                    var result = MessageBox.Show(message, "Split Ownership Detected", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;

                    var reassignDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Reassigning records...");
                    reassignDialog.Show();

                    try
                    {
                        var reassignProgress = new Progress<string>(status => reassignDialog.UpdateStatus(status));
                        int totalReassigned = 0;

                        foreach (var (projectId, issues) in allSplitIssues)
                        {
                            totalReassigned += await OwnershipHelper.ResolveSplitOwnershipAsync(
                                projectId, App.CurrentUser!.Username, issues, reassignProgress);
                        }

                        reassignDialog.Close();

                        MessageBox.Show(
                            $"Reassigned {totalReassigned} records to their respective owners.\n\nContinuing with sync...",
                            "Records Reassigned",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        reassignDialog.Close();
                        AppLogger.Error(ex, "ProgressView.BtnSync_Click.Reassign");
                        MessageBox.Show($"Error reassigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var syncDialog = new SyncDialog();
                bool? dialogResult = syncDialog.ShowDialog();
                if (dialogResult == true)
                {
                    // Refresh grid to show updated LocalDirty and pulled records
                    await RefreshData();

                    // After successful sync - save the timestamp
                    SettingsManager.SetUserSetting(
                        App.CurrentUserID,
                        "LastSyncUtcDate",
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        "text"
                    );

                    // Refresh MainWindow status bar
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateLastSyncDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "ProgressView.BtnSync_Click");
            }
        }
        // Remove: lstColumnVisibility references
        // Remove: InitializeColumnVisibility() 
        // Remove: LstColumnVisibility_SelectionChanged event handler
        // Keep: _columnMap dictionary

        private void BtnColumnVisibility_Click(object sender, RoutedEventArgs e)
        {
            var popup = new Window
            {
                Title = "Column Visibility",
                Width = 350,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = (System.Windows.Media.Brush)FindResource("ControlBackground"),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var listBox = new ListBox
            {
                Background = (System.Windows.Media.Brush)FindResource("ControlBackground"),
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1)
            };

            // Populate with current grid columns
            foreach (var column in sfActivities.Columns)
            {
                string columnName = GetColumnPropertyName(column);
                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = !column.IsHidden,
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White,
                    Tag = column
                };

                checkBox.Checked += (s, args) =>
                {
                    var col = (Syncfusion.UI.Xaml.Grid.GridColumn)((CheckBox)s).Tag;
                    col.IsHidden = false;
                    SaveColumnState();
                };

                checkBox.Unchecked += (s, args) =>
                {
                    var col = (Syncfusion.UI.Xaml.Grid.GridColumn)((CheckBox)s).Tag;
                    col.IsHidden = true;
                    SaveColumnState();
                };

                listBox.Items.Add(checkBox);
            }

            Grid.SetRow(listBox, 0);
            grid.Children.Add(listBox);

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = (System.Windows.Media.Brush)FindResource("AccentColor"),
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor")
            };
            closeButton.Click += (s, args) => popup.Close();

            Grid.SetRow(closeButton, 1);
            grid.Children.Add(closeButton);

            popup.Content = grid;
            popup.ShowDialog();
        }
        // === FILTER EVENT HANDLERS ===

        private void BtnFilterComplete_Click(object sender, RoutedEventArgs e)
        {
            // Check filter state BEFORE clearing - look for EQUALS 100, not just any predicate with 100
            bool filterActive = sfActivities.Columns["PercentEntry"].FilterPredicates.Count > 0 &&
                                sfActivities.Columns["PercentEntry"].FilterPredicates.Any(p =>
                                    p.FilterType == Syncfusion.Data.FilterType.Equals && p.FilterValue.Equals(100.0));

            // Clear all percent filters
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            if (!filterActive)
            {
                // Apply "Complete" filter (PercentEntry = 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Reset other buttons and activate this one
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }
            else
            {
                // Just deactivate this button
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }
        private void btnFilterInProgress_Click(object sender, RoutedEventArgs e)
        {
            // Check filter state BEFORE clearing (looking for BOTH predicates: >0 AND <100)
            bool filterActive = sfActivities.Columns["PercentEntry"].FilterPredicates.Count == 2 &&
                                sfActivities.Columns["PercentEntry"].FilterPredicates.Any(p =>
                                    p.FilterType == Syncfusion.Data.FilterType.GreaterThan && p.FilterValue.Equals(0.0)) &&
                                sfActivities.Columns["PercentEntry"].FilterPredicates.Any(p =>
                                    p.FilterType == Syncfusion.Data.FilterType.LessThan && p.FilterValue.Equals(100.0));

            // Clear all percent filters
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            if (!filterActive)
            {
                // Apply "In Progress" filter (PercentEntry > 0 AND < 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.GreaterThan,
                    FilterValue = 0.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.LessThan,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Reset other buttons and activate this one
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }
            else
            {
                // Just deactivate this button
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnFilterNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // Check filter state BEFORE clearing - look for EQUALS 0, not just any predicate with 0
            bool filterActive = sfActivities.Columns["PercentEntry"].FilterPredicates.Count > 0 &&
                                sfActivities.Columns["PercentEntry"].FilterPredicates.Any(p =>
                                    p.FilterType == Syncfusion.Data.FilterType.Equals && p.FilterValue.Equals(0.0));

            // Clear all percent filters
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            if (!filterActive)
            {
                // Apply "Not Started" filter (PercentEntry = 0)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 0.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Reset other buttons and activate this one
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Just deactivate this button
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter based on whether predicates already exist
            bool filterActive = sfActivities.Columns["AssignedTo"].FilterPredicates.Count > 0;

            if (!filterActive)
            {
                // Apply "My Records" filter (AssignedTo = current username)
                sfActivities.Columns["AssignedTo"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = App.CurrentUser!.Username,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals - active
                btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Clear this filter
                sfActivities.Columns["AssignedTo"].FilterPredicates.Clear();

                // Update button visuals - inactive
                btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private async void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearAllFiltersAsync();
            // Clear all column filters (including column header filters)
            foreach (var column in sfActivities.Columns)
            {
                column.FilterPredicates.Clear();
            }

            // Reset all filter button visuals
            //btnFilterComplete.Content = "Complete";
            btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            //btnFilterInProgress.Content = "In Progress";
            btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            //btnFilterNotStarted.Content = "Not Started";
            btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            //btnFilterMyRecords.Content = "My Records";
            btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }
        // Helper method: Get all users from database
        private List<User> GetAllUsers()
        {
            var users = new List<User>();

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT UserID, Username, FullName FROM Users ORDER BY Username";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add(new User
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        FullName = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.SomeArea");
            }


            return users;
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
            UpdateRecordCount();
            UpdateSummaryPanel(); // Update summary panel after refresh
            await CalculateMetadataErrorCount();

        }
        
        /// Public method to refresh the grid data from the database
        /// Used by MainWindow after bulk operations like resetting LocalDirty
        
        public async Task RefreshData()
        {
            await _viewModel.RefreshAsync();
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        // Capture original value when cell edit begins
        // Helper method to get cached property
        private PropertyInfo? GetCachedProperty(string columnName)
        {
            if (!_propertyCache.ContainsKey(columnName))
            {
                var prop = typeof(Activity).GetProperty(columnName);
                if (prop != null)
                {
                    _propertyCache[columnName] = prop;
                }
                else
                {
                    return null;
                }
            }
            return _propertyCache[columnName];
        }
        /// Prevent editing of records not assigned to current user

        private void SfActivities_CurrentCellBeginEdit(object? sender, CurrentCellBeginEditEventArgs e)

        {
            var activity = sfActivities.SelectedItem as Activity;
            if (activity == null) return;

            // Check if user has permission to edit this record
            bool canEdit = string.Equals(activity.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);

            if (!canEdit)
            {
                e.Cancel = true;
                // Silently prevent editing without showing a message
                return;
            }

            if (sfActivities.CurrentColumn == null) return;

            var property = GetCachedProperty(sfActivities.CurrentColumn.MappingName);
            if (property != null)
            {
                _originalCellValue = property.GetValue(activity);
            }
        }
        /// Auto-save when user finishes editing a cell
        // Auto-save when user finishes editing a cell - ONLY if value changed
        private async void sfActivities_CurrentCellEndEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellEndEditEventArgs e)
        {
            try
            {
                var editedActivity = sfActivities.SelectedItem as Activity;
                if (editedActivity == null)
                    return;

                // Get column from the grid's current column
                if (sfActivities.CurrentColumn == null)
                    return;

                string columnName = sfActivities.CurrentColumn.MappingName;
                var property = GetCachedProperty(columnName);
                if (property == null)
                    return;

                object? currentValue = property.GetValue(editedActivity);

                // Compare with original value - only save if changed
                bool valueChanged = false;

                if (_originalCellValue == null && currentValue != null)
                {
                    valueChanged = true;
                }
                else if (_originalCellValue != null && currentValue == null)
                {
                    valueChanged = true;
                }
                else if (_originalCellValue != null && currentValue != null)
                {
                    if (_originalCellValue is string origStr && currentValue is string currStr)
                    {
                        valueChanged = origStr != currStr;
                    }
                    else if (_originalCellValue is double origDbl && currentValue is double currDbl)
                    {
                        valueChanged = Math.Abs(origDbl - currDbl) > 0.0001;
                    }
                    else if (_originalCellValue is DateTime origDt && currentValue is DateTime currDt)
                    {
                        valueChanged = origDt != currDt;
                    }
                    else
                    {
                        valueChanged = !_originalCellValue.Equals(currentValue);
                    }
                }

                if (!valueChanged)
                {
                    return;
                }
                if (columnName == "PercentEntry")
                {
                    double oldPercent = (_originalCellValue as double?) ?? 0;
                    double newPercent = (currentValue as double?) ?? 0;

                    // 0% → clear both dates
                    if (newPercent == 0)
                    {
                        editedActivity.SchStart = null;
                        editedActivity.SchFinish = null;
                    }
                    // >0 and <100 → set SchStart if null, clear SchFinish
                    else if (newPercent > 0 && newPercent < 100)
                    {
                        if (editedActivity.SchStart == null)
                        {
                            editedActivity.SchStart = DateTime.Today;
                        }
                        editedActivity.SchFinish = null;
                    }
                    // 100% → set both dates if null
                    else if (newPercent >= 100)
                    {
                        if (editedActivity.SchStart == null)
                        {
                            editedActivity.SchStart = DateTime.Today;
                        }
                        if (editedActivity.SchFinish == null)
                        {
                            editedActivity.SchFinish = DateTime.Today;
                        }
                    }
                }
                else if (columnName == "SchStart")
                {
                    // Can't set start if percent is 0
                    if (editedActivity.SchStart != null && editedActivity.PercentEntry == 0)
                    {
                        MessageBox.Show("Cannot set Start date when % Complete is 0.\n\nSet % Complete to greater than 0 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchStart = null;
                        sfActivities.View?.Refresh();
                        return;
                    }

                    // Can't set start in the future
                    if (editedActivity.SchStart != null && editedActivity.SchStart.Value.Date > DateTime.Today)
                    {
                        MessageBox.Show("Start date cannot be in the future.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchStart = DateTime.Today;
                        sfActivities.View?.Refresh();
                    }
                    // Can't set start after finish
                    if (editedActivity.SchStart != null && editedActivity.SchFinish != null &&
                        editedActivity.SchStart.Value.Date > editedActivity.SchFinish.Value.Date)
                    {
                        MessageBox.Show("Start date cannot be after Finish date.\n\nFinish date has been updated to match Start date.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchFinish = editedActivity.SchStart;
                        sfActivities.View?.Refresh();
                    }
                    // Can't clear start if percent > 0
                    if (editedActivity.SchStart == null && editedActivity.PercentEntry > 0)
                    {
                        MessageBox.Show("Cannot clear Start date when % Complete is greater than 0.\n\nSet % Complete to 0 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchStart = DateTime.Today;
                        sfActivities.View?.Refresh();
                        return;
                    }
                }
                else if (columnName == "SchFinish")
                {
                    // Can't set finish if percent < 100
                    if (editedActivity.SchFinish != null && editedActivity.PercentEntry < 100)
                    {
                        MessageBox.Show("Cannot set Finish date when % Complete is less than 100.\n\nSet % Complete to 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchFinish = null;
                        sfActivities.View?.Refresh();
                        return;
                    }

                    // Can't set finish in the future
                    if (editedActivity.SchFinish != null && editedActivity.SchFinish.Value.Date > DateTime.Today)
                    {
                        MessageBox.Show("Finish date cannot be in the future.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchFinish = DateTime.Today;
                        sfActivities.View?.Refresh();
                    }

                    // Can't set finish before start
                    if (editedActivity.SchFinish != null && editedActivity.SchStart != null &&
                        editedActivity.SchFinish.Value.Date < editedActivity.SchStart.Value.Date)
                    {
                        MessageBox.Show("Finish date cannot be before Start date.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchFinish = editedActivity.SchStart;
                        sfActivities.View?.Refresh();
                    }

                    // Can't clear finish if percent is 100
                    if (editedActivity.SchFinish == null && editedActivity.PercentEntry >= 100)
                    {
                        MessageBox.Show("Cannot clear Finish date when % Complete is 100.\n\nSet % Complete to less than 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.SchFinish = DateTime.Today;
                        sfActivities.View?.Refresh();
                        return;
                    }
                }

                editedActivity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                editedActivity.UpdatedUtcDate = DateTime.UtcNow;
                editedActivity.LocalDirty = 1;

                bool success = await ActivityRepository.UpdateActivityInDatabase(editedActivity);

                if (success)
                {
                    UpdateSummaryPanel();
                    System.Diagnostics.Debug.WriteLine($"✓ Auto-saved: ActivityID={editedActivity.ActivityID}, Column={columnName}, UpdatedBy={editedActivity.UpdatedBy}");
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to save changes for Activity {editedActivity.ActivityID}.\nPlease try again.",
                        "Save Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in auto-save: {ex.Message}");
                AppLogger.Error(ex, "sfActivities_CurrentCellEndEdit", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void MenuFindReplaceColumn_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var contextMenuInfo = menuItem.DataContext as Syncfusion.UI.Xaml.Grid.GridColumnContextMenuInfo;
            if (contextMenuInfo == null)
            {
                MessageBox.Show("Could not determine which column was clicked.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var column = contextMenuInfo.Column;
            string columnName = column.MappingName;
            string columnHeader = column.HeaderText;

            // Open Find & Replace dialog
            var dialog = new FindReplaceDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.SetTargetColumn(sfActivities, columnName, columnHeader);

            dialog.ShowDialog();
        }
        private void sfActivities_GridContextMenuOpening(object sender, Syncfusion.UI.Xaml.Grid.GridContextMenuEventArgs e)
        {
            // Only show RecordContextMenu when right-clicking row header
            if (e.ContextMenuType == Syncfusion.UI.Xaml.Grid.ContextMenuType.RecordCell)
            {
                // Cancel context menu for regular cells - only allow on row header
                if (e.RowColumnIndex.ColumnIndex > 0)
                {
                    e.Handled = true;
                }
            }
            // Check if this is a column header and if it's read-only
            else if (e.ContextMenuType == Syncfusion.UI.Xaml.Grid.ContextMenuType.Header)
            {
                var columnIndex = sfActivities.ResolveToGridVisibleColumnIndex(e.RowColumnIndex.ColumnIndex);
                if (columnIndex >= 0 && columnIndex < sfActivities.Columns.Count)
                {
                    var column = sfActivities.Columns[columnIndex];

                    // If column is read-only, cancel the context menu
                    if (VANTAGE.Utilities.ColumnPermissions.IsReadOnly(column.MappingName))
                    {
                        e.Handled = true; // Cancel the entire context menu for read-only columns
                    }
                }
            }
        }
        private void sfActivities_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
		{
			// With SelectionUnit="Any", row header clicks select all cells in the row
			// but don't populate SelectedItems. We need to manually populate it.

			var selectedCells = sfActivities.GetSelectedCells();

			if (selectedCells.Count > 0)
			{
				// Group cells by row to identify which rows are fully or partially selected
				var rowsWithSelectedCells = selectedCells
					.GroupBy(cell => cell.RowData)
					.Select(g => g.Key as Activity)
					.Where(activity => activity != null)
					.Distinct()
					.ToList();

				// Clear and repopulate SelectedItems with rows that have selected cells
				sfActivities.SelectedItems.Clear();
				foreach (var row in rowsWithSelectedCells)
				{
					sfActivities.SelectedItems.Add(row);
				}
			}
			else if (sfActivities.SelectedItems.Count > 0)
			{
				// No cells selected, clear SelectedItems
				sfActivities.SelectedItems.Clear();
			}
		}

        private async void MenuAssignToUser_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = sfActivities.SelectedItems.Cast<Activity>().ToList();
            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to assign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Filter: Only allow assigning records that user has permission to modify
            var allowedActivities = selectedActivities.Where(a =>
                App.CurrentUser!.IsAdmin ||
                a.AssignedTo == App.CurrentUser!.Username
            ).ToList();

            if (!allowedActivities.Any())
            {
                MessageBox.Show("You can only assign your own records.\n\nAdmins can assign any record.",
                    "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (allowedActivities.Count < selectedActivities.Count)
            {
                var result = MessageBox.Show(
                    $"You can only assign {allowedActivities.Count} of {selectedActivities.Count} selected records.\n\n" +
                    $"Records assigned to other users cannot be reassigned.\n\nContinue with allowed records?",
                    "Partial Assignment",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Check for metadata errors in allowed records
            var recordsWithErrors = allowedActivities.Where(a =>
                string.IsNullOrWhiteSpace(a.WorkPackage) ||
                string.IsNullOrWhiteSpace(a.PhaseCode) ||
                string.IsNullOrWhiteSpace(a.CompType) ||
                string.IsNullOrWhiteSpace(a.PhaseCategory) ||
                a.HasInvalidProjectID ||
                string.IsNullOrWhiteSpace(a.SchedActNO) ||
                string.IsNullOrWhiteSpace(a.Description) ||
                string.IsNullOrWhiteSpace(a.ROCStep) ||
                string.IsNullOrWhiteSpace(a.UDF18)
            ).ToList();

            if (recordsWithErrors.Any())
            {
                MessageBox.Show(
                    $"Cannot reassign. {recordsWithErrors.Count} selected record(s) have missing required metadata.\n\n" +
                    "Click 'Metadata Errors' button to view and fix these records.\n\n" +
                    "Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, UDF18",
                    "Metadata Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            // Check for split SchedActNO ownership (per ProjectID)
            var selectedByProjectAndActNO = allowedActivities
                .Where(a => !string.IsNullOrWhiteSpace(a.SchedActNO) && !string.IsNullOrWhiteSpace(a.ProjectID))
                .GroupBy(a => (a.ProjectID!, a.SchedActNO!))
                .ToDictionary(g => g.Key, g => g.Count());

            if (selectedByProjectAndActNO.Any())
            {
                var splitActNOs = new List<(string ProjectID, string SchedActNO, int Selected, int Total)>();

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                foreach (var kvp in selectedByProjectAndActNO)
                {
                    var countCmd = connection.CreateCommand();
                    countCmd.CommandText = @"
            SELECT COUNT(*) FROM Activities 
            WHERE ProjectID = @projectId 
              AND SchedActNO = @schedActNO";
                    countCmd.Parameters.AddWithValue("@projectId", kvp.Key.Item1);
                    countCmd.Parameters.AddWithValue("@schedActNO", kvp.Key.Item2);
                    var totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

                    if (totalCount > kvp.Value)
                    {
                        splitActNOs.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value, totalCount));
                    }
                }

                connection.Close();

                if (splitActNOs.Any())
                {
                    var displayList = splitActNOs
                        .Take(10)
                        .Select(s => $"{s.ProjectID}/{s.SchedActNO} ({s.Selected} of {s.Total} selected)");

                    var message = "The following SchedActNOs have activities that are not selected:\n\n" +
                                  string.Join("\n", displayList);

                    if (splitActNOs.Count > 10)
                        message += $"\n... and {splitActNOs.Count - 10} more";

                    message += "\n\nAll activities sharing a SchedActNO within a project must be reassigned together.\n\n" +
                               "Click YES to automatically include all related activities and continue with assignment.\n" +
                               "Click NO to cancel.";

                    var result = MessageBox.Show(message, "Include Related Activities?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;

                    // Get all activities for the split SchedActNOs and add to allowedActivities
                    var allActivities = _viewModel.Activities;
                    if (allActivities != null && allActivities.Any())
                    {
                        var currentIds = new HashSet<string>(allowedActivities.Select(a => a.UniqueID));

                        foreach (var split in splitActNOs)
                        {
                            var matching = allActivities
                                .Where(a => a.ProjectID == split.ProjectID && a.SchedActNO == split.SchedActNO)
                                .Where(a => !currentIds.Contains(a.UniqueID));

                            foreach (var activity in matching)
                            {
                                // Check permission for newly added activities
                                if (App.CurrentUser!.IsAdmin || activity.AssignedTo == App.CurrentUser!.Username)
                                {
                                    allowedActivities.Add(activity);
                                    currentIds.Add(activity.UniqueID);
                                }
                            }
                        }

                        // Update grid selection to reflect what will be assigned
                        sfActivities.SelectedItems.Clear();
                        foreach (var activity in allowedActivities)
                        {
                            sfActivities.SelectedItems.Add(activity);
                        }
                    }
                }
            }
            // Check for unsaved changes (LocalDirty=1)
            var dirtyRecords = allowedActivities.Where(a => a.LocalDirty == 1).ToList();
            if (dirtyRecords.Any())
            {
                var syncResult = MessageBox.Show(
                    $"{dirtyRecords.Count} of the selected records have unsaved changes.\n\n" +
                    $"You must sync these records as the current owner before reassigning.\n\n" +
                    $"Sync now?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (syncResult != MessageBoxResult.Yes)
                    return;

                MessageBox.Show("Please use the SYNC button to sync your changes first, then try reassignment again.",
                    "Sync Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check connection to Azure with retry option
            bool isConnected = false;
            while (!isConnected)
            {
                var connectDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking Azure connection...");
                connectDialog.Show();

                var (connected, connError) = await Task.Run(() =>
                {
                    bool result = AzureDbManager.CheckConnection(out string err);
                    return (result, err);
                });

                connectDialog.Close();

                if (connected)
                {
                    isConnected = true;
                }
                else
                {
                    var retryResult = MessageBox.Show(
                        $"Cannot connect to Azure database.\n\n{connError}\n\nWould you like to retry?",
                        "Connection Failed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (retryResult != MessageBoxResult.Yes)
                        return;
                }
            }

            // Get list of all users for dropdown
            var allUsers = GetAllUsers().Select(u => u.Username).ToList();
            if (!allUsers.Any())
            {
                MessageBox.Show("No users found in the database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Show user selection dialog
            var dialog = new Window
            {
                Title = "Assign to User",
                Width = 300,
                Height = 165,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E"))
            };

            var comboBox = new ComboBox
            {
                ItemsSource = allUsers,
                SelectedIndex = 0,
                Margin = new Thickness(10),
                Height = 30
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Select user to assign records to:",
                Margin = new Thickness(10),
                Foreground = Brushes.White
            });
            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            bool? dialogResult = false;
            okButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };

            if (dialog.ShowDialog() == true || dialogResult == true)
            {
                string? selectedUser = comboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedUser))
                    return;

                var busyDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Starting...");
                busyDialog.Show();

                var progress = new Progress<string>(status => busyDialog.UpdateStatus(status));
                var currentUser = App.CurrentUser!.Username;
                var isAdmin = App.CurrentUser!.IsAdmin;

                try
                {
                    var (success, resultMessage, updatedCount) = await Task.Run(async () =>
                    {
                        var reporter = (IProgress<string>)progress;

                        // Step 1: Verify ownership at Azure BEFORE making any changes (bulk query)
                        reporter.Report("Verifying ownership...");

                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        // Build temp table with UniqueIDs to check
                        var uniqueIds = allowedActivities.Select(a => a.UniqueID).ToList();

                        var createTempCmd = azureConn.CreateCommand();
                        createTempCmd.CommandText = "CREATE TABLE #CheckOwnership (UniqueID NVARCHAR(50) PRIMARY KEY)";
                        createTempCmd.ExecuteNonQuery();

                        // Bulk insert UniqueIDs into temp table
                        using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(azureConn))
                        {
                            bulkCopy.DestinationTableName = "#CheckOwnership";

                            var dt = new System.Data.DataTable();
                            dt.Columns.Add("UniqueID", typeof(string));
                            foreach (var id in uniqueIds)
                            {
                                dt.Rows.Add(id);
                            }
                            bulkCopy.WriteToServer(dt);
                        }

                        // Query all ownerships in one call
                        var ownershipMap = new Dictionary<string, string>();
                        var ownerQuery = azureConn.CreateCommand();
                        ownerQuery.CommandText = @"
            SELECT a.UniqueID, a.AssignedTo 
            FROM Activities a
            INNER JOIN #CheckOwnership c ON a.UniqueID = c.UniqueID";

                        using (var reader = ownerQuery.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var id = reader.GetString(0);
                                var owner = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                ownershipMap[id] = owner;
                            }
                        }

                        var ownedRecords = new List<Activity>();
                        var deniedRecords = new List<string>();

                        foreach (var activity in allowedActivities)
                        {
                            if (ownershipMap.TryGetValue(activity.UniqueID, out var azureOwner))
                            {
                                if (azureOwner == currentUser || isAdmin)
                                {
                                    ownedRecords.Add(activity);
                                }
                                else
                                {
                                    deniedRecords.Add(activity.UniqueID);
                                }
                            }
                            else
                            {
                                // Record not found in Azure - allow assignment
                                ownedRecords.Add(activity);
                            }
                        }

                        if (deniedRecords.Any())
                        {
                            var deniedMsg = $"{deniedRecords.Count} record(s) could not be reassigned - ownership changed at Azure.\n\n" +
                                           $"First few: {string.Join(", ", deniedRecords.Take(3))}";

                            if (!ownedRecords.Any())
                            {
                                return (false, deniedMsg, 0);
                            }

                            // Continue with owned records - we'll show a warning after
                        }

                        if (!ownedRecords.Any())
                        {
                            return (false, "No records available to reassign.", 0);
                        }

                        // Step 2: Bulk update Azure using temp table
                        reporter.Report("Updating records...");

                        var createUpdateTempCmd = azureConn.CreateCommand();
                        createUpdateTempCmd.CommandText = "CREATE TABLE #UpdateBatch (UniqueID NVARCHAR(50) PRIMARY KEY)";
                        createUpdateTempCmd.ExecuteNonQuery();

                        using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(azureConn))
                        {
                            bulkCopy.DestinationTableName = "#UpdateBatch";

                            var dt = new System.Data.DataTable();
                            dt.Columns.Add("UniqueID", typeof(string));
                            foreach (var activity in ownedRecords)
                            {
                                dt.Rows.Add(activity.UniqueID);
                            }
                            bulkCopy.WriteToServer(dt);
                        }

                        var updateCmd = azureConn.CreateCommand();
                        updateCmd.CommandText = @"
            UPDATE a
            SET AssignedTo = @newOwner, 
                UpdatedBy = @updatedBy, 
                UpdatedUtcDate = @updatedDate,
                SyncVersion = SyncVersion + 1
            FROM Activities a
            INNER JOIN #UpdateBatch b ON a.UniqueID = b.UniqueID";
                        updateCmd.Parameters.AddWithValue("@newOwner", selectedUser);
                        updateCmd.Parameters.AddWithValue("@updatedBy", currentUser);
                        updateCmd.Parameters.AddWithValue("@updatedDate", DateTime.UtcNow.ToString("o"));
                        var updated = updateCmd.ExecuteNonQuery();

                        azureConn.Close();

                        AppLogger.Info($"Reassigned {updated} records to {selectedUser}", "ProgressView.MenuAssignToUser_Click", currentUser);

                        // Step 3: Pull updated records back to local
                        reporter.Report("Syncing changes...");

                        var projectIds = ownedRecords
                            .Select(a => a.ProjectID)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Cast<string>()
                            .Distinct()
                            .ToList();

                        await SyncManager.PullRecordsAsync(projectIds);

                        return (true, string.Empty, updated);
                    });

                    if (!success)
                    {
                        busyDialog.Close();
                        MessageBox.Show(resultMessage, "Assignment Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Refresh grid (must be on UI thread)
                    busyDialog.UpdateStatus("Refreshing...");
                    await RefreshData();

                    // Send email notification to assignee
                    busyDialog.UpdateStatus("Sending notification...");
                    bool emailSent = false;

                    // Only send email if assigning to someone else (not self)
                    if (selectedUser != currentUser)
                    {
                        // Get recipient's email and full name from Users table
                        string? recipientEmail = null;
                        string recipientName = selectedUser;

                        using (var localConn = DatabaseSetup.GetConnection())
                        {
                            localConn.Open();
                            var userCmd = localConn.CreateCommand();
                            userCmd.CommandText = "SELECT Email, FullName FROM Users WHERE Username = @username";
                            userCmd.Parameters.AddWithValue("@username", selectedUser);

                            using var reader = userCmd.ExecuteReader();
                            if (reader.Read())
                            {
                                recipientEmail = reader.IsDBNull(0) ? null : reader.GetString(0);
                                string fullName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                if (!string.IsNullOrWhiteSpace(fullName))
                                    recipientName = fullName;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(recipientEmail))
                        {
                            // Get project IDs from assigned records
                            var projectIds = allowedActivities
                                .Select(a => a.ProjectID)
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .Distinct()
                                .ToList()!;

                            emailSent = await EmailService.SendAssignmentNotificationAsync(
                                recipientEmail,
                                recipientName,
                                currentUser,
                                updatedCount,
                                projectIds!);
                        }
                    }

                    busyDialog.Close();

                    string successMessage = $"Successfully reassigned {updatedCount} record(s) to {selectedUser}.";
                    if (selectedUser != currentUser)
                    {
                        successMessage += emailSent
                            ? "\n\nEmail notification sent."
                            : "\n\nEmail notification could not be sent.";
                    }

                    MessageBox.Show(
                        successMessage,
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    busyDialog.Close();
                    MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AppLogger.Error(ex, "ProgressView.MenuAssignToUser_Click");
                }
            }
        }
    }
}