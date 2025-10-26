using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private Dictionary<string, DataGridColumn> _columnMap = new Dictionary<string, DataGridColumn>();
        private ProgressViewModel _viewModel;

        public ProgressView()
        {
            InitializeComponent();

            _viewModel = new ProgressViewModel();
            this.DataContext = _viewModel;

            dgActivities.ItemsSource = _viewModel.ActivitiesView;

            InitializeColumnVisibility();
            InitializeColumnTooltips();
            UpdateRecordCount();

            // Load data AFTER the view is loaded
            this.Loaded += OnViewLoaded;
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
            var columnName = button?.Tag as string;

            System.Diagnostics.Debug.WriteLine($"Filter button clicked for column: {columnName}");

            // Close any existing popup
            if (_activeFilterPopup != null)
            {
                _activeFilterPopup.IsOpen = false;
            }

            // Create filter popup
            var filterControl = new Controls.ColumnFilterPopup();
            filterControl.FilterApplied += FilterControl_FilterApplied;
            filterControl.FilterCleared += FilterControl_FilterCleared;

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

        private void FilterControl_FilterApplied(object sender, Controls.FilterEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Filter applied to {_activeFilterColumn}: {e.FilterType} = '{e.FilterValue}'");

            // Apply filter through ViewModel
            _viewModel?.ApplyFilter(_activeFilterColumn, e.FilterType, e.FilterValue);

            _activeFilterPopup.IsOpen = false;
        }

        private void FilterControl_FilterCleared(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Filter cleared for {_activeFilterColumn}");

            // Clear filter through ViewModel
            _viewModel?.ClearFilter(_activeFilterColumn);

            _activeFilterPopup.IsOpen = false;
        }
        /// <summary>
        /// Auto-save when user finishes editing a cell
        /// </summary>

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {


            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }
        /// <summary>
        /// Initialize tooltips for column headers showing OldVantage names
        /// </summary>
        private void InitializeColumnTooltips()
        {
            try
            {
                // Apply tooltips to columns
                int tooltipsSet = 0;
                foreach (var column in dgActivities.Columns)
                {
                    // Get the property name from the column
                    string propertyName = GetColumnPropertyName(column);

                    // Get the DbColumnName from property name
                    string dbColumnName = ColumnMapper.GetDbColumnName(propertyName);

                    // Query the ColumnMappings table for this specific column
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                SELECT OldVantageName 
                FROM ColumnMappings 
                WHERE DbColumnName = @dbColumn";
                    command.Parameters.AddWithValue("@dbColumn", dbColumnName);

                    var result = command.ExecuteScalar();
                    string tooltip;

                    if (result == null || result == DBNull.Value)
                    {
                        // Not in Excel
                        tooltip = $"{propertyName} - Not in export";
                    }
                    else
                    {
                        // Show OldVantage name
                        tooltip = $"Excel: {result}";
                    }

                    // Set tooltip on the column header
                    if (column.Header is string headerString)
                    {
                        var textBlock = new System.Windows.Controls.TextBlock
                        {
                            Text = headerString,
                            ToolTip = tooltip
                        };
                        column.Header = textBlock;
                        tooltipsSet++;
                    }
                    else if (column.Header is System.Windows.Controls.ContentControl headerControl)
                    {
                        headerControl.ToolTip = tooltip;
                        tooltipsSet++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Column tooltips initialized: {tooltipsSet} tooltips set");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error initializing tooltips: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract property name from column (tries binding path first, then header text)
        /// </summary>
        private string GetColumnPropertyName(DataGridColumn column)
        {
            // Try to get property name from binding path (most reliable)
            if (column is DataGridBoundColumn boundColumn)
            {
                if (boundColumn.Binding is System.Windows.Data.Binding binding)
                {
                    string propertyName = binding.Path.Path;

                    // Strip _Display suffix if present (for display wrapper properties)
                    if (propertyName.EndsWith("_Display"))
                    {
                        propertyName = propertyName.Replace("_Display", "");
                    }

                    return propertyName;
                }
            }

            // Fallback: Extract from header (for template columns or complex headers)
            if (column.Header == null)
                return "Unknown";

            if (column.Header is string headerString)
                return headerString;

            if (column.Header is System.Windows.Controls.ContentControl contentControl)
            {
                if (contentControl.Content is string content)
                    return content;
                return contentControl.Content?.ToString() ?? "Unknown";
            }

            return column.Header.ToString();
        }
        private void InitializeColumnVisibility()
        {
            lstColumnVisibility.Items.Clear();
            _columnMap.Clear();

            foreach (var column in dgActivities.Columns)
            {
                // Get property name from binding path
                string columnName = GetColumnPropertyName(column);

                _columnMap[columnName] = column;

                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = column.Visibility == Visibility.Visible,
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White,
                    Tag = column
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }

            System.Diagnostics.Debug.WriteLine($"✓ Column visibility initialized: {dgActivities.Columns.Count} columns");
        }
        /// <summary>
        /// Prevent editing of records not assigned to current user
        /// </summary>
        private void DgActivities_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var activity = e.Row.Item as Activity;
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
            column.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        private async void MenuUnassign_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();
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
                        System.Diagnostics.Debug.WriteLine($"✓ Activity {activity.ActivityID} unassigned");
                    }
                }

                MessageBox.Show($"Unassigned {successCount} record(s).", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                dgActivities.Items.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error unassigning records: {ex.Message}");
                MessageBox.Show($"Error unassigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UpdateRecordCount()
        {
            txtFilteredCount.Text = $"{_viewModel.FilteredCount} of {_viewModel.PageSize} records (Total: {_viewModel.TotalRecordCount})";
            UpdateSummaryPanel();
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

            UpdateRecordCount();
            UpdatePagingControls();
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
                System.Diagnostics.Debug.WriteLine($"Error loading users: {ex.Message}");
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

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void LstColumnVisibility_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Not needed - using CheckBox events instead
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.SearchText = txtSearch.Text;
            UpdateRecordCount();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        /// <summary>
        /// Auto-save when user finishes editing a cell
        /// </summary>
        private async void DgActivities_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            // Only save if edit was committed (not cancelled)
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            try
            {
                // Get the edited activity
                var editedActivity = e.Row.Item as Activity;
                if (editedActivity == null)
                    return;

                // Update LastModifiedBy with current user
                editedActivity.LastModifiedBy = App.CurrentUser?.Username ?? "Unknown";

                // Wait for the edit to fully commit before saving
                await Dispatcher.InvokeAsync(async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"→ Saving changes for Activity {editedActivity.ActivityID}...");
                    System.Diagnostics.Debug.WriteLine($"   Catg_ROC_Step = '{editedActivity.ROCStep}'");

                    // Save to database
                    bool success = await ActivityRepository.UpdateActivityInDatabase(editedActivity);

                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Activity {editedActivity.ActivityID} saved successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to save Activity {editedActivity.ActivityID}");
                        MessageBox.Show(
                            $"Failed to save changes for Activity {editedActivity.ActivityID}.\nPlease try again.",
                            "Save Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in RowEditEnding: {ex.Message}");
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DgActivities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Implement
        }

        private async void MenuAssignToMe_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();
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
                        System.Diagnostics.Debug.WriteLine($"✓ Activity {activity.ActivityID} assigned to {App.CurrentUser.Username}");
                    }
                }

                MessageBox.Show($"Assigned {successCount} record(s) to you.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                dgActivities.Items.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error assigning records: {ex.Message}");
                MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuAssignToUser_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();
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
                Height = 150,
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
                            System.Diagnostics.Debug.WriteLine($"✓ Activity {activity.ActivityID} assigned to {selectedUser}");
                        }
                    }

                    MessageBox.Show($"Assigned {successCount} record(s) to {selectedUser}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    dgActivities.Items.Refresh();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error assigning records: {ex.Message}");
                    MessageBox.Show($"Error assigning records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuMarkComplete_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();
            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to mark complete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                int successCount = 0;
                foreach (var activity in selectedActivities)
                {
                    // Set to 100% complete
                    activity.PercentEntry = 1.0;

                    // This will trigger calculated field updates automatically via INotifyPropertyChanged
                    // Val_EarnedQty, Val_Percent_Earned, Val_EarnedHours_Ind will all update

                    // Update LastModifiedBy
                    activity.LastModifiedBy = App.CurrentUser.Username;

                    // Save to database
                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to save Activity {activity.ActivityID}");
                    }
                }

                if (successCount == selectedActivities.Count)
                {
                    MessageBox.Show($"Marked {successCount} record(s) as complete.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Marked {successCount} of {selectedActivities.Count} record(s) as complete. Some failed to save.", "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Refresh the DataGrid to show changes
                dgActivities.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error marking records complete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuMarkNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();
            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to mark not started.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                int successCount = 0;
                foreach (var activity in selectedActivities)
                {
                    // Set to 0% complete
                    activity.PercentEntry = 0.0;

                    // Update LastModifiedBy
                    activity.LastModifiedBy = App.CurrentUser.Username;

                    // Save to database
                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to save Activity {activity.ActivityID}");
                    }
                }

                if (successCount == selectedActivities.Count)
                {
                    MessageBox.Show($"Marked {successCount} record(s) as not started.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Marked {successCount} of {selectedActivities.Count} record(s) as not started. Some failed to save.", "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Refresh the DataGrid to show changes
                dgActivities.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error marking records not started: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}