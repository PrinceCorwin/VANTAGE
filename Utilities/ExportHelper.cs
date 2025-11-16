using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    
    /// Helper class for exporting activities to Excel with different scenarios
    
    public static class ExportHelper
    {
        
        /// Export activities with options dialog (for MainWindow)
        /// Shows dialog to choose between all records or filtered records
        
        public static async Task ExportActivitiesWithOptionsAsync(
            Window owner,
            List<Activity> allActivities,
            List<Activity> filteredActivities,
            bool hasActiveFilters)
        {
            try
            {
                // Validate inputs
                if (allActivities == null || allActivities.Count == 0)
                {
                    MessageBox.Show(
                        "No activities to export.",
                        "Export",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                List<Activity> activitiesToExport;
                string exportType;

                // If filters are active, ask user which set to export
                if (hasActiveFilters && filteredActivities != null && filteredActivities.Count < allActivities.Count)
                {
                    var result = MessageBox.Show(
                        owner,
                        $"Choose which records to export:\n\n" +
                        $"• All Records: {allActivities.Count:N0} activities\n" +
                        $"• Filtered Records: {filteredActivities.Count:N0} activities\n\n" +
                        $"Export filtered records only?",
                        "Export Options",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                    if (result == MessageBoxResult.Cancel)
                        return;

                    activitiesToExport = result == MessageBoxResult.Yes ? filteredActivities : allActivities;
                    exportType = result == MessageBoxResult.Yes ? "Filtered Activities" : "All Activities";
                }
                else
                {
                    // No filters active, just export all
                    activitiesToExport = allActivities;
                    exportType = "All Activities";
                }

                // Show save file dialog
                var filePath = ShowSaveFileDialog($"VANTAGE_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Export asynchronously
                await Task.Run(() => ExcelExporter.ExportActivities(filePath, activitiesToExport));

                // Log the export
                LogExport(exportType, activitiesToExport.Count, filePath);

                // Show success message
                MessageBox.Show(
                    owner,
                    $"Successfully exported {activitiesToExport.Count:N0} activities to:\n\n{filePath}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleExportError(owner, "Export Activities", ex);
            }
        }

        
        /// Export selected activities directly without options (for ProgressView context menu)
        
        public static async Task ExportSelectedActivitiesAsync(
            Window owner,
            List<Activity> selectedActivities)
        {
            try
            {
                // Validate inputs
                if (selectedActivities == null || selectedActivities.Count == 0)
                {
                    MessageBox.Show(
                        owner,
                        "No activities selected to export.",
                        "Export Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Show save file dialog
                var filePath = ShowSaveFileDialog($"VANTAGE_Selected_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Export asynchronously
                await Task.Run(() => ExcelExporter.ExportActivities(filePath, selectedActivities));

                // Log the export
                LogExport("Export Selected", selectedActivities.Count, filePath);

                // Show success message
                MessageBox.Show(
                    owner,
                    $"Successfully exported {selectedActivities.Count:N0} selected activities to:\n\n{filePath}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleExportError(owner, "Export Selected Activities", ex);
            }
        }

        
        /// Export deleted records directly without options (for DeletedRecordsView)
        
        public static async Task ExportDeletedRecordsAsync(
            Window owner,
            List<Activity> deletedActivities)
        {
            try
            {
                // Validate inputs
                if (deletedActivities == null || deletedActivities.Count == 0)
                {
                    MessageBox.Show(
                        owner,
                        "No deleted records to export.",
                        "Export Deleted Records",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Show save file dialog
                var filePath = ShowSaveFileDialog($"VANTAGE_Deleted_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Export asynchronously
                await Task.Run(() => ExcelExporter.ExportActivities(filePath, deletedActivities));

                // Log the export
                LogExport("Export Deleted Records", deletedActivities.Count, filePath);

                // Show success message
                MessageBox.Show(
                    owner,
                    $"Successfully exported {deletedActivities.Count:N0} deleted records to:\n\n{filePath}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleExportError(owner, "Export Deleted Records", ex);
            }
        }

        
        /// Export template (headers only) without any data
        
        public static async Task ExportTemplateAsync(Window owner)
        {
            try
            {
                // Show save file dialog
                var filePath = ShowSaveFileDialog($"VANTAGE_Template_{DateTime.Now:yyyyMMdd}.xlsx");
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Export template asynchronously
                await Task.Run(() => ExcelExporter.ExportTemplate(filePath));

                // Log the export
                AppLogger.Info(
                    $"Exported template to {Path.GetFileName(filePath)}",
                    "Export Template",
                    App.CurrentUser?.Username ?? "Unknown");

                // Show success message
                MessageBox.Show(
                    owner,
                    $"Successfully exported template to:\n\n{filePath}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleExportError(owner, "Export Template", ex);
            }
        }

        #region Private Helper Methods

        
        /// Show save file dialog and return selected file path
        
        private static string ShowSaveFileDialog(string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export to Excel",
                FileName = defaultFileName,
                DefaultExt = ".xlsx",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                FilterIndex = 1,
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        
        /// Log export action
        
        private static void LogExport(string action, int recordCount, string filePath)
        {
            try
            {
                AppLogger.Info(
                $"Exported {recordCount:N0} records to {Path.GetFileName(filePath)}",
                action,
                App.CurrentUser?.Username ?? "Unknown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log export: {ex.Message}");
                // Don't throw - logging failure shouldn't break export
            }
        }

        
        /// Handle export errors with logging and user notification
        
        private static void HandleExportError(Window owner, string action, Exception ex)
        {
            try
            {
                // Log the error
                AppLogger.Error(ex, action, App.CurrentUser?.Username ?? "Unknown");
            }
            catch
            {
                // Ignore logging errors
            }

            // Show error message to user
            MessageBox.Show(
                owner,
                $"Export failed:\n\n{ex.Message}\n\nPlease check that:\n" +
                $"• The file is not already open in Excel\n" +
                $"• You have write permission to the selected location\n" +
                $"• There is enough disk space",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        #endregion
    }
}