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

        /// <summary>
        /// Load theme from UserSettings on app startup
        /// </summary>
        public static void LoadThemeFromSettings(int userId)
        {
            try
            {
                string savedTheme = SettingsManager.GetUserSetting(userId, "Theme", "Dark");

                // Validate theme exists
                if (Array.Exists(AvailableThemes, t => t.Equals(savedTheme, StringComparison.OrdinalIgnoreCase)))
                {
                    SwitchTheme(savedTheme, saveToSettings: false);
                }
                else
                {
                    // Invalid theme, default to Dark
                    SwitchTheme("Dark", saveToSettings: true, userId);
                }
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging (e.g., to file or central log)
                SwitchTheme("Dark", saveToSettings: false);
            }
        }

        /// <summary>
        /// Switch to a different theme
        /// </summary>
        public static void SwitchTheme(string themeName, bool saveToSettings = true, int? userId = null)
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
                if (saveToSettings && userId.HasValue)
                {
                    SettingsManager.SetUserSetting(userId.Value, "Theme", themeName, "string");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching theme: {ex.Message}");
                // TODO: Add proper logging (e.g., to file or central log)
            }
        }

        /// <summary>
        /// Get current theme name
        /// </summary>
        public static string GetCurrentTheme()
        {
            return CurrentTheme;
        }
    }
}