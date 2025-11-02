using System;
using System.Windows;
using System.Windows.Controls;

namespace VANTAGE.Controls
{
    public partial class TextFilterPopup : UserControl
    {
        public event EventHandler<FilterEventArgs> FilterApplied;
        private string _columnName;
        private string _columnType = "text"; // text, number, date

        public TextFilterPopup()
        {
            InitializeComponent();
            this.Loaded += TextFilterPopup_Loaded;
            lstNumberFilterType.SelectionChanged += LstNumberFilterType_SelectionChanged;
            lstDateFilterType.SelectionChanged += LstDateFilterType_SelectionChanged;
        }

        private void TextFilterPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure the control uses theme resources for foreground/background
            this.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"];
            this.Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"];
        }

        public void Initialize(string columnName)
        {
            _columnName = columnName;
            InferColumnType(columnName);
            ShowCorrectPanel();
        }

        private void InferColumnType(string columnName)
        {
            // Known numeric columns
            var numericColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Quantity", "EarnQtyEntry", "PercentEntry", "PercentEntry_Display", "BudgetMHs", "EarnMHsCalc", "ROCPercent", "ROCBudgetQTY", "PipeSize1", "PipeSize2", "PrevEarnMHs", "PrevEarnQTY", "ClientEquivQty", "ClientBudget", "ClientCustom3", "XRay", "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "EarnedMHsRoc", "EquivQTY", "ROCID", "HexNO"
            };
            // Known date columns (must match DataGrid property names)
            var dateColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SchStart", "SchFinish", "ProgDate", "WeekEndDate", "AzureUploadDate"
            };
            if (dateColumns.Contains(columnName))
                _columnType = "date";
            else if (numericColumns.Contains(columnName))
                _columnType = "number";
            else
                _columnType = "text";
        }

        private void ShowCorrectPanel()
        {
            panelTextFilters.Visibility = _columnType == "text" ? Visibility.Visible : Visibility.Collapsed;
            panelNumberFilters.Visibility = _columnType == "number" ? Visibility.Visible : Visibility.Collapsed;
            panelDateFilters.Visibility = _columnType == "date" ? Visibility.Visible : Visibility.Collapsed;
            lblFilterTitle.Text = _columnType switch
            {
                "number" => "Number Filters",
                "date" => "Date Filters",
                _ => "Text Filters"
            };
        }

        private void LstNumberFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (lstNumberFilterType.SelectedItem as ListBoxItem)?.Content?.ToString();
            bool isBetween = selected == "Between";
            lblAndNumber.Visibility = isBetween ? Visibility.Visible : Visibility.Collapsed;
            txtNumberValue2.Visibility = isBetween ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LstDateFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (lstDateFilterType.SelectedItem as ListBoxItem)?.Content?.ToString();
            bool isBetween = selected == "Between";
            lblAndDate.Visibility = isBetween ? Visibility.Visible : Visibility.Collapsed;
            dateValue2.Visibility = isBetween ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            string filterType = null;
            string filterValue = null;
            if (_columnType == "text")
            {
                filterType = (lstTextFilterType.SelectedItem as ListBoxItem)?.Content?.ToString();
                if (filterType == "Is Blank" || filterType == "Is Not Blank")
                {
                    filterValue = null;
                }
                else
                {
                    filterValue = txtTextFilterValue.Text;
                    if (string.IsNullOrWhiteSpace(filterValue))
                    {
                        MessageBox.Show("Please enter a filter value.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }
            else if (_columnType == "number")
            {
                filterType = (lstNumberFilterType.SelectedItem as ListBoxItem)?.Content?.ToString();
                if (filterType == "Is Blank" || filterType == "Is Not Blank")
                {
                    filterValue = null;
                }
                else if (filterType == "Between")
                {
                    var val1 = txtNumberValue1.Text;
                    var val2 = txtNumberValue2.Text;
                    if (string.IsNullOrWhiteSpace(val1) || string.IsNullOrWhiteSpace(val2))
                    {
                        MessageBox.Show("Please enter both values for 'Between'.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    filterValue = val1 + "," + val2;
                }
                else
                {
                    var val1 = txtNumberValue1.Text;
                    if (string.IsNullOrWhiteSpace(val1))
                    {
                        MessageBox.Show("Please enter a value.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    filterValue = val1;
                }
            }
            else if (_columnType == "date")
            {
                filterType = (lstDateFilterType.SelectedItem as ListBoxItem)?.Content?.ToString();
                if (filterType == "Is Blank" || filterType == "Is Not Blank")
                {
                    filterValue = null;
                }
                else if (filterType == "Between")
                {
                    var date1 = dateValue1.SelectedDate;
                    var date2 = dateValue2.SelectedDate;
                    if (!date1.HasValue || !date2.HasValue)
                    {
                        MessageBox.Show("Please select both dates for 'Between'.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    filterValue = date1.Value.ToString("yyyy-MM-dd") + "," + date2.Value.ToString("yyyy-MM-dd");
                }
                else
                {
                    var date1 = dateValue1.SelectedDate;
                    if (!date1.HasValue)
                    {
                        MessageBox.Show("Please select a date.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    filterValue = date1.Value.ToString("yyyy-MM-dd");
                }
            }
            FilterApplied?.Invoke(this, new FilterEventArgs
            {
                FilterType = filterType,
                FilterValue = filterValue
            });
            // Close parent window if present
            var win = Window.GetWindow(this);
            win?.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            win?.Close();
        }
    }
}