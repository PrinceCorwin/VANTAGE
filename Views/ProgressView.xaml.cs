using Microsoft.Data.Sqlite;
using Syncfusion.Data;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.ScrollAxis;
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
using System.Runtime.InteropServices;
using System.Windows.Interop;
using VANTAGE.Dialogs;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private const int ColumnUniqueValueDisplayLimit = 1000; // configurable
        private const int WM_MOUSEHWHEEL = 0x020E;
        private HwndSource? _hwndSource;
        private Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn> _columnMap = new Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn>();
        private UserFilter? _activeUserFilter;
        private bool _scanResultsFilterActive;
        private bool _metadataErrorsFilterActive;
        private string _globalSearchText = string.Empty;
        private ProgressViewModel _viewModel;
        // one key per grid/view
        private const string GridPrefsKey = "ProgressGrid.PreferencesJson";
        private const string FrozenColumnsKey = "ProgressGrid.FrozenColumnCount";
        private bool _skipSaveColumnState = false;
        private HashSet<string> _xamlHiddenColumns = new(); // XAML-defined default hidden columns

        // Debounce timer for summary panel updates during rapid filter changes
        private DispatcherTimer? _summaryDebounce;
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
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
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

                // Verify ownership for each record (Azure if exists, local if not)
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                var ownedRecords = new List<Activity>();
                var localOnlyRecords = new HashSet<string>(); // Records not yet in Azure
                var deniedRecords = new List<string>();

                foreach (var activity in selected)
                {
                    var checkCmd = azureConn.CreateCommand();
                    checkCmd.CommandText = "SELECT AssignedTo FROM VMS_Activities WHERE UniqueID = @id";
                    checkCmd.Parameters.AddWithValue("@id", activity.UniqueID);
                    var azureOwner = checkCmd.ExecuteScalar()?.ToString();

                    if (azureOwner == null)
                    {
                        // Record doesn't exist in Azure yet — check local ownership
                        if (isAdmin || string.Equals(activity.AssignedTo, currentUser?.Username, StringComparison.OrdinalIgnoreCase))
                        {
                            ownedRecords.Add(activity);
                            localOnlyRecords.Add(activity.UniqueID);
                        }
                        else
                        {
                            deniedRecords.Add(activity.UniqueID);
                        }
                    }
                    else if (isAdmin || string.Equals(azureOwner, currentUser?.Username, StringComparison.OrdinalIgnoreCase))
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

                // Soft-delete in Azure only for records that exist there
                int azureDeleted = 0;
                var azureIds = uniqueIds.Where(id => !localOnlyRecords.Contains(id)).ToList();
                if (azureIds.Count > 0)
                {
                    string azureIdList = string.Join(",", azureIds.Select(id => $"'{id}'"));
                    var deleteAzureCmd = azureConn.CreateCommand();
                    deleteAzureCmd.CommandText = $@"
                        UPDATE VMS_Activities
                        SET IsDeleted = 1,
                            UpdatedBy = @user,
                            UpdatedUtcDate = @date
                        WHERE UniqueID IN ({azureIdList})";
                    deleteAzureCmd.Parameters.AddWithValue("@user", currentUser?.Username ?? "Unknown");
                    deleteAzureCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    azureDeleted = deleteAzureCmd.ExecuteNonQuery();
                }

                azureConn.Close();

                // Refresh grid and totals
                if (ViewModel != null)
                {
                    await ViewModel.RefreshAsync();
                    await ViewModel.UpdateTotalsAsync();
                }

                // Update metadata error count (deleted rows may have had errors)
                await CalculateMetadataErrorCount();

                AppLogger.Info(
                    $"User deleted {localDeleted} activities (IsDeleted=1 set in Azure for {azureDeleted} records).",
                    "ProgressView.DeleteSelectedActivities_Click",
                    currentUser?.Username ?? "UnknownUser"
                );

                MessageBox.Show(
                    $"{localDeleted} record(s) deleted successfully.",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
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
                // Clear filter state without reload to avoid flash (ApplyFilter will reload)
                _viewModel.ClearFiltersWithoutReload();

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
                        RespParty IS NULL OR RespParty = '' OR
                        (PercentEntry > 0 AND (ActStart IS NULL OR ActStart = '')) OR
                        (PercentEntry >= 100 AND (ActFin IS NULL OR ActFin = ''))
                    )";

                await _viewModel.ApplyFilter("MetadataErrors", "IN", errorFilter);

                _metadataErrorsFilterActive = true;
                UpdateRecordCount();
                DebouncedUpdateSummary();
                UpdateClearFiltersBorder();
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
                    a.RespParty IS NULL OR a.RespParty = '' OR
                    (a.PercentEntry > 0 AND (a.ActStart IS NULL OR a.ActStart = '')) OR
                    (a.PercentEntry >= 100 AND (a.ActFin IS NULL OR a.ActFin = ''))
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

        // Navigate to first row in grid
        private void BtnGoToFirst_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sfActivities.View?.Records == null || sfActivities.View.Records.Count == 0)
                    return;

                sfActivities.SelectedIndex = 0;
                var gridRowIndex = sfActivities.ResolveToRowIndex(0);
                sfActivities.ScrollInView(new RowColumnIndex(gridRowIndex,
                    sfActivities.ResolveToScrollColumnIndex(0)));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.BtnGoToFirst_Click");
            }
        }

        // Navigate to last row in grid
        private void BtnGoToLast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sfActivities.View?.Records == null || sfActivities.View.Records.Count == 0)
                    return;

                var lastRecordIndex = sfActivities.View.Records.Count - 1;
                sfActivities.SelectedIndex = lastRecordIndex;
                var gridRowIndex = sfActivities.ResolveToRowIndex(lastRecordIndex);
                sfActivities.ScrollInView(new RowColumnIndex(gridRowIndex,
                    sfActivities.ResolveToScrollColumnIndex(0)));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.BtnGoToLast_Click");
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
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
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
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
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
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
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
                        // Use BeginInit/EndInit to suppress calculations until all fields are set.
                        // Without this, the non-zero defaults for _quantity and _budgetMHs (0.001)
                        // cause cascading recalculations that corrupt progress values.
                        var duplicate = new Activity();
                        duplicate.BeginInit();
                        duplicate.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
                        duplicate.ActivityID = 0;
                        duplicate.AssignedTo = currentUser;
                        duplicate.LocalDirty = 1;
                        duplicate.CreatedBy = currentUser;
                        duplicate.UpdatedBy = currentUser;
                        duplicate.UpdatedUtcDate = DateTime.UtcNow;
                        duplicate.SyncVersion = 0;
                        duplicate.WeekEndDate = null;
                        duplicate.ProgDate = null;
                        duplicate.AzureUploadUtcDate = null;
                        duplicate.ActStart = null;
                        duplicate.ActFin = null;
                        duplicate.Area = original.Area;
                        duplicate.Aux1 = original.Aux1;
                        duplicate.Aux2 = original.Aux2;
                        duplicate.Aux3 = original.Aux3;
                        duplicate.BaseUnit = original.BaseUnit;
                        duplicate.BudgetHoursGroup = original.BudgetHoursGroup;
                        duplicate.BudgetHoursROC = original.BudgetHoursROC;
                        duplicate.BudgetMHs = original.BudgetMHs;
                        duplicate.ChgOrdNO = original.ChgOrdNO;
                        duplicate.ClientBudget = original.ClientBudget;
                        duplicate.ClientCustom3 = original.ClientCustom3;
                        duplicate.ClientEquivQty = original.ClientEquivQty;
                        duplicate.CompType = original.CompType;
                        duplicate.DateTrigger = original.DateTrigger;
                        duplicate.Description = original.Description;
                        duplicate.DwgNO = original.DwgNO;
                        duplicate.EarnQtyEntry = original.EarnQtyEntry;
                        duplicate.EarnedMHsRoc = original.EarnedMHsRoc;
                        duplicate.EqmtNO = original.EqmtNO;
                        duplicate.EquivQTY = original.EquivQTY;
                        duplicate.EquivUOM = original.EquivUOM;
                        duplicate.Estimator = original.Estimator;
                        duplicate.HexNO = original.HexNO;
                        duplicate.HtTrace = original.HtTrace;
                        duplicate.InsulType = original.InsulType;
                        duplicate.LineNumber = original.LineNumber;
                        duplicate.MtrlSpec = original.MtrlSpec;
                        duplicate.Notes = original.Notes;
                        duplicate.PaintCode = original.PaintCode;
                        duplicate.PercentEntry = original.PercentEntry;
                        duplicate.PhaseCategory = original.PhaseCategory;
                        duplicate.PhaseCode = original.PhaseCode;
                        duplicate.PipeGrade = original.PipeGrade;
                        duplicate.PipeSize1 = original.PipeSize1;
                        duplicate.PipeSize2 = original.PipeSize2;
                        duplicate.PrevEarnMHs = original.PrevEarnMHs;
                        duplicate.PrevEarnQTY = original.PrevEarnQTY;
                        duplicate.ProjectID = original.ProjectID;
                        duplicate.Quantity = original.Quantity;
                        duplicate.RevNO = original.RevNO;
                        duplicate.RFINO = original.RFINO;
                        duplicate.ROCBudgetQTY = original.ROCBudgetQTY;
                        duplicate.ROCID = original.ROCID;
                        duplicate.ROCPercent = original.ROCPercent;
                        duplicate.ROCStep = original.ROCStep;
                        duplicate.SchedActNO = original.SchedActNO;
                        duplicate.SecondActno = original.SecondActno;
                        duplicate.SecondDwgNO = original.SecondDwgNO;
                        duplicate.Service = original.Service;
                        duplicate.ShopField = original.ShopField;
                        duplicate.ShtNO = original.ShtNO;
                        duplicate.SubArea = original.SubArea;
                        duplicate.PjtSystem = original.PjtSystem;
                        duplicate.PjtSystemNo = original.PjtSystemNo;
                        duplicate.TagNO = original.TagNO;
                        duplicate.UDF1 = original.UDF1;
                        duplicate.UDF2 = original.UDF2;
                        duplicate.UDF3 = original.UDF3;
                        duplicate.UDF4 = original.UDF4;
                        duplicate.UDF5 = original.UDF5;
                        duplicate.UDF6 = original.UDF6;
                        duplicate.UDF7 = original.UDF7;
                        duplicate.UDF8 = original.UDF8;
                        duplicate.UDF9 = original.UDF9;
                        duplicate.UDF10 = original.UDF10;
                        duplicate.UDF11 = original.UDF11;
                        duplicate.UDF12 = original.UDF12;
                        duplicate.UDF13 = original.UDF13;
                        duplicate.UDF14 = original.UDF14;
                        duplicate.UDF15 = original.UDF15;
                        duplicate.UDF16 = original.UDF16;
                        duplicate.UDF17 = original.UDF17;
                        duplicate.RespParty = original.RespParty;
                        duplicate.UDF20 = original.UDF20;
                        duplicate.UOM = original.UOM;
                        duplicate.WorkPackage = original.WorkPackage;
                        duplicate.XRay = original.XRay;
                        duplicate.EndInit();

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
                        ROCBudgetQTY, ROCID, ROCPercent, ROCStep, SchedActNO, ActFin, ActStart,
                        SecondActno, SecondDwgNO, Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo,
                        TagNO, UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9,
                        UDF10, UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                        UpdatedBy, UpdatedUtcDate, UOM, WeekEndDate, WorkPackage, XRay, SyncVersion
                    ) VALUES (
                        @UniqueID, @ActivityID, @Area, @AssignedTo, @AzureUploadUtcDate, @Aux1, @Aux2, @Aux3,
                        @BaseUnit, @BudgetHoursGroup, @BudgetHoursROC, @BudgetMHs, @ChgOrdNO, @ClientBudget,
                        @ClientCustom3, @ClientEquivQty, @CompType, @CreatedBy, @DateTrigger, @Description,
                        @DwgNO, @EarnQtyEntry, @EarnedMHsRoc, @EqmtNO, @EquivQTY, @EquivUOM, @Estimator,
                        @HexNO, @HtTrace, @InsulType, @LineNumber, @LocalDirty, @MtrlSpec, @Notes, @PaintCode,
                        @PercentEntry, @PhaseCategory, @PhaseCode, @PipeGrade, @PipeSize1, @PipeSize2,
                        @PrevEarnMHs, @PrevEarnQTY, @ProgDate, @ProjectID, @Quantity, @RevNO, @RFINO,
                        @ROCBudgetQTY, @ROCID, @ROCPercent, @ROCStep, @SchedActNO, @ActFin, @ActStart,
                        @SecondActno, @SecondDwgNO, @Service, @ShopField, @ShtNO, @SubArea, @PjtSystem, @PjtSystemNo,
                        @TagNO, @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9,
                        @UDF10, @UDF11, @UDF12, @UDF13, @UDF14, @UDF15, @UDF16, @UDF17, @RespParty, @UDF20,
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
                        insertCmd.Parameters.AddWithValue("@ActFin", duplicate.ActFin?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@ActStart", duplicate.ActStart?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                        insertCmd.Parameters.AddWithValue("@SecondActno", duplicate.SecondActno ?? "");
                        insertCmd.Parameters.AddWithValue("@SecondDwgNO", duplicate.SecondDwgNO ?? "");
                        insertCmd.Parameters.AddWithValue("@Service", duplicate.Service ?? "");
                        insertCmd.Parameters.AddWithValue("@ShopField", duplicate.ShopField ?? "");
                        insertCmd.Parameters.AddWithValue("@ShtNO", duplicate.ShtNO ?? "");
                        insertCmd.Parameters.AddWithValue("@SubArea", duplicate.SubArea ?? "");
                        insertCmd.Parameters.AddWithValue("@PjtSystem", duplicate.PjtSystem ?? "");
                        insertCmd.Parameters.AddWithValue("@PjtSystemNo", duplicate.PjtSystemNo ?? "");
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
                        insertCmd.Parameters.AddWithValue("@RespParty", duplicate.RespParty ?? "");
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
                        "Duplication Complete", MessageBoxButton.OK, MessageBoxImage.None);
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

        // Adds a blank activity row with only system fields populated
        private async void MenuAddBlankRow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentUser = App.CurrentUser?.Username ?? "Unknown";
                var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
                var userSuffix = App.CurrentUser?.Username?.Length >= 3
                    ? App.CurrentUser.Username.Substring(App.CurrentUser.Username.Length - 3).ToLower()
                    : "usr";
                var uniqueId = $"i{timestamp}1{userSuffix}";

                var newActivity = new Activity();
                newActivity.BeginInit();
                newActivity.UniqueID = uniqueId;
                newActivity.ActivityID = 0;
                newActivity.AssignedTo = currentUser;
                newActivity.LocalDirty = 1;
                newActivity.CreatedBy = currentUser;
                newActivity.UpdatedBy = currentUser;
                newActivity.UpdatedUtcDate = DateTime.UtcNow;
                newActivity.SyncVersion = 0;
                newActivity.Quantity = 0.001;
                newActivity.BudgetMHs = 0.001;
                newActivity.EndInit();

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Activities (
                        UniqueID, ActivityID, Area, AssignedTo, AzureUploadUtcDate, Aux1, Aux2, Aux3,
                        BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs, ChgOrdNO, ClientBudget,
                        ClientCustom3, ClientEquivQty, CompType, CreatedBy, DateTrigger, Description,
                        DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO, EquivQTY, EquivUOM, Estimator,
                        HexNO, HtTrace, InsulType, LineNumber, LocalDirty, MtrlSpec, Notes, PaintCode,
                        PercentEntry, PhaseCategory, PhaseCode, PipeGrade, PipeSize1, PipeSize2,
                        PrevEarnMHs, PrevEarnQTY, ProgDate, ProjectID, Quantity, RevNO, RFINO,
                        ROCBudgetQTY, ROCID, ROCPercent, ROCStep, SchedActNO, ActFin, ActStart,
                        SecondActno, SecondDwgNO, Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo,
                        TagNO, UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9,
                        UDF10, UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                        UpdatedBy, UpdatedUtcDate, UOM, WeekEndDate, WorkPackage, XRay, SyncVersion
                    ) VALUES (
                        @UniqueID, @ActivityID, @Area, @AssignedTo, @AzureUploadUtcDate, @Aux1, @Aux2, @Aux3,
                        @BaseUnit, @BudgetHoursGroup, @BudgetHoursROC, @BudgetMHs, @ChgOrdNO, @ClientBudget,
                        @ClientCustom3, @ClientEquivQty, @CompType, @CreatedBy, @DateTrigger, @Description,
                        @DwgNO, @EarnQtyEntry, @EarnedMHsRoc, @EqmtNO, @EquivQTY, @EquivUOM, @Estimator,
                        @HexNO, @HtTrace, @InsulType, @LineNumber, @LocalDirty, @MtrlSpec, @Notes, @PaintCode,
                        @PercentEntry, @PhaseCategory, @PhaseCode, @PipeGrade, @PipeSize1, @PipeSize2,
                        @PrevEarnMHs, @PrevEarnQTY, @ProgDate, @ProjectID, @Quantity, @RevNO, @RFINO,
                        @ROCBudgetQTY, @ROCID, @ROCPercent, @ROCStep, @SchedActNO, @ActFin, @ActStart,
                        @SecondActno, @SecondDwgNO, @Service, @ShopField, @ShtNO, @SubArea, @PjtSystem, @PjtSystemNo,
                        @TagNO, @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9,
                        @UDF10, @UDF11, @UDF12, @UDF13, @UDF14, @UDF15, @UDF16, @UDF17, @RespParty, @UDF20,
                        @UpdatedBy, @UpdatedUtcDate, @UOM, @WeekEndDate, @WorkPackage, @XRay, @SyncVersion
                    )";

                cmd.Parameters.AddWithValue("@UniqueID", newActivity.UniqueID);
                cmd.Parameters.AddWithValue("@ActivityID", 0);
                cmd.Parameters.AddWithValue("@Area", "");
                cmd.Parameters.AddWithValue("@AssignedTo", currentUser);
                cmd.Parameters.AddWithValue("@AzureUploadUtcDate", "");
                cmd.Parameters.AddWithValue("@Aux1", "");
                cmd.Parameters.AddWithValue("@Aux2", "");
                cmd.Parameters.AddWithValue("@Aux3", "");
                cmd.Parameters.AddWithValue("@BaseUnit", 0.0);
                cmd.Parameters.AddWithValue("@BudgetHoursGroup", 0.0);
                cmd.Parameters.AddWithValue("@BudgetHoursROC", 0.0);
                cmd.Parameters.AddWithValue("@BudgetMHs", 0.001);
                cmd.Parameters.AddWithValue("@ChgOrdNO", "");
                cmd.Parameters.AddWithValue("@ClientBudget", 0.0);
                cmd.Parameters.AddWithValue("@ClientCustom3", 0.0);
                cmd.Parameters.AddWithValue("@ClientEquivQty", 0.0);
                cmd.Parameters.AddWithValue("@CompType", "");
                cmd.Parameters.AddWithValue("@CreatedBy", currentUser);
                cmd.Parameters.AddWithValue("@DateTrigger", 0);
                cmd.Parameters.AddWithValue("@Description", "");
                cmd.Parameters.AddWithValue("@DwgNO", "");
                cmd.Parameters.AddWithValue("@EarnQtyEntry", 0.0);
                cmd.Parameters.AddWithValue("@EarnedMHsRoc", 0.0);
                cmd.Parameters.AddWithValue("@EqmtNO", "");
                cmd.Parameters.AddWithValue("@EquivQTY", 0.0);
                cmd.Parameters.AddWithValue("@EquivUOM", "");
                cmd.Parameters.AddWithValue("@Estimator", "");
                cmd.Parameters.AddWithValue("@HexNO", 0);
                cmd.Parameters.AddWithValue("@HtTrace", "");
                cmd.Parameters.AddWithValue("@InsulType", "");
                cmd.Parameters.AddWithValue("@LineNumber", "");
                cmd.Parameters.AddWithValue("@LocalDirty", 1);
                cmd.Parameters.AddWithValue("@MtrlSpec", "");
                cmd.Parameters.AddWithValue("@Notes", "");
                cmd.Parameters.AddWithValue("@PaintCode", "");
                cmd.Parameters.AddWithValue("@PercentEntry", 0.0);
                cmd.Parameters.AddWithValue("@PhaseCategory", "");
                cmd.Parameters.AddWithValue("@PhaseCode", "");
                cmd.Parameters.AddWithValue("@PipeGrade", "");
                cmd.Parameters.AddWithValue("@PipeSize1", 0.0);
                cmd.Parameters.AddWithValue("@PipeSize2", 0.0);
                cmd.Parameters.AddWithValue("@PrevEarnMHs", 0.0);
                cmd.Parameters.AddWithValue("@PrevEarnQTY", 0.0);
                cmd.Parameters.AddWithValue("@ProgDate", "");
                cmd.Parameters.AddWithValue("@ProjectID", "");
                cmd.Parameters.AddWithValue("@Quantity", 0.001);
                cmd.Parameters.AddWithValue("@RevNO", "");
                cmd.Parameters.AddWithValue("@RFINO", "");
                cmd.Parameters.AddWithValue("@ROCBudgetQTY", 0.0);
                cmd.Parameters.AddWithValue("@ROCID", 0.0);
                cmd.Parameters.AddWithValue("@ROCPercent", 0.0);
                cmd.Parameters.AddWithValue("@ROCStep", "");
                cmd.Parameters.AddWithValue("@SchedActNO", "");
                cmd.Parameters.AddWithValue("@ActFin", "");
                cmd.Parameters.AddWithValue("@ActStart", "");
                cmd.Parameters.AddWithValue("@SecondActno", "");
                cmd.Parameters.AddWithValue("@SecondDwgNO", "");
                cmd.Parameters.AddWithValue("@Service", "");
                cmd.Parameters.AddWithValue("@ShopField", "");
                cmd.Parameters.AddWithValue("@ShtNO", "");
                cmd.Parameters.AddWithValue("@SubArea", "");
                cmd.Parameters.AddWithValue("@PjtSystem", "");
                cmd.Parameters.AddWithValue("@PjtSystemNo", "");
                cmd.Parameters.AddWithValue("@TagNO", "");
                cmd.Parameters.AddWithValue("@UDF1", "");
                cmd.Parameters.AddWithValue("@UDF2", "");
                cmd.Parameters.AddWithValue("@UDF3", "");
                cmd.Parameters.AddWithValue("@UDF4", "");
                cmd.Parameters.AddWithValue("@UDF5", "");
                cmd.Parameters.AddWithValue("@UDF6", "");
                cmd.Parameters.AddWithValue("@UDF7", 0);
                cmd.Parameters.AddWithValue("@UDF8", "");
                cmd.Parameters.AddWithValue("@UDF9", "");
                cmd.Parameters.AddWithValue("@UDF10", "");
                cmd.Parameters.AddWithValue("@UDF11", "");
                cmd.Parameters.AddWithValue("@UDF12", "");
                cmd.Parameters.AddWithValue("@UDF13", "");
                cmd.Parameters.AddWithValue("@UDF14", "");
                cmd.Parameters.AddWithValue("@UDF15", "");
                cmd.Parameters.AddWithValue("@UDF16", "");
                cmd.Parameters.AddWithValue("@UDF17", "");
                cmd.Parameters.AddWithValue("@RespParty", "");
                cmd.Parameters.AddWithValue("@UDF20", "");
                cmd.Parameters.AddWithValue("@UpdatedBy", currentUser);
                cmd.Parameters.AddWithValue("@UpdatedUtcDate", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@UOM", "");
                cmd.Parameters.AddWithValue("@WeekEndDate", "");
                cmd.Parameters.AddWithValue("@WorkPackage", "");
                cmd.Parameters.AddWithValue("@XRay", 0.0);
                cmd.Parameters.AddWithValue("@SyncVersion", 0);

                cmd.ExecuteNonQuery();

                AppLogger.Info($"Added blank row {uniqueId}", "ProgressView.MenuAddBlankRow_Click", currentUser);

                // Clear filters and sorts so the new row is visible at the bottom
                await _viewModel.ClearAllFiltersAsync();
                _globalSearchText = string.Empty;
                txtGlobalSearch.Text = string.Empty;
                sfActivities.View.Filter = null;
                _scanResultsFilterActive = false;
                _metadataErrorsFilterActive = false;
                sfActivities.ClearFilters();
                sfActivities.View.RefreshFilter();

                // Reset filter button visuals
                btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterLocalDirty.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                btnFilterToday.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                _activeUserFilter = null;
                txtUserFilterName.Text = "USER";
                btnUserFilters.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
                UpdateClearFiltersBorder();

                // Clear sorts
                sfActivities.SortColumnDescriptions.Clear();

                // Refresh grid to load the new row
                await _viewModel.RefreshAsync();
                UpdateRecordCount();
                UpdateSummaryPanel();
                await CalculateMetadataErrorCount();

                // Scroll to bottom and select the new row
                if (sfActivities.View?.Records?.Count > 0)
                {
                    var lastRecordIndex = sfActivities.View.Records.Count - 1;
                    sfActivities.SelectedItem = sfActivities.View.Records[lastRecordIndex].Data;
                    var gridRowIndex = sfActivities.ResolveToRowIndex(lastRecordIndex);
                    sfActivities.ScrollInView(new RowColumnIndex(gridRowIndex, sfActivities.ResolveToScrollColumnIndex(0)));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.MenuAddBlankRow_Click");
                MessageBox.Show($"Failed to add blank row: {ex.Message}", "Error",
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
                        MessageBoxImage.None);
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
            // Apply Syncfusion theme to grid
            Syncfusion.SfSkinManager.SfSkinManager.SetTheme(sfActivities, new Syncfusion.SfSkinManager.Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += (_, __) => ThemeManager.ThemeChanged += OnThemeChanged;
            Unloaded += (_, __) => ThemeManager.ThemeChanged -= OnThemeChanged;
            // Hook into Syncfusion's filter changed event
            sfActivities.FilterChanged += SfActivities_FilterChanged;
            sfActivities.CurrentCellBeginEdit += SfActivities_CurrentCellBeginEdit;
            sfActivities.GridCopyContent += SfActivities_GridCopyContent;
            sfActivities.GridPasteContent += SfActivities_GridPasteContent;
            sfActivities.PreviewKeyDown += SfActivities_PreviewKeyDown;
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
                // Capture XAML-defined defaults before user prefs override them
                _xamlHiddenColumns = new HashSet<string>(
                    sfActivities.Columns.Where(c => c.IsHidden).Select(c => c.MappingName));
                // Let layout/render complete, then apply prefs
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadColumnState();
                    LoadFrozenColumnCount();
                    // Disable auto column sizing after initial load for performance
                    sfActivities.ColumnSizer = Syncfusion.UI.Xaml.Grid.GridLengthUnitType.None;
                    sfActivities.Opacity = 1; // Show grid after state is loaded
                }),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            };

            // Hook native horizontal scroll wheel for tilt/side wheel
            this.Loaded += (_, __) => AttachHorizontalScrollHook();

            // Save when view closes
            this.Unloaded += (_, __) =>
            {
                DetachHorizontalScrollHook();
                SaveColumnState();
            };

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

        // Re-apply Syncfusion skin to grid when theme changes
        private void OnThemeChanged(string themeName)
        {
            Dispatcher.Invoke(() =>
            {
                // Hide grid during transition to prevent flash of stale row colors
                sfActivities.Opacity = 0;

                var sfTheme = new Syncfusion.SfSkinManager.Theme(ThemeManager.GetSyncfusionThemeName());
                Syncfusion.SfSkinManager.SfSkinManager.SetTheme(sfActivities, sfTheme);
            });

            // Defer RowStyleSelector reset so it runs after SfSkinManager finishes its visual updates
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                var selector = sfActivities.RowStyleSelector;
                sfActivities.RowStyleSelector = null;
                sfActivities.RowStyleSelector = selector;
                sfActivities.Opacity = 1;
            });
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

        // UserControl-level handler: intercepts Ctrl+V BEFORE the grid sees it
        // Only handles single-value-to-multiple-rows paste; all other paste flows to grid normally
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Only intercept Ctrl+V for our specific case
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Check: multiple rows selected via SelectedItems (keyboard selection like Ctrl+Shift+Down)
                if (sfActivities.SelectedItems.Count > 1 && sfActivities.CurrentColumn != null)
                {
                    var clipboardValues = GetClipboardFirstColumn();

                    // Only handle if SINGLE clipboard value being pasted to MULTIPLE rows
                    if (clipboardValues != null && clipboardValues.Count == 1)
                    {
                        e.Handled = true;
                        var rowBasedCells = BuildCellsFromSelectedRows();
                        if (rowBasedCells != null && rowBasedCells.Count > 0)
                        {
                            PasteToSelectedCells(rowBasedCells, clipboardValues);
                        }
                    }
                    // If multiple clipboard values, let grid handle it normally (don't set e.Handled)
                }
                // If not multi-row selection, let grid handle it normally (don't set e.Handled)
            }
        }

        // Intercept Ctrl+C and Ctrl+V for multi-cell copy/paste before edit control captures it
        private void SfActivities_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+C for multi-cell copy
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var selectedCells = sfActivities.GetSelectedCells();

                // Only handle multi-cell selection - let single cell use default behavior
                if (selectedCells != null && selectedCells.Count > 1)
                {
                    e.Handled = true;
                    CopySelectedCellsToClipboard(selectedCells);
                }
            }
            // Handle Ctrl+V for multi-cell paste
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var selectedCells = sfActivities.GetSelectedCells();
                var clipboardValues = GetClipboardFirstColumn();

                // Handle multi-row clipboard (even with single cell selected)
                if (clipboardValues != null && clipboardValues.Count > 0 && selectedCells != null && selectedCells.Count > 0)
                {
                    e.Handled = true;
                    PasteToSelectedCells(selectedCells, clipboardValues);
                }
            }
            // Handle Delete/Backspace to clear cell when not in edit mode
            else if ((e.Key == Key.Delete || e.Key == Key.Back) && Keyboard.Modifiers == ModifierKeys.None)
            {
                var currentCell = sfActivities.SelectionController.CurrentCellManager.CurrentCell;
                if (currentCell == null || currentCell.IsEditing)
                    return; // Let default behavior handle if in edit mode

                var activity = sfActivities.SelectedItem as Activity;
                if (activity == null)
                    return;

                // Check ownership
                bool canEdit = string.Equals(activity.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);
                if (!canEdit)
                    return;

                // Get column info
                if (sfActivities.CurrentColumn == null)
                    return;

                e.Handled = true;
                _ = ClearCurrentCellAsync(activity, sfActivities.CurrentColumn.MappingName);
            }
        }

        // Copy selected cells as tab-separated Excel-compatible format
        private void CopySelectedCellsToClipboard(IList<GridCellInfo> selectedCells)
        {
            try
            {
                // Group cells by row and organize by column
                var cellsByRow = selectedCells
                    .GroupBy(c => c.RowData)
                    .OrderBy(g => sfActivities.View?.Records.IndexOf(
                        sfActivities.View.Records.FirstOrDefault(r => r.Data == g.Key)))
                    .ToList();

                // Get all unique columns in selection, ordered by display index
                var columnsInSelection = selectedCells
                    .Select(c => c.Column)
                    .Distinct()
                    .OrderBy(col => sfActivities.Columns.IndexOf(col))
                    .ToList();

                var sb = new StringBuilder();

                foreach (var rowGroup in cellsByRow)
                {
                    var activity = rowGroup.Key as Activity;
                    if (activity == null) continue;

                    var rowValues = new List<string>();

                    foreach (var column in columnsInSelection)
                    {
                        // Check if this cell is in the selection for this row
                        var cellInRow = rowGroup.FirstOrDefault(c => c.Column == column);
                        if (cellInRow != null)
                        {
                            var property = typeof(Activity).GetProperty(column.MappingName);
                            if (property != null)
                            {
                                var value = property.GetValue(activity);
                                if (value == null)
                                {
                                    rowValues.Add(string.Empty);
                                }
                                else if (value is DateTime dt)
                                {
                                    rowValues.Add(dt.ToString("yyyy-MM-dd"));
                                }
                                else
                                {
                                    rowValues.Add(value.ToString() ?? string.Empty);
                                }
                            }
                            else
                            {
                                rowValues.Add(string.Empty);
                            }
                        }
                        else
                        {
                            // Cell not in selection for this row - add empty placeholder
                            rowValues.Add(string.Empty);
                        }
                    }

                    sb.AppendLine(string.Join("\t", rowValues));
                }

                // Copy to clipboard (trim trailing newline)
                var result = sb.ToString().TrimEnd('\r', '\n');
                Clipboard.SetText(result);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "CopySelectedCellsToClipboard", App.CurrentUser?.Username ?? "Unknown");
            }
        }

        // Extract first column from clipboard (handles multi-column clipboard data)
        private List<string>? GetClipboardFirstColumn()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return null;

                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                // Split by newlines, extract first column from each row
                var rows = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var values = new List<string>();

                foreach (var row in rows)
                {
                    if (string.IsNullOrEmpty(row))
                        continue;

                    // Split by tab to get columns, take first column only
                    var columns = row.Split('\t');
                    values.Add(columns[0]);
                }

                return values.Count > 0 ? values : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GetClipboardFirstColumn", App.CurrentUser?.Username ?? "Unknown");
                return null;
            }
        }

        // Build target cells from selected rows + current column (for Ctrl+Shift+Down selection)
        private List<(Activity activity, GridColumn column)>? BuildCellsFromSelectedRows()
        {
            var currentColumn = sfActivities.CurrentColumn;
            if (currentColumn == null)
                return null;

            var selectedActivities = sfActivities.SelectedItems.Cast<Activity>().ToList();
            if (selectedActivities.Count == 0)
                return null;

            return selectedActivities.Select(a => (a, currentColumn)).ToList();
        }

        // Paste single clipboard value to multiple rows (Ctrl+Shift+Down selection)
        private async void PasteToSelectedCells(List<(Activity activity, GridColumn column)> rowBasedCells, List<string> clipboardValues)
        {
            if (rowBasedCells.Count == 0 || clipboardValues.Count == 0)
                return;

            var column = rowBasedCells[0].column;
            string columnName = column.MappingName;
            string columnHeader = column.HeaderText;

            // Validate: column must be editable
            if (VANTAGE.Utilities.ColumnPermissions.IsReadOnly(columnName))
            {
                MessageBox.Show($"Cannot paste to '{columnHeader}' - this column is read-only.",
                    "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get property for the target column
            var property = typeof(Activity).GetProperty(columnName);
            if (property == null)
            {
                MessageBox.Show($"Column '{columnName}' not found.", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Validate: user must own ALL affected records
            var currentUser = App.CurrentUser?.Username;
            var nonOwnedRecords = rowBasedCells
                .Where(r => !string.Equals(r.activity.AssignedTo, currentUser, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonOwnedRecords.Count > 0)
            {
                MessageBox.Show("Cannot paste - some rows are owned by other users.",
                    "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Single value to paste to all rows
            string clipboardValue = clipboardValues[0];
            var now = DateTime.UtcNow;

            try
            {
                // Update all in-memory objects first
                var updates = new List<(string UniqueID, object? NewValue)>();

                foreach (var (activity, _) in rowBasedCells)
                {
                    // Try to convert and set value on in-memory object
                    if (!TrySetPropertyValue(activity, property, columnName, clipboardValue, out string? errorMessage))
                    {
                        MessageBox.Show(errorMessage ?? $"Invalid value '{clipboardValue}' for column '{columnHeader}'.",
                            "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Mark as modified in-memory
                    activity.LocalDirty = 1;
                    activity.UpdatedBy = currentUser ?? "Unknown";
                    activity.UpdatedUtcDate = now;

                    // Collect for bulk update
                    var newValue = property.GetValue(activity);
                    updates.Add((activity.UniqueID, newValue));
                }

                // Single bulk update to database
                await ActivityRepository.BulkUpdateColumnAsync(
                    columnName,
                    updates,
                    currentUser ?? "Unknown");

                // Refresh grid
                sfActivities.View?.Refresh();
                UpdateSummaryPanel();
                await CalculateMetadataErrorCount();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PasteToSelectedCells(RowBased)", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Paste failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Paste clipboard values to selected cells
        private async void PasteToSelectedCells(IList<GridCellInfo> selectedCells, List<string> clipboardValues)
        {
            try
            {
                // Find leftmost column from selection
                var leftmostColumn = selectedCells
                    .Select(c => c.Column)
                    .Distinct()
                    .OrderBy(col => sfActivities.Columns.IndexOf(col))
                    .FirstOrDefault();

                if (leftmostColumn == null)
                    return;

                string columnName = leftmostColumn.MappingName;
                string columnHeader = leftmostColumn.HeaderText;

                // Validate: column must be editable
                if (VANTAGE.Utilities.ColumnPermissions.IsReadOnly(columnName))
                {
                    MessageBox.Show($"Cannot paste to '{columnHeader}' - this column is read-only.",
                        "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine target cells
                List<(Activity activity, int viewIndex)> targetCells;

                // Filter selection to leftmost column only
                var cellsInLeftmostColumn = selectedCells
                    .Where(c => c.Column == leftmostColumn)
                    .ToList();

                if (cellsInLeftmostColumn.Count == 1 && clipboardValues.Count > 1)
                {
                    // Single cell selected + multi-row clipboard: paste downward
                    var startCell = cellsInLeftmostColumn[0];
                    var startActivity = startCell.RowData as Activity;
                    if (startActivity == null) return;

                    // Find starting row index in View
                    var startIndex = sfActivities.View?.Records
                        .Select((r, i) => new { Record = r, Index = i })
                        .FirstOrDefault(x => x.Record.Data == startActivity)?.Index ?? -1;

                    if (startIndex < 0) return;

                    // Get rows from starting point downward
                    targetCells = new List<(Activity, int)>();
                    var records = sfActivities.View?.Records;
                    if (records == null) return;

                    for (int i = 0; i < clipboardValues.Count && (startIndex + i) < records.Count; i++)
                    {
                        var activity = records[startIndex + i].Data as Activity;
                        if (activity != null)
                        {
                            targetCells.Add((activity, startIndex + i));
                        }
                    }
                }
                else
                {
                    // Multiple cells selected: use selected cells in leftmost column
                    targetCells = cellsInLeftmostColumn
                        .Select(c => (activity: c.RowData as Activity,
                            viewIndex: sfActivities.View?.Records
                                .Select((r, i) => new { Record = r, Index = i })
                                .FirstOrDefault(x => x.Record.Data == c.RowData)?.Index ?? -1))
                        .Where(x => x.activity != null && x.viewIndex >= 0)
                        .OrderBy(x => x.viewIndex)
                        .ToList()!;
                }

                if (targetCells.Count == 0)
                    return;

                // Validate: user must own ALL affected records
                var currentUser = App.CurrentUser?.Username;
                var nonOwnedRecords = targetCells
                    .Where(t => !string.Equals(t.activity.AssignedTo, currentUser, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (nonOwnedRecords.Count > 0)
                {
                    MessageBox.Show("Cannot paste - rows owned by other users would be affected.\n\nSelect only your own rows or change the copied content.",
                        "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get property for the target column
                var property = typeof(Activity).GetProperty(columnName);
                if (property == null)
                {
                    MessageBox.Show($"Column '{columnName}' not found.", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Paste values
                int pasteCount = Math.Min(clipboardValues.Count, targetCells.Count);
                var modifiedActivities = new List<Activity>();

                for (int i = 0; i < pasteCount; i++)
                {
                    var activity = targetCells[i].activity;
                    var clipboardValue = clipboardValues[i];

                    // Try to convert and set value
                    if (!TrySetPropertyValue(activity, property, columnName, clipboardValue, out string? errorMessage))
                    {
                        MessageBox.Show(errorMessage ?? $"Invalid value '{clipboardValue}' for column '{columnHeader}'.",
                            "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Mark as modified
                    activity.LocalDirty = 1;
                    activity.UpdatedBy = currentUser ?? "Unknown";
                    activity.UpdatedUtcDate = DateTime.UtcNow;
                    modifiedActivities.Add(activity);
                }

                // Save all modified activities to database
                foreach (var activity in modifiedActivities)
                {
                    await ActivityRepository.UpdateActivityInDatabase(activity);
                }

                // Refresh grid
                sfActivities.View?.Refresh();
                UpdateSummaryPanel();
                await CalculateMetadataErrorCount();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PasteToSelectedCells", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Paste failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Try to set a property value with type conversion, returns false if conversion fails
        private bool TrySetPropertyValue(Activity activity, PropertyInfo property, string columnName, string value, out string? errorMessage)
        {
            errorMessage = null;

            try
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                // Handle PercentEntry special case for auto-dates
                double? oldPercent = null;
                if (columnName == "PercentEntry")
                {
                    oldPercent = activity.PercentEntry;
                }

                object? convertedValue = null;

                if (propertyType == typeof(double))
                {
                    if (double.TryParse(value, out double dblVal))
                    {
                        convertedValue = NumericHelper.RoundToPlaces(dblVal);
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        convertedValue = 0.0;
                    }
                    else
                    {
                        errorMessage = $"Invalid number '{value}' for column '{columnName}'.";
                        return false;
                    }
                }
                else if (propertyType == typeof(int))
                {
                    if (int.TryParse(value, out int intVal))
                    {
                        convertedValue = intVal;
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        convertedValue = 0;
                    }
                    else
                    {
                        errorMessage = $"Invalid integer '{value}' for column '{columnName}'.";
                        return false;
                    }
                }
                else if (propertyType == typeof(DateTime))
                {
                    if (DateTime.TryParse(value, out DateTime dtVal))
                    {
                        convertedValue = dtVal;
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        convertedValue = null;
                    }
                    else
                    {
                        errorMessage = $"Invalid date '{value}' for column '{columnName}'.";
                        return false;
                    }
                }
                else if (propertyType == typeof(string))
                {
                    convertedValue = value;
                }
                else
                {
                    // Try generic conversion
                    convertedValue = Convert.ChangeType(value, propertyType);
                }

                // Set the property value
                property.SetValue(activity, convertedValue);

                // Handle date clearing for PercentEntry changes
                if (columnName == "PercentEntry" && convertedValue is double newPercent)
                {
                    // 0% → clear both dates
                    if (newPercent == 0)
                    {
                        activity.ActStart = null;
                        activity.ActFin = null;
                    }
                    // <100% → clear ActFin
                    else if (newPercent < 100)
                    {
                        activity.ActFin = null;
                    }
                    // Note: No auto-set of dates - user must enter ActStart/ActFin manually

                    // Recalculate derived fields
                    activity.RecalculateDerivedFields("PercentEntry");
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to set value: {ex.Message}";
                return false;
            }
        }

        // Handle copy - multi-cell selection copies as tab-separated Excel-compatible format (fallback)
        private void SfActivities_GridCopyContent(object? sender, GridCopyPasteEventArgs e)
        {
            try
            {
                var selectedCells = sfActivities.GetSelectedCells();

                // If multiple cells selected, format as Excel-compatible grid
                if (selectedCells != null && selectedCells.Count > 1)
                {
                    e.Handled = true; // Prevent default copy behavior

                    // Group cells by row and organize by column
                    var cellsByRow = selectedCells
                        .GroupBy(c => c.RowData)
                        .OrderBy(g => sfActivities.View?.Records.IndexOf(
                            sfActivities.View.Records.FirstOrDefault(r => r.Data == g.Key)))
                        .ToList();

                    // Get all unique columns in selection, ordered by display index
                    var columnsInSelection = selectedCells
                        .Select(c => c.Column)
                        .Distinct()
                        .OrderBy(col => sfActivities.Columns.IndexOf(col))
                        .ToList();

                    var sb = new StringBuilder();

                    foreach (var rowGroup in cellsByRow)
                    {
                        var activity = rowGroup.Key as Activity;
                        if (activity == null) continue;

                        var rowValues = new List<string>();

                        foreach (var column in columnsInSelection)
                        {
                            // Check if this cell is in the selection for this row
                            var cellInRow = rowGroup.FirstOrDefault(c => c.Column == column);
                            if (cellInRow != null)
                            {
                                var property = typeof(Activity).GetProperty(column.MappingName);
                                if (property != null)
                                {
                                    var value = property.GetValue(activity);
                                    if (value == null)
                                    {
                                        rowValues.Add(string.Empty);
                                    }
                                    else if (value is DateTime dt)
                                    {
                                        rowValues.Add(dt.ToString("yyyy-MM-dd"));
                                    }
                                    else
                                    {
                                        rowValues.Add(value.ToString() ?? string.Empty);
                                    }
                                }
                                else
                                {
                                    rowValues.Add(string.Empty);
                                }
                            }
                            else
                            {
                                // Cell not in selection for this row - add empty placeholder
                                rowValues.Add(string.Empty);
                            }
                        }

                        sb.AppendLine(string.Join("\t", rowValues));
                    }

                    // Copy to clipboard (trim trailing newline)
                    var result = sb.ToString().TrimEnd('\r', '\n');
                    Clipboard.SetText(result);

                    return;
                }

                // Single cell - use default behavior (enter edit mode)
                var currentCell = sfActivities.SelectionController.CurrentCellManager.CurrentCell;
                if (currentCell == null)
                    return;

                if (!currentCell.IsEditing)
                {
                    sfActivities.SelectionController.CurrentCellManager.BeginEdit();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SfActivities_GridCopyContent", App.CurrentUser?.Username ?? "Unknown");
            }
        }
        private void SfActivities_FilterChanged(object? sender, Syncfusion.UI.Xaml.Grid.GridFilterEventArgs e)

        {
            // Update FilteredCount based on Syncfusion's filtered records
            if (sfActivities.View != null)
            {
                var filteredCount = sfActivities.View.Records.Count;
                _viewModel.FilteredCount = filteredCount;
                UpdateRecordCount(); // Ensure UI label updates
                DebouncedUpdateSummary(); // debounced for rapid filter changes
                UpdateClearFiltersBorder();
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
            string value1 = SettingsManager.GetUserSetting( "CustomPercentButton1");
            if (!string.IsNullOrEmpty(value1) && int.TryParse(value1, out int percent1))
            {
                btnSetPercent100.Content = $"{percent1}%";
            }

            // Load button 2
            string value2 = SettingsManager.GetUserSetting( "CustomPercentButton2");
            if (!string.IsNullOrEmpty(value2) && int.TryParse(value2, out int percent2))
            {
                btnSetPercent0.Content = $"{percent2}%";
            }
        }
        // Skip saving column state on next unload (used by reset)
        public void SkipSaveOnClose()
        {
            _skipSaveColumnState = true;
        }

        // Save current column configuration to user settings
        private void SaveColumnState()
        {
            try
            {
                if (_skipSaveColumnState)
                    return;

                if (sfActivities?.Columns == null || sfActivities.Columns.Count == 0)
                    return;

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
                SettingsManager.SetUserSetting(GridPrefsKey, json, "json");
            }
            catch
            {
            }
        }

        // Public method for MainWindow to call after settings import
        public void ReloadColumnSettings()
        {
            LoadColumnState();
        }

        // Get current grid preferences for layout save
        public GridPreferencesData GetGridPreferences()
        {
            if (sfActivities?.Columns == null || sfActivities.Columns.Count == 0)
                return new GridPreferencesData();

            return new GridPreferencesData
            {
                Version = 1,
                SchemaHash = ComputeSchemaHash(sfActivities),
                Columns = sfActivities.Columns
                    .Select(c => new GridColumnPrefData
                    {
                        Name = c.MappingName,
                        OrderIndex = sfActivities.Columns.IndexOf(c),
                        Width = c.Width,
                        IsHidden = c.IsHidden
                    })
                    .ToList()
            };
        }

        // Apply external layout preferences
        public void ApplyGridPreferences(GridPreferencesData prefs)
        {
            try
            {
                if (sfActivities?.Columns == null || prefs?.Columns == null || prefs.Columns.Count == 0)
                    return;

                var byName = sfActivities.Columns.ToDictionary(c => c.MappingName, c => c);

                // Filter to only prefs whose columns still exist in the grid
                var matchedPrefs = prefs.Columns.Where(p => byName.ContainsKey(p.Name)).ToList();
                if (matchedPrefs.Count == 0)
                    return;

                // 1) Visibility first
                foreach (var p in matchedPrefs)
                    byName[p.Name].IsHidden = p.IsHidden;

                // 2) Order — place matched columns in their saved order, new columns stay at end
                var orderedMatched = matchedPrefs.OrderBy(x => x.OrderIndex).ToList();
                for (int target = 0; target < orderedMatched.Count; target++)
                {
                    var p = orderedMatched[target];
                    var col = byName[p.Name];
                    int cur = sfActivities.Columns.IndexOf(col);
                    if (cur != target && cur >= 0 && target < sfActivities.Columns.Count)
                    {
                        sfActivities.Columns.RemoveAt(cur);
                        sfActivities.Columns.Insert(target, col);
                    }
                }

                // 3) Width last (guard against tiny widths)
                const double MinWidth = 40.0;
                foreach (var p in matchedPrefs)
                    byName[p.Name].Width = Math.Max(MinWidth, p.Width);

                sfActivities.UpdateLayout();
            }
            catch
            {
            }
        }

        private void LoadColumnState()
        {
            try
            {
                if (sfActivities?.Columns == null)
                    return;

                var raw = SettingsManager.GetUserSetting(GridPrefsKey);
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                GridPreferences? prefs = null;
                try { prefs = JsonSerializer.Deserialize<GridPreferences>(raw); }
                catch { }

                if (prefs == null)
                    return;

                var byName = sfActivities.Columns.ToDictionary(c => c.MappingName, c => c);

                // Filter to only prefs whose columns still exist in the grid
                var matchedPrefs = prefs.Columns.Where(p => byName.ContainsKey(p.Name)).ToList();
                if (matchedPrefs.Count == 0)
                    return;

                // Visibility first
                foreach (var p in matchedPrefs)
                    byName[p.Name].IsHidden = p.IsHidden;

                // Order — place matched columns in their saved order, new columns stay at end
                var orderedMatched = matchedPrefs.OrderBy(x => x.OrderIndex).ToList();
                for (int target = 0; target < orderedMatched.Count; target++)
                {
                    var p = orderedMatched[target];
                    var col = byName[p.Name];
                    int cur = sfActivities.Columns.IndexOf(col);
                    if (cur != target && cur >= 0 && target < sfActivities.Columns.Count)
                    {
                        sfActivities.Columns.RemoveAt(cur);
                        sfActivities.Columns.Insert(target, col);
                    }
                }

                // Width last (guard against tiny widths)
                const double MinWidth = 40.0;
                foreach (var p in matchedPrefs)
                    byName[p.Name].Width = Math.Max(MinWidth, p.Width);

                // Re-save so schema hash is updated for next load
                var currentHash = ComputeSchemaHash(sfActivities);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                    SaveColumnState();

                sfActivities.UpdateLayout();
            }
            catch
            {
            }
        }

        // Load frozen column count from user settings
        private void LoadFrozenColumnCount()
        {
            try
            {
                var setting = SettingsManager.GetUserSetting(FrozenColumnsKey);
                if (!string.IsNullOrEmpty(setting) && int.TryParse(setting, out int count) && count > 0)
                {
                    sfActivities.FrozenColumnCount = count;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.LoadFrozenColumnCount");
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

            // Confirmation dialog before bulk update
            int selectedCount = sfActivities.SelectedItems.Count;
            if (selectedCount == 0)
            {
                MessageBox.Show("Please select one or more records.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var result = MessageBox.Show(
                $"Update {selectedCount:N0} selected record(s) to {percent}%?",
                "Confirm Bulk Update",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
                return;

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

            SettingsManager.SetUserSetting(
                settingKey,
                defaultValue.ToString(),
                "int");

            MessageBox.Show($"Button reset to {defaultValue}%", "Success",
                MessageBoxButton.OK, MessageBoxImage.None);
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
                    settingKey,
                    newPercent.ToString(),
                    "int"
                );

                MessageBox.Show($"{buttonName} updated to {newPercent}%",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
        }


        // Enumerate selected items in chunks to keep UI responsive
        // Returns UniqueIDs of selected records that belong to the current user
        private async Task<List<string>> GetSelectedUserUniqueIdsChunkedAsync(string currentUser)
        {
            var result = new List<string>();
            var items = sfActivities.SelectedItems;
            int count = items.Count;
            const int chunkSize = 500;

            for (int processed = 0; processed < count; processed += chunkSize)
            {
                // Process a chunk of items
                int endIndex = Math.Min(processed + chunkSize, count);
                for (int i = processed; i < endIndex; i++)
                {
                    if (items[i] is Activity a &&
                        string.Equals(a.AssignedTo, currentUser, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(a.UniqueID);
                    }
                }

                // Yield to UI thread to keep overlay animating
                if (processed + chunkSize < count)
                {
                    await Task.Delay(1);
                }
            }

            return result;
        }

        // Bulk update percent for selected records
        // Uses chunked enumeration for large selections to keep UI responsive
        private async Task SetSelectedRecordsPercent(int percent)
        {
            var mainWindow = Application.Current.Windows.OfType<VANTAGE.MainWindow>().FirstOrDefault();

            try
            {
                // Fast count check (O(1))
                int selectedCount = sfActivities.SelectedItems.Count;

                if (selectedCount == 0)
                {
                    MessageBox.Show("Please select one or more records.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                var currentUser = App.CurrentUser?.Username ?? "Unknown";
                const int largeSelectionThreshold = 5000;

                mainWindow?.ShowLoadingOverlay($"Processing {selectedCount:N0} selected records...");
                await Task.Delay(10);

                List<string> idsToUpdate;

                if (selectedCount > largeSelectionThreshold)
                {
                    // Large selection - use chunked enumeration to keep UI responsive
                    idsToUpdate = await GetSelectedUserUniqueIdsChunkedAsync(currentUser);
                }
                else
                {
                    // Small selection - enumerate directly (fast enough)
                    var selectedActivities = sfActivities.SelectedItems.Cast<Activity>().ToList();
                    idsToUpdate = selectedActivities
                        .Where(a => string.Equals(a.AssignedTo, currentUser, StringComparison.OrdinalIgnoreCase))
                        .Select(a => a.UniqueID)
                        .ToList();
                }

                if (idsToUpdate.Count == 0)
                {
                    MessageBox.Show("None of the selected records are assigned to you.",
                        "No Editable Records", MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                mainWindow?.ShowLoadingOverlay($"Updating {idsToUpdate.Count:N0} records...");

                int successCount = await ActivityRepository.BulkUpdatePercentAsync(idsToUpdate, percent, currentUser);

                // Reload from database to ensure consistency
                await _viewModel.RefreshAsync();
                UpdateSummaryPanel();
                await CalculateMetadataErrorCount();

                if (successCount > 0)
                {
                    MessageBox.Show($"Set {successCount:N0} record(s) to {percent}%.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.None);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.SetSelectedRecordsPercent");
                MessageBox.Show($"Error updating records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                mainWindow?.HideLoadingOverlay();
            }
        }
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update record count when filtered or total counts change
            if (e.PropertyName == nameof(_viewModel.TotalRecordCount) ||
                e.PropertyName == nameof(_viewModel.FilteredCount))
            {
                UpdateRecordCount();
            }
        }
        // Returns the currently filtered/visible activities from the grid
        public List<Activity> GetFilteredActivities()
        {
            if (sfActivities?.View?.Records == null)
                return _viewModel?.Activities?.ToList() ?? new List<Activity>();

            var filtered = new List<Activity>();
            foreach (var record in sfActivities.View.Records)
            {
                var recordType = record.GetType();
                var dataProperty = recordType.GetProperty("Data");
                if (dataProperty != null)
                {
                    var activity = dataProperty.GetValue(record) as Activity;
                    if (activity != null)
                        filtered.Add(activity);
                }
            }
            return filtered;
        }

        // Refreshes the grid and summary panel after external changes
        public void RefreshAfterProrate()
        {
            sfActivities.View.Refresh();
            UpdateSummaryPanel();
        }

        // Debounced wrapper - coalesces rapid filter changes into a single update
        private void DebouncedUpdateSummary()
        {
            if (_summaryDebounce == null)
            {
                _summaryDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _summaryDebounce.Tick += (s, e) =>
                {
                    _summaryDebounce.Stop();
                    UpdateSummaryPanel();
                };
            }

            _summaryDebounce.Stop();
            _summaryDebounce.Start();
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
                // Filter is active - extract filtered activities via direct cast
                recordsToSum = new List<Activity>();

                foreach (var record in sfActivities.View.Records)
                {
                    if (record is RecordEntry entry && entry.Data is Activity activity)
                        recordsToSum.Add(activity);
                }
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

            // Call ViewModel method to update bound properties
            if (_viewModel != null)
            {
                await _viewModel.UpdateTotalsAsync(recordsToSum);
            }
        }

        /// Auto-save when user finishes editing a cell

        private bool _dataLoaded = false;

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            // Skip reload if data is already in memory (cached view re-navigation)
            if (_dataLoaded) return;

            // Load saved summary column preference before loading data
            _viewModel.LoadSummaryColumnPreference();

            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdateSummaryPanel();
            await CalculateMetadataErrorCount();

            _dataLoaded = true;
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





        // === SUMMARY COLUMN SELECTOR ===

        // Opens the context menu when clicking on the summary column name
        private void SummaryColumnSelector_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.ContextMenu != null)
            {
                textBlock.ContextMenu.PlacementTarget = textBlock;
                textBlock.ContextMenu.Placement = PlacementMode.Bottom;
                textBlock.ContextMenu.IsOpen = true;
            }
        }

        // Handles selection of a column from the summary column context menu
        private void SummaryColumnMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is MenuItem menuItem && menuItem.Header is string columnName)
            {
                _viewModel.SelectedSummaryColumn = columnName;
            }
        }

        // === FILTER EVENT HANDLERS ===

        private void BtnFilterLocalDirty_Click(object sender, RoutedEventArgs e)
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
                btnFilterLocalDirty.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Clear this filter
                sfActivities.Columns["LocalDirty"].FilterPredicates.Clear();

                // Update button visuals - inactive
                btnFilterLocalDirty.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
        }

        private void BtnUserFilters_Click(object sender, RoutedEventArgs e)
        {
            // Populate and show context menu
            PopulateUserFiltersMenu();
            ctxUserFilters.PlacementTarget = btnUserFilters;
            ctxUserFilters.Placement = PlacementMode.Bottom;
            ctxUserFilters.IsOpen = true;
        }

        private void PopulateUserFiltersMenu()
        {
            ctxUserFilters.Items.Clear();

            // Get saved filters
            var filters = ManageFiltersDialog.GetSavedFilters();

            // Add each saved filter as a menu item
            foreach (var filter in filters)
            {
                var menuItem = new MenuItem
                {
                    Header = filter.Name,
                    Tag = filter
                };
                menuItem.Click += UserFilterMenuItem_Click;

                // Show checkmark if this is the active filter
                if (_activeUserFilter != null && _activeUserFilter.Name == filter.Name)
                {
                    menuItem.Icon = new System.Windows.Controls.TextBlock { Text = "\u2713", FontWeight = FontWeights.Bold };
                }

                ctxUserFilters.Items.Add(menuItem);
            }

            // Add Clear Filter option
            var clearItem = new MenuItem
            {
                Header = "Clear Filter",
                IsEnabled = _activeUserFilter != null
            };
            clearItem.Click += ClearUserFilter_Click;
            ctxUserFilters.Items.Add(clearItem);

            // Add Manage option
            var manageItem = new MenuItem
            {
                Header = "Manage..."
            };
            manageItem.Click += ManageFilters_Click;
            ctxUserFilters.Items.Add(manageItem);
        }

        private void UserFilterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is UserFilter filter)
            {
                ApplyUserFilter(filter);
            }
        }

        private void ApplyUserFilter(UserFilter filter)
        {
            try
            {
                _activeUserFilter = filter;

                // Update button text to show filter name
                txtUserFilterName.Text = filter.Name;

                // Highlight button border
                btnUserFilters.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];

                // Apply combined filter (includes global search + user filter)
                ApplyCombinedViewFilter();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.ApplyUserFilter");
                MessageBox.Show($"Error applying filter: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool EvaluateCondition(Activity activity, FilterCondition condition)
        {
            try
            {
                // Get property value using reflection
                var property = typeof(Activity).GetProperty(condition.Column);
                if (property == null)
                    return true;

                var value = property.GetValue(activity);
                var stringValue = value?.ToString() ?? string.Empty;
                var compareValue = condition.Value ?? string.Empty;

                switch (condition.Criteria)
                {
                    case FilterCriteria.Equals:
                        if (value is double d)
                            return double.TryParse(compareValue, out var cd) && Math.Abs(d - cd) < 0.0001;
                        if (value is int i)
                            return int.TryParse(compareValue, out var ci) && i == ci;
                        return stringValue.Equals(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.NotEquals:
                        if (value is double d2)
                            return !double.TryParse(compareValue, out var cd2) || Math.Abs(d2 - cd2) >= 0.0001;
                        if (value is int i2)
                            return !int.TryParse(compareValue, out var ci2) || i2 != ci2;
                        return !stringValue.Equals(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.Contains:
                        return stringValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.NotContains:
                        return !stringValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.StartsWith:
                        return stringValue.StartsWith(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.EndsWith:
                        return stringValue.EndsWith(compareValue, StringComparison.OrdinalIgnoreCase);

                    case FilterCriteria.IsEmpty:
                        return string.IsNullOrEmpty(stringValue);

                    case FilterCriteria.IsNotEmpty:
                        return !string.IsNullOrEmpty(stringValue);

                    case FilterCriteria.GreaterThan:
                        if (double.TryParse(stringValue, out var numVal) && double.TryParse(compareValue, out var numCompare))
                            return numVal > numCompare;
                        return false;

                    case FilterCriteria.GreaterThanOrEqual:
                        if (double.TryParse(stringValue, out var numVal2) && double.TryParse(compareValue, out var numCompare2))
                            return numVal2 >= numCompare2;
                        return false;

                    case FilterCriteria.LessThan:
                        if (double.TryParse(stringValue, out var numVal3) && double.TryParse(compareValue, out var numCompare3))
                            return numVal3 < numCompare3;
                        return false;

                    case FilterCriteria.LessThanOrEqual:
                        if (double.TryParse(stringValue, out var numVal4) && double.TryParse(compareValue, out var numCompare4))
                            return numVal4 <= numCompare4;
                        return false;

                    default:
                        return true;
                }
            }
            catch
            {
                return true;
            }
        }

        private void ClearUserFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearUserFilter();
        }

        private void ClearUserFilter()
        {
            _activeUserFilter = null;
            txtUserFilterName.Text = "USER";
            btnUserFilters.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            // Re-apply combined filter (keeps global search and Today filter if active)
            ApplyCombinedViewFilter();
        }

        private void ManageFilters_Click(object sender, RoutedEventArgs e)
        {
            // Clear active user filter when opening Manage dialog
            ClearUserFilter();

            // Get available columns from the grid
            var columns = sfActivities.Columns
                .Select(c => c.MappingName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            var dialog = new ManageFiltersDialog(columns)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.BusyDialog? busyDialog = null;
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
                        MessageBoxImage.None);
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
                        Background = ThemeHelper.BackgroundColor
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
                    stack.Children.Add(new TextBlock { Text = "Select project to submit:", Margin = new Thickness(10), Foreground = ThemeHelper.ForegroundColor });
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
                    Background = ThemeHelper.BackgroundColor
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
                dateStack.Children.Add(new TextBlock { Text = "Select week ending date:", Margin = new Thickness(10), Foreground = ThemeHelper.ForegroundColor });
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

                // Show busy indicator immediately so user sees feedback
                busyDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Validating...");
                busyDialog.Show();

                // Check for dates that are after the selected WeekEndDate
                var futureStartDates = _viewModel.Activities
                    .Where(a => a.AssignedTo == App.CurrentUser?.Username &&
                                a.ProjectID == selectedProject &&
                                a.ActStart != null &&
                                a.ActStart.Value.Date > selectedWeekEndDate.Date)
                    .ToList();

                var futureFinishDates = _viewModel.Activities
                    .Where(a => a.AssignedTo == App.CurrentUser?.Username &&
                                a.ProjectID == selectedProject &&
                                a.ActFin != null &&
                                a.ActFin.Value.Date > selectedWeekEndDate.Date)
                    .ToList();

                if (futureStartDates.Any() || futureFinishDates.Any())
                {
                    string message = $"Some records have dates after the selected week ending {selectedWeekEndDate:MM/dd/yyyy}:\n\n";

                    if (futureStartDates.Any())
                        message += $"• {futureStartDates.Count} activity(s) have Start dates after the selected week\n";
                    if (futureFinishDates.Any())
                        message += $"• {futureFinishDates.Count} activity(s) have Finish dates after the selected week\n";

                    message += $"\nWould you like to set these dates to {selectedWeekEndDate:MM/dd/yyyy} and continue?";

                    busyDialog.Hide();
                    var dateFixResult = MessageBox.Show(message, "Adjust Dates?",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (dateFixResult != MessageBoxResult.Yes)
                    {
                        busyDialog.Close();
                        return;
                    }

                    busyDialog.Show();
                    busyDialog.UpdateStatus("Adjusting dates...");

                    // Update the offending dates to the selected WE date
                    foreach (var activity in futureStartDates)
                    {
                        activity.ActStart = selectedWeekEndDate;
                        activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                        activity.UpdatedUtcDate = DateTime.UtcNow;
                        activity.LocalDirty = 1;
                        await ActivityRepository.UpdateActivityInDatabase(activity);
                    }
                    foreach (var activity in futureFinishDates)
                    {
                        // ActStart may have already been fixed above, only update ActFin
                        activity.ActFin = selectedWeekEndDate;
                        activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                        activity.UpdatedUtcDate = DateTime.UtcNow;
                        activity.LocalDirty = 1;
                        await ActivityRepository.UpdateActivityInDatabase(activity);
                    }

                    AppLogger.Info($"Adjusted {futureStartDates.Count} ActStart and {futureFinishDates.Count} ActFin dates to {weekEndDateStr}",
                        "ProgressView.BtnSubmit_Click", App.CurrentUser?.Username);
                }
                // Step 5: Check for existing snapshots on Azure
                busyDialog.UpdateStatus("Checking for existing snapshots...");
                var existingExportedBy = await Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var checkCmd = azureConn.CreateCommand();
                    checkCmd.CommandText = @"
                SELECT TOP 1 ExportedBy
                FROM VMS_ProgressSnapshots
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
                        busyDialog.Close();
                        MessageBox.Show(
                            $"Progress for week ending {weekEndDateStr} has already been exported by {exportedByUser}.\n\n" +
                            "You cannot overwrite snapshots exported by another user.",
                            "Already Exported",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    busyDialog.Hide();
                    var overwriteResult = MessageBox.Show(
                        $"You already exported progress for week ending {weekEndDateStr}.\n\n" +
                        "Do you want to overwrite with current progress?\n\n" +
                        "Note: This will clear the export lock.",
                        "Overwrite Exported Snapshots?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (overwriteResult != MessageBoxResult.Yes)
                    {
                        busyDialog.Close();
                        return;
                    }

                    busyDialog.Show();
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
                    FROM VMS_ProgressSnapshots
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
                        busyDialog.Hide();
                        var overwriteResult = MessageBox.Show(
                            $"You already have {existingCount} snapshots for week ending {weekEndDateStr}.\n\n" +
                            "Do you want to overwrite them with current progress?",
                            "Overwrite Existing?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (overwriteResult != MessageBoxResult.Yes)
                        {
                            busyDialog.Close();
                            return;
                        }

                        busyDialog.Show();
                        needsDelete = true;
                    }
                }

                // Step 7: Continue with submission
                busyDialog.UpdateStatus("Starting...");
                var progress = new Progress<string>(status => busyDialog.UpdateStatus(status));
                var currentUser = App.CurrentUser!.Username;
                var progDateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var ownerWindow = Window.GetWindow(this);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var (success, submitError, snapshotCount, skippedCount, skippedRecords) = await Task.Run(async () =>
                    {
                        var skippedList = new List<(string UniqueID, string SchedActNO, string Description)>();
                        var reporter = (IProgress<string>)progress;

                        // Step 6: Delete existing snapshots if needed
                        if (needsDelete)
                        {
                            reporter.Report("Deleting existing snapshots...");

                            using var deleteConn = AzureDbManager.GetConnection();
                            deleteConn.Open();

                            var deleteCmd = deleteConn.CreateCommand();
                            deleteCmd.CommandTimeout = 0;
                            deleteCmd.CommandText = @"
                        DELETE FROM VMS_ProgressSnapshots
                        WHERE AssignedTo = @username
                          AND ProjectID = @projectId
                          AND WeekEndDate = @weekEndDate";
                            deleteCmd.Parameters.AddWithValue("@username", currentUser);
                            deleteCmd.Parameters.AddWithValue("@projectId", selectedProject);
                            deleteCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);
                            var deletedCount = deleteCmd.ExecuteNonQuery();

                            AppLogger.Info($"Deleted {deletedCount} existing snapshots for overwrite", "ProgressView.BtnSubmit_Click", currentUser);
                        }

                        // Step 7: Read local activities and check for existing snapshots
                        reporter.Report("Reading local activities...");

                        // Read local activities for this user/project
                        var localActivities = new System.Data.DataTable();
                        using (var localConn = DatabaseSetup.GetConnection())
                        {
                            localConn.Open();
                            var selectCmd = localConn.CreateCommand();
                            selectCmd.CommandText = @"
                                SELECT UniqueID, Area, AssignedTo, AzureUploadUtcDate,
                                    Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                                    ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                                    DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                                    EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                                    MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                                    PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY,
                                    ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                                    ROCStep, SchedActNO, ActFin, ActStart, SecondActno, SecondDwgNO,
                                    Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo, TagNO,
                                    UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                                    UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                                    UpdatedBy, UpdatedUtcDate, UOM, WorkPackage, XRay
                                FROM Activities
                                WHERE AssignedTo = @username
                                  AND ProjectID = @projectId";
                            selectCmd.Parameters.AddWithValue("@username", currentUser);
                            selectCmd.Parameters.AddWithValue("@projectId", selectedProject);

                            // Manually fill DataTable from SQLite reader
                            using var reader = selectCmd.ExecuteReader();
                            localActivities.Load(reader);
                        }

                        if (localActivities.Rows.Count == 0)
                        {
                            return (false, "No local activities found for this user/project.", 0, 0, skippedList);
                        }

                        reporter.Report("Checking for existing snapshots...");

                        // Get existing snapshot UniqueIDs for this week from Azure
                        var existingSnapshotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var snapshotOwners = new Dictionary<string, (string SubmittedBy, string SnapshotProject)>(StringComparer.OrdinalIgnoreCase);

                        using (var azureConn = AzureDbManager.GetConnection())
                        {
                            azureConn.Open();

                            var existingCmd = azureConn.CreateCommand();
                            existingCmd.CommandText = @"
                                SELECT UniqueID, AssignedTo, ProjectID
                                FROM VMS_ProgressSnapshots
                                WHERE WeekEndDate = @weekEndDate";
                            existingCmd.Parameters.AddWithValue("@weekEndDate", weekEndDateStr);

                            using var reader = existingCmd.ExecuteReader();
                            while (reader.Read())
                            {
                                string uniqueId = reader.GetString(0);
                                existingSnapshotIds.Add(uniqueId);
                                snapshotOwners[uniqueId] = (
                                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    reader.IsDBNull(2) ? "" : reader.GetString(2)
                                );
                            }
                        }

                        // Build list of activities to insert (skip existing) and track skipped
                        var activitiesToInsert = new List<System.Data.DataRow>();
                        foreach (System.Data.DataRow row in localActivities.Rows)
                        {
                            string uniqueId = row["UniqueID"].ToString()!;
                            if (existingSnapshotIds.Contains(uniqueId))
                            {
                                // Track skipped record
                                if (snapshotOwners.TryGetValue(uniqueId, out var owner))
                                {
                                    skippedList.Add((
                                        uniqueId,
                                        row["SchedActNO"]?.ToString() ?? "",
                                        $"{row["Description"]?.ToString() ?? ""} [Submitted by: {owner.SubmittedBy}, Project: {owner.SnapshotProject}]"
                                    ));
                                }
                            }
                            else
                            {
                                activitiesToInsert.Add(row);
                            }
                        }

                        // Check for conflicts - records submitted by OTHER users
                        var conflicts = skippedList
                            .Where(s => !string.IsNullOrEmpty(s.Description) &&
                                       s.Description.Contains("Submitted by:") &&
                                       !s.Description.Contains($"Submitted by: {currentUser},", StringComparison.OrdinalIgnoreCase))
                            .GroupBy(s => {
                                var match = System.Text.RegularExpressions.Regex.Match(s.Description, @"Submitted by: ([^,]+),");
                                return match.Success ? match.Groups[1].Value : "Unknown";
                            })
                            .Select(g => (User: g.Key, Count: g.Count()))
                            .ToList();

                        if (conflicts.Any())
                        {
                            int conflictCount = conflicts.Sum(c => c.Count);
                            var conflictDetails = string.Join("\n", conflicts.Select(c => $"  • {c.Count} records by {c.User}"));

                            bool proceed = false;
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                busyDialog.Hide();

                                var result = MessageBox.Show(
                                    ownerWindow,
                                    $"{conflictCount} of your assigned records were already submitted for this week by other users:\n\n" +
                                    $"{conflictDetails}\n\n" +
                                    $"These records will be SKIPPED. Do you want to submit the remaining records?\n\n" +
                                    $"(You may need to coordinate with the users above if this is unexpected.)",
                                    "Records Already Submitted",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning);
                                proceed = (result == MessageBoxResult.Yes);

                                if (proceed)
                                    busyDialog.Show();
                            });

                            if (!proceed)
                                return (false, "Cancelled by user", 0, 0, skippedList);
                        }

                        // Step 8: Insert snapshots from local data
                        reporter.Report("Creating snapshots...");
                        int snapshots = 0;

                        if (activitiesToInsert.Count > 0)
                        {
                            using var azureConn = AzureDbManager.GetConnection();
                            azureConn.Open();

                            // Build and execute bulk insert using SqlBulkCopy
                            var snapshotTable = new System.Data.DataTable();
                            snapshotTable.Columns.Add("UniqueID", typeof(string));
                            snapshotTable.Columns.Add("WeekEndDate", typeof(string));
                            snapshotTable.Columns.Add("Area", typeof(string));
                            snapshotTable.Columns.Add("AssignedTo", typeof(string));
                            snapshotTable.Columns.Add("AzureUploadUtcDate", typeof(string));
                            snapshotTable.Columns.Add("Aux1", typeof(string));
                            snapshotTable.Columns.Add("Aux2", typeof(string));
                            snapshotTable.Columns.Add("Aux3", typeof(string));
                            snapshotTable.Columns.Add("BaseUnit", typeof(double));
                            snapshotTable.Columns.Add("BudgetHoursGroup", typeof(double));
                            snapshotTable.Columns.Add("BudgetHoursROC", typeof(double));
                            snapshotTable.Columns.Add("BudgetMHs", typeof(double));
                            snapshotTable.Columns.Add("ChgOrdNO", typeof(string));
                            snapshotTable.Columns.Add("ClientBudget", typeof(double));
                            snapshotTable.Columns.Add("ClientCustom3", typeof(double));
                            snapshotTable.Columns.Add("ClientEquivQty", typeof(double));
                            snapshotTable.Columns.Add("CompType", typeof(string));
                            snapshotTable.Columns.Add("CreatedBy", typeof(string));
                            snapshotTable.Columns.Add("DateTrigger", typeof(int));
                            snapshotTable.Columns.Add("Description", typeof(string));
                            snapshotTable.Columns.Add("DwgNO", typeof(string));
                            snapshotTable.Columns.Add("EarnQtyEntry", typeof(double));
                            snapshotTable.Columns.Add("EarnedMHsRoc", typeof(double));
                            snapshotTable.Columns.Add("EqmtNO", typeof(string));
                            snapshotTable.Columns.Add("EquivQTY", typeof(string));
                            snapshotTable.Columns.Add("EquivUOM", typeof(string));
                            snapshotTable.Columns.Add("Estimator", typeof(string));
                            snapshotTable.Columns.Add("HexNO", typeof(int));
                            snapshotTable.Columns.Add("HtTrace", typeof(string));
                            snapshotTable.Columns.Add("InsulType", typeof(string));
                            snapshotTable.Columns.Add("LineNumber", typeof(string));
                            snapshotTable.Columns.Add("MtrlSpec", typeof(string));
                            snapshotTable.Columns.Add("Notes", typeof(string));
                            snapshotTable.Columns.Add("PaintCode", typeof(string));
                            snapshotTable.Columns.Add("PercentEntry", typeof(double));
                            snapshotTable.Columns.Add("PhaseCategory", typeof(string));
                            snapshotTable.Columns.Add("PhaseCode", typeof(string));
                            snapshotTable.Columns.Add("PipeGrade", typeof(string));
                            snapshotTable.Columns.Add("PipeSize1", typeof(double));
                            snapshotTable.Columns.Add("PipeSize2", typeof(double));
                            snapshotTable.Columns.Add("PrevEarnMHs", typeof(double));
                            snapshotTable.Columns.Add("PrevEarnQTY", typeof(double));
                            snapshotTable.Columns.Add("ProgDate", typeof(string));
                            snapshotTable.Columns.Add("ProjectID", typeof(string));
                            snapshotTable.Columns.Add("Quantity", typeof(double));
                            snapshotTable.Columns.Add("RevNO", typeof(string));
                            snapshotTable.Columns.Add("RFINO", typeof(string));
                            snapshotTable.Columns.Add("ROCBudgetQTY", typeof(double));
                            snapshotTable.Columns.Add("ROCID", typeof(double));
                            snapshotTable.Columns.Add("ROCPercent", typeof(double));
                            snapshotTable.Columns.Add("ROCStep", typeof(string));
                            snapshotTable.Columns.Add("SchedActNO", typeof(string));
                            snapshotTable.Columns.Add("ActFin", typeof(string));
                            snapshotTable.Columns.Add("ActStart", typeof(string));
                            snapshotTable.Columns.Add("SecondActno", typeof(string));
                            snapshotTable.Columns.Add("SecondDwgNO", typeof(string));
                            snapshotTable.Columns.Add("Service", typeof(string));
                            snapshotTable.Columns.Add("ShopField", typeof(string));
                            snapshotTable.Columns.Add("ShtNO", typeof(string));
                            snapshotTable.Columns.Add("SubArea", typeof(string));
                            snapshotTable.Columns.Add("PjtSystem", typeof(string));
                            snapshotTable.Columns.Add("PjtSystemNo", typeof(string));
                            snapshotTable.Columns.Add("TagNO", typeof(string));
                            snapshotTable.Columns.Add("UDF1", typeof(string));
                            snapshotTable.Columns.Add("UDF2", typeof(string));
                            snapshotTable.Columns.Add("UDF3", typeof(string));
                            snapshotTable.Columns.Add("UDF4", typeof(string));
                            snapshotTable.Columns.Add("UDF5", typeof(string));
                            snapshotTable.Columns.Add("UDF6", typeof(string));
                            snapshotTable.Columns.Add("UDF7", typeof(string));
                            snapshotTable.Columns.Add("UDF8", typeof(string));
                            snapshotTable.Columns.Add("UDF9", typeof(string));
                            snapshotTable.Columns.Add("UDF10", typeof(string));
                            snapshotTable.Columns.Add("UDF11", typeof(string));
                            snapshotTable.Columns.Add("UDF12", typeof(string));
                            snapshotTable.Columns.Add("UDF13", typeof(string));
                            snapshotTable.Columns.Add("UDF14", typeof(string));
                            snapshotTable.Columns.Add("UDF15", typeof(string));
                            snapshotTable.Columns.Add("UDF16", typeof(string));
                            snapshotTable.Columns.Add("UDF17", typeof(string));
                            snapshotTable.Columns.Add("RespParty", typeof(string));
                            snapshotTable.Columns.Add("UDF20", typeof(string));
                            snapshotTable.Columns.Add("UpdatedBy", typeof(string));
                            snapshotTable.Columns.Add("UpdatedUtcDate", typeof(string));
                            snapshotTable.Columns.Add("UOM", typeof(string));
                            snapshotTable.Columns.Add("WorkPackage", typeof(string));
                            snapshotTable.Columns.Add("XRay", typeof(double));
                            snapshotTable.Columns.Add("ExportedBy", typeof(string));
                            snapshotTable.Columns.Add("ExportedDate", typeof(string));

                            foreach (var row in activitiesToInsert)
                            {
                                var newRow = snapshotTable.NewRow();
                                newRow["UniqueID"] = row["UniqueID"];
                                newRow["WeekEndDate"] = weekEndDateStr;
                                newRow["Area"] = row["Area"];
                                newRow["AssignedTo"] = row["AssignedTo"];
                                newRow["AzureUploadUtcDate"] = row["AzureUploadUtcDate"] ?? DBNull.Value;
                                newRow["Aux1"] = row["Aux1"];
                                newRow["Aux2"] = row["Aux2"];
                                newRow["Aux3"] = row["Aux3"];
                                newRow["BaseUnit"] = Convert.ToDouble(row["BaseUnit"]);
                                newRow["BudgetHoursGroup"] = Convert.ToDouble(row["BudgetHoursGroup"]);
                                newRow["BudgetHoursROC"] = Convert.ToDouble(row["BudgetHoursROC"]);
                                newRow["BudgetMHs"] = Convert.ToDouble(row["BudgetMHs"]);
                                newRow["ChgOrdNO"] = row["ChgOrdNO"];
                                newRow["ClientBudget"] = Convert.ToDouble(row["ClientBudget"]);
                                newRow["ClientCustom3"] = Convert.ToDouble(row["ClientCustom3"]);
                                newRow["ClientEquivQty"] = Convert.ToDouble(row["ClientEquivQty"]);
                                newRow["CompType"] = row["CompType"];
                                newRow["CreatedBy"] = row["CreatedBy"];
                                newRow["DateTrigger"] = Convert.ToInt32(row["DateTrigger"]);
                                newRow["Description"] = row["Description"];
                                newRow["DwgNO"] = row["DwgNO"];
                                newRow["EarnQtyEntry"] = Convert.ToDouble(row["EarnQtyEntry"]);
                                newRow["EarnedMHsRoc"] = Convert.ToDouble(row["EarnedMHsRoc"]);
                                newRow["EqmtNO"] = row["EqmtNO"];
                                newRow["EquivQTY"] = row["EquivQTY"];
                                newRow["EquivUOM"] = row["EquivUOM"];
                                newRow["Estimator"] = row["Estimator"];
                                newRow["HexNO"] = Convert.ToInt32(row["HexNO"]);
                                newRow["HtTrace"] = row["HtTrace"];
                                newRow["InsulType"] = row["InsulType"];
                                newRow["LineNumber"] = row["LineNumber"];
                                newRow["MtrlSpec"] = row["MtrlSpec"];
                                newRow["Notes"] = row["Notes"];
                                newRow["PaintCode"] = row["PaintCode"];
                                newRow["PercentEntry"] = Convert.ToDouble(row["PercentEntry"]);
                                newRow["PhaseCategory"] = row["PhaseCategory"];
                                newRow["PhaseCode"] = row["PhaseCode"];
                                newRow["PipeGrade"] = row["PipeGrade"];
                                newRow["PipeSize1"] = Convert.ToDouble(row["PipeSize1"]);
                                newRow["PipeSize2"] = Convert.ToDouble(row["PipeSize2"]);
                                newRow["PrevEarnMHs"] = Convert.ToDouble(row["PrevEarnMHs"]);
                                newRow["PrevEarnQTY"] = Convert.ToDouble(row["PrevEarnQTY"]);
                                newRow["ProgDate"] = progDateStr;
                                newRow["ProjectID"] = row["ProjectID"];
                                newRow["Quantity"] = Convert.ToDouble(row["Quantity"]);
                                newRow["RevNO"] = row["RevNO"];
                                newRow["RFINO"] = row["RFINO"];
                                newRow["ROCBudgetQTY"] = Convert.ToDouble(row["ROCBudgetQTY"]);
                                newRow["ROCID"] = Convert.ToDouble(row["ROCID"]);
                                newRow["ROCPercent"] = Convert.ToDouble(row["ROCPercent"]);
                                newRow["ROCStep"] = row["ROCStep"];
                                newRow["SchedActNO"] = row["SchedActNO"];
                                newRow["ActFin"] = row["ActFin"] ?? DBNull.Value;
                                newRow["ActStart"] = row["ActStart"] ?? DBNull.Value;
                                newRow["SecondActno"] = row["SecondActno"];
                                newRow["SecondDwgNO"] = row["SecondDwgNO"];
                                newRow["Service"] = row["Service"];
                                newRow["ShopField"] = row["ShopField"];
                                newRow["ShtNO"] = row["ShtNO"];
                                newRow["SubArea"] = row["SubArea"];
                                newRow["PjtSystem"] = row["PjtSystem"];
                                newRow["PjtSystemNo"] = row["PjtSystemNo"];
                                newRow["TagNO"] = row["TagNO"];
                                newRow["UDF1"] = row["UDF1"];
                                newRow["UDF2"] = row["UDF2"];
                                newRow["UDF3"] = row["UDF3"];
                                newRow["UDF4"] = row["UDF4"];
                                newRow["UDF5"] = row["UDF5"];
                                newRow["UDF6"] = row["UDF6"];
                                newRow["UDF7"] = row["UDF7"];
                                newRow["UDF8"] = row["UDF8"];
                                newRow["UDF9"] = row["UDF9"];
                                newRow["UDF10"] = row["UDF10"];
                                newRow["UDF11"] = row["UDF11"];
                                newRow["UDF12"] = row["UDF12"];
                                newRow["UDF13"] = row["UDF13"];
                                newRow["UDF14"] = row["UDF14"];
                                newRow["UDF15"] = row["UDF15"];
                                newRow["UDF16"] = row["UDF16"];
                                newRow["UDF17"] = row["UDF17"];
                                newRow["RespParty"] = row["RespParty"];
                                newRow["UDF20"] = row["UDF20"];
                                newRow["UpdatedBy"] = row["UpdatedBy"];
                                newRow["UpdatedUtcDate"] = row["UpdatedUtcDate"];
                                newRow["UOM"] = row["UOM"];
                                newRow["WorkPackage"] = row["WorkPackage"];
                                newRow["XRay"] = Convert.ToDouble(row["XRay"]);
                                newRow["ExportedBy"] = DBNull.Value;
                                newRow["ExportedDate"] = DBNull.Value;
                                snapshotTable.Rows.Add(newRow);
                            }

                            using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(azureConn);
                            bulkCopy.DestinationTableName = "VMS_ProgressSnapshots";
                            bulkCopy.BulkCopyTimeout = 0;

                            // Map columns
                            foreach (System.Data.DataColumn col in snapshotTable.Columns)
                            {
                                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            }

                            bulkCopy.WriteToServer(snapshotTable);
                            snapshots = snapshotTable.Rows.Count;
                        }

                        int skipped = skippedList.Count;
                        AppLogger.Info($"Created {snapshots} snapshots for week ending {weekEndDateStr} ({skipped} skipped)", "ProgressView.BtnSubmit_Click", currentUser);

                        // Step 9: Purge old snapshots (older than 4 weeks from today)
                        reporter.Report("Cleaning up old snapshots...");

                        using (var purgeConn = AzureDbManager.GetConnection())
                        {
                            purgeConn.Open();
                            var purgeCmd = purgeConn.CreateCommand();
                            purgeCmd.CommandTimeout = 0;
                            purgeCmd.CommandText = @"
                                DELETE FROM VMS_ProgressSnapshots
                                WHERE WeekEndDate < @cutoffDate";
                            purgeCmd.Parameters.AddWithValue("@cutoffDate", DateTime.Now.AddDays(-28).ToString("yyyy-MM-dd"));

                            int purgedCount = purgeCmd.ExecuteNonQuery();

                            if (purgedCount > 0)
                            {
                                AppLogger.Info($"Purged {purgedCount} old snapshots (before {DateTime.Now.AddDays(-28):yyyy-MM-dd})",
                                    "ProgressView.BtnSubmit_Click", currentUser);
                            }
                        }

                        // Step 10: Update local Activities.WeekEndDate and ProgDate
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

                            // Step 10b: Update PrevEarnMHs to current EarnMHsCalc for submitted records
                            // This captures the baseline so users can track week-over-week progress
                            if (activitiesToInsert.Count > 0)
                            {
                                var uniqueIds = activitiesToInsert.Select(r => r["UniqueID"].ToString()).ToList();
                                var idParams = string.Join(",", uniqueIds.Select((_, i) => $"@id{i}"));

                                var prevEarnCmd = localConn.CreateCommand();
                                prevEarnCmd.CommandText = $@"
                                    UPDATE Activities
                                    SET PrevEarnMHs = CASE
                                        WHEN PercentEntry >= 100 THEN BudgetMHs
                                        ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3)
                                    END,
                                    LocalDirty = 1
                                    WHERE UniqueID IN ({idParams})";

                                for (int i = 0; i < uniqueIds.Count; i++)
                                {
                                    prevEarnCmd.Parameters.AddWithValue($"@id{i}", uniqueIds[i]);
                                }

                                prevEarnCmd.ExecuteNonQuery();
                            }
                        }

                        return (true, string.Empty, snapshots, skipped, skippedList);
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

                    // Step 11: Refresh grid
                    busyDialog.UpdateStatus("Refreshing...");
                    await RefreshData();

                    busyDialog.Close();

                    stopwatch.Stop();
                    var msg = $"Successfully submitted {snapshotCount:N0} activities for week ending {weekEndDateStr}.";
                    msg += $"\n\nElapsed: {stopwatch.Elapsed.TotalSeconds:F1} seconds";

                    if (skippedCount > 0 && skippedRecords.Count > 0)
                    {
                        msg += $"\n\n{skippedCount:N0} records were skipped (already existed in snapshots).";
                        msg += "\n\nWould you like to export the skipped records to Excel?";

                        var exportResult = MessageBox.Show(msg, "Progress Submitted", MessageBoxButton.YesNo, MessageBoxImage.None);
                        if (exportResult == MessageBoxResult.Yes)
                        {
                            ExportSkippedRecordsToExcel(skippedRecords, weekEndDateStr);
                        }
                    }
                    else if (skippedCount > 0)
                    {
                        msg += $"\n\n{skippedCount:N0} records were skipped (already existed in snapshots).";
                        MessageBox.Show(msg, "Progress Submitted", MessageBoxButton.OK, MessageBoxImage.None);
                    }
                    else
                    {
                        MessageBox.Show(msg, "Progress Submitted", MessageBoxButton.OK, MessageBoxImage.None);
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
                busyDialog?.Close();
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

        // Export skipped snapshot records to Excel
        private void ExportSkippedRecordsToExcel(List<(string UniqueID, string SchedActNO, string Description)> skippedRecords, string weekEndDate)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"SkippedRecords_{weekEndDate.Replace("-", "")}.xlsx",
                    Title = "Export Skipped Records"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Skipped Records");

                // Headers
                worksheet.Cell(1, 1).Value = "UniqueID";
                worksheet.Cell(1, 2).Value = "SchedActNO";
                worksheet.Cell(1, 3).Value = "Description";
                worksheet.Cell(1, 4).Value = "WeekEndDate";

                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

                // Data rows
                for (int i = 0; i < skippedRecords.Count; i++)
                {
                    var record = skippedRecords[i];
                    worksheet.Cell(i + 2, 1).Value = record.UniqueID;
                    worksheet.Cell(i + 2, 2).Value = record.SchedActNO;
                    worksheet.Cell(i + 2, 3).Value = record.Description;
                    worksheet.Cell(i + 2, 4).Value = weekEndDate;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(saveDialog.FileName);

                MessageBox.Show($"Exported {skippedRecords.Count} skipped records to:\n\n{saveDialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.ExportSkippedRecordsToExcel");
                MessageBox.Show($"Error exporting skipped records:\n\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        "Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, RespParty\n" +
                        "Conditional: ActStart (when % > 0), ActFin (when % = 100)",
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
                            MessageBoxImage.None);
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
                    Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor"),
                    Tag = column
                };

                checkBox.Checked += CheckBox_ShowColumn;
                checkBox.Unchecked += CheckBox_HideColumn;

                listBox.Items.Add(checkBox);
            }

            Grid.SetRow(listBox, 0);
            grid.Children.Add(listBox);

            // Button panel with Default and Close
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var defaultButton = new Button
            {
                Content = "Default",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = "Reset column visibility to defaults"
            };
            defaultButton.Click += (s, args) =>
            {
                // Reset columns to XAML-defined defaults
                foreach (var column in sfActivities.Columns)
                    column.IsHidden = _xamlHiddenColumns.Contains(column.MappingName);

                // Update checkboxes without triggering their events
                foreach (CheckBox cb in listBox.Items)
                {
                    // Detach events, update, reattach
                    cb.Checked -= CheckBox_ShowColumn;
                    cb.Unchecked -= CheckBox_HideColumn;
                    var col = (Syncfusion.UI.Xaml.Grid.GridColumn)cb.Tag;
                    cb.IsChecked = !col.IsHidden;
                    cb.Checked += CheckBox_ShowColumn;
                    cb.Unchecked += CheckBox_HideColumn;
                }

                SaveColumnState();
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Background = (System.Windows.Media.Brush)FindResource("AccentColor"),
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor")
            };
            closeButton.Click += (s, args) => popup.Close();

            buttonPanel.Children.Add(defaultButton);
            buttonPanel.Children.Add(closeButton);

            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            popup.Content = grid;
            popup.ShowDialog();
        }
        // Column visibility checkbox handlers (named so they can be detached/reattached)
        private void CheckBox_ShowColumn(object sender, RoutedEventArgs e)
        {
            var col = (Syncfusion.UI.Xaml.Grid.GridColumn)((CheckBox)sender).Tag;
            col.IsHidden = false;
            SaveColumnState();
        }

        private void CheckBox_HideColumn(object sender, RoutedEventArgs e)
        {
            var col = (Syncfusion.UI.Xaml.Grid.GridColumn)((CheckBox)sender).Tag;
            col.IsHidden = true;
            SaveColumnState();
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
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
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
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
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
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
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
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
        }

        private async void BtnFilterToday_Click(object sender, RoutedEventArgs e)
        {
            bool filterActive = _viewModel.TodayFilterActive;

            if (!filterActive)
            {
                // Load 3WLA dates if not already loaded
                await _viewModel.LoadThreeWeekDatesAsync();

                // Enable the Today filter
                _viewModel.TodayFilterActive = true;

                // Update button visuals - active
                btnFilterToday.BorderBrush = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Disable the Today filter
                _viewModel.TodayFilterActive = false;

                // Update button visuals - inactive
                btnFilterToday.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            }

            // Apply combined filter (includes global search, Today filter, and user filter)
            ApplyCombinedViewFilter();
        }

        private async void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearAllFiltersAsync();

            // Clear global search
            _globalSearchText = string.Empty;
            txtGlobalSearch.Text = string.Empty;

            // Clear custom View filter
            sfActivities.View.Filter = null;
            _scanResultsFilterActive = false;
            _metadataErrorsFilterActive = false;

            // Clear all column filters using Syncfusion's built-in method
            sfActivities.ClearFilters();

            // Refresh to apply cleared filters
            sfActivities.View.RefreshFilter();

            // Reset all filter button visuals
            btnFilterComplete.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            btnFilterInProgress.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            btnFilterNotStarted.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            btnFilterLocalDirty.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            btnFilterMyRecords.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];
            btnFilterToday.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            // Reset user filter button
            _activeUserFilter = null;
            txtUserFilterName.Text = "USER";
            btnUserFilters.BorderBrush = (Brush)Application.Current.Resources["ControlBorder"];

            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
        }

        // Highlights Clear Filters border green when any filter is active
        private void UpdateClearFiltersBorder()
        {
            bool hasFilter = false;

            // Check Syncfusion column filters (sidebar buttons + column header filters)
            if (sfActivities?.Columns != null)
            {
                foreach (var col in sfActivities.Columns)
                {
                    if (col.FilterPredicates.Count > 0)
                    {
                        hasFilter = true;
                        break;
                    }
                }
            }

            // Check global search
            if (!hasFilter && !string.IsNullOrEmpty(_globalSearchText))
                hasFilter = true;

            // Check Today filter
            if (!hasFilter && _viewModel?.TodayFilterActive == true)
                hasFilter = true;

            // Check user-defined filter
            if (!hasFilter && _activeUserFilter != null)
                hasFilter = true;

            // Check scan results filter
            if (!hasFilter && _scanResultsFilterActive)
                hasFilter = true;

            // Check metadata errors filter
            if (!hasFilter && _metadataErrorsFilterActive)
                hasFilter = true;

            btnClearFilters.BorderBrush = hasFilter
                ? (Brush)Application.Current.Resources["ActiveFilterBorderColor"]
                : (Brush)Application.Current.Resources["ControlBorder"];
        }

        // Global Search handlers
        private void TxtGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _globalSearchText = txtGlobalSearch.Text.Trim();
            btnClearSearch.Visibility = string.IsNullOrEmpty(_globalSearchText) ? Visibility.Collapsed : Visibility.Visible;
            ApplyCombinedViewFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtGlobalSearch.Text = string.Empty;
            // TextChanged handler will clear the filter
        }

        // Checks if an activity matches the global search text across commonly searched fields
        private bool PassesGlobalSearch(Activity activity)
        {
            if (string.IsNullOrEmpty(_globalSearchText))
                return true;

            // Search across commonly used fields
            return activity.ActivityID.ToString().Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase)
                || (activity.Description?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.WorkPackage?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.PhaseCode?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.CompType?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.Area?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.RespParty?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.AssignedTo?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.Notes?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.TagNO?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.UniqueID?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.DwgNO?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.LineNumber?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (activity.SchedActNO?.Contains(_globalSearchText, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Applies combined View.Filter that includes global search and any active custom filters
        private void ApplyCombinedViewFilter()
        {
            // Build combined filter predicate
            sfActivities.View.Filter = record =>
            {
                if (record is not Activity activity)
                    return true;

                // Check global search first
                if (!PassesGlobalSearch(activity))
                    return false;

                // Check Today filter
                if (_viewModel.TodayFilterActive)
                {
                    if (!_viewModel.PassesTodayFilter(activity))
                        return false;
                }

                // Check active user-defined filter
                if (_activeUserFilter != null)
                {
                    bool? result = null;
                    foreach (var condition in _activeUserFilter.Conditions)
                    {
                        bool conditionResult = EvaluateCondition(activity, condition);
                        if (result == null)
                            result = conditionResult;
                        else if (condition.LogicOperator == "OR")
                            result = result.Value || conditionResult;
                        else
                            result = result.Value && conditionResult;
                    }
                    if (result == false)
                        return false;
                }

                return true;
            };

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            DebouncedUpdateSummary();
            UpdateClearFiltersBorder();
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
            DebouncedUpdateSummary();
            await CalculateMetadataErrorCount();

        }

        // Open the AI progress scan dialog
        private async void BtnScanProgress_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ProgressScanDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.AppliedUniqueIds.Count > 0)
            {
                // Refresh the grid after applying scan results
                await _viewModel.RefreshAsync();

                // Filter to show only the affected records
                _scanResultsFilterActive = true;
                var affectedIds = new HashSet<string>(dialog.AppliedUniqueIds);
                sfActivities.View.Filter = record =>
                {
                    if (record is Activity activity)
                        return affectedIds.Contains(activity.UniqueID);
                    return false;
                };
                sfActivities.View.RefreshFilter();

                UpdateRecordCount();
                DebouncedUpdateSummary();
                UpdateClearFiltersBorder();
            }
        }

        // Public method to refresh the grid data from the database
        /// Used by MainWindow after bulk operations like resetting LocalDirty
        
        public async Task RefreshData()
        {
            await _viewModel.RefreshAsync();
            UpdateRecordCount();
            DebouncedUpdateSummary();
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

        // Auto-focus the PercentEntry edit TextBox on single click
        private void PercentEntryEditBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

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

                // Round double values to 3 decimal places
                if (currentValue is double doubleValue && property.PropertyType == typeof(double))
                {
                    double roundedValue = NumericHelper.RoundToPlaces(doubleValue);
                    if (Math.Abs(doubleValue - roundedValue) > 0.00001)
                    {
                        property.SetValue(editedActivity, roundedValue);
                        currentValue = roundedValue;
                    }
                }

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
                    double newPercent = (currentValue as double?) ?? 0;

                    // 0% → clear both dates
                    if (newPercent == 0)
                    {
                        editedActivity.ActStart = null;
                        editedActivity.ActFin = null;
                    }
                    // <100% → clear ActFin (user must re-enter when reaching 100)
                    else if (newPercent < 100)
                    {
                        editedActivity.ActFin = null;
                    }
                    // Note: No auto-set of dates - user must enter ActStart/ActFin manually
                }
                else if (columnName == "ActStart")
                {
                    // Can't set start if percent is 0
                    if (editedActivity.ActStart != null && editedActivity.PercentEntry == 0)
                    {
                        MessageBox.Show("Cannot set Start date when % Complete is 0.\n\nSet % Complete to greater than 0 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActStart = null;
                        sfActivities.View?.Refresh();
                        return;
                    }

                    // Can't set start in the future
                    if (editedActivity.ActStart != null && editedActivity.ActStart.Value.Date > DateTime.Today)
                    {
                        MessageBox.Show("Start date cannot be in the future.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActStart = DateTime.Today;
                        sfActivities.View?.Refresh();
                    }
                    // Can't set start after finish
                    if (editedActivity.ActStart != null && editedActivity.ActFin != null &&
                        editedActivity.ActStart.Value.Date > editedActivity.ActFin.Value.Date)
                    {
                        MessageBox.Show("Start date cannot be after Finish date.\n\nFinish date has been updated to match Start date.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActFin = editedActivity.ActStart;
                        sfActivities.View?.Refresh();
                    }
                    // Can't clear start if percent > 0
                    if (editedActivity.ActStart == null && editedActivity.PercentEntry > 0)
                    {
                        MessageBox.Show("Cannot clear Start date when % Complete is greater than 0.\n\nSet % Complete to 0 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActStart = DateTime.Today;
                        sfActivities.View?.Refresh();
                        return;
                    }
                }
                else if (columnName == "ActFin")
                {
                    // Can't set finish if percent < 100
                    if (editedActivity.ActFin != null && editedActivity.PercentEntry < 100)
                    {
                        MessageBox.Show("Cannot set Finish date when % Complete is less than 100.\n\nSet % Complete to 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActFin = null;
                        sfActivities.View?.Refresh();
                        return;
                    }

                    // Can't set finish in the future
                    if (editedActivity.ActFin != null && editedActivity.ActFin.Value.Date > DateTime.Today)
                    {
                        MessageBox.Show("Finish date cannot be in the future.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActFin = DateTime.Today;
                        sfActivities.View?.Refresh();
                    }

                    // Can't set finish before start
                    if (editedActivity.ActFin != null && editedActivity.ActStart != null &&
                        editedActivity.ActFin.Value.Date < editedActivity.ActStart.Value.Date)
                    {
                        MessageBox.Show("Finish date cannot be before Start date.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActFin = editedActivity.ActStart;
                        sfActivities.View?.Refresh();
                    }

                    // Can't clear finish if percent is 100
                    if (editedActivity.ActFin == null && editedActivity.PercentEntry >= 100)
                    {
                        MessageBox.Show("Cannot clear Finish date when % Complete is 100.\n\nSet % Complete to less than 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedActivity.ActFin = DateTime.Today;
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
                    await CalculateMetadataErrorCount();
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
                AppLogger.Error(ex, "sfActivities_CurrentCellEndEdit", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Clear cell value when Delete/Backspace pressed outside edit mode
        private async Task ClearCurrentCellAsync(Activity activity, string columnName)
        {
            try
            {
                var property = GetCachedProperty(columnName);
                if (property == null || !property.CanWrite)
                    return;

                // Determine appropriate empty value based on type
                object? emptyValue = null;
                var propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (propType == typeof(string))
                    emptyValue = null;
                else if (propType == typeof(double))
                    emptyValue = 0.0;
                else if (propType == typeof(int))
                    emptyValue = 0;
                else if (propType == typeof(DateTime))
                    emptyValue = null;
                else
                    return; // Don't clear unknown types

                // Set the value
                property.SetValue(activity, emptyValue);

                // Mark dirty and save
                activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                activity.UpdatedUtcDate = DateTime.UtcNow;
                activity.LocalDirty = 1;

                bool success = await ActivityRepository.UpdateActivityInDatabase(activity);

                if (success)
                {
                    sfActivities.View?.Refresh();
                    UpdateSummaryPanel();
                    await CalculateMetadataErrorCount();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.ClearCurrentCellAsync", App.CurrentUser?.Username ?? "Unknown");
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

            if (dialog.ShowDialog() == true)
            {
                UpdateSummaryPanel();
                _ = CalculateMetadataErrorCount();
            }
        }

        // Copy column values with header - copies header + all visible row values for the column
        private void MenuCopyColumnWithHeader_Click(object sender, RoutedEventArgs e)
        {
            CopyColumnValues(sender, includeHeader: true);
        }

        // Copy column values without header - copies only visible row values for the column
        private void MenuCopyColumnWithoutHeader_Click(object sender, RoutedEventArgs e)
        {
            CopyColumnValues(sender, includeHeader: false);
        }

        // Helper method to copy column values
        private void CopyColumnValues(object sender, bool includeHeader)
        {
            try
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

                // Get visible records from the grid's filtered view
                var visibleRecords = sfActivities.View?.Records
                    .Select(r => r.Data as Activity)
                    .Where(a => a != null)
                    .ToList();

                if (visibleRecords == null || visibleRecords.Count == 0)
                {
                    MessageBox.Show("No visible records to copy.", "No Data", MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                var sb = new StringBuilder();

                // Add header if requested
                if (includeHeader)
                {
                    sb.AppendLine(columnHeader);
                }

                // Get the property for reflection
                var property = typeof(Activity).GetProperty(columnName);
                if (property == null)
                {
                    MessageBox.Show($"Column '{columnName}' not found on Activity.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Add each visible row's value
                foreach (var activity in visibleRecords)
                {
                    var value = property.GetValue(activity);
                    if (value == null)
                    {
                        sb.AppendLine(string.Empty);
                    }
                    else if (value is DateTime dt)
                    {
                        sb.AppendLine(dt.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        sb.AppendLine(value.ToString());
                    }
                }

                // Copy to clipboard (remove trailing newline for cleaner paste)
                var result = sb.ToString().TrimEnd('\r', '\n');
                Clipboard.SetText(result);

                var headerText = includeHeader ? "with header" : "without header";
                AppLogger.Info($"Copied column '{columnHeader}' ({visibleRecords.Count} values, {headerText})",
                    "CopyColumn", App.CurrentUser?.Username ?? "Unknown");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "CopyColumnValues", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Copy failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Freeze columns from the left edge up to and including the clicked column
        private void MenuFreezeColumnsToHere_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var menuItem = sender as MenuItem;
                if (menuItem == null) return;

                var contextMenuInfo = menuItem.DataContext as Syncfusion.UI.Xaml.Grid.GridColumnContextMenuInfo;
                if (contextMenuInfo == null) return;

                var column = contextMenuInfo.Column;

                // Get the visual index of the column (position as displayed)
                int visualIndex = sfActivities.Columns.IndexOf(column);
                if (visualIndex < 0) return;

                // Freeze from left edge to this column (inclusive)
                int freezeCount = visualIndex + 1;
                sfActivities.FrozenColumnCount = freezeCount;

                // Persist the setting
                SettingsManager.SetUserSetting(FrozenColumnsKey, freezeCount.ToString(), "int");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.MenuFreezeColumnsToHere_Click");
            }
        }

        // Remove all frozen columns
        private void MenuUnfreezeAllColumns_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                sfActivities.FrozenColumnCount = 0;
                SettingsManager.SetUserSetting(FrozenColumnsKey, "0", "int");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressView.MenuUnfreezeAllColumns_Click");
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        // Hook into parent window to handle native horizontal scroll wheel (tilt wheel)
        private void AttachHorizontalScrollHook()
        {
            var window = Window.GetWindow(this);
            if (window == null) return;
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private void DetachHorizontalScrollHook()
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }

        // Handle WM_MOUSEHWHEEL for the activities grid
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                double scrollAmount = delta > 0 ? 60 : -60;

                if (GetCursorPos(out POINT screenPt))
                {
                    var gridPoint = sfActivities.PointFromScreen(new Point(screenPt.X, screenPt.Y));
                    var gridBounds = new Rect(0, 0, sfActivities.ActualWidth, sfActivities.ActualHeight);

                    if (gridBounds.Contains(gridPoint))
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(sfActivities);
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                            handled = true;
                        }
                    }
                }
            }
            return IntPtr.Zero;
        }

        // Ctrl+ScrollWheel for horizontal scrolling
        private void SfActivities_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(sfActivities);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            }
        }

        // Helper to find a child of a specific type in the visual tree
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // Selects all rows currently visible in the filtered view
        private void SelectAllFilteredRows()
        {
            var records = sfActivities.View?.Records;
            if (records == null || records.Count == 0) return;

            sfActivities.SelectAll();
        }

        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SelectAllFilteredRows();
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
            // For column headers, show menu but hide Find & Replace for read-only columns
            else if (e.ContextMenuType == Syncfusion.UI.Xaml.Grid.ContextMenuType.Header)
            {
                var columnIndex = sfActivities.ResolveToGridVisibleColumnIndex(e.RowColumnIndex.ColumnIndex);
                if (columnIndex >= 0 && columnIndex < sfActivities.Columns.Count)
                {
                    var column = sfActivities.Columns[columnIndex];
                    bool isReadOnly = VANTAGE.Utilities.ColumnPermissions.IsReadOnly(column.MappingName);

                    // Find the Find & Replace menu item and separator, hide them for read-only columns
                    if (e.ContextMenu?.Items != null)
                    {
                        foreach (var item in e.ContextMenu.Items)
                        {
                            if (item is MenuItem menuItem && menuItem.Header?.ToString()?.Contains("Find") == true)
                            {
                                menuItem.Visibility = isReadOnly ? Visibility.Collapsed : Visibility.Visible;
                            }
                            else if (item is Separator separator)
                            {
                                separator.Visibility = isReadOnly ? Visibility.Collapsed : Visibility.Visible;
                            }
                        }
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
                MessageBox.Show("Please select one or more records to assign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
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
                string.IsNullOrWhiteSpace(a.RespParty) ||
                a.HasMissingActStart ||
                a.HasMissingActFin
            ).ToList();

            if (recordsWithErrors.Any())
            {
                MessageBox.Show(
                    $"Cannot reassign. {recordsWithErrors.Count} selected record(s) have missing required metadata.\n\n" +
                    "Click 'Metadata Errors' button to view and fix these records.\n\n" +
                    "Required fields: WorkPackage, PhaseCode, CompType, PhaseCategory, ProjectID, SchedActNO, Description, ROCStep, RespParty\n" +
                    "Conditional: ActStart (when % > 0), ActFin (when % = 100)",
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
                var validatingDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Validating selected records...");
                validatingDialog.Show();

                var splitActNOs = await Task.Run(() =>
                {
                    var splits = new List<(string ProjectID, string SchedActNO, int Selected, int Total)>();

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
                            splits.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value, totalCount));
                        }
                    }

                    connection.Close();
                    return splits;
                });

                validatingDialog.Close();

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
                    "Sync Required", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            // Check connection to Azure and load users
            var connectDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking Azure connection...");
            connectDialog.Show();

            bool isConnected = false;
            while (!isConnected)
            {
                var (connected, connError) = await Task.Run(() =>
                {
                    bool result = AzureDbManager.CheckConnection(out string err);
                    return (result, err);
                });

                if (connected)
                {
                    isConnected = true;
                }
                else
                {
                    connectDialog.Close();

                    var retryResult = MessageBox.Show(
                        $"Cannot connect to Azure database.\n\n{connError}\n\nWould you like to retry?",
                        "Connection Failed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (retryResult != MessageBoxResult.Yes)
                        return;

                    connectDialog = new Dialogs.BusyDialog(Window.GetWindow(this), "Checking Azure connection...");
                    connectDialog.Show();
                }
            }

            // Get list of all users for dropdown
            connectDialog.UpdateStatus("Loading users...");
            var allUsers = await Task.Run(() => GetAllUsers().Select(u => u.Username).ToList());
            connectDialog.Close();
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
                Background = ThemeHelper.BackgroundColor
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
                Foreground = ThemeHelper.ForegroundColor
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
            FROM VMS_Activities a
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
            FROM VMS_Activities a
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

                    busyDialog.Close();

                    string successMessage = $"Successfully reassigned {updatedCount} record(s) to {selectedUser}.";
                    successMessage += emailSent
                        ? "\n\nEmail notification sent."
                        : "\n\nEmail notification could not be sent.";

                    MessageBox.Show(
                        successMessage,
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
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