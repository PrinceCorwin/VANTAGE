using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Services.AI;

namespace VANTAGE.Dialogs
{
    public partial class SelectBatchDialog : Window
    {
        // The batch ID the user selected
        public string? SelectedBatchId { get; private set; }

        public SelectBatchDialog(List<BatchInfo> batches)
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
        }

        private void LstBatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only allow selecting complete batches
            btnOk.IsEnabled = lstBatches.SelectedItem is BatchDisplayItem { IsComplete: true };
        }

        private void LstBatches_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem { IsComplete: true } item)
            {
                SelectedBatchId = item.BatchId;
                DialogResult = true;
                Close();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (lstBatches.SelectedItem is BatchDisplayItem { IsComplete: true } item)
            {
                SelectedBatchId = item.BatchId;
                DialogResult = true;
                Close();
            }
        }
    }
}
