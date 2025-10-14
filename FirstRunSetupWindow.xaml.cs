using System.Windows;
using VANTAGE.Models;

namespace VANTAGE
{
    public partial class FirstRunSetupWindow : Window
    {
        private User _currentUser;

        public FirstRunSetupWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            // Pre-fill with existing data if available
            FullNameTextBox.Text = _currentUser.FullName ?? "";
            EmailTextBox.Text = _currentUser.Email ?? "";
            PhoneTextBox.Text = _currentUser.PhoneNumber ?? "";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                MessageBox.Show("Please enter your full name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                MessageBox.Show("Please enter your email address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Update user in database
                _currentUser.FullName = FullNameTextBox.Text.Trim();
                _currentUser.Email = EmailTextBox.Text.Trim();
                _currentUser.PhoneNumber = PhoneTextBox.Text.Trim();

                UpdateUserInDatabase(_currentUser);

                MessageBox.Show("Profile updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void UpdateUserInDatabase(User user)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Users 
                    SET FullName = @fullName, Email = @email, PhoneNumber = @phone
                    WHERE UserID = @userId";
                command.Parameters.AddWithValue("@fullName", user.FullName ?? "");
                command.Parameters.AddWithValue("@email", user.Email ?? "");
                command.Parameters.AddWithValue("@phone", user.PhoneNumber ?? "");
                command.Parameters.AddWithValue("@userId", user.UserID);

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"User {user.UserID} updated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating user: {ex.Message}");
                throw;
            }
        }
    }
}