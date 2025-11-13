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

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var syncDialog = new SyncDialog();
                bool? result = syncDialog.ShowDialog();

                if (result == true)
                {
                    // TODO: Refresh grid after sync
                    MessageBox.Show("Sync completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "ProgressView.BtnSync_Click");
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