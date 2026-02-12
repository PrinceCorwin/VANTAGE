using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Multi-page uninstall wizard: Confirmation -> Progress -> Complete
    public partial class UninstallDialog : Window
    {
        public bool KeepData { get; private set; }
        public bool UninstallCompleted { get; private set; }

        private readonly SolidColorBrush _completedBrush;

        public UninstallDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _completedBrush = (SolidColorBrush)FindResource("StatusGreen");
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            KeepData = chkKeepData.IsChecked == true;

            // Show step 4 if removing data
            if (!KeepData)
                txtStep4.Visibility = Visibility.Visible;

            // Switch to progress page
            pageConfirm.Visibility = Visibility.Collapsed;
            pageProgress.Visibility = Visibility.Visible;

            await RunUninstallStepsAsync();

            // Switch to completion page
            pageProgress.Visibility = Visibility.Collapsed;
            pageComplete.Visibility = Visibility.Visible;

            txtDataKept.Text = KeepData
                ? "Your local database and settings were preserved. They will be available if you reinstall."
                : "All local data has been removed.";

            UninstallCompleted = true;
        }

        private async Task RunUninstallStepsAsync()
        {
            // Step 1: Remove shortcuts
            await Task.Delay(300);
            UninstallService.RemoveShortcuts();
            MarkStepComplete(txtStep1, "Shortcuts removed");

            // Step 2: Remove registry
            await Task.Delay(300);
            UninstallService.RemoveRegistryEntries();
            MarkStepComplete(txtStep2, "Registry entries removed");

            // Step 3: Prepare file cleanup (batch script handles actual deletion after exit)
            await Task.Delay(300);
            MarkStepComplete(txtStep3, "Application files queued for removal");

            // Step 4: Delete local data (if opted in)
            if (!KeepData)
            {
                await Task.Delay(300);
                string vantageDataDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VANTAGE");
                UninstallService.DeleteLocalData(vantageDataDir);
                MarkStepComplete(txtStep4, "Local data removed");
            }

            progressBar.IsIndeterminate = false;
            progressBar.Value = 100;
        }

        // Update step text with checkmark and green color
        private void MarkStepComplete(System.Windows.Controls.TextBlock step, string text)
        {
            step.Text = $"  \u2714  {text}";
            step.Foreground = _completedBrush;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
