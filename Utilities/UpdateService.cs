using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Checks for app updates from a remote manifest and orchestrates the update process
    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Check for updates and initiate install if available.
        // Returns true if an update was initiated (caller should shut down).
        // Returns false if no update needed or check failed (caller continues normally).
        public static async Task<bool> CheckAndApplyUpdateAsync(
            Action<string> updateStatus,
            Action shutdownApp)
        {
            try
            {
                // Download and parse manifest
                updateStatus("Checking for updates...");
                var manifest = await GetManifestAsync();
                if (manifest == null)
                {
                    AppLogger.Info("Update check: could not retrieve manifest, skipping", "UpdateService.CheckAndApplyUpdateAsync");
                    return false;
                }

                // Compare versions
                var localVersion = GetCurrentVersion();
                if (!Version.TryParse(manifest.CurrentVersion, out var remoteVersion))
                {
                    AppLogger.Warning($"Update check: invalid version in manifest: {manifest.CurrentVersion}", "UpdateService.CheckAndApplyUpdateAsync");
                    return false;
                }

                if (localVersion.Major == remoteVersion.Major &&
                    localVersion.Minor == remoteVersion.Minor &&
                    localVersion.Build == remoteVersion.Build)
                {
                    AppLogger.Info($"App is up to date (v{localVersion.Major}.{localVersion.Minor}.{localVersion.Build})", "UpdateService.CheckAndApplyUpdateAsync");
                    return false;
                }

                AppLogger.Info($"Update available: local v{localVersion.Major}.{localVersion.Minor}.{localVersion.Build} -> remote v{remoteVersion.Major}.{remoteVersion.Minor}.{remoteVersion.Build}", "UpdateService.CheckAndApplyUpdateAsync");
                updateStatus($"Update available: v{remoteVersion.Major}.{remoteVersion.Minor}.{remoteVersion.Build}");
                await Task.Delay(800);

                // Download the ZIP
                string? zipPath = await DownloadUpdateAsync(manifest, updateStatus);
                if (zipPath == null)
                {
                    AppLogger.Warning("Update check: download failed, skipping", "UpdateService.CheckAndApplyUpdateAsync");
                    return false;
                }

                // Verify hash
                updateStatus("Verifying download...");
                if (!string.IsNullOrEmpty(manifest.Sha256) && !VerifyFileHash(zipPath, manifest.Sha256))
                {
                    AppLogger.Error("Update check: SHA-256 hash mismatch, aborting update", "UpdateService.CheckAndApplyUpdateAsync");
                    TryDelete(zipPath);
                    return false;
                }

                // Extract updater to temp directory and launch it
                updateStatus("Installing update...");
                string? updaterPath = ExtractUpdaterToTemp(zipPath);
                if (updaterPath == null)
                {
                    AppLogger.Error("Update check: could not extract updater from ZIP", "UpdateService.CheckAndApplyUpdateAsync");
                    TryDelete(zipPath);
                    return false;
                }

                // Launch the updater
                int currentPid = Environment.ProcessId;
                string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"--pid {currentPid} --zip \"{zipPath}\" --target \"{installDir}\" --exe \"VANTAGE.exe\"",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                AppLogger.Info($"Updater launched, shutting down for update to v{manifest.CurrentVersion}", "UpdateService.CheckAndApplyUpdateAsync");

                // Shut down the app so the updater can replace files
                shutdownApp();
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "UpdateService.CheckAndApplyUpdateAsync");
                return false;
            }
        }

        // Download manifest.json from the update server
        private static async Task<UpdateManifest?> GetManifestAsync()
        {
            try
            {
                string manifestUrl = $"{CredentialService.UpdateBaseUrl}/manifest.json";
                string json = await _httpClient.GetStringAsync(manifestUrl);
                return JsonSerializer.Deserialize<UpdateManifest>(json);
            }
            catch (TaskCanceledException)
            {
                // Timeout
                AppLogger.Info("Update check timed out", "UpdateService.GetManifestAsync");
                return null;
            }
            catch (HttpRequestException ex)
            {
                AppLogger.Info($"Update check network error: {ex.Message}", "UpdateService.GetManifestAsync");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "UpdateService.GetManifestAsync");
                return null;
            }
        }

        // Download the update ZIP to a temp directory
        private static async Task<string?> DownloadUpdateAsync(UpdateManifest manifest, Action<string> updateStatus)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "VANTAGE_Update");
                Directory.CreateDirectory(tempDir);

                // Extract filename from the download URL
                string fileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"VANTAGE-{manifest.CurrentVersion}.zip";

                string zipPath = Path.Combine(tempDir, fileName);

                // Use a longer timeout for the download
                using var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var response = await downloadClient.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength ?? (manifest.ZipSizeBytes > 0 ? manifest.ZipSizeBytes : null);
                string totalMb = totalBytes.HasValue ? $"{totalBytes.Value / (1024.0 * 1024.0):F0}" : "?";

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                byte[] buffer = new byte[81920];
                long downloaded = 0;
                int bytesRead;
                DateTime lastProgressUpdate = DateTime.MinValue;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;

                    // Update progress at most every 250ms to avoid UI thrashing
                    if ((DateTime.UtcNow - lastProgressUpdate).TotalMilliseconds > 250)
                    {
                        string downloadedMb = $"{downloaded / (1024.0 * 1024.0):F0}";
                        updateStatus($"Downloading update ({downloadedMb}/{totalMb} MB)...");
                        lastProgressUpdate = DateTime.UtcNow;
                    }
                }

                return zipPath;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "UpdateService.DownloadUpdateAsync");
                return null;
            }
        }

        // Verify SHA-256 hash of a file
        private static bool VerifyFileHash(string filePath, string expectedHash)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                byte[] hashBytes = SHA256.HashData(stream);
                string actualHash = Convert.ToHexString(hashBytes);
                return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "UpdateService.VerifyFileHash");
                return false;
            }
        }

        // Extract VANTAGE.Updater.exe from the ZIP to a temp directory
        private static string? ExtractUpdaterToTemp(string zipPath)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "VANTAGE_Updater_Temp");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    // Extract the updater exe and any related files
                    if (entry.Name.StartsWith("VANTAGE.Updater", StringComparison.OrdinalIgnoreCase))
                    {
                        string destPath = Path.Combine(tempDir, entry.Name);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }

                string updaterPath = Path.Combine(tempDir, "VANTAGE.Updater.exe");
                return File.Exists(updaterPath) ? updaterPath : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "UpdateService.ExtractUpdaterToTemp");
                return null;
            }
        }

        // Get the current app version from the assembly
        public static Version GetCurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
                   ?? new Version(0, 0, 0);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
