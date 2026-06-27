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
                    cmd.CommandTimeout = 300;
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
            await LoadDeletedRecordsAsync();
        }

        // Awaitable load so callers like BtnPurge_Click can wait for the refresh
        // to complete before their finally blocks run. Calling the async void
        // BtnRefresh_Click handler directly used to fire-and-forget — the
        // purge's finally would reset the busy indicator and status text before
        // the Azure round-trip completed, making it look like nothing happened.
        private async Task LoadDeletedRecordsAsync()
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
                    // Limited column list keeps the grid fast — full record is fetched at export time.
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

                    // Resolve column ordinals once instead of by-name per cell — at 100k
                    // deleted rows that removes ~15 name lookups per row.
                    int oActivityID = reader.GetOrdinal("ActivityID");
                    int oUniqueID = reader.GetOrdinal("UniqueID");
                    int oCompType = reader.GetOrdinal("CompType");
                    int oPhaseCategory = reader.GetOrdinal("PhaseCategory");
                    int oROCStep = reader.GetOrdinal("ROCStep");
                    int oDescription = reader.GetOrdinal("Description");
                    int oPhaseCode = reader.GetOrdinal("PhaseCode");
                    int oSchedActNO = reader.GetOrdinal("SchedActNO");
                    int oUDF1 = reader.GetOrdinal("UDF1");
                    int oUDF2 = reader.GetOrdinal("UDF2");
                    int oQuantity = reader.GetOrdinal("Quantity");
                    int oBudgetMHs = reader.GetOrdinal("BudgetMHs");
                    int oPercentEntry = reader.GetOrdinal("PercentEntry");
                    int oUpdatedBy = reader.GetOrdinal("UpdatedBy");
                    int oUpdatedUtcDate = reader.GetOrdinal("UpdatedUtcDate");

                    // Ordinal-based safe readers (tolerate NULLs; Dbl tolerates real/decimal/int)
                    string S(int i) => reader.IsDBNull(i) ? "" : reader.GetString(i);
                    int I(int i) => reader.IsDBNull(i) ? 0 : reader.GetInt32(i);
                    double Dbl(int i) => reader.IsDBNull(i) ? 0 : Convert.ToDouble(reader.GetValue(i));
                    DateTime? Dt(int i)
                    {
                        if (reader.IsDBNull(i)) return null;
                        return DateTime.TryParse(reader.GetString(i), out var dt) ? dt : (DateTime?)null;
                    }

                    while (reader.Read())
                    {
                        results.Add(new Activity
                        {
                            ActivityID = I(oActivityID),
                            UniqueID = S(oUniqueID),
                            CompType = S(oCompType),
                            PhaseCategory = S(oPhaseCategory),
                            ROCStep = S(oROCStep),
                            Description = S(oDescription),
                            PhaseCode = S(oPhaseCode),
                            SchedActNO = S(oSchedActNO),
                            UDF1 = S(oUDF1),
                            UDF2 = S(oUDF2),
                            Quantity = Dbl(oQuantity),
                            BudgetMHs = Dbl(oBudgetMHs),
                            PercentEntry = Dbl(oPercentEntry),
                            UpdatedBy = S(oUpdatedBy),
                            UpdatedUtcDate = Dt(oUpdatedUtcDate)
                        });
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
                AppLogger.Error(ex, "DeletedRecordsView.LoadDeletedRecordsAsync");
                txtStatus.Text = "Error loading records";
            }
            finally
            {
                SetActionBusy(false);
            }
        }

        // Full mapper used only at export time — covers every Activity property. Skips LocalDirty (local-only).
        private static Activity MapReaderToFullActivity(SqlDataReader reader)
        {
            string GetStringSafe(string name)
            {
                try { int i = reader.GetOrdinal(name); return reader.IsDBNull(i) ? "" : reader.GetString(i); }
                catch { return ""; }
            }

            int GetIntSafe(string name)
            {
                try { int i = reader.GetOrdinal(name); return reader.IsDBNull(i) ? 0 : reader.GetInt32(i); }
                catch { return 0; }
            }

            long GetLongSafe(string name)
            {
                try { int i = reader.GetOrdinal(name); return reader.IsDBNull(i) ? 0L : reader.GetInt64(i); }
                catch { return 0L; }
            }

            // Tolerates float / real / decimal / int columns
            double GetDoubleSafe(string name)
            {
                try
                {
                    int i = reader.GetOrdinal(name);
                    if (reader.IsDBNull(i)) return 0;
                    return Convert.ToDouble(reader.GetValue(i));
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

            var activity = new Activity();
            activity.BeginInit();

            activity.ActivityID = GetIntSafe("ActivityID");
            activity.UniqueID = GetStringSafe("UniqueID");
            activity.SyncVersion = GetLongSafe("SyncVersion");

            activity.Area = GetStringSafe("Area");
            activity.AssignedTo = GetStringSafe("AssignedTo");
            activity.AzureUploadUtcDate = GetDateTimeSafe("AzureUploadUtcDate");
            activity.Aux1 = GetStringSafe("Aux1");
            activity.Aux2 = GetStringSafe("Aux2");
            activity.Aux3 = GetStringSafe("Aux3");
            activity.BaseUnit = GetDoubleSafe("BaseUnit");
            activity.BudgetMHs = GetDoubleSafe("BudgetMHs");
            activity.BudgetHoursGroup = GetDoubleSafe("BudgetHoursGroup");
            activity.BudgetHoursROC = GetDoubleSafe("BudgetHoursROC");
            activity.ChgOrdNO = GetStringSafe("ChgOrdNO");
            activity.ClientBudget = GetDoubleSafe("ClientBudget");
            activity.ClientCustom3 = GetDoubleSafe("ClientCustom3");
            activity.ClientEquivQty = GetDoubleSafe("ClientEquivQty");
            activity.CompType = GetStringSafe("CompType");
            activity.CreatedBy = GetStringSafe("CreatedBy");
            activity.DateTrigger = GetIntSafe("DateTrigger");
            activity.Description = GetStringSafe("Description");
            activity.DwgNO = GetStringSafe("DwgNO");
            activity.EarnedMHsRoc = GetDoubleSafe("EarnedMHsRoc");
            activity.EarnQtyEntry = GetDoubleSafe("EarnQtyEntry");
            activity.EqmtNO = GetStringSafe("EqmtNO");
            activity.EquivQTY = GetDoubleSafe("EquivQTY");
            activity.EquivUOM = GetStringSafe("EquivUOM");
            activity.Estimator = GetStringSafe("Estimator");
            activity.HexNO = GetIntSafe("HexNO");
            activity.HtTrace = GetStringSafe("HtTrace");
            activity.InsulType = GetStringSafe("InsulType");
            activity.LineNumber = GetStringSafe("LineNumber");
            activity.MtrlSpec = GetStringSafe("MtrlSpec");
            activity.Notes = GetStringSafe("Notes");
            activity.PaintCode = GetStringSafe("PaintCode");
            activity.PercentEntry = GetDoubleSafe("PercentEntry");
            activity.PhaseCategory = GetStringSafe("PhaseCategory");
            activity.PhaseCode = GetStringSafe("PhaseCode");
            activity.PipeGrade = GetStringSafe("PipeGrade");
            activity.PipeSize1 = GetDoubleSafe("PipeSize1");
            activity.PipeSize2 = GetDoubleSafe("PipeSize2");
            activity.PrevEarnMHs = GetDoubleSafe("PrevEarnMHs");
            activity.PrevEarnQTY = GetDoubleSafe("PrevEarnQTY");
            activity.ProgDate = GetDateTimeSafe("ProgDate");
            activity.ProjectID = GetStringSafe("ProjectID");
            activity.Quantity = GetDoubleSafe("Quantity");
            activity.RevNO = GetStringSafe("RevNO");
            activity.RFINO = GetStringSafe("RFINO");
            activity.ROCBudgetQTY = GetDoubleSafe("ROCBudgetQTY");
            activity.ROCID = GetDoubleSafe("ROCID");
            activity.ROCPercent = GetDoubleSafe("ROCPercent");
            activity.ROCStep = GetStringSafe("ROCStep");
            activity.SchedActNO = GetStringSafe("SchedActNO");
            activity.ActFin = GetDateTimeSafe("ActFin");
            activity.ActStart = GetDateTimeSafe("ActStart");
            activity.PlanStart = GetDateTimeSafe("PlanStart");
            activity.PlanFin = GetDateTimeSafe("PlanFin");
            activity.SecondActno = GetStringSafe("SecondActno");
            activity.SecondDwgNO = GetStringSafe("SecondDwgNO");
            activity.Service = GetStringSafe("Service");
            activity.ShopField = GetStringSafe("ShopField");
            activity.ShtNO = GetStringSafe("ShtNO");
            activity.SubArea = GetStringSafe("SubArea");
            activity.PjtSystem = GetStringSafe("PjtSystem");
            activity.PjtSystemNo = GetStringSafe("PjtSystemNo");
            activity.TagNO = GetStringSafe("TagNO");
            activity.UDF1 = GetStringSafe("UDF1");
            activity.UDF2 = GetStringSafe("UDF2");
            activity.UDF3 = GetStringSafe("UDF3");
            activity.UDF4 = GetStringSafe("UDF4");
            activity.UDF5 = GetStringSafe("UDF5");
            activity.UDF6 = GetStringSafe("UDF6");
            activity.UDF7 = GetIntSafe("UDF7");
            activity.UDF8 = GetStringSafe("UDF8");
            activity.UDF9 = GetStringSafe("UDF9");
            activity.UDF10 = GetStringSafe("UDF10");
            activity.UDF11 = GetStringSafe("UDF11");
            activity.UDF12 = GetStringSafe("UDF12");
            activity.UDF13 = GetStringSafe("UDF13");
            activity.UDF14 = GetStringSafe("UDF14");
            activity.UDF15 = GetStringSafe("UDF15");
            activity.UDF16 = GetStringSafe("UDF16");
            activity.UDF17 = GetStringSafe("UDF17");
            activity.RespParty = GetStringSafe("RespParty");
            activity.UDF20 = GetStringSafe("UDF20");
            activity.UOM = GetStringSafe("UOM");
            activity.UpdatedBy = GetStringSafe("UpdatedBy");
            activity.UpdatedUtcDate = GetDateTimeSafe("UpdatedUtcDate");
            activity.WeekEndDate = GetDateTimeSafe("WeekEndDate");
            activity.WorkPackage = GetStringSafe("WorkPackage");
            activity.XRay = GetDoubleSafe("XRay");

            activity.EndInit();
            return activity;
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

            // Pre-restore metadata-error scan: the grid loads only 15 columns, so
            // we fetch the full Azure rows to evaluate ActivityValidator.GetAllViolations
            // (required-metadata blanks, conditional date-required rules, % vs. date
            // rules). Any offenders restored as-is would immediately fail the per-row
            // push gate for their assignee — admin should know before flipping IsDeleted.
            SetActionBusy(true, $"Scanning {selectedActivities.Count:N0} record(s)...");
            var selectedUids = selectedActivities.Select(a => a.UniqueID).ToList();
            List<(Activity Activity, List<string> Violations)> offenders;
            try
            {
                offenders = await Task.Run(() =>
                {
                    var full = FetchFullActivitiesByUniqueIds(selectedUids);
                    var list = new List<(Activity, List<string>)>();
                    foreach (var a in full)
                    {
                        var v = ActivityValidator.GetAllViolations(a);
                        if (v.Count > 0) list.Add((a, v));
                    }
                    return list;
                });
            }
            catch (Exception ex)
            {
                SetActionBusy(false);
                AppLogger.Error(ex, "DeletedRecordsView.BtnRestore_Click.Scan");
                AppMessageBox.Show("Error scanning records for metadata issues. See log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SetActionBusy(false);

            string confirmMessage;
            if (offenders.Count > 0)
            {
                var sample = offenders.Take(10)
                    .Select(o => $"  • {o.Activity.UniqueID}: {string.Join("; ", o.Violations)}")
                    .ToList();
                confirmMessage =
                    $"Restore {selectedActivities.Count:N0} record(s)?\n\n" +
                    $"⚠ {offenders.Count:N0} of the selected record(s) have metadata issues " +
                    "and will be blocked from sync by their assignee until fixed:\n\n" +
                    string.Join("\n", sample);
                if (offenders.Count > sample.Count)
                    confirmMessage += $"\n... and {offenders.Count - sample.Count} more";
                confirmMessage += "\n\nRestore anyway?";
            }
            else
            {
                confirmMessage =
                    $"Restore {selectedActivities.Count:N0} record(s)?\n\n" +
                    "Records will be set to IsDeleted=0 and users will receive them on next sync.";
            }

            var result = AppMessageBox.Show(
                confirmMessage,
                "Confirm Restore",
                MessageBoxButton.YesNo,
                offenders.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question);

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

                await LoadDeletedRecordsAsync();
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

                await LoadDeletedRecordsAsync();
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

                // Collect UniqueIDs of the rows visible after grid filters are applied
                var filteredIds = new List<string>();
                if (sfDeletedActivities?.View?.Records != null)
                {
                    foreach (var record in sfDeletedActivities.View.Records)
                    {
                        var dataProp = record.GetType().GetProperty("Data");
                        if (dataProp?.GetValue(record) is Activity a && !string.IsNullOrEmpty(a.UniqueID))
                            filteredIds.Add(a.UniqueID);
                    }
                }
                if (filteredIds.Count == 0)
                    filteredIds = _deletedActivities.Select(a => a.UniqueID).Where(id => !string.IsNullOrEmpty(id)).ToList();

                if (filteredIds.Count == 0)
                {
                    AppMessageBox.Show("No deleted records to export.", "Export Deleted Records",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                if (!AzureDbManager.CheckConnection(out string azureError))
                {
                    AppMessageBox.Show(
                        $"Cannot export - Azure database unavailable:\n\n{azureError}",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SetActionBusy(true, $"Fetching {filteredIds.Count:N0} full record(s) from Azure...");
                txtStatus.Text = "Fetching full records from Azure...";

                var fullRecords = await Task.Run(() => FetchFullActivitiesByUniqueIds(filteredIds));

                SetActionBusy(false);
                txtStatus.Text = "Ready";

                await ExportHelper.ExportDeletedRecordsAsync(this, fullRecords);
            }
            catch (Exception ex)
            {
                SetActionBusy(false);
                txtStatus.Text = "Export error";
                AppLogger.Error(ex, "Export Deleted Records Click", App.CurrentUser?.Username ?? "Unknown");
                AppMessageBox.Show("Export failed. See log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Pulls SELECT * for the given UniqueIDs via temp-table + JOIN — same pattern as
        // BulkUpdateIsDeletedFlag/BulkPurge, avoids the 2100-parameter ceiling.
        private static List<Activity> FetchFullActivitiesByUniqueIds(List<string> uniqueIds)
        {
            var results = new List<Activity>();
            if (uniqueIds.Count == 0) return results;

            using var connection = AzureDbManager.GetConnection();
            connection.Open();

            const string tempTable = "#ExportBatch";
            var createTempCmd = connection.CreateCommand();
            createTempCmd.CommandText = $@"
                IF OBJECT_ID('tempdb..{tempTable}') IS NOT NULL DROP TABLE {tempTable};
                CREATE TABLE {tempTable} (UniqueID NVARCHAR(100) PRIMARY KEY)";
            createTempCmd.ExecuteNonQuery();

            var idTable = new DataTable();
            idTable.Columns.Add("UniqueID", typeof(string));
            foreach (var id in uniqueIds) idTable.Rows.Add(id);

            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tempTable;
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.WriteToServer(idTable);
            }

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandTimeout = 600;
            selectCmd.CommandText = $@"
                SELECT a.*
                FROM VMS_Activities a
                INNER JOIN {tempTable} s ON a.UniqueID = s.UniqueID
                WHERE a.IsDeleted = 1";

            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapReaderToFullActivity(reader));
            }

            return results;
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