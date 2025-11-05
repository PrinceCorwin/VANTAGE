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

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private const int ColumnUniqueValueDisplayLimit = 1000; // configurable
        private Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn> _columnMap = new Dictionary<string, Syncfusion.UI.Xaml.Grid.GridColumn>();
        private ProgressViewModel _viewModel;

        public ProgressView()
        {
            InitializeComponent();
            _viewModel = new ProgressViewModel();
            this.DataContext = _viewModel;
            sfActivities.ItemsSource = _viewModel.ActivitiesView;

            // Subscribe to ViewModel property changes - ADD THIS LINE
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            InitializeColumnVisibility();
            // InitializeColumnTooltips();
            UpdateRecordCount();
            // Load data AFTER the view is loaded
            this.Loaded += OnViewLoaded;
            LoadCustomPercentButtons(); // Load custom button values
            LoadColumnState();
            this.Unloaded += (s, e) => SaveColumnState();  // Save when view closes
            // Save column state when user reorders columns

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
                var columnData = new List<string>();

                foreach (var column in sfActivities.Columns)
                {
                    string mappingName = column.MappingName;
                    double width = column.Width;
                    bool isHidden = column.IsHidden;

                    columnData.Add($"{mappingName}|{width}|{isHidden}");
                }

                string columnState = string.Join(";", columnData);

                SettingsManager.SetUserSetting(
                    App.CurrentUserID,
                    "GridColumnState",
                    columnState,
                    "string"
                );

                System.Diagnostics.Debug.WriteLine($"Column state saved: {columnData.Count} columns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving column state: {ex.Message}");
            }
        }

        private void LoadColumnState()
        {
            try
            {
                string savedState = SettingsManager.GetUserSetting(App.CurrentUserID, "GridColumnState");

                if (string.IsNullOrEmpty(savedState))
                {
                    System.Diagnostics.Debug.WriteLine("No saved column state found");
                    return;
                }

                var columnSettings = savedState.Split(';');
                var savedOrder = new List<(string MappingName, double Width, bool IsHidden)>();

                // Parse all saved column settings
                foreach (var setting in columnSettings)
                {
                    if (string.IsNullOrEmpty(setting)) continue;

                    var parts = setting.Split('|');
                    if (parts.Length != 3) continue;

                    string mappingName = parts[0];
                    double width = double.Parse(parts[1]);
                    bool isHidden = bool.Parse(parts[2]);

                    savedOrder.Add((mappingName, width, isHidden));
                }

                // Reorder columns by removing and re-inserting at correct position
                for (int i = 0; i < savedOrder.Count; i++)
                {
                    var savedColumn = savedOrder[i];

                    // Find the column in the current collection
                    var column = sfActivities.Columns.FirstOrDefault(c => c.MappingName == savedColumn.MappingName);

                    if (column != null)
                    {
                        // Get current index
                        int currentIndex = sfActivities.Columns.IndexOf(column);

                        // If it's not in the right position, move it
                        if (currentIndex != i)
                        {
                            // Remove from current position
                            sfActivities.Columns.Remove(column);

                            // Insert at target position
                            sfActivities.Columns.Insert(i, column);
                        }

                        // Apply width and visibility
                        column.Width = savedColumn.Width;
                        column.IsHidden = savedColumn.IsHidden;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Column state loaded and reordered: {savedOrder.Count} columns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading column state: {ex.Message}");
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

            if (e.PropertyName == nameof(_viewModel.TotalRecordCount) ||
                e.PropertyName == nameof(_viewModel.TotalPages) ||
                e.PropertyName == nameof(_viewModel.CurrentPage) ||
                e.PropertyName == nameof(_viewModel.CanGoPrevious) ||
                e.PropertyName == nameof(_viewModel.CanGoNext) ||
                e.PropertyName == nameof(_viewModel.FilteredCount) ||
                e.PropertyName == nameof(_viewModel.TotalRecords))
            {
                UpdateRecordCount();
                UpdatePagingControls();
            }

            if (e.PropertyName == nameof(_viewModel.BudgetedMHs) ||
                e.PropertyName == nameof(_viewModel.EarnedMHs) ||
                e.PropertyName == nameof(_viewModel.PercentComplete))
            {
                UpdateSummaryPanel();
            }
        }
        private void UpdateSummaryPanel()
        {
            txtBudgetedMHs.Text = _viewModel.BudgetedMHs.ToString("N2");
            txtEarnedMHs.Text = _viewModel.EarnedMHs.ToString("N2");
            txtPercentComplete.Text = $"{_viewModel.PercentComplete:N2}%";
        }
        private Popup _activeFilterPopup;
        private string _activeFilterColumn;

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string columnName = null;

            if (button != null)
            {
                columnName = button.Tag as string;
            }

            // Close any existing popup
            if (_activeFilterPopup != null)
            {
                _activeFilterPopup.IsOpen = false;
            }

            // Get unique values from filtered records
            var filteredValues = _viewModel.GetUniqueValuesForColumn(columnName);

            // Create filter popup
            var filterControl = new Controls.ColumnFilterPopup();
            filterControl.FilterApplied += FilterControl_FilterApplied;
            filterControl.FilterCleared += FilterControl_FilterCleared;
            filterControl.SortRequested += FilterControl_SortRequested;

            filterControl.Initialize(columnName, ColumnUniqueValueDisplayLimit, filteredValues);

            _activeFilterPopup = new Popup
            {
                Child = filterControl,
                PlacementTarget = button,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            _activeFilterColumn = columnName;
            _activeFilterPopup.IsOpen = true;
        }

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
            UpdatePagingControls();
        }
        
        /// Initialize tooltips for column headers showing OldVantage names
        
        //private void InitializeColumnTooltips()
        //{
        //    try
        //    {
        //        // Apply tooltips to columns
        //        int tooltipsSet = 0;
        //        foreach (var column in sfActivities.Columns)
        //        {
        //            // Get the property name from the column
        //            string propertyName = GetColumnPropertyName(column);

        //            // Get the DbColumnName from property name
        //            string dbColumnName = ColumnMapper.GetDbColumnName(propertyName);

        //            // Query the ColumnMappings table for this specific column
        //            using var connection = DatabaseSetup.GetConnection();
        //            connection.Open();

        //            var command = connection.CreateCommand();
        //            command.CommandText = @"
        //        SELECT OldVantageName 
        //        FROM ColumnMappings 
        //        WHERE DbColumnName = @dbColumn";
        //            command.Parameters.AddWithValue("@dbColumn", dbColumnName);

        //            var result = command.ExecuteScalar();
        //            string tooltip;

        //            if (result == null || result == DBNull.Value)
        //            {
        //                // Not in Excel
        //                tooltip = $"{propertyName} - Not in export";
        //            }
        //            else
        //            {
        //                // Show OldVantage name
        //                tooltip = $"Excel: {result}";
        //            }

        //            // Set tooltip on the column header
        //            // Preserve existing header templates (keep filter button). If header is a simple string, wrap it in a ContentControl that uses the FilterableColumnHeader template defined in XAML.
        //            if (column.Header is string headerString)
        //            {
        //                try
        //                {
        //                    var dataTemplate = this.TryFindResource("FilterableColumnHeader") as DataTemplate;
        //                    if (dataTemplate != null)
        //                    {
        //                        var contentControl = new System.Windows.Controls.ContentControl
        //                        {
        //                            Content = headerString,
        //                            ContentTemplate = dataTemplate,
        //                            ToolTip = tooltip
        //                        };
        //                        column.Header = contentControl;
        //                    }
        //                    else
        //                    {
        //                        // Fallback to TextBlock if template not found
        //                        var textBlock = new System.Windows.Controls.TextBlock
        //                        {
        //                            Text = headerString,
        //                            ToolTip = tooltip,
        //                            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["GridHeaderForeground"]
        //                        };
        //                        column.Header = textBlock;
        //                    }
        //                }
        //                catch
        //                {
        //                    // fallback to simple text
        //                    column.Header = headerString;
        //                }

        //                tooltipsSet++;
        //            }
        //            else if (column.Header is System.Windows.Controls.ContentControl headerControl)
        //            {
        //                headerControl.ToolTip = tooltip;
        //                tooltipsSet++;
        //            }
        //        }

        //        // Initial update of header indicators
        //        UpdateHeaderFilterIndicators();
        //    }
        //    catch (Exception ex)
        //    {
        //        // TODO: Add proper logging when logging system is implemented
        //    }
        //}

        //private void UpdateHeaderFilterIndicators()
        //{
        //    try
        //    {
        //        var activeCols = new HashSet<string>(_viewModel.ActiveFilterColumns ?? Enumerable.Empty<string>());

        //        foreach (var header in FindVisualChildren<DataGridColumnHeader>(sfActivities))
        //        {
        //            var col = header.Column;
        //            if (col == null) continue;

        //            var colName = GetColumnPropertyName(col);

        //            if (activeCols.Contains(colName))
        //            {
        //                // Use theme variable so themes can override color
        //                header.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ActiveFilter"];
        //                header.BorderThickness = new Thickness(0,0,1,1);
        //            }
        //            else
        //            {
        //                header.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderColor"];
        //                header.BorderThickness = new Thickness(0,0,1,1);
        //            }
        //        }
        //    }
        //    catch { }
        //}

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
            txtFilteredCount.Text = $"{_viewModel.FilteredCount} of {_viewModel.TotalRecords} records (Total: {_viewModel.TotalRecordCount})";
        }

        private void UpdatePagingControls()
        {
            txtPageInfo.Text = _viewModel.CurrentPageDisplay;
            btnFirstPage.IsEnabled = _viewModel.CanGoPrevious;
            btnPreviousPage.IsEnabled = _viewModel.CanGoPrevious;
            btnNextPage.IsEnabled = _viewModel.CanGoNext;
            btnLastPage.IsEnabled = _viewModel.CanGoNext;
        }

        // === PAGING EVENT HANDLERS ===

        private async void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.FirstPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.PreviousPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.NextPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LastPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        // === FILTER EVENT HANDLERS ===

        private void BtnFilterComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterNotComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterUser1_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterUser2_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterUser3_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private async void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            bool filterActive = btnFilterMyRecords.Content.ToString().Contains("✓");

            if (!filterActive)
            {
                // Apply "My Records" filter
                await _viewModel.ApplyMyRecordsFilter(true, App.CurrentUser.Username);
                btnFilterMyRecords.Content = "My Records ✓";
                btnFilterMyRecords.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)); // Accent color
            }
            else
            {
                // Clear filter
                await _viewModel.ApplyMyRecordsFilter(false, App.CurrentUser.Username);
                btnFilterMyRecords.Content = "My Records";
                btnFilterMyRecords.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)); // Default
            }

            //UpdateRecordCount();
            //UpdatePagingControls();
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

        private async void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearAllFiltersAsync();

            // reset the “My Records” button visuals if it was active
            btnFilterMyRecords.Content = "My Records";
            btnFilterMyRecords.Background = (Brush)Application.Current.Resources["ControlBackground"];


            UpdateRecordCount();
            UpdatePagingControls();
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
            UpdatePagingControls();
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