using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Export activities to Excel using OldVantage column names
    /// </summary>
    public static class ExcelExporter
    {
        /// <summary>
        /// Export all activities to Excel file
        /// </summary>
        public static void ExportActivities(string filePath, List<Models.Activity> activities)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // Get column mappings from database
                var columnMappings = GetColumnMappings();

                // Write headers (OldVantage names)
                int colIndex = 1;
                var columnOrder = new List<(string DbColumnName, string OldVantageName, int ExcelColumn)>();

                foreach (var mapping in columnMappings)
                {
                    string headerName = string.IsNullOrEmpty(mapping.OldVantageName)
                        ? mapping.DbColumnName
                        : mapping.OldVantageName;

                    worksheet.Cell(1, colIndex).Value = headerName;
                    columnOrder.Add((mapping.DbColumnName, headerName, colIndex));
                    colIndex++;
                }

                // Write data rows
                int rowIndex = 2;
                foreach (var activity in activities)
                {
                    foreach (var col in columnOrder)
                    {
                        var value = GetActivityValue(activity, col.DbColumnName);

                        // Handle different value types for ClosedXML
                        if (value == null || value.ToString() == "")
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = "";
                        }
                        else if (value is double doubleValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = doubleValue;
                        }
                        else if (value is int intValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = intValue;
                        }
                        else if (value is DateTime dateValue)
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = dateValue;
                        }
                        else
                        {
                            worksheet.Cell(rowIndex, col.ExcelColumn).Value = value.ToString();
                        }
                    }
                    rowIndex++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                System.Diagnostics.Debug.WriteLine($"✓ Exported {activities.Count} activities to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Export error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Export empty template (headers only)
        /// </summary>
        public static void ExportTemplate(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // Get column mappings from database
                var columnMappings = GetColumnMappings();

                // Write headers only (OldVantage names)
                int colIndex = 1;
                foreach (var mapping in columnMappings)
                {
                    string headerName = string.IsNullOrEmpty(mapping.OldVantageName)
                        ? mapping.DbColumnName
                        : mapping.OldVantageName;

                    worksheet.Cell(1, colIndex).Value = headerName;
                    colIndex++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                System.Diagnostics.Debug.WriteLine($"✓ Exported template to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Template export error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get column mappings from database
        /// </summary>
        private static List<(string DbColumnName, string OldVantageName)> GetColumnMappings()
        {
            var mappings = new List<(string DbColumnName, string OldVantageName)>();

            using var connection = DatabaseSetup.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT DbColumnName, OldVantageName FROM ColumnMappings ORDER BY MappingID";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0)) // Skip rows with NULL DbColumnName
                {
                    string dbColumnName = reader.GetString(0);
                    string oldVantageName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    mappings.Add((dbColumnName, oldVantageName));
                }
            }

            return mappings;
        }

        /// <summary>
        /// Get activity property value by database column name
        /// </summary>
        private static object GetActivityValue(Models.Activity activity, string dbColumnName)
        {
            // Get property name from DbColumnName
            string propertyName = ColumnMapper.GetPropertyName(dbColumnName);

            // Use reflection to get value
            var property = typeof(Models.Activity).GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(activity);

                // Handle _Display properties (convert back to 0-1)
                if (propertyName.EndsWith("_Display"))
                {
                    if (value is double displayValue)
                    {
                        return displayValue / 100.0; // Convert 0-100 back to 0-1
                    }
                }

                return value ?? "";
            }

            return "";
        }
    }
}