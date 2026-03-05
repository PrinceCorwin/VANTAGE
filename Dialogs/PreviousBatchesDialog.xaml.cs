using System.Windows;
using System.Windows.Controls;
using VANTAGE.Services.AI;

namespace VANTAGE.Dialogs
{
    // Display wrapper for BatchInfo in the ListView
    public class BatchDisplayItem
    {
        public string BatchId { get; set; } = string.Empty;
        public string ConfigName { get; set; } = "—";
        public string DateDisplay { get; set; } = "Unknown";
        public string DrawingDisplay { get; set; } = "—";
        public string? Username { get; set; }
        public string StatusDisplay { get; set; } = "";
        public bool IsComplete { get; set; }
    }

    public partial class PreviousBatchesDialog : Window
    {
        // The batch ID the user selected for download
        public string? SelectedBatchId { get; private set; }

        public PreviousBatchesDialog(List<BatchInfo> batches)
        {
            InitializeComponent();

            var items = batches.Select(b => new BatchDisplayItem
            {
                BatchId = b.BatchId,
                ConfigName = b.ConfigName ?? "—",
                DateDisplay = b.SubmittedAt?.ToString("MMM d, yyyy h:mm tt") ?? "Unknown",
                DrawingDisplay = b.DrawingCount.HasValue ? b.DrawingCount.ToString()! : "—",
                Username = b.Username ?? "—",
                StatusDisplay = b.IsComplete ? "Complete" : "Failed",
                IsComplete = b.IsComplete
            }).ToList();

            lstBatches.ItemsSource = items;
            txtStatus.Text = $"{batches.Count} batch(es) found.";
        }

        private void LstBatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem item)
                btnDownload.IsEnabled = item.IsComplete;
            else
                btnDownload.IsEnabled = false;
        }

        private void LstBatches_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem item && item.IsComplete)
            {
                SelectedBatchId = item.BatchId;
                DialogResult = true;
                Close();
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem item && item.IsComplete)
            {
                SelectedBatchId = item.BatchId;
                DialogResult = true;
                Close();
            }
        }
    }
}
