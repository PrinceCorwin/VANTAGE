using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using VANTAGE.Utilities;

namespace VANTAGE.Services
{
    // Context containing data needed for token resolution
    public class TokenContext
    {
        public string ProjectID { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public string PKGManagerUsername { get; set; } = string.Empty;
        public string PKGManagerFullName { get; set; } = string.Empty;
        public string SchedulerUsername { get; set; } = string.Empty;
        public string SchedulerFullName { get; set; } = string.Empty;
        public string WPNamePattern { get; set; } = string.Empty;
        public int ExpirationDays { get; set; } = 14;
        public string OutputFolder { get; set; } = string.Empty;

        // Cached values from database (lazy loaded)
        private Dictionary<string, string>? _resolvedTokens;

        public Dictionary<string, string> ResolvedTokens
        {
            get
            {
                _resolvedTokens ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return _resolvedTokens;
            }
        }
    }

    // Service for resolving {tokens} in template text with actual values
    public static class TokenResolver
    {
        private static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        // Resolve all tokens in the given text using the provided context
        public static string Resolve(string text, TokenContext context)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Load database values if not already loaded
            if (context.ResolvedTokens.Count == 0)
            {
                LoadTokenValues(context);
            }

            // Replace all tokens
            return TokenPattern.Replace(text, match =>
            {
                var tokenName = match.Groups[1].Value;
                return ResolveToken(tokenName, context);
            });
        }

        // Resolve WP Name pattern (special handling because it may contain activity field tokens)
        public static string ResolveWPName(TokenContext context)
        {
            if (string.IsNullOrEmpty(context.WPNamePattern))
                return context.WorkPackage;

            return Resolve(context.WPNamePattern, context);
        }

        // Load all token values from database
        private static void LoadTokenValues(TokenContext context)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                connection.Open();

                // Load project info
                LoadProjectInfo(connection, context);

