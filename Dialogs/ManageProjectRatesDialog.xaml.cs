using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageProjectRatesDialog : Window
    {
        private ObservableCollection<ProjectRateSetDisplay> _sets = new();
        private bool _isAdmin;

        public ManageProjectRatesDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _isAdmin = AzureDbManager.IsUserAdmin(App.CurrentUser?.Username ?? "");

            if (!_isAdmin)
            {
                btnUpload.IsEnabled = false;
                btnDelete.IsEnabled = false;
            }

            Loaded += ManageProjectRatesDialog_Loaded;
        }

        private async void ManageProjectRatesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSetsAsync();
        }

        private async System.Threading.Tasks.Task LoadSetsAsync()
        {
            try
            {
                SetStatus("Loading rate sets...");
                var sets = await ProjectRateRepository.GetRateSetsAsync();

                _sets = new ObservableCollection<ProjectRateSetDisplay>();
                foreach (var s in sets)
                {
                    _sets.Add(new ProjectRateSetDisplay
                    {
                        ProjectID = s.ProjectID,
                        SetName = s.SetName,
                        RowCount = s.RowCount,
                        CreatedBy = s.CreatedBy,
                        CreatedDateDisplay = s.CreatedDate.ToLocalTime().ToString("yyyy-MM-dd h:mm tt"),
                        UpdatedBy = s.UpdatedBy,
                        UpdatedDateDisplay = s.UpdatedDate.ToLocalTime().ToString("yyyy-MM-dd h:mm tt")
                    });
                }

                sfGrid.ItemsSource = _sets;
                SetStatus($"{_sets.Count} rate set(s)");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProjectRatesDialog.LoadSetsAsync");
                SetStatus($"Error: {ex.Message}");
            }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool uploaded = await ProjectRateUploader.UploadAsync(this);
                if (uploaded)
                    await LoadSetsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProjectRatesDialog.BtnUpload_Click");
                AppMessageBox.Show($"Upload error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sfGrid.SelectedItem is not ProjectRateSetDisplay selected)
            {
                AppMessageBox.Show("Select a rate set to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = AppMessageBox.Show(
                $"Delete rate set '{selected.SetName}' for project '{selected.ProjectID}'?\n\n({selected.RowCount} rate(s) will be removed)\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string username = App.CurrentUser?.Username ?? "Unknown";
                await ProjectRateRepository.DeleteRateSetAsync(selected.ProjectID, selected.SetName);
                AppLogger.Info($"Deleted project rate set '{selected.SetName}' for '{selected.ProjectID}'",
                    "ManageProjectRatesDialog.BtnDelete_Click", username);
                await LoadSetsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProjectRatesDialog.BtnDelete_Click");
                AppMessageBox.Show($"Error deleting: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "RateSheet_Template.xlsx",
                Filter = "Excel Files|*.xlsx",
                Title = "Save Rate Sheet Template"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                using var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Rates");

                // Headers
                ws.Cell(1, 1).Value = "Item";
                ws.Cell(1, 2).Value = "Size";
                ws.Cell(1, 3).Value = "Sch-Class";
                ws.Cell(1, 4).Value = "Unit";
                ws.Cell(1, 5).Value = "MH";

                // Style header row
                var headerRange = ws.Range(1, 1, 1, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#DAEEF3");

                // Example row
                ws.Cell(2, 1).Value = "BW";
                ws.Cell(2, 2).Value = 2;
                ws.Cell(2, 3).Value = "S40";
                ws.Cell(2, 4).Value = "EA";
                ws.Cell(2, 5).Value = 0.5;

                ws.Columns().AdjustToContents();
                wb.SaveAs(dialog.FileName);

                SetStatus("Template exported");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageProjectRatesDialog.BtnExportTemplate_Click");
                AppMessageBox.Show($"Error exporting template: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetStatus(string message)
        {
            txtStatus.Text = message;
        }
    }

    // Display model for the rate set grid
    public class ProjectRateSetDisplay
    {
        public string ProjectID { get; set; } = "";
        public string SetName { get; set; } = "";
        public int RowCount { get; set; }
        public string CreatedBy { get; set; } = "";
        public string CreatedDateDisplay { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public string UpdatedDateDisplay { get; set; } = "";
    }
}
