using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Data.Sqlite;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class P6ImportDialog : Window
    {
        public DateTime SelectedWeekEndDate { get; private set; }
        public List<string> SelectedProjectIDs { get; private set; } = new List<string>();

        private readonly ObservableCollection<ProjectSelectionItem> _projects = new ObservableCollection<ProjectSelectionItem>();

        public P6ImportDialog(string filePath)
        {
            InitializeComponent();

            txtFileName.Text = Path.GetFileName(filePath);

            // Default to previous Sunday
            SelectedWeekEndDate = GetPreviousSunday();
            dpWeekEndDate.SelectedDate = SelectedWeekEndDate;

            LoadUserProjects();
            lvProjects.ItemsSource = _projects;
        }

        // Get previous Sunday from today
        private DateTime GetPreviousSunday()
        {
            var today = DateTime.Today;
            int daysToSubtract = (int)today.DayOfWeek;
            if (daysToSubtract == 0) daysToSubtract = 7; // If today is Sunday, go back 7 days
            return today.AddDays(-daysToSubtract);
        }

        // Load projects assigned to current user
        private void LoadUserProjects()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    AppLogger.Warning("No current user - cannot load projects", "P6ImportDialog.LoadUserProjects");
                    return;
                }

                using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                connection.Open();

                // Get distinct ProjectIDs assigned to current user, join with Projects table for names
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT 
                        a.ProjectID,
                        COALESCE(p.Description, a.ProjectID) as ProjectName
                    FROM Activities a
                    LEFT JOIN Projects p ON a.ProjectID = p.ProjectID
                    WHERE a.AssignedTo = @username
                    ORDER BY a.ProjectID";
                cmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string projectId = reader.GetString(0);
                    string projectName = reader.GetString(1);

                    _projects.Add(new ProjectSelectionItem
                    {
                        ProjectID = projectId,
                        ProjectName = projectName,
                        IsSelected = false // User must explicitly select
                    });
                }

                AppLogger.Info($"Loaded {_projects.Count} projects for P6 import dialog", "P6ImportDialog.LoadUserProjects");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "P6ImportDialog.LoadUserProjects");
                MessageBox.Show($"Error loading projects: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DpWeekEndDate_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dpWeekEndDate.SelectedDate.HasValue)
            {
                SelectedWeekEndDate = dpWeekEndDate.SelectedDate.Value;

                // Show helper text if not a Sunday
                if (SelectedWeekEndDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    txtWeekEndHelper.Text = "⚠ Warning: Selected date is not a Sunday";
                    txtWeekEndHelper.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    txtWeekEndHelper.Text = "Week ending date confirmed";
                    txtWeekEndHelper.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // Validate at least one project selected
            SelectedProjectIDs = _projects.Where(p => p.IsSelected).Select(p => p.ProjectID).ToList();

            if (SelectedProjectIDs.Count == 0)
            {
                txtError.Text = "Please select at least one ProjectID";
                txtError.Visibility = Visibility.Visible;
                return;
            }

            // Validate WeekEndDate
            if (dpWeekEndDate.SelectedDate == null)
            {
                txtError.Text = "Please select a week ending date";
                txtError.Visibility = Visibility.Visible;
                return;
            }

            AppLogger.Info($"P6 import confirmed: WeekEndDate={SelectedWeekEndDate:yyyy-MM-dd}, Projects={string.Join(",", SelectedProjectIDs)}", 
                "P6ImportDialog.BtnImport_Click", 
                App.CurrentUser?.Username);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Model for project selection in ListView
    public class ProjectSelectionItem : INotifyPropertyChanged
    {
        public string ProjectID { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string DisplayText => $"{ProjectID} - {ProjectName}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
