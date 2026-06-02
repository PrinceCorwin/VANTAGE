using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Modal picker for the Analysis view's snapshot source. Loads the same fast
    // GROUP BY (AssignedTo, ProjectID, WeekEndDate) query ManageSnapshotsDialog uses,
    // pre-checks any items in the caller's prior selection, and returns the user's
    // checked set via SelectedSnapshots when Apply is clicked.
    public partial class SelectAnalysisSnapshotsDialog : Window
    {
        private ObservableCollection<AnalysisSnapshotKey> _rows = new();
        private readonly HashSet<string> _initialKeys;

        // The user's final picked snapshots when DialogResult == true.
        public List<AnalysisSnapshotKey> SelectedSnapshots { get; private set; } = new();

        public SelectAnalysisSnapshotsDialog(IEnumerable<AnalysisSnapshotKey>? currentSelection = null)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _initialKeys = new HashSet<string>(
                (currentSelection ?? System.Linq.Enumerable.Empty<AnalysisSnapshotKey>())
                    .Select(KeyOf),
                System.StringComparer.OrdinalIgnoreCase);

            Loaded += async (_, __) => await LoadAsync();
        }

        // Composite identity for matching pre-existing selection across reloads.
        private static string KeyOf(AnalysisSnapshotKey k) =>
            $"{k.AssignedTo}\u0001{k.ProjectID}\u0001{k.WeekEndDate}";

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                if (!AzureDbManager.CheckConnection(out string connError))
                {
                    busyOverlay.Visibility = Visibility.Collapsed;
                    AppMessageBox.Show(
                        $"Cannot connect to Azure: {connError}",
                        "Connection Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                var list = await System.Threading.Tasks.Task.Run(() =>
                {
                    var results = new List<AnalysisSnapshotKey>();
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 120;
                    // Same shape ManageSnapshotsDialog uses. ProgDate intentionally absent —
                    // scanning it across all rows times out at production volume.
                    cmd.CommandText = @"
                        SELECT AssignedTo, ProjectID, WeekEndDate, COUNT(*) AS [RowCount]
                        FROM VMS_ProgressSnapshots
                        GROUP BY AssignedTo, ProjectID, WeekEndDate
                        ORDER BY WeekEndDate DESC, ProjectID, AssignedTo";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(new AnalysisSnapshotKey
                        {
                            AssignedTo = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString() ?? string.Empty,
                            ProjectID = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1)?.ToString() ?? string.Empty,
                            WeekEndDate = reader.IsDBNull(2) ? string.Empty : reader.GetValue(2)?.ToString() ?? string.Empty,
                            RowCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                        });
                    }
                    return results;
                });

                // Pre-check rows matching the caller's prior selection.
                foreach (var k in list)
                {
                    if (_initialKeys.Contains(KeyOf(k)))
                        k.IsSelected = true;

                    k.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(AnalysisSnapshotKey.IsSelected))
                            UpdateSelectionCount();
                    };
                }

                _rows = new ObservableCollection<AnalysisSnapshotKey>(list);
                sfSnapshots.ItemsSource = _rows;
                busyOverlay.Visibility = Visibility.Collapsed;

                UpdateSelectionCount();
                AppLogger.Info(
                    $"SelectAnalysisSnapshotsDialog loaded {list.Count} snapshot groups",
                    "SelectAnalysisSnapshotsDialog.LoadAsync");
            }
            catch (System.Exception ex)
            {
                AppLogger.Error(ex, "SelectAnalysisSnapshotsDialog.LoadAsync");
                busyOverlay.Visibility = Visibility.Collapsed;
                AppMessageBox.Show(
                    "Failed to load snapshots. See log for details.",
                    "Load Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void UpdateSelectionCount()
        {
            int picked = _rows.Count(r => r.IsSelected);
            txtSelectionCount.Text = $"{picked} of {_rows.Count} selected";
            btnApply.IsEnabled = true;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.IsSelected = true;
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.IsSelected = false;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedSnapshots = _rows.Where(r => r.IsSelected).ToList();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
