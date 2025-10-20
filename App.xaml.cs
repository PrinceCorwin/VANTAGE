using System;
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

                // ADD THE NEW LINE HERE (commented out so it only ran once):
                DatabaseSetup.SeedTestUsers();

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

                // Step 4a: Make Steve admin on first run (ONE-TIME SETUP)
                if ((CurrentUser.Username.Equals("Steve.Amalfitano", StringComparison.OrdinalIgnoreCase) ||
                     CurrentUser.Username.Equals("Steve", StringComparison.OrdinalIgnoreCase)) && !CurrentUser.IsAdmin)
                {
                    AdminHelper.GrantAdmin(CurrentUserID, CurrentUser.Username);
                    CurrentUser.IsAdmin = true;
                    CurrentUser.AdminToken = AdminHelper.GenerateAdminToken(CurrentUserID, CurrentUser.Username);
                    System.Diagnostics.Debug.WriteLine("✓ Steve granted admin privileges");
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
                        System.Diagnostics.Debug.WriteLine("⚠ Admin token invalid - revoking admin privileges");
                        AdminHelper.RevokeAdmin(CurrentUserID);
                        CurrentUser.IsAdmin = false;
                        CurrentUser.AdminToken = null;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("✓ Admin token verified");
                    }
                }

                // Step 5: Check if user has completed profile
                if (string.IsNullOrEmpty(CurrentUser.FullName) || string.IsNullOrEmpty(CurrentUser.Email))
                {
                    // Show first-run setup
                    System.Diagnostics.Debug.WriteLine("→ Showing first-run setup");
                    FirstRunSetupWindow setupWindow = new FirstRunSetupWindow(CurrentUser);
                    bool? result = setupWindow.ShowDialog();

                    if (result != true)
                    {
                        // User cancelled setup - exit app
                        System.Diagnostics.Debug.WriteLine("✗ User cancelled profile setup");
                        MessageBox.Show("Profile setup is required to use VANTAGE.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.Shutdown();
                        return;
                    }

                    // Refresh user data after setup
                    var refreshedUser = GetUserByID(CurrentUserID);
                    if (refreshedUser != null)
                    {
                        CurrentUser = refreshedUser;
                        System.Diagnostics.Debug.WriteLine("✓ User profile refreshed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Failed to refresh user data, continuing with existing user");
                    }
                }

                // Step 6: Initialize user settings
                SettingsManager.InitializeDefaultUserSettings(CurrentUserID);
                System.Diagnostics.Debug.WriteLine("✓ User settings initialized");

                // Step 6a: Load user's theme preference
                ThemeManager.LoadThemeFromSettings(CurrentUserID);
                System.Diagnostics.Debug.WriteLine("✓ Theme loaded");

                // Step 7: Determine which module to load
                string lastModule = SettingsManager.GetLastModuleUsed(CurrentUserID, "PROGRESS");
                System.Diagnostics.Debug.WriteLine($"✓ Loading module: {lastModule}");

                // Step 8: Open main window
                try
                {
                    System.Diagnostics.Debug.WriteLine("→ Creating MainWindow...");
                    MainWindow mainWindow = new MainWindow();
                    System.Diagnostics.Debug.WriteLine("→ Showing MainWindow...");
                    mainWindow.Show();
                    System.Diagnostics.Debug.WriteLine("✓ Main window opened");

                    // Force the window to stay visible
                    mainWindow.Activate();
                    mainWindow.Focus();
                    System.Diagnostics.Debug.WriteLine("→ MainWindow activated and focused");
                }
                catch (Exception mainWindowEx)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ MainWindow error: {mainWindowEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {mainWindowEx.StackTrace}");
                    MessageBox.Show($"Failed to open main window: {mainWindowEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw; // Re-throw so outer catch handles shutdown
                }
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
                System.Diagnostics.Debug.WriteLine($"Error getting user by ID: {ex.Message}");
                return null;
            }
        }
    }
}