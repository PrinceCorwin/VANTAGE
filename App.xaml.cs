using System;
using System.Windows;
using VANTAGE.Data;
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

            // Register Syncfusion license FIRST (before any UI components or database setup)
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH9dcXVVRGBYVUNxWUdWYEg=");


            try
            {

                // Step 1: Initialize database
                DatabaseSetup.InitializeDatabase();

                // ADD THE NEW LINE HERE (commented out so it only ran once):
                 DatabaseSetup.SeedTestUsers();

                // Step 2: Initialize default app settings
                SettingsManager.InitializeDefaultAppSettings();

                // Step 3: Get current Windows username
                string windowsUsername = UserHelper.GetCurrentWindowsUsername();;

                // Step 4: Check if user exists in database
                CurrentUser = GetOrCreateUser(windowsUsername);
                CurrentUserID = CurrentUser.UserID;

                // Step 4a: Make Steve admin on first run (ONE-TIME SETUP)
                if ((CurrentUser.Username.Equals("Steve.Amalfitano", StringComparison.OrdinalIgnoreCase) ||
                CurrentUser.Username.Equals("Steve", StringComparison.OrdinalIgnoreCase)) && !CurrentUser.IsAdmin)
                {
                    AdminHelper.GrantAdmin(CurrentUserID, CurrentUser.Username);
                    CurrentUser.IsAdmin = true;
                    CurrentUser.AdminToken = AdminHelper.GenerateAdminToken(CurrentUserID, CurrentUser.Username);
                }

                // Step 4b: Verify admin token if user claims to be admin
                if (CurrentUser.IsAdmin)
                {
                    bool tokenValid = AdminHelper.VerifyAdminToken(
                        CurrentUser.UserID,
                        CurrentUser.Username,
                        CurrentUser.AdminToken
                    );

                    if (!tokenValid)
                    {
                        AdminHelper.RevokeAdmin(CurrentUserID);
                        CurrentUser.IsAdmin = false;
                        CurrentUser.AdminToken = null;
                    }
                    else
                    {
                        // TODO: Add proper logging when logging system is implemented
                    }
                }

                // Step 5: Check if user has completed profile
                if (string.IsNullOrEmpty(CurrentUser.FullName) || string.IsNullOrEmpty(CurrentUser.Email))
                {
                    // Show first-run setup
                    FirstRunSetupWindow setupWindow = new FirstRunSetupWindow(CurrentUser);
                    bool? result = setupWindow.ShowDialog();

                    if (result != true)
                    {
                        // User cancelled setup - exit app
                        MessageBox.Show("Profile setup is required to use VANTAGE.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.Shutdown();
                        return;
                    }

                    // Refresh user data after setup
                    var refreshedUser = GetUserByID(CurrentUserID);
                    if (refreshedUser != null)
                    {
                        CurrentUser = refreshedUser;
                    }
                    else
                    {
                        // TODO: Add proper logging when logging system is implemented
                    }
                }

                // Step 6: Initialize user settings
                SettingsManager.InitializeDefaultUserSettings(CurrentUserID);

                // Step 6a: Initialize column mappings (use default project for now)
                ActivityRepository.InitializeMappings(null); // null = use defaults

                // Step 7: Determine which module to load
                string lastModule = SettingsManager.GetLastModuleUsed(CurrentUserID, "PROGRESS");

                // Step 8: Open main window
                try
                {
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();

                    // Force the window to stay visible
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
                catch (Exception mainWindowEx)
                {
                    MessageBox.Show($"Failed to open main window: {mainWindowEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw; // Re-throw so outer catch handles shutdown
                }
            }
            catch (Exception ex)
            {
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
                command.CommandText = "SELECT UserID, Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken FROM Users WHERE Username = @username";
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
                        PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        IsAdmin = reader.IsDBNull(5) ? false : reader.GetInt32(5) == 1,
                        AdminToken = reader.IsDBNull(6) ? "" : reader.GetString(6)
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
                // TODO: Add proper logging when logging system is implemented
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
                command.CommandText = "SELECT UserID, Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken FROM Users WHERE UserID = @id";
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
                        PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        IsAdmin = reader.IsDBNull(5) ? false : reader.GetInt32(5) == 1,
                        AdminToken = reader.IsDBNull(6) ? "" : reader.GetString(6)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
                return null;
            }
        }
    }
}