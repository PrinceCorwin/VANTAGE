using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using VANTAGE.Dialogs;

namespace VANTAGE.Utilities
{
    // Handles the --uninstall flow: confirm with user, remove files/registry/shortcut
    static class UninstallService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VANTAGE";

        // Entry point called from App.xaml.cs when --uninstall is detected
        public static void RunUninstall()
        {
            // Load theme so the dialog renders correctly
            ThemeManager.LoadThemeFromSettings();

            var dialog = new UninstallDialog();
            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                Application.Current.Shutdown();
                return;
            }

            bool keepData = dialog.KeepData;
            string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string vantageDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VANTAGE");

            RemoveShortcuts();
            RemoveRegistryEntries();

            if (!keepData)
                DeleteLocalData(vantageDataDir);

            // Self-delete via batch script (exe can't delete itself while running)
            LaunchSelfDeleteScript(installDir, vantageDataDir, keepData);
            Application.Current.Shutdown();
        }

        private static void RemoveShortcuts()
        {
            // Desktop shortcut
            try
            {
                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "VANTAGE Milestone.lnk");
                if (File.Exists(desktopShortcut))
                    File.Delete(desktopShortcut);
            }
            catch { }

            // Start Menu shortcut
            try
            {
                string startMenuShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs", "VANTAGE Milestone.lnk");
                if (File.Exists(startMenuShortcut))
                    File.Delete(startMenuShortcut);
            }
            catch { }
        }

        private static void RemoveRegistryEntries()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(RegistryKeyPath, throwOnMissingSubKey: false);
            }
            catch { }
        }

        // Delete database, logs, and settings in %LocalAppData%\VANTAGE (outside the App folder)
        private static void DeleteLocalData(string vantageDataDir)
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
    }
}
