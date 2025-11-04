using System;

namespace VANTAGE.Utilities
{
    
    /// Helper methods for building SQL queries
    
    public static class SqlHelper
    {
        
        /// Wrap column name in brackets if it contains spaces or hyphens
        
        public static string WrapColumnName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return columnName;

            // Check if column name needs brackets (has spaces or hyphens)
            if (columnName.Contains(" ") || columnName.Contains("-"))
            {
                return $"[{columnName}]";
            }

            return columnName;
        }

        
        /// Wrap multiple column names for SELECT statements
        
        public static string WrapColumnNames(params string[] columnNames)
        {
            return string.Join(", ", Array.ConvertAll(columnNames, WrapColumnName));
        }
    }
}