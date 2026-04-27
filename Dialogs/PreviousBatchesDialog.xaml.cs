using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Display wrapper for BatchInfo in the ListView
    public class BatchDisplayItem
    {
        public string BatchId { get; set; } = string.Empty;
        public string BatchName { get; set; } = "—";
        public string ConfigName { get; set; } = "—";
        public string DateDisplay { get; set; } = "Unknown";
        public string DrawingDisplay { get; set; } = "—";
        public string? Username { get; set; }
        public string StatusDisplay { get; set; } = "";
        public bool IsComplete { get; set; }
    }

    public partial class PreviousBatchesDialog : Window
    {
        // The batch ID and name the user selected for download
        public string? SelectedBatchId { get; private set; }
        public string? SelectedBatchName { get; private set; }

        public PreviousBatchesDialog(List<BatchInfo> batches)
        {
            InitializeComponent();

            var items = batches.Select(b => new BatchDisplayItem
            {
                BatchId = b.BatchId,
                BatchName = b.BatchName ?? b.BatchId,
                ConfigName = b.ConfigName ?? "—",
                DateDisplay = b.SubmittedAt?.ToString("MMM d, yyyy h:mm tt") ?? "Unknown",
                DrawingDisplay = b.DrawingCount.HasValue ? b.DrawingCount.ToString()! : "—",
                Username = b.Username ?? "—",
                StatusDisplay = b.IsComplete ? "Complete" : "Failed",
                IsComplete = b.IsComplete
            }).ToList();

            lstBatches.ItemsSource = items;
            txtStatus.Text = $"{batches.Count} batch(es) found.";

            // Show delete buttons for admins
            if (App.CurrentUser != null && AzureDbManager.IsUserAdmin(App.CurrentUser.Username))
            {
                pnlAdminButtons.Visibility = Visibility.Visible;
                btnDeleteAll.IsEnabled = items.Count > 0;
            }
        }

        private void LstBatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = lstBatches.SelectedItem is BatchDisplayItem item;
            btnDownload.IsEnabled = hasSelection && ((BatchDisplayItem)lstBatches.SelectedItem!).IsComplete;
            btnDeleteSelected.IsEnabled = hasSelection;
            btnRename.IsEnabled = hasSelection;
        }

        private void LstBatches_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem item && item.IsComplete)
            {
                SelectedBatchId = item.BatchId;
                SelectedBatchName = item.BatchName;
                DialogResult = true;
                Close();
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem item && item.IsComplete)
            {
                SelectedBatchId = item.BatchId;
                SelectedBatchName = item.BatchName;
                DialogResult = true;
                Close();
            }
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lstBatches.SelectedItem is not BatchDisplayItem item) return;

            var result = AppMessageBox.Show(
                $"Delete batch '{item.ConfigName}' from {item.DateDisplay}?\n\nThis cannot be undone.",
                "Delete Batch", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                SetButtonsEnabled(false);
                txtStatus.Text = "Deleting batch...";

                using var service = new TakeoffService();
                await service.DeleteBatchAsync(item.BatchId);

                // Remove from list
                var items = (List<BatchDisplayItem>)lstBatches.ItemsSource;
                items.Remove(item);
                lstBatches.ItemsSource = null;
                lstBatches.ItemsSource = items;
                txtStatus.Text = $"{items.Count} batch(es) found.";
                btnDeleteAll.IsEnabled = items.Count > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PreviousBatchesDialog.BtnDeleteSelected_Click");
                txtStatus.Text = $"Delete failed: {ex.Message}";
            }
            finally
            {
                SetButtonsEnabled(true);
                btnDeleteSelected.IsEnabled = lstBatches.SelectedItem != null;
            }
        }

        private async void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            var items = lstBatches.ItemsSource as List<BatchDisplayItem>;
            if (items == null || items.Count == 0) return;

            var result = AppMessageBox.Show(
                $"Delete ALL {items.Count} batches?\n\nThis cannot be undone.",
                "Delete All Batches", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                SetButtonsEnabled(false);

                using var service = new TakeoffService();
                int total = items.Count;

                for (int i = 0; i < total; i++)
                {
                    txtStatus.Text = $"Deleting batch {i + 1} of {total}...";
                    await service.DeleteBatchAsync(items[i].BatchId);
                }

                items.Clear();
                lstBatches.ItemsSource = null;
                lstBatches.ItemsSource = items;
                txtStatus.Text = "All batches deleted.";
                btnDeleteAll.IsEnabled = false;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PreviousBatchesDialog.BtnDeleteAll_Click");
                txtStatus.Text = $"Delete failed: {ex.Message}";
            }
            finally
            {
                SetButtonsEnabled(true);
                btnDeleteSelected.IsEnabled = lstBatches.SelectedItem != null;
            }
        }

        private async void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (lstBatches.SelectedItem is not BatchDisplayItem item) return;

            // Prompt for new name
            var inputDialog = new InputDialog(
                "Rename Batch",
                "Enter a new name for this batch:",
                item.BatchName);

            if (inputDialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(inputDialog.InputText))
                return;

            string newName = inputDialog.InputText.Trim();
            if (newName == item.BatchName) return;

            // Validate batch name characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(newName, @"^[a-zA-Z0-9\-_]+$"))
            {
                AppMessageBox.Show(
                    "Batch name can only contain letters, numbers, hyphens, and underscores (no spaces or special characters).",
                    "Invalid Batch Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetButtonsEnabled(false);
                txtStatus.Text = "Renaming batch...";

                using var service = new TakeoffService();
                await service.RenameBatchAsync(item.BatchId, newName);

                // Update display
                item.BatchName = newName;
                lstBatches.Items.Refresh();
                txtStatus.Text = $"Batch renamed to '{newName}'.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PreviousBatchesDialog.BtnRename_Click");
                txtStatus.Text = $"Rename failed: {ex.Message}";
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        // Enable/disable all action buttons during async operations
        private void SetButtonsEnabled(bool enabled)
        {
            btnDownload.IsEnabled = enabled && lstBatches.SelectedItem is BatchDisplayItem { IsComplete: true };
            btnDeleteSelected.IsEnabled = enabled && lstBatches.SelectedItem != null;
            btnDeleteAll.IsEnabled = enabled && lstBatches.Items.Count > 0;
            btnRename.IsEnabled = enabled && lstBatches.SelectedItem != null;
        }
    }
}
