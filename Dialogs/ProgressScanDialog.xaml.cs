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

        // UniqueIDs of records that were updated - used by caller to filter grid
        public List<string> AppliedUniqueIds { get; } = new();

        // Settings keys for persistence
        private const string SettingDialogWidth = "ProgressScanDialog.Width";
        private const string SettingDialogHeight = "ProgressScanDialog.Height";
        private const string SettingColumnWidths = "ProgressScanDialog.ColumnWidths";

        public ProgressScanDialog()
        {
            InitializeComponent();
            lstFiles.ItemsSource = _files;
            sfReviewGrid.ItemsSource = _reviewItems;

            // Subscribe to review item changes to update selection count
            _reviewItems.CollectionChanged += (s, e) => UpdateSelectionCount();

            // Wire up rescan slider value changed handler
            sliderRescanContrast.ValueChanged += SliderRescanContrast_ValueChanged;

            // Load saved settings and save on close
            Loaded += (s, e) => LoadDialogSettings();
            Closed += (s, e) =>
            {
                SaveDialogSettings();
                _scanService.Dispose();
            };
        }

        // Load dialog size and column widths from user settings
        private void LoadDialogSettings()
        {
            try
            {
                // Load dialog size
                var widthStr = SettingsManager.GetUserSetting(SettingDialogWidth);
                var heightStr = SettingsManager.GetUserSetting(SettingDialogHeight);

                if (double.TryParse(widthStr, out double width) && width >= MinWidth)
                    Width = width;
                if (double.TryParse(heightStr, out double height) && height >= MinHeight)
                    Height = height;

                // Load column widths (format: "col1:width1,col2:width2,...")
                var columnWidthsStr = SettingsManager.GetUserSetting(SettingColumnWidths);
                if (!string.IsNullOrEmpty(columnWidthsStr))
                {
                    var widthDict = columnWidthsStr.Split(',')
                        .Select(s => s.Split(':'))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => double.TryParse(parts[1], out var w) ? w : 0);

                    foreach (var column in sfReviewGrid.Columns)
                    {
                        if (widthDict.TryGetValue(column.MappingName, out double colWidth) && colWidth > 0)
                        {
                            column.Width = colWidth;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressScanDialog.LoadDialogSettings");
            }
        }

        // Save dialog size and column widths to user settings
        private void SaveDialogSettings()
        {
            try
            {
                // Save dialog size (only if not minimized)
                if (WindowState == WindowState.Normal)
                {
                    SettingsManager.SetUserSetting(SettingDialogWidth, Width.ToString("F0"));
                    SettingsManager.SetUserSetting(SettingDialogHeight, Height.ToString("F0"));
                }

                // Save column widths
                var columnWidths = sfReviewGrid.Columns
                    .Select(c => $"{c.MappingName}:{c.ActualWidth:F0}")
                    .ToArray();
                SettingsManager.SetUserSetting(SettingColumnWidths, string.Join(",", columnWidths));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressScanDialog.SaveDialogSettings");
            }
        }

        // Update rescan contrast display when slider changes
        private void SliderRescanContrast_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtRescanContrastValue != null)
                txtRescanContrastValue.Text = e.NewValue.ToString("F1");
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
                    ExtractedPct = extraction.Pct,
                    Confidence = extraction.Confidence
                };

                // Try to match to database by ActivityID (parse extracted string to int)
                if (int.TryParse(extraction.UniqueId, out int activityId) &&
                    activityDict.TryGetValue(activityId, out var activity))
                {
                    reviewItem.MatchedRecord = activity;
                    reviewItem.MatchedUniqueId = activity.UniqueID;
                    reviewItem.Description = activity.Description;
                    reviewItem.CurrentPercent = (decimal)activity.PercentEntry;
                    reviewItem.BudgetMHs = activity.BudgetMHs.ToString("N2");

                    // Set new percent from extraction
                    if (extraction.Pct.HasValue)
                    {
                        reviewItem.NewPercent = extraction.Pct;
                    }

                    // Validate
                    ValidateReviewItem(reviewItem);

                    if (reviewItem.Status == ScanMatchStatus.Ready || reviewItem.Status == ScanMatchStatus.Warning)
                    {
                        // Auto-select if confidence is high enough
                        reviewItem.IsSelected = reviewItem.Confidence >= 90;
                        matchedCount++;
                        if (reviewItem.Status == ScanMatchStatus.Warning)
                            warningCount++;
                    }
                }
                else
                {
                    reviewItem.Status = ScanMatchStatus.NotFound;
                    reviewItem.ValidationMessage = "Not found in your records";
                    reviewItem.Description = "(not found)";
                    reviewItem.BudgetMHs = "NOT FOUND";
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
            // No percent extracted - can't apply
            if (!item.NewPercent.HasValue)
            {
                item.Status = ScanMatchStatus.Error;
                item.ValidationMessage = "No % value extracted";
                return;
            }

            // Invalid percent
            if (item.NewPercent > 100)
            {
                item.Status = ScanMatchStatus.Error;
                item.ValidationMessage = "% cannot exceed 100";
                return;
            }

            var warnings = new List<string>();

            // Low confidence
            if (item.Confidence < 70)
            {
                warnings.Add("Low confidence");
            }

            // Progress decrease
            if (item.CurrentPercent.HasValue && item.NewPercent < item.CurrentPercent)
            {
                warnings.Add("% decreased");
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

        // Cell edit completed - update selection count when checkbox or NewPercent changes
        private void SfReviewGrid_CurrentCellEndEdit(object sender, Syncfusion.UI.Xaml.Grid.CurrentCellEndEditEventArgs e)
        {
            UpdateSelectionCount();
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

        // Select all items
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _reviewItems)
            {
                item.IsSelected = true;
            }
            sfReviewGrid.View?.RefreshFilter();
            UpdateSelectionCount();
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

        // Rescan with new contrast setting
        private async void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            // Update contrast from rescan slider
            _scanService.ContrastFactor = (float)sliderRescanContrast.Value;

            // Clear current results and switch to processing panel
            _reviewItems.Clear();
            panelReview.Visibility = Visibility.Collapsed;
            panelProcessing.Visibility = Visibility.Visible;

            // Re-run processing with same files
            _cancellationTokenSource = new CancellationTokenSource();
            var filePaths = _files.Select(f => f.FilePath).ToList();

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
                    // User cancelled - go back to review
                    panelProcessing.Visibility = Visibility.Collapsed;
                    panelReview.Visibility = Visibility.Visible;
                    return;
                }

                // Process results and show review
                await ProcessResultsAsync(result);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressScanDialog.BtnRescan_Click");
                MessageBox.Show($"Error during rescan: {ex.Message}",
                    "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);

                panelProcessing.Visibility = Visibility.Collapsed;
                panelReview.Visibility = Visibility.Visible;
            }
        }

        // Apply selected updates
        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Get ALL selected items - user's selection is final
            var selectedItems = _reviewItems.Where(i => i.IsSelected).ToList();

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
            int skipCount = 0;
            var skippedReasons = new List<string>();

            foreach (var item in selectedItems)
            {
                // Check for issues and report them
                if (item.MatchedRecord == null)
                {
                    skipCount++;
                    skippedReasons.Add($"ID {item.ExtractedUniqueId}: No matching record in database");
                    continue;
                }

                if (!item.NewPercent.HasValue)
                {
                    skipCount++;
                    skippedReasons.Add($"ID {item.ExtractedUniqueId}: No percent value to apply");
                    continue;
                }

                try
                {
                    var activity = item.MatchedRecord;

                    // Update the activity (setter triggers EarnQtyEntry recalculation)
                    activity.PercentEntry = (double)item.NewPercent.Value;
                    activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                    activity.UpdatedUtcDate = DateTime.UtcNow;
                    activity.LocalDirty = 1;

                    await ActivityRepository.UpdateActivityInDatabase(activity);
                    AppliedUniqueIds.Add(activity.UniqueID);
                    successCount++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressScanDialog.BtnApply_Click");
                    skippedReasons.Add($"ID {item.ExtractedUniqueId}: Database error - {ex.Message}");
                    skipCount++;
                }
            }

            AppLogger.Info($"Progress scan applied: {successCount} records updated, {skipCount} skipped",
                "ProgressScanDialog.BtnApply_Click", App.CurrentUser?.Username);

            if (skipCount > 0)
            {
                string reasons = string.Join("\n", skippedReasons.Take(10)); // Show first 10
                if (skippedReasons.Count > 10)
                    reasons += $"\n... and {skippedReasons.Count - 10} more";

                MessageBox.Show($"Updated {successCount} records.\n\n{skipCount} records skipped:\n{reasons}",
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
