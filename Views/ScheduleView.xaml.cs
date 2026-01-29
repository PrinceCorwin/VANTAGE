using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Grid;
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
        private const string DetailGridPrefsKey = "ScheduleDetailGrid.PreferencesJson";
        private DispatcherTimer _resizeSaveTimer = null!;
        private bool _skipSaveColumnState = false;

        // Stores old cell values before editing for change logging
        private string? _detailEditOldValue;
        private string? _detailEditColumnName;

        public ScheduleView()
        {
            InitializeComponent();

            _viewModel = new ScheduleViewModel();
            DataContext = _viewModel;

            Loaded += ScheduleView_Loaded;

            // Hook native horizontal scroll wheel for this view
            Loaded += (_, __) => AttachHorizontalScrollHook();

            // Load column state and splitter position after view is loaded
            Loaded += (_, __) =>
            {
                sfScheduleMaster.Opacity = 0;
                sfScheduleDetail.Opacity = 0;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadColumnState();
                    LoadDetailColumnState();
                    LoadSplitterState();
                    sfScheduleMaster.Opacity = 1;
                    sfScheduleDetail.Opacity = 1;
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
                if (_resizeSaveTimer.Tag as string == "Detail")
                    SaveDetailColumnState();
                else
                    SaveColumnState();
                _resizeSaveTimer.Tag = null;
            };
            SetupColumnResizeSave();

            // Wire up splitter drag completed
            gridSplitter.DragCompleted += GridSplitter_DragCompleted;

            // Wire up master grid selection changed
            sfScheduleMaster.SelectionChanged += SfScheduleMaster_SelectionChanged;
            // Wire up detail grid edit handlers for change logging
            sfScheduleDetail.CurrentCellBeginEdit += SfScheduleDetail_CurrentCellBeginEdit;
            sfScheduleDetail.CurrentCellEndEdit += SfScheduleDetail_CurrentCellEndEdit;
            // Detail grid column persistence
            sfScheduleDetail.QueryColumnDragging += (s, e) =>
            {
                if (e.Reason == Syncfusion.UI.Xaml.Grid.QueryColumnDraggingReason.Dropped)
                {
                    Dispatcher.BeginInvoke(new Action(() => SaveDetailColumnState()), DispatcherPriority.Background);
                }
            };
            sfScheduleDetail.ResizingColumns += (s, e) =>
            {
                if (e.Reason == Syncfusion.UI.Xaml.Grid.ColumnResizingReason.Resized)
                {
                    _resizeSaveTimer.Stop();
                    _resizeSaveTimer.Tag = "Detail"; // Mark which grid triggered the save
                    _resizeSaveTimer.Start();
                }
            };
            // Wire up master grid edit handler for tracking unsaved changes
            sfScheduleMaster.CurrentCellEndEdit += SfScheduleMaster_CurrentCellEndEdit;

            // Wire up copy/paste handlers for detail grid
            sfScheduleDetail.GridCopyContent += SfScheduleDetail_GridCopyContent;
            sfScheduleDetail.GridPasteContent += SfScheduleDetail_GridPasteContent;
        }

        // ========================================
        // SPLITTER STATE PERSISTENCE
        // ========================================

        private void LoadSplitterState()
        {
            try
            {
                var masterHeightStr = SettingsManager.GetUserSetting(MasterHeightKey);
                var detailHeightStr = SettingsManager.GetUserSetting(DetailHeightKey);

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
                if (_skipSaveColumnState)
                    return;

                // Get actual rendered heights
                double masterHeight = MasterGridRow.ActualHeight;
                double detailHeight = DetailGridRow.ActualHeight;

                // Only save if we have valid heights
                if (masterHeight > 0 && detailHeight > 0)
                {
                    SettingsManager.SetUserSetting(MasterHeightKey, masterHeight.ToString(), "double");
                    SettingsManager.SetUserSetting(DetailHeightKey, detailHeight.ToString(), "double");
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
                    _viewModel.UpdateRequiredFieldsCount();
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
        private void SfScheduleMaster_CurrentCellBeginEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellBeginEditEventArgs e)
        {
            // Block editing of 3WLA date columns when actuals exist
            if (e.Column == null)
                return;

            var row = sfScheduleMaster.CurrentItem as ScheduleMasterRow;
            if (row == null)
                return;

            string columnName = e.Column.MappingName;

            if (columnName == "ThreeWeekStart" && !row.IsThreeWeekStartEditable)
            {
                e.Cancel = true;
                txtStatus.Text = "3WLA Start is locked (actual start exists)";
            }
            else if (columnName == "ThreeWeekFinish" && !row.IsThreeWeekFinishEditable)
            {
                e.Cancel = true;
                txtStatus.Text = "3WLA Finish is locked (actual finish exists)";
            }
        }
        private void ScheduleView_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachHorizontalScrollHook();
            SaveColumnState();
            SaveDetailColumnState();
            SaveSplitterState();
        }

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

        // Captures old value before editing for change logging
        private void SfScheduleDetail_CurrentCellBeginEdit(object? sender, Syncfusion.UI.Xaml.Grid.CurrentCellBeginEditEventArgs e)
        {
            try
            {
                var snapshot = sfScheduleDetail.CurrentItem as ProgressSnapshot;
                if (snapshot == null || e.Column == null)
                    return;

                _detailEditColumnName = e.Column.MappingName;

                // Capture the current value before editing
                switch (_detailEditColumnName)
                {
                    case "PercentEntry":
                        _detailEditOldValue = snapshot.PercentEntry.ToString();
                        break;
                    case "BudgetMHs":
                        _detailEditOldValue = snapshot.BudgetMHs.ToString();
                        break;
                    case "SchStart":
                        _detailEditOldValue = snapshot.SchStart?.ToString("M/d/yyyy") ?? string.Empty;
                        break;
                    case "SchFinish":
                        _detailEditOldValue = snapshot.SchFinish?.ToString("M/d/yyyy") ?? string.Empty;
                        break;
                    default:
                        _detailEditOldValue = null;
                        _detailEditColumnName = null;
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SfScheduleDetail_CurrentCellBeginEdit");
            }
        }

        // Public method for MainWindow to call after P6 import
        public async Task RefreshDataAsync(DateTime weekEndDate)
        {
            try
            {
                // Reload available week end dates (new import may have added one)
                await _viewModel.InitializeAsync();

                // Select the imported week end date
                _viewModel.SelectedWeekEndDate = weekEndDate;

                // Load data for that date
                await _viewModel.LoadScheduleDataAsync(weekEndDate);

                txtStatus.Text = "Data refreshed";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.RefreshDataAsync");
                txtStatus.Text = "Refresh failed";
            }
        }
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
                DateTime weekEndDate = _viewModel.SelectedWeekEndDate ?? DateTime.Today;

                // Handle PercentEntry changes - auto-adjust dates
                if (columnName == "PercentEntry")
                {
                    if (editedSnapshot.PercentEntry == 0)
                    {
                        // 0% = not started - clear both dates
                        editedSnapshot.SchStart = null;
                        editedSnapshot.SchFinish = null;
                    }
                    else if (editedSnapshot.PercentEntry > 0 && editedSnapshot.PercentEntry < 100)
                    {
                        // In progress - needs start, no finish
                        if (editedSnapshot.SchStart == null)
                        {
                            editedSnapshot.SchStart = weekEndDate;
                        }
                        editedSnapshot.SchFinish = null;
                    }
                    else if (editedSnapshot.PercentEntry >= 100)
                    {
                        // Complete - needs both start and finish
                        if (editedSnapshot.SchStart == null)
                        {
                            editedSnapshot.SchStart = weekEndDate;
                        }
                        if (editedSnapshot.SchFinish == null)
                        {
                            editedSnapshot.SchFinish = weekEndDate;
                        }
                    }
                }

                // Handle SchStart changes - validate
                if (columnName == "SchStart")
                {
                    // Can't set start if percent is 0
                    if (editedSnapshot.SchStart != null && editedSnapshot.PercentEntry == 0)
                    {
                        MessageBox.Show("Cannot set Start date when % Complete is 0.\n\nSet % Complete first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchStart = null;
                        sfScheduleDetail.View?.Refresh();
                        return;
                    }

                    // Can't set start in the future
                    if (editedSnapshot.SchStart != null && editedSnapshot.SchStart.Value.Date > weekEndDate.Date)
                    {
                        MessageBox.Show($"Start date cannot be after Week End Date ({weekEndDate:M/d/yyyy}).\n\nActual dates must be in the past.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchStart = weekEndDate;
                        sfScheduleDetail.View?.Refresh();
                    }

                    // Can't clear start if percent > 0
                    if (editedSnapshot.SchStart == null && editedSnapshot.PercentEntry > 0)
                    {
                        MessageBox.Show("Cannot clear Start date when % Complete is greater than 0.\n\nSet % Complete to 0 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchStart = weekEndDate;
                        sfScheduleDetail.View?.Refresh();
                        return;
                    }
                }

                // Handle SchFinish changes - validate
                if (columnName == "SchFinish")
                {
                    // Can't set finish if percent is not 100
                    if (editedSnapshot.SchFinish != null && editedSnapshot.PercentEntry < 100)
                    {
                        MessageBox.Show("Cannot set Finish date when % Complete is less than 100.\n\nSet % Complete to 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchFinish = null;
                        sfScheduleDetail.View?.Refresh();
                        return;
                    }

                    // Can't set finish in the future
                    if (editedSnapshot.SchFinish != null && editedSnapshot.SchFinish.Value.Date > weekEndDate.Date)
                    {
                        MessageBox.Show($"Finish date cannot be after Week End Date ({weekEndDate:M/d/yyyy}).\n\nActual dates must be in the past.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchFinish = weekEndDate;
                        sfScheduleDetail.View?.Refresh();
                    }

                    // Can't set finish before start
                    if (editedSnapshot.SchFinish != null && editedSnapshot.SchStart != null &&
                        editedSnapshot.SchFinish.Value.Date < editedSnapshot.SchStart.Value.Date)
                    {
                        MessageBox.Show("Finish date cannot be before Start date.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchFinish = editedSnapshot.SchStart;
                        sfScheduleDetail.View?.Refresh();
                    }

                    // Can't clear finish if percent is 100
                    if (editedSnapshot.SchFinish == null && editedSnapshot.PercentEntry >= 100)
                    {
                        MessageBox.Show("Cannot clear Finish date when % Complete is 100.\n\nSet % Complete to less than 100 first.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        editedSnapshot.SchFinish = weekEndDate;
                        sfScheduleDetail.View?.Refresh();
                        return;
                    }
                }

                // Update timestamp
                editedSnapshot.UpdatedBy = username;
                editedSnapshot.UpdatedUtcDate = DateTime.UtcNow;

                txtStatus.Text = "Saving...";

                // Update Azure snapshot
                bool success = await ScheduleRepository.UpdateSnapshotAsync(editedSnapshot, username);

                if (success)
                {
                    // Recalculate MS rollups for this SchedActNO
                    if (!string.IsNullOrEmpty(editedSnapshot.SchedActNO))
                    {
                        // Capture current state before recalculation
                        var masterRow = _viewModel.MasterRows?.FirstOrDefault(r => r.SchedActNO == editedSnapshot.SchedActNO);
                        DateTime? previousMSStart = masterRow?.MS_ActualStart;
                        DateTime? previousMSFinish = masterRow?.MS_ActualFinish;

                        await _viewModel.RecalculateMSRollupsAsync(editedSnapshot.SchedActNO);

                        // Check if actuals were created (changed from null to non-null)
                        if (masterRow != null)
                        {
                            bool startCreated = previousMSStart == null && masterRow.MS_ActualStart != null;
                            bool finishCreated = previousMSFinish == null && masterRow.MS_ActualFinish != null;

                            if (startCreated || finishCreated)
                            {
                                // Get ProjectID for the 3WLA table
                                string? projectId = ScheduleRepository.GetFirstProjectIDForWeek(weekEndDate);
                                if (!string.IsNullOrEmpty(projectId))
                                {
                                    // Clear the 3WLA dates that are now obsolete
                                    await ScheduleRepository.ClearThreeWeekDatesAsync(
                                        editedSnapshot.SchedActNO,
                                        projectId,
                                        startCreated,
                                        finishCreated,
                                        username);

                                    // Update the in-memory master row
                                    if (startCreated)
                                        masterRow.ThreeWeekStart = null;
                                    if (finishCreated)
                                        masterRow.ThreeWeekFinish = null;

                                    string cleared = (startCreated && finishCreated) ? "3WLA Start and Finish" :
                                                     startCreated ? "3WLA Start" : "3WLA Finish";
                                    txtStatus.Text = $"Saved - {cleared} cleared";
                                }
                                else
                                {
                                    txtStatus.Text = "Saved";
                                }
                            }
                            else
                            {
                                txtStatus.Text = "Saved";
                            }
                        }
                        else
                        {
                            txtStatus.Text = "Saved";
                        }
                    }

                    // Force master grid to refresh and show updated values
                    sfScheduleMaster.View?.Refresh();
                    sfScheduleDetail.View?.Refresh();

                    // Log the change for optional application to Activities
                    if (_detailEditColumnName == columnName && _detailEditOldValue != null)
                    {
                        string newValue = columnName switch
                        {
                            "PercentEntry" => editedSnapshot.PercentEntry.ToString(),
                            "BudgetMHs" => editedSnapshot.BudgetMHs.ToString(),
                            "SchStart" => editedSnapshot.SchStart?.ToString("M/d/yyyy") ?? string.Empty,
                            "SchFinish" => editedSnapshot.SchFinish?.ToString("M/d/yyyy") ?? string.Empty,
                            _ => string.Empty
                        };

                        // Only log if value actually changed
                        if (_detailEditOldValue != newValue)
                        {
                            ScheduleChangeLogger.LogChange(
                                weekEndDate.ToString("M/d/yyyy"),
                                editedSnapshot.UniqueID,
                                editedSnapshot.SchedActNO,
                                editedSnapshot.Description ?? string.Empty,
                                columnName,
                                _detailEditOldValue,
                                newValue,
                                username);
                        }
                    }

                    // Clear tracked values
                    _detailEditOldValue = null;
                    _detailEditColumnName = null;

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

        // Public method for MainWindow to call when clearing local schedule data
        public void ClearScheduleDisplay()
        {
            _viewModel.MasterRows.Clear();
            _viewModel.DetailActivities.Clear();
            _viewModel.SelectedWeekEndDate = null;
            _viewModel.HasUnsavedChanges = false;
            txtDetailHeader.Text = "Select a row above to view detail activities";
            txtStatus.Text = "Schedule cleared";
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
        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_viewModel.SelectedWeekEndDate.HasValue)
                {
                    txtStatus.Text = "No week selected";
                    return;
                }

                txtStatus.Text = "Refreshing...";

                // Clear all filters before reloading
                _viewModel.ClearAllFilters();
                if (sfScheduleMaster?.View != null)
                {
                    sfScheduleMaster.View.Filter = null;
                    sfScheduleMaster.View.RefreshFilter();
                }

                await _viewModel.LoadScheduleDataAsync(_viewModel.SelectedWeekEndDate.Value);
                txtStatus.Text = "Refreshed";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.btnRefresh_Click");
                txtStatus.Text = "Refresh failed";
            }
        }
        // Public method for MainWindow to call when exporting
        public void SaveChanges()
        {
            btnSave_Click(this, new RoutedEventArgs());
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                if (_viewModel.MasterRows == null || _viewModel.MasterRows.Count == 0)
                    return;

                btnSave.IsEnabled = false;
                txtStatus.Text = "Saving...";

                string username = App.CurrentUser?.Username ?? "Unknown";
                int savedCount = await ScheduleRepository.SaveAllScheduleRowsAsync(_viewModel.MasterRows, username);

                txtStatus.Text = $"Saved {savedCount} rows";
                AppLogger.Info($"Saved {savedCount} schedule rows", "ScheduleView.SaveChanges", username);
                _viewModel.HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SaveChanges");
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
                    _resizeSaveTimer.Tag = "Master";
                    _resizeSaveTimer.Start();
                }
            };
        }

        // Skip saving column state on next unload (used by reset)
        public void SkipSaveOnClose()
        {
            _skipSaveColumnState = true;
        }

        private void SaveColumnState()
        {
            try
            {
                if (_skipSaveColumnState)
                    return;

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
                SettingsManager.SetUserSetting(GridPrefsKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SaveColumnState");
            }
        }

        // Public method for MainWindow to call after settings import
        public void ReloadColumnSettings()
        {
            LoadColumnState();
            LoadDetailColumnState();
        }

        // Get current master grid preferences for layout save
        public GridPreferencesData GetMasterGridPreferences()
        {
            if (sfScheduleMaster?.Columns == null || sfScheduleMaster.Columns.Count == 0)
                return new GridPreferencesData();

            return new GridPreferencesData
            {
                Version = 1,
                SchemaHash = ComputeSchemaHash(sfScheduleMaster),
                Columns = sfScheduleMaster.Columns
                    .Select(c => new GridColumnPrefData
                    {
                        Name = c.MappingName,
                        OrderIndex = sfScheduleMaster.Columns.IndexOf(c),
                        Width = c.Width,
                        IsHidden = c.IsHidden
                    })
                    .ToList()
            };
        }

        // Get current detail grid preferences for layout save
        public GridPreferencesData GetDetailGridPreferences()
        {
            if (sfScheduleDetail?.Columns == null || sfScheduleDetail.Columns.Count == 0)
                return new GridPreferencesData();

            return new GridPreferencesData
            {
                Version = 1,
                SchemaHash = ComputeSchemaHash(sfScheduleDetail),
                Columns = sfScheduleDetail.Columns
                    .Select(c => new GridColumnPrefData
                    {
                        Name = c.MappingName,
                        OrderIndex = sfScheduleDetail.Columns.IndexOf(c),
                        Width = c.Width,
                        IsHidden = c.IsHidden
                    })
                    .ToList()
            };
        }

        // Get current splitter heights for layout save
        public (double Master, double Detail) GetSplitterHeights()
        {
            return (MasterGridRow.ActualHeight, DetailGridRow.ActualHeight);
        }

        // Apply external master grid layout preferences
        public void ApplyMasterGridPreferences(GridPreferencesData prefs)
        {
            try
            {
                if (sfScheduleMaster?.Columns == null || prefs?.Columns == null || prefs.Columns.Count == 0)
                    return;

                var currentHash = ComputeSchemaHash(sfScheduleMaster);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                    return;

                var byName = sfScheduleMaster.Columns.ToDictionary(c => c.MappingName, c => c);

                // 1) Visibility
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.IsHidden = p.IsHidden;

                // 2) Order
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

                // 3) Width
                const double MinWidth = 40.0;
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.Width = Math.Max(MinWidth, p.Width);

                sfScheduleMaster.UpdateLayout();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.ApplyMasterGridPreferences");
            }
        }

        // Apply external detail grid layout preferences
        public void ApplyDetailGridPreferences(GridPreferencesData prefs)
        {
            try
            {
                if (sfScheduleDetail?.Columns == null || prefs?.Columns == null || prefs.Columns.Count == 0)
                    return;

                var currentHash = ComputeSchemaHash(sfScheduleDetail);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                    return;

                var byName = sfScheduleDetail.Columns.ToDictionary(c => c.MappingName, c => c);

                // 1) Visibility
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.IsHidden = p.IsHidden;

                // 2) Order
                var orderedPrefs = prefs.Columns.OrderBy(x => x.OrderIndex).ToList();
                for (int target = 0; target < orderedPrefs.Count; target++)
                {
                    var p = orderedPrefs[target];
                    if (!byName.TryGetValue(p.Name, out var col)) continue;
                    int cur = sfScheduleDetail.Columns.IndexOf(col);
                    if (cur != target && cur >= 0)
                    {
                        sfScheduleDetail.Columns.RemoveAt(cur);
                        sfScheduleDetail.Columns.Insert(target, col);
                    }
                }

                // 3) Width
                const double MinWidth = 40.0;
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.Width = Math.Max(MinWidth, p.Width);

                sfScheduleDetail.UpdateLayout();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.ApplyDetailGridPreferences");
            }
        }

        // Apply external splitter heights
        public void ApplySplitterHeights(double masterHeight, double detailHeight)
        {
            try
            {
                masterHeight = Math.Max(100, masterHeight);
                detailHeight = Math.Max(80, detailHeight);

                MasterGridRow.Height = new GridLength(masterHeight);
                DetailGridRow.Height = new GridLength(detailHeight);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.ApplySplitterHeights");
            }
        }

        private void LoadColumnState()
        {
            try
            {
                if (sfScheduleMaster?.Columns == null)
                    return;

                var raw = SettingsManager.GetUserSetting(GridPrefsKey);

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
        private void SaveDetailColumnState()
        {
            try
            {
                if (_skipSaveColumnState)
                    return;

                if (sfScheduleDetail?.Columns == null || sfScheduleDetail.Columns.Count == 0)
                    return;

                var prefs = new GridPreferences
                {
                    Version = 1,
                    SchemaHash = ComputeSchemaHash(sfScheduleDetail),
                    Columns = sfScheduleDetail.Columns
                        .Select(c => new GridColumnPref
                        {
                            Name = c.MappingName,
                            OrderIndex = sfScheduleDetail.Columns.IndexOf(c),
                            Width = c.Width,
                            IsHidden = c.IsHidden
                        })
                        .ToList()
                };

                var json = JsonSerializer.Serialize(prefs);
                SettingsManager.SetUserSetting(DetailGridPrefsKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.SaveDetailColumnState");
            }
        }

        private void LoadDetailColumnState()
        {
            try
            {
                if (sfScheduleDetail?.Columns == null)
                    return;

                var raw = SettingsManager.GetUserSetting(DetailGridPrefsKey);

                if (string.IsNullOrWhiteSpace(raw))
                    return;

                GridPreferences? prefs = null;
                try { prefs = JsonSerializer.Deserialize<GridPreferences>(raw); }
                catch { return; }

                if (prefs == null)
                    return;

                var currentHash = ComputeSchemaHash(sfScheduleDetail);
                if (!string.Equals(prefs.SchemaHash, currentHash, StringComparison.Ordinal))
                    return;

                var byName = sfScheduleDetail.Columns.ToDictionary(c => c.MappingName, c => c);

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
                    int cur = sfScheduleDetail.Columns.IndexOf(col);
                    if (cur != target && cur >= 0)
                    {
                        sfScheduleDetail.Columns.RemoveAt(cur);
                        sfScheduleDetail.Columns.Insert(target, col);
                    }
                }

                // 3) Width last (guard against tiny widths)
                const double MinWidth = 40.0;
                foreach (var p in prefs.Columns)
                    if (byName.TryGetValue(p.Name, out var col))
                        col.Width = Math.Max(MinWidth, p.Width);

                sfScheduleDetail.UpdateLayout();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.LoadDetailColumnState");
            }
        }

        // ========================================
        // DETAIL GRID COPY/PASTE HANDLERS
        // ========================================

        private void SfScheduleDetail_GridCopyContent(object? sender, Syncfusion.UI.Xaml.Grid.GridCopyPasteEventArgs e)
        {
            var currentCell = sfScheduleDetail.SelectionController.CurrentCellManager.CurrentCell;
            if (currentCell == null)
                return;

            // Enter edit mode so copy gets just the cell value
            if (!currentCell.IsEditing)
            {
                sfScheduleDetail.SelectionController.CurrentCellManager.BeginEdit();
            }
        }

        private void SfScheduleDetail_GridPasteContent(object? sender, Syncfusion.UI.Xaml.Grid.GridCopyPasteEventArgs e)
        {
            var currentCell = sfScheduleDetail.SelectionController.CurrentCellManager.CurrentCell;
            if (currentCell == null)
                return;

            // Check permission to edit this record
            var snapshot = sfScheduleDetail.SelectedItem as ProgressSnapshot;
            if (snapshot == null)
                return;

            bool canEdit = string.Equals(snapshot.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);
            if (!canEdit)
            {
                e.Handled = true;
                txtStatus.Text = "Cannot paste - you don't own this record";
                return;
            }

            // Enter edit mode so paste triggers normal edit flow
            if (!currentCell.IsEditing)
            {
                sfScheduleDetail.SelectionController.CurrentCellManager.BeginEdit();
            }
        }

        private static string ComputeSchemaHash(Syncfusion.UI.Xaml.Grid.SfDataGrid grid)
        {
            using var sha = SHA256.Create();
            var names = string.Join("|", grid.Columns.Select(c => c.MappingName).OrderBy(n => n));
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(names)));
        }

        // ========================================
        // DISCREPANCY FILTER DROPDOWN HANDLERS
        // ========================================

        // Clear discrepancy filter
        private void FilterDiscrepancy_Clear_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DiscrepancyFilter = DiscrepancyFilterType.None;
            txtStatus.Text = "Filter cleared";
        }

        // Filter by Actual Start variance (P6 vs MS)
        private void FilterDiscrepancy_Start_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DiscrepancyFilter = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.Start
                ? DiscrepancyFilterType.None
                : DiscrepancyFilterType.Start;
            txtStatus.Text = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.Start
                ? "Filtered: Actual Start discrepancies"
                : "Filter cleared";
        }

        // Filter by Actual Finish variance (P6 vs MS)
        private void FilterDiscrepancy_Finish_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DiscrepancyFilter = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.Finish
                ? DiscrepancyFilterType.None
                : DiscrepancyFilterType.Finish;
            txtStatus.Text = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.Finish
                ? "Filtered: Actual Finish discrepancies"
                : "Filter cleared";
        }

        // Filter by BudgetMHs variance (P6 vs MS)
        private void FilterDiscrepancy_MHs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DiscrepancyFilter = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.MHs
                ? DiscrepancyFilterType.None
                : DiscrepancyFilterType.MHs;
            txtStatus.Text = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.MHs
                ? "Filtered: MHs discrepancies"
                : "Filter cleared";
        }

        // Filter by PercentComplete variance (P6 vs MS)
        private void FilterDiscrepancy_Percent_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DiscrepancyFilter = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.PercentComplete
                ? DiscrepancyFilterType.None
                : DiscrepancyFilterType.PercentComplete;
            txtStatus.Text = _viewModel.DiscrepancyFilter == DiscrepancyFilterType.PercentComplete
                ? "Filtered: % Complete discrepancies"
                : "Filter cleared";
        }

        // Clear all filters (button and header filters)
        private void btnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearAllFilters();

            // Clear column header filters on master grid
            if (sfScheduleMaster?.View != null)
            {
                sfScheduleMaster.View.Filter = null;
                sfScheduleMaster.View.RefreshFilter();
            }

            txtStatus.Text = "All filters cleared";
        }

        // ========================================
        // HORIZONTAL SCROLLING
        // ========================================

        private const int WM_MOUSEHWHEEL = 0x020E;
        private HwndSource? _hwndSource;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        // Hook into parent window to handle native horizontal scroll wheel (tilt wheel)
        private void AttachHorizontalScrollHook()
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private void DetachHorizontalScrollHook()
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }

        // Handle WM_MOUSEHWHEEL for the schedule master grid
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                double scrollAmount = delta > 0 ? 60 : -60;

                // Check if cursor is over the master grid
                if (GetCursorPos(out POINT screenPt))
                {
                    var gridPoint = sfScheduleMaster.PointFromScreen(new Point(screenPt.X, screenPt.Y));
                    var gridBounds = new Rect(0, 0, sfScheduleMaster.ActualWidth, sfScheduleMaster.ActualHeight);

                    if (gridBounds.Contains(gridPoint))
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(sfScheduleMaster);
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                            handled = true;
                        }
                    }
                }
            }
            return IntPtr.Zero;
        }

        // Ctrl+ScrollWheel for horizontal scrolling
        private void SfScheduleMaster_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(sfScheduleMaster);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            }
        }

        // Helper to find a child of a specific type in the visual tree
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
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