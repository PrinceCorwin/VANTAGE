using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VANTAGE.Repositories;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class ScheduleView : UserControl
    {
        private readonly ScheduleViewModel _viewModel;
        private const string GridPrefsKey = "ScheduleGrid.PreferencesJson";
        private DispatcherTimer _resizeSaveTimer = null!;

        public ScheduleView()
        {
            InitializeComponent();

            _viewModel = new ScheduleViewModel();
            DataContext = _viewModel;

            Loaded += ScheduleView_Loaded;

            // Load column state after view is loaded and columns are realized
            Loaded += (_, __) =>
            {
                sfScheduleMaster.Opacity = 0;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadColumnState();
                    sfScheduleMaster.Opacity = 1;
                }), DispatcherPriority.ContextIdle);
            };

            // Save when view closes
            Unloaded += (_, __) => SaveColumnState();

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

        }
        
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