using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class OwnershipCheckDialog : Window
    {
        public OwnershipCheckDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += OwnershipCheckDialog_Loaded;
        }

        private async void OwnershipCheckDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
        }

        // Load all projects from Azure
        private async Task LoadProjectsAsync()
        {
            var projects = new List<ProjectItem>();

            try
            {
                await Task.Run(() =>
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 30;
                    cmd.CommandText = "SELECT ProjectID, Description FROM VMS_Projects ORDER BY ProjectID DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var id = reader.GetString(0);
                        var desc = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        projects.Add(new ProjectItem
                        {
                            ProjectID = id,
                            Description = desc
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "OwnershipCheckDialog.LoadProjectsAsync");
                AppMessageBox.Show($"Error loading projects: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cboProject.ItemsSource = projects;
            if (projects.Count > 0)
                cboProject.SelectedIndex = 0;
        }

        private async void BtnCheck_Click(object sender, RoutedEventArgs e)
        {
            var selected = cboProject.SelectedItem as ProjectItem;
            if (selected == null)
            {
                AppMessageBox.Show("Please select a project.", "Ownership Check",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show loading state
            btnCheck.IsEnabled = false;
            pnlLoading.Visibility = Visibility.Visible;
            txtEmpty.Visibility = Visibility.Collapsed;
            lstResults.Visibility = Visibility.Collapsed;
            txtStatus.Text = "";

            try
            {
                var issues = await OwnershipHelper.CheckSplitOwnershipAsync(selected.ProjectID);

                pnlLoading.Visibility = Visibility.Collapsed;

                if (issues.Count == 0)
                {
                    txtEmpty.Text = "No split ownership found. All SchedActNOs have a single owner.";
                    txtEmpty.Visibility = Visibility.Visible;
                    txtStatus.Text = "Check complete — no issues found.";
                }
                else
                {
                    lstResults.ItemsSource = issues;
                    lstResults.Visibility = Visibility.Visible;

                    int uniqueActNOs = issues.Select(i => i.SchedActNO).Distinct().Count();
                    int totalRecords = issues.Sum(i => i.RecordCount);
                    int owners = issues.Select(i => i.AssignedTo).Distinct().Count();

                    txtStatus.Text = $"{uniqueActNOs} split ActNO(s) involving {owners} user(s), {totalRecords} total records.";
                }
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                txtEmpty.Text = "Error checking ownership. Check logs for details.";
                txtEmpty.Visibility = Visibility.Visible;
                txtStatus.Text = $"Error: {ex.Message}";
                AppLogger.Error(ex, "OwnershipCheckDialog.BtnCheck_Click");
            }
            finally
            {
                btnCheck.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ProjectItem
        {
            public string ProjectID { get; set; } = null!;
            public string Description { get; set; } = "";
            public string Display => string.IsNullOrEmpty(Description) ? ProjectID : $"{ProjectID} — {Description}";
        }
    }
}
