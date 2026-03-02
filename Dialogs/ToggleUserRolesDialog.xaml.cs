using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Services;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ToggleUserRolesDialog : Window
    {
        private readonly List<(int UserID, string Username, string FullName, string Email)> _users;
        private readonly HashSet<string> _adminUsers;
        private readonly HashSet<string> _estimatorUsers;
        private readonly Microsoft.Data.SqlClient.SqlConnection _azureConn;

        // Raised when a role change affects the current user's UI visibility
        public event Action<string, bool>? RoleChanged;

        public ToggleUserRolesDialog(
            List<(int UserID, string Username, string FullName, string Email)> users,
            HashSet<string> adminUsers,
            HashSet<string> estimatorUsers,
            Microsoft.Data.SqlClient.SqlConnection azureConn)
        {
            InitializeComponent();

            _users = users;
            _adminUsers = adminUsers;
            _estimatorUsers = estimatorUsers;
            _azureConn = azureConn;

            RefreshList();
        }

        private void RefreshList()
        {
            int selectedIdx = lstUsers.SelectedIndex;
            lstUsers.Items.Clear();

            foreach (var u in _users)
            {
                var roles = new List<string>();
                if (_adminUsers.Contains(u.Username)) roles.Add("ADMIN");
                if (_estimatorUsers.Contains(u.Username)) roles.Add("ESTIMATOR");
                string roleTag = roles.Count > 0 ? string.Join(", ", roles) : "User";
                lstUsers.Items.Add($"{u.Username} ({u.FullName}) — {roleTag}");
            }

            if (selectedIdx >= 0 && selectedIdx < lstUsers.Items.Count)
                lstUsers.SelectedIndex = selectedIdx;
        }

        private void BtnToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            ToggleRole("VMS_Admins", _adminUsers, "Admin");
        }

        private void BtnToggleEstimator_Click(object sender, RoutedEventArgs e)
        {
            ToggleRole("VMS_Estimators", _estimatorUsers, "Estimator");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleRole(string table, HashSet<string> roleSet, string roleName)
        {
            if (lstUsers.SelectedIndex < 0) return;

            var selectedUser = _users[lstUsers.SelectedIndex];
            bool hasRole = roleSet.Contains(selectedUser.Username);

            try
            {
                var cmd = _azureConn.CreateCommand();
                string action;

                if (hasRole)
                {
                    cmd.CommandText = $"DELETE FROM {table} WHERE Username = @username";
                    cmd.Parameters.AddWithValue("@username", selectedUser.Username);
                    cmd.ExecuteNonQuery();
                    roleSet.Remove(selectedUser.Username);
                    action = "revoked";
                }
                else
                {
                    cmd.CommandText = $"INSERT INTO {table} (Username, FullName) VALUES (@username, @fullname)";
                    cmd.Parameters.AddWithValue("@username", selectedUser.Username);
                    cmd.Parameters.AddWithValue("@fullname", selectedUser.FullName);
                    cmd.ExecuteNonQuery();
                    roleSet.Add(selectedUser.Username);
                    action = "granted";
                }

                AppLogger.Info($"{roleName} {action} for {selectedUser.Username}",
                    "ToggleUserRolesDialog.ToggleRole", App.CurrentUser?.Username);

                // Send email notification
                if (!string.IsNullOrWhiteSpace(selectedUser.Email))
                {
                    SendRoleChangeEmail(selectedUser, roleName, action);
                }

                // Notify MainWindow if the current user's roles changed
                if (selectedUser.UserID == App.CurrentUserID)
                {
                    RoleChanged?.Invoke(roleName, !hasRole);
                }

                RefreshList();

                MessageBox.Show($"{roleName} {action} for {selectedUser.Username}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating {roleName} status: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendRoleChangeEmail(
            (int UserID, string Username, string FullName, string Email) user,
            string roleName, string action)
        {
            string recipientName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;
            string changedBy = App.CurrentUser?.Username ?? "Unknown";
            string emailSubject = $"VANTAGE: MS - {roleName} privileges {action}";
            string emailHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px 20px; border-radius: 4px 4px 0 0; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 0 0 4px 4px; }}
        .highlight {{ font-size: 20px; font-weight: bold; color: {(action == "granted" ? "#2E7D32" : "#C62828")}; }}
        .details {{ margin-top: 15px; }}
        .detail-row {{ padding: 8px 0; border-bottom: 1px solid #ddd; }}
        .label {{ font-weight: 600; color: #555; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>VANTAGE: MS Role Change</h2>
        </div>
        <div class='content'>
            <p>Hello {WebUtility.HtmlEncode(recipientName)},</p>
            <p class='highlight'>{roleName} privileges {action}</p>
            <div class='details'>
                <div class='detail-row'>
                    <span class='label'>Changed by:</span> {WebUtility.HtmlEncode(changedBy)}
                </div>
                <div class='detail-row'>
                    <span class='label'>Date:</span> {DateTime.Now:MMMM d, yyyy h:mm tt}
                </div>
            </div>
            <p style='margin-top: 20px;'>The change will take effect next time you open VANTAGE: MS.</p>
            <div class='footer'>
                <p>This is an automated message from VANTAGE: MS. Please do not reply to this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";
            _ = EmailService.SendEmailAsync(user.Email, emailSubject, emailHtml);
        }
    }
}
