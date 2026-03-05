using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.Plugins
{
    public class PluginAutoUpdateSummary
    {
        public int InstalledPluginCount { get; set; }
        public int CheckedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public static class PluginAutoUpdateService
    {
        public static async Task<PluginAutoUpdateSummary> CheckAndUpdateInstalledPluginsAsync(Action<string>? updateStatus = null)
        {
            var summary = new PluginAutoUpdateSummary();

            try
            {
                var installed = PluginCatalogService.GetInstalledPlugins();
                summary.InstalledPluginCount = installed.Count;
                if (installed.Count == 0)
                {
                    return summary;
                }

                var available = await PluginFeedService.GetAvailablePluginsAsync();
                if (available.Count == 0)
                {
                    return summary;
                }

                var installedLatestById = installed
                    .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(p => ParseVersionOrDefault(p.Version)).First())
                    .ToList();

                var availableLatestById = available
                    .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(p => ParseVersionOrDefault(p.Version)).First())
                    .ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var installedPlugin in installedLatestById)
                {
                    summary.CheckedCount++;

                    if (!availableLatestById.TryGetValue(installedPlugin.Id, out var feedPlugin))
                    {
                        continue;
                    }

                    var installedVersion = ParseVersionOrDefault(installedPlugin.Version);
                    var feedVersion = ParseVersionOrDefault(feedPlugin.Version);
                    if (feedVersion <= installedVersion)
                    {
                        continue;
                    }

                    updateStatus?.Invoke($"Updating plugin '{installedPlugin.Name}' to v{feedPlugin.Version}...");

                    var result = await PluginInstallService.InstallFromFeedAsync(feedPlugin);
                    if (!result.Success)
                    {
                        summary.FailedCount++;
                        AppLogger.Warning(
                            $"Plugin auto-update failed for {installedPlugin.Id}: {result.Message}",
                            "PluginAutoUpdateService.CheckAndUpdateInstalledPluginsAsync",
                            App.CurrentUser?.Username);
                        continue;
                    }

                    summary.UpdatedCount++;
                    AppLogger.Info(
                        $"Plugin auto-updated: {installedPlugin.Id} {installedPlugin.Version} -> {feedPlugin.Version}",
                        "PluginAutoUpdateService.CheckAndUpdateInstalledPluginsAsync",
                        App.CurrentUser?.Username);

                    // Remove older versions of the same plugin after successful update
                    var currentInstalled = PluginCatalogService.GetInstalledPlugins()
                        .Where(p => string.Equals(p.Id, installedPlugin.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var oldVersion in currentInstalled.Where(p => !string.Equals(p.Version, feedPlugin.Version, StringComparison.OrdinalIgnoreCase)))
                    {
                        await PluginInstallService.UninstallAsync(oldVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginAutoUpdateService.CheckAndUpdateInstalledPluginsAsync", App.CurrentUser?.Username);
            }

            return summary;
        }

        private static Version ParseVersionOrDefault(string? versionText)
        {
            if (Version.TryParse(versionText, out var parsed))
            {
                return parsed;
            }

            return new Version(0, 0, 0);
        }
    }
}
