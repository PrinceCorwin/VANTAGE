using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageDrawingsDialog : Window
    {
        private readonly List<(string Key, string DisplayName)> _configs;
        private TakeoffService? _service;
        private List<S3DrawingItem> _drawings = new();

        public ManageDrawingsDialog(
            List<(string Key, string DisplayName)> configs,
            int selectedConfigIndex = 0)
        {
            InitializeComponent();

            _configs = configs;

            foreach (var config in _configs)
                cboConfig.Items.Add(config.DisplayName);

            if (selectedConfigIndex >= 0 && selectedConfigIndex < _configs.Count)
                cboConfig.SelectedIndex = selectedConfigIndex;

            lstDrawings.SelectionChanged += LstDrawings_SelectionChanged;
        }

        private void LstDrawings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnDelete.IsEnabled = lstDrawings.SelectedItems.Count > 0;
        }

        private async void CboConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadDrawingsAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDrawingsAsync();
        }

        private async System.Threading.Tasks.Task LoadDrawingsAsync()
        {
            if (cboConfig.SelectedIndex < 0)
                return;

            string configKey = _configs[cboConfig.SelectedIndex].Key;
            string prefix = TakeoffService.GetDrawingPrefix(configKey);

            txtStatus.Text = "Loading drawings...";
            lstDrawings.ItemsSource = null;
            btnDelete.IsEnabled = false;

            try
            {
                _service?.Dispose();
                _service = new TakeoffService();

                var results = await _service.ListDrawingsAsync(prefix);

                _drawings = results
                    .OrderBy(r => r.FileName)
                    .Select(r => new S3DrawingItem
                    {
                        Key = r.Key,
                        FileName = r.FileName,
                        SizeBytes = r.Size,
                        LastModified = r.LastModified
                    })
                    .ToList();

                lstDrawings.ItemsSource = _drawings;

                long totalBytes = _drawings.Sum(d => d.SizeBytes);
                txtSubtitle.Text = $"Prefix: {prefix}/";
                txtStatus.Text = $"{_drawings.Count} drawing(s), {FormatSize(totalBytes)} total";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageDrawingsDialog.LoadDrawingsAsync");
                txtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstDrawings.SelectedItems.Cast<S3DrawingItem>().ToList();
            if (selected.Count == 0)
                return;

            string message = selected.Count == 1
                ? $"Delete \"{selected[0].FileName}\" from S3?\n\nThis cannot be undone."
                : $"Delete {selected.Count} drawings from S3?\n\nThis cannot be undone.";

            if (AppMessageBox.Show(message, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            txtStatus.Text = $"Deleting {selected.Count} drawing(s)...";

            try
            {
                if (_service == null)
                {
                    _service = new TakeoffService();
                }

                var keys = selected.Select(d => d.Key).ToList();
                await _service.DeleteDrawingsAsync(keys);

                AppLogger.Info($"Deleted {keys.Count} drawing(s) from S3",
                    "ManageDrawingsDialog.BtnDelete_Click", App.CurrentUser?.Username);

                await LoadDrawingsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageDrawingsDialog.BtnDelete_Click");
                txtStatus.Text = $"Delete error: {ex.Message}";
                btnDelete.IsEnabled = lstDrawings.SelectedItems.Count > 0;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _service?.Dispose();
            Close();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    // View model for drawing list items
    public class S3DrawingItem
    {
        public string Key { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }

        public string SizeDisplay => FormatSize(SizeBytes);
        public string LastModifiedDisplay => LastModified == DateTime.MinValue
            ? ""
            : LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
