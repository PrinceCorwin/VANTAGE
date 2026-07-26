using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Cloud library browser for shared Project Dashboard report layouts. Lists dbo.ReportLayouts,
    // lets any user import a one-time local copy, and lets admins delete a shared layout for everyone.
    public partial class ReportLayoutImportDialog : Window
    {
        // Set when the user chooses to import; the caller downloads the JSON and saves it locally.
        public Guid? SelectedLayoutId { get; private set; }

        private sealed class Item
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public string UpdatedLocal { get; set; } = string.Empty;
        }

        public ReportLayoutImportDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            bool isAdmin = AzureDbManager.IsUserAdmin(App.CurrentUser?.Username ?? string.Empty);
            btnDelete.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var list = await AzureReportLayoutRepository.GetListAsync();
                lstLayouts.ItemsSource = list
                    .Select(c => new Item
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Author = c.Author,
                        UpdatedLocal = c.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ReportLayoutImportDialog.LoadAsync");
                AppMessageBox.Show("Could not load the Cloud layout library — see log.",
                    "Import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstLayouts_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstLayouts.SelectedItem is Item) Import();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e) => Import();

        private void Import()
        {
            if (lstLayouts.SelectedItem is not Item it)
            {
                AppMessageBox.Show("Select a layout to import first.",
                    "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedLayoutId = it.Id;
            DialogResult = true;
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lstLayouts.SelectedItem is not Item it)
            {
                AppMessageBox.Show("Select a layout to delete first.",
                    "Delete from Cloud", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = AppMessageBox.Show(
                $"Delete \"{it.Name}\" from the shared Cloud library for everyone?\n\nThis cannot be undone.",
                "Delete from Cloud", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await AzureReportLayoutRepository.DeleteAsync(it.Id);
                AppLogger.Info($"Deleted cloud report layout '{it.Name}'",
                    "ReportLayoutImportDialog.BtnDelete_Click", App.CurrentUser?.Username ?? "Unknown");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ReportLayoutImportDialog.BtnDelete_Click");
                AppMessageBox.Show("Could not delete the layout — see log.",
                    "Delete from Cloud", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
