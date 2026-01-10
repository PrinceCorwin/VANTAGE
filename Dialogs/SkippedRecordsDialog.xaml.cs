using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class SkippedRecordsDialog : Window
    {
        private readonly List<SkippedRecordItem> _records;

        public SkippedRecordsDialog(List<SkippedRecordItem> records)
        {
            InitializeComponent();
            _records = records;

            lvSkippedRecords.ItemsSource = _records;
            txtSummary.Text = $"{_records.Count} record(s) could not be restored:";
        }

        private void BtnCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("UniqueID\tReason");

            foreach (var record in _records)
            {
                sb.AppendLine($"{record.UniqueID}\t{record.Reason}");
            }

            Clipboard.SetText(sb.ToString());

            MessageBox.Show(
                $"Copied {_records.Count} records to clipboard.",
                "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Model for skipped record display
    public class SkippedRecordItem
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
