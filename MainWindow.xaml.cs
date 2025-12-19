using Syncfusion.Windows.Shared;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Models;
using VANTAGE.Utilities;
using VANTAGE.Views;

namespace VANTAGE
{
    public partial class MainWindow : ChromelessWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            LoadInitialModule();

            UpdateStatusBar();

            this.Loaded += (s, e) =>
            {
                HighlightNavigationButton(btnProgress);

                // Force taskbar icon refresh (fixes first-run icon not showing)
                var iconPath = new Uri("pack://application:,,,/images/AppIcon.ico", UriKind.Absolute);
                this.Icon = BitmapFrame.Create(iconPath);
            };
            this.Closing += MainWindow_Closing;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                btnMaximize.Content = "☐";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                btnMaximize.Content = "❐";
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowLoadingOverlay(string message = "Processing...")
        {
            txtLoadingMessage.Text = message;
            txtLoadingProgress.Text = "";
            LoadingProgressBar.Value = 0;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateLoadingProgress(int current, int total, string? message = null)
        {
            if (message != null)
                txtLoadingMessage.Text = message;

            txtLoadingProgress.Text = $"{current:N0} of {total:N0} records";
            LoadingProgressBar.Value = total > 0 ? (current * 100.0 / total) : 0;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            AppLogger.Info("Application closing", "MainWindow.MainWindow_Closing", App.CurrentUser?.Username);
        }

        private void UpdateStatusBar()
        {
            // Update current user (with null check)
            if (App.CurrentUser != null)
            {
                txtCurrentUser.Text = $"User: {App.CurrentUser.Username}";
            }
            else
            {
                txtCurrentUser.Text = "User: Unknown";
            }

            // Update last sync time
            UpdateLastSyncDisplay();
        }

        public void UpdateLastSyncDisplay()
        {
            var lastSyncString = SettingsManager.GetUserSetting(App.CurrentUserID, "LastSyncUtcDate");

            if (string.IsNullOrEmpty(lastSyncString))
            {
                txtLastSyncsd.Text = "Last Sync: Never";
                return;
            }

            if (DateTime.TryParse(lastSyncString, out DateTime lastSyncUtc))
            {
                var localTime = lastSyncUtc.ToLocalTime();
                txtLastSyncsd.Text = $"Last Sync: {localTime:M/d/yyyy HH:mm}";
            }
            else
            {
                txtLastSyncsd.Text = "Last Sync: Never";
            }
        }

        private void LoadInitialModule()
        {
            // Set app version dynamically
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            txtAppVersion.Text = $"Vantage: MILESTONE v{version?.Major}.{version?.Minor}.{version?.Build}";

            // Load PROGRESS module by default
            LoadProgressModule();

            // Disable ADMIN button if not admin (checked against Azure)
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                btnAdmin.IsEnabled = false;
                btnAdmin.Opacity = 0.5;
                btnAdmin.ToolTip = "Admin privileges required (or offline)";
            }
        }

        // TOOLBAR BUTTON HANDLERS
        private void HighlightNavigationButton(Syncfusion.Windows.Tools.Controls.ButtonAdv activeButton)
        {
            // Reset both navigation buttons
            borderProgress.Background = System.Windows.Media.Brushes.Transparent;
            btnProgress.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor");

            borderSchedule.Background = System.Windows.Media.Brushes.Transparent;
            btnSchedule.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor");

            ProgBookBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnPbook.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor");

            WorkPackageBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnWorkPackage.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor");

            // Highlight active button
            if (activeButton == btnProgress)
            {
                borderProgress.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnProgress.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
            }
            else if (activeButton == btnSchedule)
            {
                borderSchedule.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnSchedule.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
            }
            else if (activeButton == btnPbook)
            {
                ProgBookBorder.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnPbook.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
            }
            else if (activeButton == btnWorkPackage)
            {
                WorkPackageBorder.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnWorkPackage.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
            }
        }

        private void BtnProgress_Click(object sender, RoutedEventArgs e)
        {
            LoadProgressModule();
            HighlightNavigationButton(btnProgress);
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
        {
            HighlightNavigationButton(btnSchedule);

            // Clear current content
            ContentArea.Content = null;

            // Load Schedule Module
            var scheduleView = new VANTAGE.Views.ScheduleView();
            ContentArea.Content = scheduleView;
        }

        private void BtnPbook_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PRINT module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            HighlightNavigationButton(btnPbook);
        }

        private void BtnWorkPackage_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WORK PACKAGE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            HighlightNavigationButton(btnWorkPackage);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("CREATE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ImportP6File_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Step 1: File picker
                var fileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Excel Files|*.xlsx;*.xls",
                    Title = "Select P6 Schedule File"
                };

