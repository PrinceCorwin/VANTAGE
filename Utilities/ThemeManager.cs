using System;
using System.Windows;

namespace VANTAGE.Utilities
{
    public static class ThemeManager
    {
        // Available themes
        public static readonly string[] AvailableThemes = { "Dark", "Light" };

        // Current theme name
        public static string CurrentTheme { get; private set; } = "Dark";

        // Load theme from UserSettings on app startup
        public static void LoadThemeFromSettings()
        {
            try
            {
                string savedTheme = SettingsManager.GetUserSetting("Theme", "Dark");

                // Validate theme exists
                if (Array.Exists(AvailableThemes, t => t.Equals(savedTheme, StringComparison.OrdinalIgnoreCase)))
                {
                    SwitchTheme(savedTheme, saveToSettings: false);
                }
                else
                {
                    // Invalid theme, default to Dark
                    SwitchTheme("Dark", saveToSettings: true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ThemeManager.LoadThemeFromSettings");
                SwitchTheme("Dark", saveToSettings: false);
            }
        }

        // Switch to a different theme
        public static void SwitchTheme(string themeName, bool saveToSettings = true)
        {
            try
            {
                // Validate theme exists
                if (!Array.Exists(AvailableThemes, t => t.Equals(themeName, StringComparison.OrdinalIgnoreCase)))
                {
                    themeName = "Dark";
                }

                // Build theme path
                string themeUri = $"Themes/{themeName}Theme.xaml";

                // Load new theme
                var newTheme = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Relative)
                };

                // Clear existing merged dictionaries and add new theme
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(newTheme);

                CurrentTheme = themeName;

                // Save to user settings if requested
                if (saveToSettings)
                {
                    SettingsManager.SetUserSetting("Theme", themeName, "string");
                }
            }
            catch
            {
            }
        }

        
        /// Get current theme name
        
        public static string GetCurrentTheme()
        {
            return CurrentTheme;
        }
    }
}