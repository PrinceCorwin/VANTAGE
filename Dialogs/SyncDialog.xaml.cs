using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class SyncDialog : Window
    {
        private List<ProjectSelection> _projects = null!;
        private HashSet<string> _projectsWithLocalRecords = null!;
        private bool _previousMyRecordsOnlySetting;

        public SyncDialog()
        {
            InitializeComponent();
            LoadMyRecordsOnlySetting();
            LoadProjects();
        }

        private void LoadMyRecordsOnlySetting()
        {
            // Load the previous setting state (default "false" if never set)
            var settingValue = SettingsManager.GetUserSetting(App.CurrentUser!.UserID, "MyRecordsOnlySync", "false");
            _previousMyRecordsOnlySetting = settingValue.Equals("true", StringComparison.OrdinalIgnoreCase);
            chkMyRecordsOnly.IsChecked = _previousMyRecordsOnlySetting;
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

                // Determine current checkbox state and if full pull is needed
                bool myRecordsOnly = chkMyRecordsOnly.IsChecked == true;
                bool needsFullPull = !myRecordsOnly && _previousMyRecordsOnlySetting;

                if (needsFullPull)
                {
                    var confirmResult = MessageBox.Show(
                        "You previously synced with 'My Records Only' enabled.\n\n" +
                        "Disabling this option requires a full re-sync to restore all records.\n\n" +
                        "This may take longer than usual. Continue?",
                        "Full Sync Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // Reset LastPulledSyncVersion for selected projects to force full pull
                    foreach (var projectId in selectedProjects)
                    {
                        SettingsManager.RemoveAppSetting($"LastPulledSyncVersion_{projectId}");
                    }

                    AppLogger.Info($"Full pull triggered: MyRecordsOnly toggled OFF, reset sync versions for {selectedProjects.Count} projects",
                        "SyncDialog.BtnConfirmSync_Click", App.CurrentUser?.Username);
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
                chkMyRecordsOnly.IsEnabled = false;

                // Show loading overlay
                ShowLoadingOverlay("Syncing with Azure Database...");
                txtLoadingProgress.Text = "Please wait...";

                // Small delay to let UI update before blocking operations
                await Task.Delay(100);

                // Start timer
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Get current user for owner filtering
                string currentUsername = App.CurrentUser?.Username ?? "";
                int localRecordsRemoved = 0;

                // Run sync operations on background thread to keep UI responsive
                // Run sync operations on background thread to keep UI responsive
                var (pushResult, pullResult) = await Task.Run(() =>
                {
                    // Mirror reference tables
                    DatabaseSetup.MirrorTablesFromAzure();

                    // Push dirty records (always push ALL dirty, regardless of MyRecordsOnly)
                    var push = SyncManager.PushRecordsAsync(selectedProjects).Result;

                    // Pull updates with optional owner filter
                    var pull = SyncManager.PullRecordsAsync(selectedProjects, myRecordsOnly ? currentUsername : null).Result;

                    return (push, pull);
                });

                // If MyRecordsOnly, delete local records not owned by current user
                if (myRecordsOnly && !string.IsNullOrEmpty(currentUsername))
                {
                    localRecordsRemoved = RemoveNonOwnedLocalRecords(selectedProjects, currentUsername);
                }

                // Save the MyRecordsOnly setting after successful sync
                SettingsManager.SetUserSetting(App.CurrentUser!.UserID, "MyRecordsOnlySync", myRecordsOnly.ToString().ToLower(), "bool");

                stopwatch.Stop();

                // Hide loading overlay
                HideLoadingOverlay();

                // Show results with timing
                var message = $"Sync completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds\n\n" +
                             $"Pushed: {pushResult.InsertedRecords} inserted, {pushResult.UpdatedRecords} updated\n" +
                             $"Pulled: {pullResult.PulledRecords} records\n" +
                             $"Skipped: {pullResult.SkippedRecords} records";

                if (localRecordsRemoved > 0)
                {
                    message += $"\n\nRemoved {localRecordsRemoved} local records (other users' records)";
                }

                if (needsFullPull)
                {
                    message += "\n\n(Full sync performed)";
                }

                if (pushResult.FailedRecords.Count > 0)
                {
                    message += $"\n\nFailed to push {pushResult.FailedRecords.Count} records:\n" +
                              string.Join("\n", pushResult.FailedRecords.Take(5));
                    if (pushResult.FailedRecords.Count > 5)
                        message += $"\n... and {pushResult.FailedRecords.Count - 5} more";
                }

                AppLogger.Info($"Sync completed: {pushResult.PushedRecords} pushed, {pullResult.PulledRecords} pulled, {localRecordsRemoved} removed (MyRecordsOnly={myRecordsOnly})",
                    "SyncDialog.BtnConfirmSync_Click", App.CurrentUser?.Username);

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
                chkMyRecordsOnly.IsEnabled = true;
            }
        }
        // Remove local records not owned by current user for selected projects
        private static int RemoveNonOwnedLocalRecords(List<string> projectIds, string currentUsername)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // Build project filter
                var projectParams = string.Join(",", projectIds.Select((_, i) => $"@p{i}"));

                var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
            DELETE FROM Activities 
            WHERE ProjectID IN ({projectParams}) 
            AND AssignedTo != @username";

                for (int i = 0; i < projectIds.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@p{i}", projectIds[i]);
                }
                cmd.Parameters.AddWithValue("@username", currentUsername);

                int deleted = cmd.ExecuteNonQuery();

                if (deleted > 0)
                {
                    AppLogger.Info($"Removed {deleted} non-owned records from local database (MyRecordsOnly sync)",
                        "SyncDialog.RemoveNonOwnedLocalRecords", App.CurrentUser?.Username);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SyncDialog.RemoveNonOwnedLocalRecords");
                return 0;
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
