using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Data.SqlClient;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
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
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            txtFileName.Text = Path.GetFileName(filePath);

            // Default to previous Sunday
            SelectedWeekEndDate = GetPreviousSunday();
            dpWeekEndDate.SelectedDate = SelectedWeekEndDate;

            lvProjects.ItemsSource = _projects;

            // Load projects asynchronously after dialog is shown
            Loaded += async (s, e) => await LoadUserProjectsAsync();
        }

        // Get previous Sunday from today
        private DateTime GetPreviousSunday()
        {
            var today = DateTime.Today;
            int daysToSubtract = (int)today.DayOfWeek;
            if (daysToSubtract == 0) daysToSubtract = 7; // If today is Sunday, go back 7 days
            return today.AddDays(-daysToSubtract);
        }

        // Load projects from user's submitted snapshots in Azure
        private async System.Threading.Tasks.Task LoadUserProjectsAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    AppLogger.Warning("No current user - cannot load projects", "P6ImportDialog.LoadUserProjectsAsync");
                    return;
                }

                // Show loading state
                txtLoading.Visibility = Visibility.Visible;
                lvProjects.Visibility = Visibility.Collapsed;
                btnImport.IsEnabled = false;

                var projectList = await System.Threading.Tasks.Task.Run(() =>
                {
                    var results = new List<(string ProjectID, string ProjectName)>();

                    // Check Azure connection first
                    if (!AzureDbManager.CheckConnection(out string connError))
                    {
                        throw new Exception($"Cannot connect to Azure: {connError}");
                    }

                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    // Get distinct ProjectIDs from user's snapshots, join with Projects table for names
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT DISTINCT
                            ps.ProjectID,
                            COALESCE(p.Description, ps.ProjectID) as ProjectName
                        FROM VMS_ProgressSnapshots ps
                        LEFT JOIN VMS_Projects p ON ps.ProjectID = p.ProjectID
                        WHERE ps.AssignedTo = @username
                        ORDER BY ps.ProjectID";
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser.Username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add((reader.GetString(0), reader.GetString(1)));
                    }

                    return results;
                });

                // Update UI on main thread
                txtLoading.Visibility = Visibility.Collapsed;
                lvProjects.Visibility = Visibility.Visible;

                foreach (var (projectId, projectName) in projectList)
                {
                    _projects.Add(new ProjectSelectionItem
                    {
                        ProjectID = projectId,
                        ProjectName = projectName,
                        IsSelected = false
                    });
                }

                // Show message if no snapshots exist
                if (_projects.Count == 0)
                {
                    txtNoSnapshots.Visibility = Visibility.Visible;
                    txtNoSnapshots.Text = "No submitted snapshots found. You must Submit Week in Progress module first.";
                }
                else
                {
                    btnImport.IsEnabled = true;
                }

                AppLogger.Info($"Loaded {_projects.Count} projects from snapshots for P6 import dialog", "P6ImportDialog.LoadUserProjectsAsync");
            }
            catch (Exception ex)
            {
                txtLoading.Visibility = Visibility.Collapsed;
                lvProjects.Visibility = Visibility.Visible;
                txtError.Text = ex.Message;
                txtError.Visibility = Visibility.Visible;
                AppLogger.Error(ex, "P6ImportDialog.LoadUserProjectsAsync");
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
                    txtWeekEndHelper.Foreground = ThemeHelper.WarningText;
                }
                else
                {
                    txtWeekEndHelper.Text = "Week ending date confirmed";
                    txtWeekEndHelper.Foreground = ThemeHelper.TextColorSecondary;
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
