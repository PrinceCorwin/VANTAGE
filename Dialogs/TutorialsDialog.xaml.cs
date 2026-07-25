using System;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Services;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Lists the available tutorial videos (from the S3 tutorials.json manifest).
    // Clicking a row opens the in-app player for that video. The list is data-
    // driven, so new videos appear here as soon as the manifest is updated — no
    // app release required.
    public partial class TutorialsDialog : Window
    {
        public TutorialsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += TutorialsDialog_Loaded;
        }

        private async void TutorialsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTutorialsAsync();
        }

        private async Task LoadTutorialsAsync()
        {
            try
            {
                var items = await TutorialService.GetTutorialsAsync();
                if (items.Count == 0)
                {
                    ShowStatus("No tutorials are available yet.");
                    return;
                }

                listTutorials.ItemsSource = items;
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialsDialog.LoadTutorialsAsync");
                ShowStatus("Could not load tutorials — see log.");
            }
        }

        // Keep the overlay visible to carry an empty/error message.
        private void ShowStatus(string message)
        {
            busyIndicator.IsBusy = false;
            txtStatus.Text = message;
            loadingOverlay.Visibility = Visibility.Visible;
        }

        private void TutorialItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TutorialItem item)
            {
                OpenPlayer(item);
            }
        }

        private void OpenPlayer(TutorialItem item)
        {
            try
            {
                string url = TutorialService.GetTutorialUrl(item.Key);
                var player = new TutorialPlayerWindow(url, item.Key)
                {
                    // Own the player to the main window so it survives closing this picker.
                    Owner = Application.Current.MainWindow
                };
                player.Show();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialsDialog.OpenPlayer");
                AppMessageBox.Show("Could not open the tutorial video — see log.",
                    "Tutorials", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
