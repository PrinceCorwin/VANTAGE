using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        private Dictionary<string, DataGridColumn> _columnMap = new Dictionary<string, DataGridColumn>();
        private ProgressViewModel _viewModel;

        public ProgressView()
        {
            InitializeComponent();

            _viewModel = new ProgressViewModel();
            this.DataContext = _viewModel;

            dgActivities.ItemsSource = _viewModel.ActivitiesView;

            InitializeColumnVisibility();
            UpdateRecordCount();

            // Load data AFTER the view is loaded
            this.Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private void InitializeColumnVisibility()
        {
            foreach (var column in dgActivities.Columns)
            {
                string headerText = column.Header?.ToString() ?? "Unknown";
                _columnMap[headerText] = column;
            }

            foreach (var columnName in _columnMap.Keys)
            {
                var column = _columnMap[columnName];

                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = column.Visibility == Visibility.Visible,
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }
        }

        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            string columnName = checkBox.Content?.ToString();
            if (string.IsNullOrEmpty(columnName) || !_columnMap.ContainsKey(columnName))
                return;

            var column = _columnMap[columnName];
            column.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRecordCount()
        {
            txtFilteredCount.Text = $"{_viewModel.FilteredCount} of {_viewModel.PageSize} records (Total: {_viewModel.TotalRecordCount})";
        }

        private void UpdatePagingControls()
        {
            txtPageInfo.Text = _viewModel.CurrentPageDisplay;
            btnFirstPage.IsEnabled = _viewModel.CanGoPrevious;
            btnPreviousPage.IsEnabled = _viewModel.CanGoPrevious;
            btnNextPage.IsEnabled = _viewModel.CanGoNext;
            btnLastPage.IsEnabled = _viewModel.CanGoNext;
        }

        // === PAGING EVENT HANDLERS ===

        private async void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.FirstPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.PreviousPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.NextPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private async void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LastPageAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        // === FILTER EVENT HANDLERS ===

        private void BtnFilterComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterNotComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterPipe_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterSteel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterElec_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnFilterMyRecords_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void LstColumnVisibility_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Not needed - using CheckBox events instead
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.SearchText = txtSearch.Text;
            UpdateRecordCount();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private void DgActivities_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // TODO: Implement auto-save
        }

        private void DgActivities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuAssignToMe_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuAssignToUser_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuMarkComplete_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuMarkNotStarted_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }

        private void MenuUpdatePercent_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
        }
    }
}