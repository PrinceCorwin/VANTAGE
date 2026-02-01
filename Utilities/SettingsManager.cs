using VANTAGE;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.Json;
using VANTAGE.Models;
using VANTAGE.Utilities;


namespace VANTAGE.Utilities
{
    // Export format for a single setting
    public class UserSettingExport
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";
    }

    // Export file format
    public class UserSettingsExportFile
    {
        public string ExportedBy { get; set; } = string.Empty;
        public string ExportedDate { get; set; } = string.Empty;
        public string AppVersion { get; set; } = "1.0.0";
        public List<UserSettingExport> Settings { get; set; } = new();
    }

    public static class SettingsManager
    {

        /// Get an app-wide setting by name

        /// 
        /// <summary>
        /// Remove an app-wide setting by name.
        /// Used to clean up LastPulledSyncVersion entries when removing projects from Local.
        /// </summary>
        /// <param name="settingName">The setting name to remove</param>
        /// <returns>True if a setting was removed, false if it didn't exist</returns>
        public static bool RemoveAppSetting(string settingName)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM AppSettings WHERE SettingName = @name";
                command.Parameters.AddWithValue("@name", settingName);

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    AppLogger.Info($"Removed AppSetting: {settingName}", "SettingsManager.RemoveAppSetting");
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.RemoveAppSetting");
                return false;
            }
        }
        public static string GetAppSetting(string settingName, string defaultValue = "")
        {

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingValue FROM AppSettings WHERE SettingName = @name";
                command.Parameters.AddWithValue("@name", settingName);

                var result = command.ExecuteScalar();
                return result != null ? result.ToString() ?? defaultValue : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        
        /// Set an app-wide setting
        
        public static void SetAppSetting(string settingName, string settingValue, string dataType = "string")
        {

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO AppSettings (SettingName, SettingValue, DataType) 
                    VALUES (@name, @value, @type)
                    ON CONFLICT(SettingName) 
                    DO UPDATE SET SettingValue = @value, DataType = @type";
                command.Parameters.AddWithValue("@name", settingName);
                command.Parameters.AddWithValue("@value", settingValue);
                command.Parameters.AddWithValue("@type", dataType);

                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        // Get a user-specific setting by name
        public static string GetUserSetting(string settingName, string defaultValue = "")
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingValue FROM UserSettings WHERE SettingName = @name";
                command.Parameters.AddWithValue("@name", settingName);

                var result = command.ExecuteScalar();
                return result != null ? result.ToString() ?? defaultValue : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        // Set a user-specific setting
        public static void SetUserSetting(string settingName, string settingValue, string dataType = "string")
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO UserSettings (SettingName, SettingValue, DataType)
                    VALUES (@name, @value, @type)
                    ON CONFLICT(SettingName)
                    DO UPDATE SET SettingValue = @value, DataType = @type";
                command.Parameters.AddWithValue("@name", settingName);
                command.Parameters.AddWithValue("@value", settingValue);
                command.Parameters.AddWithValue("@type", dataType);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.SetUserSetting");
            }
        }

        // Remove a user-specific setting by name
        public static bool RemoveUserSetting(string settingName)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM UserSettings WHERE SettingName = @name";
                command.Parameters.AddWithValue("@name", settingName);

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    AppLogger.Info($"Removed UserSetting: {settingName}", "SettingsManager.RemoveUserSetting");
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.RemoveUserSetting");
                return false;
            }
        }

        // Legacy I/O visibility setting - controls whether Legacy import/export menu items are shown
        public static bool GetShowLegacyIO()
        {
            var value = GetUserSetting("ShowLegacyIO", "true");
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetShowLegacyIO(bool show)
        {
            SetUserSetting("ShowLegacyIO", show.ToString().ToLower(), "bool");
        }

        public static void InitializeDefaultAppSettings()
        {
            try
            {
                // Set default theme settings if not already set
                if (string.IsNullOrEmpty(GetAppSetting("Theme")))
                {
                    SetAppSetting("Theme", "Dark", "string");
                    SetAppSetting("ToolbarLocation", "Top", "string");
                    SetAppSetting("WindowWidth", "1920", "int");
                    SetAppSetting("WindowHeight", "1080", "int");
                }
            }
            catch
            {
            }
        }

        // Initialize default user settings on first login
        public static void InitializeDefaultUserSettings()
        {
            try
            {
                // Only set default theme if no theme setting exists yet
                if (string.IsNullOrEmpty(GetUserSetting("Theme")))
                {
                    SetUserSetting("Theme", "Dark", "string");
                }
            }
            catch
            {
            }
        }

        // Get all settings for export (excludes LastSyncUtcDate to ensure full sync on new machines)
        public static List<UserSettingExport> GetAllUserSettings()
        {
            var settings = new List<UserSettingExport>();

            // Settings to exclude from export (would cause sync issues on new machines)
            var excludedSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LastSyncUtcDate"
            };

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingName, SettingValue, DataType FROM UserSettings";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var settingName = reader.GetString(0);
                    if (excludedSettings.Contains(settingName))
                        continue;

                    settings.Add(new UserSettingExport
                    {
                        Name = settingName,
                        Value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        DataType = reader.IsDBNull(2) ? "string" : reader.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetAllUserSettings");
            }

            return settings;
        }

        // Import settings - replaceAll: true = delete existing first; false = merge (update existing, add new)
        public static int ImportUserSettings(List<UserSettingExport> settings, bool replaceAll)
        {
            int imported = 0;

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                using var transaction = connection.BeginTransaction();

                if (replaceAll)
                {
                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM UserSettings";
                    deleteCmd.ExecuteNonQuery();
                }

                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO UserSettings (SettingName, SettingValue, DataType)
                    VALUES (@name, @value, @type)
                    ON CONFLICT(SettingName)
                    DO UPDATE SET SettingValue = @value, DataType = @type";
                insertCmd.Parameters.Add("@name", SqliteType.Text);
                insertCmd.Parameters.Add("@value", SqliteType.Text);
                insertCmd.Parameters.Add("@type", SqliteType.Text);

                foreach (var setting in settings)
                {
                    insertCmd.Parameters["@name"].Value = setting.Name;
                    insertCmd.Parameters["@value"].Value = setting.Value;
                    insertCmd.Parameters["@type"].Value = setting.DataType;
                    insertCmd.ExecuteNonQuery();
                    imported++;
                }

                transaction.Commit();

                AppLogger.Info($"Imported {imported} user settings (replaceAll={replaceAll})",
                    "SettingsManager.ImportUserSettings", App.CurrentUser?.Username ?? "Unknown");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.ImportUserSettings");
                return 0;
            }

            return imported;
        }

        // Grid Layout constants
        private const string LayoutIndexKey = "GridLayouts.Index";
        private const string LayoutDataPrefix = "GridLayout.";
        private const string LayoutDataSuffix = ".Data";
        private const string ActiveLayoutKey = "GridLayouts.ActiveLayout";
        public const int MaxLayouts = 5;

        // Get list of saved layout names
        public static List<string> GetGridLayoutNames()
        {
            try
            {
                var json = GetUserSetting(LayoutIndexKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetGridLayoutNames");
            }
            return new List<string>();
        }

        // Save layout names index
        public static void SaveGridLayoutNames(List<string> names)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(names);
                SetUserSetting(LayoutIndexKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.SaveGridLayoutNames");
            }
        }

        // Get a specific layout by name
        public static GridLayout? GetGridLayout(string layoutName)
        {
            try
            {
                var key = $"{LayoutDataPrefix}{layoutName}{LayoutDataSuffix}";
                var json = GetUserSetting(key);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<GridLayout>(json);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetGridLayout");
            }
            return null;
        }

        // Save a layout
        public static void SaveGridLayout(GridLayout layout)
        {
            try
            {
                var key = $"{LayoutDataPrefix}{layout.Name}{LayoutDataSuffix}";
                var json = System.Text.Json.JsonSerializer.Serialize(layout);
                SetUserSetting(key, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.SaveGridLayout");
            }
        }

        // Delete a layout and remove from index
        public static void DeleteGridLayout(string layoutName)
        {
            try
            {
                // Remove from index
                var names = GetGridLayoutNames();
                if (names.Remove(layoutName))
                {
                    SaveGridLayoutNames(names);
                }

                // Delete the layout data
                var key = $"{LayoutDataPrefix}{layoutName}{LayoutDataSuffix}";
                RemoveUserSetting(key);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.DeleteGridLayout");
            }
        }

        // Get the currently active layout name
        public static string GetActiveLayoutName()
        {
            return GetUserSetting(ActiveLayoutKey);
        }

        // Set the currently active layout name
        public static void SetActiveLayoutName(string layoutName)
        {
            SetUserSetting(ActiveLayoutKey, layoutName);
        }

        // Delete all layout data (for reset)
        public static void ClearAllGridLayouts()
        {
            try
            {
                var names = GetGridLayoutNames();
                foreach (var name in names)
                {
                    var key = $"{LayoutDataPrefix}{name}{LayoutDataSuffix}";
                    RemoveUserSetting(key);
                }
                RemoveUserSetting(LayoutIndexKey);
                RemoveUserSetting(ActiveLayoutKey);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.ClearAllGridLayouts");
            }
        }
    }
}