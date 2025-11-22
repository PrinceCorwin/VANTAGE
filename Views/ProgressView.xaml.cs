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
        private Dictionary<string, PropertyInfo> _propertyCache = new Dictionary<string, PropertyInfo>();
        // ProgressView.xaml.cs
        // Replace DeleteSelectedActivities_Click in ProgressView.xaml.cs

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

                // Check connection to Central
                string centralPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");
                if (string.IsNullOrEmpty(centralPath))
                {
                    MessageBox.Show("Central database path not configured.",
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!SyncManager.CheckCentralConnection(centralPath, out string connectionError))
                {
                    MessageBox.Show($"Deletion requires connection to Central database.\n\n{connectionError}\n\nPlease try again when connected.",
                        "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verify ownership in Central for each record
                using var centralConn = new SqliteConnection($"Data Source={centralPath}");
                centralConn.Open();

                var ownedRecords = new List<Activity>();
                var deniedRecords = new List<string>();

                foreach (var activity in selected)
                {
                    var checkCmd = centralConn.CreateCommand();
                    checkCmd.CommandText = "SELECT AssignedTo FROM Activities WHERE UniqueID = @id";
                    checkCmd.Parameters.AddWithValue("@id", activity.UniqueID);
                    var centralOwner = checkCmd.ExecuteScalar()?.ToString();

                    if (isAdmin || string.Equals(centralOwner, currentUser?.Username, StringComparison.OrdinalIgnoreCase))
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
                    centralConn.Close();
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

                // Set IsDeleted=1 in Central (SyncVersion auto-increments via trigger)
                var deleteCentralCmd = centralConn.CreateCommand();
                deleteCentralCmd.CommandText = $@"
            UPDATE Activities 
            SET IsDeleted = 1, 
                UpdatedBy = @user, 
                UpdatedUtcDate = @date 
            WHERE UniqueID IN ({uniqueIdList})";
                deleteCentralCmd.Parameters.AddWithValue("@user", currentUser?.Username ?? "Unknown");
                deleteCentralCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                int centralDeleted = deleteCentralCmd.ExecuteNonQuery();

                centralConn.Close();

                // Refresh grid and totals
                if (ViewModel != null)
                {
                    await ViewModel.RefreshAsync();
                    await ViewModel.UpdateTotalsAsync();
                }

                AppLogger.Info(
                    $"User deleted {localDeleted} activities (IsDeleted=1 set in Central for {centralDeleted} records).",
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
        // PLACEHOLDER HANDLERS (Not Yet Implemented)
        // ========================================
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
                SELECT COUNT(*) FROM Activities 
                WHERE AssignedTo = @currentUser
                  AND (
                    WorkPackage IS NULL OR WorkPackage = '' OR
                    PhaseCode IS NULL OR PhaseCode = '' OR
                    CompType IS NULL OR CompType = '' OR
                    PhaseCategory IS NULL OR PhaseCategory = '' OR
                    ProjectID IS NULL OR ProjectID = '' OR
                    SchedActNO IS NULL OR SchedActNO = '' OR
                    Description IS NULL OR Description = '' OR
                    ROCStep IS NULL OR ROCStep = '' OR
                    UDF18 IS NULL OR UDF18 = ''
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
                            LineNO = original.LineNO,
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
                        HexNO, HtTrace, InsulType, LineNO, LocalDirty, MtrlSpec, Notes, PaintCode,
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
                        @HexNO, @HtTrace, @InsulType, @LineNO, @LocalDirty, @MtrlSpec, @Notes, @PaintCode,
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
                        insertCmd.Parameters.AddWithValue("@LineNO", duplicate.LineNO ?? "");
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
                System.Diagnostics.Debug.WriteLine($"UpdateSummaryPanel: Calculating from {recordsToSum.Count} total records (no filter)");
            }
            else
            {
                // No records at all
                recordsToSum = new List<Activity>();
                System.Diagnostics.Debug.WriteLine("UpdateSummaryPanel: No records available");
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
            await _viewModel.UpdateTotalsAsync(recordsToSum);
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
        //private void InitializeColumnVisibility()
        //{
        //    lstColumnVisibility.Items.Clear();
        //    _columnMap.Clear();

        //    foreach (var column in sfActivities.Columns)
        //    {
        //        // Get property name from mapping name
        //        string columnName = GetColumnPropertyName(column);
        //        _columnMap[columnName] = column;

        //        var checkBox = new CheckBox
        //        {
        //            Content = columnName,
        //            IsChecked = !column.IsHidden, // Syncfusion uses IsHidden instead of Visibility
        //            Margin = new Thickness(5, 2, 5, 2),
        //            Foreground = System.Windows.Media.Brushes.White,
        //            Tag = column
        //        };

        //        checkBox.Checked += ColumnCheckBox_Changed;
        //        checkBox.Unchecked += ColumnCheckBox_Changed;

        //        lstColumnVisibility.Items.Add(checkBox);
        //    }
        //}

        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            string columnName = checkBox.Content?.ToString();
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

        private void BtnFilterUser2_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterUser3_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private async void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check connection FIRST
                string centralPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");
                if (!SyncManager.CheckCentralConnection(centralPath, out string errorMessage))
                {
                    MessageBox.Show(
                        $"Cannot sync - Central database unavailable:\n\n{errorMessage}\n\n" +
                        "Please ensure you have network access and try again.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
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

                var syncDialog = new SyncDialog();
                bool? result = syncDialog.ShowDialog();
                if (result == true)
                {
                    // Refresh grid to show updated LocalDirty and pulled records
                    await RefreshData();
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
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
            }
            else
            {
                // Just deactivate this button
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
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
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
            }
            else
            {
                // Just deactivate this button
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
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
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Just deactivate this button
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
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
                    FilterValue = App.CurrentUser.Username,
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
                btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
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
            btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];

            //btnFilterInProgress.Content = "In Progress";
            btnFilterInProgress.Background = (Brush)Application.Current.Resources["ControlBackground"];

            //btnFilterNotStarted.Content = "Not Started";
            btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];

            //btnFilterMyRecords.Content = "My Records";
            btnFilterMyRecords.Background = (Brush)Application.Current.Resources["ControlBackground"];

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
        private PropertyInfo GetCachedProperty(string columnName)
        {
            if (!_propertyCache.ContainsKey(columnName))
            {
                _propertyCache[columnName] = typeof(Activity).GetProperty(columnName);
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

                object currentValue = property.GetValue(editedActivity);

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
                    System.Diagnostics.Debug.WriteLine($"⊘ No change detected for {columnName}, skipping save");
                    return;
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
        private Activity _lastSelectedRow = null;
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
                App.CurrentUser.IsAdmin ||
                a.AssignedTo == App.CurrentUser.Username
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
                string.IsNullOrWhiteSpace(a.ProjectID) ||
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

            // Check connection to Central
            string centralPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");
            if (!SyncManager.CheckCentralConnection(centralPath, out string errorMessage))
            {
                MessageBox.Show(
                    $"Reassignment requires connection to Central database.\n\n{errorMessage}\n\n" +
                    $"Please ensure you have network access and try again.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
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
                string selectedUser = comboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedUser))
                    return;

                try
                {
                    // Step 1: Verify ownership at Central BEFORE making any changes
                    using var centralConn = new SqliteConnection($"Data Source={centralPath}");
                    centralConn.Open();

                    var ownedRecords = new List<Activity>();
                    var deniedRecords = new List<string>();

                    foreach (var activity in allowedActivities)
                    {
                        var checkCmd = centralConn.CreateCommand();
                        checkCmd.CommandText = "SELECT AssignedTo FROM Activities WHERE UniqueID = @id";
                        checkCmd.Parameters.AddWithValue("@id", activity.UniqueID);
                        var centralOwner = checkCmd.ExecuteScalar()?.ToString();

                        if (centralOwner == App.CurrentUser.Username || App.CurrentUser.IsAdmin)
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
                            $"{deniedRecords.Count} record(s) could not be reassigned - ownership changed at Central.\n\n" +
                            $"First few: {string.Join(", ", deniedRecords.Take(3))}",
                            "Ownership Conflict",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        if (!ownedRecords.Any())
                            return;
                    }

                    // Step 2: Update Central directly with transaction
                    using var transaction = centralConn.BeginTransaction();

                    foreach (var activity in ownedRecords)
                    {
                        var updateCmd = centralConn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = @"
                            UPDATE Activities 
                            SET AssignedTo = @newOwner, 
                                UpdatedBy = @updatedBy, 
                                UpdatedUtcDate = @updatedDate
                            WHERE UniqueID = @id";
                        updateCmd.Parameters.AddWithValue("@newOwner", selectedUser);
                        updateCmd.Parameters.AddWithValue("@updatedBy", App.CurrentUser.Username);
                        updateCmd.Parameters.AddWithValue("@updatedDate", DateTime.UtcNow.ToString("o"));
                        updateCmd.Parameters.AddWithValue("@id", activity.UniqueID);
                        updateCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    centralConn.Close();

                    // Step 3: Pull updated records back to local
                    var projectIds = ownedRecords.Select(a => a.ProjectID).Distinct().ToList();
                    var pullResult = await SyncManager.PullRecordsAsync(centralPath, projectIds);

                    MessageBox.Show(
                        $"Successfully reassigned {ownedRecords.Count} record(s) to {selectedUser}.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Refresh grid
                    await RefreshData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AppLogger.Error(ex, "ProgressView.MenuAssignToUser_Click");
                }
            }
        }
    }
}