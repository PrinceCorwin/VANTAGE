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
                var users = await System.Threading.Tasks.Task.Run(() =>
                {
                    var userList = new List<UserItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = "SELECT UserID, Username, FullName, Email FROM VMS_Users ORDER BY Username";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        userList.Add(new UserItem
                        {
                            UserID = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            FullName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            IsNew = false
                        });
                    }

                    return userList;
                });

                _users = new ObservableCollection<UserItem>(users);
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
                MessageBox.Show($"Error loading users:\n{ex.Message}", "Error",
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

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Username is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUsername.Focus();
                return;
            }

            // Check for duplicate username on new users
            if (_selectedUser == null)
            {
                if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("A user with this username already exists.", "Duplicate Username",
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
                        cmd.CommandText = @"
                            INSERT INTO VMS_Users (Username, FullName, Email)
                            OUTPUT INSERTED.UserID
                            VALUES (@username, @fullName, @email)";
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                        cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    });

                    var newUser = new UserItem
                    {
                        UserID = newId,
                        Username = username,
                        FullName = fullName,
                        Email = email,
                        IsNew = false
                    };
                    _users.Add(newUser);

                    AppLogger.Info($"Added new user: {username} (ID: {newId})",
                        "AdminUsersDialog.BtnSave_Click", App.CurrentUser?.Username);

                    MessageBox.Show($"User '{username}' added successfully.", "User Added",
                        MessageBoxButton.OK, MessageBoxImage.None);
                }
                else
                {
                    // Update existing user
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE VMS_Users
                            SET FullName = @fullName, Email = @email
                            WHERE UserID = @userId";
                        cmd.Parameters.AddWithValue("@fullName", string.IsNullOrEmpty(fullName) ? DBNull.Value : fullName);
                        cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
                        cmd.Parameters.AddWithValue("@userId", _selectedUser.UserID);

                        cmd.ExecuteNonQuery();
                    });

                    // Update in-memory
                    _selectedUser.FullName = fullName;
                    _selectedUser.Email = email;

                    AppLogger.Info($"Updated user: {username} (ID: {_selectedUser.UserID})",
                        "AdminUsersDialog.BtnSave_Click", App.CurrentUser?.Username);

                    MessageBox.Show($"User '{username}' updated successfully.", "User Updated",
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
                MessageBox.Show($"Error saving user:\n{ex.Message}", "Error",
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
                MessageBox.Show("You cannot delete your own user account.", "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
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

                    // Also remove from Admins table if present
                    var adminCmd = azureConn.CreateCommand();
                    adminCmd.CommandText = "DELETE FROM VMS_Admins WHERE Username = @username";
                    adminCmd.Parameters.AddWithValue("@username", username);
                    adminCmd.ExecuteNonQuery();
                });

                _users.Remove(_selectedUser);

                AppLogger.Info($"Deleted user: {username} (ID: {userId})",
                    "AdminUsersDialog.BtnDelete_Click", App.CurrentUser?.Username);

                MessageBox.Show($"User '{username}' deleted successfully.", "User Deleted",
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
                MessageBox.Show($"Error deleting user:\n{ex.Message}", "Error",
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

        public bool IsNew { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}