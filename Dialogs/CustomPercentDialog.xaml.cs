using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace VANTAGE.Dialogs
{
    public partial class CustomPercentDialog : Window
    {
        public int PercentValue { get; private set; }

        public CustomPercentDialog(int currentValue)
        {
            InitializeComponent();
            txtPercent.Text = currentValue.ToString();
            txtPercent.SelectAll();
            txtPercent.Focus();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPercent.Text))
            {
                MessageBox.Show("Please enter a value.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtPercent.Text, out int value) || value < 0 || value > 100)
            {
                MessageBox.Show("Please enter a valid number between 0 and 100.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PercentValue = value;
            DialogResult = true;
            Close();
        }

        // Only allow numeric input
        private void TxtPercent_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextNumeric(e.Text);
        }

        private static bool IsTextNumeric(string text)
        {
            Regex regex = new Regex("[^0-9]+");
            return !regex.IsMatch(text);
        }
    }
}