using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using VANTAGE.Dialogs;

namespace VANTAGE.Utilities
{
    // Handles the --uninstall flow with a multi-page wizard dialog
    static class UninstallService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VANTAGE";

        // Entry point called from App.xaml.cs when --uninstall is detected
        public static void RunUninstall()
        {
            ThemeManager.LoadThemeFromSettings();

            var dialog = new UninstallDialog();
            dialog.ShowDialog();

            if (!dialog.UninstallCompleted)
            {
                Application.Current.Shutdown();
                return;
            }

            // Launch self-delete batch script before shutting down
            string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string vantageDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VANTAGE");
            LaunchSelfDeleteScript(installDir, vantageDataDir, dialog.KeepData);
            Application.Current.Shutdown();
        }

        // Remove desktop and Start Menu shortcuts
        public static void RemoveShortcuts()
        {
            TryDeleteFile(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "VANTAGE Milestone.lnk"));

            TryDeleteFile(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "VANTAGE Milestone.lnk"));
        }

        // Remove Add/Remove Programs registry entries
        public static void RemoveRegistryEntries()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(RegistryKeyPath, throwOnMissingSubKey: false);
            }
            catch { }
        }

        // Delete database, logs, and settings in %LocalAppData%\VANTAGE (outside the App folder)
        public static void DeleteLocalData(string vantageDataDir)
        {
            try
            {
                if (!Directory.Exists(vantageDataDir)) return;
                foreach (var file in Directory.GetFiles(vantageDataDir))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        // Batch script waits for this process to exit, then deletes the install directory
        private static void LaunchSelfDeleteScript(string installDir, string vantageDataDir, bool keepData)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), "vantage_uninstall.bat");
            int pid = Environment.ProcessId;

            string deleteDataLine = keepData ? "" : $"rmdir /s /q \"{vantageDataDir}\"";

            string script =
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                ":waitloop\r\n" +
                $"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
                "if not errorlevel 1 (\r\n" +
                "    timeout /t 1 /nobreak >nul\r\n" +
                "    goto waitloop\r\n" +
                ")\r\n" +
                $"rmdir /s /q \"{installDir}\"\r\n" +
                (string.IsNullOrEmpty(deleteDataLine) ? "" : deleteDataLine + "\r\n") +
                "del \"%~f0\"\r\n";

            File.WriteAllText(batchPath, script);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            });
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