                // Load activity-related tokens
                LoadActivityTokens(connection, context);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TokenResolver.LoadTokenValues");
            }
        }

        // Load project info (ProjectName, Phone, Fax)
        private static void LoadProjectInfo(SqliteConnection connection, TokenContext context)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Description, Phone, Fax, ProjectManager, SiteManager
                FROM Projects
                WHERE ProjectID = @projectId";
            cmd.Parameters.AddWithValue("@projectId", context.ProjectID);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                context.ResolvedTokens["ProjectName"] = reader.GetString(0);
                context.ResolvedTokens["Phone"] = reader.IsDBNull(1) ? "" : reader.GetString(1);
                context.ResolvedTokens["Fax"] = reader.IsDBNull(2) ? "" : reader.GetString(2);
                context.ResolvedTokens["ProjectManager"] = reader.IsDBNull(3) ? "" : reader.GetString(3);
                context.ResolvedTokens["SiteManager"] = reader.IsDBNull(4) ? "" : reader.GetString(4);
            }
        }

        // Fields that resolve to a comma-joined list of all distinct non-empty values
        // for the work package. Every other Activities column resolves to the first
        // distinct value (alphabetical), suitable for header titles.
        private static readonly HashSet<string> CommaSeparatedFields =
            new(StringComparer.OrdinalIgnoreCase) { "SchedActNO", "PhaseCode" };

        // Cached list of Activities table column names. Populated once per process from
        // PRAGMA table_info. Used as the allowlist of token names that map to columns,
        // so users can reference any column from the grid (Estimator, DwgNO, UDF11, etc.)
        // without the resolver hardcoding a list.
        private static List<string>? _activityColumns;
        private static readonly object _activityColumnsLock = new();

        private static List<string> GetActivityColumns(SqliteConnection connection)
        {
            if (_activityColumns != null) return _activityColumns;

            lock (_activityColumnsLock)
            {
                if (_activityColumns != null) return _activityColumns;

                var cols = new List<string>();
                try
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "PRAGMA table_info('Activities')";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        cols.Add(reader.GetString(1));
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TokenResolver.GetActivityColumns");
                }

                _activityColumns = cols;
                return cols;
            }
        }

        // Load distinct values for every Activities column for the given WorkPackage.
        // Single wide query instead of one query per field — better for large datasets.
        private static void LoadActivityTokens(SqliteConnection connection, TokenContext context)
        {
            var columns = GetActivityColumns(connection);
            if (columns.Count == 0) return;

            // Column names come from PRAGMA (trusted) but we still bracket-quote defensively.
            var bracketed = string.Join(", ", columns.Select(c => $"[{c}]"));

            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT {bracketed} FROM Activities WHERE WorkPackage = @wp";
                cmd.Parameters.AddWithValue("@wp", context.WorkPackage);

                // Per-column distinct sets (case-sensitive, preserve insertion order)
                var seen = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in columns)
                {
                    seen[col] = new HashSet<string>(StringComparer.Ordinal);
                    values[col] = new List<string>();
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (reader.IsDBNull(i)) continue;
                        var val = reader.GetValue(i)?.ToString();
                        if (string.IsNullOrEmpty(val)) continue;

                        if (seen[columns[i]].Add(val))
                        {
                            values[columns[i]].Add(val);
                        }
                    }
                }

                // Sort each column's values for deterministic resolution
                foreach (var col in columns)
                {
                    var sorted = values[col];
                    sorted.Sort(StringComparer.Ordinal);

                    if (CommaSeparatedFields.Contains(col))
                    {
                        context.ResolvedTokens[col] = string.Join(", ", sorted);
                    }
                    else
                    {
                        context.ResolvedTokens[col] = sorted.Count > 0 ? sorted[0] : "";
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TokenResolver.LoadActivityTokens");
            }
        }

        // Resolve a single token
        private static string ResolveToken(string tokenName, TokenContext context)
        {
            // Special built-in tokens
            switch (tokenName.ToUpperInvariant())
            {
                case "PRINTEDDATE":
                    return DateTime.Now.ToString("MM/dd/yyyy");

                case "EXPIRATIONDATE":
                    return DateTime.Now.AddDays(context.ExpirationDays).ToString("MM/dd/yyyy");

                case "WORKPACKAGE":
                    return context.WorkPackage;

                case "WPNAME":
                    // Resolve the WP Name pattern (recursive, but WPName won't appear in pattern)
                    return ResolveWPName(context);

                case "PKGMANAGER":
                    return context.PKGManagerFullName;

                case "SCHEDULER":
                    return context.SchedulerFullName;

                case "CURRENTUSER":
                    return App.CurrentUser?.FullName ?? "Unknown";

                case "CURRENTDATE":
                    return DateTime.Now.ToString("MM/dd/yyyy");

                case "CURRENTTIME":
                    return DateTime.Now.ToString("hh:mm tt");
            }

            // Check resolved tokens from database
            if (context.ResolvedTokens.TryGetValue(tokenName, out var value))
            {
                return value;
            }

            // Token not found - return as-is for debugging
            return $"{{{tokenName}}}";
        }

        // Get placeholder data for preview when no project/WP selected
        public static TokenContext GetPlaceholderContext()
        {
            return new TokenContext
            {
                ProjectID = "SAMPLE",
                WorkPackage = "50.MOD1.A",
                PKGManagerUsername = "pkgmgr",
                PKGManagerFullName = "Package Manager",
                SchedulerUsername = "scheduler",
                SchedulerFullName = "Scheduler",
                WPNamePattern = "Sample Work Package",
                ExpirationDays = 14,
                ResolvedTokens =
                {
                    ["ProjectName"] = "Sample Project",
                    ["Phone"] = "(555) 123-4567",
                    ["Fax"] = "(555) 123-4568",
                    ["SchedActNO"] = "ACT-001, ACT-002",
                    ["PhaseCode"] = "PIPE",
                    ["Area"] = "AREA-1",
                    ["PjtSystemNo"] = "SYS-100",
                    ["ProjectManager"] = "John Smith",
                    ["SiteManager"] = "Jane Doe"
                }
            };
        }
    }
}
