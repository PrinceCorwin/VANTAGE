using System.Windows;
using System.Windows.Controls;
using VANTAGE.Utilities;
using VANTAGE.Views;

namespace VANTAGE
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
        
            InitializeComponent();
            
            LoadInitialModule();
            
            UpdateStatusBar();
            

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("⚠ MainWindow is closing!");
        }

        private void LoadInitialModule()
        {
            // Load PROGRESS module by default
            LoadProgressModule();

            // Disable ADMIN button if not admin (with null check)
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                btnAdmin.IsEnabled = false;
                btnAdmin.Opacity = 0.5;
                btnAdmin.ToolTip = "Admin privileges required";
            }
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

            // TODO: Load projects into dropdown
            // TODO: Update last sync time
            // TODO: Update record count
        }

        // TOOLBAR BUTTON HANDLERS

        private void BtnProgress_Click(object sender, RoutedEventArgs e)
        {
            LoadProgressModule();
            HighlightActiveButton(btnProgress);
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("SCHEDULE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("CREATE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === EXCEL DROPDOWN ===

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown menu
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuExcelImportReplace_Click(object sender, RoutedEventArgs e)
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
                    System.Diagnostics.Debug.WriteLine($"→ Selected file: {openFileDialog.FileName}");

                    // Confirm replace action
                    var result = MessageBox.Show(
                        "This will REPLACE all existing activities with data from the Excel file.\n\nAre you sure you want to continue?",
                        "Confirm Replace",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Debug.WriteLine("→ Starting import...");

                        // Import with replace mode
                        int imported = ExcelImporter.ImportActivities(openFileDialog.FileName, replaceMode: true);

                        System.Diagnostics.Debug.WriteLine($"→ Import returned: {imported} records");

                        MessageBox.Show(
                            $"Successfully imported {imported} activities.\n\nAll previous data has been replaced.",
                            "Import Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );

                        // Refresh the view if we're on Progress module
                        if (ContentArea.Content is Views.ProgressView)
                        {
                            LoadProgressModule(); // Reload to show new data
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ IMPORT ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                MessageBox.Show(
                    $"Error importing Excel file:\n\n{ex.Message}\n\nCheck Output window for details.",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void MenuExcelImportCombine_Click(object sender, RoutedEventArgs e)
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
                    // Import with combine mode
                    int imported = ExcelImporter.ImportActivities(openFileDialog.FileName, replaceMode: false);

                    MessageBox.Show(
                        $"Successfully imported {imported} new activities.\n\nExisting activities were preserved (duplicates skipped).",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // Refresh the view if we're on Progress module
                    if (ContentArea.Content is Views.ProgressView)
                    {
                        LoadProgressModule(); // Reload to show new data
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error importing Excel file:\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void MenuExcelExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Excel Export coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPbook_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PRINT module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnWorkPackage_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WORK PACKAGE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === REPORTS DROPDOWN ===

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown menu
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
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

        // === ANALYSIS DROPDOWN ===

        private void BtnAnalysis_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown menu
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
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

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Open the dropdown menu
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null) return;

            // Toggle admin status (in memory only for this session)
            App.CurrentUser.IsAdmin = !App.CurrentUser.IsAdmin;

            string status = App.CurrentUser.IsAdmin ? "ADMIN" : "Regular User";
            MessageBox.Show($"Admin status toggled!\n\nCurrent status: {status}\n\n(This is temporary - restarting the app will restore your real status)",
                "Admin Toggle",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            System.Diagnostics.Debug.WriteLine($"✓ Admin status toggled to: {App.CurrentUser.IsAdmin}");
        }

        private void MenuAdmin2_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Admin 2 coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // === TOOLS DROPDOWN ===

        private void BtnTools_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown menu
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
            // Load the actual ProgressView
            var progressView = new Views.ProgressView();
            ContentArea.Content = progressView;

            // Save last module used
            SettingsManager.SetLastModuleUsed(App.CurrentUserID, "PROGRESS");
        }

        private void HighlightActiveButton(System.Windows.Controls.Button activeButton)
        {
            // Reset all buttons to default
            btnProgress.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnSchedule.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnCreate.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnExcel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnPbook.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnWorkPackage.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnReports.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnAnalysis.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnAdmin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnTools.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));

            // Highlight active button
            activeButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)); // Accent blue
        }
    }
}