using System;
using System.Collections.Generic;
using System.Linq;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Service that provides column mappings (default or project-specific)
    /// </summary>
    public class MappingService
    {
        private readonly string _projectID;
        private Dictionary<string, ColumnMappingInfo> _mappings;

        public MappingService(string projectID = null)
        {
            _projectID = projectID;
            LoadMappings();
        }

        /// <summary>
        /// Get property name from database column name
        /// </summary>
        public string GetPropertyName(string dbColumnName)
        {
            if (_mappings.TryGetValue(dbColumnName, out var mapping))
            {
                return mapping.PropertyName;
            }

            // Fallback to ColumnMapper default
            return ColumnMapper.GetPropertyName(dbColumnName);
        }

        /// <summary>
        /// Get database column name from property name
        /// </summary>
        public string GetDbColumnName(string propertyName)
        {
            var mapping = _mappings.Values.FirstOrDefault(m => m.PropertyName == propertyName);
            if (mapping != null)
            {
                return mapping.DbColumnName;
            }

            // Fallback to ColumnMapper default
            return ColumnMapper.GetDbColumnName(propertyName);
        }

        /// <summary>
        /// Get display name for a database column
        /// </summary>
        public string GetDisplayName(string dbColumnName)
        {
            if (_mappings.TryGetValue(dbColumnName, out var mapping))
            {
                return mapping.DisplayName;
            }

            // Fallback: Use property name as display name
            return GetPropertyName(dbColumnName);
        }

        /// <summary>
        /// Check if column should be visible
        /// </summary>
        public bool IsVisible(string dbColumnName)
        {
            if (_mappings.TryGetValue(dbColumnName, out var mapping))
            {
                return mapping.IsVisible;
            }

            // Default: visible
            return true;
        }

        /// <summary>
        /// Get column order
        /// </summary>
        public int GetColumnOrder(string dbColumnName)
        {
            if (_mappings.TryGetValue(dbColumnName, out var mapping))
            {
                return mapping.ColumnOrder;
            }

            return 0;
        }

        /// <summary>
        /// Get column width
        /// </summary>
        public int GetColumnWidth(string dbColumnName)
        {
            if (_mappings.TryGetValue(dbColumnName, out var mapping))
            {
                return mapping.Width;
            }

            return 100; // Default width
        }

        /// <summary>
        /// Load mappings from database or use defaults
        /// </summary>
        private void LoadMappings()
        {
            _mappings = new Dictionary<string, ColumnMappingInfo>();

            if (string.IsNullOrEmpty(_projectID))
            {
                // No project specified, use defaults only
                System.Diagnostics.Debug.WriteLine("→ Using default column mappings (no project specified)");
                return;
            }

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DbColumnName, DisplayName, PropertyName, IsVisible, ColumnOrder, Width
                    FROM ProjectColumnOverrides
                    WHERE ProjectID = @projectID";
                command.Parameters.AddWithValue("@projectID", _projectID);

                using var reader = command.ExecuteReader();
                int count = 0;

                while (reader.Read())
                {
                    var dbColumnName = reader.GetString(0);
                    _mappings[dbColumnName] = new ColumnMappingInfo
                    {
                        DbColumnName = dbColumnName,
                        DisplayName = reader.GetString(1),
                        PropertyName = reader.GetString(2),
                        IsVisible = reader.GetInt32(3) == 1,
                        ColumnOrder = reader.GetInt32(4),
                        Width = reader.GetInt32(5)
                    };
                    count++;
                }

                if (count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {count} custom column mappings for project '{_projectID}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"→ No custom mappings found for project '{_projectID}', using defaults");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Error loading project mappings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("→ Falling back to default mappings");
            }
        }

        /// <summary>
        /// Get all database column names in display order
        /// </summary>
        public IEnumerable<string> GetAllDbColumnNames()
        {
            if (_mappings.Any())
            {
                return _mappings.Values
                    .OrderBy(m => m.ColumnOrder)
                    .Select(m => m.DbColumnName);
            }

            // Fallback to ColumnMapper defaults
            return ColumnMapper.GetAllDbColumnNames();
        }
    }

    /// <summary>
    /// Column mapping information
    /// </summary>
    public class ColumnMappingInfo
    {
        public string DbColumnName { get; set; }
        public string DisplayName { get; set; }
        public string PropertyName { get; set; }
        public bool IsVisible { get; set; }
        public int ColumnOrder { get; set; }
        public int Width { get; set; }
    }
}