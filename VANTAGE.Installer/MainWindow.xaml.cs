using System.Windows;

namespace VANTAGE.Installer
{
    public partial class MainWindow : Window
    {
        private readonly InstallerService _installer = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void InstallMilestone_Click(object sender, RoutedEventArgs e)
        {
            btnMilestone.IsEnabled = false;
            progressPanel.Visibility = Visibility.Visible;

            var progress = new Progress<(double percent, string message)>(update =>
            {
                progressBar.Value = update.percent;
                statusText.Text = update.message;
            });

            bool success = await _installer.InstallAsync(progress);

            if (success)
            {
                var result = MessageBox.Show(
                    "VANTAGE: Milestone has been installed successfully!\n" +
                    "A shortcut has been created on your desktop.\n\n" +
                    "Would you like to launch it now?",
                    "Installation Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.None);

                if (result == MessageBoxResult.Yes)
                {
                    _installer.LaunchApp();
                }

                Application.Current.Shutdown();
            }
            else
            {
                btnMilestone.IsEnabled = true;
            }
        }
    }
}
