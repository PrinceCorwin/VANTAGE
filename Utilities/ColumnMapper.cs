using System;
using System.Collections.Generic;
using System.Linq;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Maps column names for Excel/Azure import/export ONLY
    /// Database now uses NewVantage names directly - no translation needed!
    /// </summary>
    public static class ColumnMapper
    {
        // Cache for mappings loaded from database
        private static Dictionary<string, (string OldVantage, string Azure)> _mappings;
     private static bool _isLoaded = false;

        /// <summary>
        /// Load mappings from ColumnMappings table
        /// </summary>
        private static void LoadMappingsFromDatabase()
        {
 if (_isLoaded) return;

            _mappings = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

            try
    {
         using var connection = DatabaseSetup.GetConnection();
 connection.Open();

         var cmd = connection.CreateCommand();
       cmd.CommandText = "SELECT ColumnName, OldVantageName, AzureName FROM ColumnMappings";

         using var reader = cmd.ExecuteReader();
   while (reader.Read())
       {
             string colName = reader.GetString(0);
  string oldName = reader.IsDBNull(1) ? null : reader.GetString(1);
        string azureName = reader.IsDBNull(2) ? null : reader.GetString(2);

            _mappings[colName] = (oldName, azureName);
         }

          _isLoaded = true;
        }
     catch (Exception ex)
  {
         System.Diagnostics.Debug.WriteLine($"✗ Error loading column mappings: {ex.Message}");
        // Initialize empty to prevent repeated failures
         _mappings = new Dictionary<string, (string, string)>();
            _isLoaded = true;
    }
  }

        /// <summary>
        /// For Excel export: NewVantage name → OldVantage name
        /// Example: "ProjectID" → "Tag_ProjectID"
        /// </summary>
        public static string GetOldVantageName(string newVantageName)
        {
            LoadMappingsFromDatabase();

         if (_mappings.TryGetValue(newVantageName, out var tuple) && tuple.OldVantage != null)
   {
                return tuple.OldVantage;
      }

            return newVantageName; // Return as-is if no mapping
        }

        /// <summary>
        /// For Azure upload: NewVantage name → Azure name
        /// Example: "ProjectID" → "Tag_ProjectID"
        /// </summary>
        public static string GetAzureName(string newVantageName)
      {
   LoadMappingsFromDatabase();

     if (_mappings.TryGetValue(newVantageName, out var tuple) && tuple.Azure != null)
{
return tuple.Azure;
       }

        return newVantageName; // Return as-is if no mapping
  }

        /// <summary>
        /// For Excel import: OldVantage name → NewVantage name
        /// Example: "Tag_ProjectID" → "ProjectID"
        /// </summary>
   public static string GetColumnNameFromOldVantage(string oldVantageName)
        {
            LoadMappingsFromDatabase();

            var entry = _mappings.FirstOrDefault(kvp =>
            kvp.Value.OldVantage != null &&
     kvp.Value.OldVantage.Equals(oldVantageName, StringComparison.OrdinalIgnoreCase));

      return entry.Key ?? oldVantageName; // Return as-is if no mapping
        }

        /// <summary>
        /// For Azure sync: Azure name → NewVantage name
        /// Example: "Tag_ProjectID" → "ProjectID"
     /// </summary>
 public static string GetColumnNameFromAzure(string azureName)
      {
            LoadMappingsFromDatabase();

            var entry = _mappings.FirstOrDefault(kvp =>
             kvp.Value.Azure != null &&
       kvp.Value.Azure.Equals(azureName, StringComparison.OrdinalIgnoreCase));

            return entry.Key ?? azureName; // Return as-is if no mapping
        }

        /// <summary>
   /// DEPRECATED: Database columns now match property names directly
     /// This method is kept for backward compatibility but just returns the input
        /// </summary>
        [Obsolete("Database now uses NewVantage column names directly. This method will be removed in Phase 7.")]
        public static string GetDbColumnName(string propertyName)
        {
      // Database columns now match property names - no translation needed!
 return propertyName;
}

        /// <summary>
/// DEPRECATED: Database columns now match property names directly
        /// This method is kept for backward compatibility but just returns the input
        /// </summary>
        [Obsolete("Database now uses NewVantage column names directly. This method will be removed in Phase 7.")]
        public static string GetPropertyName(string dbColumnName)
        {
            // Database columns now match property names - no translation needed!
   return dbColumnName;
        }

     /// <summary>
  /// DEPRECATED: Use ColumnMappings table queries instead
        /// </summary>
        [Obsolete("Use ColumnMappings table queries instead. This method will be removed in Phase 7.")]
        public static bool IsValidDbColumn(string dbColumnName)
        {
         LoadMappingsFromDatabase();
return _mappings.ContainsKey(dbColumnName);
        }

   /// <summary>
        /// DEPRECATED: Use ColumnMappings table queries instead
    /// </summary>
      [Obsolete("Use ColumnMappings table queries instead. This method will be removed in Phase 7.")]
        public static IEnumerable<string> GetAllDbColumnNames()
        {
LoadMappingsFromDatabase();
            return _mappings.Keys;
    }
    }
}