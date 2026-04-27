using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;
using System.Windows;
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
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (_, __) => ThemeManager.ThemeChanged -= OnThemeChanged;
            Loaded += OnViewLoaded;
            sfDeletedActivities.FilterChanged += SfDeletedActivities_FilterChanged;
        }

        private void SfDeletedActivities_FilterChanged(object? sender, Syncfusion.UI.Xaml.Grid.GridFilterEventArgs e)
        {
            UpdateRecordCount();
        }

        private void UpdateRecordCount()
        {
            int total = _deletedActivities?.Count ?? 0;
            int filtered = sfDeletedActivities.View?.Records?.Count ?? total;

            txtRecordCount.Text = filtered == total
                ? $"{total:N0} deleted records"
                : $"{filtered:N0} of {total:N0} deleted records";
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectsFromAzureAsync();
        }

        // Re-apply Syncfusion skin to grid when theme changes
        private void OnThemeChanged(string themeName)
        {
            Dispatcher.Invoke(() =>
            {
                var sfTheme = new Theme(ThemeManager.GetSyncfusionThemeName());
                SfSkinManager.SetTheme(sfDeletedActivities, sfTheme);
            });
        }

        private async Task LoadProjectsFromAzureAsync()
        {
            try
            {
                // Check connection to Azure
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    AppMessageBox.Show(
                        $"Cannot load projects - Azure database unavailable:\n\n{errorMessage}\n\n" +
                        "Please check your internet connection and try again.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Load projects on background thread
                _projects = await Task.Run(() =>
                {
                    var projects = new List<ProjectSelection>();

                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT ProjectID FROM VMS_Activities WHERE IsDeleted = 1 ORDER BY ProjectID";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var projectId = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(projectId))
                        {
                            projects.Add(new ProjectSelection { ProjectID = projectId, IsSelected = false });
                        }
                    }

                    return projects;
                });

                lstProjectFilter.ItemsSource = _projects;
                txtStatus.Text = $"Found {_projects.Count} projects with deleted records";

                // Hide overlay and enable content
                loadingOverlay.Visibility = Visibility.Collapsed;
                mainContent.IsEnabled = true;
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                mainContent.IsEnabled = true;
                AppMessageBox.Show("Error loading projects. See log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.LoadProjectsFromAzureAsync");
            }
        }

        // Show/hide the main-grid action overlay with the supplied header text.
        private void SetActionBusy(bool busy, string header = "")
        {
            if (busy)
            {
                actionBusyIndicator.Header = header;
                actionBusyIndicator.IsBusy = true;
                actionBusyOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                actionBusyIndicator.IsBusy = false;
                actionBusyOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnRefresh_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                if (!AzureDbManager.CheckConnection(out string errorMessage))
                {
                    AppMessageBox.Show(
                        $"Cannot refresh - Azure database unavailable:\n\n{errorMessage}\n\n" +
                        "Please try again when connected.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var selectedProjects = _projects?.Where(p => p.IsSelected).Select(p => p.ProjectID).ToList();

                if (selectedProjects == null || !selectedProjects.Any())
                {
                    AppMessageBox.Show(
                        "Please select at least one project to view deleted records.",
                        "No Projects Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
                    return;
                }

                SetActionBusy(true, "Loading deleted records...");
                txtStatus.Text = "Loading deleted records from Azure...";

                _deletedActivities = await Task.Run(() =>
                {
                    var results = new List<Activity>();

                    using var connection = AzureDbManager.GetConnection();
                    connection.Open();

                    var projectParams = string.Join(",", selectedProjects.Select((p, i) => $"@p{i}"));
                    var cmd = connection.CreateCommand();
                    cmd.CommandTimeout = 300;
                    // Explicit column list — matches MapReaderToActivity (avoids pulling unused
                    // columns over Azure latency for large result sets).
                    cmd.CommandText = $@"
                SELECT ActivityID, UniqueID, CompType, PhaseCategory, ROCStep, Description,
                       PhaseCode, SchedActNO, UDF1, UDF2, Quantity, BudgetMHs, PercentEntry,
                       UpdatedBy, UpdatedUtcDate
                FROM VMS_Activities
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
                        results.Add(MapReaderToActivity(reader));
                    }

                    return results;
                });

                sfDeletedActivities.ItemsSource = _deletedActivities;
                UpdateRecordCount();
                txtStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                AppMessageBox.Show("Error loading deleted records. See log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnRefresh_Click");
                txtStatus.Text = "Error loading records";
            }
            finally
            {
                SetActionBusy(false);
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

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_deletedActivities == null || _deletedActivities.Count == 0)
            {
                AppMessageBox.Show("No records loaded. Click REFRESH first.",
                    "Nothing to Select", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            sfDeletedActivities.SelectAll();
            sfDeletedActivities.Focus();
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                AppMessageBox.Show("Please select one or more records to restore.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var result = AppMessageBox.Show(
                $"Restore {selectedActivities.Count:N0} record(s)?\n\n" +
                "Records will be set to IsDeleted=0 and users will receive them on next sync.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                SetActionBusy(true, $"Restoring {selectedActivities.Count:N0} record(s)...");
                txtStatus.Text = "Restoring records...";

                var uniqueIds = selectedActivities.Select(a => a.UniqueID).ToList();
                var username = App.CurrentUser?.Username ?? "Admin";
                var utcDate = DateTime.UtcNow.ToString("o");

                int restored = await Task.Run(() => BulkUpdateIsDeletedFlag(uniqueIds, username, utcDate, restoring: true));

                AppMessageBox.Show($"Successfully restored {restored:N0} record(s).\n\nUsers will receive them on next sync.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.None);

                AppLogger.Info($"Admin restored {restored} records", "DeletedRecordsView.BtnRestore_Click", App.CurrentUser?.Username);

                BtnRefresh_Click(sender, e);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show("Error restoring records. See log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnRestore_Click");
            }
            finally
            {
                SetActionBusy(false);
                txtStatus.Text = "Ready";
            }
        }

        // Single-statement bulk update via temp table + SqlBulkCopy + INNER JOIN.
        // Why: WHERE UniqueID IN (@u0..@uN) hits SQL Server's 2100-parameter ceiling
        // and produces poor query plans at scale. SqlBulkCopy + JOIN is the same
        // pattern SyncManager uses for thousands-of-rows operations.
        private static int BulkUpdateIsDeletedFlag(List<string> uniqueIds, string username, string utcDate, bool restoring)
        {
            using var connection = AzureDbManager.GetConnection();
            connection.Open();

            var tempTable = "#RestoreBatch";
            var createTempCmd = connection.CreateCommand();
            createTempCmd.CommandText = $@"
                IF OBJECT_ID('tempdb..{tempTable}') IS NOT NULL DROP TABLE {tempTable};
                CREATE TABLE {tempTable} (UniqueID NVARCHAR(100) PRIMARY KEY)";
            createTempCmd.ExecuteNonQuery();

            var idTable = new DataTable();
            idTable.Columns.Add("UniqueID", typeof(string));
            foreach (var id in uniqueIds)
            {
                idTable.Rows.Add(id);
            }

            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tempTable;
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.WriteToServer(idTable);
            }

            var newFlag = restoring ? 0 : 1;
            var updateCmd = connection.CreateCommand();
            updateCmd.CommandTimeout = 600;
            updateCmd.CommandText = $@"
                UPDATE a
                SET IsDeleted = {newFlag},
                    UpdatedBy = @user,
                    UpdatedUtcDate = @date
                FROM VMS_Activities a
                INNER JOIN {tempTable} s ON a.UniqueID = s.UniqueID";
            updateCmd.Parameters.AddWithValue("@user", username);
            updateCmd.Parameters.AddWithValue("@date", utcDate);

            return updateCmd.ExecuteNonQuery();
        }

        private async void BtnPurge_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                AppMessageBox.Show("Please select one or more records to purge.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var result = AppMessageBox.Show(
                $"PERMANENTLY DELETE {selectedActivities.Count:N0} record(s)?\n\n" +
                "⚠️ WARNING: This action CANNOT be undone!\n" +
                "⚠️ Records will be DELETED from Azure database FOREVER!\n\n" +
                "Are you absolutely sure?",
                "⚠️ PERMANENT DELETION WARNING ⚠️",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var doubleCheck = AppMessageBox.Show(
                "FINAL WARNING: Click YES to PERMANENTLY DELETE.",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (doubleCheck != MessageBoxResult.Yes)
                return;

            try
            {
                SetActionBusy(true, $"Purging {selectedActivities.Count:N0} record(s)...");
                txtStatus.Text = "Purging records...";

                var uniqueIds = selectedActivities.Select(a => a.UniqueID).ToList();

                int purged = await Task.Run(() => BulkPurge(uniqueIds));

                AppMessageBox.Show($"Permanently deleted {purged:N0} record(s) from Azure database.",
                    "Purge Complete", MessageBoxButton.OK, MessageBoxImage.None);

                AppLogger.Warning($"Admin purged {purged} records permanently", "DeletedRecordsView.BtnPurge_Click", App.CurrentUser?.Username);

                BtnRefresh_Click(sender, e);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show("Error purging records. See log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLogger.Error(ex, "DeletedRecordsView.BtnPurge_Click");
            }
            finally
            {
                SetActionBusy(false);
                txtStatus.Text = "Ready";
            }
        }

        // Single-statement bulk delete via temp table + SqlBulkCopy + INNER JOIN.
        // Same rationale as BulkUpdateIsDeletedFlag — avoids the 2100-parameter
        // limit and gets a clean index-seek plan.
        private static int BulkPurge(List<string> uniqueIds)
        {
            using var connection = AzureDbManager.GetConnection();
            connection.Open();

            var tempTable = "#PurgeBatch";
            var createTempCmd = connection.CreateCommand();
            createTempCmd.CommandText = $@"
                IF OBJECT_ID('tempdb..{tempTable}') IS NOT NULL DROP TABLE {tempTable};
                CREATE TABLE {tempTable} (UniqueID NVARCHAR(100) PRIMARY KEY)";
            createTempCmd.ExecuteNonQuery();

            var idTable = new DataTable();
            idTable.Columns.Add("UniqueID", typeof(string));
            foreach (var id in uniqueIds)
            {
                idTable.Rows.Add(id);
            }

            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tempTable;
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.WriteToServer(idTable);
            }

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandTimeout = 600;
            deleteCmd.CommandText = $@"
                DELETE a
                FROM VMS_Activities a
                INNER JOIN {tempTable} s ON a.UniqueID = s.UniqueID
                WHERE a.IsDeleted = 1";

            return deleteCmd.ExecuteNonQuery();
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_deletedActivities == null || _deletedActivities.Count == 0)
                {
                    AppMessageBox.Show("No deleted records to export.", "Export Deleted Records",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                await ExportHelper.ExportDeletedRecordsAsync(this, _deletedActivities);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Export Deleted Records Click", App.CurrentUser?.Username ?? "Unknown");
                AppMessageBox.Show("Export failed. See log for details.", "Error",
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