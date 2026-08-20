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
                using var connection = DatabaseSetup.GetUserSettingsConnection();
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
                using var connection = DatabaseSetup.GetUserSettingsConnection();
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
                using var connection = DatabaseSetup.GetUserSettingsConnection();
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
                using var connection = DatabaseSetup.GetUserSettingsConnection();
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
                using var connection = DatabaseSetup.GetUserSettingsConnection();
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

        // === ONE-TIME MIGRATION TO SHARED USER-SETTINGS STORE ===
        // Runs once, the first launch after the shared user-settings database is
        // introduced. Sweeps existing UserSettings out of every registered project
        // database into the shared store so preferences saved before the switch are
        // not lost. The active database's settings are the baseline (copied wholesale);
        // grid- and report-layout collections from the OTHER databases are unioned in
        // so layouts stranded in databases the user switched away from are recovered.
        public static void MigrateUserSettingsToSharedStore()
        {
            try
            {
                var entries = DatabaseRegistry.Load();
                var active = DatabaseRegistry.GetActive();
                string activePath = DatabaseRegistry.FullPath(active.FileName);

                // 1. Baseline: copy the active database's UserSettings wholesale. This is
                //    the user's canonical current state and wins on every scalar key.
                int baseline = 0;
                foreach (var kvp in ReadRawUserSettings(activePath))
                {
                    SetUserSetting(kvp.Key, kvp.Value.Value, kvp.Value.Type);
                    baseline++;
                }

                // 2. Union grid/report layouts from the other databases so nothing saved
                //    in a switched-away-from database is left stranded.
                int recovered = 0;
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.FileName, active.FileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rows = ReadRawUserSettings(DatabaseRegistry.FullPath(entry.FileName));
                    recovered += MergeGridLayouts(rows, entry.Name);
                    recovered += MergeReportLayouts(rows);
                }

                AppLogger.Info(
                    $"User-settings recovery: copied {baseline} settings from active database '{active.Name}', recovered {recovered} extra layout(s) from other databases",
                    "SettingsManager.MigrateUserSettingsToSharedStore", App.CurrentUser?.Username ?? "System");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.MigrateUserSettingsToSharedStore");
            }
        }

        // Read a single database's UserSettings table into a name -> (value, type) map.
        // Tolerant: a missing file or missing table yields an empty map (logged).
        private static Dictionary<string, (string Value, string Type)> ReadRawUserSettings(string dbPath)
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            try
            {
                if (!System.IO.File.Exists(dbPath)) return result;

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingName, SettingValue, DataType FROM UserSettings";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var type = reader.IsDBNull(2) ? "string" : reader.GetString(2);
                    result[name] = (value, type);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.ReadRawUserSettings");
            }
            return result;
        }

        // Union the grid layouts from a source database's rows into the shared store.
        // On name collision the source layout is kept under a "<name> (<db>)" label so
        // nothing is overwritten. Returns the number of layouts added.
        private static int MergeGridLayouts(Dictionary<string, (string Value, string Type)> source, string sourceDbName)
        {
            int added = 0;
            try
            {
                if (!source.TryGetValue(LayoutIndexKey, out var idxRow) || string.IsNullOrWhiteSpace(idxRow.Value))
                    return 0;

                var sourceNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(idxRow.Value) ?? new List<string>();
                if (sourceNames.Count == 0) return 0;

                var sharedNames = GetGridLayoutNames();
                var sharedSet = new HashSet<string>(sharedNames, StringComparer.OrdinalIgnoreCase);

                foreach (var name in sourceNames)
                {
                    var dataKey = $"{LayoutDataPrefix}{name}{LayoutDataSuffix}";
                    if (!source.TryGetValue(dataKey, out var dataRow) || string.IsNullOrWhiteSpace(dataRow.Value))
                        continue;

                    // Resolve a non-colliding name so both databases' layouts survive.
                    string finalName = name;
                    if (sharedSet.Contains(finalName))
                    {
                        finalName = $"{name} ({sourceDbName})";
                        int n = 2;
                        while (sharedSet.Contains(finalName))
                        {
                            finalName = $"{name} ({sourceDbName} {n})";
                            n++;
                        }
                    }

                    // Keep the layout's own Name field in step with the (possibly renamed)
                    // index entry, so the manager label and the lookup key stay aligned.
                    string dataJson = dataRow.Value;
                    if (!string.Equals(finalName, name, StringComparison.Ordinal))
                    {
                        try
                        {
                            var layout = System.Text.Json.JsonSerializer.Deserialize<GridLayout>(dataRow.Value);
                            if (layout != null)
                            {
                                layout.Name = finalName;
                                dataJson = System.Text.Json.JsonSerializer.Serialize(layout);
                            }
                        }
                        catch { /* fall back to the raw source JSON */ }
                    }

                    SetUserSetting($"{LayoutDataPrefix}{finalName}{LayoutDataSuffix}", dataJson, "json");
                    sharedNames.Add(finalName);
                    sharedSet.Add(finalName);
                    added++;
                }

                if (added > 0) SaveGridLayoutNames(sharedNames);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.MergeGridLayouts");
            }
            return added;
        }

        // Union the Project Dashboard report layouts from a source database's rows into
        // the shared store. Keyed by Id (unique-generated), so existing ids win and only
        // genuinely new layouts are added. Returns the number of layouts added.
        private static int MergeReportLayouts(Dictionary<string, (string Value, string Type)> source)
        {
            int added = 0;
            try
            {
                if (!source.TryGetValue(ReportLayoutIndexKey, out var idxRow) || string.IsNullOrWhiteSpace(idxRow.Value))
                    return 0;

                var sourceList = System.Text.Json.JsonSerializer.Deserialize<List<ReportLayoutRef>>(idxRow.Value) ?? new List<ReportLayoutRef>();
                if (sourceList.Count == 0) return 0;

                var sharedList = GetReportLayoutList();
                var sharedIds = new HashSet<string>(sharedList.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

                bool changed = false;
                foreach (var refItem in sourceList)
                {
                    if (string.IsNullOrWhiteSpace(refItem.Id) || sharedIds.Contains(refItem.Id)) continue;

                    var dataKey = $"{ReportLayoutDataPrefix}{refItem.Id}{ReportLayoutDataSuffix}";
                    if (!source.TryGetValue(dataKey, out var dataRow) || string.IsNullOrWhiteSpace(dataRow.Value)) continue;

                    SetUserSetting(dataKey, dataRow.Value, "json");
                    sharedList.Add(refItem);
                    sharedIds.Add(refItem.Id);
                    changed = true;
                    added++;
                }

                if (changed)
                    SetUserSetting(ReportLayoutIndexKey, System.Text.Json.JsonSerializer.Serialize(sharedList), "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.MergeReportLayouts");
            }
            return added;
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

        // === PROJECT DASHBOARD REPORT LAYOUTS (Customize feature) ===
        // Named report layouts for the Project Dashboard, stored the same way as grid layouts:
        //   ReportLayouts.Index    — JSON List<ReportLayoutRef> ({ Id, Name })
        //   ReportLayout.<id>.Data — the layout's raw JSON (authored by the dashboard page)
        //   ReportLayouts.LastUsed — id of the last layout the user viewed (opens next session)
        // Keyed by page-generated Id (not name), so renames don't orphan data.
        private const string ReportLayoutIndexKey = "ReportLayouts.Index";
        private const string ReportLayoutDataPrefix = "ReportLayout.";
        private const string ReportLayoutDataSuffix = ".Data";
        private const string ReportLayoutLastUsedKey = "ReportLayouts.LastUsed";

        public sealed class ReportLayoutRef
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public static List<ReportLayoutRef> GetReportLayoutList()
        {
            try
            {
                var json = GetUserSetting(ReportLayoutIndexKey);
                if (!string.IsNullOrWhiteSpace(json))
                    return System.Text.Json.JsonSerializer.Deserialize<List<ReportLayoutRef>>(json) ?? new List<ReportLayoutRef>();
            }
            catch (Exception ex) { AppLogger.Error(ex, "SettingsManager.GetReportLayoutList"); }
            return new List<ReportLayoutRef>();
        }

        public static string? GetReportLayoutJson(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var json = GetUserSetting($"{ReportLayoutDataPrefix}{id}{ReportLayoutDataSuffix}");
            return string.IsNullOrWhiteSpace(json) ? null : json;
        }

        // Upsert a layout (index entry + data). rawJson is the exact JSON the dashboard page authored.
        public static void SaveReportLayout(string id, string name, string rawJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return;
                var list = GetReportLayoutList();
                var existing = list.Find(r => r.Id == id);
                if (existing != null) existing.Name = name;
                else list.Add(new ReportLayoutRef { Id = id, Name = name });
                SetUserSetting(ReportLayoutIndexKey, System.Text.Json.JsonSerializer.Serialize(list), "json");
                SetUserSetting($"{ReportLayoutDataPrefix}{id}{ReportLayoutDataSuffix}", rawJson, "json");
            }
            catch (Exception ex) { AppLogger.Error(ex, "SettingsManager.SaveReportLayout"); }
        }

        public static void DeleteReportLayout(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return;
                var list = GetReportLayoutList();
                if (list.RemoveAll(r => r.Id == id) > 0)
                    SetUserSetting(ReportLayoutIndexKey, System.Text.Json.JsonSerializer.Serialize(list), "json");
                RemoveUserSetting($"{ReportLayoutDataPrefix}{id}{ReportLayoutDataSuffix}");
                if (GetLastUsedReportLayout() == id) SetLastUsedReportLayout(string.Empty);
            }
            catch (Exception ex) { AppLogger.Error(ex, "SettingsManager.DeleteReportLayout"); }
        }

        public static string GetLastUsedReportLayout() => GetUserSetting(ReportLayoutLastUsedKey);
        public static void SetLastUsedReportLayout(string id) => SetUserSetting(ReportLayoutLastUsedKey, id ?? string.Empty);

        // === UDF NAME MAPPINGS (Manage UDF Names dialog) ===

        // Storage key for the Manage UDF Names feature.
        //   ProgressUDFNames.Active — JSON Dictionary<string,string> of currently-applied overrides
        private const string ActiveUDFNamesKey = "ProgressUDFNames.Active";

        // Get the currently-applied UDF column-header overrides.
        // Empty/missing key for a UDF means "use default header (MappingName)".
        public static Dictionary<string, string> GetActiveUDFNames()
        {
            try
            {
                var json = GetUserSetting(ActiveUDFNamesKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetActiveUDFNames");
            }
            return new Dictionary<string, string>();
        }

        // Set the currently-applied UDF column-header overrides
        public static void SetActiveUDFNames(Dictionary<string, string> names)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(names);
                SetUserSetting(ActiveUDFNamesKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.SetActiveUDFNames");
            }
        }

        // Clear the active UDF overrides (revert all UDF headers to defaults).
        // Saved maps are not affected.
        public static void ClearActiveUDFNames()
        {
            RemoveUserSetting(ActiveUDFNamesKey);
        }

        // === ANALYSIS VIEW SETTINGS ===

        // Group By field selection (default: PhaseCode)
        public static string GetAnalysisGroupField()
        {
            return GetUserSetting("AnalysisGroupField", "PhaseCode");
        }

        public static void SetAnalysisGroupField(string field)
        {
            SetUserSetting("AnalysisGroupField", field, "string");
        }

        // Current User Only toggle. Default false = "All Users" when no saved setting exists.
        public static bool GetAnalysisCurrentUserOnly()
        {
            return GetUserSetting("AnalysisCurrentUserOnly", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetAnalysisCurrentUserOnly(bool value)
        {
            SetUserSetting("AnalysisCurrentUserOnly", value.ToString().ToLower(), "bool");
        }

        // Source mode for the Analysis summary grid — "Local" or "Snapshot". Default "Local".
        public static string GetAnalysisSourceMode()
        {
            return GetUserSetting("AnalysisSourceMode", "Local");
        }

        public static void SetAnalysisSourceMode(string mode)
        {
            SetUserSetting("AnalysisSourceMode", mode, "string");
        }

        // Selected projects (comma-separated list of ProjectIDs)
        public static string GetAnalysisSelectedProjects()
        {
            return GetUserSetting("AnalysisSelectedProjects", "");
        }

        public static void SetAnalysisSelectedProjects(string csv)
        {
            SetUserSetting("AnalysisSelectedProjects", csv, "string");
        }

        // Grid layout (JSON with column widths and row heights)
        public static string GetAnalysisGridLayout()
        {
            return GetUserSetting("AnalysisGridLayout", "");
        }

        public static void SetAnalysisGridLayout(string json)
        {
            SetUserSetting("AnalysisGridLayout", json, "json");
        }

        // === SCHEDULE UDF MAPPING SETTINGS ===

        private const string ScheduleUDFMappingKey = "Schedule.UDFMappings";

        // Get UDF column mappings for P6 import
        public static Models.ScheduleUDFMappingConfig GetScheduleUDFMappings()
        {
            try
            {
                var json = GetUserSetting(ScheduleUDFMappingKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var config = System.Text.Json.JsonSerializer.Deserialize<Models.ScheduleUDFMappingConfig>(json);
                    if (config != null)
                        return config;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetScheduleUDFMappings");
            }
            return Models.ScheduleUDFMappingConfig.CreateDefault();
        }

        // Save UDF column mappings
        public static void SetScheduleUDFMappings(Models.ScheduleUDFMappingConfig config)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(config);
                SetUserSetting(ScheduleUDFMappingKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.SetScheduleUDFMappings");
            }
        }

        // === TUTORIAL WATCH TRACKING ===
        // Set of S3 object keys the user has opened, stored as a JSON array under
        // Tutorials.Watched. Drives the "Watched" badge in the Tutorials dialog.
        private const string TutorialsWatchedKey = "Tutorials.Watched";

        public static HashSet<string> GetWatchedTutorials()
        {
            try
            {
                var json = GetUserSetting(TutorialsWatchedKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                        return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.GetWatchedTutorials");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static void MarkTutorialWatched(string videoKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(videoKey)) return;
                var set = GetWatchedTutorials();
                if (set.Add(videoKey))
                    SetUserSetting(TutorialsWatchedKey, System.Text.Json.JsonSerializer.Serialize(set), "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.MarkTutorialWatched");
            }
        }

        // Drop watched keys that no longer exist in the manifest (e.g. an admin deleted
        // the video). Self-heals each user's set on Tutorials open: keeps it from
        // accumulating orphans and stops a re-used filename from inheriting a stale
        // "watched" flag. Persists only when something actually changed.
        public static void PruneWatchedTutorials(IEnumerable<string> validKeys)
        {
            try
            {
                var set = GetWatchedTutorials();
                if (set.Count == 0) return;

                var valid = new HashSet<string>(validKeys, StringComparer.OrdinalIgnoreCase);
                int before = set.Count;
                set.IntersectWith(valid);

                if (set.Count != before)
                    SetUserSetting(TutorialsWatchedKey, System.Text.Json.JsonSerializer.Serialize(set), "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SettingsManager.PruneWatchedTutorials");
            }
        }
    }
}