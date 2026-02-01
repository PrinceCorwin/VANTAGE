using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace VANTAGE.Installer
{
    // Handles downloading, extracting, and creating shortcuts for VANTAGE: Milestone
    public class InstallerService
    {
        // Manifest URL — reads from GitHub raw content (switch to Azure Blob URL later)
        private const string ManifestUrl = "https://raw.githubusercontent.com/PrinceCorwin/VANTAGE/main/updates/manifest.json";

        private static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VANTAGE", "App");

        private static readonly string ExePath = Path.Combine(InstallDir, "VANTAGE.exe");

        // Run the full install: download, verify, extract, create shortcut
        public async Task<bool> InstallAsync(IProgress<(double percent, string message)> progress)
        {
            try
            {
                // Check if already installed
                if (File.Exists(ExePath))
                {
                    var result = MessageBox.Show(
                        "VANTAGE: Milestone is already installed.\n\n" +
                        "Would you like to reinstall?",
                        "Already Installed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        progress.Report((0, "Installation cancelled."));
                        return false;
                    }
                }

                // Download manifest
                progress.Report((5, "Checking for latest version..."));
                var manifest = await DownloadManifestAsync();
                if (manifest == null)
                {
                    MessageBox.Show(
                        "Could not retrieve the update manifest.\n\n" +
                        "Check your internet connection and try again.",
                        "Download Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(manifest.DownloadUrl))
                {
                    MessageBox.Show(
                        "No download URL found in the update manifest.\n\n" +
                        "The application may not be published yet.",
                        "Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                // Download ZIP
                progress.Report((10, $"Downloading VANTAGE: Milestone v{manifest.CurrentVersion}..."));
                string? zipPath = await DownloadZipAsync(manifest, progress);
                if (zipPath == null)
                {
                    MessageBox.Show(
                        "Download failed. Check your internet connection and try again.",
                        "Download Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Verify hash
                if (!string.IsNullOrEmpty(manifest.Sha256))
                {
                    progress.Report((85, "Verifying download..."));
                    if (!VerifyHash(zipPath, manifest.Sha256))
                    {
                        TryDelete(zipPath);
                        MessageBox.Show(
                            "Download verification failed (hash mismatch).\n\n" +
                            "The file may be corrupted. Please try again.",
                            "Verification Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                // Extract
                progress.Report((90, "Installing..."));
                Directory.CreateDirectory(InstallDir);
                ZipFile.ExtractToDirectory(zipPath, InstallDir, overwriteFiles: true);
                TryDelete(zipPath);

                // Create desktop shortcut
                progress.Report((95, "Creating shortcut..."));
                CreateDesktopShortcut();

                progress.Report((100, "Installation complete!"));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Installation failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        // Launch the installed app
        public void LaunchApp()
        {
            if (File.Exists(ExePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ExePath,
                    UseShellExecute = true,
                    WorkingDirectory = InstallDir
                });
            }
        }

        private static async Task<InstallerManifest?> DownloadManifestAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string json = await client.GetStringAsync(ManifestUrl);
                return JsonSerializer.Deserialize<InstallerManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> DownloadZipAsync(
            InstallerManifest manifest,
            IProgress<(double percent, string message)> progress)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "VANTAGE_Install");
                Directory.CreateDirectory(tempDir);

                string fileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"VANTAGE-{manifest.CurrentVersion}.zip";

                string zipPath = Path.Combine(tempDir, fileName);

                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                using var response = await client.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength
                    ?? (manifest.ZipSizeBytes > 0 ? manifest.ZipSizeBytes : 0);

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                byte[] buffer = new byte[81920];
                long downloaded = 0;
                int bytesRead;
                DateTime lastUpdate = DateTime.MinValue;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;

                    if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds > 250)
                    {
                        double downloadedMb = downloaded / (1024.0 * 1024.0);
                        double percent = totalBytes > 0
                            ? 10 + (downloaded / (double)totalBytes * 75) // 10-85% range
                            : 50;
                        string totalMb = totalBytes > 0 ? $"{totalBytes / (1024.0 * 1024.0):F0}" : "?";
                        progress.Report((percent, $"Downloading... {downloadedMb:F0} / {totalMb} MB"));
                        lastUpdate = DateTime.UtcNow;
                    }
                }

                return zipPath;
            }
            catch
            {
                return null;
            }
        }

        private static bool VerifyHash(string filePath, string expectedHash)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);
            string actualHash = Convert.ToHexString(hashBytes);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        // Create a .lnk shortcut on the desktop using COM Shell interop
        private static void CreateDesktopShortcut()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, "VANTAGE Milestone.lnk");

            // Use the Windows Script Host COM object to create a proper .lnk file
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            try
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = ExePath;
                shortcut.WorkingDirectory = InstallDir;
                shortcut.Description = "VANTAGE: Milestone — Construction Progress Tracking";
                shortcut.IconLocation = $"{ExePath},0";
                shortcut.Save();
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    // Minimal manifest model for the installer (no dependency on main app's Models)
    public class InstallerManifest
    {
        [JsonPropertyName("currentVersion")]
        public string CurrentVersion { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("zipSizeBytes")]
        public long ZipSizeBytes { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
