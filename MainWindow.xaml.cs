using Syncfusion.Windows.Shared;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private void MenuScheduleDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VANTAGE.Diagnostics.ScheduleDiagnostic.RunDiagnostic();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuScheduleDiagnostics_Click");
                MessageBox.Show(
                    $"Diagnostic failed: {ex.Message}",
                    "Diagnostic Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
            // Check for unsaved Schedule changes
            if (ContentArea.Content is ScheduleView scheduleView)
            {
                if (!scheduleView.TryClose())
                {
                    e.Cancel = true;
                    return;
                }
            }

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

            // Hide ADMIN button if not admin (checked against Azure)
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                btnAdmin.Visibility = Visibility.Collapsed;
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
            if (!CanLeaveCurrentView())
                return;

            LoadProgressModule();
            HighlightNavigationButton(btnProgress);
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
        {
            // Already on Schedule? No need to check
            if (ContentArea.Content is ScheduleView)
            {
                HighlightNavigationButton(btnSchedule);
                return;
            }

            if (!CanLeaveCurrentView())
                return;

            HighlightNavigationButton(btnSchedule);
            ContentArea.Content = null;
            var scheduleView = new ScheduleView();
            ContentArea.Content = scheduleView;
        }

        private void BtnPbook_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeaveCurrentView())
                return;

            MessageBox.Show("PRINT module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            HighlightNavigationButton(btnPbook);
        }

        private void BtnWorkPackage_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeaveCurrentView())
                return;

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
                    // Refresh Schedule view if currently active
                    if (ContentArea.Content is VANTAGE.Views.ScheduleView scheduleView)
                    {
                        await scheduleView.RefreshDataAsync(p6Dialog.SelectedWeekEndDate);
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
                AppLogger.Error(ex, "MainWindow.ImportP6File_Click", App.CurrentUser?.Username);
                MessageBox.Show(
                    $"Import failed: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void ExportP6File_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if we're in Schedule view
                if (!(ContentArea.Content is Views.ScheduleView scheduleView))
                {
                    MessageBox.Show(
                        "Please navigate to the Schedule module first.",
                        "Export to P6",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var viewModel = scheduleView.DataContext as ViewModels.ScheduleViewModel;
                if (viewModel == null || viewModel.MasterRows == null || viewModel.MasterRows.Count == 0)
                {
                    MessageBox.Show(
                        "No schedule data loaded. Please select a Week Ending date first.",
                        "Export to P6",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Check for unsaved changes
                if (viewModel.HasUnsavedChanges)
                {
                    var saveResult = MessageBox.Show(
                        "You have unsaved changes that must be saved before exporting.\n\nSave now and continue with export?",
                        "Save Required",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information);

                    if (saveResult == MessageBoxResult.Cancel)
                        return;

                    // Save before proceeding
                    scheduleView.SaveChanges();
                }

                // Check required fields count
                if (viewModel.RequiredFieldsCount > 0)
                {
                    MessageBox.Show(
                        $"Cannot export: {viewModel.RequiredFieldsCount} required field(s) are incomplete.\n\n" +
                        "Please complete all Missed Reason and 3WLA fields before exporting.\n\n" +
                        "Click the 'Required Fields' button in the status bar to see which rows need attention.",
                        "Export Blocked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Get all master rows (unfiltered) for export
                var allRows = viewModel.GetAllMasterRows();

                // Show export options dialog
                var exportDialog = new Dialogs.P6ExportDialog(viewModel.SelectedWeekEndDate ?? DateTime.Today, allRows.Count);
                exportDialog.Owner = this;
                if (exportDialog.ShowDialog() != true)
                    return;

                // Get current date for filenames
                string dateStamp = DateTime.Now.ToString("yyyy-MM-dd");

                // Show save dialog for P6 file
                var p6SaveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Save P6 Export File",
                    FileName = $"To_P6_{dateStamp}.xlsx"
                };

                if (p6SaveDialog.ShowDialog() != true)
                    return;

                string p6FilePath = p6SaveDialog.FileName;

                // Show save dialog for Schedule Reports file
                var reportsSaveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Save 3WLA File",
                    FileName = $"3WLA_{dateStamp}.xlsx",
                    InitialDirectory = System.IO.Path.GetDirectoryName(p6FilePath)
                };

                if (reportsSaveDialog.ShowDialog() != true)
                    return;

                string reportsFilePath = reportsSaveDialog.FileName;

                // Export with busy dialog
                var busyDialog = new Dialogs.BusyDialog(this);
                busyDialog.UpdateStatus("Exporting to P6 format...");
                busyDialog.Show();

                try
                {
                    // Export P6 file
                    int exported = await Utilities.ScheduleExcelExporter.ExportToP6Async(
                        allRows,
                        p6FilePath,
                        exportDialog.StartTime,
                        exportDialog.FinishTime,
                        new Progress<string>(msg => busyDialog.UpdateStatus(msg)));

                    // Export Schedule Reports file
                    busyDialog.UpdateStatus("Creating Schedule Reports...");
                    var weekEndDate = allRows.FirstOrDefault()?.WeekEndDate ?? DateTime.Today;
                    await Utilities.ScheduleReportExporter.ExportAsync(
                        allRows,
                        weekEndDate,
                        reportsFilePath,
                        new Progress<string>(msg => busyDialog.UpdateStatus(msg)));
                    busyDialog.Close();

                    AppLogger.Info(
                        $"Exported {exported} schedule activities to P6 file: {p6FilePath}",
                        "MainWindow.ExportP6File_Click",
                        App.CurrentUser?.Username);

                    AppLogger.Info(
                        $"Created Schedule Reports: {reportsFilePath}",
                        "MainWindow.ExportP6File_Click",
                        App.CurrentUser?.Username);

                    MessageBox.Show(
                        $"Successfully exported {exported} activities.\n\n" +
                        $"P6 File:\n{p6FilePath}\n\n" +
                        $"Schedule Reports:\n{reportsFilePath}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch
                {
                    busyDialog.Close();
                    throw;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.ExportP6File_Click", App.CurrentUser?.Username);
                MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
                                    btnAdmin.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    // Granted admin to current user
                                    App.CurrentUser.IsAdmin = true;
                                    btnAdmin.Visibility = Visibility.Visible;
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

        // ============================================
        // ADD THIS METHOD TO MainWindow.xaml.cs
        // Replace one of the MenuAdmin#_Click placeholders (e.g., MenuAdmin3_Click)
        // ============================================

        private void MenuEditSnapshots_Click(object sender, RoutedEventArgs e)
        {
            // Check admin status
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nThis feature requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.AdminSnapshotsDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            // Refresh ScheduleView if loaded (snapshots may have been deleted)
            if (ContentArea.Content is Views.ScheduleView scheduleView)
            {
                var viewModel = scheduleView.DataContext as ViewModels.ScheduleViewModel;
                if (viewModel?.SelectedWeekEndDate != null)
                {
                    _ = viewModel.LoadScheduleDataAsync(viewModel.SelectedWeekEndDate.Value);
                }
            }
        }

        private void MenuEditUsers_Click(object sender, RoutedEventArgs e)
        {
            // Check admin status
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nThis feature requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.AdminUsersDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        // ============================================
        // ADD THIS METHOD TO MainWindow.xaml.cs
        // Replace one of the MenuAdmin#_Click placeholders (e.g., MenuAdmin5_Click)
        // ============================================

        private void MenuEditProjects_Click(object sender, RoutedEventArgs e)
        {
            // Check admin status
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nThis feature requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.AdminProjectsDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
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
                    btnAdmin.Visibility = Visibility.Collapsed;
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
                    btnAdmin.Visibility = Visibility.Visible;
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

        // ============================================
        // ADD THIS METHOD TO MainWindow.xaml.cs
        // Replace one of the MenuTest#_Click placeholders
        // ============================================

        private async void MenuClearAzureActivities_Click(object sender, RoutedEventArgs e)
        {
            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // First warning
            var result = MessageBox.Show(
                "⚠️ DANGER: This will DELETE ALL ACTIVITIES from the AZURE database!\n\n" +
                "This affects ALL USERS and CANNOT be undone.\n\n" +
                "Are you absolutely sure?",
                "Clear Azure Activities",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Require typing DELETE to confirm
            var confirmDialog = new Window
            {
                Title = "Confirm Deletion",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E")),
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = "Type DELETE to confirm:",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var textBox = new TextBox
            {
                Height = 30,
                FontSize = 14,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2D2D2D")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3F3F3F")),
                Padding = new Thickness(5)
            };
            stack.Children.Add(textBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3F3F3F")),
                Foreground = Brushes.White
            };
            btnCancel.Click += (s, args) => confirmDialog.DialogResult = false;

            var btnConfirm = new Button
            {
                Content = "Confirm",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB33A3A")),
                Foreground = Brushes.White
            };
            btnConfirm.Click += (s, args) =>
            {
                if (textBox.Text == "DELETE")
                    confirmDialog.DialogResult = true;
                else
                    MessageBox.Show("You must type DELETE exactly.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnConfirm);
            stack.Children.Add(btnPanel);

            confirmDialog.Content = stack;

            if (confirmDialog.ShowDialog() != true)
                return;

            // Execute deletion
            try
            {
                int totalCount = 0;
                int deletedCount = 0;
                const int batchSize = 50000;

                await Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    // Get count first
                    using var countCmd = azureConn.CreateCommand();
                    countCmd.CommandText = "SELECT COUNT(*) FROM Activities";
                    countCmd.CommandTimeout = 120;
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

                    // Delete in batches to avoid timeout
                    int batchDeleted;
                    do
                    {
                        using var deleteCmd = azureConn.CreateCommand();
                        deleteCmd.CommandText = $"DELETE TOP ({batchSize}) FROM Activities";
                        deleteCmd.CommandTimeout = 300;
                        batchDeleted = deleteCmd.ExecuteNonQuery();
                        deletedCount += batchDeleted;
                    } while (batchDeleted > 0);
                });

                // After deleting from Azure, reset local sync state
                await Task.Run(() =>
                {
                    using var localConn = DatabaseSetup.GetConnection();
                    localConn.Open();

                    // Reset LastPulledSyncVersion so next sync works properly
                    using var resetCmd = localConn.CreateCommand();
                    resetCmd.CommandText = "DELETE FROM AppSettings WHERE SettingName LIKE 'LastPulledSyncVersion_%'";
                    resetCmd.ExecuteNonQuery();

                    // Set all local records to dirty so they'll push on next sync
                    using var dirtyCmd = localConn.CreateCommand();
                    dirtyCmd.CommandText = "UPDATE Activities SET LocalDirty = 1";
                    dirtyCmd.ExecuteNonQuery();
                });

                // Refresh the grid if ProgressView is loaded
                if (ContentArea.Content is Views.ProgressView progressView)
                {
                    await progressView.RefreshData();
                }

                AppLogger.Info($"CLEARED AZURE ACTIVITIES: Deleted {deletedCount} records",
                    "MainWindow.MenuClearAzureActivities_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted {deletedCount:N0} activities from Azure.\n\n" +
                    "Sync state has been reset.\n" +
                    "Local records marked dirty - sync to re-upload.",
                    "Azure Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuClearAzureActivities_Click");
                MessageBox.Show($"Error clearing Azure activities:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void MenuDeleteSnapshots_Click(object sender, RoutedEventArgs e)
        {
            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nThis feature requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.DeleteSnapshotsDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Snapshots were deleted - refresh ScheduleView if loaded
                if (ContentArea.Content is Views.ScheduleView scheduleView)
                {
                    var viewModel = scheduleView.DataContext as ViewModels.ScheduleViewModel;
                    if (viewModel?.SelectedWeekEndDate != null)
                    {
                        _ = viewModel.LoadScheduleDataAsync(viewModel.SelectedWeekEndDate.Value);
                    }
                }
            }
        }

        private async void MenuClearLocalActivities_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will DELETE ALL ACTIVITIES from your local database.\n\n" +
                "This does NOT affect the Azure database.\n" +
                "You can restore data by syncing from Azure.\n\n" +
                "Continue?",
                "Clear Local Activities",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                int deletedCount = 0;

                await Task.Run(() =>
                {
                    using var conn = DatabaseSetup.GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM Activities";
                    deletedCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "DELETE FROM Activities";
                    cmd.ExecuteNonQuery();

                    // Also reset LastPulledSyncVersion so next sync pulls everything
                    cmd.CommandText = "DELETE FROM AppSettings WHERE SettingName LIKE 'LastPulledSyncVersion_%'";
                    cmd.ExecuteNonQuery();
                });

                AppLogger.Info($"Cleared {deletedCount} local activities", "MainWindow.MenuClearLocalActivities_Click", App.CurrentUser?.Username);

                // Clear the grid if ProgressView is loaded
                if (ContentArea.Content is Views.ProgressView progressView)
                {
                    await progressView.RefreshData();
                }

                MessageBox.Show(
                    $"Successfully deleted {deletedCount:N0} activities from local database.\n\n" +
                    "LastPulledSyncVersion has been reset.\n" +
                    "Sync to restore data from Azure.",
                    "Clear Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuClearLocalActivities_Click");
                MessageBox.Show($"Error clearing activities:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuClearLocalSchedule_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will DELETE ALL SCHEDULE DATA from your local database:\n\n" +
                "• Schedule table (P6 import data)\n" +
                "• ScheduleProjectMappings\n" +
                "• ThreeWeekLookahead\n\n" +
                "This does NOT affect Azure data.\n\n" +
                "Continue?",
                "Clear Local Schedule",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                int scheduleCount = 0;
                int mappingsCount = 0;
                int lookaheadCount = 0;

                await Task.Run(() =>
                {
                    using var conn = DatabaseSetup.GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();

                    // Get counts before delete
                    cmd.CommandText = "SELECT COUNT(*) FROM Schedule";
                    scheduleCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM ScheduleProjectMappings";
                    mappingsCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM ThreeWeekLookahead";
                    lookaheadCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    // Delete all schedule data
                    cmd.CommandText = "DELETE FROM Schedule";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DELETE FROM ScheduleProjectMappings";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DELETE FROM ThreeWeekLookahead";
                    cmd.ExecuteNonQuery();
                });

                AppLogger.Info(
                    $"Cleared local schedule: {scheduleCount} schedule rows, {mappingsCount} mappings, {lookaheadCount} lookahead rows",
                    "MainWindow.MenuClearLocalSchedule_Click",
                    App.CurrentUser?.Username);

                // Refresh ScheduleView if loaded
                if (ContentArea.Content is Views.ScheduleView scheduleView)
                {
                    scheduleView.ClearScheduleDisplay();
                }

                MessageBox.Show(
                    $"Successfully cleared local schedule data:\n\n" +
                    $"• Schedule rows: {scheduleCount:N0}\n" +
                    $"• Project mappings: {mappingsCount:N0}\n" +
                    $"• Lookahead rows: {lookaheadCount:N0}\n\n" +
                    "Import a new P6 file to reload.",
                    "Clear Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuClearLocalSchedule_Click");
                MessageBox.Show($"Error clearing schedule:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuExportSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.GetAllUserSettings(App.CurrentUserID);

                if (settings.Count == 0)
                {
                    MessageBox.Show("No settings to export.", "Export Settings",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var exportFile = new UserSettingsExportFile
                {
                    ExportedBy = App.CurrentUser?.Username ?? "Unknown",
                    ExportedDate = DateTime.UtcNow.ToString("o"),
                    AppVersion = "1.0.0",
                    Settings = settings
                };

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Settings",
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = ".json",
                    FileName = $"MILESTONE_Settings_{App.CurrentUser?.Username}_{DateTime.Now:yyyy-MM-dd}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(exportFile,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);

                    AppLogger.Info($"Exported {settings.Count} settings to {dialog.FileName}",
                        "MainWindow.MenuExportSettings_Click", App.CurrentUser?.Username ?? "Unknown");

                    MessageBox.Show($"Exported {settings.Count} settings successfully.",
                        "Export Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuExportSettings_Click");
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuImportSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Settings",
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() != true)
                    return;

                var json = System.IO.File.ReadAllText(dialog.FileName);
                var importFile = System.Text.Json.JsonSerializer.Deserialize<UserSettingsExportFile>(json);

                if (importFile == null || importFile.Settings == null || importFile.Settings.Count == 0)
                {
                    MessageBox.Show("No settings found in file.", "Import Settings",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Found {importFile.Settings.Count} settings exported by '{importFile.ExportedBy}' on {importFile.ExportedDate}.\n\n" +
                    "Choose import mode:\n" +
                    "YES = Replace all (delete existing, import new)\n" +
                    "NO = Merge (keep existing, add/update from file)\n" +
                    "CANCEL = Abort import",
                    "Import Settings",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                bool replaceAll = (result == MessageBoxResult.Yes);
                int imported = SettingsManager.ImportUserSettings(App.CurrentUserID, importFile.Settings, replaceAll);

                // Reload settings in current views
                if (ContentArea.Content is ProgressView progressView)
                {
                    progressView.ReloadColumnSettings();
                }
                else if (ContentArea.Content is ScheduleView scheduleView)
                {
                    scheduleView.ReloadColumnSettings();
                }

                MessageBox.Show(
                    $"Imported {imported} settings.\n\nSettings applied to current view.",
                    "Import Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Text.Json.JsonException)
            {
                MessageBox.Show("Invalid settings file format.", "Import Settings",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuImportSettings_Click");
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        // Check if current view is ScheduleView with unsaved changes
        private bool CanLeaveCurrentView()
        {
            if (ContentArea.Content is ScheduleView scheduleView)
            {
                return scheduleView.TryClose();
            }
            return true;
        }
    }
}