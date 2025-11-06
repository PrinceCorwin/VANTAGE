using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private const int ColumnUniqueValueDisplayLimit = 1000; // configurable
        private Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn> _columnMap = new Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn>();
        private ProgressViewModel _viewModel;
        // one key per grid/view
        private const string GridPrefsKey = "ProgressGrid.PreferencesJson";

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

        private void BtnAssign_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
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
                for (int target =0; target < orderedPrefs.Count; target++)
                {
                    var p = orderedPrefs[target];
                    if (!byName.TryGetValue(p.Name, out var col)) continue;
                    int cur = sfActivities.Columns.IndexOf(col);
                    if (cur != target && cur >=0)
                    {
                        sfActivities.Columns.RemoveAt(cur);
                        sfActivities.Columns.Insert(target, col);
                    }
                }

                //3) Width last (guard against tiny widths)
                const double MinWidth =40.0;
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
                int successCount = 0;
                foreach (var activity in selectedActivities)
                {
                    activity.PercentEntry = percent;
                    activity.LastModifiedBy = App.CurrentUser?.Username ?? "Unknown";

                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success) successCount++;
                }

                MessageBox.Show($"Set {successCount} record(s) to {percent}%.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                sfActivities.View.Refresh();
                await _viewModel.UpdateTotalsAsync();
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
        private void UpdateSummaryPanel()
        {
            txtBudgetedMHs.Text = _viewModel.BudgetedMHs.ToString("N2");
            txtEarnedMHs.Text = _viewModel.EarnedMHs.ToString("N2");
            txtPercentComplete.Text = $"{_viewModel.PercentComplete:N2}%";
        }
        private Popup _activeFilterPopup;
        private string _activeFilterColumn;

        

        private void FilterControl_SortRequested(object sender, Controls.ColumnFilterPopup.SortEventArgs e)
        {
            // Remove any existing sort
            var view = _viewModel.ActivitiesView;
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription(e.ColumnName, e.Direction));
                view.Refresh();
            }
            _activeFilterPopup.IsOpen = false;
        }

        private async void FilterControl_FilterApplied(object sender, Controls.FilterEventArgs e)
        {
            // Handle list filter type (pipe-delimited)
            if (e.FilterType == "List")
            {
                var selected = (e.FilterValue ?? "").Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                // Build SQL condition that supports blank/null sentinel '__BLANK__'
                string dbCol = ColumnMapper.GetDbColumnName(_activeFilterColumn);
                var nonBlankValues = selected.Where(s => s != "__BLANK__").ToList();
                bool includeBlanks = selected.Any(s => s == "__BLANK__");
                var parts = new List<string>();

                // Special handling for Status (use CASE expression)
                if (_activeFilterColumn == "Status")
                {
                    string statusCase = "CASE WHEN Val_Perc_Complete IS NULL OR Val_Perc_Complete = 0 THEN 'Not Started' WHEN Val_Perc_Complete >= 1.0 THEN 'Complete' ELSE 'In Progress' END";
                    if (nonBlankValues.Any())
                    {
                        var escaped = nonBlankValues.Select(s => s.Replace("'", "''"));
                        var inList = string.Join(",", escaped.Select(s => $"'{s}'"));
                        parts.Add($"{statusCase} IN ({inList})");
                    }
                    if (includeBlanks)
                    {
                        parts.Add($"({statusCase} IS NULL OR {statusCase} = '')");
                    }
                }
                // Special handling for percent/ratio columns: use numeric comparison (no quotes)
                else if (_activeFilterColumn == "PercentEntry" || _activeFilterColumn == "PercentEntry_Display" || _activeFilterColumn == "PercentCompleteCalc" || _activeFilterColumn == "PercentCompleteCalc_Display" || _activeFilterColumn == "EarnedQtyCalc" || _activeFilterColumn == "EarnedQtyCalc_Display")
                {
                    if (nonBlankValues.Any())
                    {
                        var inList = string.Join(",", nonBlankValues.Select(s => double.TryParse(s, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-99999"));
                        parts.Add($"{dbCol} IN ({inList})");
                    }
                    if (includeBlanks)
                    {
                        parts.Add($"({dbCol} IS NULL OR {dbCol} = '')");
                    }
                }
                // Special handling for AssignedTo: treat 'Unassigned' as blank string or 'Unassigned'
                else if (_activeFilterColumn == "AssignedTo")
                {
                    var assignedParts = new List<string>();
                    foreach (var val in nonBlankValues)
                    {
                        if (val.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
                        {
                            assignedParts.Add($"({dbCol} IS NULL OR {dbCol} = '' OR {dbCol} = 'Unassigned')");
                        }
                        else
                        {
                            assignedParts.Add($"{dbCol} = '{val.Replace("'", "''")}'");
                        }
                    }
                    if (assignedParts.Any())
                        parts.Add(string.Join(" OR ", assignedParts));
                }
                else
                {
                    if (nonBlankValues.Any())
                    {
                        var escaped = nonBlankValues.Select(s => s.Replace("'", "''"));
                        var inList = string.Join(",", escaped.Select(s => $"'{s}'"));
                        parts.Add($"{dbCol} IN ({inList})");
                    }
                    if (includeBlanks)
                    {
                        parts.Add($"({dbCol} IS NULL OR {dbCol} = '')");
                    }
                }

                var cond = parts.Count == 1 ? parts[0] : "(" + string.Join(" OR ", parts) + ")";

                // Apply to ViewModel by using ApplyFilter with a synthetic FilterType 'IN' and FilterValue cond
                await _viewModel.ApplyFilter(_activeFilterColumn, "IN", cond);
            }
            else
            {
                // Use existing mechanism
                await _viewModel.ApplyFilter(_activeFilterColumn, e.FilterType, e.FilterValue);
            }

            _activeFilterPopup.IsOpen = false;
        }

        private async void FilterControl_FilterCleared(object sender, EventArgs e)
        {
            // Clear filter through ViewModel and WAIT
            await _viewModel.ClearFilter(_activeFilterColumn);

            _activeFilterPopup.IsOpen = false;
        }
        
        /// Auto-save when user finishes editing a cell
        

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {


            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();

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
        
        /// Prevent editing of records not assigned to current user
        
        private void sfActivities_CurrentCellBeginEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellBeginEditEventArgs e)
        {
            // Get the activity from the current row
            var activity = sfActivities.SelectedItem as Activity;
            if (activity != null && !activity.IsEditable)
            {
                // Cancel the edit
                e.Cancel = true;
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
        private async void MenuUnassign_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = sfActivities.SelectedItems.Cast<Activity>().ToList();
            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to unassign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Filter: Only allow unassigning records that user has permission to modify
            var allowedActivities = selectedActivities.Where(a =>
                App.CurrentUser.IsAdmin || // Admins can unassign any record
                a.AssignedToUsername == App.CurrentUser.Username || // User's own records
                a.AssignedToUsername == "Unassigned" // Already unassigned (no-op but allowed)
            ).ToList();

            if (!allowedActivities.Any())
            {
                MessageBox.Show("You can only unassign your own records or unassigned records.\n\nAdmins can unassign any record.",
                    "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (allowedActivities.Count < selectedActivities.Count)
            {
                var result = MessageBox.Show(
                    $"You can only unassign {allowedActivities.Count} of {selectedActivities.Count} selected records.\n\n" +
                    $"Records assigned to other users cannot be unassigned.\n\nContinue with allowed records?",
                    "Partial Unassignment",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                int successCount = 0;
                foreach (var activity in allowedActivities)
                {
                    activity.AssignedTo = "Unassigned";
                    activity.LastModifiedBy = App.CurrentUser.Username;

                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success)
                    {
                        successCount++;

                    }
                }

                MessageBox.Show($"Unassigned {successCount} record(s).", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                sfActivities.View.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unassigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void BtnFilterComplete_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterComplete.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Complete" filter (PercentEntry = 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals
                btnFilterComplete.Content = "Complete ✓";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["AccentColor"];

                // Clear other filter buttons
                ResetFilterButtonVisuals(btnFilterComplete);
            }
            else
            {
                // Clear filter
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                btnFilterComplete.Content = "Complete";
                btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            UpdateRecordCount();
        }

        private void BtnFilterNotComplete_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterNotComplete.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Not Complete" filter (PercentEntry < 100)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.LessThan,
                    FilterValue = 100.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals
                btnFilterNotComplete.Content = "Not Complete ✓";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["AccentColor"];

                // Clear other filter buttons
                ResetFilterButtonVisuals(btnFilterNotComplete);
            }
            else
            {
                // Clear filter
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                btnFilterNotComplete.Content = "Not Complete";
                btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            UpdateRecordCount();
        }

        private void BtnFilterNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterNotStarted.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "Not Started" filter (PercentEntry = 0)
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                sfActivities.Columns["PercentEntry"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = 0.0,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals
                btnFilterNotStarted.Content = "Not Started ✓";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["AccentColor"];

                // Clear other filter buttons
                ResetFilterButtonVisuals(btnFilterNotStarted);
            }
            else
            {
                // Clear filter
                sfActivities.Columns["PercentEntry"].FilterPredicates.Clear();
                btnFilterNotStarted.Content = "Not Started";
                btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            UpdateRecordCount();
        }

        private void BtnFilterUser2_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterUser3_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterMyRecords.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "My Records" filter (AssignedTo = current username)
                sfActivities.Columns["AssignedTo"].FilterPredicates.Clear();
                sfActivities.Columns["AssignedTo"].FilterPredicates.Add(new Syncfusion.Data.FilterPredicate()
                {
                    FilterType = Syncfusion.Data.FilterType.Equals,
                    FilterValue = App.CurrentUser.Username,
                    PredicateType = Syncfusion.Data.PredicateType.And
                });

                // Update button visuals
                btnFilterMyRecords.Content = "My Records ✓";
                btnFilterMyRecords.Background = (Brush)Application.Current.Resources["AccentColor"];

                // Clear other filter buttons
                ResetFilterButtonVisuals(btnFilterMyRecords);
            }
            else
            {
                // Clear filter
                sfActivities.Columns["AssignedTo"].FilterPredicates.Clear();
                btnFilterMyRecords.Content = "My Records";
                btnFilterMyRecords.Background = (Brush)Application.Current.Resources["ControlBackground"];
            }

            sfActivities.View.RefreshFilter();
            UpdateRecordCount();
        }
        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // Clear all column filters
            foreach (var column in sfActivities.Columns)
            {
                column.FilterPredicates.Clear();
            }

            // Reset all filter button visuals
            btnFilterComplete.Content = "Complete";
            btnFilterComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterNotComplete.Content = "Not Complete";
            btnFilterNotComplete.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterNotStarted.Content = "Not Started";
            btnFilterNotStarted.Background = (Brush)Application.Current.Resources["ControlBackground"];

            btnFilterMyRecords.Content = "My Records";
            btnFilterMyRecords.Background = (Brush)Application.Current.Resources["ControlBackground"];

            sfActivities.View.RefreshFilter();
            UpdateRecordCount();
        }
        // Helper method to reset other filter button visuals
        private void ResetFilterButtonVisuals(Button activeButton)
        {
            var buttons = new[] { btnFilterComplete, btnFilterNotComplete, btnFilterNotStarted, btnFilterMyRecords };

            foreach (var button in buttons)
            {
                if (button != activeButton && button.Content.ToString().Contains("✓"))
                {
                    button.Content = button.Content.ToString().Replace(" ✓", "");
                    button.Background = (Brush)Application.Current.Resources["ControlBackground"];
                }
            }
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

        // Helper method: Show user selection dialog
        private User ShowUserSelectionDialog(List<User> users)
        {
            var dialog = new Window
            {
                Title = "Assign to User",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Select user to assign records to:",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);

            var comboBox = new ComboBox
            {
                ItemsSource = users,
                DisplayMemberPath = "Username",
                Height = 30,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(comboBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30
            };
            okButton.Click += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    dialog.DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Please select a user.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(label);
            grid.Children.Add(comboBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            return dialog.ShowDialog() == true ? (User)comboBox.SelectedItem : null;
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

        }

        
        /// Auto-save when user finishes editing a cell
        
        private async void sfActivities_CurrentCellEndEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellEndEditEventArgs e)
        {
            try
            {
                // Get the edited activity from the current row
                var editedActivity = sfActivities.SelectedItem as Activity;
                if (editedActivity == null)
                    return;

                // Update LastModifiedBy with current user
                editedActivity.LastModifiedBy = App.CurrentUser?.Username ?? "Unknown";

                // Save to database
                bool success = await ActivityRepository.UpdateActivityInDatabase(editedActivity);

                if (success)
                {
                    // TODO: Add proper logging when logging system is implemented
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

        private async void MenuAssignToMe_Click(object sender, RoutedEventArgs e)
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
                App.CurrentUser.IsAdmin || // Admins can assign any record
                a.AssignedToUsername == App.CurrentUser.Username || // User's own records
                a.AssignedToUsername == "Unassigned" // Unassigned records
            ).ToList();

            if (!allowedActivities.Any())
            {
                MessageBox.Show("You can only assign your own records or unassigned records.\n\nAdmins can assign any record.",
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

            try
            {
                int successCount = 0;
                foreach (var activity in allowedActivities)
                {
                    activity.AssignedTo = App.CurrentUser.Username;
                    activity.LastModifiedBy = App.CurrentUser.Username;

                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success)
                    {
                        successCount++;
                    }
                }

                MessageBox.Show($"Assigned {successCount} record(s) to you.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                sfActivities.View.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                App.CurrentUser.IsAdmin || // Admins can assign any record
                a.AssignedToUsername == App.CurrentUser.Username || // User's own records
                a.AssignedToUsername == "Unassigned" // Unassigned records
            ).ToList();

            if (!allowedActivities.Any())
            {
                MessageBox.Show("You can only assign your own records or unassigned records.\n\nAdmins can assign any record.",
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
                    int successCount = 0;
                    foreach (var activity in allowedActivities)
                    {
                        activity.AssignedTo = selectedUser;
                        activity.LastModifiedBy = App.CurrentUser.Username;

                        bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                        if (success)
                        {
                            successCount++;
                        }
                    }

                    MessageBox.Show($"Assigned {successCount} record(s) to {selectedUser}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    sfActivities.View.Refresh();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}