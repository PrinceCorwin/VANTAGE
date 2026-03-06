using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.Windows.Tools.Controls;
using VANTAGE.Utilities;
using VANTAGE.Views;

namespace VANTAGE.Services.Plugins
{
    // Loads and manages plugin instances at runtime
    public class PluginLoaderService : IPluginHost
    {
        private readonly Window _mainWindow;
        private readonly DropDownMenuGroup _toolsMenuGroup;
        private readonly List<IVantagePlugin> _loadedPlugins = new();
        private readonly List<UIElement> _addedMenuItems = new();

        public PluginLoaderService(Window mainWindow, DropDownMenuGroup toolsMenuGroup)
        {
            _mainWindow = mainWindow;
            _toolsMenuGroup = toolsMenuGroup;
        }

        // Load all installed plugins
        public void LoadAllPlugins()
        {
            var installedPlugins = PluginCatalogService.GetInstalledPlugins();

            foreach (var pluginInfo in installedPlugins)
            {
                try
                {
                    LoadPlugin(pluginInfo);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"PluginLoaderService.LoadAllPlugins ({pluginInfo.Id})");
                }
            }

            AppLogger.Info($"Loaded {_loadedPlugins.Count} plugin(s)", "PluginLoaderService.LoadAllPlugins");
        }

        private void LoadPlugin(InstalledPluginInfo pluginInfo)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(pluginInfo.AssemblyFile))
            {
                AppLogger.Info($"Plugin {pluginInfo.Id} has no assembly file, skipping", "PluginLoaderService.LoadPlugin");
                return;
            }

            if (string.IsNullOrWhiteSpace(pluginInfo.EntryType))
            {
                AppLogger.Info($"Plugin {pluginInfo.Id} has no entry type, skipping", "PluginLoaderService.LoadPlugin");
                return;
            }

            // Build assembly path
            var assemblyPath = Path.Combine(pluginInfo.PluginDirectory, pluginInfo.AssemblyFile);
            if (!File.Exists(assemblyPath))
            {
                AppLogger.Error(new FileNotFoundException($"Plugin assembly not found: {assemblyPath}"),
                    "PluginLoaderService.LoadPlugin");
                return;
            }

            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Find the entry type
            var entryType = assembly.GetType(pluginInfo.EntryType);
            if (entryType == null)
            {
                AppLogger.Error(new TypeLoadException($"Entry type not found: {pluginInfo.EntryType}"),
                    "PluginLoaderService.LoadPlugin");
                return;
            }

            // Verify it implements IVantagePlugin
            if (!typeof(IVantagePlugin).IsAssignableFrom(entryType))
            {
                AppLogger.Error(new InvalidOperationException($"Entry type {pluginInfo.EntryType} does not implement IVantagePlugin"),
                    "PluginLoaderService.LoadPlugin");
                return;
            }

            // Create instance
            var plugin = (IVantagePlugin?)Activator.CreateInstance(entryType);
            if (plugin == null)
            {
                AppLogger.Error(new InvalidOperationException($"Failed to create instance of {pluginInfo.EntryType}"),
                    "PluginLoaderService.LoadPlugin");
                return;
            }

            // Initialize the plugin
            plugin.Initialize(this);
            _loadedPlugins.Add(plugin);

            AppLogger.Info($"Loaded plugin: {plugin.Name} ({plugin.Id})", "PluginLoaderService.LoadPlugin");
        }

        // Shutdown all loaded plugins
        public void ShutdownAllPlugins()
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Shutdown();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"PluginLoaderService.ShutdownAllPlugins ({plugin.Id})");
                }
            }

            // Remove added menu items
            foreach (var item in _addedMenuItems)
            {
                _toolsMenuGroup.Items.Remove(item);
            }

            _loadedPlugins.Clear();
            _addedMenuItems.Clear();
        }

        // IPluginHost implementation

        public Window MainWindow => _mainWindow;

        public string CurrentUsername => App.CurrentUser?.Username ?? "Unknown";

        public void AddToolsMenuItem(string header, Action onClick, bool addSeparatorBefore = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (addSeparatorBefore)
                {
                    var separator = new Separator
                    {
                        Background = (System.Windows.Media.Brush)Application.Current.Resources["BorderColor"],
                        Margin = new Thickness(5, 2, 5, 2)
                    };
                    _toolsMenuGroup.Items.Add(separator);
                    _addedMenuItems.Add(separator);
                }

                var menuItem = new DropDownMenuItem
                {
                    Header = header
                };
                menuItem.Click += (s, e) => onClick();

                _toolsMenuGroup.Items.Add(menuItem);
                _addedMenuItems.Add(menuItem);
            });
        }

        public void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void LogInfo(string message, string source)
        {
            AppLogger.Info(message, source);
        }

        public void LogError(Exception ex, string source)
        {
            AppLogger.Error(ex, source);
        }

        // Refresh Progress view data, summary stats, and metadata error count
        public async Task RefreshProgressViewAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (_mainWindow is MainWindow mw)
                {
                    var contentArea = mw.FindName("ContentArea") as ContentControl;
                    if (contentArea?.Content is ProgressView progressView)
                    {
                        await progressView.RefreshData();
                        await progressView.CalculateMetadataErrorCount();
                    }
                }
            });
        }
    }
}
