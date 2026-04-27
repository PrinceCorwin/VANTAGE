using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class AdminUsersDialog : Window
    {
        private ObservableCollection<UserItem> _users = new();
        private UserItem? _selectedUser;
        private bool _hasRoleColumns; // True if VMS_Users has IsAdmin/IsEstimator/IsManager columns

        public AdminUsersDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += AdminUsersDialog_Loaded;
        }

        private async void AdminUsersDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvUsers.Visibility = Visibility.Collapsed;

            try
            {
                var result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var userList = new List<UserItem>();
                    bool hasRoleCols = false;

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    // Check if role columns exist
                    try
                    {
                        var testCmd = azureConn.CreateCommand();
                        testCmd.CommandText = "SELECT TOP 1 IsAdmin FROM VMS_Users";
                        testCmd.ExecuteScalar();
                        hasRoleCols = true;
                    }
                    catch
                    {
                        hasRoleCols = false;
                    }

                    var cmd = azureConn.CreateCommand();

                    if (hasRoleCols)
                    {
                        // New schema - read role columns directly
                        cmd.CommandText = "SELECT UserID, Username, FullName, Email, IsAdmin, IsEstimator, IsManager FROM VMS_Users ORDER BY Username";

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            userList.Add(new UserItem
                            {
                                UserID = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                FullName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                IsAdmin = !reader.IsDBNull(4) && reader.GetBoolean(4),
                                IsEstimator = !reader.IsDBNull(5) && reader.GetBoolean(5),
                                IsManager = !reader.IsDBNull(6) && reader.GetBoolean(6),
                                IsNew = false
                            });
                        }
                    }
                    else
                    {
                        // Old schema - read from separate tables
                        var adminUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var estimatorUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var managerUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        var adminCmd = azureConn.CreateCommand();
                        adminCmd.CommandText = "SELECT Username FROM VMS_Admins";
                        using (var adminReader = adminCmd.ExecuteReader())
                        {
                            while (adminReader.Read()) adminUsers.Add(adminReader.GetString(0));
                        }

                        var estCmd = azureConn.CreateCommand();
                        estCmd.CommandText = "SELECT Username FROM VMS_Estimators";
                        using (var estReader = estCmd.ExecuteReader())
                        {
                            while (estReader.Read()) estimatorUsers.Add(estReader.GetString(0));
                        }

                        // VMS_Managers uses FullName to store username for role assignment
                        var mgrCmd = azureConn.CreateCommand();
                        mgrCmd.CommandText = "SELECT FullName FROM VMS_Managers WHERE IsActive = 1";
                        using (var mgrReader = mgrCmd.ExecuteReader())
                        {
                            while (mgrReader.Read()) managerUsers.Add(mgrReader.GetString(0));
                        }

                        cmd.CommandText = "SELECT UserID, Username, FullName, Email FROM VMS_Users ORDER BY Username";
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string username = reader.GetString(1);
                            userList.Add(new UserItem
                            {
                                UserID = reader.GetInt32(0),
                                Username = username,
                                FullName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                IsAdmin = adminUsers.Contains(username),
                                IsEstimator = estimatorUsers.Contains(username),
                                IsManager = managerUsers.Contains(username),
                                IsNew = false
                            });
                        }
                    }

                    return (userList, hasRoleCols);
                });

                _users = new ObservableCollection<UserItem>(result.userList);
                _hasRoleColumns = result.hasRoleCols;
                lvUsers.ItemsSource = _users;

                pnlLoading.Visibility = Visibility.Collapsed;
                lvUsers.Visibility = Visibility.Visible;

                txtUserCount.Text = $"{_users.Count} user(s)";
                ClearForm();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AdminUsersDialog.LoadUsersAsync");
                AppMessageBox.Show($"Error loading users:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LvUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedUser = lvUsers.SelectedItem as UserItem;

            if (_selectedUser != null)
            {
                txtUsername.Text = _selectedUser.Username;
                txtFullName.Text = _selectedUser.FullName;
                txtEmail.Text = _selectedUser.Email;
                chkIsAdmin.IsChecked = _selectedUser.IsAdmin;
                chkIsEstimator.IsChecked = _selectedUser.IsEstimator;
                chkIsManager.IsChecked = _selectedUser.IsManager;
                txtUsername.IsEnabled = false; // Can't change username of existing user
                btnSave.Content = "Save Changes";
                btnDelete.IsEnabled = true;
            }
            else
            {
                ClearForm();
            }
        }

        private void ClearForm()
        {
            _selectedUser = null;
            txtUsername.Text = string.Empty;
            txtFullName.Text = string.Empty;
            txtEmail.Text = string.Empty;
            chkIsAdmin.IsChecked = false;
            chkIsEstimator.IsChecked = false;
            chkIsManager.IsChecked = false;
            txtUsername.IsEnabled = true;
            btnSave.Content = "Add User";
            btnDelete.IsEnabled = false;
            lvUsers.SelectedItem = null;
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            txtUsername.Focus();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            string username = txtUsername.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim();

            bool isAdmin = chkIsAdmin.IsChecked == true;
            bool isEstimator = chkIsEstimator.IsChecked == true;
            bool isManager = chkIsManager.IsChecked == true;

            if (string.IsNullOrEmpty(username))
            {
                AppMessageBox.Show("Username is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUsername.Focus();
                return;
            }

            // Check for duplicate username on new users
            if (_selectedUser == null)
            {
                if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    AppMessageBox.Show("A user with this username already exists.", "Duplicate Username",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtUsername.Focus();
                    return;
                }
            }

            btnSave.IsEnabled = false;

            try
            {
                if (_selectedUser == null)
                {
                    // Insert new user
                    int newId = await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();

                        if (_hasRoleColumns)
                        {
                            // New schema - include role columns
                            cmd.CommandText = @"
                                INSERT INTO VMS_Users (Username, FullName, Email, IsAdmin, IsEstimator, IsManager)
                                OUTPUT INSERTED.UserID
                                VALUES (@username, @fullName, @email, @isAdmin, @isEstimator, @isManager)";
                            cmd.Parameters.AddWithValue("@username", username);
                            cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                            cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
                            cmd.Parameters.AddWithValue("@isAdmin", isAdmin);
                            cmd.Parameters.AddWithValue("@isEstimator", isEstimator);
                            cmd.Parameters.AddWithValue("@isManager", isManager);
                        }
                        else
                        {
                            // Old schema - insert user, then add to role tables
                            cmd.CommandText = @"
                                INSERT INTO VMS_Users (Username, FullName, Email)
                                OUTPUT INSERTED.UserID
                                VALUES (@username, @fullName, @email)";
                            cmd.Parameters.AddWithValue("@username", username);
                            cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                            cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
                        }

                        int userId = Convert.ToInt32(cmd.ExecuteScalar());

                        // Handle role tables for old schema
                        if (!_hasRoleColumns)
                        {
                            string fn = string.IsNullOrEmpty(fullName) ? username : fullName;
                            if (isAdmin)
                            {
                                var adminCmd = azureConn.CreateCommand();
                                adminCmd.CommandText = "INSERT INTO VMS_Admins (Username, FullName) VALUES (@username, @fullName)";
                                adminCmd.Parameters.AddWithValue("@username", username);
                                adminCmd.Parameters.AddWithValue("@fullName", fn);
                                adminCmd.ExecuteNonQuery();
                            }
                            if (isEstimator)
                            {
                                var estCmd = azureConn.CreateCommand();
                                estCmd.CommandText = "INSERT INTO VMS_Estimators (Username, FullName) VALUES (@username, @fullName)";
                                estCmd.Parameters.AddWithValue("@username", username);
                                estCmd.Parameters.AddWithValue("@fullName", fn);
                                estCmd.ExecuteNonQuery();
                            }
                            if (isManager)
                            {
                                // VMS_Managers uses FullName=username for role tracking
                                var mgrCmd = azureConn.CreateCommand();
                                mgrCmd.CommandText = @"INSERT INTO VMS_Managers (FullName, Position, Company, Email, IsActive)
                                                       VALUES (@username, 'User Role', 'Summit Industrial', @email, 1)";
                                mgrCmd.Parameters.AddWithValue("@username", username);
                                mgrCmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? "" : email);
                                mgrCmd.ExecuteNonQuery();
                            }
                        }

                        return userId;
                    });

                    var newUser = new UserItem
                    {
                        UserID = newId,
                        Username = username,
                        FullName = fullName,
                        Email = email,
                        IsAdmin = isAdmin,
                        IsEstimator = isEstimator,
                        IsManager = isManager,
                        IsNew = false
                    };
                    _users.Add(newUser);

                    AppLogger.Info($"Added new user: {username} (ID: {newId})",
                        "AdminUsersDialog.BtnSave_Click", App.CurrentUser?.Username);

                    AppMessageBox.Show($"User '{username}' added successfully.", "User Added",
                        MessageBoxButton.OK, MessageBoxImage.None);
                }
                else
                {
                    // Track if current user's roles changed for live UI update
                    bool isCurrentUser = _selectedUser.Username.Equals(App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);
                    bool oldIsAdmin = _selectedUser.IsAdmin;
                    bool oldIsEstimator = _selectedUser.IsEstimator;
                    bool oldIsManager = _selectedUser.IsManager;

                    // Update existing user
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();

                        if (_hasRoleColumns)
                        {
                            // New schema - update role columns directly
                            cmd.CommandText = @"
                                UPDATE VMS_Users
                                SET FullName = @fullName, Email = @email, IsAdmin = @isAdmin, IsEstimator = @isEstimator, IsManager = @isManager
                                WHERE UserID = @userId";
                            cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                            cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
                            cmd.Parameters.AddWithValue("@isAdmin", isAdmin);
                            cmd.Parameters.AddWithValue("@isEstimator", isEstimator);
                            cmd.Parameters.AddWithValue("@isManager", isManager);
                            cmd.Parameters.AddWithValue("@userId", _selectedUser.UserID);
                            cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            // Old schema - update user and manage role tables
                            cmd.CommandText = "UPDATE VMS_Users SET FullName = @fullName, Email = @email WHERE UserID = @userId";
                            cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                            cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
                            cmd.Parameters.AddWithValue("@userId", _selectedUser.UserID);
                            cmd.ExecuteNonQuery();

                            string fn = string.IsNullOrEmpty(fullName) ? username : fullName;

                            // Manage VMS_Admins
                            if (isAdmin && !oldIsAdmin)
                            {
                                var addCmd = azureConn.CreateCommand();
                                addCmd.CommandText = "INSERT INTO VMS_Admins (Username, FullName) VALUES (@username, @fullName)";
                                addCmd.Parameters.AddWithValue("@username", username);
                                addCmd.Parameters.AddWithValue("@fullName", fn);
                                addCmd.ExecuteNonQuery();
                            }
                            else if (!isAdmin && oldIsAdmin)
                            {
                                var delCmd = azureConn.CreateCommand();
                                delCmd.CommandText = "DELETE FROM VMS_Admins WHERE Username = @username";
                                delCmd.Parameters.AddWithValue("@username", username);
                                delCmd.ExecuteNonQuery();
                            }

                            // Manage VMS_Estimators
                            if (isEstimator && !oldIsEstimator)
                            {
                                var addCmd = azureConn.CreateCommand();
                                addCmd.CommandText = "INSERT INTO VMS_Estimators (Username, FullName) VALUES (@username, @fullName)";
                                addCmd.Parameters.AddWithValue("@username", username);
                                addCmd.Parameters.AddWithValue("@fullName", fn);
                                addCmd.ExecuteNonQuery();
                            }
                            else if (!isEstimator && oldIsEstimator)
                            {
                                var delCmd = azureConn.CreateCommand();
                                delCmd.CommandText = "DELETE FROM VMS_Estimators WHERE Username = @username";
                                delCmd.Parameters.AddWithValue("@username", username);
                                delCmd.ExecuteNonQuery();
                            }

                            // Manage VMS_Managers (uses FullName=username for role tracking)
                            if (isManager && !oldIsManager)
                            {
                                var addCmd = azureConn.CreateCommand();
                                addCmd.CommandText = @"INSERT INTO VMS_Managers (FullName, Position, Company, Email, IsActive)
                                                       VALUES (@username, 'User Role', 'Summit Industrial', @email, 1)";
                                addCmd.Parameters.AddWithValue("@username", username);
                                addCmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? "" : email);
                                addCmd.ExecuteNonQuery();
                            }
                            else if (!isManager && oldIsManager)
                            {
                                var delCmd = azureConn.CreateCommand();
                                delCmd.CommandText = "DELETE FROM VMS_Managers WHERE FullName = @username";
                                delCmd.Parameters.AddWithValue("@username", username);
                                delCmd.ExecuteNonQuery();
                            }
                        }
                    });

                    // Update in-memory
                    _selectedUser.FullName = fullName;
                    _selectedUser.Email = email;
                    _selectedUser.IsAdmin = isAdmin;
                    _selectedUser.IsEstimator = isEstimator;
                    _selectedUser.IsManager = isManager;

                    // Update App.CurrentUser if editing own account
                    if (isCurrentUser && App.CurrentUser != null)
                    {
                        App.CurrentUser.IsAdmin = isAdmin;
                        App.CurrentUser.IsEstimator = isEstimator;
                        App.CurrentUser.IsManager = isManager;
                    }

                    AppLogger.Info($"Updated user: {username} (ID: {_selectedUser.UserID})",
                        "AdminUsersDialog.BtnSave_Click", App.CurrentUser?.Username);

                    // Send role change notification if roles changed and user has email
                    if (!string.IsNullOrEmpty(email))
                    {
                        var roleChanges = new List<(string Role, bool Granted)>();
                        if (isAdmin != oldIsAdmin) roleChanges.Add(("Admin", isAdmin));
                        if (isEstimator != oldIsEstimator) roleChanges.Add(("Estimator", isEstimator));
                        if (isManager != oldIsManager) roleChanges.Add(("Manager", isManager));

                        if (roleChanges.Any())
                        {
                            await SendRoleChangeNotificationAsync(username, email, roleChanges);
                        }
                    }

                    AppMessageBox.Show($"User '{username}' updated successfully.", "User Updated",
                        MessageBoxButton.OK, MessageBoxImage.None);
                }

                // Refresh list view
                lvUsers.Items.Refresh();
                txtUserCount.Text = $"{_users.Count} user(s)";
                ClearForm();

                // Also update local Users table
                await RefreshLocalUsersAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminUsersDialog.BtnSave_Click");
                AppMessageBox.Show($"Error saving user:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
                return;

            // Prevent deleting yourself
            if (_selectedUser.Username.Equals(App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase))
            {
                AppMessageBox.Show("You cannot delete your own user account.", "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = AppMessageBox.Show(
                $"Are you sure you want to delete user '{_selectedUser.Username}'?\n\n" +
                "This will NOT delete their activities or snapshots.\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;

            try
            {
                int userId = _selectedUser.UserID;
                string username = _selectedUser.Username;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = "DELETE FROM VMS_Users WHERE UserID = @userId";
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();
                });

                _users.Remove(_selectedUser);

                AppLogger.Info($"Deleted user: {username} (ID: {userId})",
                    "AdminUsersDialog.BtnDelete_Click", App.CurrentUser?.Username);

                AppMessageBox.Show($"User '{username}' deleted successfully.", "User Deleted",
                    MessageBoxButton.OK, MessageBoxImage.None);

                lvUsers.Items.Refresh();
                txtUserCount.Text = $"{_users.Count} user(s)";
                ClearForm();

                // Also update local Users table
                await RefreshLocalUsersAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminUsersDialog.BtnDelete_Click");
                AppMessageBox.Show($"Error deleting user:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDelete.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task RefreshLocalUsersAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    DatabaseSetup.MirrorTablesFromAzure();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminUsersDialog.RefreshLocalUsersAsync");
                // Don't show error to user - local sync failure is not critical
            }
        }

        private async System.Threading.Tasks.Task SendRoleChangeNotificationAsync(
            string username, string userEmail, List<(string Role, bool Granted)> roleChanges)
        {
            try
            {
                var granted = roleChanges.Where(r => r.Granted).Select(r => r.Role).ToList();
                var revoked = roleChanges.Where(r => !r.Granted).Select(r => r.Role).ToList();

                string subject = "VANTAGE: MS - Your Account Roles Have Changed";

                string changesHtml = "";
                if (granted.Any())
                {
                    changesHtml += $"<p style='color: #28a745; font-weight: bold;'>Roles Granted: {string.Join(", ", granted)}</p>";
                }
                if (revoked.Any())
                {
                    changesHtml += $"<p style='color: #dc3545; font-weight: bold;'>Roles Revoked: {string.Join(", ", revoked)}</p>";
                }

                // Build role descriptions
                string roleDescriptions = "<ul style='margin: 10px 0;'>";
                foreach (var change in roleChanges)
                {
                    string desc = change.Role switch
                    {
                        "Admin" => "Full administrative privileges including user management and system settings",
                        "Estimator" => "Access to the AI Takeoff module for estimation work",
                        "Manager" => "Ability to reassign records to other users",
                        _ => ""
                    };
                    string status = change.Granted ? "granted" : "revoked";
                    roleDescriptions += $"<li><strong>{change.Role}</strong> ({status}): {desc}</li>";
                }
                roleDescriptions += "</ul>";

                string htmlBody = $@"
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
            <h2 style='margin: 0;'>Account Role Changes</h2>
        </div>
        <div class='content'>
            <p>Hello {username},</p>
            <p>Your VANTAGE: Milestone account roles have been updated by {App.CurrentUser?.Username ?? "an administrator"}.</p>
            {changesHtml}
            <p><strong>Role Descriptions:</strong></p>
            {roleDescriptions}
            <p>If you have questions about these changes, please contact your administrator.</p>
            <div class='footer'>
                <p>This is an automated message from VANTAGE: Milestone.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                await EmailService.SendEmailAsync(userEmail, subject, htmlBody);

                AppLogger.Info(
                    $"Sent role change notification to {username} ({userEmail}): {string.Join(", ", roleChanges.Select(r => $"{r.Role}={r.Granted}"))}",
                    "AdminUsersDialog.SendRoleChangeNotificationAsync",
                    App.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminUsersDialog.SendRoleChangeNotificationAsync");
                // Don't show error to user - notification failure shouldn't block the update
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    // Model for user item
    public class UserItem : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private bool _isAdmin;
        private bool _isEstimator;
        private bool _isManager;

        public int UserID { get; set; }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(nameof(FullName)); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(nameof(Email)); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(nameof(IsAdmin)); OnPropertyChanged(nameof(RoleDisplay)); }
        }

        public bool IsEstimator
        {
            get => _isEstimator;
            set { _isEstimator = value; OnPropertyChanged(nameof(IsEstimator)); OnPropertyChanged(nameof(RoleDisplay)); }
        }

        public bool IsManager
        {
            get => _isManager;
            set { _isManager = value; OnPropertyChanged(nameof(IsManager)); OnPropertyChanged(nameof(RoleDisplay)); }
        }

        public string RoleDisplay
        {
            get
            {
                var roles = new List<string>();
                if (IsAdmin) roles.Add("Admin");
                if (IsEstimator) roles.Add("Estimator");
                if (IsManager) roles.Add("Manager");
                return roles.Count > 0 ? string.Join(", ", roles) : "User";
            }
        }

        public bool IsNew { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}