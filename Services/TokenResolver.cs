using System;
using System.Collections.Generic;
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

        // Load distinct values from Activities for the given WorkPackage
        private static void LoadActivityTokens(SqliteConnection connection, TokenContext context)
        {
            // SchedActNO - comma-separated list of all distinct values
            var schedActNOs = LoadDistinctValues(connection, "SchedActNO", context.WorkPackage);
            context.ResolvedTokens["SchedActNO"] = string.Join(", ", schedActNOs);

            // PhaseCode - comma-separated list of all distinct values
            var phaseCodes = LoadDistinctValues(connection, "PhaseCode", context.WorkPackage);
            context.ResolvedTokens["PhaseCode"] = string.Join(", ", phaseCodes);

            // Load first distinct value for other activity fields (for WP Name pattern)
            var singleValueFields = new[] { "Area", "SystemNO", "UDF1", "UDF2", "UDF3", "UDF4", "UDF5",
                "UDF6", "UDF7", "UDF8", "UDF9", "UDF10", "CompType", "PhaseCategory", "SubArea" };

            foreach (var field in singleValueFields)
            {
                var values = LoadDistinctValues(connection, field, context.WorkPackage);
                context.ResolvedTokens[field] = values.Count > 0 ? values[0] : "";
            }
        }

        // Load distinct values for a field from Activities table
        private static List<string> LoadDistinctValues(SqliteConnection connection, string fieldName, string workPackage)
        {
            var values = new List<string>();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT DISTINCT {fieldName}
                    FROM Activities
                    WHERE WorkPackage = @workPackage
                      AND {fieldName} IS NOT NULL
                      AND {fieldName} != ''
                    ORDER BY {fieldName}";
                cmd.Parameters.AddWithValue("@workPackage", workPackage);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        values.Add(reader.GetString(0));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"TokenResolver.LoadDistinctValues({fieldName})");
            }
            return values;
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
                    ["SystemNO"] = "SYS-100",
                    ["ProjectManager"] = "John Smith",
                    ["SiteManager"] = "Jane Doe"
                }
            };
        }
    }
}
