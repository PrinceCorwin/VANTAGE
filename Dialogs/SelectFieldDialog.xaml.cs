using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VANTAGE.Dialogs
{
    public partial class SelectFieldDialog : Window
    {
        public string? SelectedField { get; private set; }

        public SelectFieldDialog(List<string> availableFields)
        {
            InitializeComponent();
            lstFields.ItemsSource = availableFields;
        }

        private void LstFields_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnAdd.IsEnabled = lstFields.SelectedItem != null;
        }

        private void LstFields_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstFields.SelectedItem != null)
            {
                SelectedField = lstFields.SelectedItem as string;
                DialogResult = true;
                Close();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (lstFields.SelectedItem != null)
            {
                SelectedField = lstFields.SelectedItem as string;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
