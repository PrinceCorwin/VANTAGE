using System;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Helper methods for building SQL queries
    /// </summary>
    public static class SqlHelper
    {
        /// <summary>
        /// Wrap column name in brackets if it contains spaces or hyphens
        /// </summary>
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

        /// <summary>
        /// Wrap multiple column names for SELECT statements
        /// </summary>
        public static string WrapColumnNames(params string[] columnNames)
        {
            return string.Join(", ", Array.ConvertAll(columnNames, WrapColumnName));
        }
    }
}