using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class ScheduleView : UserControl
    {
        private readonly ScheduleViewModel _viewModel;
        private const string GridPrefsKey = "ScheduleGrid.PreferencesJson";
        private const string MasterHeightKey = "ScheduleView_MasterGridHeight";
        private const string DetailHeightKey = "ScheduleView_DetailGridHeight";
        private DispatcherTimer _resizeSaveTimer = null!;

        public ScheduleView()
        {
            InitializeComponent();

            _viewModel = new ScheduleViewModel();
            DataContext = _viewModel;

            Loaded += ScheduleView_Loaded;

            // Load column state and splitter position after view is loaded
            Loaded += (_, __) =>
            {
                sfScheduleMaster.Opacity = 0;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadColumnState();
                    LoadSplitterState();
                    sfScheduleMaster.Opacity = 1;
                }), DispatcherPriority.ContextIdle);
            };

            // Save column/splitter state and check for unsaved changes when view closes
            Unloaded += ScheduleView_Unloaded;

            // Save when columns are dragged
            sfScheduleMaster.QueryColumnDragging += (s, e) =>
            {
                if (e.Reason == Syncfusion.UI.Xaml.Grid.QueryColumnDraggingReason.Dropped)
                {
                    Dispatcher.BeginInvoke(new Action(() => SaveColumnState()), DispatcherPriority.Background);
                }
            };

            // Setup resize save timer
            _resizeSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _resizeSaveTimer.Tick += (s, e) =>
            {
                _resizeSaveTimer.Stop();
                SaveColumnState();
            };
            SetupColumnResizeSave();

            // Wire up splitter drag completed
            gridSplitter.DragCompleted += GridSplitter_DragCompleted;

            // Wire up master grid selection changed
            sfScheduleMaster.SelectionChanged += SfScheduleMaster_SelectionChanged;
            // Wire up detail grid edit handler
            sfScheduleDetail.CurrentCellEndEdit += SfScheduleDetail_CurrentCellEndEdit;
            // Wire up master grid edit handler for tracking unsaved changes
            sfScheduleMaster.CurrentCellEndEdit += SfScheduleMaster_CurrentCellEndEdit;
        }

        // ========================================
        // SPLITTER STATE PERSISTENCE
        // ========================================

        private void LoadSplitterState()
        {
            try
            {
                if (App.CurrentUserID <= 0)
                    return;

                var masterHeightStr = SettingsManager.GetUserSetting(App.CurrentUserID, MasterHeightKey);
                var detailHeightStr = SettingsManager.GetUserSetting(App.CurrentUserID, DetailHeightKey);

                if (string.IsNullOrWhiteSpace(masterHeightStr) || string.IsNullOrWhiteSpace(detailHeightStr))
                    return;

                if (double.TryParse(masterHeightStr, out double masterHeight) &&
                    double.TryParse(detailHeightStr, out double detailHeight))
                {
                    // Ensure minimum heights
                    masterHeight = Math.Max(100, masterHeight);
                    detailHeight = Math.Max(80, detailHeight);

                    MasterGridRow.Height = new GridLength(masterHeight);
                    DetailGridRow.Height = new GridLength(detailHeight);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.LoadSplitterState");
            }
        }

        private void SaveSplitterState()
        {
            try
            {
                if (App.CurrentUserID <= 0)
                    return;

                // Get actual rendered heights
                double masterHeight = MasterGridRow.ActualHeight;
                double detailHeight = DetailGridRow.ActualHeight;

                // Only save if we have valid heights
                if (masterHeight > 0 && detailHeight > 0)
                {
                    SettingsManager.SetUserSetting(App.CurrentUserID, MasterHeightKey, masterHeight.ToString(), "double");
                    SettingsManager.SetUserSetting(App.CurrentUserID, DetailHeightKey, detailHeight.ToString(), "double");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SaveSplitterState");
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SaveSplitterState();
        }

        // ========================================
        // MASTER GRID EDIT - TRACK UNSAVED CHANGES
        // ========================================

        private void SfScheduleMaster_CurrentCellEndEdit(object? sender, Syncfusion.UI.Xaml.Grid.CurrentCellEndEditEventArgs e)
        {
            try
            {
                if (sfScheduleMaster.CurrentColumn == null)
                    return;

                string columnName = sfScheduleMaster.CurrentColumn.MappingName;

                // Only track editable columns
                if (columnName == "MissedStartReason" || columnName == "MissedFinishReason" ||
                    columnName == "ThreeWeekStart" || columnName == "ThreeWeekFinish")
                {
                    _viewModel.HasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SfScheduleMaster_CurrentCellEndEdit");
            }
        }

        // ========================================
        // EXIT HANDLING - PROMPT FOR UNSAVED CHANGES
        // ========================================

        private void ScheduleView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Save UI state regardless
            SaveColumnState();
            SaveSplitterState();

            // Note: Prompting in Unloaded is too late - the view is already closing.
            // For proper exit prompting, we expose a public method for MainWindow to call.
        }

        // Call this from MainWindow before switching away from Schedule view
        public bool TryClose()
        {
            if (!_viewModel.HasUnsavedChanges)
                return true;

            var result = MessageBox.Show(
                "You have unsaved changes to the schedule.\n\nDo you want to save before leaving?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Save synchronously
                btnSave_Click(this, new RoutedEventArgs());
                return true;
            }
            else if (result == MessageBoxResult.No)
            {
                // Discard changes
                _viewModel.HasUnsavedChanges = false;
                return true;
            }
            else
            {
                // Cancel - stay on this view
                return false;
            }
        }
        // ========================================
        // DETAIL GRID EDIT HANDLER
        // ========================================

        private async void SfScheduleDetail_CurrentCellEndEdit(object? sender, Syncfusion.UI.Xaml.Grid.CurrentCellEndEditEventArgs e)
        {
            try
            {
                var editedSnapshot = sfScheduleDetail.SelectedItem as ProgressSnapshot;
                if (editedSnapshot == null)
                    return;

                // Get the column that was edited
                if (sfScheduleDetail.CurrentColumn == null)
                    return;

                string columnName = sfScheduleDetail.CurrentColumn.MappingName;

                // Only process editable columns
                if (columnName != "PercentEntry" && columnName != "BudgetMHs" &&
                    columnName != "SchStart" && columnName != "SchFinish")
                    return;

                string username = App.CurrentUser?.Username ?? "Unknown";

                // Update timestamp
                editedSnapshot.UpdatedBy = username;
                editedSnapshot.UpdatedUtcDate = DateTime.UtcNow;

                txtStatus.Text = "Saving...";

                // Update both Azure snapshot and local Activity
                bool success = await ScheduleRepository.UpdateSnapshotAndActivityAsync(editedSnapshot, username);

                if (success)
                {
                    // Recalculate MS rollups for this SchedActNO
                    if (!string.IsNullOrEmpty(_viewModel.SelectedSchedActNO))
                    {
                        await _viewModel.RecalculateMSRollupsAsync(_viewModel.SelectedSchedActNO);
                    }

                    // Force master grid to refresh and show updated values
                    sfScheduleMaster.View?.Refresh();

                    txtStatus.Text = "Saved";
                    AppLogger.Info($"Detail edit saved: {editedSnapshot.UniqueID} - {columnName}",
                        "ScheduleView.SfScheduleDetail_CurrentCellEndEdit", username);
                }
                else
                {
                    txtStatus.Text = "Save failed";
                    MessageBox.Show("Failed to save changes. Please try again.",
                        "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SfScheduleDetail_CurrentCellEndEdit");
                txtStatus.Text = "Save failed";
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // ========================================
        // MASTER GRID SELECTION - LOAD DETAIL
        // ========================================

        private async void SfScheduleMaster_SelectionChanged(object? sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
        {
            try
            {
                // Get selected row data
                var selectedRow = sfScheduleMaster.SelectedItem as ScheduleMasterRow;

                if (selectedRow == null)
                {
                    txtDetailHeader.Text = "Select a row above to view detail activities";
                    _viewModel.ClearDetailActivities();
                    return;
                }

                // Update header
                txtDetailHeader.Text = $"Activities for: {selectedRow.SchedActNO} - {selectedRow.Description}";

                // Load detail activities from ViewModel
                await _viewModel.LoadDetailActivitiesAsync(selectedRow.SchedActNO);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SfScheduleMaster_SelectionChanged");
                txtDetailHeader.Text = "Error loading detail activities";
            }
        }

        // ========================================
        // EXISTING METHODS
        // ========================================

        private void btnRequiredFields_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the filter - clicking again clears it
            _viewModel.FilterRequiredFields = !_viewModel.FilterRequiredFields;

            // Clear other filters when this one is activated
            if (_viewModel.FilterRequiredFields)
            {
                _viewModel.FilterActualStart = false;
                _viewModel.FilterActualFinish = false;
                _viewModel.Filter3WLA = false;
            }

            // Update status bar
            if (_viewModel.FilterRequiredFields)
            {
                txtStatus.Text = "Filtered: Required Fields";
            }
            else
            {
                txtStatus.Text = "Ready";
            }
        }

        private async void ScheduleView_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel.MasterRows == null || _viewModel.MasterRows.Count == 0)
                {
                    MessageBox.Show("No data to save.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                btnSave.IsEnabled = false;
                txtStatus.Text = "Saving...";

                string username = App.CurrentUser?.Username ?? "Unknown";
                int savedCount = await ScheduleRepository.SaveAllScheduleRowsAsync(_viewModel.MasterRows, username);

                txtStatus.Text = $"Saved {savedCount} rows";
                AppLogger.Info($"Saved {savedCount} schedule rows", "ScheduleView.btnSave_Click", username);
                _viewModel.HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.btnSave_Click");
                MessageBox.Show($"Error saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Save failed";
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        // ========================================
        // COLUMN STATE PERSISTENCE
        // ========================================

        private void SetupColumnResizeSave()
        {
            sfScheduleMaster.ResizingColumns += (s, e) =>
            {
                if (e.Reason == Syncfusion.UI.Xaml.Grid.ColumnResizingReason.Resized)
                {
                    _resizeSaveTimer.Stop();
                    _resizeSaveTimer.Start();
                }
            };
        }

        private void SaveColumnState()
        {
            try
            {
                if (sfScheduleMaster?.Columns == null || sfScheduleMaster.Columns.Count == 0)
                    return;

                var prefs = new GridPreferences
                {
                    Version = 1,
                    SchemaHash = ComputeSchemaHash(sfScheduleMaster),
                    Columns = sfScheduleMaster.Columns
                        .Select(c => new GridColumnPref
                        {
                            Name = c.MappingName,
                            OrderIndex = sfScheduleMaster.Columns.IndexOf(c),
                            Width = c.Width,
                            IsHidden = c.IsHidden
                        })
                        .ToList()
                };

                var json = JsonSerializer.Serialize(prefs);
                SettingsManager.SetUserSetting(App.CurrentUserID, GridPrefsKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SaveColumnState");
            }
        }

        private void LoadColumnState()
        {
            try
            {
                if (sfScheduleMaster?.Columns == null || App.CurrentUserID <= 0)
                    return;

                var raw = SettingsManager.GetUserSetting(App.CurrentUserID, GridPrefsKey);

                if (string.IsNullOrWhiteSpace(raw))
                    return;

                GridPreferences? prefs = null;
                try { prefs = JsonSerializer.Deserialize<GridPreferences>(raw); }
                catch { return; }

                if (prefs == null)
                    return;

                var currentHash = ComputeSchemaHash(sfScheduleMaster);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                    return;

                var byName = sfScheduleMaster.Columns.ToDictionary(c => c.MappingName, c => c);

                // 1) Visibility first
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.IsHidden = p.IsHidden;

                // 2) Order (move columns to target positions)
                var orderedPrefs = prefs.Columns.OrderBy(x => x.OrderIndex).ToList();
                for (int target = 0; target < orderedPrefs.Count; target++)
                {
                    var p = orderedPrefs[target];
                    if (!byName.TryGetValue(p.Name, out var col)) continue;
                    int cur = sfScheduleMaster.Columns.IndexOf(col);
                    if (cur != target && cur >= 0)
                    {
                        sfScheduleMaster.Columns.RemoveAt(cur);
                        sfScheduleMaster.Columns.Insert(target, col);
                    }
                }

                // 3) Width last (guard against tiny widths)
                const double MinWidth = 40.0;
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.Width = Math.Max(MinWidth, p.Width);

                sfScheduleMaster.UpdateLayout();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.LoadColumnState");
            }
        }

        private static string ComputeSchemaHash(Syncfusion.UI.Xaml.Grid.SfDataGrid grid)
        {
            using var sha = SHA256.Create();
            var names = string.Join("|", grid.Columns.Select(c => c.MappingName).OrderBy(n => n));
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(names)));
        }

        // ========================================
        // HELPER CLASSES
        // ========================================

        public class GridPreferences
        {
            public int Version { get; set; } = 1;
            public string SchemaHash { get; set; } = "";
            public List<GridColumnPref> Columns { get; set; } = new();
        }

        public class GridColumnPref
        {
            public string Name { get; set; } = "";
            public int OrderIndex { get; set; }
            public double Width { get; set; }
            public bool IsHidden { get; set; }
        }
    }
}