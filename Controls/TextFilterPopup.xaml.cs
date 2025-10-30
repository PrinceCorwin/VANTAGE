using System;
using System.Windows;
using System.Windows.Controls;

namespace VANTAGE.Controls
{
    public partial class TextFilterPopup : UserControl
    {
        public event EventHandler<FilterEventArgs> FilterApplied;
        private string _columnName;

        public TextFilterPopup()
        {
            InitializeComponent();
            this.Loaded += TextFilterPopup_Loaded;
        }

        private void TextFilterPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure the control uses theme resources for foreground/background
            this.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"];
            this.Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"];

            // Apply Foreground to list items
            foreach (var item in lstFilterType.Items)
            {
                if (item is ListBoxItem lbi)
                {
                    lbi.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"];
                }
            }
        }

        public void Initialize(string columnName)
        {
            _columnName = columnName;
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