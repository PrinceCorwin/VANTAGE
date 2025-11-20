using System;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;
using System.IO;

namespace VANTAGE
{
    public partial class App : Application
    {

        // Current user info (global for app)
        public static User CurrentUser { get; set; }
        public static int CurrentUserID { get; set; }

        // Add this field at the top of the App class
        private string _tempCentralDbPath;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register Syncfusion license FIRST (before any UI components or database setup)
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH9dcXVVRGBYVUNxWUdWYEg=");

            try
            {
                // NEW: Check if local database exists
                string localDbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VANTAGE",
                    "VANTAGE_Local.db"
                );

                bool isFirstRun = !File.Exists(localDbPath);

                if (isFirstRun)
                {
                    // Prompt user to browse to Central.db
                    var openFileDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Locate Central Database",
                        Filter = "Database files (*.db)|*.db|All files (*.*)|*.*",
                        InitialDirectory = @"C:\"
                    };

                    if (openFileDialog.ShowDialog() != true)
                    {
                        MessageBox.Show("Central database location is required to initialize VANTAGE.",
                            "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.Shutdown();
                        return;
                    }

                    string selectedPath = openFileDialog.FileName;

                    // Validate the selected database
                    if (!DatabaseSetup.ValidateCentralDatabase(selectedPath, out string validationError))
                    {
                        MessageBox.Show($"Invalid Central database:\n\n{validationError}\n\nPlease select a valid Central database file.",
                            "Invalid Database", MessageBoxButton.OK, MessageBoxImage.Error);
                        this.Shutdown();
                        return;
                    }

                    _tempCentralDbPath = selectedPath;
                }

                // Step 1: Initialize database
                DatabaseSetup.InitializeDatabase();

                // Save or read Central.db path
                string centralDbPath = string.Empty;

                if (isFirstRun && !string.IsNullOrEmpty(_tempCentralDbPath))
                {
                    SettingsManager.SetAppSetting("CentralDatabasePath", _tempCentralDbPath, "string");
                    centralDbPath = _tempCentralDbPath;
                    _tempCentralDbPath = null;
                }
                else if (!isFirstRun)
                {
                    centralDbPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");
                    if (string.IsNullOrEmpty(centralDbPath))
                    {
                        MessageBox.Show("Central database path not found.",
                            "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.Shutdown();
                        return;
                    }
                }

                // Check connection to Central before attempting to mirror tables
                bool centralOnline = false;
                while (!centralOnline)
                {
                    if (SyncManager.CheckCentralConnection(centralDbPath, out string connectionError))
                    {
                        centralOnline = true;
                    }
                    else
                    {
                        // Build detailed error message
                        string dialogMessage = $"{connectionError}\n\n" +
                            "Please check your network connection or ensure Google Drive is running.\n\n" +
                            "• Click RETRY to test connection again\n" +
                            "• Click WORK OFFLINE to continue without syncing (you can sync later when connection is restored)";

                        // Show custom dialog with RETRY and WORK OFFLINE buttons
                        var dialog = new VANTAGE.Views.ConnectionRetryDialog(dialogMessage);
                        bool? result = dialog.ShowDialog();

                        if (result == true && dialog.RetrySelected)
                        {
                            // User clicked RETRY - loop continues and connection check runs again
                            continue;
                        }
                        else
                        {
                            // User clicked WORK OFFLINE
                            AppLogger.Info("User chose to work offline - Central database unavailable at startup", "App.OnStartup");
                            break;
                        }
                    }
                }

                // Mirror reference tables from Central to Local (only if connection successful)
                if (centralOnline)
                {
                    try
                    {
                        DatabaseSetup.MirrorTablesFromCentral(centralDbPath);
                        AppLogger.Info("Successfully mirrored reference tables from Central database", "App.OnStartup");
                    }
                    catch (Exception mirrorEx)
                    {
                        AppLogger.Error(mirrorEx, "App.OnStartup - MirrorTablesFromCentral");
                        MessageBox.Show(
                            $"Failed to sync reference tables from Central database:\n\n{mirrorEx.Message}\n\n" +
                            "The application will continue, but some data may be outdated.",
                            "Sync Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                VANTAGE.Utilities.AppLogger.Initialize();
                VANTAGE.Utilities.AppLogger.Info("Milestone starting up...", "App.OnStartup");

                // Step 2: Initialize default app settings
                SettingsManager.InitializeDefaultAppSettings();

                // Step 3: Get current Windows username
                string windowsUsername = UserHelper.GetCurrentWindowsUsername();

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
                }

                // Step 5: Check if user has completed profile
                if (string.IsNullOrEmpty(CurrentUser.FullName) || string.IsNullOrEmpty(CurrentUser.Email))
                {
                    FirstRunSetupWindow setupWindow = new FirstRunSetupWindow(CurrentUser);
                    bool? result = setupWindow.ShowDialog();

                    if (result != true)
                    {
                        MessageBox.Show("Profile setup is required to use VANTAGE.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.Shutdown();
                        return;
                    }

                    var refreshedUser = GetUserByID(CurrentUserID);
                    if (refreshedUser != null)
                    {
                        CurrentUser = refreshedUser;

                        // Sync new/updated user to Central database
                        string centralPath = SettingsManager.GetAppSetting("CentralDatabasePath", "");
                        if (!string.IsNullOrEmpty(centralPath))
                        {
                            DatabaseSetup.AddUserToCentral(
                                centralPath,
                                CurrentUser.UserID,
                                CurrentUser.Username,
                                CurrentUser.FullName,
                                CurrentUser.Email,
                                CurrentUser.PhoneNumber,
                                CurrentUser.IsAdmin,
                                CurrentUser.AdminToken
                            );
                        }
                    }
                }

                // Step 6: Initialize user settings
                SettingsManager.InitializeDefaultUserSettings(CurrentUserID);

                // Step 6a: Initialize column mappings
                ActivityRepository.InitializeMappings(null);

                // Step 7: Determine which module to load
                string lastModule = SettingsManager.GetLastModuleUsed(CurrentUserID, "PROGRESS");

                // Step 8: Open main window
                try
                {
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
                catch (Exception mainWindowEx)
                {
                    MessageBox.Show($"Failed to open main window: {mainWindowEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
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

                // 1) Check if user exists
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

                // 2) New user: Insert as non-admin by default
                int isAdminInt = 0;

                // Insert with explicit IsAdmin = 0 to avoid NULLs
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

                // Re-read the row so we return authoritative values
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
                    IsAdmin = false,
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