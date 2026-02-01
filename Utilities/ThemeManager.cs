using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VANTAGE.Utilities
{
    public static class ThemeManager
    {
        // Available themes: display name -> Syncfusion theme name
        private static readonly Dictionary<string, string> ThemeMap = new()
        {
            { "Dark", "FluentDark" },
            { "Light", "FluentLight" }
        };

        // Syncfusion MSControl dictionaries that must be swapped per theme
        private static readonly string[] SyncfusionMSControls =
        {
            "Button", "Window", "StatusBar", "TabControl"
        };

        public static readonly string[] AvailableThemes = { "Dark", "Light" };

        // Current active theme name ("Dark" or "Light")
        public static string CurrentTheme { get; private set; } = "Dark";

        // Load saved theme and apply it before any windows are created
        public static void LoadThemeFromSettings()
        {
            try
            {
                string savedTheme = SettingsManager.GetUserSetting("Theme", "Dark");
                AppLogger.Info($"Theme setting from DB: '{savedTheme}'", "ThemeManager.LoadThemeFromSettings");

                // Handle migration from old format ("DarkTheme.xaml" -> "Dark")
                if (savedTheme.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    savedTheme = savedTheme.Replace("Theme.xaml", "", StringComparison.OrdinalIgnoreCase);
                    AppLogger.Info($"Migrated theme value to: '{savedTheme}'", "ThemeManager.LoadThemeFromSettings");
                }

                // Validate theme name
                if (!Array.Exists(AvailableThemes, t => t.Equals(savedTheme, StringComparison.OrdinalIgnoreCase)))
                {
                    AppLogger.Info($"Invalid theme '{savedTheme}', defaulting to Dark", "ThemeManager.LoadThemeFromSettings");
                    savedTheme = "Dark";
                }

                // App.xaml already loads Dark as default, only swap if different
                if (!savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Info($"Applying theme: '{savedTheme}'", "ThemeManager.LoadThemeFromSettings");
                    ApplyTheme(savedTheme);
                }

                CurrentTheme = savedTheme;
                AppLogger.Info($"Theme loaded: '{CurrentTheme}'", "ThemeManager.LoadThemeFromSettings");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ThemeManager.LoadThemeFromSettings");
                CurrentTheme = "Dark";
            }
        }

        // Save theme to UserSettings (called by ThemeManagerDialog)
        public static void SaveTheme(string themeName)
        {
            if (!Array.Exists(AvailableThemes, t => t.Equals(themeName, StringComparison.OrdinalIgnoreCase)))
            {
                themeName = "Dark";
            }

            CurrentTheme = themeName;
            SettingsManager.SetUserSetting("Theme", themeName, "string");
            AppLogger.Info($"Theme saved: '{themeName}'", "ThemeManager.SaveTheme");
        }

        // Get the Syncfusion theme name for SfSkinManager calls
        public static string GetSyncfusionThemeName()
        {
            return ThemeMap.GetValueOrDefault(CurrentTheme, "FluentDark");
        }

        // Swap resource dictionaries from Dark to the target theme
        private static void ApplyTheme(string themeName)
        {
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            string sfThemeName = ThemeMap.GetValueOrDefault(themeName, "FluentDark");

            // Remove existing Syncfusion MSControl dictionaries (identified by URI)
            var toRemove = mergedDicts
                .Where(d => d.Source != null && d.Source.OriginalString.Contains("Syncfusion.Themes."))
                .ToList();
            AppLogger.Info($"Removing {toRemove.Count} Syncfusion dictionaries", "ThemeManager.ApplyTheme");
            foreach (var dict in toRemove)
            {
                mergedDicts.Remove(dict);
            }

            // Remove existing custom theme dictionary (DarkTheme.xaml or LightTheme.xaml)
            var customTheme = mergedDicts
                .Where(d => d.Source != null && d.Source.OriginalString.Contains("Themes/") && d.Source.OriginalString.EndsWith("Theme.xaml"))
                .ToList();
            AppLogger.Info($"Removing {customTheme.Count} custom theme dictionaries", "ThemeManager.ApplyTheme");
            foreach (var dict in customTheme)
            {
                mergedDicts.Remove(dict);
            }

            // Add new Syncfusion MSControl dictionaries
            string sfPackage = $"Syncfusion.Themes.{sfThemeName}.WPF";
            foreach (var control in SyncfusionMSControls)
            {
                mergedDicts.Add(new ResourceDictionary
                {
                    Source = new Uri($"/{sfPackage};component/MSControl/{control}.xaml", UriKind.Relative)
                });
            }

            // Add new custom theme dictionary (must be last to override Syncfusion)
            mergedDicts.Add(new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative)
            });

            AppLogger.Info($"Applied theme dictionaries for '{themeName}' (Syncfusion: {sfThemeName})", "ThemeManager.ApplyTheme");
        }
    }
}
