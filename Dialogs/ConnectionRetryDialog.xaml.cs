using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class ConnectionRetryDialog : Window
    {
        public bool RetrySelected { get; private set; }

        public ConnectionRetryDialog(string errorMessage)
        {
            InitializeComponent();
            txtErrorMessage.Text = errorMessage;
        }

        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            RetrySelected = true;
            DialogResult = true;
            Close();
        }

        private void BtnWorkOffline_Click(object sender, RoutedEventArgs e)
        {
            RetrySelected = false;
            DialogResult = true;
            Close();
        }
    }
}