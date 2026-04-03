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
using VANTAGE.Services.AI;
using VANTAGE.Services.Plugins;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;
using VANTAGE.Views;


namespace VANTAGE
{
    public partial class MainWindow : ChromelessWindow
    {
        private Button? _activeNavButton;
        private PluginLoaderService? _pluginLoader;

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
                HighlightNavigationButton(_activeNavButton ?? btnProgress);

                // Force taskbar icon refresh (fixes first-run icon not showing)
                var iconPath = new Uri("pack://application:,,,/images/AppIcon.ico", UriKind.Absolute);
                this.Icon = BitmapFrame.Create(iconPath);

                // Load installed plugins
                LoadPlugins();
            };
            this.Closing += MainWindow_Closing;

            // Re-apply nav button colors when theme changes
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(string themeName)
        {
            Dispatcher.Invoke(() =>
            {
                if (_activeNavButton != null)
                    HighlightNavigationButton(_activeNavButton);
            });
        }

        // Initialize and load all installed plugins
        private void LoadPlugins()
        {
            try
            {
                _pluginLoader = new PluginLoaderService(this, ToolsMenuGroup);
                _pluginLoader.LoadAllPlugins();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.LoadPlugins");
            }
        }
        // Cached view instance to avoid full data reload on every navigation
        private Views.ProgressView? _cachedProgressView;

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
                $"Vantage: Milestone\n\n" +
                $"Version: {version?.Major}.{version?.Minor}.{version?.Build}\n\n" +
                $"Construction Project Management System\n\n" +
                $"© {DateTime.Now.Year} Summit Industrial",
                "About Vantage: Milestone",
                MessageBoxButton.OK,
                MessageBoxImage.None);
        }

        private void MenuReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
            var dialog = new ReleaseNotesDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        // Show release notes automatically if the app version changed since last launch
        public void ShowReleaseNotesIfVersionChanged()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                string currentVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";
                string lastSeenVersion = SettingsManager.GetUserSetting("LastSeenVersion");

                if (lastSeenVersion != currentVersion)
                {
                    SettingsManager.SetUserSetting("LastSeenVersion", currentVersion);

                    // Don't show on very first launch (no previous version to compare)
                    if (!string.IsNullOrEmpty(lastSeenVersion))
                    {
                        var dialog = new ReleaseNotesDialog();
                        dialog.Owner = this;
                        dialog.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.ShowReleaseNotesIfVersionChanged");
            }
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

            // Shutdown plugins
            _pluginLoader?.ShutdownAllPlugins();

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
            txtAppVersion.Text = $"VANTAGE: Milestone v{version?.Major}.{version?.Minor}.{version?.Build}";

            // Hide ADMIN button if not admin (checked against Azure)
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                btnAdmin.Visibility = Visibility.Collapsed;
            }

            // Hide TAKEOFFS button unless user is allowed
            // TEMPORARY: Restricted to specific users during development
            // TO REVERT: Replace IsTakeoffAllowed() call with: App.CurrentUser.IsEstimator
            if (App.CurrentUser == null || !IsTakeoffAllowed())
            {
                btnTakeoff.Visibility = Visibility.Collapsed;
                menuImportTakeoff.Visibility = Visibility.Collapsed;
            }

            // Restore last visited view (default to Progress)
            string lastView = SettingsManager.GetUserSetting("LastView", "Progress");
            switch (lastView)
            {
                case "Schedule":
                    BtnSchedule_Click(this, new RoutedEventArgs());
                    break;
                case "ProgBooks":
                    BtnPbook_Click(this, new RoutedEventArgs());
                    break;
                case "WorkPkgs":
                    BtnWorkPackage_Click(this, new RoutedEventArgs());
                    break;
                case "Analysis":
                    BtnAnalysis_Click(this, new RoutedEventArgs());
                    break;
                case "Takeoffs":
                    if (IsTakeoffAllowed())
                        BtnTakeoff_Click(this, new RoutedEventArgs());
                    else
                        LoadProgressModule();
                    break;
                default:
                    LoadProgressModule();
                    break;
            }
        }

        // TEMPORARY: Restrict Takeoff access to specific users during development
        // TO REVERT: Delete this method and replace IsTakeoffAllowed() calls with App.CurrentUser.IsEstimator
        private bool IsTakeoffAllowed()
        {
            if (App.CurrentUser == null) return false;
            var username = App.CurrentUser.Username;
            return username.Equals("steve", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("Steve.Amalfitano", StringComparison.OrdinalIgnoreCase);
        }

        // TOOLBAR BUTTON HANDLERS
        private void HighlightNavigationButton(Button activeButton)
        {
            _activeNavButton = activeButton;

            // Save last visited view
            string viewName = activeButton == btnProgress ? "Progress"
                : activeButton == btnSchedule ? "Schedule"
                : activeButton == btnPbook ? "ProgBooks"
                : activeButton == btnWorkPackage ? "WorkPkgs"
                : activeButton == btnAnalysis ? "Analysis"
                : activeButton == btnTakeoff ? "Takeoffs"
                : "Progress";
            SettingsManager.SetUserSetting("LastView", viewName);

            // Reset all navigation buttons to toolbar foreground
            borderProgress.Background = System.Windows.Media.Brushes.Transparent;
            btnProgress.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

            borderSchedule.Background = System.Windows.Media.Brushes.Transparent;
            btnSchedule.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

            ProgBookBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnPbook.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

            WorkPackageBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnWorkPackage.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

            AnalysisBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnAnalysis.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

            TakeoffBorder.Background = System.Windows.Media.Brushes.Transparent;
            btnTakeoff.Foreground = (System.Windows.Media.Brush)FindResource("ToolbarForeground");

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
            else if (activeButton == btnAnalysis)
            {
                AnalysisBorder.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnAnalysis.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
            }
            else if (activeButton == btnTakeoff)
            {
                TakeoffBorder.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                btnTakeoff.Foreground = (System.Windows.Media.Brush)FindResource("AccentColor");
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

        private void BtnAnalysis_Click(object sender, RoutedEventArgs e)
        {
            // Already on Analysis? No need to check
            if (ContentArea.Content is Views.AnalysisView)
            {
                HighlightNavigationButton(btnAnalysis);
                return;
            }

            if (!CanLeaveCurrentView())
                return;

            HighlightNavigationButton(btnAnalysis);
            ContentArea.Content = null;
            var analysisView = new Views.AnalysisView();
            ContentArea.Content = analysisView;
        }

        private void BtnTakeoff_Click(object sender, RoutedEventArgs e)
        {
            if (ContentArea.Content is Views.TakeoffView)
            {
                HighlightNavigationButton(btnTakeoff);
                return;
            }

            if (!CanLeaveCurrentView())
                return;

            HighlightNavigationButton(btnTakeoff);
            ContentArea.Content = null;
            var takeoffView = new Views.TakeoffView();
            ContentArea.Content = takeoffView;
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!AzureDbManager.CheckConnection(out string error))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{error}\n\nThis feature requires an active connection.",
                    "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.ImportTakeoffDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
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
                        MessageBoxImage.None);
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
                        MessageBoxImage.None);
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
                        MessageBoxImage.None);

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
                        MessageBoxImage.None);
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
                            MessageBoxImage.None
                        );

                        // Refresh the view if we're on Progress module
                        if (ContentArea.Content is Views.ProgressView)
                        {
                            LoadProgressModule(forceReload: true);
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
                        MessageBoxImage.None
                    );

                    // Refresh the view if we're on Progress module
                    if (ContentArea.Content is Views.ProgressView)
                    {
                        LoadProgressModule(forceReload: true);
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
                    MessageBox.Show("No activities to export.", "Export", MessageBoxButton.OK, MessageBoxImage.None);
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
                    MessageBox.Show("No activities to export.", "Export", MessageBoxButton.OK, MessageBoxImage.None);
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

        // === ADMIN DROPDOWN ===

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

        // Open Manage S3 Drawings dialog (for orphaned drawings cleanup)
        private async void MenuManageS3Drawings_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var service = new TakeoffService();
                var configs = await service.ListConfigsAsync();

                if (configs.Count == 0)
                {
                    MessageBox.Show("No takeoff configs found in S3.", "No Configs",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new ManageDrawingsDialog(configs);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "MainWindow.MenuManageS3Drawings_Click");
                MessageBox.Show($"Error loading configs: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuProjectRates_Click(object sender, RoutedEventArgs e)
        {
            if (!AzureDbManager.CheckConnection(out string prError))
            {
                MessageBox.Show(
                    $"Cannot connect to Azure database:\n\n{prError}\n\nThis feature requires an active connection.",
                    "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Dialogs.ManageProjectRatesDialog();
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

                // Clear the grid if ProgressView is loaded, otherwise invalidate cache
                if (ContentArea.Content is Views.ProgressView progressView)
                {
                    await progressView.RefreshData();
                }
                else
                {
                    // Invalidate cached view so next navigation creates fresh instance
                    _cachedProgressView = null;
                }

                MessageBox.Show(
                    $"Successfully deleted {deletedCount:N0} activities from local database.\n\n" +
                    "LastPulledSyncVersion has been reset.\n" +
                    "Sync to restore data from Azure.",
                    "Clear Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
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
                    MessageBoxImage.None);
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
                        MessageBoxButton.OK, MessageBoxImage.None);
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
                        "Export Settings", MessageBoxButton.OK, MessageBoxImage.None);
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
                    MessageBoxImage.None);
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

        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
            var dialog = new ThemeManagerDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void MenuPluginManager_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
            var dialog = new Dialogs.PluginManagerDialog();
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
                LoadProgressModule(forceReload: true);
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
                    "Wrong View", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            // Get filtered activities
            var filteredActivities = progressView.GetFilteredActivities();
            if (filteredActivities.Count == 0)
            {
                MessageBox.Show("No activities to prorate. Import or filter activities first.",
                    "No Activities", MessageBoxButton.OK, MessageBoxImage.None);
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

        // Opens the Schedule UDF Mapping dialog to configure custom P6 column mappings
        private void MenuScheduleUDFMapping_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ScheduleUDFMappingDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                // Refresh ScheduleView column headers if loaded
                if (ContentArea.Content is Views.ScheduleView scheduleView)
                {
                    scheduleView.UpdateUDFColumnHeaders();
                }
            }
        }

        private void MenuROCManager_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ManageROCRatesDialog { Owner = this };
            dialog.ShowDialog();
        }

        // MODULE LOADING

        // Loads the Progress module, reusing the cached instance unless forceReload is true
        private void LoadProgressModule(bool forceReload = false)
        {
            if (forceReload || _cachedProgressView == null)
            {
                _cachedProgressView = new Views.ProgressView();
            }
            ContentArea.Content = _cachedProgressView;
        }

        // Called by ScheduleView when save updates Activities (e.g., PlanStart/PlanFin)
        public async Task NotifyActivitiesModifiedAsync()
        {
            if (_cachedProgressView != null)
            {
                await _cachedProgressView.RefreshData();
            }
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
