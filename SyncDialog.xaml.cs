using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE
{
    public partial class SyncDialog : Window
    {
        private List<ProjectSelection> _projects;

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
                var existingProjects = new HashSet<string>();
                while (activityReader.Read())
                {
                    if (!activityReader.IsDBNull(0))
                    {
                        existingProjects.Add(activityReader.GetString(0));
                    }
                }

                // Mark projects as selected if they have activities
                foreach (var project in _projects)
                {
                    if (existingProjects.Contains(project.ProjectID))
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

                // Get Central path
                string centralPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");

                // Check connection
                if (!SyncManager.CheckCentralConnection(centralPath, out string errorMessage))
                {
                    MessageBox.Show($"MILESTONE could not establish connection:\n\n{errorMessage}\n\nPlease try again later.",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Disable UI during sync
                btnSync.IsEnabled = false;
                btnCancel.IsEnabled = false;
                projectList.IsEnabled = false;

                // Start timer
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Mirror reference tables
                DatabaseSetup.MirrorTablesFromCentral(centralPath);

                // Push dirty records
                var pushResult = await SyncManager.PushRecordsAsync(centralPath, selectedProjects);

                // Pull updates
                var pullResult = await SyncManager.PullRecordsAsync(centralPath, selectedProjects);

                stopwatch.Stop();

                // Show results with timing
                var message = $"Sync completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds\n\n" +
                             $"Pushed: {pushResult.PushedRecords} records\n" +
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
                MessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "SyncDialog.BtnSync_Click");
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
        public string ProjectID { get; set; }
        public string ProjectName { get; set; }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}