// File: Utilities/AppLogger.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VANTAGE.Utilities
{
    // Flat-file logger. One file per UTC day: %LocalAppData%\VANTAGE\Logs\app-yyyyMMdd.log
    // No SQLite involvement — the Logs table was removed in schema v12. Export Logs
    // dialog reads these files directly via ReadLogFilesAsText.
    public static class AppLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error }

        private static readonly object _sync = new object();
        private static bool _initialized = false;
        private static string _logDir = null!;
        private static string _logFilePath = null!;

        public static bool FileLoggingEnabled { get; set; } = true;

        public static void Initialize()
        {
            if (_initialized) return;

            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDir = Path.Combine(localApp, "VANTAGE", "Logs");
            Directory.CreateDirectory(_logDir);
            _logFilePath = Path.Combine(_logDir, $"app-{DateTime.UtcNow:yyyyMMdd}.log");

            _initialized = true;
            Info("AppLogger initialized.");
        }

        public static void Debug(string message, string? context = null, string? username = null)
            => Write(LogLevel.Debug, message, context, username);

        public static void Info(string message, string? context = null, string? username = null)
            => Write(LogLevel.Info, message, context, username);

        public static void Warning(string message, string? context = null, string? username = null)
            => Write(LogLevel.Warning, message, context, username);

        public static void Error(string message, string? context = null, string? username = null, Exception? ex = null)
            => Write(LogLevel.Error, message, context, username, ex);

        public static void Error(Exception? ex, string? context = null, string? username = null, string? message = null)
            => Write(LogLevel.Error, message ?? ex?.Message ?? "Unhandled exception", context, username, ex);

        private static void Write(LogLevel level, string message, string? context, string? username, Exception? ex = null)
        {
            if (!_initialized) Initialize();
            if (!FileLoggingEnabled) return;

            var nowUtc = DateTime.UtcNow;

            try
            {
                var line = new StringBuilder()
                    .Append('[').Append(nowUtc.ToString("o")).Append("] ")
                    .Append(level.ToString()).Append(' ')
                    .Append(context != null ? $"[{context}] " : "")
                    .Append(username != null ? $"(user:{username}) " : "")
                    .Append(message ?? "")
                    .ToString();

                lock (_sync)
                {
                    // rotate daily silently
                    var desired = Path.Combine(_logDir, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
                    if (!string.Equals(_logFilePath, desired, StringComparison.OrdinalIgnoreCase))
                        _logFilePath = desired;

                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                    if (ex != null)
                        File.AppendAllText(_logFilePath, ex + Environment.NewLine);
                }
            }
            catch
            {
                // swallow: logging must never crash the app
            }
        }

        // Purges log files older than the specified number of days.
        // DB purge is gone — schema v12 dropped the Logs table.
        public static void PurgeOldLogs(int daysToKeep = 15)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            PurgeOldLogFiles(cutoffDate, daysToKeep);
        }

        // Deletes log files older than cutoff date by parsing the filename (app-yyyyMMdd.log)
        private static void PurgeOldLogFiles(DateTime cutoffDate, int daysToKeep)
        {
            try
            {
                if (!_initialized || string.IsNullOrEmpty(_logDir) || !Directory.Exists(_logDir))
                    return;

                var files = Directory.GetFiles(_logDir, "app-*.log");
                int purgedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName.Length >= 12 && fileName.StartsWith("app-"))
                        {
                            string dateStr = fileName.Substring(4, 8);
                            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null,
                                System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                            {
                                if (fileDate < cutoffDate.Date)
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
                    Info($"Purged {purgedCount} log file(s) older than {daysToKeep} days", "AppLogger.PurgeOldLogFiles");
                }
            }
            catch
            {
                // Don't let purge failures affect app startup
            }
        }

        // Reads log files in the given date range, optionally filtered by minimum level,
        // and returns the concatenated text ready for export or email attachment.
        // fromDate/toDate are compared against the file's date token (app-yyyyMMdd.log);
        // null means "no bound on that side".
        // Exception continuation lines (not starting with a [timestamp] Level prefix) are
        // kept or dropped together with their preceding log line.
        public static string ReadLogFilesAsText(DateTime? fromDate, DateTime? toDate, LogLevel? minLevel)
        {
            if (!_initialized) Initialize();
            if (string.IsNullOrEmpty(_logDir) || !Directory.Exists(_logDir))
                return string.Empty;

            var allowedLevels = GetAllowedLevels(minLevel);
            var files = GetLogFilesInRange(fromDate, toDate);

            var result = new StringBuilder();
            result.AppendLine($"VANTAGE: Milestone Log Export - Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            result.AppendLine($"Source: {_logDir}");
            result.AppendLine($"Files: {files.Count}");
            result.AppendLine(new string('=', 80));
            result.AppendLine();

            foreach (var file in files)
            {
                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                bool currentIncluded = false;
                foreach (var line in lines)
                {
                    if (TryExtractLevel(line, out var lineLevel))
                    {
                        currentIncluded = allowedLevels == null || allowedLevels.Contains(lineLevel);
                        if (currentIncluded)
                            result.AppendLine(line);
                    }
                    else if (currentIncluded)
                    {
                        // Exception stack or other continuation line — include if owning log was included.
                        result.AppendLine(line);
                    }
                }
            }

            return result.ToString();
        }

        // Counts log entry lines in pre-filtered log text. "Entries" = lines that start with
        // [timestamp] Level (i.e. exception continuation lines don't inflate the count).
        public static void CountEntriesByLevel(string logText, out int total, out int errors, out int warnings)
        {
            total = 0;
            errors = 0;
            warnings = 0;
            if (string.IsNullOrEmpty(logText)) return;

            foreach (var line in logText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryExtractLevel(line, out var level))
                {
                    total++;
                    if (level == "Error") errors++;
                    else if (level == "Warning") warnings++;
                }
            }
        }

        // Checks whether a line starts with "[timestamp] Level " and extracts the level.
        // Returns false for exception continuation lines and malformed lines.
        private static bool TryExtractLevel(string line, out string level)
        {
            level = "";
            if (line.Length < 30 || line[0] != '[') return false;

            int closeBracket = line.IndexOf(']');
            if (closeBracket <= 0 || closeBracket + 2 >= line.Length) return false;

            int afterBracket = closeBracket + 2;
            int spaceAfterLevel = line.IndexOf(' ', afterBracket);
            if (spaceAfterLevel <= afterBracket) return false;

            var lvl = line.Substring(afterBracket, spaceAfterLevel - afterBracket);
            if (lvl == "Debug" || lvl == "Info" || lvl == "Warning" || lvl == "Error")
            {
                level = lvl;
                return true;
            }
            return false;
        }

        private static HashSet<string>? GetAllowedLevels(LogLevel? minLevel)
        {
            if (!minLevel.HasValue) return null; // all levels allowed
            return minLevel.Value switch
            {
                LogLevel.Error => new HashSet<string> { "Error" },
                LogLevel.Warning => new HashSet<string> { "Warning", "Error" },
                LogLevel.Info => new HashSet<string> { "Info", "Warning", "Error" },
                _ => null
            };
        }

        private static List<string> GetLogFilesInRange(DateTime? fromDate, DateTime? toDate)
        {
            var fromDay = fromDate?.Date;
            var toDay = toDate?.Date;

            var files = new List<(DateTime date, string path)>();
            foreach (var path in Directory.GetFiles(_logDir, "app-*.log"))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Length < 12 || !fileName.StartsWith("app-")) continue;

                if (!DateTime.TryParseExact(fileName.Substring(4, 8), "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                    continue;

                if (fromDay.HasValue && fileDate < fromDay.Value) continue;
                if (toDay.HasValue && fileDate > toDay.Value) continue;

                files.Add((fileDate, path));
            }
            return files.OrderBy(f => f.date).Select(f => f.path).ToList();
        }
    }
}
