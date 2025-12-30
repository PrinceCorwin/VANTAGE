using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class DeleteSnapshotsDialog : Window
    {
        private List<SnapshotWeekItem> _weeks = new();

        public DeleteSnapshotsDialog()
        {
            InitializeComponent();
            Loaded += DeleteSnapshotsDialog_Loaded;
        }

        private async void DeleteSnapshotsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null)
            {
                MessageBox.Show("No user logged in.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            txtUserInfo.Text = $"Snapshots for: {App.CurrentUser.Username}";
            await LoadSnapshotsAsync();
        }

        private async System.Threading.Tasks.Task LoadSnapshotsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvWeeks.Visibility = Visibility.Collapsed;
            txtNoSnapshots.Visibility = Visibility.Collapsed;

            try
            {
                _weeks = await System.Threading.Tasks.Task.Run(() =>
                {
                    var weekList = new List<SnapshotWeekItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT WeekEndDate, COUNT(*) as SnapshotCount
                        FROM ProgressSnapshots
                        WHERE AssignedTo = @username
                        GROUP BY WeekEndDate
                        ORDER BY WeekEndDate DESC";
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string weekEndDateStr = reader.GetString(0);
                        int count = reader.GetInt32(1);

                        if (DateTime.TryParse(weekEndDateStr, out DateTime weekEndDate))
                        {
                            var item = new SnapshotWeekItem
                            {
                                WeekEndDate = weekEndDate,
                                WeekEndDateStr = weekEndDateStr,
                                SnapshotCount = count
                            };
                            item.PropertyChanged += WeekItem_PropertyChanged;
                            weekList.Add(item);
                        }
                    }

                    return weekList;
                });

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_weeks.Count == 0)
                {
                    txtNoSnapshots.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                }
                else
                {
                    lvWeeks.ItemsSource = _weeks;
                    lvWeeks.Visibility = Visibility.Visible;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "DeleteSnapshotsDialog.LoadSnapshotsAsync");
                MessageBox.Show($"Error loading snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WeekItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapshotWeekItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();
            int weekCount = selectedWeeks.Count;
            int snapshotCount = selectedWeeks.Sum(w => w.SnapshotCount);

            txtSelectionSummary.Text = $"{weekCount} week(s) selected ({snapshotCount} snapshots)";
            btnDelete.IsEnabled = weekCount > 0;
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();

            if (selectedWeeks.Count == 0)
            {
                MessageBox.Show("Please select at least one week to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalSnapshots = selectedWeeks.Sum(w => w.SnapshotCount);
            string weekList = string.Join("\n", selectedWeeks.Select(w => $"  - {w.WeekEndDateDisplay} ({w.SnapshotCount} snapshots)"));

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to delete {totalSnapshots} snapshot(s) from the following weeks?\n\n{weekList}\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    int deleted = 0;

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    foreach (var week in selectedWeeks)
                    {
                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            DELETE FROM ProgressSnapshots
                            WHERE AssignedTo = @username
                              AND WeekEndDate = @weekEndDate";
                        cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);
                        cmd.Parameters.AddWithValue("@weekEndDate", week.WeekEndDateStr);

                        deleted += cmd.ExecuteNonQuery();
                    }

                    return deleted;
                });

                AppLogger.Info(
                    $"Deleted {deletedTotal} snapshots from {selectedWeeks.Count} week(s)",
                    "DeleteSnapshotsDialog.BtnDelete_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DeleteSnapshotsDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                btnDelete.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Model for week selection
    public class SnapshotWeekItem : INotifyPropertyChanged
    {
        public DateTime WeekEndDate { get; set; }
        public string WeekEndDateStr { get; set; } = string.Empty;
        public int SnapshotCount { get; set; }

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

        public string WeekEndDateDisplay => $"Week ending {WeekEndDate:MM/dd/yyyy}";
        public string SnapshotCountDisplay => $"({SnapshotCount} snapshots)";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}