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
            var settingValue = SettingsManager.GetUserSetting("MyRecordsOnlySync", "false");
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
                projectCmd.CommandText = "SELECT ProjectID, Description FROM Projects ORDER BY ProjectID DESC";

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
                AppMessageBox.Show($"Error loading projects: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    AppMessageBox.Show("Please select at least one project to sync.", "No Projects Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check Azure connection
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    AppMessageBox.Show($"MILESTONE could not establish connection:\n\n{errorMessage}\n\nPlease try again later.",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Determine current checkbox state and if full pull is needed
                bool myRecordsOnly = chkMyRecordsOnly.IsChecked == true;
                bool needsFullPull = !myRecordsOnly && _previousMyRecordsOnlySetting;

                if (needsFullPull)
                {
                    var confirmResult = AppMessageBox.Show(
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
                var (pushResult, pullResult) = await Task.Run(() =>
                {
                    // Mirror reference tables
                    DatabaseSetup.MirrorTablesFromAzure();

                    // Push dirty records (always push ALL dirty, regardless of MyRecordsOnly)
                    var push = SyncManager.PushRecordsAsync(selectedProjects).Result;

                    // Skip pull if push had an error — pulling would overwrite local changes with old Azure data
                    if (!string.IsNullOrEmpty(push.ErrorMessage))
                    {
                        return (push, new SyncManager.SyncResult());
                    }

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
                SettingsManager.SetUserSetting("MyRecordsOnlySync", myRecordsOnly.ToString().ToLower(), "bool");

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

                if (!string.IsNullOrEmpty(pushResult.ErrorMessage))
                {
                    message += $"\n\nPush error (pull skipped to protect your changes):\n{pushResult.ErrorMessage}";
                }

                // Pre-sync validation gate (SyncManager.PushRecords) — rows that
                // failed ActivityValidator / ActivityRequiredMetadata rules were
                // left LocalDirty = 1 and excluded from this push. Distinct record
                // count uses ValidationFailedUniqueIds so multiple violations on
                // one row don't inflate the tally.
                if (pushResult.ValidationFailedUniqueIds.Count > 0)
                {
                    int blocked = pushResult.ValidationFailedUniqueIds.Count;
                    int valid = pushResult.PushedRecords;
                    int total = pushResult.TotalRecordsToPush;
                    message += $"\n\nPushed {valid} of {total} rows. {blocked} row(s) have validation issues " +
                              "and remain marked as unsaved — fix and re-sync.\n" +
                              "(Use Tools → Validate My Records to review all issues.)";
                    var sample = pushResult.ValidationFailedRecords.Take(5).ToList();
                    if (sample.Count > 0)
                    {
                        message += "\n\nExamples:\n" + string.Join("\n", sample);
                        if (pushResult.ValidationFailedRecords.Count > sample.Count)
                            message += $"\n... and {pushResult.ValidationFailedRecords.Count - sample.Count} more";
                    }
                }

                if (pushResult.FailedRecords.Count > 0)
                {
                    message += $"\n\nFailed to push {pushResult.FailedRecords.Count} records:\n" +
                              string.Join("\n", pushResult.FailedRecords.Take(5));
                    if (pushResult.FailedRecords.Count > 5)
                        message += $"\n... and {pushResult.FailedRecords.Count - 5} more";
                }

                // Pull-guard: Azure had newer versions for rows still LocalDirty = 1
                // (push-blocked). Pull declined to overwrite to protect the user's
                // pending edits. Surfacing the count tells the user Azure changed
                // underneath them and their local copy was preserved.
                if (pullResult.SkippedDirtyConflicts.Count > 0)
                {
                    int conflicts = pullResult.SkippedDirtyConflicts.Count;
                    message += $"\n\n{conflicts} of your unsynced row(s) had newer data in Azure. " +
                               "Your local edits were preserved — fix the validation issues and re-sync to push.";
                }

                AppLogger.Info($"Sync completed: {pushResult.PushedRecords} pushed, {pullResult.PulledRecords} pulled, {localRecordsRemoved} removed, {pushResult.ValidationFailedUniqueIds.Count} validation-blocked, {pullResult.SkippedDirtyConflicts.Count} pull-conflicts preserved (MyRecordsOnly={myRecordsOnly})",
                    "SyncDialog.BtnConfirmSync_Click", App.CurrentUser?.Username);

                bool syncIncomplete = !string.IsNullOrEmpty(pushResult.ErrorMessage)
                    || pushResult.ValidationFailedUniqueIds.Count > 0
                    || pushResult.FailedRecords.Count > 0
                    || pullResult.SkippedDirtyConflicts.Count > 0;
                var messageTitle = syncIncomplete ? "Sync Incomplete" : "Sync Complete";
                var messageIcon = syncIncomplete ? MessageBoxImage.Warning : MessageBoxImage.None;
                AppMessageBox.Show(message, messageTitle, MessageBoxButton.OK, messageIcon);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                AppMessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // Remove local records not owned by current user for selected projects.
        //
        // Ownership is read from AZURE, not from the local AssignedTo column.
        // MyRecordsOnly's pull filter (`AND AssignedTo = @owner`) excludes any
        // row that was reassigned to someone else in Azure, so the local copy
        // never sees the update and its local AssignedTo stays stale. A delete
        // keyed on local AssignedTo would miss those rows entirely. Querying
        // Azure for "what's actually mine right now" is authoritative.
        private static int RemoveNonOwnedLocalRecords(List<string> projectIds, string currentUsername)
        {
            try
            {
                // Fetch the authoritative "still mine" set from Azure for the
                // selected projects.
                HashSet<string> stillMine = new(StringComparer.OrdinalIgnoreCase);
                using (var azureConn = AzureDbManager.GetConnection())
                {
                    azureConn.Open();
                    var azureProjectParams = string.Join(",", projectIds.Select((_, i) => $"@p{i}"));
                    var azureCmd = azureConn.CreateCommand();
                    azureCmd.CommandTimeout = 0;
                    azureCmd.CommandText = $@"
                        SELECT UniqueID FROM VMS_Activities
                        WHERE ProjectID IN ({azureProjectParams})
                          AND AssignedTo = @username
                          AND IsDeleted = 0";
                    for (int i = 0; i < projectIds.Count; i++)
                    {
                        azureCmd.Parameters.AddWithValue($"@p{i}", projectIds[i]);
                    }
                    azureCmd.Parameters.AddWithValue("@username", currentUsername);
                    using var azureReader = azureCmd.ExecuteReader();
                    while (azureReader.Read())
                    {
                        stillMine.Add(azureReader.GetString(0));
                    }
                }

                // Stream local UniqueIDs in the selected projects and collect
                // any that aren't in the Azure "still mine" set.
                using var localConn = DatabaseSetup.GetConnection();
                localConn.Open();

                var localProjectParams = string.Join(",", projectIds.Select((_, i) => $"@p{i}"));
                var localCmd = localConn.CreateCommand();
                localCmd.CommandText = $"SELECT UniqueID FROM Activities WHERE ProjectID IN ({localProjectParams})";
                for (int i = 0; i < projectIds.Count; i++)
                {
                    localCmd.Parameters.AddWithValue($"@p{i}", projectIds[i]);
                }

                var toDelete = new List<string>();
                using (var localReader = localCmd.ExecuteReader())
                {
                    while (localReader.Read())
                    {
                        string uid = localReader.GetString(0);
                        if (!stillMine.Contains(uid))
                        {
                            toDelete.Add(uid);
                        }
                    }
                }

                if (toDelete.Count == 0) return 0;

                // Batched DELETE — keeps the parameter count well under SQLite's
                // default ceiling and scales to large reassignment churn.
                const int batchSize = 500;
                int deleted = 0;
                using var tx = localConn.BeginTransaction();
                for (int i = 0; i < toDelete.Count; i += batchSize)
                {
                    int end = Math.Min(i + batchSize, toDelete.Count);
                    var ps = string.Join(",", Enumerable.Range(0, end - i).Select(k => $"@u{k}"));
                    var deleteCmd = localConn.CreateCommand();
                    deleteCmd.Transaction = tx;
                    deleteCmd.CommandText = $"DELETE FROM Activities WHERE UniqueID IN ({ps})";
                    for (int k = i; k < end; k++)
                    {
                        deleteCmd.Parameters.AddWithValue($"@u{k - i}", toDelete[k]);
                    }
                    deleted += deleteCmd.ExecuteNonQuery();
                }
                tx.Commit();

                if (deleted > 0)
                {
                    AppLogger.Info($"MyRecordsOnly sync removed {deleted} local record(s) no longer assigned to user in Azure",
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
