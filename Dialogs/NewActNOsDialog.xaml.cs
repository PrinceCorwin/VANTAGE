using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Shown immediately after a P6 import when there are SchedActNOs in the imported file
    // that aren't in the snapshot for the imported week. User picks the subset to create as
    // stub Activity + Snapshot rows. One ProjectID per batch.
    public partial class NewActNOsDialog : Window
    {
        private readonly ObservableCollection<MissingActNOCandidate> _rows;
        private readonly System.DateTime _weekEndDate;
        private readonly string _username;

        // Set to true when at least one record was created successfully — caller uses this
        // to decide whether to refresh the Schedule view / update the success message.
        public int CreatedCount { get; private set; }

        public NewActNOsDialog(
            List<MissingActNOCandidate> candidates,
            List<string> availableProjectIds,
            System.DateTime weekEndDate,
            string username)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _rows = new ObservableCollection<MissingActNOCandidate>(candidates ?? new List<MissingActNOCandidate>());
            _weekEndDate = weekEndDate;
            _username = username;

            sfCandidates.ItemsSource = _rows;

            cmbProject.ItemsSource = availableProjectIds;
            if (availableProjectIds != null && availableProjectIds.Count > 0)
                cmbProject.SelectedIndex = 0;

            txtIntro.Text =
                $"P6 has {_rows.Count} SchedActNO(s) that aren't in your snapshot for week ending " +
                $"{_weekEndDate:yyyy-MM-dd}. Check the rows you want to create. Required metadata not " +
                $"in P6 (WorkPackage, PhaseCode, CompType, PhaseCategory, ROCStep, RespParty) will be " +
                $"set to 'X' so you can fix them up later in Progress.";

            UpdateSelectionCount();

            // Refresh count when any row's IsSelected toggles.
            foreach (var r in _rows)
                r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MissingActNOCandidate.IsSelected)) UpdateSelectionCount(); };
        }

        private void UpdateSelectionCount()
        {
            int picked = _rows.Count(r => r.IsSelected);
            txtSelectionCount.Text = $"{picked} of {_rows.Count} selected";
            btnCreate.IsEnabled = picked > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.IsSelected = true;
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.IsSelected = false;
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            var picked = _rows.Where(r => r.IsSelected).ToList();
            if (picked.Count == 0) return;

            string? projectId = cmbProject.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(projectId))
            {
                AppMessageBox.Show(
                    "Select a ProjectID for the new records.",
                    "Project Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!AzureDbManager.CheckConnection(out string connError))
            {
                AppMessageBox.Show(
                    $"Cannot connect to Azure database. New records can't be created right now.\n\n{connError}",
                    "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            btnCreate.IsEnabled = false;
            btnCancel.IsEnabled = false;
            busyOverlay.Visibility = Visibility.Visible;
            txtBusyMessage.Text = $"Creating {picked.Count} record(s)...";

            try
            {
                using var _opTracker = LongRunningOps.Begin();

                int created = await ScheduleRepository.CreateStubActivitiesFromP6Async(
                    picked, _weekEndDate, projectId, _username);

                CreatedCount = created;
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error(ex, "NewActNOsDialog.BtnCreate_Click", _username);
                AppMessageBox.Show(
                    "Failed to create the new records. See log for details.",
                    "Create Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                btnCreate.IsEnabled = true;
                btnCancel.IsEnabled = true;
                busyOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
