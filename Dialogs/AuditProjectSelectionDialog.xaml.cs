using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Source the admin wants the audit to scan.
    public enum AuditScope
    {
        // Pull rows directly from Azure VMS_Activities. Sees everything but Show in
        // ProgressView only finds rows already in the admin's local cache.
        Azure,
        // Pull rows from the local Activities table — only what's been synced down,
        // so Show in ProgressView surfaces every offender.
        Local
    }

    // Pre-step for Admin → Audit All Records. Lists every project (from Azure or local
    // depending on the radio choice) with a checkbox, all checked by default. The
    // header checkbox is a tri-state Excel-style toggle (Select All / Select None /
    // Indeterminate when partial). Returns the chosen scope and project IDs to the caller.
    public partial class AuditProjectSelectionDialog : Window
    {
        // Set on successful Run Audit. Empty if the dialog was cancelled.
        public IReadOnlyList<string> SelectedProjectIds { get; private set; } = Array.Empty<string>();
        public AuditScope SelectedScope { get; private set; } = AuditScope.Azure;

        private readonly ObservableCollection<ProjectChoice> _projects = new();

        // Suppresses re-entrant updates when the header checkbox propagates a state
        // change to individual items, which would otherwise trigger another header
        // recalculation per item.
        private bool _suppressHeaderSync;

        public AuditProjectSelectionDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += AuditProjectSelectionDialog_Loaded;
        }

        private async void AuditProjectSelectionDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await ReloadProjectsAsync();
        }

        // Reloads the project list for the currently-selected scope. Wired to both
        // initial Loaded and the Local/Azure radio click handler.
        private async Task ReloadProjectsAsync()
        {
            try
            {
                pnlLoading.Visibility = Visibility.Visible;
                btnRun.IsEnabled = false;
                foreach (var p in _projects)
                    p.PropertyChanged -= Project_PropertyChanged;
                _projects.Clear();

                AuditScope scope = rbLocal.IsChecked == true ? AuditScope.Local : AuditScope.Azure;
                var loaded = await Task.Run(() => LoadProjects(scope));

                foreach (var p in loaded)
                {
                    p.PropertyChanged += Project_PropertyChanged;
                    _projects.Add(p);
                }

                projectList.ItemsSource = _projects;
                pnlLoading.Visibility = Visibility.Collapsed;
                UpdateHeaderState();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AuditProjectSelectionDialog.ReloadProjectsAsync");
                AppMessageBox.Show($"Error loading projects:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ModeChanged(object sender, RoutedEventArgs e)
        {
            // RadioButton.Click fires for BOTH on-click changes, so reload on every
            // click. Only the now-checked radio's branch is taken inside LoadProjects.
            if (!IsLoaded) return;
            await ReloadProjectsAsync();
        }

        private static List<ProjectChoice> LoadProjects(AuditScope scope)
        {
            return scope == AuditScope.Local
                ? LoadProjectsFromLocal()
                : LoadProjectsFromAzure();
        }

        private static List<ProjectChoice> LoadProjectsFromAzure()
        {
            var result = new List<ProjectChoice>();

            using var conn = AzureDbManager.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 60;
            cmd.CommandText = @"
                SELECT ProjectID, Description
                FROM VMS_Projects
                ORDER BY ProjectID DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ProjectChoice
                {
                    ProjectID = reader.GetString(0),
                    ProjectName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    IsSelected = true
                });
            }

            return result;
        }

        // Local mode lists only projects that have at least one Activity row in the
        // local cache — auditing a project with no synced rows would always return
        // zero issues, so it's noise.
        private static List<ProjectChoice> LoadProjectsFromLocal()
        {
            var result = new List<ProjectChoice>();

            using var conn = DatabaseSetup.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.ProjectID, COALESCE(p.Description, '')
                FROM Projects p
                WHERE EXISTS (SELECT 1 FROM Activities a WHERE a.ProjectID = p.ProjectID)
                ORDER BY p.ProjectID DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ProjectChoice
                {
                    ProjectID = reader.GetString(0),
                    ProjectName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    IsSelected = true
                });
            }

            return result;
        }

        private void Project_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ProjectChoice.IsSelected)) return;
            if (_suppressHeaderSync) return;
            UpdateHeaderState();
        }

        // Header checkbox is tri-state — null (indeterminate) when partial selection,
        // true when all selected, false when none selected.
        private void UpdateHeaderState()
        {
            int total = _projects.Count;
            int selected = _projects.Count(p => p.IsSelected);

            _suppressHeaderSync = true;
            try
            {
                if (selected == 0) chkSelectAll.IsChecked = false;
                else if (selected == total) chkSelectAll.IsChecked = true;
                else chkSelectAll.IsChecked = null;
            }
            finally
            {
                _suppressHeaderSync = false;
            }

            btnRun.IsEnabled = selected > 0;
        }

        // Click cycles Unchecked → Checked → Unchecked (no Indeterminate state from a
        // user click). The CheckBox is set to non-three-state in its IsChecked logic
        // here; IsThreeState was left default so the indeterminate visual still applies
        // when WE programmatically set it.
        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;

            // After a click, IsChecked is either true or false (clicks never produce
            // null on a non-three-state checkbox). Propagate to all items.
            bool target = cb.IsChecked == true;

            _suppressHeaderSync = true;
            try
            {
                foreach (var p in _projects)
                    p.IsSelected = target;
            }
            finally
            {
                _suppressHeaderSync = false;
            }

            btnRun.IsEnabled = _projects.Any(p => p.IsSelected);
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            SelectedProjectIds = _projects
                .Where(p => p.IsSelected)
                .Select(p => p.ProjectID)
                .ToList();

            if (SelectedProjectIds.Count == 0)
            {
                AppMessageBox.Show("Select at least one project to audit.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedScope = rbLocal.IsChecked == true ? AuditScope.Local : AuditScope.Azure;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ProjectChoice : INotifyPropertyChanged
    {
        public string ProjectID { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
