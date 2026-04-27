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
        private CoverPageData _coverPageData = new();

        public GenerateProgressBookDialog(ProgressBookConfiguration config)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

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

                // Build base where clause from config filter (without ExcludeCompleted - we need all for cover page totals)
                string baseWhereClause;
                if (!string.IsNullOrEmpty(_config.FilterField) && !string.IsNullOrEmpty(_config.FilterValue))
                {
                    var safeValue = _config.FilterValue.Replace("'", "''");
                    if (_config.IncludeAllUsers)
                    {
                        baseWhereClause = $"{_config.FilterField} = '{safeValue}'";
                    }
                    else
                    {
                        baseWhereClause = $"AssignedTo = '{username}' AND {_config.FilterField} = '{safeValue}'";
                    }
                }
                else
                {
                    // No filter - get all records (optionally for current user only)
                    if (_config.IncludeAllUsers)
                    {
                        baseWhereClause = "1=1"; // All records
                    }
                    else
                    {
                        baseWhereClause = $"AssignedTo = '{username}'";
                    }
                }

                // Query ALL activities first (for cover page totals)
                var (allActivities, _) = await ActivityRepository.GetAllActivitiesAsync(baseWhereClause);

                // Filter out excluded column values if configured
                if (!string.IsNullOrEmpty(_config.ExcludeColumn) && _config.ExcludeValues.Count > 0)
                {
                    allActivities = allActivities
                        .Where(a => !_config.ExcludeValues.Contains(GetActivityFieldValue(a, _config.ExcludeColumn) ?? ""))
                        .ToList();
                }

                // Compute cover page data from ALL activities (including completed)
                _coverPageData = new CoverPageData
                {
                    TotalBudgetMHs = allActivities.Sum(a => a.BudgetMHs),
                    TotalEarnedMHs = allActivities.Sum(a => a.EarnMHsCalc),
                    LastSyncDisplay = GetLastSyncDisplay()
                };

                // Separate completed activities
                var completedActivities = allActivities.Where(a => a.PercentEntry >= 100).ToList();
                var activities = _config.ExcludeCompleted
                    ? allActivities.Where(a => a.PercentEntry < 100).ToList()
                    : allActivities;

                // Set cover page counts
                _coverPageData.IncludedCount = activities.Count;
                if (_config.ExcludeCompleted)
                {
                    _coverPageData.ExcludedCompletedCount = completedActivities.Count;
                    _coverPageData.ExcludedCompletedBudgetMHs = completedActivities.Sum(a => a.BudgetMHs);
                    _coverPageData.ExcludedCompletedEarnedMHs = completedActivities.Sum(a => a.EarnMHsCalc);
                }

                // Get max UpdatedUtcDate from included activities
                _coverPageData.LastUpdatedDisplay = GetLastUpdatedDisplay(activities);

                _activities = activities;

                txtRecordCount.Text = activities.Count.ToString("N0");

                // Estimate pages (roughly 30 rows per letter page, 45 per tabloid) + 1 for cover page
                int rowsPerPage = _config.PaperSize == PaperSize.Tabloid ? 45 : 30;
                int estimatedPages = Math.Max(1, (int)Math.Ceiling((double)activities.Count / rowsPerPage)) + 1;
                txtEstimatedPages.Text = estimatedPages.ToString();

                // Show project ID from first activity
                var projectId = activities.FirstOrDefault()?.ProjectID ?? "-";
                txtProjectId.Text = projectId;

                if (allActivities.Count == 0)
                {
                    txtStatus.Text = "No records found for this filter.";
                    btnGenerate.IsEnabled = false;
                }
                else
                {
                    // Check ALL activities for unsynced ActivityID (still 0) - cover page needs all synced
                    var unsyncedCount = allActivities.Count(a => a.ActivityID == 0);
                    if (unsyncedCount > 0)
                    {
                        txtStatus.Text = $"⚠ {unsyncedCount} of {allActivities.Count} record(s) have not been synced to Azure.\nPlease sync in the Progress module before generating.";
                        statusBorder.Visibility = Visibility.Visible;
                        btnGenerate.IsEnabled = false;
                    }
                    else
                    {
                        statusBorder.Visibility = Visibility.Collapsed;
                        btnGenerate.IsEnabled = true;
                    }
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
                    AppMessageBox.Show("No records to generate.", "No Records",
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

                // Generate PDF with cover page data
                var generator = new ProgressBookPdfGenerator();
                var pdfDocument = generator.Generate(_config, _activities, bookName, projectId, projectDescription, _coverPageData);

                // Save to file
                using (var fileStream = new FileStream(saveDialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    pdfDocument.Save(fileStream);
                }
                pdfDocument.Close(true);

                // Log the operation
                AppLogger.Info($"Progress Book generated: {bookName}, {_activities.Count} records, saved to {saveDialog.FileName}",
                    "GenerateProgressBookDialog.BtnGenerate_Click", App.CurrentUser?.Username ?? "Unknown");

                AppMessageBox.Show($"Progress Book saved to:\n{saveDialog.FileName}",
                    "Generated", MessageBoxButton.OK, MessageBoxImage.None);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GenerateProgressBookDialog.BtnGenerate_Click");
                AppMessageBox.Show($"Error generating PDF:\n{ex.Message}",
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

        // Get a field value from an Activity using reflection
        private string? GetActivityFieldValue(Activity activity, string fieldName)
        {
            try
            {
                var prop = typeof(Activity).GetProperty(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return prop?.GetValue(activity)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        // Get the last sync display string from user settings
        private string GetLastSyncDisplay()
        {
            var lastSyncString = SettingsManager.GetUserSetting("LastSyncUtcDate");
            if (string.IsNullOrEmpty(lastSyncString))
                return "Never";

            if (DateTime.TryParse(lastSyncString, out DateTime lastSyncUtc))
            {
                var localTime = lastSyncUtc.ToLocalTime();
                return localTime.ToString("M/d/yyyy HH:mm");
            }
            return "Never";
        }

        // Get the max UpdatedUtcDate from activities as display string
        private string GetLastUpdatedDisplay(List<Activity> activities)
        {
            if (activities.Count == 0)
                return "N/A";

            DateTime? maxDate = null;
            foreach (var activity in activities)
            {
                if (activity.UpdatedUtcDate.HasValue)
                {
                    if (!maxDate.HasValue || activity.UpdatedUtcDate.Value > maxDate.Value)
                        maxDate = activity.UpdatedUtcDate.Value;
                }
            }

            if (!maxDate.HasValue)
                return "N/A";

            var localTime = maxDate.Value.ToLocalTime();
            return localTime.ToString("M/d/yyyy HH:mm");
        }
    }
}
