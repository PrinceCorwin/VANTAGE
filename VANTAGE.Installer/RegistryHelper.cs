using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace VANTAGE.Installer
{
    // Writes Add/Remove Programs registry entries so VANTAGE appears in Settings > Apps
    static class RegistryHelper
    {
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VANTAGE";

        // Write all registry keys after a successful install
        public static void WriteInstallRegistry(string installDir, string exePath, string version)
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
            if (key == null) return;

            key.SetValue("DisplayName", "VANTAGE: Milestone");
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", "Summit Industrial");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{exePath}\" --uninstall");
            key.SetValue("DisplayIcon", $"{exePath},0");
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            // EstimatedSize in KB
            try
            {
                var dirInfo = new DirectoryInfo(installDir);
                long sizeKb = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length) / 1024;
                key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
            }
            catch { }
        }
    }
}
