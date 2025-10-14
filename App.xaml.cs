using System.Windows;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE
{
    public partial class App : Application
    {
        // Current user info (global for app)
        public static User CurrentUser { get; set; }
        public static int CurrentUserID { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Step 1: Initialize database
                DatabaseSetup.InitializeDatabase();
                System.Diagnostics.Debug.WriteLine("✓ Database initialized");

                // Step 2: Initialize default app settings
                SettingsManager.InitializeDefaultAppSettings();
                System.Diagnostics.Debug.WriteLine("✓ App settings initialized");

                // Step 3: Get current Windows username
                string windowsUsername = UserHelper.GetCurrentWindowsUsername();
                System.Diagnostics.Debug.WriteLine($"✓ Windows user: {windowsUsername}");

                // Step 4: Check if user exists in database
                CurrentUser = GetOrCreateUser(windowsUsername);
                CurrentUserID = CurrentUser.UserID;
                System.Diagnostics.Debug.WriteLine($"✓ Current user ID: {CurrentUserID}");

                // Step 5: Check if user has completed profile
                if (string.IsNullOrEmpty(CurrentUser.FullName) || string.IsNullOrEmpty(CurrentUser.Email))
                {
                    // Show first-run setup
                    System.Diagnostics.Debug.WriteLine("→ Showing first-run setup");
                    FirstRunSetupWindow setupWindow = new FirstRunSetupWindow(CurrentUser);
                    setupWindow.ShowDialog();

                    // Refresh user data after setup
                    CurrentUser = GetUserByID(CurrentUserID);
                }

                // Step 6: Initialize user settings
                SettingsManager.InitializeDefaultUserSettings(CurrentUserID);
                System.Diagnostics.Debug.WriteLine("✓ User settings initialized");

                // Step 7: Determine which module to load
                string lastModule = SettingsManager.GetLastModuleUsed(CurrentUserID, "PROGRESS");
                System.Diagnostics.Debug.WriteLine($"✓ Loading module: {lastModule}");

                // Step 8: Open main window
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                System.Diagnostics.Debug.WriteLine("✓ Main window opened");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Startup error: {ex.Message}");
                MessageBox.Show($"Application startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }

        /// <summary>
        /// Get existing user or create new user if doesn't exist
        /// </summary>
        private static User GetOrCreateUser(string username)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // Check if user exists
                var command = connection.CreateCommand();
                command.CommandText = "SELECT UserID, Username, FullName, Email, PhoneNumber FROM Users WHERE Username = @username";
                command.Parameters.AddWithValue("@username", username);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new User
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        FullName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4)
                    };
                }

                // User doesn't exist, create new one
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Users (Username) VALUES (@username); SELECT last_insert_rowid();";
                insertCommand.Parameters.AddWithValue("@username", username);

                var newUserID = (long)insertCommand.ExecuteScalar();

                return new User
                {
                    UserID = (int)newUserID,
                    Username = username,
                    FullName = "",
                    Email = "",
                    PhoneNumber = ""
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting/creating user: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        private static User GetUserByID(int userID)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT UserID, Username, FullName, Email, PhoneNumber FROM Users WHERE UserID = @id";
                command.Parameters.AddWithValue("@id", userID);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new User
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        FullName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting user by ID: {ex.Message}");
                return null;
            }
        }
    }
}