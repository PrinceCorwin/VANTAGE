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

                VANTAGE.Utilities.AppLogger.Initialize();
                VANTAGE.Utilities.AppLogger.Info("Milestone starting up...", "App.OnStartup");

                // ADD THE NEW LINE HERE (commented out so it only ran once):
                DatabaseSetup.SeedTestUsers();

                // Step 2: Initialize default app settings
                SettingsManager.InitializeDefaultAppSettings();

                // Step 3: Get current Windows username
                string windowsUsername = UserHelper.GetCurrentWindowsUsername();;

                // Step 4: Check if user exists in database
                CurrentUser = GetOrCreateUser(windowsUsername);
                CurrentUserID = CurrentUser.UserID;

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


        /// Get existing user or create new user if doesn't exist

        private static User GetOrCreateUser(string username)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // 1) Check if user exists (keep your original semantics)
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT UserID, Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken
                FROM Users
                WHERE Username = @username";
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
                            IsAdmin = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                            AdminToken = reader.IsDBNull(6) ? null : reader.GetString(6)
                        };
                    }
                }

                // 2) New user: decide admin based on username
                bool shouldBeAdmin =
                    username.Equals("Steve", StringComparison.OrdinalIgnoreCase) ||
                    username.Equals("Steve.Amalfitano", StringComparison.OrdinalIgnoreCase);

                int isAdminInt = shouldBeAdmin ? 1 : 0;

                // Insert with explicit IsAdmin = 0/1 to avoid NULLs
                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.CommandText = @"
                INSERT INTO Users (Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken)
                VALUES (@username, '', '', '', @isAdmin, NULL)";
                    insertCommand.Parameters.AddWithValue("@username", username);
                    insertCommand.Parameters.AddWithValue("@isAdmin", isAdminInt);
                    insertCommand.ExecuteNonQuery();
                }

                // Get new ID
                long newUserID;
                using (var idCmd = connection.CreateCommand())
                {
                    idCmd.CommandText = "SELECT last_insert_rowid()";
                    newUserID = (long)idCmd.ExecuteScalar();
                }

                // If Steve, grant admin via helper (centralizes token + flip logic)
                if (shouldBeAdmin)
                {
                    AdminHelper.GrantAdmin((int)newUserID, username);
                }

                // Re-read the row so we return authoritative values (including token if granted)
                using (var getCmd = connection.CreateCommand())
                {
                    getCmd.CommandText = @"
                SELECT UserID, Username, FullName, Email, PhoneNumber, IsAdmin, AdminToken
                FROM Users
                WHERE UserID = @id";
                    getCmd.Parameters.AddWithValue("@id", (int)newUserID);

                    using var reader = getCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new User
                        {
                            UserID = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            FullName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            IsAdmin = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                            AdminToken = reader.IsDBNull(6) ? null : reader.GetString(6)
                        };
                    }
                }

                // Fallback (shouldn't hit)
                return new User
                {
                    UserID = (int)newUserID,
                    Username = username,
                    FullName = "",
                    Email = "",
                    PhoneNumber = "",
                    IsAdmin = shouldBeAdmin,
                    AdminToken = null
                };
            }
            catch
            {
                // TODO: Add proper logging when logging system is implemented
                throw;
            }
        }



        /// Get user by ID

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