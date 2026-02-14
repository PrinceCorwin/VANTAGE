using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VANTAGE.Data;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageProgressLogDialog : Window
    {
        private ObservableCollection<ProgressLogUploadItem> _uploads = new();

        public ManageProgressLogDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ManageProgressLogDialog_Loaded;
        }

        private async void ManageProgressLogDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUploadsAsync();
        }

        private async Task LoadUploadsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            sfUploads.Visibility = Visibility.Collapsed;
            txtNoUploads.Visibility = Visibility.Collapsed;

            try
            {
                var list = await Task.Run(() =>
                {
                    var items = new List<ProgressLogUploadItem>();

                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Username, ProjectID, WeekEndDate, UploadUtcDate,
                               SUM(RecordCount) as RecordCount, UploadedBy
                        FROM VMS_ProgressLogUploads
                        GROUP BY Username, ProjectID, WeekEndDate, UploadUtcDate, UploadedBy
                        ORDER BY UploadUtcDate DESC, ProjectID";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new ProgressLogUploadItem
                        {
                            Username = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            ProjectID = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            WeekEndDate = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            UploadUtcDate = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            RecordCount = reader.GetInt32(4),
                            UploadedBy = reader.IsDBNull(5) ? "" : reader.GetString(5)
                        };
                        item.PropertyChanged += UploadItem_PropertyChanged;
                        items.Add(item);
                    }
                    return items;
                });

                _uploads = new ObservableCollection<ProgressLogUploadItem>(list);
                pnlLoading.Visibility = Visibility.Collapsed;

                if (_uploads.Count == 0)
                {
                    txtNoUploads.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                }
                else
                {
                    sfUploads.ItemsSource = _uploads;
                    sfUploads.Visibility = Visibility.Visible;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "ManageProgressLogDialog.LoadUploadsAsync");
                MessageBox.Show($"Error loading upload records:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UploadItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProgressLogUploadItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selected = _uploads.Where(u => u.IsSelected).ToList();
            int batchCount = selected.Count;
            int recordCount = selected.Sum(u => u.RecordCount);

            txtSelectionSummary.Text = $"{batchCount} batch(es) selected ({recordCount:N0} records)";
            btnDelete.IsEnabled = batchCount > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var upload in _uploads)
                upload.IsSelected = true;
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var upload in _uploads)
                upload.IsSelected = false;
        }

        // REFRESH menu handlers
        private async void MenuRefresh7Days_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Scan Progress Log for uploads from the last 7 days?\n\nThis will import any batches not already tracked.",
                "Confirm Refresh", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await RefreshFromProgressLogAsync(7);
            }
        }

        private async void MenuRefresh30Days_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Scan Progress Log for uploads from the last 30 days?\n\nThis will import any batches not already tracked.",
                "Confirm Refresh", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await RefreshFromProgressLogAsync(30);
            }
        }

        private async void MenuRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Scan Progress Log for ALL uploads?\n\nThis may take several minutes for large datasets.",
                "Confirm Refresh", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await RefreshFromProgressLogAsync(null);
            }
        }

        // Scan VANTAGE_Global_ProgressLog and create tracking entries for batches not in VMS_ProgressLogUploads
        private async Task RefreshFromProgressLogAsync(int? daysBack)
        {
            string rangeText = daysBack.HasValue ? $"last {daysBack} days" : "all time";
            btnRefresh.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnCancel.IsEnabled = false;

            ShowOperationLoading($"Scanning Progress Log ({rangeText})...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Task.Run(() =>
                {
                    int added = 0;
                    int existing = 0;
                    string currentUser = App.CurrentUser?.Username ?? "System";

                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    // Query VANTAGE_Global_ProgressLog grouped by batch identifiers
                    using var queryCmd = conn.CreateCommand();
                    queryCmd.CommandTimeout = 0; // unlimited - table is large

                    string dateFilter = daysBack.HasValue
                        ? $"WHERE [Timestamp] >= DATEADD(day, -{daysBack.Value}, GETDATE())"
                        : "";

                    queryCmd.CommandText = $@"
                        SELECT Tag_ProjectID, Val_TimeStamp, [Timestamp], UserID, COUNT(*) as RecordCount
                        FROM VANTAGE_Global_ProgressLog
                        {dateFilter}
                        GROUP BY Tag_ProjectID, Val_TimeStamp, [Timestamp], UserID
                        ORDER BY [Timestamp] DESC";

                    var batches = new List<(string ProjectID, string WeekEndDate, DateTime WeekEndDateDt, DateTime Timestamp, string UserID, int RecordCount)>();
                    using (var reader = queryCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var projectId = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            // Val_TimeStamp is DateTime in the database
                            var weekEndDateDt = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
                            var weekEndDate = weekEndDateDt == DateTime.MinValue ? "" : weekEndDateDt.ToString("M/d/yyyy");
                            var timestamp = reader.GetDateTime(2);
                            var userId = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            var recordCount = reader.GetInt32(4);
                            batches.Add((projectId, weekEndDate, weekEndDateDt, timestamp, userId, recordCount));
                        }
                    }

                    // For each batch, check if it exists in VMS_ProgressLogUploads
                    foreach (var batch in batches)
                    {
                        string uploadUtcDateStr = batch.Timestamp.ToString("M/d/yyyy h:mm:ss tt");

                        using var checkCmd = conn.CreateCommand();
                        // Compare as DateTime, allowing 2 second tolerance for precision differences
                        checkCmd.CommandText = @"
                            SELECT COUNT(*) FROM VMS_ProgressLogUploads
                            WHERE ProjectID = @projectId
                              AND CAST(TRY_CONVERT(datetime, WeekEndDate) AS DATE) = CAST(@weekEndDate AS DATE)
                              AND ABS(DATEDIFF(second, TRY_CONVERT(datetime, UploadUtcDate), @uploadUtcDate)) < 2";
                        checkCmd.Parameters.AddWithValue("@projectId", batch.ProjectID);
                        checkCmd.Parameters.AddWithValue("@weekEndDate", batch.WeekEndDateDt);
                        checkCmd.Parameters.AddWithValue("@uploadUtcDate", batch.Timestamp);

                        int count = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
                        if (count > 0)
                        {
                            existing++;
                            continue;
                        }

                        // Insert new tracking entry
                        using var insertCmd = conn.CreateCommand();
                        insertCmd.CommandText = @"
                            INSERT INTO VMS_ProgressLogUploads
                                (ProjectID, RespParty, WeekEndDate, UploadUtcDate, RecordCount, Username, UploadedBy)
                            VALUES
                                (@projectId, @respParty, @weekEndDate, @uploadUtcDate, @recordCount, @username, @uploadedBy)";
                        insertCmd.Parameters.AddWithValue("@projectId", batch.ProjectID);
                        insertCmd.Parameters.AddWithValue("@respParty", ""); // empty for legacy imports
                        insertCmd.Parameters.AddWithValue("@weekEndDate", batch.WeekEndDate);
                        insertCmd.Parameters.AddWithValue("@uploadUtcDate", uploadUtcDateStr);
                        insertCmd.Parameters.AddWithValue("@recordCount", batch.RecordCount);
                        insertCmd.Parameters.AddWithValue("@username", currentUser);
                        insertCmd.Parameters.AddWithValue("@uploadedBy", batch.UserID);
                        insertCmd.ExecuteNonQuery();
                        added++;
                    }

                    return (added, existing, batches.Count);
                });

                stopwatch.Stop();
                string elapsed = FormatElapsed(stopwatch.Elapsed);

                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                AppLogger.Info(
                    $"Refreshed Progress Log ({rangeText}): {result.added} added, {result.existing} existing, {result.Item3} total batches in {elapsed}",
                    "ManageProgressLogDialog.RefreshFromProgressLogAsync",
                    currentUser);

                HideOperationLoading();
                MessageBox.Show(
                    $"Refresh complete ({rangeText}):\n\n" +
                    $"• {result.added:N0} new batch(es) imported\n" +
                    $"• {result.existing:N0} batch(es) already tracked\n" +
                    $"• {result.Item3:N0} total batch(es) scanned\n\n" +
                    $"Elapsed: {elapsed}",
                    "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.None);

                await LoadUploadsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProgressLogDialog.RefreshFromProgressLogAsync");
                MessageBox.Show($"Error refreshing from Progress Log:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOperationLoading();
                btnRefresh.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _uploads.Where(u => u.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one upload batch to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalRecords = selected.Sum(u => u.RecordCount);

            // Show confirmation dialog requiring user to type DELETE
            var confirmDialog = new ConfirmDeleteDialog(
                $"You are about to permanently delete {selected.Count} batch(es) containing {totalRecords:N0} records from the Progress Log.\n\n" +
                "This action cannot be undone.\n\n" +
                "Type DELETE to confirm:");
            confirmDialog.Owner = this;

            if (confirmDialog.ShowDialog() != true)
                return;

            btnDelete.IsEnabled = false;
            btnCancel.IsEnabled = false;
            btnRefresh.IsEnabled = false;

            ShowOperationLoading($"Deleting {totalRecords:N0} record(s) from Progress Log...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Task.Run(() =>
                {
                    int totalDeleted = 0;
                    var failedBatches = new List<string>();

                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    foreach (var upload in selected)
                    {
                        try
                        {
                            // Delete from ProgressLog first
                            // Use tolerance-based comparison since tracking table stores dates as strings without milliseconds
                            using var deleteCmd = conn.CreateCommand();
                            deleteCmd.CommandTimeout = 0;
                            deleteCmd.CommandText = @"
                                DELETE FROM VANTAGE_global_ProgressLog
                                WHERE Tag_ProjectID = @projectId
                                  AND ABS(DATEDIFF(second, [Timestamp], @uploadUtcDate)) < 2
                                  AND CAST(Val_TimeStamp AS DATE) = CAST(@weekEndDate AS DATE)";
                            deleteCmd.Parameters.AddWithValue("@projectId", upload.ProjectID);
                            deleteCmd.Parameters.AddWithValue("@uploadUtcDate", DateTime.Parse(upload.UploadUtcDate));
                            deleteCmd.Parameters.AddWithValue("@weekEndDate", DateTime.Parse(upload.WeekEndDate));
                            int deleted = deleteCmd.ExecuteNonQuery();
                            totalDeleted += deleted;

                            // Delete tracking records
                            using var trackCmd = conn.CreateCommand();
                            trackCmd.CommandText = @"
                                DELETE FROM VMS_ProgressLogUploads
                                WHERE Username = @username
                                  AND ProjectID = @projectId
                                  AND WeekEndDate = @weekEndDate
                                  AND UploadUtcDate = @uploadUtcDate";
                            trackCmd.Parameters.AddWithValue("@username", upload.Username);
                            trackCmd.Parameters.AddWithValue("@projectId", upload.ProjectID);
                            trackCmd.Parameters.AddWithValue("@weekEndDate", upload.WeekEndDate);
                            trackCmd.Parameters.AddWithValue("@uploadUtcDate", upload.UploadUtcDate);
                            trackCmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            failedBatches.Add($"{upload.Username}/{upload.ProjectID}/{upload.WeekEndDateDisplay}: {ex.Message}");
                            AppLogger.Error(ex, "ManageProgressLogDialog.BtnDelete_Click");
                        }
                    }

                    return (totalDeleted, failedBatches);
                });

                stopwatch.Stop();
                string elapsed = FormatElapsed(stopwatch.Elapsed);

                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                AppLogger.Info(
                    $"Admin deleted {result.totalDeleted} ProgressLog records from {selected.Count} batch(es) in {elapsed}",
                    "ManageProgressLogDialog.BtnDelete_Click",
                    currentUser);

                HideOperationLoading();
                if (result.failedBatches.Count > 0)
                {
                    string failList = string.Join("\n", result.failedBatches);
                    MessageBox.Show(
                        $"Deleted {result.totalDeleted:N0} record(s), but {result.failedBatches.Count} batch(es) failed:\n\n{failList}\n\nElapsed: {elapsed}",
                        "Partial Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Successfully deleted {result.totalDeleted:N0} record(s) from {selected.Count} batch(es).\n\nElapsed: {elapsed}",
                        "Delete Complete", MessageBoxButton.OK, MessageBoxImage.None);
                }

                await LoadUploadsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProgressLogDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting records:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOperationLoading();
                btnCancel.IsEnabled = true;
                btnRefresh.IsEnabled = true;
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds < 60
                ? $"{elapsed.TotalSeconds:F1}s"
                : $"{elapsed.TotalMinutes:F1}m";
        }

        private void ShowOperationLoading(string message)
        {
            txtOperationStatus.Text = message;
            pnlOperationLoading.Visibility = Visibility.Visible;
        }

        private void HideOperationLoading()
        {
            pnlOperationLoading.Visibility = Visibility.Collapsed;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    // Model for progress log upload tracking record
    public class ProgressLogUploadItem : INotifyPropertyChanged
    {
        public string ProjectID { get; set; } = string.Empty;
        public string WeekEndDate { get; set; } = string.Empty;
        public string UploadUtcDate { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;

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

        // Formatted display properties
        public string WeekEndDateDisplay => DateTime.TryParse(WeekEndDate, out var d)
            ? d.ToString("MM/dd/yyyy") : WeekEndDate;
        public string UploadUtcDateDisplay => DateTime.TryParse(UploadUtcDate, out var d)
            ? d.ToString("MM/dd/yyyy HH:mm:ss") : UploadUtcDate;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
