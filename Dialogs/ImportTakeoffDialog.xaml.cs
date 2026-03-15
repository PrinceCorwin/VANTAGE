using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ImportTakeoffDialog : Window
    {
        private List<(string ProjectID, string SetName)> _rocSets = new();
        private string? _selectedFilePath;
        private string? _selectedBatchId;
        private bool _isLoadingDropdowns;

        public ImportTakeoffDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ImportTakeoffDialog_Loaded;
        }

        private async void ImportTakeoffDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadROCSetsAsync();
        }

        // Load ROC sets into the dropdown
        private async System.Threading.Tasks.Task LoadROCSetsAsync()
        {
            try
            {
                _isLoadingDropdowns = true;
                _rocSets = await ProjectRateRepository.GetROCSetsAsync();

                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                foreach (var (projectId, setName) in _rocSets)
                    cboROCSet.Items.Add($"{projectId} - {setName}");

                cboROCSet.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.LoadROCSetsAsync");
                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                cboROCSet.SelectedIndex = 1;
            }
            finally
            {
                _isLoadingDropdowns = false;
            }
        }

        // Toggle button enabled states based on radio selection
        private void RbSource_Checked(object sender, RoutedEventArgs e)
        {
            if (btnSelectFile == null || btnBatches == null) return;

            bool isFile = rbFromFile.IsChecked == true;
            btnSelectFile.IsEnabled = isFile;
            btnBatches.IsEnabled = !isFile;

            // Clear previous selection when switching
            _selectedFilePath = null;
            _selectedBatchId = null;
            txtSourceLabel.Text = isFile ? "No file selected" : "No batch selected";
            txtSourceLabel.Opacity = 0.6;
        }

        // Open file picker for .xlsx
        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Takeoff Excel File",
                Filter = "Excel Files|*.xlsx|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            _selectedFilePath = dialog.FileName;
            _selectedBatchId = null;
            txtSourceLabel.Text = System.IO.Path.GetFileName(_selectedFilePath);
            txtSourceLabel.Opacity = 1.0;
        }

        // Open SelectBatchDialog to pick a batch
        private async void BtnBatches_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var service = new TakeoffService();
                var batches = await service.ListBatchesAsync();

                if (batches.Count == 0)
                {
                    MessageBox.Show("No previous batches found.", "No Batches",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SelectBatchDialog(batches)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true && dialog.SelectedBatchId != null)
                {
                    _selectedBatchId = dialog.SelectedBatchId;
                    _selectedFilePath = null;
                    txtSourceLabel.Text = $"Batch: {_selectedBatchId}";
                    txtSourceLabel.Opacity = 1.0;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.BtnBatches_Click");
                MessageBox.Show($"Error loading batches: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Handle ROC Set dropdown — "+ Create New..." opens the manager
        private async void CboROCSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingDropdowns || cboROCSet.SelectedIndex != 0) return;

            // Reset selection before opening dialog
            _isLoadingDropdowns = true;
            cboROCSet.SelectedIndex = 1;
            _isLoadingDropdowns = false;

            var dialog = new ManageROCRatesDialog
            {
                Owner = this
            };
            dialog.ShowDialog();

            // Reload sets in case new ones were created
            await LoadROCSetsAsync();
        }

        // Import button — no functionality yet, will be implemented next
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
