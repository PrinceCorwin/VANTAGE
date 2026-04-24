using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Grid.Helpers;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Modeless dialog for editing a historical snapshot week's rows. Loads the snapshot
    // from Azure VMS_ProgressSnapshots, lets the user edit any non-system field, then
    // writes changes back via ScheduleRepository.UpdateSnapshotFullAsync.
    // Critical invariant: never touches Activities table, LocalDirty, SyncVersion, or
    // AzureUploadUtcDate. Snapshot edits are sync-inert.
    public partial class ModifySnapshotDialog : Window
    {
        private readonly SnapshotWeekItem _week;
        private readonly string _username;

        // Rows currently shown in the grid (two-way bound via ObservableCollection).
        private ObservableCollection<SnapshotData> _rows = new();

        // Pre-edit row snapshots so a per-cell validation failure can revert the cell
        // to its prior committed state. Keyed by UniqueID.
        private readonly Dictionary<string, SnapshotData> _originals = new(StringComparer.OrdinalIgnoreCase);

        // UniqueIDs of rows the user has edited since load. Drives Save button enable
        // and scopes the save to dirty rows only.
        private readonly HashSet<string> _dirtyUniqueIds = new(StringComparer.OrdinalIgnoreCase);

        // Set to true when Save completes successfully so OnClosing knows not to prompt
        // about discarding unsaved edits.
        private bool _savedSuccessfully;

        // Suppress CurrentCellEndEdit side effects while we programmatically revert a cell.
        private bool _suppressCellEndEdit;

        public ModifySnapshotDialog(SnapshotWeekItem week, string username)
        {
            _week = week ?? throw new ArgumentNullException(nameof(week));
            _username = username ?? throw new ArgumentNullException(nameof(username));

            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            txtHeader.Text = $"Edit Snapshot: {_week.ProjectID} — Week Ending {_week.WeekEndDate:MM/dd/yyyy}";

            Loaded += ModifySnapshotDialog_Loaded;
        }

        private async void ModifySnapshotDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSnapshotRowsAsync();
        }

        private async Task LoadSnapshotRowsAsync()
        {
            ShowBusy("Loading snapshot...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                AppLogger.Info(
                    $"Loading snapshot rows for {_week.ProjectID} WE {_week.WeekEndDateStr} ProgDate {_week.ProgDate}",
                    "ModifySnapshotDialog.LoadSnapshotRowsAsync", _username);

                var loaded = await ManageSnapshotsDialog.LoadSnapshotsFromAzureAsync(_week, _username);
                long azureMs = stopwatch.ElapsedMilliseconds;

                _rows = new ObservableCollection<SnapshotData>(loaded);
                _originals.Clear();
                foreach (var row in loaded)
                {
                    _originals[row.UniqueID] = CloneSnapshot(row);
                }

                txtBusyMessage.Text = $"Rendering {_rows.Count} row(s)...";

                sfSnapshot.ItemsSource = _rows;

                stopwatch.Stop();
                AppLogger.Info(
                    $"Snapshot rows loaded: {_rows.Count} rows. Azure {azureMs}ms, total {stopwatch.ElapsedMilliseconds}ms",
                    "ModifySnapshotDialog.LoadSnapshotRowsAsync", _username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ModifySnapshotDialog.LoadSnapshotRowsAsync");
                MessageBox.Show($"Failed to load snapshot:\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                HideBusy();
            }
        }

        // Hide non-editable columns at generation time and lock editability per the rule.
        private void SfSnapshot_AutoGeneratingColumn(object? sender, AutoGeneratingColumnArgs e)
        {
            if (e.Column == null) return;

            string name = e.Column.MappingName;

            // Columns we never want to show in this dialog (identity + system-only fields).
            // Keeping ProgDate hidden here because it's the snapshot timestamp, not data
            // the user is meant to modify; the week-end date is already shown in the header.
            if (!SnapshotEditableColumns.IsEditable(name) || name == "ProgDate")
            {
                e.Cancel = true;
                return;
            }

            e.Column.AllowEditing = true;
        }

        // Per-cell validation for the date/% rules. Required-metadata non-empty check is
        // deferred to Save so the user can shuffle between cells without being blocked.
        private void SfSnapshot_CurrentCellEndEdit(object? sender, CurrentCellEndEditEventArgs e)
        {
            if (_suppressCellEndEdit) return;

            var grid = sender as SfDataGrid;
            if (grid == null) return;

            var record = grid.CurrentItem as SnapshotData;
            if (record == null) return;

            // Hard rule validation only for the three fields ActivityValidator covers.
            string? column = e.RowColumnIndex.ColumnIndex >= 0 && e.RowColumnIndex.ColumnIndex < grid.Columns.Count
                ? grid.Columns[e.RowColumnIndex.ColumnIndex].MappingName
                : null;

            if (column == nameof(SnapshotData.PercentEntry)
                || column == nameof(SnapshotData.ActStart)
                || column == nameof(SnapshotData.ActFin))
            {
                var (parseOk, actStart, actFin) = ParseDates(record.ActStart, record.ActFin);
                if (!parseOk)
                {
                    // Invalid date text — tell user and revert the cell to its last committed value.
                    MessageBox.Show("Dates must be in a recognized format (e.g. yyyy-MM-dd).",
                        "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RevertRow(record);
                    return;
                }

                string? violation = ActivityValidator.Validate(record.PercentEntry, actStart, actFin);
                if (violation != null)
                {
                    MessageBox.Show(violation, "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    RevertRow(record);
                    return;
                }
            }

            // Mark row as dirty (it may not actually have changed if user committed the same
            // value, but that's harmless — the save path re-reads the current values anyway).
            _dirtyUniqueIds.Add(record.UniqueID);
            btnSave.IsEnabled = true;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_dirtyUniqueIds.Count == 0)
            {
                Close();
                return;
            }

            // Snapshot the current dirty set so we can iterate safely.
            var dirtyRows = _rows.Where(r => _dirtyUniqueIds.Contains(r.UniqueID)).ToList();

            // Pre-validate every dirty row.
            var failures = new List<string>();
            foreach (var row in dirtyRows)
            {
                foreach (var fieldName in ActivityRequiredMetadata.Fields)
                {
                    var prop = typeof(SnapshotData).GetProperty(fieldName);
                    if (prop == null) continue;

                    object? value = prop.GetValue(row);
                    if (value is string s && string.IsNullOrWhiteSpace(s))
                    {
                        failures.Add($"{row.SchedActNO} ({row.UniqueID}): {fieldName} cannot be blank");
                    }
                }

                var (parseOk, actStart, actFin) = ParseDates(row.ActStart, row.ActFin);
                if (!parseOk)
                {
                    failures.Add($"{row.SchedActNO} ({row.UniqueID}): dates are not in a recognized format");
                    continue;
                }

                string? violation = ActivityValidator.Validate(row.PercentEntry, actStart, actFin);
                if (violation != null)
                {
                    failures.Add($"{row.SchedActNO} ({row.UniqueID}): {violation}");
                }
            }

            if (failures.Count > 0)
            {
                string preview = string.Join("\n", failures.Take(10));
                string footer = failures.Count > 10 ? $"\n…and {failures.Count - 10} more" : string.Empty;
                MessageBox.Show(
                    $"Cannot save. {failures.Count} validation error(s):\n\n{preview}{footer}\n\n" +
                    $"Required fields: {ActivityRequiredMetadata.FieldsDisplay}\n" +
                    "Conditional: ActStart (when % > 0), ActFin (when % = 100)",
                    "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable UI during save.
            btnSave.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ShowBusy($"Saving {dirtyRows.Count} edit(s)...");

            int saved = 0;
            var zeroAffected = new List<string>();
            Exception? firstEx = null;

            try
            {
                using var _opTracker = LongRunningOps.Begin();

                foreach (var row in dirtyRows)
                {
                    try
                    {
                        int affected = await ScheduleRepository.UpdateSnapshotFullAsync(
                            row, _week.WeekEndDateStr, _username);
                        if (affected == 0)
                        {
                            zeroAffected.Add(row.UniqueID);
                        }
                        else
                        {
                            saved += affected;
                        }
                    }
                    catch (Exception rowEx)
                    {
                        firstEx ??= rowEx;
                        AppLogger.Error(rowEx, "ModifySnapshotDialog.BtnSave_Click row");
                    }
                }
            }
            finally
            {
                HideBusy();
                btnCancel.IsEnabled = true;
            }

            if (firstEx != null)
            {
                MessageBox.Show(
                    $"Save failed: {firstEx.Message}\n\nPartial progress: {saved} row(s) saved before the error.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSave.IsEnabled = true;
                return;
            }

            if (zeroAffected.Count == dirtyRows.Count)
            {
                MessageBox.Show(
                    "None of your edits were applied. The snapshot appears to have been regenerated " +
                    "externally (most likely by a Submit Week for the same project and week).\n\n" +
                    "Close this dialog and reopen Modify to edit the current version.",
                    "Snapshot Regenerated", MessageBoxButton.OK, MessageBoxImage.Warning);
                btnSave.IsEnabled = true;
                return;
            }

            AppLogger.Info(
                $"Snapshot modify saved {saved} row(s) for {_week.ProjectID} WE {_week.WeekEndDateStr}" +
                (zeroAffected.Count > 0 ? $" ({zeroAffected.Count} row(s) not found — external change)" : ""),
                "ModifySnapshotDialog.BtnSave_Click", _username);

            if (zeroAffected.Count > 0)
            {
                string preview = string.Join("\n  ", zeroAffected.Take(10));
                string footer = zeroAffected.Count > 10 ? $"\n  …and {zeroAffected.Count - 10} more" : string.Empty;
                MessageBox.Show(
                    $"Saved {saved} row(s).\n\n" +
                    $"{zeroAffected.Count} row(s) were not found on Azure and were not updated " +
                    "(the snapshot may have been partially regenerated externally):\n  " +
                    preview + footer,
                    "Save Complete (partial)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Successfully saved {saved} row(s) to the snapshot.",
                    "Save Complete", MessageBoxButton.OK, MessageBoxImage.None);
            }

            // Flag parent for refresh so week-level counts stay current if we ever add any.
            if (Owner is ManageSnapshotsDialog parent)
            {
                parent.SetNeedsRefresh();
            }

            _savedSuccessfully = true;
            _dirtyUniqueIds.Clear();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_savedSuccessfully && _dirtyUniqueIds.Count > 0)
            {
                var result = MessageBox.Show(
                    $"You have {_dirtyUniqueIds.Count} unsaved edit(s). Discard and close?",
                    "Unsaved Edits", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        private void ShowBusy(string message)
        {
            txtBusyMessage.Text = message;
            busyOverlay.Visibility = Visibility.Visible;
        }

        private void HideBusy()
        {
            busyOverlay.Visibility = Visibility.Collapsed;
        }

        // Restore every editable property on a row from its pre-edit copy. Used after a
        // per-cell validation failure to back out the bad value.
        private void RevertRow(SnapshotData row)
        {
            if (!_originals.TryGetValue(row.UniqueID, out var original))
                return;

            _suppressCellEndEdit = true;
            try
            {
                foreach (var prop in typeof(SnapshotData).GetProperties())
                {
                    if (!prop.CanWrite) continue;
                    prop.SetValue(row, prop.GetValue(original));
                }
                sfSnapshot.View?.Refresh();
            }
            finally
            {
                _suppressCellEndEdit = false;
            }
        }

        private static SnapshotData CloneSnapshot(SnapshotData source)
        {
            var clone = new SnapshotData();
            foreach (var prop in typeof(SnapshotData).GetProperties())
            {
                if (!prop.CanWrite) continue;
                prop.SetValue(clone, prop.GetValue(source));
            }
            return clone;
        }

        // Parses snapshot date strings (stored as TEXT per the app's date convention) into
        // nullable DateTime. Empty/null → null, valid → parsed, unparseable → signals error.
        private static (bool ok, DateTime? start, DateTime? finish) ParseDates(string? actStart, string? actFin)
        {
            DateTime? start = null, finish = null;

            if (!string.IsNullOrWhiteSpace(actStart))
            {
                if (!DateTime.TryParse(actStart, out var s)) return (false, null, null);
                start = s;
            }
            if (!string.IsNullOrWhiteSpace(actFin))
            {
                if (!DateTime.TryParse(actFin, out var f)) return (false, null, null);
                finish = f;
            }

            return (true, start, finish);
        }
    }
}
