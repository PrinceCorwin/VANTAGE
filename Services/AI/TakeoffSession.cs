using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // App-level session that owns the upload -> start -> poll lifecycle of one
    // AI Takeoff batch. Held at App.CurrentTakeoff so the user can navigate away
    // from the Takeoffs tab and return to a live view (full nav-persistence
    // behavior lands in subsequent phases of Plans/Takeoff_Session_PRD.md).
    public sealed class TakeoffSession
    {
        public string BatchId { get; }
        public string ConfigKey { get; }
        public string ConfigDisplayName { get; }
        public IReadOnlyList<string> SubmittedFiles { get; }
        public bool RevBubbleOnly { get; }
        public DateTime StartedUtc { get; private set; }

        public string LastStatus { get; private set; } = string.Empty;
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool CompletedSuccessfully { get; private set; }

        // Set on successful completion. Consumed by TakeoffView after the user
        // saves (or cancels) the SaveFileDialog via ClearPendingDownload().
        public string? PendingDownloadBatchId { get; private set; }

        // Live until RunAsync's finally records the final value, then frozen.
        public TimeSpan Elapsed
        {
            get
            {
                if (_finalElapsed.HasValue) return _finalElapsed.Value;
                if (StartedUtc == default) return TimeSpan.Zero;
                return DateTime.UtcNow - StartedUtc;
            }
        }

        public event EventHandler? StatusChanged;
        public event EventHandler? RunningChanged;
        public event EventHandler? Completed;

        private readonly CancellationTokenSource _cts = new();
        private TakeoffService? _service;
        private string? _executionArn;
        private TimeSpan? _finalElapsed;

        public TakeoffSession(
            string batchId,
            string configKey,
            string configDisplayName,
            IReadOnlyList<string> submittedFiles,
            bool revBubbleOnly)
        {
            BatchId = batchId;
            ConfigKey = configKey;
            ConfigDisplayName = configDisplayName;
            SubmittedFiles = submittedFiles;
            RevBubbleOnly = revBubbleOnly;
        }

        // Run the full takeoff lifecycle. Caller should fire-and-forget and
        // subscribe to events to drive UI; awaiting is optional.
        public async Task RunAsync()
        {
            using (LongRunningOps.Begin())
            {
                IsRunning = true;
                StartedUtc = DateTime.UtcNow;
                RaiseRunningChanged();

                List<string>? drawingKeys = null;

                try
                {
                    _service = new TakeoffService();

                    string drawingPrefix = TakeoffService.GetDrawingPrefix(ConfigKey);
                    SetStatus($"Uploading {SubmittedFiles.Count} drawing(s) to S3...");
                    var progress = new Progress<(int current, int total)>(p =>
                    {
                        SetStatus($"Uploading drawing {p.current} of {p.total}...");
                    });

                    var fileList = new List<string>(SubmittedFiles);
                    drawingKeys = await _service.UploadDrawingsAsync(
                        drawingPrefix, fileList, progress, _cts.Token);

                    _cts.Token.ThrowIfCancellationRequested();

                    string username = App.CurrentUser?.Username ?? "Unknown";
                    await _service.WriteMetadataAsync(
                        BatchId, SubmittedFiles.Count, username, ConfigDisplayName, BatchId, _cts.Token);

                    SetStatus("Starting AI extraction...");
                    _executionArn = await _service.StartBatchAsync(
                        BatchId, ConfigKey, drawingKeys, RevBubbleOnly, _cts.Token);

                    SetStatus("Processing - polling for completion...");

                    while (true)
                    {
                        await Task.Delay(3000, _cts.Token);

                        var (status, output) = await _service.PollExecutionAsync(_executionArn, _cts.Token);
                        string elapsedText = FormatElapsed(Elapsed);

                        SetStatus($"Status: {status}  ({elapsedText} elapsed, {SubmittedFiles.Count} drawing(s))");

                        if (status == "RUNNING")
                            continue;

                        if (status == "SUCCEEDED")
                        {
                            // Step Functions output only carries status/batch_id/excel_path (no counts).
                            // The Failed DWGs tab in the batch Excel is authoritative; here we only
                            // check whether the aggregation Lambda itself failed.
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
                                catch { /* parse error -- treat as success */ }
                            }

                            if (appFailed)
                            {
                                SetStatus($"Processing failed - {SubmittedFiles.Count} drawing(s) in {elapsedText}. No Excel output generated.");
                                CompletedSuccessfully = false;
                            }
                            else
                            {
                                SetStatus($"Completed in {elapsedText} - ready to download.");
                                PendingDownloadBatchId = BatchId;
                                CompletedSuccessfully = true;
                            }
                        }
                        else
                        {
                            SetStatus($"Execution {status} after {elapsedText}");
                            CompletedSuccessfully = false;
                        }

                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Processing cancelled by user.");
                    AppLogger.Info("Batch processing cancelled by user", "TakeoffSession.RunAsync");
                    CompletedSuccessfully = false;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TakeoffSession.RunAsync");
                    SetStatus($"Error: {ex.Message}");
                    CompletedSuccessfully = false;
                }
                finally
                {
                    _finalElapsed = StartedUtc == default ? TimeSpan.Zero : DateTime.UtcNow - StartedUtc;

                    // Clean up uploaded drawings from S3 (fire-and-forget so a slow
                    // S3 delete doesn't block the Completed event).
                    if (_service != null && drawingKeys != null && drawingKeys.Count > 0)
                        _ = CleanupDrawingsAsync(_service, drawingKeys);

                    _executionArn = null;
                    IsRunning = false;
                    IsCompleted = true;
                    RaiseRunningChanged();
                    RaiseCompleted();
                }
            }
        }

        // Stop the Step Functions execution and cancel the local polling loop.
        public async Task CancelAsync()
        {
            if (!IsRunning) return;

            SetStatus("Cancelling...");

            if (_service != null && !string.IsNullOrEmpty(_executionArn))
            {
                try
                {
                    await _service.StopExecutionAsync(_executionArn, "User cancelled batch");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TakeoffSession.CancelAsync.StopExecution");
                }
            }

            try { _cts.Cancel(); } catch { /* already disposed */ }
        }

        // Mark the pending download as consumed. Call after the SaveFileDialog
        // has been shown (regardless of save vs cancel) so a future nav-and-return
        // doesn't reopen it.
        public void ClearPendingDownload()
        {
            PendingDownloadBatchId = null;
        }

        private static async Task CleanupDrawingsAsync(TakeoffService service, List<string> drawingKeys)
        {
            try
            {
                await service.DeleteDrawingsAsync(drawingKeys);
                AppLogger.Info($"Cleaned up {drawingKeys.Count} drawing(s) from S3", "TakeoffSession.CleanupDrawingsAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffSession.CleanupDrawingsAsync");
            }
        }

        private void SetStatus(string message)
        {
            LastStatus = message;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseRunningChanged() => RunningChanged?.Invoke(this, EventArgs.Empty);
        private void RaiseCompleted() => Completed?.Invoke(this, EventArgs.Empty);

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
            return $"{elapsed.TotalSeconds:F0}s";
        }
    }
}
