using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class ConnectionRetryDialog : Window
    {
        public bool RetrySelected { get; private set; }
        private string? _detectedIp;

        public ConnectionRetryDialog(string errorMessage)
        {
            InitializeComponent();
            txtErrorMessage.Text = errorMessage;
            Loaded += ConnectionRetryDialog_Loaded;
        }

        // Detect public IP on dialog load so user can share it with admin for whitelisting
        private async void ConnectionRetryDialog_Loaded(object sender, RoutedEventArgs e)
        {
            _detectedIp = await DetectPublicIpAsync();

            if (_detectedIp != null)
            {
                txtPublicIp.Text = _detectedIp;
                btnCopyIp.IsEnabled = true;
            }
            else
            {
                txtPublicIp.Text = "Could not detect";
            }
        }

        // Get public IP from ipify.org with short timeout
        private static async Task<string?> DetectPublicIpAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var ip = await client.GetStringAsync("https://api.ipify.org");
                return ip?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private async void BtnCopyIp_Click(object sender, RoutedEventArgs e)
        {
            if (_detectedIp == null) return;

            Clipboard.SetText(_detectedIp);
            btnCopyIp.Content = "COPIED";
            btnCopyIp.IsEnabled = false;

            await Task.Delay(2000);

            btnCopyIp.Content = "COPY";
            btnCopyIp.IsEnabled = true;
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
