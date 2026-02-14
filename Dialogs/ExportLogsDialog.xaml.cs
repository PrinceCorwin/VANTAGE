using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ExportLogsDialog : Window
    {
        private List<LogEntry> _logs = new();
        private List<UserItem> _users = new();

        public ExportLogsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ExportLogsDialog_Loaded;
        }

        private void ExportLogsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
            RefreshPreview();
        }

        private void LoadUsers()
        {
            try
            {
                _users.Clear();
                _users.Add(new UserItem { UserID = 0, Username = "(None)", FullName = "", Email = "" });

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT UserID, Username, FullName, Email FROM Users ORDER BY Username";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _users.Add(new UserItem
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        FullName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }

                cboUsers.ItemsSource = _users;
                cboUsers.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ExportLogsDialog.LoadUsers");
            }
        }

        private void RefreshPreview()
        {
            var (fromDate, toDate) = GetDateRange();
            var minLevel = GetMinLevel();

            _logs = AppLogger.GetLogs(fromDate, toDate, minLevel);

            int errorCount = 0;
            int warningCount = 0;
            foreach (var log in _logs)
            {
                if (log.Level == "Error") errorCount++;
                else if (log.Level == "Warning") warningCount++;
            }

            txtPreview.Text = $"{_logs.Count} log entries found ({errorCount} errors, {warningCount} warnings)";
        }

        private (DateTime? from, DateTime? to) GetDateRange()
        {
            var now = DateTime.UtcNow;
            return cboDateRange.SelectedIndex switch
            {
                0 => (now.AddHours(-24), now),
                1 => (now.AddDays(-7), now),
                2 => (now.AddDays(-30), now),
                _ => (null, null)
            };
        }

        private AppLogger.LogLevel? GetMinLevel()
        {
            return cboLogLevel.SelectedIndex switch
            {
                1 => AppLogger.LogLevel.Info,
                2 => AppLogger.LogLevel.Warning,
                3 => AppLogger.LogLevel.Error,
                _ => null
            };
        }

        private void CboDateRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) RefreshPreview();
        }

        private void CboLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) RefreshPreview();
        }

        private void CboUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var user = cboUsers.SelectedItem as UserItem;
            if (user != null && user.UserID > 0)
            {
                txtEmail.Text = user.Email;
                btnEmail.IsEnabled = !string.IsNullOrWhiteSpace(user.Email);
            }
            else
            {
                txtEmail.Text = "";
                btnEmail.IsEnabled = false;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_logs.Count == 0)
            {
                MessageBox.Show("No logs to export.", "Export Logs",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Logs",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"MILESTONE_Logs_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = AppLogger.ExportLogsToText(_logs);
                    System.IO.File.WriteAllText(dialog.FileName, content);

                    AppLogger.Info($"Exported {_logs.Count} logs to {dialog.FileName}",
                        "ExportLogsDialog.BtnExport_Click", App.CurrentUser?.Username ?? "Unknown");

                    MessageBox.Show($"Exported {_logs.Count} log entries.", "Export Logs",
                        MessageBoxButton.OK, MessageBoxImage.None);

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ExportLogsDialog.BtnExport_Click");
                    MessageBox.Show($"Export failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            var user = cboUsers.SelectedItem as UserItem;
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                MessageBox.Show("Please select a user with an email address.", "Send Email",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_logs.Count == 0)
            {
                MessageBox.Show("No logs to send.", "Send Email",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            btnEmail.IsEnabled = false;
            btnEmail.Content = "Sending...";

            try
            {
                var content = AppLogger.ExportLogsToText(_logs);
                var bytes = Encoding.UTF8.GetBytes(content);
                var fileName = $"MILESTONE_Logs_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt";

                var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px 20px; border-radius: 4px 4px 0 0; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 0 0 4px 4px; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>MILESTONE Log Export</h2>
        </div>
        <div class='content'>
            <p>Hello {user.FullName ?? user.Username},</p>
            <p>Please find attached the MILESTONE application logs.</p>
            <p><strong>Log Summary:</strong></p>
            <ul>
                <li>Total entries: {_logs.Count}</li>
                <li>Exported by: {App.CurrentUser?.Username ?? "Unknown"}</li>
                <li>Date: {DateTime.Now:MMMM d, yyyy h:mm tt}</li>
            </ul>
            <div class='footer'>
                <p>This is an automated message from MILESTONE.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                bool success = await EmailService.SendEmailWithAttachmentAsync(
                    user.Email,
                    user.FullName ?? user.Username,
                    $"MILESTONE Logs - {DateTime.Now:yyyy-MM-dd}",
                    htmlBody,
                    fileName,
                    bytes);

                if (success)
                {
                    MessageBox.Show($"Logs sent to {user.Email}", "Send Email",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    DialogResult = true;
                    Close();
                    return;
                }
                else
                {
                    MessageBox.Show("Failed to send email. Check logs for details.", "Send Email",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ExportLogsDialog.BtnEmail_Click");
                MessageBox.Show($"Failed to send email: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnEmail.Content = "Send Email";
                btnEmail.IsEnabled = true;
            }
        }

        // Simple user item class for the dropdown
        private class UserItem
        {
            public int UserID { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string DisplayName => string.IsNullOrEmpty(FullName) ? Username : $"{FullName} ({Username})";
        }
    }
}