                if (fileDialog.ShowDialog() != true)
                    return;

                // Step 2: Show P6ImportDialog
                var p6Dialog = new VANTAGE.Dialogs.P6ImportDialog(fileDialog.FileName);
                if (p6Dialog.ShowDialog() != true)
                    return;

                // Step 3: Import with BusyDialog
                var busyDialog = new BusyDialog(this);
                busyDialog.UpdateStatus("Importing P6 schedule...");
                busyDialog.Show();

                try
                {
                    int imported = await ScheduleExcelImporter.ImportFromP6Async(
                        fileDialog.FileName,
                        p6Dialog.SelectedWeekEndDate,
                        p6Dialog.SelectedProjectIDs,
                        new Progress<string>(msg => busyDialog.UpdateStatus(msg))
                    );

                    busyDialog.Close();

                    // Step 4: Show results
                    MessageBox.Show(
                        $"Successfully imported {imported} schedule activities for week ending {p6Dialog.SelectedWeekEndDate:yyyy-MM-dd}\n\n" +
                        $"Projects: {string.Join(", ", p6Dialog.SelectedProjectIDs)}",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    busyDialog.Close();
                    throw;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.ImportP6File_Click", App.CurrentUser?.Username);
                MessageBox.Show(
                    $"Import failed: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportP6File_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void MenuExcelImportReplace_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Excel File to Import",
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Confirm replace action
                    var result = MessageBox.Show(
                        "This will REPLACE all existing activities with data from the Excel file.\n\nAre you sure you want to continue?",
                        "Confirm Replace",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        // Show loading overlay
                        ShowLoadingOverlay("Importing Excel File...");

                        // Create progress reporter
                        var progress = new Progress<(int current, int total, string message)>(report =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (report.total > 0)
                                {
                                    UpdateLoadingProgress(report.current, report.total, report.message);
                                }
                                else
                                {
                                    txtLoadingMessage.Text = report.message;
                                }
                            });
                        });

                        // Import with replace mode (async)
                        int imported = await ExcelImporter.ImportActivitiesAsync(openFileDialog.FileName, replaceMode: true, progress);

                        // Hide loading overlay
                        HideLoadingOverlay();

                        MessageBox.Show(
                            $"Successfully imported {imported} activities.\n\nAll previous data has been replaced.",
                            "Import Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );

                        // Refresh the view if we're on Progress module
                        if (ContentArea.Content is Views.ProgressView)
                        {
                            LoadProgressModule();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                MessageBox.Show(
                    $"Error importing Excel file:\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void MenuExcelImportCombine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Excel File to Import",
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Show loading overlay
                    ShowLoadingOverlay("Importing Excel File...");

                    // Create progress reporter
                    var progress = new Progress<(int current, int total, string message)>(report =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (report.total > 0)
                            {
                                UpdateLoadingProgress(report.current, report.total, report.message);
                            }
                            else
                            {
                                txtLoadingMessage.Text = report.message;
                            }
                        });
                    });

                    // Import with combine mode (async)
                    int imported = await ExcelImporter.ImportActivitiesAsync(openFileDialog.FileName, replaceMode: false, progress);

                    // Hide loading overlay
                    HideLoadingOverlay();

                    MessageBox.Show(
                        $"Successfully imported {imported} new activities.\n\nExisting activities were preserved (duplicates skipped).",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // Refresh the view if we're on Progress module
                    if (ContentArea.Content is Views.ProgressView)
                    {
                        LoadProgressModule();
                    }
                }
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                MessageBox.Show(
                    $"Error importing Excel file:\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void ExcelExportActivities_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current ProgressView instance
                var progressView = ContentArea.Content as ProgressView;
                if (progressView == null)
                {
                    MessageBox.Show("Progress module not loaded.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get the ViewModel
                var viewModel = progressView.DataContext as ViewModels.ProgressViewModel;
                if (viewModel == null)
                {
                    MessageBox.Show("Unable to access progress data.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get all activities from ViewModel
                var allActivities = viewModel.Activities?.ToList();
                if (allActivities == null || allActivities.Count == 0)
                {
                    MessageBox.Show("No activities to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get filtered activities from the grid's view
                var gridField = progressView.GetType().GetField("sfActivities",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                List<Activity>? filteredActivities = null;
                bool hasActiveFilters = false;

                if (gridField != null)
                {
                    var grid = gridField.GetValue(progressView) as Syncfusion.UI.Xaml.Grid.SfDataGrid;
                    if (grid?.View != null)
                    {
                        filteredActivities = grid.View.Records
                            .Select(r => r.Data as Activity)
                            .Where(a => a != null)
                            .Cast<Activity>()
                            .ToList();

                        hasActiveFilters = filteredActivities.Count < allActivities.Count;
                    }
                }

                if (filteredActivities == null)
                {
                    filteredActivities = allActivities;
                    hasActiveFilters = false;
                }

                await ExportHelper.ExportActivitiesWithOptionsAsync(
                    this,
                    allActivities,
                    filteredActivities,
                    hasActiveFilters);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Export Activities Click", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExcelExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            await ExportHelper.ExportTemplateAsync(this);
        }

        private void MenuReport1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 1 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 2 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 3 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport4_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 4 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport5_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 5 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport6_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 6 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport7_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 7 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport8_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 8 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport9_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 9 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuReport10_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Report 10 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 1 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 2 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 3 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis4_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 4 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis5_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 5 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis6_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 6 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis7_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 7 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis8_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 8 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis9_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 9 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAnalysis10_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analysis 10 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === ADMIN DROPDOWN ===

        private void ToggleUserAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Check Azure connection first
                if (!AzureDbManager.CheckConnection(out string connectionError))
                {
                    MessageBox.Show($"Cannot manage admins - Azure unavailable:\n\n{connectionError}",
                        "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get list of all users from local database (for display)
                using var localConn = DatabaseSetup.GetConnection();
                localConn.Open();

                var command = localConn.CreateCommand();
                command.CommandText = "SELECT UserID, Username, FullName FROM Users ORDER BY Username";

                var users = new System.Collections.Generic.List<(int UserID, string Username, string FullName)>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2)
                    ));
                }
                reader.Close();
                localConn.Close();

                if (users.Count == 0)
                {
                    MessageBox.Show("No users found in database.", "No Users", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Check which users are admins from Azure
                var adminUsers = new System.Collections.Generic.HashSet<string>();
                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();
                var adminCmd = azureConn.CreateCommand();
                adminCmd.CommandText = "SELECT Username FROM Admins";
                using var adminReader = adminCmd.ExecuteReader();
                while (adminReader.Read())
                {
                    adminUsers.Add(adminReader.GetString(0).ToLower());
                }
                adminReader.Close();

                // Build display list
                var userList = users.Select(u =>
                    $"{u.Username} ({u.FullName}) - {(adminUsers.Contains(u.Username.ToLower()) ? "ADMIN" : "User")}"
                ).ToList();

                var dialog = new System.Windows.Window
                {
                    Title = "Toggle Admin Status",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1E1E1E"))
                };

                var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

                var label = new System.Windows.Controls.TextBlock
                {
                    Text = "Select user to toggle admin status:",
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var listBox = new System.Windows.Controls.ListBox
                {
                    Height = 150,
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF2A2A2A")),
                    Foreground = System.Windows.Media.Brushes.White
                };

                foreach (var userStr in userList)
                {
                    listBox.Items.Add(userStr);
                }

                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "Toggle Admin",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "Cancel",
                    Width = 100,
                    Height = 30
                };

                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        var selectedUser = users[listBox.SelectedIndex];
                        bool isCurrentlyAdmin = adminUsers.Contains(selectedUser.Username.ToLower());

                        try
                        {
                            var toggleCmd = azureConn.CreateCommand();

                            if (isCurrentlyAdmin)
                            {
                                // Remove from Admins table
                                toggleCmd.CommandText = "DELETE FROM Admins WHERE Username = @username";
                                toggleCmd.Parameters.AddWithValue("@username", selectedUser.Username);
                                toggleCmd.ExecuteNonQuery();
                                MessageBox.Show($"Admin revoked from {selectedUser.Username}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                // Add to Admins table
                                toggleCmd.CommandText = "INSERT INTO Admins (Username, FullName) VALUES (@username, @fullname)";
                                toggleCmd.Parameters.AddWithValue("@username", selectedUser.Username);
                                toggleCmd.Parameters.AddWithValue("@fullname", selectedUser.FullName);
                                toggleCmd.ExecuteNonQuery();
                                MessageBox.Show($"Admin granted to {selectedUser.Username}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }

                            // If the selected user is the current user, update UI
                            if (selectedUser.UserID == App.CurrentUserID)
                            {
                                if (isCurrentlyAdmin)
                                {
                                    // Revoked admin from current user
                                    App.CurrentUser.IsAdmin = false;
                                    btnAdmin.IsEnabled = false;
                                    btnAdmin.Opacity = 0.5;
                                    btnAdmin.ToolTip = "Admin privileges required";
                                }
                                else
                                {
                                    // Granted admin to current user
                                    App.CurrentUser.IsAdmin = true;
                                    btnAdmin.IsEnabled = true;
                                    btnAdmin.Opacity = 1.0;
                                    btnAdmin.ToolTip = null;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error updating admin status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        dialog.DialogResult = true;
                        dialog.Close();
                    }
                };

                cancelButton.Click += (s, args) =>
                {
                    dialog.Close();
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(listBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;
                dialog.ShowDialog();

                azureConn.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletedRecordsUi_Click(object sender, RoutedEventArgs e)
        {
            // Security check
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("This feature is only available to administrators.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var deletedRecordsWindow = new VANTAGE.Views.DeletedRecordsView();
            deletedRecordsWindow.Owner = this;
            deletedRecordsWindow.ShowDialog();
        }

        private void MenuAdmin3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 3 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin4_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 4 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin5_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 5 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin6_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 6 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin7_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 7 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin8_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 8 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin9_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 9 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAdmin10_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 10 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // TEST BUTTON HANDLER
        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            // Show context menu
            btnTest.ContextMenu.PlacementTarget = btnTest;
            btnTest.ContextMenu.IsOpen = true;
        }

        // TEST MENU HANDLERS
        private void MenuToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            // This is a TEST function - toggle current user's admin status via Azure
            try
            {
                if (App.CurrentUser == null)
                {
                    MessageBox.Show("No current user!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!AzureDbManager.CheckConnection(out string connectionError))
                {
                    MessageBox.Show($"Cannot toggle admin - Azure unavailable:\n\n{connectionError}",
                        "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                // Check if current user is admin
                var checkCmd = azureConn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Admins WHERE Username = @username";
                checkCmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);
                bool isAdmin = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                if (isAdmin)
                {
                    // Remove from Admins
                    var deleteCmd = azureConn.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM Admins WHERE Username = @username";
                    deleteCmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);
                    deleteCmd.ExecuteNonQuery();

                    App.CurrentUser.IsAdmin = false;
                    btnAdmin.IsEnabled = false;
                    btnAdmin.Opacity = 0.5;
                    MessageBox.Show($"Admin revoked from {App.CurrentUser.Username}", "Admin Toggled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Add to Admins
                    var insertCmd = azureConn.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO Admins (Username, FullName) VALUES (@username, @fullname)";
                    insertCmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);
                    insertCmd.Parameters.AddWithValue("@fullname", App.CurrentUser.FullName ?? "");
                    insertCmd.ExecuteNonQuery();

                    App.CurrentUser.IsAdmin = true;
                    btnAdmin.IsEnabled = true;
                    btnAdmin.Opacity = 1.0;
                    MessageBox.Show($"Admin granted to {App.CurrentUser.Username}", "Admin Toggled", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                azureConn.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling admin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Placeholder test handlers
        private async void MenuResetLocalDirty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will set LocalDirty = 0 for ALL records in the database.\n\n" +
                    "This is a TEST function to verify that cell edits properly set LocalDirty = 1.\n\n" +
                    "Continue?",
                    "Reset LocalDirty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                int count = await ActivityRepository.ResetAllLocalDirtyAsync();

                // Refresh the current view if it's ProgressView
                if (ContentArea.Content is Views.ProgressView progressView)
                {
                    await progressView.RefreshData();
                }

                MessageBox.Show(
                    $"Successfully reset LocalDirty to 0 for {count:N0} records.\n\n" +
                    "Grid has been refreshed with updated values.\n\n" +
                    "Now edit a cell to verify LocalDirty gets set to 1.",
                    "Reset Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error resetting LocalDirty: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void ToggleUpdatedBy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will set UpdatedBy = 'Bob' for ALL records in the database.\n\n" +
                    "This is a TEST function to verify that cell edits properly update UpdatedBy.\n\n" +
                    "Continue?",
                    "Set UpdatedBy to Bob",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                int count = await ActivityRepository.SetAllUpdatedByAsync("Bob");

                // Refresh the current view if it's ProgressView
                if (ContentArea.Content is Views.ProgressView progressView)
                {
                    await progressView.RefreshData();
                }

                MessageBox.Show(
                    $"Successfully set UpdatedBy = 'Bob' for {count:N0} records.\n\n" +
                    "Grid has been refreshed with updated values.\n\n" +
                    "Now edit a cell to verify UpdatedBy gets set to your username.",
                    "Update Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error setting UpdatedBy: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MenuTest2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 2 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 3 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest4_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 4 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest5_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 5 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest6_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 6 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest7_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 7 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTest8_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test 8 - Not implemented", "Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === TOOLS DROPDOWN ===

        private void BtnTools_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuTool1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 1 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 2 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 3 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool4_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 4 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool5_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 5 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool6_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 6 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool7_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 7 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool8_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 8 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool9_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 9 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTool10_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tool 10 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // MODULE LOADING

        private void LoadProgressModule()
        {
            var progressView = new Views.ProgressView();
            ContentArea.Content = progressView;
        }
    }
}