using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using VANTAGE.Models;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class DeletedRecordsView : Window
    {
        private List<Activity>? _deletedActivities;
        private List<ProjectSelection>? _projects;

        public DeletedRecordsView()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            LoadProjectsFromAzure();
        }

        private void LoadProjectsFromAzure()
        {
            try
            {
                busyIndicator.IsBusy = true;

                // Check connection to Azure
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    MessageBox.Show(
                        $"Cannot load projects - Azure database unavailable:\n\n{errorMessage}\n\n" +
                        "Please check your internet connection and try again.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Get distinct ProjectIDs from Azure where IsDeleted = 1
                using var connection = AzureDbManager.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT ProjectID FROM VMS_Activities WHERE IsDeleted = 1 ORDER BY ProjectID";

                _projects = new List<ProjectSelection>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var projectId = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        _projects.Add(new ProjectSelection { ProjectID = projectId, IsSelected = false });
                    }
                }

                lstProjectFilter.ItemsSource = _projects;
                txtStatus.Text = $"Found {_projects.Count} projects with deleted records";
                busyIndicator.IsBusy = false;
            }
            catch (Exception ex)
            {
                busyIndicator.IsBusy = false;
                MessageBox.Show($"Error loading projects: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.LoadProjectsFromAzure");
            }
        }

        private async void BtnRefresh_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                busyIndicator.IsBusy = true;

                // Check connection to Azure
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    MessageBox.Show(
                        $"Cannot refresh - Azure database unavailable:\n\n{errorMessage}\n\n" +
                        "Please try again when connected.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Get selected projects
                var selectedProjects = _projects?.Where(p => p.IsSelected).Select(p => p.ProjectID).ToList();

                if (selectedProjects == null || !selectedProjects.Any())
                {
                    MessageBox.Show(
                        "Please select at least one project to view deleted records.",
                        "No Projects Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
                    return;
                }

                txtStatus.Text = "Loading deleted records from Azure...";

                _deletedActivities = await Task.Run(() =>
                {
                    var results = new List<Activity>();

                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    var projectParams = string.Join(",", selectedProjects.Select((p, i) => $"@p{i}"));
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"
                SELECT * FROM VMS_Activities
                WHERE IsDeleted = 1
                  AND ProjectID IN ({projectParams})
                ORDER BY UpdatedUtcDate DESC";

                    for (int i = 0; i < selectedProjects.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", selectedProjects[i]);
                    }

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var activity = MapReaderToActivity(reader);
                        results.Add(activity);
                    }

                    return results;
                });

                sfDeletedActivities.ItemsSource = _deletedActivities;
                txtRecordCount.Text = $"{_deletedActivities.Count} deleted records";
                txtStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading deleted records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnRefresh_Click");
                txtStatus.Text = "Error loading records";
            }
            finally
            {
                busyIndicator.IsBusy = false;
            }
        }

        // Map database reader to Activity object
        private Activity MapReaderToActivity(SqlDataReader reader)
        {
            string GetStringSafe(string name)
            {
                try
                {
                    int i = reader.GetOrdinal(name);
                    return reader.IsDBNull(i) ? "" : reader.GetString(i);
                }
                catch { return ""; }
            }

            int GetIntSafe(string name)
            {
                try
                {
                    int i = reader.GetOrdinal(name);
                    return reader.IsDBNull(i) ? 0 : reader.GetInt32(i);
                }
                catch { return 0; }
            }

            double GetDoubleSafe(string name)
            {
                try
                {
                    int i = reader.GetOrdinal(name);
                    return reader.IsDBNull(i) ? 0 : reader.GetDouble(i);
                }
                catch { return 0; }
            }

            DateTime? GetDateTimeSafe(string name)
            {
                try
                {
                    int i = reader.GetOrdinal(name);
                    if (reader.IsDBNull(i)) return null;
                    var s = reader.GetString(i);
                    if (DateTime.TryParse(s, out var dt)) return dt;
                    return null;
                }
                catch { return null; }
            }

            return new Activity
            {
                ActivityID = GetIntSafe("ActivityID"),
                UniqueID = GetStringSafe("UniqueID"),
                CompType = GetStringSafe("CompType"),
                PhaseCategory = GetStringSafe("PhaseCategory"),
                ROCStep = GetStringSafe("ROCStep"),
                Description = GetStringSafe("Description"),
                PhaseCode = GetStringSafe("PhaseCode"),
                SchedActNO = GetStringSafe("SchedActNO"),
                UDF1 = GetStringSafe("UDF1"),
                UDF2 = GetStringSafe("UDF2"),
                Quantity = GetDoubleSafe("Quantity"),
                BudgetMHs = GetDoubleSafe("BudgetMHs"),
                PercentEntry = GetDoubleSafe("PercentEntry"),
                UpdatedBy = GetStringSafe("UpdatedBy"),
                UpdatedUtcDate = GetDateTimeSafe("UpdatedUtcDate")
            };
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to restore.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var result = MessageBox.Show(
                $"Restore {selectedActivities.Count} record(s)?\n\n" +
                "Records will be set to IsDeleted=0 and users will receive them on next sync.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                busyIndicator.IsBusy = true;
                txtStatus.Text = "Restoring records...";

                var uniqueIds = selectedActivities.Select(a => a.UniqueID).ToList();
                var username = App.CurrentUser?.Username ?? "Admin";

                int restored = await Task.Run(() =>
                {
                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    var uniqueIdParams = string.Join(",", uniqueIds.Select((id, i) => $"@uid{i}"));

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"
                UPDATE VMS_Activities
                SET IsDeleted = 0,
                    UpdatedBy = @user,
                    UpdatedUtcDate = @date
                WHERE UniqueID IN ({uniqueIdParams})";

                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));

                    for (int i = 0; i < uniqueIds.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@uid{i}", uniqueIds[i]);
                    }

                    return cmd.ExecuteNonQuery();
                });

                MessageBox.Show($"Successfully restored {restored} record(s).\n\nUsers will receive them on next sync.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.None);

                AppLogger.Info($"Admin restored {restored} records", "DeletedRecordsView.BtnRestore_Click", App.CurrentUser?.Username);

                // Refresh grid
                await Task.Run(() => { }); // Yield to UI
                BtnRefresh_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnRestore_Click");
            }
            finally
            {
                busyIndicator.IsBusy = false;
                txtStatus.Text = "Ready";
            }
        }

        private async void BtnPurge_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to purge.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var result = MessageBox.Show(
                $"PERMANENTLY DELETE {selectedActivities.Count} record(s)?\n\n" +
                "⚠️ WARNING: This action CANNOT be undone!\n" +
                "⚠️ Records will be DELETED from Azure database FOREVER!\n\n" +
                "Are you absolutely sure?",
                "⚠️ PERMANENT DELETION WARNING ⚠️",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Double confirmation
            var doubleCheck = MessageBox.Show(
                "FINAL WARNING: Click YES to PERMANENTLY DELETE.",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (doubleCheck != MessageBoxResult.Yes)
                return;

            try
            {
                busyIndicator.IsBusy = true;
                txtStatus.Text = "Purging records...";

                var uniqueIds = selectedActivities.Select(a => a.UniqueID).ToList();

                int purged = await Task.Run(() =>
                {
                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    var uniqueIdParams = string.Join(",", uniqueIds.Select((id, i) => $"@uid{i}"));

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"
                DELETE FROM VMS_Activities
                WHERE UniqueID IN ({uniqueIdParams})
                  AND IsDeleted = 1";

                    for (int i = 0; i < uniqueIds.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@uid{i}", uniqueIds[i]);
                    }

                    return cmd.ExecuteNonQuery();
                });

                MessageBox.Show($"Permanently deleted {purged} record(s) from Azure database.",
                    "Purge Complete", MessageBoxButton.OK, MessageBoxImage.None);

                AppLogger.Warning($"Admin purged {purged} records permanently", "DeletedRecordsView.BtnPurge_Click", App.CurrentUser?.Username);

                // Refresh grid
                BtnRefresh_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error purging records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnPurge_Click");
            }
            finally
            {
                busyIndicator.IsBusy = false;
                txtStatus.Text = "Ready";
            }
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_deletedActivities == null || _deletedActivities.Count == 0)
                {
                    MessageBox.Show("No deleted records to export.", "Export Deleted Records",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                await ExportHelper.ExportDeletedRecordsAsync(this, _deletedActivities);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Export Deleted Records Click", App.CurrentUser?.Username ?? "Unknown");
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper class for project selection
        public class ProjectSelection : INotifyPropertyChanged
        {
            private bool _isSelected;
            public string ProjectID { get; set; } = null!;
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
            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}