using Microsoft.Data.Sqlite;
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

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private const int ColumnUniqueValueDisplayLimit = 1000; // configurable
        private Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn> _columnMap = new Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn>();
        private ProgressViewModel _viewModel;
        // one key per grid/view
        private const string GridPrefsKey = "ProgressGrid.PreferencesJson";
        private ProgressViewModel ViewModel => DataContext as ProgressViewModel;
        private object _originalCellValue;
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
                    currentUser?.Username);

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
                BtnClearFilters_Click(null, null);

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

                await _viewModel.ApplyFilter("MetadataErrors", "Custom", errorFilter);

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
        private void MenuCopyRows_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Copy Row(s) feature coming soon!",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuDuplicateRows_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Duplicate Row(s) feature coming soon!",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
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
            // Hook into Syncfusion's filter changed event
            sfActivities.FilterChanged += SfActivities_FilterChanged;
            sfActivities.CurrentCellBeginEdit += SfActivities_CurrentCellBeginEdit;

            // VM
            _viewModel = new ProgressViewModel();
            this.DataContext = _viewModel;
            sfActivities.ItemsSource = _viewModel.ActivitiesView;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            InitializeColumnVisibility();
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
            SetupColumnResizeSave();
        }
        private void SfActivities_FilterChanged(object sender, Syncfusion.UI.Xaml.Grid.GridFilterEventArgs e)
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
        private System.Windows.Threading.DispatcherTimer _resizeSaveTimer;

        private void SetupColumnResizeSave()
        {
            // Create timer that waits 500ms after last resize before saving
            _resizeSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            _resizeSaveTimer.Tick += (s, e) =>
            {
                _resizeSaveTimer.Stop();
                SaveColumnState();
            };

            // Hook into column width changes
            foreach (var column in sfActivities.Columns)
            {
                var descriptor = System.ComponentModel.DependencyPropertyDescriptor
                    .FromProperty(Syncfusion.UI.Xaml.Grid.GridColumn.WidthProperty,
                                 typeof(Syncfusion.UI.Xaml.Grid.GridColumn));

                descriptor?.AddValueChanged(column, (sender, args) =>
                {
                    // Reset timer on each resize event
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
                InitializeColumnVisibility(); // Sync sidebar checkboxes
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
            if (button == null) return;

            // Parse percent from button content
            var buttonContent = button.Content.ToString();
            if (!int.TryParse(buttonContent.TrimEnd('%'), out int percent))
            {
                MessageBox.Show("Invalid percent value.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await SetSelectedRecordsPercent(percent);
        }


        /// Context menu: Reset button to default value

        private void MenuItem_ResetPercent_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem?.Tag == null) return;

            // Get default value from menu item tag
            int defaultValue = int.Parse(menuItem.Tag.ToString());

            // Find which button's context menu this came from
            var contextMenu = ((MenuItem)sender).Parent as ContextMenu;
            var button = contextMenu?.PlacementTarget as Button;

            if (button == null) return;

            // Parse button Tag: "ButtonName|SettingKey|DefaultValue"
            var tagParts = button.Tag?.ToString().Split('|');
            if (tagParts == null || tagParts.Length != 3) return;

            string settingKey = tagParts[1];

            // Update button
            button.Content = $"{defaultValue}%";

            // Save to user settings
            SettingsManager.SetUserSetting(App.CurrentUserID, settingKey,
                defaultValue.ToString(), "int");

            MessageBox.Show($"Button reset to {defaultValue}%", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }


        /// Context menu: Set custom percent value

        private void MenuItem_CustomPercent_Click(object sender, RoutedEventArgs e)
        {
            // Find which button's context menu this came from
            var contextMenu = ((MenuItem)sender).Parent as ContextMenu;
            var button = contextMenu?.PlacementTarget as Button;

            if (button == null) return;

            // Parse button Tag: "ButtonName|SettingKey|DefaultValue"
            var tagParts = button.Tag?.ToString().Split('|');
            if (tagParts == null || tagParts.Length != 3)
            {
                MessageBox.Show("Button configuration error.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string buttonName = tagParts[0];
            string settingKey = tagParts[1];

            // Get current value
            string currentValue = button.Content.ToString().TrimEnd('%');
            int.TryParse(currentValue, out int currentPercent);

            // Show custom dialog
            var dialog = new CustomPercentDialog(currentPercent);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                int newPercent = dialog.PercentValue;

                // Update button
                button.Content = $"{newPercent}%";

                // Save to user settings
                SettingsManager.SetUserSetting(App.CurrentUserID, settingKey,
                    newPercent.ToString(), "int");

                MessageBox.Show($"{buttonName} updated to {newPercent}%", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
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
                var filteredIds = new HashSet<int>();
                foreach (var record in sfActivities.View.Records)
                {
                    // Extract the underlying Activity from Syncfusion's record wrapper
                    var dataProperty = record.GetType().GetProperty("Data");
                    if (dataProperty != null)
                    {
                        var data = dataProperty.GetValue(record);
                        if (data is Activity activity)
                        {
                            filteredIds.Add(activity.ActivityID);
                        }
                    }
                }

                recordsToSum = _viewModel.Activities.Where(a => filteredIds.Contains(a.ActivityID)).ToList();
                System.Diagnostics.Debug.WriteLine($"UpdateSummaryPanel: Calculating from {recordsToSum.Count} filtered records");
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
        private void InitializeColumnVisibility()
        {
            lstColumnVisibility.Items.Clear();
            _columnMap.Clear();

            foreach (var column in sfActivities.Columns)
            {
                // Get property name from mapping name
                string columnName = GetColumnPropertyName(column);
                _columnMap[columnName] = column;

                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = !column.IsHidden, // Syncfusion uses IsHidden instead of Visibility
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White,
                    Tag = column
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }
        }

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
        // === FILTER EVENT HANDLERS ===

        private void BtnFilterComplete_Click(object sender, RoutedEventArgs e)
        {
            // Clear other percent filters first
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            // Toggle this filter
            bool filterActive = btnFilterComplete.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Complete" filter (PercentEntry = 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update all button visuals - only this one active
                btnFilterComplete.Content = "Complete ✓";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["AccentColor"];
                btnFilterNotComplete.Content = "Not Complete";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
                btnFilterNotStarted.Content = "Not Started";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }
            else
            {
                // Clear this filter
                btnFilterComplete.Content = "Complete";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
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
        private void BtnFilterNotComplete_Click(object sender, RoutedEventArgs e)
        {
            // Clear other percent filters first
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            // Toggle this filter
            bool filterActive = btnFilterNotComplete.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Not Complete" filter (PercentEntry < 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.LessThan,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update all button visuals - only this one active
                btnFilterComplete.Content = "Complete";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
                btnFilterNotComplete.Content = "Not Complete ✓";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["AccentColor"];
                btnFilterNotStarted.Content = "Not Started";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }
            else
            {
                // Clear this filter
                btnFilterNotComplete.Content = "Not Complete";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnFilterNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // Clear other percent filters first
            sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();

            // Toggle this filter
            bool filterActive = btnFilterNotStarted.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Not Started" filter (PercentEntry = 0)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 0.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update all button visuals - only this one active
                btnFilterComplete.Content = "Complete";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
                btnFilterNotComplete.Content = "Not Complete";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
                btnFilterNotStarted.Content = "Not Started ✓";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Clear this filter
                btnFilterNotStarted.Content = "Not Started";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterMyRecords.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "My Records" filter (AssignedTo = current username)
                sfActivities.Columns["AssignedTo"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = App.CurrentUser.Username,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals
                btnFilterMyRecords.Content = "My Records ✓";
                btnFilterMyRecords.Background = (Brush)Application.Current.Resources["AccentColor"];
            }
            else
            {
                // Clear this filter only
                sfActivities.Columns["AssignedTo"].FilterPredicates.Clear();
                btnFilterMyRecords.Content = "My Records";
                btnFilterMyRecords.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            _viewModel.FilteredCount = sfActivities.View.Records.Count;
            UpdateRecordCount();
            UpdateSummaryPanel();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // Clear all column filters (including column header filters)
            foreach (var column in sfActivities.Columns)
            {
                column.FilterPredicates.Clear();
            }

            // Reset all filter button visuals
            btnFilterComplete.Content = "Complete";
            btnFilterComplete.Content = "Complete";
            btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterNotComplete.Content = "Not Complete";
            btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterNotStarted.Content = "Not Started";
            btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterMyRecords.Content = "My Records";
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
                // TODO: Add proper logging when logging system is implemented
            }

            return users;
        }

        private void LstColumnVisibility_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Not needed - using CheckBox events instead
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            //_viewModel.SearchText = txtSearch.Text;
            //UpdateRecordCount();
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

        private void SfActivities_CurrentCellBeginEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellBeginEditEventArgs e)
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

        private void sfActivities_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
        {
            // TODO: Implement selection change logic if needed
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