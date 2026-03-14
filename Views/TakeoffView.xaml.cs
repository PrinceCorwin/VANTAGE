using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VANTAGE.Data;
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

        // Dropdown data for unit rates and ROC sets
        private List<(string ProjectID, string SetName)> _rateSets = new();
        private List<(string ProjectID, string SetName)> _rocSets = new();
        private bool _isLoadingRateDropdowns;

        public TakeoffView()
        {
            InitializeComponent();
            Loaded += TakeoffView_Loaded;
        }

        private async void TakeoffView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadConfigsAsync();
            await LoadRateOptionsAsync();
            await LoadROCSetsAsync();
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

                // Write metadata for Previous Batches listing
                string username = App.CurrentUser?.Username ?? "Unknown";
                string configName = _configs[configIndex].DisplayName;
                await _service.WriteMetadataAsync(_currentBatchId, _selectedFiles.Count, username, configName);

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
                        }
                        else
                        {
                            SetStatus($"Succeeded — {_selectedFiles.Count} drawing(s) processed in {elapsed}");

                            // Auto-download the Excel
                            await DownloadBatchExcelAsync(_currentBatchId);
                        }
                    }
                    else
                    {
                        SetStatus($"Execution {status} after {elapsed}");
                    }

                    // Clean up uploaded drawings from S3
                    try
                    {
                        await _service.DeleteDrawingsAsync(drawingKeys);
                        AppLogger.Info($"Cleaned up {drawingKeys.Count} drawing(s) from S3", "TakeoffView.BtnProcess_Click");
                    }
                    catch (Exception cleanupEx)
                    {
                        AppLogger.Error(cleanupEx, "TakeoffView.BtnProcess_Click.Cleanup");
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

        // Download batch Excel, run post-processor, and open the file
        private async System.Threading.Tasks.Task DownloadBatchExcelAsync(string batchId)
        {
            if (_service == null) return;

            var dialog = new SaveFileDialog
            {
                FileName = $"takeoff_{batchId}.xlsx",
                Filter = "Excel Files|*.xlsx",
                Title = "Save Takeoff Output"
            };

            if (dialog.ShowDialog() != true)
            {
                SetStatus("Download cancelled.");
                return;
            }

            SetStatus("Downloading Excel file...");

            try
            {
                await _service.DownloadExcelAsync(batchId, dialog.FileName);

                // Load project rate cache if selected
                var projectRateCache = await GetSelectedProjectRateCacheAsync();

                // Generate Labor and Summary tabs from the downloaded Material/Flagged output
                SetStatus("Generating Labor and Summary tabs...");
                var (missedMakeups, missedRates) = await System.Threading.Tasks.Task.Run(() =>
                    TakeoffPostProcessor.GenerateLaborAndSummary(dialog.FileName, projectRateCache));

                SetStatus($"Downloaded to {dialog.FileName}");

                // Send missed data to admins if checkbox checked and there are misses
                if (chkSendMissedToAdmin.IsChecked == true && (missedMakeups > 0 || missedRates > 0))
                    _ = SendMissedToAdminsAsync(dialog.FileName, missedMakeups, missedRates);

                // Open the file
                Process.Start(new ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.DownloadBatchExcelAsync");
                SetStatus($"Download error: {ex.Message}");
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

        // Show dropdown of previous batches for re-download
        private async void BtnPreviousBatches_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnPreviousBatches.IsEnabled = false;
                SetStatus("Loading previous batches...");

                _service?.Dispose();
                _service = new TakeoffService();

                var batches = await _service.ListBatchesAsync();

                if (batches.Count == 0)
                {
                    SetStatus("No previous batches found.");
                    return;
                }

                SetStatus("");

                var dialog = new Dialogs.PreviousBatchesDialog(batches)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true && dialog.SelectedBatchId != null)
                {
                    await DownloadBatchExcelAsync(dialog.SelectedBatchId);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.BtnPreviousBatches_Click");
                SetStatus($"Error loading batches: {ex.Message}");
            }
            finally
            {
                btnPreviousBatches.IsEnabled = true;
            }
        }

        // Send only Missed Makeups and Missed Rates tabs to all admins
        private async System.Threading.Tasks.Task SendMissedToAdminsAsync(
            string excelPath, int missedMakeups, int missedRates)
        {
            try
            {
                var adminEmails = await AzureDbManager.GetAdminEmailsAsync();
                if (adminEmails.Count == 0)
                {
                    AppLogger.Warning("No admin emails found — skipping missed data notification",
                        "TakeoffView.SendMissedToAdminsAsync");
                    return;
                }

                // Build a smaller Excel with only the missed tabs
                byte[] fileData = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var source = new ClosedXML.Excel.XLWorkbook(excelPath);
                    using var missedWb = new ClosedXML.Excel.XLWorkbook();

                    if (source.Worksheets.TryGetWorksheet("Missed Makeups", out var makeupSheet))
                        makeupSheet.CopyTo(missedWb, "Missed Makeups");

                    if (source.Worksheets.TryGetWorksheet("Missed Rates", out var ratesSheet))
                        ratesSheet.CopyTo(missedWb, "Missed Rates");

                    using var ms = new System.IO.MemoryStream();
                    missedWb.SaveAs(ms);
                    return ms.ToArray();
                });

                string username = App.CurrentUser?.Username ?? "Unknown";
                string baseName = System.IO.Path.GetFileNameWithoutExtension(excelPath);
                string fileName = $"{baseName}_missed.xlsx";

                var parts = new List<string>();
                if (missedMakeups > 0) parts.Add($"{missedMakeups} missed makeup(s)");
                if (missedRates > 0) parts.Add($"{missedRates} missed rate(s)");
                string summary = string.Join(" and ", parts);

                string subject = $"Takeoff Missed Data — {summary} ({username})";
                string body = $@"<p>A takeoff batch processed by <b>{username}</b> has {summary}.</p>
<p>The attached Excel contains only the <b>Missed Makeups</b> and <b>Missed Rates</b> tabs.</p>
<p style='color:#888;font-size:12px;'>This notification was sent automatically by VANTAGE: Milestone.</p>";

                foreach (string adminEmail in adminEmails)
                {
                    await EmailService.SendEmailWithAttachmentAsync(
                        adminEmail, "Admin", subject, body,
                        fileName, fileData,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                }

                AppLogger.Info($"Sent missed data notification to {adminEmails.Count} admin(s)",
                    "TakeoffView.SendMissedToAdminsAsync", username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.SendMissedToAdminsAsync");
            }
        }

        // Load project rate sets for unit rates dropdown
        private async System.Threading.Tasks.Task LoadRateOptionsAsync()
        {
            try
            {
                _isLoadingRateDropdowns = true;
                _rateSets = await ProjectRateRepository.GetProjectRateSetsAsync();

                cboUnitRates.Items.Clear();
                cboUnitRates.Items.Add("+ Upload New...");
                cboUnitRates.Items.Add("Default (Embedded)");
                foreach (var (projectId, setName) in _rateSets)
                    cboUnitRates.Items.Add($"{projectId} / {setName}");
                cboUnitRates.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.LoadRateOptionsAsync");
                cboUnitRates.Items.Clear();
                cboUnitRates.Items.Add("+ Upload New...");
                cboUnitRates.Items.Add("Default (Embedded)");
                cboUnitRates.SelectedIndex = 1;
            }
            finally
            {
                _isLoadingRateDropdowns = false;
            }
        }

        // Load distinct (ProjectID, SetName) from VMS_ROCRates for ROC set dropdown
        private async System.Threading.Tasks.Task LoadROCSetsAsync()
        {
            try
            {
                _isLoadingRateDropdowns = true;
                _rocSets = await ProjectRateRepository.GetROCSetsAsync();

                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                foreach (var (projectId, setName) in _rocSets)
                    cboROCSet.Items.Add($"{projectId} / {setName}");
                cboROCSet.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.LoadROCSetsAsync");
                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                cboROCSet.SelectedIndex = 1;
            }
            finally
            {
                _isLoadingRateDropdowns = false;
            }
        }

        // Handle "Upload New..." selection in unit rates dropdown — opens the management dialog
        private async void CboUnitRates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingRateDropdowns || cboUnitRates.SelectedIndex != 0) return;

            var dialog = new ManageProjectRatesDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();

            await LoadRateOptionsAsync();
        }

        // Handle "Create New..." selection in ROC set dropdown
        private void CboROCSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingRateDropdowns || cboROCSet.SelectedIndex != 0) return;

            var dialog = new ManageROCRatesDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();

            _ = LoadROCSetsAsync();
        }

        // Get selected project rate cache (null = use defaults)
        // Index 0 = "Upload New...", Index 1 = "Default (Embedded)", 2+ = project sets
        private async System.Threading.Tasks.Task<Dictionary<string, (double MH, string Unit)>?> GetSelectedProjectRateCacheAsync()
        {
            int rateIndex = cboUnitRates.SelectedIndex - 2;
            if (rateIndex < 0 || rateIndex >= _rateSets.Count)
                return null;

            var (projectId, setName) = _rateSets[rateIndex];
            return await ProjectRateRepository.BuildLookupCacheAsync(projectId, setName);
        }

        private void SetStatus(string message)
        {
            txtStatus.Text = message;
        }

    }
}
