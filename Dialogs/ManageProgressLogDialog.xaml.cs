using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageProgressLogDialog : Window
    {
        private List<ProgressLogUploadItem> _uploads = new();

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

        private async System.Threading.Tasks.Task LoadUploadsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvUploads.Visibility = Visibility.Collapsed;
            txtNoUploads.Visibility = Visibility.Collapsed;

            try
            {
                _uploads = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<ProgressLogUploadItem>();

                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT UploadID, ProjectID, RespParty, WeekEndDate,
                               UploadUtcDate, RecordCount, Username, UploadedBy
                        FROM VMS_ProgressLogUploads
                        ORDER BY UploadUtcDate DESC, ProjectID, RespParty";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new ProgressLogUploadItem
                        {
                            UploadID = reader.GetInt32(0),
                            ProjectID = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            RespParty = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            WeekEndDate = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            UploadUtcDate = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            RecordCount = reader.GetInt32(5),
                            Username = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            UploadedBy = reader.IsDBNull(7) ? "" : reader.GetString(7)
                        };
                        item.PropertyChanged += UploadItem_PropertyChanged;
                        list.Add(item);
                    }
                    return list;
                });

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_uploads.Count == 0)
                {
                    txtNoUploads.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                }
                else
                {
                    lvUploads.ItemsSource = _uploads;
                    lvUploads.Visibility = Visibility.Visible;
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

            txtSelectionSummary.Text = $"{batchCount} batch(es) selected ({recordCount} records)";
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
            string summary = string.Join("\n", selected.Select(u =>
                $"  {u.Username} / {u.ProjectID} / {u.RespParty} / {u.WeekEndDateDisplay} ({u.RecordCount} records)"));

            var confirmResult = MessageBox.Show(
                $"Delete {selected.Count} upload batch(es) ({totalRecords} total records) from the Progress Log?\n\n{summary}\n\n" +
                "This will permanently remove records from VANTAGE_global_ProgressLog.\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ShowOperationLoading($"Deleting {totalRecords} record(s) from Progress Log...");

            try
            {
                var overallSw = System.Diagnostics.Stopwatch.StartNew();
                var result = await System.Threading.Tasks.Task.Run(() =>
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
                            using var deleteCmd = conn.CreateCommand();
                            deleteCmd.CommandTimeout = 0; // no timeout - table is large
                            deleteCmd.CommandText = @"
                                DELETE FROM VANTAGE_global_ProgressLog
                                WHERE Tag_ProjectID = @projectId
                                  AND UDF18 = @respParty
                                  AND [Timestamp] = @uploadUtcDate
                                  AND Val_TimeStamp = @weekEndDate";
                            deleteCmd.Parameters.AddWithValue("@projectId", upload.ProjectID);
                            deleteCmd.Parameters.AddWithValue("@respParty", upload.RespParty);
                            deleteCmd.Parameters.AddWithValue("@uploadUtcDate", DateTime.Parse(upload.UploadUtcDate));
                            deleteCmd.Parameters.AddWithValue("@weekEndDate", DateTime.Parse(upload.WeekEndDate));
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            int deleted = deleteCmd.ExecuteNonQuery();
                            sw.Stop();
                            totalDeleted += deleted;
                            AppLogger.Info($"Deleted {deleted} ProgressLog records for {upload.ProjectID}/{upload.RespParty} in {sw.Elapsed.TotalSeconds:F1}s",
                                "ManageProgressLogDialog.BtnDelete_Click", "System");

                            // Then delete tracking record
                            using var trackCmd = conn.CreateCommand();
                            trackCmd.CommandText = "DELETE FROM VMS_ProgressLogUploads WHERE UploadID = @uploadId";
                            trackCmd.Parameters.AddWithValue("@uploadId", upload.UploadID);
                            trackCmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            failedBatches.Add($"{upload.ProjectID}/{upload.RespParty}: {ex.Message}");
                            AppLogger.Error(ex, "ManageProgressLogDialog.BtnDelete_Click");
                        }
                    }

                    return (totalDeleted, failedBatches);
                });

                overallSw.Stop();
                string elapsed = overallSw.Elapsed.TotalSeconds < 60
                    ? $"{overallSw.Elapsed.TotalSeconds:F1}s"
                    : $"{overallSw.Elapsed.TotalMinutes:F1}m";

                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                AppLogger.Info(
                    $"Admin deleted {result.totalDeleted} ProgressLog records from {selected.Count} batch(es) in {elapsed}",
                    "ManageProgressLogDialog.BtnDelete_Click",
                    currentUser);

                if (result.failedBatches.Count > 0)
                {
                    string failList = string.Join("\n", result.failedBatches);
                    MessageBox.Show(
                        $"Deleted {result.totalDeleted} record(s), but {result.failedBatches.Count} batch(es) failed:\n\n{failList}\n\nElapsed: {elapsed}",
                        "Partial Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Successfully deleted {result.totalDeleted} record(s) from {selected.Count} batch(es).\n\nElapsed: {elapsed}",
                        "Delete Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Reload the list
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
            }
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
        public int UploadID { get; set; }
        public string ProjectID { get; set; } = string.Empty;
        public string RespParty { get; set; } = string.Empty;
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
