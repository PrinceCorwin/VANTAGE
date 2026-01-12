using System.Windows;
using MILESTONE.Services.Procore;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs;

public partial class ProcoreAuthDialog : Window
{
    private readonly ProcoreAuthService _authService;

    public bool IsConnected { get; private set; }

    public ProcoreAuthDialog(ProcoreAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _authService.OpenBrowserForAuth();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthDialog.BtnOpenBrowser_Click");
            MessageBox.Show("Failed to open browser. Please try again.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtAuthCode_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        btnConnect.IsEnabled = !string.IsNullOrWhiteSpace(txtAuthCode.Text);
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var code = txtAuthCode.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        btnConnect.IsEnabled = false;
        btnOpenBrowser.IsEnabled = false;
        btnCancel.IsEnabled = false;
        btnConnect.Content = "Connecting...";

        try
        {
            var success = await _authService.ExchangeCodeForTokenAsync(code);

            if (success)
            {
                IsConnected = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(
                    "Failed to connect to Procore. The authorization code may have expired.\n\n" +
                    "Please click 'Open Procore Login' to get a new code.",
                    "Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                txtAuthCode.Clear();
                btnConnect.Content = "Connect";
                btnConnect.IsEnabled = false;
                btnOpenBrowser.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthDialog.BtnConnect_Click");
            MessageBox.Show("An error occurred while connecting to Procore.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            btnConnect.Content = "Connect";
            btnConnect.IsEnabled = true;
            btnOpenBrowser.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}