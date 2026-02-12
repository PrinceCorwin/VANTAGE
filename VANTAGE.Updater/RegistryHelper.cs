using Microsoft.Win32;

namespace VANTAGE.Updater;

// Updates Add/Remove Programs DisplayVersion after applying an update
static class RegistryHelper
{
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VANTAGE";

    // Update DisplayVersion in registry. Skips silently if key doesn't exist (pre-feature install).
    public static void UpdateVersionInRegistry(string version)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, writable: true);
            if (key == null) return;
            key.SetValue("DisplayVersion", version);
        }
        catch { }
    }
}
