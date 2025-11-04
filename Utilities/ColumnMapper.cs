using System;
using System.Collections.Generic;
using System.Linq;

namespace VANTAGE.Utilities
{
    
    /// Maps column names for Excel/Azure import/export ONLY
    /// Database now uses NewVantage names directly - no translation needed!
    
    public static class ColumnMapper
    {
        // Cache for mappings loaded from database
        private static Dictionary<string, (string OldVantage, string Azure)> _mappings;
     private static bool _isLoaded = false;

     // Add a second mapping for OldVantageName -> NewVantage/ColumnName
     private static Dictionary<string, string> _oldToNewMapping;

        
        /// Load mappings from ColumnMappings table
        
        private static void LoadMappingsFromDatabase()
        {
 if (_isLoaded) return;

            _mappings = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
 _oldToNewMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
    {
         using var connection = DatabaseSetup.GetConnection();
 connection.Open();

         var cmd = connection.CreateCommand();
       cmd.CommandText = "SELECT ColumnName, OldVantageName, AzureName FROM ColumnMappings";

         using var reader = cmd.ExecuteReader();
   while (reader.Read())
       {
             string colName = reader.IsDBNull(0) ? null : reader.GetString(0);
  string oldName = reader.IsDBNull(1) ? null : reader.GetString(1);
        string azureName = reader.IsDBNull(2) ? null : reader.GetString(2);

            if (!string.IsNullOrEmpty(colName))
 _mappings[colName] = (oldName, azureName);
            if (!string.IsNullOrEmpty(oldName) && !string.IsNullOrEmpty(colName))
 _oldToNewMapping[oldName] = colName;
         }

          _isLoaded = true;
        }
     catch (Exception ex)
  {
         System.Diagnostics.Debug.WriteLine($"✗ Error loading column mappings: {ex.Message}");
        // Initialize empty to prevent repeated failures
         _mappings = new Dictionary<string, (string, string)>();
 _oldToNewMapping = new Dictionary<string, string>();
            _isLoaded = true;
    }
  }

        
        /// For Excel export: NewVantage name → OldVantage name
        /// Example: "ProjectID" → "Tag_ProjectID"
        
        public static string GetOldVantageName(string newVantageName)
        {
            LoadMappingsFromDatabase();

         if (_mappings.TryGetValue(newVantageName, out var tuple) && tuple.OldVantage != null)
   {
                return tuple.OldVantage;
      }

            return newVantageName; // Return as-is if no mapping
        }

        
        /// For Azure upload: NewVantage name → Azure name
        /// Example: "ProjectID" → "Tag_ProjectID"
        
        public static string GetAzureName(string newVantageName)
      {
   LoadMappingsFromDatabase();

     if (_mappings.TryGetValue(newVantageName, out var tuple) && tuple.Azure != null)
{
return tuple.Azure;
       }

        return newVantageName; // Return as-is if no mapping
  }

        
        /// For Excel import: OldVantage name → NewVantage name
        /// Example: "Tag_ProjectID" → "ProjectID"
        
   public static string GetColumnNameFromOldVantage(string oldVantageName)
        {
            LoadMappingsFromDatabase();
   if (_oldToNewMapping.TryGetValue(oldVantageName, out var newName))
 return newName;
            return oldVantageName; // Return as-is if no mapping
        }

        
        /// For Azure sync: Azure name → NewVantage name
        /// Example: "Tag_ProjectID" → "ProjectID"
     
 public static string GetColumnNameFromAzure(string azureName)
      {
            LoadMappingsFromDatabase();

            var entry = _mappings.FirstOrDefault(kvp =>
             kvp.Value.Azure != null &&
       kvp.Value.Azure.Equals(azureName, StringComparison.OrdinalIgnoreCase));

            return entry.Key ?? azureName; // Return as-is if no mapping
        }

        
   /// DEPRECATED: Database columns now match property names directly
     /// This method is kept for backward compatibility but just returns the input
        
        [Obsolete("Database now uses NewVantage column names directly. This method will be removed in Phase 7.")]
        public static string GetDbColumnName(string propertyName)
        {
      // Database columns now match property names - no translation needed!
 return propertyName;
}

        
/// DEPRECATED: Database columns now match property names directly
        /// This method is kept for backward compatibility but just returns the input
        
        [Obsolete("Database now uses NewVantage column names directly. This method will be removed in Phase 7.")]
        public static string GetPropertyName(string dbColumnName)
        {
            // Database columns now match property names - no translation needed!
   return dbColumnName;
        }

     
  /// DEPRECATED: Use ColumnMappings table queries instead
        
        [Obsolete("Use ColumnMappings table queries instead. This method will be removed in Phase 7.")]
        public static bool IsValidDbColumn(string dbColumnName)
        {
         LoadMappingsFromDatabase();
return _mappings.ContainsKey(dbColumnName);
        }

   
        /// DEPRECATED: Use ColumnMappings table queries instead
    
      [Obsolete("Use ColumnMappings table queries instead. This method will be removed in Phase 7.")]
        public static IEnumerable<string> GetAllDbColumnNames()
        {
LoadMappingsFromDatabase();
            return _mappings.Keys;
    }
    }
}