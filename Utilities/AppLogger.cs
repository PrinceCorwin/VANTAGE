// File: Utilities/AppLogger.cs
using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace VANTAGE.Utilities
{
    public class LogEntry
    {
        public int LogID { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Context { get; set; }
        public string? Username { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
    }

    public static class AppLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error }

        private static readonly object _sync = new object();
        private static bool _initialized = false;
        private static string _logDir = null!;
        private static string _logFilePath = null!;

        // Toggle file logging (DB logging is always attempted)
        public static bool FileLoggingEnabled { get; set; } = true;

        
        /// Call once on startup (e.g., App.xaml.cs OnStartup):
        /// AppLogger.Initialize();
        
        public static void Initialize()
        {
            if (_initialized) return;

            EnsureLogsTable();

            // File log path under LocalAppData\VANTAGE\Logs
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
            if (!_initialized) Initialize(); // safety net

            var nowUtc = DateTime.UtcNow;
            var levelStr = level.ToString();
            var exceptionType = ex?.GetType().FullName;
            var exceptionMessage = ex?.Message;
            var stackTrace = ex?.ToString(); // includes message + stack

            // 1) Try DB write
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS Logs (
                          LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                          TimestampUtc TEXT NOT NULL,
                          Level TEXT NOT NULL,
                          Message TEXT NOT NULL,
                          Context TEXT NULL,
                          Username TEXT NULL,
                          ExceptionType TEXT NULL,
                          ExceptionMessage TEXT NULL,
                          StackTrace TEXT NULL
                      );
                      CREATE INDEX IF NOT EXISTS IX_Logs_TimestampUtc ON Logs(TimestampUtc);";
                cmd.ExecuteNonQuery();

                using var insert = connection.CreateCommand();
                insert.CommandText =
                    @"INSERT INTO Logs
                        (TimestampUtc, Level, Message, Context, Username, ExceptionType, ExceptionMessage, StackTrace)
                      VALUES
                        (@ts, @lvl, @msg, @ctx, @usr, @etype, @emsg, @st);";
                insert.Parameters.AddWithValue("@ts", nowUtc.ToString("o"));
                insert.Parameters.AddWithValue("@lvl", levelStr);
                insert.Parameters.AddWithValue("@msg", message ?? "");
                insert.Parameters.AddWithValue("@ctx", (object?)context ?? DBNull.Value);
                insert.Parameters.AddWithValue("@usr", (object?)username ?? DBNull.Value);
                insert.Parameters.AddWithValue("@etype", (object?)exceptionType ?? DBNull.Value);
                insert.Parameters.AddWithValue("@emsg", (object?)exceptionMessage ?? DBNull.Value);
                insert.Parameters.AddWithValue("@st", (object?)stackTrace ?? DBNull.Value);
                insert.ExecuteNonQuery();
            }
            catch
            {
                // If DB logging fails for any reason, just fall back to file logging below.
            }

            // 2) File write (best-effort)
            if (FileLoggingEnabled)
            {
                try
                {
                    var line = new StringBuilder()
                        .Append('[').Append(nowUtc.ToString("o")).Append("] ")
                        .Append(levelStr).Append(' ')
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
        }

        private static void EnsureLogsTable()
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS Logs (
                          LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                          TimestampUtc TEXT NOT NULL,
                          Level TEXT NOT NULL,
                          Message TEXT NOT NULL,
                          Context TEXT NULL,
                          Username TEXT NULL,
                          ExceptionType TEXT NULL,
                          ExceptionMessage TEXT NULL,
                          StackTrace TEXT NULL
                      );
                      CREATE INDEX IF NOT EXISTS IX_Logs_TimestampUtc ON Logs(TimestampUtc);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // If this fails at very early startup, we still let the app run.
            }
        }

        // Get logs from database with optional filters
        public static List<LogEntry> GetLogs(DateTime? fromDate = null, DateTime? toDate = null, LogLevel? minLevel = null)
        {
            var logs = new List<LogEntry>();

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var sql = new StringBuilder("SELECT LogID, TimestampUtc, Level, Message, Context, Username, ExceptionType, ExceptionMessage, StackTrace FROM Logs WHERE 1=1");

                if (fromDate.HasValue)
                    sql.Append(" AND TimestampUtc >= @fromDate");
                if (toDate.HasValue)
                    sql.Append(" AND TimestampUtc <= @toDate");
                if (minLevel.HasValue)
                {
                    var levels = minLevel.Value switch
                    {
                        LogLevel.Error => "'Error'",
                        LogLevel.Warning => "'Warning','Error'",
                        LogLevel.Info => "'Info','Warning','Error'",
                        _ => "'Debug','Info','Warning','Error'"
                    };
                    sql.Append($" AND Level IN ({levels})");
                }

                sql.Append(" ORDER BY TimestampUtc DESC");

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql.ToString();

                if (fromDate.HasValue)
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.ToString("o"));
                if (toDate.HasValue)
                    cmd.Parameters.AddWithValue("@toDate", toDate.Value.ToString("o"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    logs.Add(new LogEntry
                    {
                        LogID = reader.GetInt32(0),
                        TimestampUtc = DateTime.TryParse(reader.GetString(1), out var ts) ? ts : DateTime.MinValue,
                        Level = reader.GetString(2),
                        Message = reader.GetString(3),
                        Context = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Username = reader.IsDBNull(5) ? null : reader.GetString(5),
                        ExceptionType = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ExceptionMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                        StackTrace = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLogs error: {ex.Message}");
            }

            return logs;
        }

        // Export logs to formatted text
        public static string ExportLogsToText(List<LogEntry> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MILESTONE Log Export - Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Entries: {logs.Count}");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();

            foreach (var log in logs)
            {
                sb.AppendLine($"[{log.TimestampUtc:yyyy-MM-dd HH:mm:ss}] {log.Level}");
                if (!string.IsNullOrEmpty(log.Context))
                    sb.AppendLine($"  Context: {log.Context}");
                if (!string.IsNullOrEmpty(log.Username))
                    sb.AppendLine($"  User: {log.Username}");
                sb.AppendLine($"  Message: {log.Message}");
                if (!string.IsNullOrEmpty(log.ExceptionType))
                {
                    sb.AppendLine($"  Exception: {log.ExceptionType}");
                    sb.AppendLine($"  {log.ExceptionMessage}");
                    if (!string.IsNullOrEmpty(log.StackTrace))
                        sb.AppendLine($"  {log.StackTrace}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
