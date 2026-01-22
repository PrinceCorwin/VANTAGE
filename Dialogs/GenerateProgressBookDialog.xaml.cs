using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Services.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog for generating a Progress Book PDF using the filter from the layout configuration
    public partial class GenerateProgressBookDialog : Window
    {
        private ProgressBookConfiguration _config;
        private List<Activity> _activities = new();

        public GenerateProgressBookDialog(ProgressBookConfiguration config)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme("FluentDark"));

            _config = config;

            Loaded += GenerateProgressBookDialog_Loaded;
        }

        private async void GenerateProgressBookDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Display the filter value as the book name (this will appear in PDF header)
                var filterValue = _config.FilterValue;
                if (string.IsNullOrEmpty(filterValue))
                {
                    txtBookName.Text = "All Records";
                    txtFilterInfo.Text = "No filter applied - generating for all your records";
                }
                else
                {
                    txtBookName.Text = filterValue;
                    txtFilterInfo.Text = $"Filter: {_config.FilterField} = {filterValue}";
                }

                // Load records using the filter from config
                await LoadRecordsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GenerateProgressBookDialog.Loaded");
                txtStatus.Text = $"Error loading: {ex.Message}";
            }
        }

        // Load records using the filter from the configuration
        private async System.Threading.Tasks.Task LoadRecordsAsync()
        {
            try
            {
                txtStatus.Text = "Loading records...";
                btnGenerate.IsEnabled = false;

                var username = App.CurrentUser?.Username ?? "";

                // Build where clause from config filter
                string whereClause;
                if (!string.IsNullOrEmpty(_config.FilterField) && !string.IsNullOrEmpty(_config.FilterValue))
                {
                    var safeValue = _config.FilterValue.Replace("'", "''");
                    whereClause = $"AssignedTo = '{username}' AND {_config.FilterField} = '{safeValue}'";
                }
                else
                {
                    // No filter - get all records for current user
                    whereClause = $"AssignedTo = '{username}'";
                }

                // Add filter for excluding completed activities
                if (_config.ExcludeCompleted)
                {
                    whereClause += " AND PercentEntry < 100";
                }

                var (activities, _) = await ActivityRepository.GetAllActivitiesAsync(whereClause);
                _activities = activities;

                txtRecordCount.Text = activities.Count.ToString("N0");

                // Estimate pages (roughly 30 rows per letter page, 45 per tabloid)
                int rowsPerPage = _config.PaperSize == PaperSize.Tabloid ? 45 : 30;
                int estimatedPages = Math.Max(1, (int)Math.Ceiling((double)activities.Count / rowsPerPage));
                txtEstimatedPages.Text = estimatedPages.ToString();

                // Show project ID from first activity
                var projectId = activities.FirstOrDefault()?.ProjectID ?? "-";
                txtProjectId.Text = projectId;

                if (activities.Count == 0)
                {
                    txtStatus.Text = "No records found for this filter.";
                    btnGenerate.IsEnabled = false;
                }
                else
                {
                    txtStatus.Text = "";
                    btnGenerate.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GenerateProgressBookDialog.LoadRecordsAsync");
                txtStatus.Text = $"Error: {ex.Message}";
                btnGenerate.IsEnabled = false;
            }
        }

        // Generate the PDF
        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activities.Count == 0)
                {
                    MessageBox.Show("No records to generate.", "No Records",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Use filter value as book name (or default if no filter)
                var bookName = !string.IsNullOrEmpty(_config.FilterValue)
                    ? _config.FilterValue
                    : "Progress Book";

                // Show save dialog
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = $"{bookName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf",
                    Title = "Save Progress Book"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                btnGenerate.IsEnabled = false;
                txtStatus.Text = "Generating PDF...";

                // Get project info
                var projectId = _activities.FirstOrDefault()?.ProjectID ?? "Unknown";
                var projectDescription = ProjectCache.GetProjectDescription(projectId);

                // Generate PDF
                var generator = new ProgressBookPdfGenerator();
                var pdfDocument = generator.Generate(_config, _activities, bookName, projectId, projectDescription);

                // Save to file
                using (var fileStream = new FileStream(saveDialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    pdfDocument.Save(fileStream);
                }
                pdfDocument.Close(true);

                // Log the operation
                AppLogger.Info($"Progress Book generated: {bookName}, {_activities.Count} records, saved to {saveDialog.FileName}",
                    "GenerateProgressBookDialog.BtnGenerate_Click", App.CurrentUser?.Username ?? "Unknown");

                MessageBox.Show($"Progress Book saved to:\n{saveDialog.FileName}",
                    "Generated", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GenerateProgressBookDialog.BtnGenerate_Click");
                MessageBox.Show($"Error generating PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnGenerate.IsEnabled = true;
                txtStatus.Text = "";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
