using VANTAGE;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    public static class SettingsManager
    {
        /// <summary>
        /// Get an app-wide setting by name
        /// </summary>
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
                return result != null ? result.ToString() : defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting app setting: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Set an app-wide setting
        /// </summary>
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting app setting: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a user-specific setting by name
        /// </summary>
        public static string GetUserSetting(int userId, string settingName, string defaultValue = "")
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingValue FROM UserSettings WHERE UserID = @userId AND SettingName = @name";
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@name", settingName);

                var result = command.ExecuteScalar();
                return result != null ? result.ToString() : defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting user setting: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Set a user-specific setting
        /// </summary>
        public static void SetUserSetting(int userId, string settingName, string settingValue, string dataType = "string")
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO UserSettings (UserID, SettingName, SettingValue, DataType) 
                    VALUES (@userId, @name, @value, @type)
                    ON CONFLICT(UserID, SettingName) 
                    DO UPDATE SET SettingValue = @value, DataType = @type";
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@name", settingName);
                command.Parameters.AddWithValue("@value", settingValue);
                command.Parameters.AddWithValue("@type", dataType);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting user setting: {ex.Message}");
            }
        }

        /// <summary>
        /// Get last module used by user
        /// </summary>
        public static string GetLastModuleUsed(int userId, string defaultModule = "PROGRESS")
        {
            return GetUserSetting(userId, "LastModuleUsed", defaultModule);
        }

        /// <summary>
        /// Set last module used by user
        /// </summary>
        public static void SetLastModuleUsed(int userId, string moduleName)
        {
            SetUserSetting(userId, "LastModuleUsed", moduleName, "string");
        }

        /// <summary>
        /// Get column order for a user (as JSON array)
        /// </summary>
        public static List<string> GetColumnOrder(int userId)
        {
            try
            {
                string json = GetUserSetting(userId, "ColumnOrder", "");
                if (string.IsNullOrEmpty(json))
                    return GetDefaultColumnOrder();

                var columns = JsonConvert.DeserializeObject<List<string>>(json);
                return columns ?? GetDefaultColumnOrder();
            }
            catch
            {
                return GetDefaultColumnOrder();
            }
        }

        /// <summary>
        /// Set column order for a user (saves as JSON array)
        /// </summary>
        public static void SetColumnOrder(int userId, List<string> columnNames)
        {
            try
            {
                string json = JsonConvert.SerializeObject(columnNames);
                SetUserSetting(userId, "ColumnOrder", json, "json");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting column order: {ex.Message}");
            }
        }

        /// <summary>
        /// Get default column order (if user hasn't customized)
        /// </summary>
        private static List<string> GetDefaultColumnOrder()
        {
            return new List<string>
            {
                "Tag_TagNo",
                "Tag_Descriptions",
                "Tag_Area",
                "Catg_ComponentType",
                "Val_Quantity",
                "Val_UOM",
                "Val_EarnedQty",
                "Val_Perc_Complete",
                "Status",
                "AssignedToUsername",
                "LastModifiedBy"
            };
        }

        /// <summary>
        /// Initialize default app settings on first run
        /// </summary>
        public static void InitializeDefaultAppSettings()
        {
            try
            {
                // Set default theme settings if not already set
                if (string.IsNullOrEmpty(GetAppSetting("Theme")))
                {
                    SetAppSetting("Theme", "Dark", "string");
                    SetAppSetting("ToolbarLocation", "Top", "string");
                    SetAppSetting("WindowWidth", "1200", "int");
                    SetAppSetting("WindowHeight", "700", "int");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing app settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize default user settings on first login
        /// </summary>
        public static void InitializeDefaultUserSettings(int userId)
        {
            try
            {
                // Set default user settings if not already set
                if (string.IsNullOrEmpty(GetUserSetting(userId, "LastModuleUsed")))
                {
                    SetLastModuleUsed(userId, "PROGRESS");
                    SetColumnOrder(userId, GetDefaultColumnOrder());
                    SetUserSetting(userId, "DefaultProject", "", "string");
                    SetUserSetting(userId, "Theme", "Dark", "string");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing user settings: {ex.Message}");
            }
        }
    }
}