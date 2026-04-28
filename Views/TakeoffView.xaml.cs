using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
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
        private TakeoffService? _service;
        private bool _isLoadingConfigs;

        // Currently-subscribed session (set when this view fires off a batch).
        // Phase 2 will also subscribe in Loaded for nav-and-return.
        private TakeoffSession? _subscribedSession;

        // Dropdown data for unit rates and ROC sets
        private List<(string ProjectID, string SetName)> _rateSets = new();
        private bool _isLoadingRateDropdowns;

        public TakeoffView()
        {
            InitializeComponent();
            Loaded += TakeoffView_Loaded;
            Unloaded += TakeoffView_Unloaded;
        }

        private async void TakeoffView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadConfigsAsync();
            await LoadRateOptionsAsync();
            await RestoreFromSessionIfActive();
        }

        private void TakeoffView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Detach from any in-flight session so its events don't fire on a
            // detached view. Do NOT cancel or dispose the session — that's
            // exactly the state the next view instance will restore from.
            UnsubscribeFromSession();
        }

        // If a takeoff session is in flight (or recently completed) when this
        // view is constructed, subscribe and rebuild the UI to reflect that
        // session's state. Lets a batch survive a tab switch.
        // If the session completed while the user was on another tab and the
        // download has not been consumed yet, the SaveFileDialog opens here.
        private async System.Threading.Tasks.Task RestoreFromSessionIfActive()
        {
            var session = App.CurrentTakeoff;
            if (session == null) return;

            SubscribeToSession(session);
            SetStatus(session.LastStatus);

            // Restore the file list AND the local _selectedFiles field so a
            // subsequent Process click sees the same set the session used.
            // Without this sync, txtFileCount would say "1 file(s) selected"
            // but BtnProcess_Click's _selectedFiles.Count == 0 check would
            // reject the click.
            _selectedFiles = new List<string>(session.SubmittedFiles);
            txtFileCount.Text = $"{_selectedFiles.Count} file(s) selected";
            txtFileCount.Opacity = 1.0;
            chkRevBubbleOnly.IsChecked = session.RevBubbleOnly;

            if (session.IsRunning)
            {
                btnProcess.Visibility = Visibility.Collapsed;
                btnCancel.Visibility = Visibility.Visible;
                btnCancel.IsEnabled = true;
                btnSelectFiles.IsEnabled = false;
                btnPreviousBatches.IsEnabled = false;
                btnRecalcExcel.IsEnabled = false;
                return;
            }

            // Session completed while we were away — auto-open SaveFileDialog.
            // ClearPendingDownload first so a cancelled dialog doesn't reopen
            // on the next nav-and-return; user falls back to Previous Batches.
            if (!string.IsNullOrEmpty(session.PendingDownloadBatchId))
            {
                string pendingBatchId = session.PendingDownloadBatchId;
                int totalSubmitted = session.SubmittedFiles.Count;
                string elapsed = FormatElapsed(session.Elapsed);

                session.ClearPendingDownload();

                await DownloadBatchExcelAsync(
                    pendingBatchId, totalSubmitted: totalSubmitted, elapsed: elapsed);
            }
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
                {
                    // Restore last-selected config; fall back to first config if the
                    // saved key no longer exists (config deleted) or never existed.
                    string? savedKey = SettingsManager.GetUserSetting("Takeoff.LastConfigKey");
                    int savedIndex = -1;
                    if (!string.IsNullOrEmpty(savedKey))
                    {
                        for (int i = 0; i < _configs.Count; i++)
                        {
                            if (_configs[i].Key == savedKey)
                            {
                                savedIndex = i + 1;  // +1 because index 0 is "+ Create New Config..."
                                break;
                            }
                        }
                    }
                    cboConfig.SelectedIndex = savedIndex > 0 ? savedIndex : 1;
                }

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

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            int configIndex = cboConfig.SelectedIndex - 1;
            if (configIndex < 0 || configIndex >= _configs.Count)
            {
                AppMessageBox.Show("Please select a config.", "No Config Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedFiles.Count == 0)
            {
                AppMessageBox.Show("Please select drawing files.", "No Files Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string configKey = _configs[configIndex].Key;
            string configName = _configs[configIndex].DisplayName;
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string? customName = txtBatchName.Text?.Trim();

            // Validate batch name before starting the expensive agent run
            if (!string.IsNullOrEmpty(customName) && !Regex.IsMatch(customName, @"^[a-zA-Z0-9\-_]+$"))
            {
                AppMessageBox.Show(
                    "Batch name can only contain letters, numbers, hyphens, and underscores (no spaces or special characters).",
                    "Invalid Batch Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBatchName.Focus();
                return;
            }

            string batchId = string.IsNullOrEmpty(customName)
                ? $"AwsDwgTakeoff-{timestamp}"
                : $"{customName}-{timestamp}";

            bool revBubbleOnly = chkRevBubbleOnly.IsChecked == true;

            var session = new TakeoffSession(
                batchId, configKey, configName,
                _selectedFiles.ToList(), revBubbleOnly);

            SubscribeToSession(session);
            App.SetCurrentTakeoff(session);

            // UI state for in-progress run
            btnProcess.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Visible;
            btnSelectFiles.IsEnabled = false;

            // Fire-and-forget; the session raises events the view subscribes to.
            _ = session.RunAsync();
        }

        private void SubscribeToSession(TakeoffSession session)
        {
            UnsubscribeFromSession();
            _subscribedSession = session;
            session.StatusChanged += OnSessionStatusChanged;
            session.RunningChanged += OnSessionRunningChanged;
            session.Completed += OnSessionCompleted;
        }

        private void UnsubscribeFromSession()
        {
            if (_subscribedSession == null) return;
            _subscribedSession.StatusChanged -= OnSessionStatusChanged;
            _subscribedSession.RunningChanged -= OnSessionRunningChanged;
            _subscribedSession.Completed -= OnSessionCompleted;
            _subscribedSession = null;
        }

        private void OnSessionStatusChanged(object? sender, EventArgs e)
        {
            if (sender is TakeoffSession s)
                SetStatus(s.LastStatus);
        }

        private void OnSessionRunningChanged(object? sender, EventArgs e)
        {
            // Disable Previous Batches + Recalc Excel while a takeoff is in
            // flight (each spawns its own TakeoffService, but greying out
            // avoids the user kicking off competing S3/Bedrock work mid-batch).
            // Re-enabled when the session ends — fires from the same finally
            // block in TakeoffSession.RunAsync that flips IsRunning to false.
            if (sender is TakeoffSession s)
            {
                btnPreviousBatches.IsEnabled = !s.IsRunning;
                btnRecalcExcel.IsEnabled = !s.IsRunning;
            }
        }

        private async void OnSessionCompleted(object? sender, EventArgs e)
        {
            if (sender is not TakeoffSession s) return;

            try
            {
                if (s.CompletedSuccessfully && !string.IsNullOrEmpty(s.PendingDownloadBatchId))
                {
                    string pendingBatchId = s.PendingDownloadBatchId;
                    int totalSubmitted = s.SubmittedFiles.Count;
                    string elapsed = FormatElapsed(s.Elapsed);

                    SetStatus($"Completed in {elapsed} — downloading results...");
                    s.ClearPendingDownload();

                    await DownloadBatchExcelAsync(pendingBatchId, totalSubmitted: totalSubmitted, elapsed: elapsed);
                }
            }
            finally
            {
                btnProcess.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Collapsed;
                btnCancel.IsEnabled = true;
                btnSelectFiles.IsEnabled = true;

                UnsubscribeFromSession();
            }
        }

        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var session = App.CurrentTakeoff;
            if (session == null || !session.IsRunning) return;

            btnCancel.IsEnabled = false;
            await session.CancelAsync();
        }

        // Download batch Excel, run post-processor, and open the file.
        // When totalSubmitted is provided (initial download path), the final status reports
        // per-drawing success/failure counts from the Failed DWGs tab.
        private async System.Threading.Tasks.Task DownloadBatchExcelAsync(
            string batchId, string? batchName = null, int? totalSubmitted = null, string? elapsed = null)
        {
            if (_service == null) return;

            // Use batch name for filename if provided, otherwise fall back to batch ID
            string fileName = !string.IsNullOrEmpty(batchName) ? $"{batchName}.xlsx" : $"takeoff_{batchId}.xlsx";

            var dialog = new SaveFileDialog
            {
                FileName = fileName,
                Filter = "Excel Files|*.xlsx",
                Title = "Save Takeoff Output"
            };

            if (dialog.ShowDialog() != true)
            {
                SetStatus("Download cancelled.");
                return;
            }

            try
            {
                await _service.DownloadExcelAsync(batchId, dialog.FileName);

                // Prompt for any blank Component values before processing
                PromptForBlankComponents(dialog.FileName);

                // Load project rate cache if selected
                var projectRateCache = await GetSelectedProjectRateCacheAsync();
                var (missedMakeups, missedRates) = await System.Threading.Tasks.Task.Run(() =>
                    TakeoffPostProcessor.GenerateLaborAndSummary(dialog.FileName, projectRateCache));

                int failedDrawings = CountFailedDwgRows(dialog.FileName);

                if (totalSubmitted.HasValue)
                {
                    int succeeded = Math.Max(0, totalSubmitted.Value - failedDrawings);
                    string prefix = !string.IsNullOrEmpty(elapsed) ? $"Completed in {elapsed} — " : "";
                    string summary = failedDrawings > 0
                        ? $"{prefix}{succeeded} succeeded, {failedDrawings} failed ({totalSubmitted.Value} total)"
                        : $"{prefix}all {totalSubmitted.Value} drawing(s) succeeded";
                    SetStatus(summary);
                    AppendStatus($"Downloaded to {dialog.FileName}");
                }
                else
                {
                    string summary = failedDrawings > 0
                        ? $"Downloaded to {dialog.FileName} — {failedDrawings} failed drawing(s)"
                        : $"Downloaded to {dialog.FileName}";
                    SetStatus(summary);
                }

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

        // Count data rows in the batch Excel's "Failed DWGs" tab.
        // Returns 0 if the tab is missing or contains only the aggregation Lambda's
        // "No failed drawings..." sentinel in cell A1.
        private static int CountFailedDwgRows(string excelPath)
        {
            try
            {
                using var workbook = new XLWorkbook(excelPath);
                if (!workbook.TryGetWorksheet("Failed DWGs", out var ws))
                    return 0;

                var lastRow = ws.LastRowUsed();
                if (lastRow == null) return 0;

                string cellA1 = ws.Cell(1, 1).GetString() ?? "";
                if (cellA1.StartsWith("No failed drawings", StringComparison.OrdinalIgnoreCase))
                    return 0;

                // Header row + N data rows — LastRowUsed includes the header, so subtract 1.
                return Math.Max(0, lastRow.RowNumber() - 1);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.CountFailedDwgRows");
                return 0;
            }
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

            // Persist last-selected config across tab navigations and sessions
            int configIdx = cboConfig.SelectedIndex - 1;
            if (configIdx >= 0 && configIdx < _configs.Count)
            {
                SettingsManager.SetUserSetting(
                    "Takeoff.LastConfigKey", _configs[configIdx].Key, "string");
            }
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
                    await DownloadBatchExcelAsync(dialog.SelectedBatchId, dialog.SelectedBatchName);
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

        // Recalculate Labor, Summary, and other tabs from an existing Excel's Material tab
        private async void BtnRecalcExcel_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Select Takeoff Excel to Recalculate"
            };

            if (openDialog.ShowDialog() != true)
                return;

            string filePath = openDialog.FileName;

            try
            {
                btnRecalcExcel.IsEnabled = false;
                SetStatus("Recalculating Excel...");

                // Prompt for any blank Component values before recalculating
                PromptForBlankComponents(filePath);

                // Load project rate cache if selected
                var projectRateCache = await GetSelectedProjectRateCacheAsync();

                var (missedMakeups, missedRates) = await System.Threading.Tasks.Task.Run(() =>
                    TakeoffPostProcessor.GenerateLaborAndSummary(filePath, projectRateCache));

                SetStatus($"Recalculated: {missedMakeups} missed makeups, {missedRates} missed rates");

                // Send missed data to admins if checkbox checked and there are misses
                if (chkSendMissedToAdmin.IsChecked == true && (missedMakeups > 0 || missedRates > 0))
                    _ = SendMissedToAdminsAsync(filePath, missedMakeups, missedRates);

                // Open the file
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffView.BtnRecalcExcel_Click");
                SetStatus($"Recalc error: {ex.Message}");
            }
            finally
            {
                btnRecalcExcel.IsEnabled = true;
            }
        }

        // Prompt user to assign components for any Material rows with blank Component
        // Returns false if user cancelled and processing should stop
        private bool PromptForBlankComponents(string excelPath)
        {
            var blankRows = TakeoffPostProcessor.GetBlankComponentRows(excelPath);
            if (blankRows.Count == 0) return true;

            var dialog = new BlankComponentDialog(blankRows) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                var assignments = dialog.GetAssignments();
                if (assignments.Count > 0)
                    TakeoffPostProcessor.WriteBlankComponents(excelPath, assignments);
            }

            return true;
        }

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


        // Handle "Upload New..." selection in unit rates dropdown — opens the management dialog
        private async void CboUnitRates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingRateDropdowns || cboUnitRates.SelectedIndex != 0) return;

            var dialog = new ManageProjectRatesDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();

            await LoadRateOptionsAsync();
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

        // Append a line to the status panel without replacing existing text
        private void AppendStatus(string message)
        {
            txtStatus.Text += "\n" + message;
        }

        // Format elapsed time as "Xs" for short durations, "M:SS" for longer
        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
            return $"{elapsed.TotalSeconds:F0}s";
        }

    }
}
