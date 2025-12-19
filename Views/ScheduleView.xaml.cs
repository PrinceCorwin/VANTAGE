using System;
using System.Windows;
using System.Windows.Controls;
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
    }
}