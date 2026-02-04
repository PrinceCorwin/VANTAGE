using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Logs changes made in the Schedule detail grid to JSON files for later review and optional application to Activities
    public static class ScheduleChangeLogger
    {
        // Save to same location as AppLogger: %LocalAppData%\VANTAGE\Logs\ScheduleChanges
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VANTAGE", "Logs", "ScheduleChanges");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        // Logs a single change to the appropriate log file based on WeekEndDate
        public static void LogChange(
            string weekEndDate,
            string uniqueId,
            string schedActNo,
            string description,
            string field,
            string? oldValue,
            string? newValue,
            string username)
        {
            try
            {
                EnsureDirectoryExists();

                var entry = new ScheduleChangeLogEntry
                {
                    Timestamp = DateTime.Now,
                    Username = username,
                    WeekEndDate = weekEndDate,
                    UniqueID = uniqueId,
                    SchedActNO = schedActNo,
                    Description = description,
                    Field = field,
                    OldValue = oldValue ?? string.Empty,
                    NewValue = newValue ?? string.Empty
                };

                string fileName = GetLogFileName(weekEndDate);
                string filePath = Path.Combine(LogDirectory, fileName);

                // Load existing entries or create new list
                var entries = new List<ScheduleChangeLogEntry>();
                if (File.Exists(filePath))
                {
                    string existingJson = File.ReadAllText(filePath);
                    var existing = JsonSerializer.Deserialize<List<ScheduleChangeLogEntry>>(existingJson);
                    if (existing != null)
                        entries = existing;
                }

                entries.Add(entry);

                string json = JsonSerializer.Serialize(entries, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogger.LogChange");
            }
        }

        // Reads all change log entries from all log files
        public static List<ScheduleChangeLogEntry> ReadAllEntries()
        {
            var allEntries = new List<ScheduleChangeLogEntry>();

            try
            {
                if (!Directory.Exists(LogDirectory))
                    return allEntries;

                var files = Directory.GetFiles(LogDirectory, "ScheduleChanges_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var entries = JsonSerializer.Deserialize<List<ScheduleChangeLogEntry>>(json);
                        if (entries != null)
                            allEntries.AddRange(entries);
                    }
                    catch
                    {
                        // Skip corrupted files
                    }
                }

                // Sort by timestamp descending (most recent first)
                allEntries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogger.ReadAllEntries");
            }

            return allEntries;
        }

        // Removes entries that have been applied to Activities
        public static void RemoveAppliedEntries(List<ScheduleChangeLogEntry> appliedEntries)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                // Group applied entries by their log file
                var entriesByFile = new Dictionary<string, HashSet<string>>();
                foreach (var entry in appliedEntries)
                {
                    string fileName = GetLogFileName(entry.WeekEndDate);
                    if (!entriesByFile.ContainsKey(fileName))
                        entriesByFile[fileName] = new HashSet<string>();

                    // Create unique key for each entry
                    string key = $"{entry.Timestamp:O}|{entry.UniqueID}|{entry.Field}";
                    entriesByFile[fileName].Add(key);
                }

                // Process each file
                foreach (var kvp in entriesByFile)
                {
                    string filePath = Path.Combine(LogDirectory, kvp.Key);
                    if (!File.Exists(filePath))
                        continue;

                    string json = File.ReadAllText(filePath);
                    var entries = JsonSerializer.Deserialize<List<ScheduleChangeLogEntry>>(json);
                    if (entries == null)
                        continue;

                    // Remove applied entries
                    entries.RemoveAll(e =>
                    {
                        string key = $"{e.Timestamp:O}|{e.UniqueID}|{e.Field}";
                        return kvp.Value.Contains(key);
                    });

                    if (entries.Count == 0)
                    {
                        // Delete empty file
                        File.Delete(filePath);
                    }
                    else
                    {
                        // Write remaining entries
                        string updatedJson = JsonSerializer.Serialize(entries, JsonOptions);
                        File.WriteAllText(filePath, updatedJson);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogger.RemoveAppliedEntries");
            }
        }

        // Deletes log files older than the specified number of days based on filename date
        public static void PurgeOldLogs(int daysToKeep = 30)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep).Date;
                var files = Directory.GetFiles(LogDirectory, "ScheduleChanges_*.json");
                int purgedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        // Extract date from filename: ScheduleChanges_yyyy-MM-dd.json
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName.Length >= 26 && fileName.StartsWith("ScheduleChanges_"))
                        {
                            string dateStr = fileName.Substring(16, 10);
                            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null,
                                System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                            {
                                if (fileDate < cutoffDate)
                                {
                                    File.Delete(file);
                                    purgedCount++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be processed
                    }
                }

                if (purgedCount > 0)
                {
                    AppLogger.Info($"Purged {purgedCount} schedule change log(s) older than {daysToKeep} days",
                        "ScheduleChangeLogger.PurgeOldLogs");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogger.PurgeOldLogs");
            }
        }

        // Returns true if there are any change log entries
        public static bool HasEntries()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return false;

                var files = Directory.GetFiles(LogDirectory, "ScheduleChanges_*.json");
                return files.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // Deletes all change log files (called when importing new P6 schedule)
        public static void ClearAll()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                var files = Directory.GetFiles(LogDirectory, "ScheduleChanges_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Skip files that can't be deleted
                    }
                }

                if (files.Length > 0)
                {
                    AppLogger.Info($"Cleared {files.Length} schedule change log file(s)",
                        "ScheduleChangeLogger.ClearAll");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogger.ClearAll");
            }
        }

        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
        }

        private static string GetLogFileName(string weekEndDate)
        {
            // Parse the date and format as yyyy-MM-dd for consistent file naming
            if (DateTime.TryParse(weekEndDate, out DateTime date))
                return $"ScheduleChanges_{date:yyyy-MM-dd}.json";

            // Fallback if date parsing fails
            return $"ScheduleChanges_{weekEndDate.Replace("/", "-")}.json";
        }
    }
}
