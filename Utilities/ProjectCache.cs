using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VANTAGE.Utilities;

namespace VANTAGE.Utilities
{
    // Static cache of valid ProjectIDs from the Projects table
    // Used by Activity.HasInvalidProjectID for metadata validation
    public static class ProjectCache
    {
        private static HashSet<string> _validProjectIds = new(StringComparer.OrdinalIgnoreCase);
        private static bool _isLoaded = false;
        private static readonly object _lock = new();

        // Check if a ProjectID is valid (exists in Projects table)
        public static bool IsValidProjectId(string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return false;

            EnsureLoaded();
            return _validProjectIds.Contains(projectId);
        }

        // Get count of valid ProjectIDs (for diagnostics)
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _validProjectIds.Count;
            }
        }

        // Force reload from database (call after MirrorTablesFromAzure)
        public static void Reload()
        {
            lock (_lock)
            {
                _isLoaded = false;
                _validProjectIds.Clear();
                LoadFromDatabase();
            }
        }

        // Ensure cache is loaded (lazy initialization)
        private static void EnsureLoaded()
        {
            if (_isLoaded) return;

            lock (_lock)
            {
                if (_isLoaded) return;
                LoadFromDatabase();
            }
        }

        private static void LoadFromDatabase()
        {
            try
            {
                _validProjectIds.Clear();

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT ProjectID FROM Projects";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string projectId = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        _validProjectIds.Add(projectId);
                    }
                }

                _isLoaded = true;
                AppLogger.Info($"ProjectCache loaded {_validProjectIds.Count} valid ProjectIDs", "ProjectCache.LoadFromDatabase");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectCache.LoadFromDatabase");
                // Don't throw - cache will be empty but app can continue
                _isLoaded = true;
            }
        }
    }
}