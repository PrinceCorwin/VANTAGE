using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using VANTAGE.Models;
using VANTAGE.Services.Plugins;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class PluginManagerDialog : Window
    {
        private List<InstalledPluginInfo> _installedPlugins = new();

        public PluginManagerDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += PluginManagerDialog_Loaded;
        }

        private async void PluginManagerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync();
        }

        private void LoadInstalledPlugins()
        {
            try
            {
                _installedPlugins = PluginCatalogService.GetInstalledPlugins();
                sfInstalled.ItemsSource = _installedPlugins;
                txtNoInstalledPlugins.Visibility = _installedPlugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                btnUninstall.IsEnabled = sfInstalled.SelectedItem is InstalledPluginInfo;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginManagerDialog.LoadInstalledPlugins");
                AppMessageBox.Show(
                    $"Error loading plugins:\n\n{ex.Message}",
                    "Plugin Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadAvailablePluginsAsync()
        {
            var available = await PluginFeedService.GetAvailablePluginsAsync();

            var installedById = _installedPlugins
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First(), StringComparer.OrdinalIgnoreCase);

            var availableDisplay = available
                .Select(p =>
                {
                    installedById.TryGetValue(p.Id, out var installed);
                    return new AvailablePluginDisplay
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Version = p.Version,
                        Project = p.Project,
                        Description = p.Description,
                        PackageUrl = p.PackageUrl,
                        Sha256 = p.Sha256,
                        InstalledVersionDisplay = installed?.Version ?? "-"
                    };
                })
                .ToList();

            sfAvailable.ItemsSource = availableDisplay;
            txtNoAvailablePlugins.Visibility = availableDisplay.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            btnInstall.IsEnabled = sfAvailable.SelectedItem is AvailablePluginDisplay;
            txtStatus.Text = $"Installed: {_installedPlugins.Count} | Available: {availableDisplay.Count}";
        }

        private async Task RefreshAllAsync()
        {
            LoadInstalledPlugins();
            await LoadAvailablePluginsAsync();
        }

        private void SfInstalled_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            btnUninstall.IsEnabled = sfInstalled.SelectedItem is InstalledPluginInfo;
        }

        private void SfAvailable_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            btnInstall.IsEnabled = sfAvailable.SelectedItem is AvailablePluginDisplay;
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sfAvailable.SelectedItem is not AvailablePluginDisplay selected)
            {
                AppMessageBox.Show("Select an available plugin first.", "Plugin Manager",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var confirm = AppMessageBox.Show(
                $"Install '{selected.Name}' v{selected.Version}?",
                "Install Plugin",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                btnInstall.IsEnabled = false;
                btnRefresh.IsEnabled = false;
                btnUninstall.IsEnabled = false;

                txtStatus.Text = $"Installing {selected.Name} v{selected.Version}...";

                var result = await PluginInstallService.InstallFromFeedAsync(new PluginFeedItem
                {
                    Id = selected.Id,
                    Name = selected.Name,
                    Version = selected.Version,
                    Project = selected.Project,
                    Description = selected.Description,
                    PackageUrl = selected.PackageUrl,
                    Sha256 = selected.Sha256
                });

                var message = result.Success
                    ? $"{result.Message}\n\nRestart VANTAGE for the plugin to become active."
                    : result.Message;

                AppMessageBox.Show(
                    message,
                    result.Success ? "Install Complete" : "Install Failed",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

                await RefreshAllAsync();
            }
            finally
            {
                btnRefresh.IsEnabled = true;
                btnInstall.IsEnabled = sfAvailable.SelectedItem is AvailablePluginDisplay;
                btnUninstall.IsEnabled = sfInstalled.SelectedItem is InstalledPluginInfo;
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sfInstalled.SelectedItem is not InstalledPluginInfo selected)
            {
                AppMessageBox.Show("Select a plugin first.", "Plugin Manager",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var confirm = AppMessageBox.Show(
                $"Uninstall '{selected.Name}' v{selected.Version}?",
                "Uninstall Plugin",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                btnInstall.IsEnabled = false;
                btnRefresh.IsEnabled = false;
                btnUninstall.IsEnabled = false;
                txtStatus.Text = $"Uninstalling {selected.Name} v{selected.Version}...";

                var result = await PluginInstallService.UninstallAsync(selected);

                var uninstallMessage = result.Success
                    ? $"{result.Message}\n\nRestart VANTAGE for the plugin to be fully removed."
                    : result.Message;

                AppMessageBox.Show(
                    uninstallMessage,
                    result.Success ? "Uninstall Complete" : "Uninstall Failed",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

                await RefreshAllAsync();
            }
            finally
            {
                btnRefresh.IsEnabled = true;
                btnInstall.IsEnabled = sfAvailable.SelectedItem is AvailablePluginDisplay;
                btnUninstall.IsEnabled = sfInstalled.SelectedItem is InstalledPluginInfo;
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class AvailablePluginDisplay
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string Project { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string PackageUrl { get; set; } = string.Empty;
            public string Sha256 { get; set; } = string.Empty;
            public string InstalledVersionDisplay { get; set; } = "-";
        }
    }
}
