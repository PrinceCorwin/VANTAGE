using MILESTONE.Services.Procore;
using Syncfusion.Windows.Shared;
using System;
using System.IO;
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
using VANTAGE.ViewModels;
using VANTAGE.Views;


namespace VANTAGE
{
    public partial class MainWindow : ChromelessWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            // Enable horizontal scroll wheel support (MX Master, etc.)
            HorizontalScrollBehavior.EnableForWindow(this);

            InitializeSidePanel();

            LoadInitialModule();

            UpdateStatusBar();

            this.Loaded += (s, e) =>
            {
                HighlightNavigationButton(btnProgress);

                // Force taskbar icon refresh (fixes first-run icon not showing)
                var iconPath = new Uri("pack://application:,,,/images/AppIcon.ico", UriKind.Absolute);
                this.Icon = BitmapFrame.Create(iconPath);

                // Initialize Legacy I/O menu visibility from settings
                UpdateLegacyMenuVisibility();
            };
            this.Closing += MainWindow_Closing;
        }
        private SidePanelViewModel _sidePanelViewModel = null!;
        // ========================================
        // SIDE PANEL / HELP
        // ========================================
        private void SidebarSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Save the new width to the ViewModel (which persists to settings)
            _sidePanelViewModel.PanelWidth = SidebarColumn.Width.Value;
        }
        private void InitializeSidePanel()
        {
            _sidePanelViewModel = new SidePanelViewModel();
            SidePanel.DataContext = _sidePanelViewModel;
            _sidePanelViewModel.PropertyChanged += SidePanelViewModel_PropertyChanged;
        }

        private void SidePanelViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SidePanelViewModel.IsOpen))
            {
                UpdateSidebarVisibility();
            }
        }

        private void UpdateSidebarVisibility()
        {
            if (_sidePanelViewModel.IsOpen)
            {
                SidebarColumn.Width = new GridLength(_sidePanelViewModel.PanelWidth);
                SplitterColumn.Width = new GridLength(5);
                SidebarSplitter.Visibility = Visibility.Visible;
                SidePanel.Visibility = Visibility.Visible;
            }
            else
            {
                SidebarColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                SidebarSplitter.Visibility = Visibility.Collapsed;
                SidePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void MenuHelpSidebar_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
            _sidePanelViewModel.ShowHelp();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(
                $"MILESTONE\n\n" +
                $"Version: {version?.Major}.{version?.Minor}.{version?.Build}\n\n" +
                $"Construction Project Management System\n\n" +
                $"© {DateTime.Now.Year} Summit Constructors",
                "About MILESTONE",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Optional: Allow toggling sidebar with keyboard shortcut (F1)
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F1)
            {
                MenuHelpSidebar_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _sidePanelViewModel.IsOpen)
            {
                _sidePanelViewModel.Close();
                e.Handled = true;
            }
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

        public void ShowLoadingOverlay(string message = "Processing...")
        {
            txtLoadingMessage.Text = message;
            txtLoadingProgress.Text = "";
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        public void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateLoadingProgress(int current, int total, string? message = null)
        {
            if (message != null)
                txtLoadingMessage.Text = message;

            txtLoadingProgress.Text = $"{current:N0} of {total:N0} records";
            // Progress bar uses indeterminate mode - animation runs automatically
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
            var lastSyncString = SettingsManager.GetUserSetting( "LastSyncUtcDate");

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
            // Already on ProgressBooks? No need to check
            if (ContentArea.Content is ProgressBooksView)
            {
                HighlightNavigationButton(btnPbook);
                return;
            }

            if (!CanLeaveCurrentView())
                return;

            HighlightNavigationButton(btnPbook);
            ContentArea.Content = null;
            var progressBooksView = new ProgressBooksView();
            ContentArea.Content = progressBooksView;
        }

        private void BtnWorkPackage_Click(object sender, RoutedEventArgs e)
        {
            // Already on WorkPackage? No need to check
            if (ContentArea.Content is Views.WorkPackageView)
            {
                HighlightNavigationButton(btnWorkPackage);
                return;
            }

            if (!CanLeaveCurrentView())
                return;

            HighlightNavigationButton(btnWorkPackage);
            ContentArea.Content = null;
            var workPackageView = new Views.WorkPackageView();
            ContentArea.Content = workPackageView;
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

            try
            {
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

                // Show progress bar during export
                viewModel.IsLoading = true;

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
            finally
            {
                viewModel.IsLoading = false;
            }
        }

        private async void ExcelExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            await ExportHelper.ExportTemplateAsync(this);
        }

        // Legacy Export event handlers (imports are handled by auto-detecting Import Activities buttons)

        private async void MenuLegacyExportActivities_Click(object sender, RoutedEventArgs e)
        {
            var progressView = ContentArea.Content as ProgressView;
            if (progressView == null)
            {
                MessageBox.Show("Progress module not loaded.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var viewModel = progressView.DataContext as ViewModels.ProgressViewModel;
            if (viewModel == null)
            {
                MessageBox.Show("Unable to access progress data.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var allActivities = viewModel.Activities?.ToList();
                if (allActivities == null || allActivities.Count == 0)
                {
                    MessageBox.Show("No activities to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get filtered activities
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

                // Show progress bar during export
                viewModel.IsLoading = true;

                await ExportHelper.ExportActivitiesWithOptionsAsync(
                    this, allActivities, filteredActivities, hasActiveFilters, ExportFormat.Legacy);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Legacy Export Activities Click", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                viewModel.IsLoading = false;
            }
        }

        private async void MenuLegacyExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            await ExportHelper.ExportTemplateAsync(this, ExportFormat.Legacy);
        }

        // Toggle Legacy I/O menu visibility
        private void MenuToggleLegacyIO_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
            bool currentState = SettingsManager.GetShowLegacyIO();
            SettingsManager.SetShowLegacyIO(!currentState);
            UpdateLegacyMenuVisibility();
        }

        // Update visibility of Legacy Export menu items based on setting
        private void UpdateLegacyMenuVisibility()
        {
            var visibility = SettingsManager.GetShowLegacyIO() ? Visibility.Visible : Visibility.Collapsed;
            sepLegacyIO.Visibility = visibility;
            menuLegacyExport.Visibility = visibility;
            menuLegacyTemplate.Visibility = visibility;
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
                command.CommandText = "SELECT UserID, Username, FullName, Email FROM Users ORDER BY Username";

                var users = new System.Collections.Generic.List<(int UserID, string Username, string FullName, string Email)>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        reader.IsDBNull(3) ? "" : reader.GetString(3)
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
                adminCmd.CommandText = "SELECT Username FROM VMS_Admins";
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
                    Background = ThemeHelper.BackgroundColor
                };

                var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

                var label = new System.Windows.Controls.TextBlock
                {
                    Text = "Select user to toggle admin status:",
                    Foreground = ThemeHelper.ForegroundColor,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var listBox = new System.Windows.Controls.ListBox
                {
                    Height = 150,
                    Background = ThemeHelper.ControlBackground,
                    Foreground = ThemeHelper.ForegroundColor
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

                            string action;
                            if (isCurrentlyAdmin)
                            {
                                // Remove from Admins table
                                toggleCmd.CommandText = "DELETE FROM VMS_Admins WHERE Username = @username";
                                toggleCmd.Parameters.AddWithValue("@username", selectedUser.Username);
                                toggleCmd.ExecuteNonQuery();
                                action = "revoked";
                                MessageBox.Show($"Admin revoked from {selectedUser.Username}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                // Add to Admins table
                                toggleCmd.CommandText = "INSERT INTO VMS_Admins (Username, FullName) VALUES (@username, @fullname)";
                                toggleCmd.Parameters.AddWithValue("@username", selectedUser.Username);
                                toggleCmd.Parameters.AddWithValue("@fullname", selectedUser.FullName);
                                toggleCmd.ExecuteNonQuery();
                                action = "granted";
                                MessageBox.Show($"Admin granted to {selectedUser.Username}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }

                            // Send email notification to the target user
                            if (!string.IsNullOrWhiteSpace(selectedUser.Email))
                            {
                                string recipientName = !string.IsNullOrWhiteSpace(selectedUser.FullName) ? selectedUser.FullName : selectedUser.Username;
                                string changedBy = App.CurrentUser?.Username ?? "Unknown";
                                string emailSubject = $"VANTAGE: MS - Admin privileges {action}";
                                string emailHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px 20px; border-radius: 4px 4px 0 0; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 0 0 4px 4px; }}
        .highlight {{ font-size: 20px; font-weight: bold; color: {(action == "granted" ? "#2E7D32" : "#C62828")}; }}
        .details {{ margin-top: 15px; }}
        .detail-row {{ padding: 8px 0; border-bottom: 1px solid #ddd; }}
        .label {{ font-weight: 600; color: #555; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>VANTAGE: MS Admin Status Change</h2>
        </div>
        <div class='content'>
            <p>Hello {System.Net.WebUtility.HtmlEncode(recipientName)},</p>
            <p class='highlight'>Admin privileges {action}</p>
            <div class='details'>
                <div class='detail-row'>
                    <span class='label'>Changed by:</span> {System.Net.WebUtility.HtmlEncode(changedBy)}
                </div>
                <div class='detail-row'>
                    <span class='label'>Date:</span> {DateTime.Now:MMMM d, yyyy h:mm tt}
                </div>
            </div>
            <p style='margin-top: 20px;'>{(action == "granted" ? "You now have access to admin features. Restart VANTAGE: MS to see the Admin menu." : "Your admin access has been removed. The change will take effect next time you open VANTAGE: MS.")}</p>
            <div class='footer'>
                <p>This is an automated message from VANTAGE: MS. Please do not reply to this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";
                                _ = EmailService.SendEmailAsync(selectedUser.Email, emailSubject, emailHtml);
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

        // Open the Manage Progress Log dialog
        private void MenuManageProgressLog_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nThis feature requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.ManageProgressLogDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
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

        // TEST BUTTON HANDLER
        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            // Show context menu
            btnTest.ContextMenu.PlacementTarget = btnTest;
            btnTest.ContextMenu.IsOpen = true;
        }
        private async void MenuTestTextract_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                using var client = new Amazon.Textract.AmazonTextractClient(
                    Credentials.AwsAccessKey,
                    Credentials.AwsSecretKey,
                    Amazon.RegionEndpoint.GetBySystemName(Credentials.AwsRegion));

                // Test with a 1x1 pixel PNG (minimal API call)
                var testImage = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

                var response = await client.DetectDocumentTextAsync(
                    new Amazon.Textract.Model.DetectDocumentTextRequest
                    {
                        Document = new Amazon.Textract.Model.Document
                        {
                            Bytes = new MemoryStream(testImage)
                        }
                    });

                Mouse.OverrideCursor = null;

                MessageBox.Show(
                    $"AWS Textract connection successful!\n\nRegion: {Credentials.AwsRegion}\nAPI responded with {response.Blocks?.Count ?? 0} blocks.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;

                MessageBox.Show(
                    $"AWS Textract connection failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                AppLogger.Error(ex, "MainWindow.MenuTestTextract_Click");
            }
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
                checkCmd.CommandText = "SELECT COUNT(*) FROM VMS_Admins WHERE Username = @username";
                checkCmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);
                bool isAdmin = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                if (isAdmin)
                {
                    // Remove from Admins
                    var deleteCmd = azureConn.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM VMS_Admins WHERE Username = @username";
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
                    insertCmd.CommandText = "INSERT INTO VMS_Admins (Username, FullName) VALUES (@username, @fullname)";
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
                Background = ThemeHelper.BackgroundColor,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = "Type DELETE to confirm:",
                Foreground = ThemeHelper.ForegroundColor,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var textBox = new TextBox
            {
                Height = 30,
                FontSize = 14,
                Background = ThemeHelper.ControlBackground,
                Foreground = ThemeHelper.ForegroundColor,
                BorderBrush = ThemeHelper.SidebarBorder,
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
                Background = ThemeHelper.SidebarBorder,
                Foreground = ThemeHelper.ForegroundColor
            };
            btnCancel.Click += (s, args) => confirmDialog.DialogResult = false;

            var btnConfirm = new Button
            {
                Content = "Confirm",
                Width = 80,
                Height = 30,
                Background = ThemeHelper.ButtonDangerBackground,
                Foreground = ThemeHelper.ForegroundColor
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
                    countCmd.CommandText = "SELECT COUNT(*) FROM VMS_Activities";
                    countCmd.CommandTimeout = 120;
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

                    // Delete in batches to avoid timeout
                    int batchDeleted;
                    do
                    {
                        using var deleteCmd = azureConn.CreateCommand();
                        deleteCmd.CommandText = $"DELETE TOP ({batchSize}) FROM VMS_Activities";
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
        private async void MenuTestProcoreDrawings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var authService = new ProcoreAuthService();
                var apiService = new ProcoreApiService(authService);

                // Check if we need to authenticate
                var token = await authService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    // Show auth dialog
                    var authDialog = new ProcoreAuthDialog(authService);
                    authDialog.Owner = this;

                    if (authDialog.ShowDialog() != true)
                    {
                        return; // User cancelled
                    }
                }

                // Fetch companies
                var companies = await apiService.GetCompaniesAsync();
                if (companies.Count == 0)
                {
                    MessageBox.Show("No companies found.", "Procore Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var company = companies[0];

                // Fetch projects
                var projects = await apiService.GetProjectsAsync(company.Id);
                if (projects.Count == 0)
                {
                    MessageBox.Show($"No projects found for {company.Name}.", "Procore Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var project = projects[0];

                // Fetch drawings
                var drawings = await apiService.GetDrawingsAsync(project.Id);

                // Build result message
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Company: {company.Name} (ID: {company.Id})");
                sb.AppendLine($"Project: {project.DisplayName} (ID: {project.Id})");
                sb.AppendLine($"Drawings: {drawings.Count}");
                sb.AppendLine();

                foreach (var dwg in drawings.Take(10))
                {
                    sb.AppendLine($"  • {dwg.Number} - {dwg.Title} (Rev {dwg.RevisionNumber})");
                }

                if (drawings.Count > 10)
                {
                    sb.AppendLine($"  ... and {drawings.Count - 10} more");
                }

                MessageBox.Show(sb.ToString(), "Procore Test - Success", MessageBoxButton.OK, MessageBoxImage.Information);
                AppLogger.Info($"Procore test successful: {drawings.Count} drawings fetched", "MainWindow.MenuTestProcoreDrawings_Click", App.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuTestProcoreDrawings_Click");
                MessageBox.Show($"Procore test failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void MenuManageSnapshots_Click(object sender, RoutedEventArgs e)
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

            var dialog = new Dialogs.ManageSnapshotsDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Snapshots were deleted or reverted - refresh views if loaded
                if (ContentArea.Content is Views.ScheduleView scheduleView)
                {
                    var viewModel = scheduleView.DataContext as ViewModels.ScheduleViewModel;
                    if (viewModel?.SelectedWeekEndDate != null)
                    {
                        _ = viewModel.LoadScheduleDataAsync(viewModel.SelectedWeekEndDate.Value);
                    }
                }
                else if (ContentArea.Content is Views.ProgressView progressView)
                {
                    _ = progressView.RefreshData();
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
                var settings = SettingsManager.GetAllUserSettings();

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
                int imported = SettingsManager.ImportUserSettings(importFile.Settings, replaceAll);

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

        private void MenuExportLogs_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ExportLogsDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = !popupSettings.IsOpen;
        }

        private void MenuGridLayouts_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;

            var dialog = new ManageLayoutsDialog(
                getCurrentLayout: GatherCurrentLayout,
                applyLayout: ApplyLayout,
                resetToDefault: ResetGridLayoutsToDefault);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        // Gather current grid state from all views into a unified layout
        private GridLayout GatherCurrentLayout()
        {
            var layout = new GridLayout();

            // Get Progress grid preferences
            if (ContentArea.Content is ProgressView progressView)
            {
                layout.ProgressGrid = progressView.GetGridPreferences();
            }
            else
            {
                // Not currently viewing Progress - try to load from saved settings
                var json = SettingsManager.GetUserSetting( "ProgressGrid.PreferencesJson");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var prefs = System.Text.Json.JsonSerializer.Deserialize<GridPreferencesData>(json);
                        if (prefs != null) layout.ProgressGrid = prefs;
                    }
                    catch { }
                }
            }

            // Get Schedule grid preferences
            if (ContentArea.Content is ScheduleView scheduleView)
            {
                layout.ScheduleMasterGrid = scheduleView.GetMasterGridPreferences();
                layout.ScheduleDetailGrid = scheduleView.GetDetailGridPreferences();
                var heights = scheduleView.GetSplitterHeights();
                layout.ScheduleMasterHeight = heights.Master;
                layout.ScheduleDetailHeight = heights.Detail;
            }
            else
            {
                // Not currently viewing Schedule - try to load from saved settings
                var masterJson = SettingsManager.GetUserSetting( "ScheduleGrid.PreferencesJson");
                var detailJson = SettingsManager.GetUserSetting( "ScheduleDetailGrid.PreferencesJson");
                var masterHeight = SettingsManager.GetUserSetting( "ScheduleView_MasterGridHeight");
                var detailHeight = SettingsManager.GetUserSetting( "ScheduleView_DetailGridHeight");

                try
                {
                    if (!string.IsNullOrWhiteSpace(masterJson))
                    {
                        var prefs = System.Text.Json.JsonSerializer.Deserialize<GridPreferencesData>(masterJson);
                        if (prefs != null) layout.ScheduleMasterGrid = prefs;
                    }
                    if (!string.IsNullOrWhiteSpace(detailJson))
                    {
                        var prefs = System.Text.Json.JsonSerializer.Deserialize<GridPreferencesData>(detailJson);
                        if (prefs != null) layout.ScheduleDetailGrid = prefs;
                    }
                    if (double.TryParse(masterHeight, out double mh))
                        layout.ScheduleMasterHeight = mh;
                    if (double.TryParse(detailHeight, out double dh))
                        layout.ScheduleDetailHeight = dh;
                }
                catch { }
            }

            return layout;
        }

        // Apply a layout to all grids
        private void ApplyLayout(GridLayout layout)
        {
            // Apply to Progress grid
            if (ContentArea.Content is ProgressView progressView)
            {
                progressView.ApplyGridPreferences(layout.ProgressGrid);
            }

            // Apply to Schedule grid
            if (ContentArea.Content is ScheduleView scheduleView)
            {
                scheduleView.ApplyMasterGridPreferences(layout.ScheduleMasterGrid);
                scheduleView.ApplyDetailGridPreferences(layout.ScheduleDetailGrid);
                if (layout.ScheduleMasterHeight > 0 && layout.ScheduleDetailHeight > 0)
                {
                    scheduleView.ApplySplitterHeights(layout.ScheduleMasterHeight, layout.ScheduleDetailHeight);
                }
            }

            // Also save to individual settings so they persist when views reload
            SaveLayoutToIndividualSettings(layout);
        }

        // Save layout data to individual UserSettings keys for view persistence
        private void SaveLayoutToIndividualSettings(GridLayout layout)
        {
            try
            {
                if (layout.ProgressGrid?.Columns?.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(layout.ProgressGrid);
                    SettingsManager.SetUserSetting( "ProgressGrid.PreferencesJson", json, "json");
                }

                if (layout.ScheduleMasterGrid?.Columns?.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(layout.ScheduleMasterGrid);
                    SettingsManager.SetUserSetting( "ScheduleGrid.PreferencesJson", json, "json");
                }

                if (layout.ScheduleDetailGrid?.Columns?.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(layout.ScheduleDetailGrid);
                    SettingsManager.SetUserSetting( "ScheduleDetailGrid.PreferencesJson", json, "json");
                }

                if (layout.ScheduleMasterHeight > 0)
                {
                    SettingsManager.SetUserSetting( "ScheduleView_MasterGridHeight",
                        layout.ScheduleMasterHeight.ToString(), "double");
                }

                if (layout.ScheduleDetailHeight > 0)
                {
                    SettingsManager.SetUserSetting( "ScheduleView_DetailGridHeight",
                        layout.ScheduleDetailHeight.ToString(), "double");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.SaveLayoutToIndividualSettings");
            }
        }

        // Reset grids to XAML defaults without affecting saved layouts
        private void ResetGridLayoutsToDefault()
        {
            // Tell current view to skip saving on unload
            if (ContentArea.Content is ProgressView currentProgressView)
            {
                currentProgressView.SkipSaveOnClose();
            }
            else if (ContentArea.Content is ScheduleView currentScheduleView)
            {
                currentScheduleView.SkipSaveOnClose();
            }

            // Clear individual grid settings (but NOT saved layouts)
            string[] gridSettingKeys = new[]
            {
                "ProgressGrid.PreferencesJson",
                "ScheduleGrid.PreferencesJson",
                "ScheduleDetailGrid.PreferencesJson",
                "ScheduleView_MasterGridHeight",
                "ScheduleView_DetailGridHeight"
            };

            foreach (var key in gridSettingKeys)
            {
                SettingsManager.RemoveUserSetting(key);
            }

            // Reload module to recreate view with XAML defaults
            if (ContentArea.Content is ProgressView)
            {
                LoadProgressModule();
            }
            else if (ContentArea.Content is ScheduleView)
            {
                ContentArea.Content = null;
                ContentArea.Content = new ScheduleView();
            }

            AppLogger.Info("Reset grid layouts to defaults",
                "MainWindow.ResetGridLayoutsToDefault", App.CurrentUser?.Username);
        }

        private void MenuFeedbackBoard_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;

            // Check Azure connection first
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{errorMessage}\n\nFeedback Board requires an active connection.",
                    "Connection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.FeedbackDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        // Opens the Prorate MHs dialog for the currently filtered activities in Progress view
        private void MenuProrateMHs_Click(object sender, RoutedEventArgs e)
        {
            // Check if Progress view is active
            if (ContentArea.Content is not Views.ProgressView progressView)
            {
                MessageBox.Show("Prorate MHs is only available in the Progress view.",
                    "Wrong View", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get filtered activities
            var filteredActivities = progressView.GetFilteredActivities();
            if (filteredActivities.Count == 0)
            {
                MessageBox.Show("No activities to prorate. Import or filter activities first.",
                    "No Activities", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Open prorate dialog
            var dialog = new Dialogs.ProrateDialog(filteredActivities, progressView.RefreshAfterProrate);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        // Opens the Schedule Change Log dialog to view and apply detail grid changes
        private async void MenuScheduleChangeLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ScheduleChangeLogDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            // Refresh Progress view if changes were applied to Activities
            if (dialog.ChangesApplied && ContentArea.Content is Views.ProgressView progressView)
            {
                await progressView.RefreshData();
            }
        }

        // MODULE LOADING

        private void LoadProgressModule()
        {
            var progressView = new Views.ProgressView();
            ContentArea.Content = progressView;
        }

        // Check if current view has unsaved changes
        private bool CanLeaveCurrentView()
        {
            if (ContentArea.Content is ScheduleView scheduleView)
            {
                return scheduleView.TryClose();
            }
            if (ContentArea.Content is Views.WorkPackageView workPackageView)
            {
                return workPackageView.CanLeaveView();
            }
            return true;
        }
    }
}