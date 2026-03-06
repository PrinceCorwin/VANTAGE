using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.Plugins
{
    public static class PluginInstallService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        public static async Task<(bool Success, string Message)> InstallFromFeedAsync(PluginFeedItem feedItem)
        {
            if (string.IsNullOrWhiteSpace(feedItem.Id) ||
                string.IsNullOrWhiteSpace(feedItem.Version) ||
                string.IsNullOrWhiteSpace(feedItem.PackageUrl))
            {
                return (false, "Plugin feed entry is missing required fields.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "VANTAGE_PluginInstall", Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempDir, $"{feedItem.Id}.{feedItem.Version}.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                using (var response = await _httpClient.GetAsync(feedItem.PackageUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    AppLogger.Info(
                        $"Downloading plugin package from {feedItem.PackageUrl}",
                        "PluginInstallService.InstallFromFeedAsync",
                        App.CurrentUser?.Username);
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await stream.CopyToAsync(fs);
                }

                if (!string.IsNullOrWhiteSpace(feedItem.Sha256))
                {
                    if (!VerifyFileHash(zipPath, feedItem.Sha256))
                    {
                        return (false, "Download verification failed (SHA-256 mismatch).");
                    }
                }

                return await InstallFromZipPayloadAsync(zipPath, feedItem);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginInstallService.InstallFromFeedAsync", App.CurrentUser?.Username);
                return (false, $"Install failed for URL '{feedItem.PackageUrl}': {ex.Message}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // cleanup best effort
                }
            }
        }

        private static async Task<(bool Success, string Message)> InstallFromZipPayloadAsync(string zipPath, PluginFeedItem? expectedFeedItem)
        {
            string pluginsRoot = PluginCatalogService.GetPluginsRootPath();
            Directory.CreateDirectory(pluginsRoot);

            string tempDir = Path.Combine(Path.GetTempPath(), "VANTAGE_PluginExtract", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string extractDir = Path.Combine(tempDir, "extract");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                string[] manifestCandidates = Directory.GetFiles(extractDir, "plugin.json", SearchOption.AllDirectories);
                if (manifestCandidates.Length == 0)
                {
                    return (false, "Package is missing plugin.json.");
                }

                string manifestPath = manifestCandidates[0];
                string packageRoot = Path.GetDirectoryName(manifestPath) ?? extractDir;

                string manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Version))
                {
                    return (false, "plugin.json is invalid or missing required 'id'/'version'.");
                }

                if (expectedFeedItem != null)
                {
                    if (!string.Equals(manifest.Id, expectedFeedItem.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"plugin.json id '{manifest.Id}' does not match feed id '{expectedFeedItem.Id}'.");
                    }

                    if (!string.Equals(manifest.Version, expectedFeedItem.Version, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"plugin.json version '{manifest.Version}' does not match feed version '{expectedFeedItem.Version}'.");
                    }
                }

                string targetDir = Path.Combine(pluginsRoot, manifest.Id, manifest.Version);

                if (Directory.Exists(targetDir))
                {
                    string[] existingManifests = Directory.GetFiles(targetDir, "plugin.json", SearchOption.AllDirectories);
                    if (existingManifests.Length == 0)
                    {
                        Directory.Delete(targetDir, recursive: true);
                    }
                    else
                    {
                        return (false, $"Plugin '{manifest.Name}' v{manifest.Version} is already installed.");
                    }
                }

                Directory.CreateDirectory(targetDir);
                CopyDirectory(packageRoot, targetDir);

                AppLogger.Info(
                    $"Installed plugin {manifest.Id} v{manifest.Version}",
                    "PluginInstallService.InstallFromZipPayloadAsync",
                    App.CurrentUser?.Username);

                return (true, $"Installed '{manifest.Name}' v{manifest.Version}.");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // cleanup best effort
                }
            }
        }

        public static Task<(bool Success, string Message)> UninstallAsync(InstalledPluginInfo installedPlugin)
        {
            if (installedPlugin == null)
            {
                return Task.FromResult((false, "No plugin selected."));
            }

            try
            {
                string pluginDirectory = installedPlugin.PluginDirectory;
                if (string.IsNullOrWhiteSpace(pluginDirectory) || !Directory.Exists(pluginDirectory))
                {
                    return Task.FromResult((false, "Installed plugin folder was not found."));
                }

                Directory.Delete(pluginDirectory, recursive: true);

                // Clean up the parent <plugin-id> folder if no versions remain
                string? pluginIdDir = Path.GetDirectoryName(pluginDirectory);
                if (!string.IsNullOrWhiteSpace(pluginIdDir) && Directory.Exists(pluginIdDir))
                {
                    if (Directory.GetFileSystemEntries(pluginIdDir).Length == 0)
                    {
                        Directory.Delete(pluginIdDir, recursive: false);
                    }
                }

                AppLogger.Info(
                    $"Uninstalled plugin {installedPlugin.Id} v{installedPlugin.Version}",
                    "PluginInstallService.UninstallAsync",
                    App.CurrentUser?.Username);

                return Task.FromResult((true, $"Uninstalled '{installedPlugin.Name}' v{installedPlugin.Version}."));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginInstallService.UninstallAsync", App.CurrentUser?.Username);
                return Task.FromResult((false, $"Uninstall failed: {ex.Message}"));
            }
        }

        private static bool VerifyFileHash(string filePath, string expectedHash)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);
            string actualHash = Convert.ToHexString(hashBytes);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, directory);
                Directory.CreateDirectory(Path.Combine(destDir, relative));
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, overwrite: true);
            }
        }
    }
}
