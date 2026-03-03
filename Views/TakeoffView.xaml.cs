using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VANTAGE.Dialogs;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class TakeoffView : UserControl
    {
        private List<string> _selectedFiles = new();
        private List<(string Key, string DisplayName)> _configs = new();
        private string? _currentBatchId;
        private TakeoffService? _service;
        private bool _isLoadingConfigs;

        public TakeoffView()
        {
            InitializeComponent();
            Loaded += TakeoffView_Loaded;
        }

        private async void TakeoffView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadConfigsAsync();
        }

        private async System.Threading.Tasks.Task LoadConfigsAsync()
        {
            try
            {
                _isLoadingConfigs = true;
                SetStatus("Loading configs from S3...");
                _service?.Dispose();
                _service = new TakeoffService();

                _configs = await _service.ListConfigsAsync();

                cboConfig.Items.Clear();
                cboConfig.Items.Add("+ Create New Config...");
                foreach (var config in _configs)
                {
                    cboConfig.Items.Add(config.DisplayName);
                }

                if (_configs.Count > 0)
                    cboConfig.SelectedIndex = 1;

                btnEditConfig.IsEnabled = cboConfig.SelectedIndex > 0;
                SetStatus($"Loaded {_configs.Count} config(s). Select a config and drawing files to begin.");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.LoadConfigsAsync");
                SetStatus($"Error loading configs: {ex.Message}");
            }
            finally
            {
                _isLoadingConfigs = false;
            }
        }

        private async void BtnRefreshConfigs_Click(object sender, RoutedEventArgs e)
        {
            await LoadConfigsAsync();
        }

        private void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Drawing Files",
                Filter = "PDF Files|*.pdf|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            _selectedFiles = dialog.FileNames.ToList();
            txtFileCount.Text = $"{_selectedFiles.Count} file(s) selected";
            txtFileCount.Opacity = 1.0;

            // Show filenames in status
            var names = _selectedFiles.Select(System.IO.Path.GetFileName);
            string fileList = string.Join("\n    ", names);
            SetStatus($"Selected {_selectedFiles.Count} drawing(s):\n    {fileList}");
        }

        private async void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            int configIndex = cboConfig.SelectedIndex - 1;
            if (configIndex < 0 || configIndex >= _configs.Count)
            {
                MessageBox.Show("Please select a config.", "No Config Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedFiles.Count == 0)
            {
                MessageBox.Show("Please select drawing files.", "No Files Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string configKey = _configs[configIndex].Key;
            _currentBatchId = $"vantage-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            btnProcess.IsEnabled = false;
            btnSelectFiles.IsEnabled = false;
            btnDownload.Visibility = Visibility.Collapsed;
            resultsPanel.Visibility = Visibility.Collapsed;

            try
            {
                _service?.Dispose();
                _service = new TakeoffService();

                // Upload drawings to config-based prefix (overwrites existing)
                string drawingPrefix = TakeoffService.GetDrawingPrefix(configKey);
                SetStatus($"Uploading {_selectedFiles.Count} drawing(s) to S3...");
                var progress = new Progress<(int current, int total)>(p =>
                {
                    SetStatus($"Uploading drawing {p.current} of {p.total}...");
                });

                var drawingKeys = await _service.UploadDrawingsAsync(
                    drawingPrefix, _selectedFiles, progress);

                // Start execution
                SetStatus("Starting AI extraction...");
                var executionArn = await _service.StartBatchAsync(
                    _currentBatchId, configKey, drawingKeys);

                // Poll until done
                SetStatus("Processing — polling for completion...");
                var stopwatch = Stopwatch.StartNew();

                while (true)
                {
                    await System.Threading.Tasks.Task.Delay(3000);

                    var (status, output) = await _service.PollExecutionAsync(executionArn);
                    string elapsed = $"{stopwatch.Elapsed.TotalSeconds:F0}s";

                    SetStatus($"Status: {status}  ({elapsed} elapsed, {_selectedFiles.Count} drawing(s))");

                    if (status == "RUNNING")
                        continue;

                    stopwatch.Stop();

                    if (status == "SUCCEEDED")
                    {
                        // Check if the app-level output status is "failed" — SF execution
                        // succeeded but the processing itself may have failed (no Excel generated)
                        bool appFailed = false;
                        if (!string.IsNullOrEmpty(output))
                        {
                            try
                            {
                                using var check = JsonDocument.Parse(output);
                                if (check.RootElement.TryGetProperty("status", out var appStatus)
                                    && appStatus.GetString()?.Equals("failed", StringComparison.OrdinalIgnoreCase) == true)
                                    appFailed = true;
                            }
                            catch { /* parse error — show download anyway */ }
                        }

                        if (appFailed)
                        {
                            SetStatus($"Processing failed — {_selectedFiles.Count} drawing(s) in {elapsed}. No Excel output generated.");
                            btnDownload.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            SetStatus($"Succeeded — {_selectedFiles.Count} drawing(s) processed in {elapsed}");
                            btnDownload.Visibility = Visibility.Visible;
                        }
                        ShowResults(output);
                    }
                    else
                    {
                        SetStatus($"Execution {status} after {elapsed}");
                        ShowResults(output);
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.BtnProcess_Click");
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                btnProcess.IsEnabled = true;
                btnSelectFiles.IsEnabled = true;
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBatchId == null || _service == null)
                return;

            var dialog = new SaveFileDialog
            {
                FileName = $"takeoff_{_currentBatchId}.xlsx",
                Filter = "Excel Files|*.xlsx",
                Title = "Save Takeoff Output"
            };

            if (dialog.ShowDialog() != true)
                return;

            btnDownload.IsEnabled = false;
            SetStatus("Downloading Excel file...");

            try
            {
                await _service.DownloadExcelAsync(_currentBatchId, dialog.FileName);
                SetStatus($"Downloaded to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.BtnDownload_Click");
                SetStatus($"Download error: {ex.Message}");
            }
            finally
            {
                btnDownload.IsEnabled = true;
            }
        }

        private void BtnManageDrawings_Click(object sender, RoutedEventArgs e)
        {
            if (_configs.Count == 0)
            {
                MessageBox.Show("No configs loaded. Please refresh configs first.", "No Configs",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int configIndex = cboConfig.SelectedIndex - 1;
            var dialog = new ManageDrawingsDialog(_configs, Math.Max(0, configIndex));
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }

        private void CboConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingConfigs) return;

            if (cboConfig.SelectedIndex == 0)
            {
                // "Create New Config..." selected — open creator and revert selection
                var creator = new ConfigCreatorWindow();
                creator.Owner = Window.GetWindow(this);
                bool? result = creator.ShowDialog();

                if (result == true)
                {
                    // Reload configs to pick up the new one
                    _ = LoadConfigsAsync();
                }
                else if (_configs.Count > 0)
                {
                    _isLoadingConfigs = true;
                    cboConfig.SelectedIndex = 1;
                    _isLoadingConfigs = false;
                }
                else
                {
                    _isLoadingConfigs = true;
                    cboConfig.SelectedIndex = -1;
                    _isLoadingConfigs = false;
                }
                return;
            }

            // Enable Edit button only when a real config is selected
            btnEditConfig.IsEnabled = cboConfig.SelectedIndex > 0;
        }

        private void BtnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            int configIndex = cboConfig.SelectedIndex - 1;
            if (configIndex < 0 || configIndex >= _configs.Count) return;

            string configKey = _configs[configIndex].Key;
            var editor = new ConfigCreatorWindow(configKey);
            editor.Owner = Window.GetWindow(this);
            bool? result = editor.ShowDialog();

            if (result == true)
                _ = LoadConfigsAsync();
        }

        private void SetStatus(string message)
        {
            txtStatus.Text = message;
        }

        // Parse the execution output JSON and display a clean summary
        private void ShowResults(string? outputJson)
        {
            summaryPanel.Children.Clear();
            drawingsListPanel.Children.Clear();
            connTypePanel.Children.Clear();
            connSizePanel.Children.Clear();
            compTypePanel.Children.Clear();
            connDrawingPanel.Children.Clear();

            if (string.IsNullOrEmpty(outputJson))
            {
                AddSummaryLine("No output returned.");
                resultsPanel.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(outputJson);
                var root = doc.RootElement;

                // Top-level status
                string? appStatus = null;
                if (root.TryGetProperty("status", out var statusEl))
                {
                    appStatus = statusEl.GetString();
                    AddSummaryLine($"Status:  {appStatus}", true);
                }

                // Show error/message from backend if present
                if (root.TryGetProperty("error", out var errorEl))
                    AddSummaryLine($"Error:  {errorEl.GetString()}", false, true);
                if (root.TryGetProperty("message", out var msgEl))
                    AddSummaryLine($"Message:  {msgEl.GetString()}", false, true);

                if (appStatus?.Equals("failed", StringComparison.OrdinalIgnoreCase) == true)
                    AddSummaryLine("Processing failed — results below may be partial or from prior cached data.", false, true);

                // Summary section
                if (root.TryGetProperty("summary", out var summary))
                {
                    if (summary.TryGetProperty("total_drawings", out var td))
                        AddSummaryLine($"Total Drawings:  {td.GetInt32()}");

                    if (summary.TryGetProperty("total_bom_items", out var tb))
                        AddSummaryLine($"Total BOM Items:  {tb.GetInt32()}");

                    if (summary.TryGetProperty("total_connections", out var tc))
                        AddSummaryLine($"Total Connections:  {tc.GetInt32()}");

                    // Drawing numbers list
                    if (summary.TryGetProperty("drawing_numbers", out var drawings))
                    {
                        int count = 0;
                        foreach (var d in drawings.EnumerateArray())
                        {
                            AddDetailLine(drawingsListPanel, d.GetString() ?? "");
                            count++;
                        }
                        drawingsExpander.Header = $"Drawings Processed ({count})";
                    }

                    // Connections by type
                    if (summary.TryGetProperty("connections_by_type", out var cbt))
                    {
                        int total = 0;
                        foreach (var prop in cbt.EnumerateObject())
                        {
                            int val = prop.Value.GetInt32();
                            total += val;
                            AddDetailLine(connTypePanel, $"{prop.Name}:  {val}");
                        }
                        connTypeExpander.Header = $"Connections by Type ({total})";
                    }

                    // Connections by size
                    if (summary.TryGetProperty("connections_by_size", out var cbs))
                    {
                        foreach (var prop in cbs.EnumerateObject())
                            AddDetailLine(connSizePanel, $"{prop.Name}\":  {prop.Value.GetInt32()}");
                        connSizeExpander.Header = $"Connections by Size ({cbs.EnumerateObject().Count()})";
                    }

                    // Components by type
                    if (summary.TryGetProperty("components_by_type", out var cbtype))
                    {
                        int total = 0;
                        foreach (var prop in cbtype.EnumerateObject())
                        {
                            int val = prop.Value.GetInt32();
                            total += val;
                            AddDetailLine(compTypePanel, $"{prop.Name}:  {val}");
                        }
                        compTypeExpander.Header = $"Components by Type ({total})";
                    }

                    // Connections by drawing
                    if (summary.TryGetProperty("connections_by_drawing", out var cbd))
                    {
                        foreach (var prop in cbd.EnumerateObject())
                            AddDetailLine(connDrawingPanel, $"{prop.Name}:  {prop.Value.GetInt32()}");
                        connDrawingExpander.Header = $"Connections by Drawing ({cbd.EnumerateObject().Count()})";
                    }
                }

                // Flagged count if present
                if (root.TryGetProperty("summary", out var sum2) &&
                    sum2.TryGetProperty("flagged_count", out var flagged) &&
                    flagged.GetInt32() > 0)
                {
                    AddSummaryLine($"Flagged Items:  {flagged.GetInt32()}", false, true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.ShowResults");
                AddSummaryLine("Could not parse results. Raw output:");
                AddSummaryLine(outputJson);
            }

            resultsPanel.Visibility = Visibility.Visible;
        }

        // Add a line to the summary stats area
        private void AddSummaryLine(string text, bool bold = false, bool warning = false)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = warning
                    ? System.Windows.Media.Brushes.Orange
                    : (System.Windows.Media.Brush)FindResource("ForegroundColor"),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, 2, 0, 2)
            };
            summaryPanel.Children.Add(tb);
        }

        // Add a line to a detail expander panel
        private void AddDetailLine(StackPanel panel, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor"),
                Opacity = 0.85,
                Margin = new Thickness(0, 1, 0, 1)
            };
            panel.Children.Add(tb);
        }
    }
}
