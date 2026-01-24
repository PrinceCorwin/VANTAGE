using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Syncfusion.UI.Xaml.Grid;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Models.AI;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ProgressScanDialog : Window
    {
        // File info for display in list
        public class FileItem
        {
            public string FilePath { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string PageInfo { get; set; } = string.Empty;
            public int PageCount { get; set; }
        }

        private readonly ObservableCollection<FileItem> _files = new();
        private readonly ObservableCollection<ScanReviewItem> _reviewItems = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ProgressScanService _scanService = new();

        public ProgressScanDialog()
        {
            InitializeComponent();
            lstFiles.ItemsSource = _files;
            sfReviewGrid.ItemsSource = _reviewItems;

            // Subscribe to review item changes to update selection count
            _reviewItems.CollectionChanged += (s, e) => UpdateSelectionCount();

            // Dispose service when window closes
            Closed += (s, e) => _scanService.Dispose();
        }

        // Drag and drop handlers
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                dropZone.BorderBrush = (Brush)FindResource("AccentColor");
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            dropZone.BorderBrush = (Brush)FindResource("ControlBorder");
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            dropZone.BorderBrush = (Brush)FindResource("ControlBorder");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        // Browse button handler
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Progress Sheets",
                Filter = "Supported Files|*.pdf;*.png;*.jpg;*.jpeg|PDF Files|*.pdf|Image Files|*.png;*.jpg;*.jpeg",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        // Add files to the list
        private void AddFiles(IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                // Skip if already added
                if (_files.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Skip unsupported files
                if (!PdfToImageConverter.IsPdfFile(path) && !PdfToImageConverter.IsImageFile(path))
                    continue;

                var fileName = Path.GetFileName(path);
                int pageCount = 1;
                string pageInfo = "";

                if (PdfToImageConverter.IsPdfFile(path))
                {
                    pageCount = PdfToImageConverter.GetPageCount(path);
                    pageInfo = $"({pageCount} pages)";
                }

                _files.Add(new FileItem
                {
                    FilePath = path,
                    FileName = fileName,
                    PageCount = pageCount,
                    PageInfo = pageInfo
                });
            }

            UpdateFileListUI();
        }

        // Remove file from list
        private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string filePath)
            {
                var item = _files.FirstOrDefault(f => f.FilePath == filePath);
                if (item != null)
                {
                    _files.Remove(item);
                    UpdateFileListUI();
                }
            }
        }

        // Clear all files
        private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
            UpdateFileListUI();
        }

        // Update UI based on file list
        private void UpdateFileListUI()
        {
            bool hasFiles = _files.Count > 0;

            emptyDropState.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            filesListState.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;

            int totalPages = _files.Sum(f => f.PageCount);
            txtTotalPages.Text = totalPages.ToString();

            btnStartProcessing.IsEnabled = hasFiles;
        }

        // Start processing
        private async void BtnStartProcessing_Click(object sender, RoutedEventArgs e)
        {
            if (_files.Count == 0) return;

            // Switch to processing panel
            panelUpload.Visibility = Visibility.Collapsed;
            panelProcessing.Visibility = Visibility.Visible;

            _cancellationTokenSource = new CancellationTokenSource();
            var filePaths = _files.Select(f => f.FilePath).ToList();

            // Progress handler
            var progress = new Progress<ScanProgress>(p =>
            {
                progressBar.Progress = p.ProgressPercent;
                txtProcessingStatus.Text = $"Processing page {p.CurrentPage} of {p.TotalPages}...";
                txtExtractedCount.Text = $"Extracted: {p.ExtractedCount} entries";
            });

            try
            {
                var result = await _scanService.ProcessFilesAsync(
                    filePaths, progress, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // User cancelled - go back to upload
                    panelProcessing.Visibility = Visibility.Collapsed;
                    panelUpload.Visibility = Visibility.Visible;
                    return;
                }

                // Process results and show review
                await ProcessResultsAsync(result);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressScanDialog.BtnStartProcessing_Click");
                MessageBox.Show($"Error during processing: {ex.Message}",
                    "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);

                panelProcessing.Visibility = Visibility.Collapsed;
                panelUpload.Visibility = Visibility.Visible;
            }
        }

        // Process extraction results and match to database
        private async System.Threading.Tasks.Task ProcessResultsAsync(ScanBatchResult result)
        {
            _reviewItems.Clear();

            // Get all user's activities for matching by ActivityID
            var username = App.CurrentUser?.Username ?? "";
            var (activities, _) = await ActivityRepository.GetAllActivitiesAsync($"AssignedTo = '{username}'");
            var activityDict = activities.ToDictionary(a => a.ActivityID, a => a);

            int matchedCount = 0;
            int notFoundCount = 0;
            int warningCount = 0;

            foreach (var extraction in result.Extractions)
            {
                var reviewItem = new ScanReviewItem
                {
                    ExtractedUniqueId = extraction.UniqueId,
                    ExtractedDone = extraction.Done,
                    ExtractedQty = extraction.Qty,
                    ExtractedPct = extraction.Pct,
                    Confidence = extraction.Confidence,
                    RawExtraction = extraction.Raw
                };

                // Try to match to database by ActivityID (parse extracted string to int)
                if (int.TryParse(extraction.UniqueId, out int activityId) &&
                    activityDict.TryGetValue(activityId, out var activity))
                {
                    reviewItem.MatchedRecord = activity;
                    reviewItem.Description = activity.Description;
                    reviewItem.CurrentQty = (decimal)activity.EarnQtyEntry;
                    reviewItem.CurrentPercent = (decimal)activity.PercentEntry;

                    // Set new values from extraction
                    if (extraction.Done == true)
                    {
                        reviewItem.NewPercent = 100;
                    }
                    else if (extraction.Pct.HasValue)
                    {
                        reviewItem.NewPercent = extraction.Pct;
                    }

                    if (extraction.Qty.HasValue)
                    {
                        reviewItem.NewQty = extraction.Qty;
                    }

                    // Validate
                    ValidateReviewItem(reviewItem);

                    if (reviewItem.Status == ScanMatchStatus.Ready)
                    {
                        reviewItem.IsSelected = reviewItem.Confidence >= 90;
                        matchedCount++;
                    }
                    else if (reviewItem.Status == ScanMatchStatus.Warning)
                    {
                        warningCount++;
                        matchedCount++;
                    }
                }
                else
                {
                    reviewItem.Status = ScanMatchStatus.NotFound;
                    reviewItem.ValidationMessage = "Not found in your records";
                    reviewItem.Description = "(not found)";
                    notFoundCount++;
                }

                _reviewItems.Add(reviewItem);
            }

            // Update summary
            txtReviewSummary.Text = $"{result.Extractions.Count} extracted │ {matchedCount} matched │ {notFoundCount} not found │ {warningCount} warnings";

            if (result.HasErrors)
            {
                txtReviewSummary.Text += $" │ {result.FailedPages} pages failed";
            }

            UpdateSelectionCount();

            // Switch to review panel
            panelProcessing.Visibility = Visibility.Collapsed;
            panelReview.Visibility = Visibility.Visible;
        }

        // Validate a review item and set status/message
        private void ValidateReviewItem(ScanReviewItem item)
        {
            var warnings = new List<string>();

            // Low confidence
            if (item.Confidence < 70)
            {
                warnings.Add("Low confidence");
            }

            // Progress decrease
            if (item.NewPercent.HasValue && item.CurrentPercent.HasValue &&
                item.NewPercent < item.CurrentPercent)
            {
                warnings.Add("% decreased");
            }

            // Invalid percent
            if (item.NewPercent.HasValue && item.NewPercent > 100)
            {
                item.Status = ScanMatchStatus.Error;
                item.ValidationMessage = "% cannot exceed 100";
                return;
            }

            // QTY exceeds total (would need total qty from activity)
            if (item.MatchedRecord != null && item.NewQty.HasValue)
            {
                var totalQty = (decimal)item.MatchedRecord.Quantity;
                if (totalQty > 0 && item.NewQty > totalQty)
                {
                    warnings.Add("QTY exceeds total");
                }
            }

            if (warnings.Count > 0)
            {
                item.Status = ScanMatchStatus.Warning;
                item.ValidationMessage = string.Join(", ", warnings);
            }
            else
            {
                item.Status = ScanMatchStatus.Ready;
                item.ValidationMessage = "Ready";
            }
        }

        // Cancel processing
        private void BtnCancelProcessing_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        // Filter changed
        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (sfReviewGrid?.View == null) return;

            sfReviewGrid.View.Filter = item =>
            {
                if (item is not ScanReviewItem reviewItem) return false;

                if (rbFilterAll.IsChecked == true) return true;
                if (rbFilterReady.IsChecked == true) return reviewItem.Status == ScanMatchStatus.Ready;
                if (rbFilterWarnings.IsChecked == true) return reviewItem.Status == ScanMatchStatus.Warning;
                if (rbFilterErrors.IsChecked == true) return reviewItem.Status == ScanMatchStatus.NotFound || reviewItem.Status == ScanMatchStatus.Error;

                return true;
            };

            sfReviewGrid.View.RefreshFilter();
        }

        // Select all ready items
        private void BtnSelectAllReady_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _reviewItems)
            {
                item.IsSelected = item.Status == ScanMatchStatus.Ready ||
                                  (item.Status == ScanMatchStatus.Warning && item.Confidence >= 70);
            }
            sfReviewGrid.View?.RefreshFilter();
            UpdateSelectionCount();
        }

        // Clear selection
        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _reviewItems)
            {
                item.IsSelected = false;
            }
            sfReviewGrid.View?.RefreshFilter();
            UpdateSelectionCount();
        }

        // Update selected count
        private void UpdateSelectionCount()
        {
            int count = _reviewItems.Count(i => i.IsSelected);
            txtSelectedCount.Text = $"Selected: {count}";
            btnApply.IsEnabled = count > 0;
            btnApply.Content = $"Apply Selected ({count})";
        }

        // Back to upload
        private void BtnBackToUpload_Click(object sender, RoutedEventArgs e)
        {
            _reviewItems.Clear();
            panelReview.Visibility = Visibility.Collapsed;
            panelUpload.Visibility = Visibility.Visible;
        }

        // Apply selected updates
        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _reviewItems.Where(i => i.IsSelected && i.MatchedRecord != null).ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No items selected to apply.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirmation
            var result = MessageBox.Show(
                $"You are about to update {selectedItems.Count} records.\n\nThis action will:\n• Update progress percentages for selected records\n• Mark records as modified for next sync\n\nContinue?",
                "Confirm Progress Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            int successCount = 0;
            int failCount = 0;

            foreach (var item in selectedItems)
            {
                try
                {
                    var activity = item.MatchedRecord!;

                    // Determine the new percent
                    double newPercent;
                    if (item.NewPercent.HasValue)
                    {
                        newPercent = (double)item.NewPercent.Value;
                    }
                    else if (item.NewQty.HasValue && activity.Quantity > 0)
                    {
                        // Calculate percent from qty
                        newPercent = ((double)item.NewQty.Value / activity.Quantity) * 100;
                    }
                    else
                    {
                        continue; // No update to make
                    }

                    // Update the activity
                    activity.PercentEntry = newPercent;
                    activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                    activity.UpdatedUtcDate = DateTime.UtcNow;
                    activity.LocalDirty = 1;

                    await ActivityRepository.UpdateActivityInDatabase(activity);
                    successCount++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressScanDialog.BtnApply_Click");
                    failCount++;
                }
            }

            AppLogger.Info($"Progress scan applied: {successCount} records updated",
                "ProgressScanDialog.BtnApply_Click", App.CurrentUser?.Username);

            if (failCount > 0)
            {
                MessageBox.Show($"Updated {successCount} records.\n{failCount} records failed to update.",
                    "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"Successfully updated {successCount} records.",
                    "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            DialogResult = true;
            Close();
        }

        // Cancel dialog
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            DialogResult = false;
            Close();
        }
    }
}
