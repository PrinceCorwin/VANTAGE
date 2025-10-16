using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VANTAGE.Views
{
    public partial class ProgressView : UserControl
    {
        // Track which columns are visible
        private Dictionary<string, DataGridColumn> _columnMap = new Dictionary<string, DataGridColumn>();

        public ProgressView()
        {
            InitializeComponent();
            InitializeColumnVisibility();
        }

        /// <summary>
        /// Populate the Column Visibility ListBox with all available columns
        /// </summary>
        private void InitializeColumnVisibility()
        {
            // Build dictionary of all columns
            foreach (var column in dgActivities.Columns)
            {
                string headerText = column.Header?.ToString() ?? "Unknown";
                _columnMap[headerText] = column;
            }

            // Populate ListBox with column names
            foreach (var columnName in _columnMap.Keys)
            {
                var column = _columnMap[columnName];

                var checkBox = new CheckBox
                {
                    Content = columnName,
                    IsChecked = column.Visibility == Visibility.Visible, // Match actual visibility
                    Margin = new Thickness(5, 2, 5, 2),
                    Foreground = System.Windows.Media.Brushes.White
                };

                checkBox.Checked += ColumnCheckBox_Changed;
                checkBox.Unchecked += ColumnCheckBox_Changed;

                lstColumnVisibility.Items.Add(checkBox);
            }
        }

        /// <summary>
        /// Handle column visibility changes
        /// </summary>
        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            string columnName = checkBox.Content?.ToString();
            if (string.IsNullOrEmpty(columnName) || !_columnMap.ContainsKey(columnName))
                return;

            var column = _columnMap[columnName];

            if (checkBox.IsChecked == true)
            {
                column.Visibility = Visibility.Visible;
            }
            else
            {
                column.Visibility = Visibility.Collapsed;
            }
        }

        // Event handlers (we'll implement these later)
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
            // TODO: Implement
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
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