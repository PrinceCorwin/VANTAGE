using System;
using System.Text.RegularExpressions;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog for users to submit an access request to administrators
    public partial class AccessRequestDialog : Window
    {
        public string WindowsUsername { get; private set; }
        public string FullName => txtFullName.Text.Trim();
        public string Email => txtEmail.Text.Trim();

        public AccessRequestDialog(string windowsUsername)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            WindowsUsername = windowsUsername;
            txtUsername.Text = windowsUsername;

            Loaded += (s, e) =>
            {
                txtFullName.Focus();
            };
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            // Validate Full Name
            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                ShowError("Please enter your full name.");
                txtFullName.Focus();
                return;
            }

            // Validate Email
            if (string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                ShowError("Please enter your email address.");
                txtEmail.Focus();
                return;
            }

            // Basic email format validation
            if (!IsValidEmail(txtEmail.Text.Trim()))
            {
                ShowError("Please enter a valid email address.");
                txtEmail.Focus();
                txtEmail.SelectAll();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }

        // Basic email validation using regex
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Simple pattern: something@something.something
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }
    }
}
