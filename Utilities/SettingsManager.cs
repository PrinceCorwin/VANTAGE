using VANTAGE;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using VANTAGE.Utilities;


namespace VANTAGE.Utilities
{
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
                return result != null ? result.ToString() : defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting app setting: {ex.Message}");
                // TODO: Add proper logging (e.g., to file or central log)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting app setting: {ex.Message}");
                // TODO: Add proper logging (e.g., to file or central log)
            }
        }

        
        /// Get a user-specific setting by name
        
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
                // TODO: Add proper logging (e.g., to file or central log)
                return defaultValue;
            }
        }

        
        /// Set a user-specific setting
        
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
                // TODO: Add proper logging (e.g., to file or central log)
            }
        } 
        public static void InitializeDefaultAppSettings()
        {
            try
            {
                // Set default theme settings if not already set
                if (string.IsNullOrEmpty(GetAppSetting("Theme")))
                {
                    SetAppSetting("Theme", "DarkTheme.xaml", "string");
                    SetAppSetting("ToolbarLocation", "Top", "string");
                    SetAppSetting("WindowWidth", "1920", "int");
                    SetAppSetting("WindowHeight", "1080", "int");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing app settings: {ex.Message}");
                // TODO: Add proper logging (e.g., to file or central log)
            }
        }

        
        /// Initialize default user settings on first login
        
        public static void InitializeDefaultUserSettings(int userId)
        {
            try
            {
                // Set default user settings if not already set
                if (string.IsNullOrEmpty(GetUserSetting(userId, "LastModuleUsed")))
                {
                    SetUserSetting(userId, "Theme", "DarkTheme.xaml", "string");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing user settings: {ex.Message}");
                // TODO: Add proper logging (e.g., to file or central log)
            }
        }
    }
}