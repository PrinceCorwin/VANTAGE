using System;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Repositories;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class ScheduleView : UserControl
    {
        private readonly ScheduleViewModel _viewModel;

        public ScheduleView()
        {
            InitializeComponent();

            _viewModel = new ScheduleViewModel();
            DataContext = _viewModel;

            Loaded += ScheduleView_Loaded;
        }

        private async void ScheduleView_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel.MasterRows == null || _viewModel.MasterRows.Count == 0)
                {
                    MessageBox.Show("No data to save.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                btnSave.IsEnabled = false;
                txtStatus.Text = "Saving...";

                string username = App.CurrentUser?.Username ?? "Unknown";
                int savedCount = await ScheduleRepository.SaveAllScheduleRowsAsync(_viewModel.MasterRows, username);

                txtStatus.Text = $"Saved {savedCount} rows";
                AppLogger.Info($"Saved {savedCount} schedule rows", "ScheduleView.btnSave_Click", username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleView.btnSave_Click");
                MessageBox.Show($"Error saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Save failed";
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }
    }
}