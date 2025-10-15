using System.Windows;
using VANTAGE.Utilities;

namespace VANTAGE
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            System.Diagnostics.Debug.WriteLine("→ MainWindow constructor starting...");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("→ MainWindow InitializeComponent complete");
            LoadInitialModule();
            System.Diagnostics.Debug.WriteLine("→ MainWindow LoadInitialModule complete");
            UpdateStatusBar();
            System.Diagnostics.Debug.WriteLine("→ MainWindow UpdateStatusBar complete");
            System.Diagnostics.Debug.WriteLine("→ MainWindow constructor finished");

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

        private void BtnExcelImport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("EXCEL IMPORT module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPbook_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PRINT module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnWorkPackage_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WORK PACKAGE module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("REPORTS module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAnalysis_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ANALYSIS module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!App.CurrentUser.IsAdmin)
            {
                MessageBox.Show("You do not have admin privileges.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("ADMIN module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnTools_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TOOLS module coming soon!", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // MODULE LOADING

        private void LoadProgressModule()
        {
            // For now, show placeholder
            var placeholder = new System.Windows.Controls.TextBlock
            {
                Text = "PROGRESS MODULE\n\nDataGrid will go here",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentArea.Content = placeholder;

            // Save last module used
            SettingsManager.SetLastModuleUsed(App.CurrentUserID, "PROGRESS");
        }

        private void HighlightActiveButton(System.Windows.Controls.Button activeButton)
        {
            // Reset all buttons to default
            btnProgress.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnSchedule.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnCreate.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
            btnExcelImport.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
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