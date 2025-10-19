using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.ViewModels;
using VANTAGE.Models;

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

            InitializeColumnVisibility();  // ← PUT THIS BACK
            UpdateRecordCount();

            // Load data AFTER the view is loaded
            this.Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            // REMOVE InitializeColumnVisibility() from here

            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private void InitializeColumnVisibility()
        {
            // Build dictionary of existing columns first
            foreach (var column in dgActivities.Columns)
            {
                string headerText = column.Header?.ToString() ?? "Unknown";
                _columnMap[headerText] = column;
            }

            // Hardcoded list of ALL columns (in order they should appear)
            var allColumns = new List<string>
    {
        "Activity ID",
        "Description",
        "Component Type",
        "Phase Category",
        "ROC Step",
        "Project ID",
        "Work Package",
        "Phase Code",
        "Drawing No",
        "Sch Start",
        "Sch Finish",
        "Sch Status",
        "Activity No",
        "Quantity",
        "Earned Qty",
        "% Complete",
        "Budget Hrs",
        "Earned Hrs",
        "Status",
        "UDF One",
        "UDF Two",
        "Assigned To",
        "Hex NO",
        "Revision No",
        "Secondary Drawing",
        "Sheet No",
        "Comments",
        "Sch Act No (Field)",
        "Tag Aux1",
        "Tag Aux2",
        "Tag Aux3",
        "Tag Area",
        "CO No",
        "Equipment No",
        "Estimator",
        "Insulation Type",
        "Line No",
        "Material Spec",
        "Paint Code",
        "Pipe Grade",
        "RFI No",
        "Service",
        "Shop/Field",
        "Sub Area",
        "System",
        "System No",
        "Tag No",
        "Tracing",
        "XRAY",
        "Date Trigger",
        "UDF Three",
        "UDF Four",
        "UDF Five",
        "UDF Six",
        "UDF Seven",
        "UDF Eight",
        "UDF Nine",
        "UDF Ten",
        "Last Modified By",
        "Created By",
        "UDF Fourteen",
        "UDF Fifteen",
        "UDF Sixteen",
        "UDF Seventeen",
        "UDF Eighteen",
        "UDF Twenty",
        "Base Unit",
        "Budget Hrs Group",
        "Budget Hrs ROC",
        "Earned Hrs ROC",
        "UOM",
        "Earn Qty (Calc)",
        "Percent Earned",
        "EQ QTY",
        "EQ UOM",
        "ROC ID",
        "Lookup ROC ID",
        "ROC Perc",
        "ROC Budget Qty",
        "Pipe Size 1",
        "Pipe Size 2",
        "Prev Earned Hrs",
        "Prev Earned Qty",
        "Timestamp",
        "Client EQ QTY Budget",
        "UDF Two (Val)",
        "UDF Three (Val)",
        "Client Earned EQ QTY"
    };

            // Add checkboxes for ALL columns
            foreach (var columnName in allColumns)
            {
                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = _columnMap.ContainsKey(columnName) && _columnMap[columnName].Visibility == Visibility.Visible,
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }

            System.Diagnostics.Debug.WriteLine($"→ Total checkboxes added: {lstColumnVisibility.Items.Count}");
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

        private void UpdateRecordCount()
        {
            txtFilteredCount.Text = $"{_viewModel.FilteredCount} of {_viewModel.PageSize} records (Total: {_viewModel.TotalRecordCount})";
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

        private void BtnFilterPipe_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterSteel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterElec_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // Toggle filter
            if (_viewModel.ActivitiesView.Filter == null)
            {
                // Apply "My Records" filter
                _viewModel.ActivitiesView.Filter = obj =>
                {
                    if (obj is Activity activity)
                    {
                        return activity.IsMyRecord;
                    }
                    return false;
                };

                btnFilterMyRecords.Content = "My Records ✓";
                btnFilterMyRecords.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)); // Accent color
            }
            else
            {
                // Clear filter
                _viewModel.ActivitiesView.Filter = null;
                btnFilterMyRecords.Content = "My Records";
                btnFilterMyRecords.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)); // Default
            }

            UpdateRecordCount();
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

        private void DgActivities_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // TODO: Implement auto-save
        }

        private void DgActivities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuAssignToMe_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to assign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Assign to current user
            foreach (var activity in selectedActivities)
            {
                activity.AssignedToUsername = App.CurrentUser.Username;
                activity.LastModifiedBy = App.CurrentUser.Username;

                // TODO: Save to database (Phase 2 - Auto-save)
            }

            MessageBox.Show($"Assigned {selectedActivities.Count} record(s) to you.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            // Refresh the DataGrid to show color changes
            dgActivities.Items.Refresh();
        }

        private void MenuAssignToUser_Click(object sender, RoutedEventArgs e)
        {
            // Get selected activities
            var selectedActivities = dgActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to assign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get list of all users from database
            var users = GetAllUsers();

            if (!users.Any())
            {
                MessageBox.Show("No users found in the database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Show user selection dialog
            var selectedUser = ShowUserSelectionDialog(users);

            if (selectedUser == null)
            {
                return; // User cancelled
            }

            // Assign to selected user
            foreach (var activity in selectedActivities)
            {
                activity.AssignedToUsername = selectedUser.Username;
                activity.LastModifiedBy = App.CurrentUser.Username;

                // TODO: Save to database (Phase 2 - Auto-save)
            }

            MessageBox.Show($"Assigned {selectedActivities.Count} record(s) to {selectedUser.Username}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            // Refresh the DataGrid
            dgActivities.Items.Refresh();
        }

        private void MenuMarkComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuMarkNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuUpdatePercent_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }
    }
}