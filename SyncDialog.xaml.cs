using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Utilities;

namespace VANTAGE
{
    public partial class SyncDialog : Window
    {
        private List<ProjectSelection> _projects = null!;
        private HashSet<string> _projectsWithLocalRecords = null!;

        public SyncDialog()
        {
            InitializeComponent();
            LoadProjects();
        }

        private void LoadProjects()
        {
            try
            {
                _projects = new List<ProjectSelection>();
                _projectsWithLocalRecords = new HashSet<string>();

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // Get all projects from Projects table
                var projectCmd = connection.CreateCommand();
                projectCmd.CommandText = "SELECT ProjectID, Description FROM Projects ORDER BY ProjectID";

                using var projectReader = projectCmd.ExecuteReader();
                while (projectReader.Read())
                {
                    var project = new ProjectSelection
                    {
                        ProjectID = projectReader.GetString(0),
                        ProjectName = projectReader.IsDBNull(1) ? "" : projectReader.GetString(1),
                        IsSelected = false
                    };
                    _projects.Add(project);
                }
                projectReader.Close();

                // Pre-select projects that have Activities in local database
                var activityCmd = connection.CreateCommand();
                activityCmd.CommandText = "SELECT DISTINCT ProjectID FROM Activities WHERE ProjectID IS NOT NULL";

                using var activityReader = activityCmd.ExecuteReader();
                while (activityReader.Read())
                {
                    if (!activityReader.IsDBNull(0))
                    {
                        _projectsWithLocalRecords.Add(activityReader.GetString(0));
                    }
                }

                // Mark projects as selected if they have activities
                foreach (var project in _projects)
                {
                    if (_projectsWithLocalRecords.Contains(project.ProjectID))
                    {
                        project.IsSelected = true;
                    }
                }

                projectList.ItemsSource = _projects;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SyncDialog.LoadProjects");
                MessageBox.Show($"Error loading projects: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void ShowLoadingOverlay(string message = "Processing...")
        {
            txtLoadingMessage.Text = message;
            txtLoadingProgress.Text = "";
            LoadingProgressBar.Value = 0;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnConfirmSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProjects = _projects.Where(p => p.IsSelected).Select(p => p.ProjectID).ToList();

                if (selectedProjects.Count == 0)
                {
                    MessageBox.Show("Please select at least one project to sync.", "No Projects Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check Azure connection
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    MessageBox.Show($"MILESTONE could not establish connection:\n\n{errorMessage}\n\nPlease try again later.",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Check for excluded projects with unsaved changes
                var excludedProjects = _projectsWithLocalRecords
                    .Where(p => !selectedProjects.Contains(p))
                    .ToList();

                if (excludedProjects.Count > 0)
                {
                    var dirtyCountsByProject = await ActivityRepository.GetDirtyCountByExcludedProjectsAsync(selectedProjects);

                    if (dirtyCountsByProject.Count > 0)
                    {
                        var projectNames = _projects.ToDictionary(p => p.ProjectID, p => p.ProjectName);
                        var warningDialog = new UnsyncedChangesWarningDialog(dirtyCountsByProject, projectNames);
                        warningDialog.Owner = this;

                        bool? result = warningDialog.ShowDialog();

                        if (result != true)
                        {
                            return;
                        }

                        AppLogger.Info($"User confirmed removal of {dirtyCountsByProject.Values.Sum()} dirty records from {dirtyCountsByProject.Count} excluded projects",
                            "SyncDialog.BtnConfirmSync_Click");
                    }

                    // Remove records from Local for excluded projects
                    int removedCount = await ActivityRepository.RemoveActivitiesByProjectIdsAsync(excludedProjects);

                    // Remove LastPulledSyncVersion settings for excluded projects
                    foreach (var projectId in excludedProjects)
                    {
                        SettingsManager.RemoveAppSetting($"LastPulledSyncVersion_{projectId}");
                    }

                    if (removedCount > 0)
                    {
                        AppLogger.Info($"Removed {removedCount} records from {excludedProjects.Count} excluded projects: {string.Join(", ", excludedProjects)}",
                            "SyncDialog.BtnConfirmSync_Click");
                    }
                }

                // Disable UI during sync
                btnSync.IsEnabled = false;
                btnCancel.IsEnabled = false;
                projectList.IsEnabled = false;

                // Show loading overlay
                ShowLoadingOverlay("Syncing with Azure Database...");
                txtLoadingProgress.Text = "Please wait...";

                // Small delay to let UI update before blocking operations
                await Task.Delay(100);

                // Start timer
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Run sync operations on background thread to keep UI responsive
                var (pushResult, pullResult) = await Task.Run(() =>
                {
                    // Mirror reference tables
                    DatabaseSetup.MirrorTablesFromAzure();

                    // Push dirty records
                    var push = SyncManager.PushRecordsAsync(selectedProjects).Result;

                    // Pull updates
                    var pull = SyncManager.PullRecordsAsync(selectedProjects).Result;

                    return (push, pull);
                });

                stopwatch.Stop();

                // Hide loading overlay
                HideLoadingOverlay();

                // Show results with timing
                var message = $"Sync completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds\n\n" +
                             $"Inserted: {pushResult.InsertedRecords} records\n" +
                             $"Updated: {pushResult.UpdatedRecords} records\n" +
                             $"Pulled: {pullResult.PulledRecords} records\n" +
                             $"Skipped: {pullResult.SkippedRecords} records";

                if (pushResult.FailedRecords.Count > 0)
                {
                    message += $"\n\nFailed to push {pushResult.FailedRecords.Count} records:\n" +
                              string.Join("\n", pushResult.FailedRecords.Take(5));
                    if (pushResult.FailedRecords.Count > 5)
                        message += $"\n... and {pushResult.FailedRecords.Count - 5} more";
                }

                MessageBox.Show(message, "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                MessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "SyncDialog.BtnConfirmSync_Click");
            }
            finally
            {
                // Re-enable UI
                btnSync.IsEnabled = true;
                btnCancel.IsEnabled = true;
                projectList.IsEnabled = true;
            }
        }
    }

    // Model for project selection
    public class ProjectSelection : INotifyPropertyChanged
    {
        public string ProjectID { get; set; } = null!;
        public string ProjectName { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
