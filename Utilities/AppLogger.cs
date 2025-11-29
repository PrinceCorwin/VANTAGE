// File: Utilities/AppLogger.cs
using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace VANTAGE.Utilities
{
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
    }
}
