using System;
using System.Windows;
using System.Windows.Controls;

namespace VANTAGE.Controls
{
    public partial class ColumnFilterPopup : UserControl
    {
        public event EventHandler<FilterEventArgs> FilterApplied;
        public event EventHandler FilterCleared;

        public ColumnFilterPopup()
        {
            InitializeComponent();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var filterType = (lstFilterType.SelectedItem as ListBoxItem)?.Content.ToString();
            var filterValue = txtFilterValue.Text;

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                MessageBox.Show("Please enter a filter value.", "Filter Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FilterApplied?.Invoke(this, new FilterEventArgs
            {
                FilterType = filterType,
                FilterValue = filterValue
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            txtFilterValue.Clear();
            lstFilterType.SelectedIndex = 0;
            FilterCleared?.Invoke(this, EventArgs.Empty);
        }

        private void cmbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    public class FilterEventArgs : EventArgs
    {
        public string FilterType { get; set; }
        public string FilterValue { get; set; }
    }
}