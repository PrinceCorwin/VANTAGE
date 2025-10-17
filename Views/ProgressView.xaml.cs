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

            InitializeColumnVisibility();  // ← PUT THIS BACK
            UpdateRecordCount();

            // Load data AFTER the view is loaded
            this.Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            // REMOVE InitializeColumnVisibility() from here

            await _viewModel.LoadInitialDataAsync();
            UpdateRecordCount();
            UpdatePagingControls();
        }

        private void InitializeColumnVisibility()
        {
            // Build dictionary of existing columns first
            foreach (var column in dgActivities.Columns)
            {
                string headerText = column.Header?.ToString() ?? "Unknown";
                _columnMap[headerText] = column;
            }

            // Hardcoded list of ALL columns (in order they should appear)
            var allColumns = new List<string>
    {
        "Activity ID",
        "Description",
        "Component Type",
        "Phase Category",
        "ROC Step",
        "Project ID",
        "Work Package",
        "Phase Code",
        "Drawing No",
        "Sch Start",
        "Sch Finish",
        "Sch Status",
        "Activity No",
        "Quantity",
        "Earned Qty",
        "% Complete",
        "Budget Hrs",
        "Earned Hrs",
        "Status",
        "UDF One",
        "UDF Two",
        "Assigned To",
        "Hex NO",
        "Revision No",
        "Secondary Drawing",
        "Sheet No",
        "Comments",
        "Sch Act No (Field)",
        "Tag Aux1",
        "Tag Aux2",
        "Tag Aux3",
        "Tag Area",
        "CO No",
        "Equipment No",
        "Estimator",
        "Insulation Type",
        "Line No",
        "Material Spec",
        "Paint Code",
        "Pipe Grade",
        "RFI No",
        "Service",
        "Shop/Field",
        "Sub Area",
        "System",
        "System No",
        "Tag No",
        "Tracing",
        "XRAY",
        "Date Trigger",
        "UDF Three",
        "UDF Four",
        "UDF Five",
        "UDF Six",
        "UDF Seven",
        "UDF Eight",
        "UDF Nine",
        "UDF Ten",
        "Last Modified By",
        "Created By",
        "UDF Fourteen",
        "UDF Fifteen",
        "UDF Sixteen",
        "UDF Seventeen",
        "UDF Eighteen",
        "UDF Twenty",
        "Base Unit",
        "Budget Hrs Group",
        "Budget Hrs ROC",
        "Earned Hrs ROC",
        "UOM",
        "Earn Qty (Calc)",
        "Percent Earned",
        "EQ QTY",
        "EQ UOM",
        "ROC ID",
        "Lookup ROC ID",
        "ROC Perc",
        "ROC Budget Qty",
        "Pipe Size 1",
        "Pipe Size 2",
        "Prev Earned Hrs",
        "Prev Earned Qty",
        "Timestamp",
        "Client EQ QTY Budget",
        "UDF Two (Val)",
        "UDF Three (Val)",
        "Client Earned EQ QTY"
    };

            // Add checkboxes for ALL columns
            foreach (var columnName in allColumns)
            {
                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = _columnMap.ContainsKey(columnName) && _columnMap[columnName].Visibility == Visibility.Visible,
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }

            System.Diagnostics.Debug.WriteLine($"→ Total checkboxes added: {lstColumnVisibility.Items.Count}");
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