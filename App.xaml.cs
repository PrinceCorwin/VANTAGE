using System;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Models;
using VANTAGE.Utilities;
using System.IO;
using System.Threading.Tasks;

namespace VANTAGE
{
    public partial class App : Application
    {
        // Current user info (global for app)
        public static User? CurrentUser { get; set; }
        public static int CurrentUserID { get; set; }

        // Loading splash window
        private LoadingSplashWindow? _splashWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register Syncfusion license FIRST (before any UI components or database setup)
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjGyl/VkR+XU9Ff1RDX3xKf0x/TGpQb19xflBPallYVBYiSV9jS3hTd0VnWXhbcHFQQWhfVE91XQ==");

            // Start async initialization
            InitializeApplicationAsync();
        }

        private async void InitializeApplicationAsync()
        {
            try
            {
                // Check if local database exists
                string localDbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VANTAGE",
                    "VANTAGE_Local.db"
                );

                bool isFirstRun = !File.Exists(localDbPath);
                if (isFirstRun)
                {
                    // Show loading splash (also establishes correct taskbar icon)
                    _splashWindow = new LoadingSplashWindow();
                    _splashWindow.Show();
                    _splashWindow.UpdateStatus("First Run Setup...");
                }

                // Show loading splash for subsequent runs (if not already showing)
                if (_splashWindow == null)
                {
                    _splashWindow = new LoadingSplashWindow();
                    _splashWindow.Show();
                }

                // Step 0: Check for updates before anything else
                _splashWindow.UpdateStatus("Checking for updates...");
                bool updateInitiated = await UpdateService.CheckAndApplyUpdateAsync(
                    status => _splashWindow?.UpdateStatus(status),
                    () => Application.Current.Shutdown());
                if (updateInitiated) return;

                // Step 1: Initialize database
                _splashWindow.UpdateStatus("Initializing Database...");
                await Task.Run(() =>
                {
                    DatabaseSetup.InitializeDatabase();
                });

                // Check connection to Azure before attempting to mirror tables
                _splashWindow.UpdateStatus("Connecting to Azure Database...");
                bool azureOnline = false;
                while (!azureOnline)
                {
                    string? connectionError = null;
                    bool connectionResult = await Task.Run(() =>
                    {
                        return AzureDbManager.CheckConnection(out connectionError);
                    });

                    if (connectionResult)
                    {
                        azureOnline = true;
                    }
                    else
                    {
                        // Hide splash temporarily to show retry dialog
                        _splashWindow.Hide();

                        string dialogMessage = $"{connectionError}\n\n" +
                            "Please check your internet connection.\n\n" +
                            "• Click RETRY to test connection again\n" +
                            "• Click WORK OFFLINE to continue without syncing (you can sync later when connection is restored)";

                        // Show custom dialog with RETRY and WORK OFFLINE buttons
                        var dialog = new VANTAGE.Dialogs.ConnectionRetryDialog(dialogMessage);
                        bool? result = dialog.ShowDialog();

                        // Show splash again
                        _splashWindow.Show();

                        if (result == true && dialog.RetrySelected)
                        {
                            // User clicked RETRY - loop continues and connection check runs again
                            _splashWindow.UpdateStatus("Retrying Connection...");
                            continue;
                        }
                        else
                        {
                            // User clicked WORK OFFLINE
                            AppLogger.Info("User chose to work offline - Azure database unavailable at startup", "App.OnStartup");
                            break;
                        }
                    }
                }

                // Mirror reference tables from Azure to Local (only if connection successful)
                if (azureOnline)
                {
                    try
                    {
                        _splashWindow.UpdateStatus("Syncing Reference Tables...");
                        await Task.Run(() =>
                        {
                            DatabaseSetup.MirrorTablesFromAzure();
                        });
                        AppLogger.Info("Successfully mirrored reference tables from Azure database", "App.OnStartup");
                        ProjectCache.Reload();

                        // Ensure Azure indexes exist (awaited so it completes before app opens)
                        _splashWindow.UpdateStatus("Verifying Azure indexes...");
                        await Task.Run(() => DatabaseSetup.EnsureAzureIndexes());
                    }
                    catch (Exception mirrorEx)
                    {
                        AppLogger.Error(mirrorEx, "App.OnStartup - MirrorTablesFromAzure");
                        MessageBox.Show(
                            $"Failed to sync reference tables from Azure database:\n\n{mirrorEx.Message}\n\n" +
                            "The application will continue, but some data may be outdated.",
                            "Sync Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                VANTAGE.Utilities.AppLogger.Initialize();
                VANTAGE.Utilities.AppLogger.Info("Milestone starting up...", "App.OnStartup");

                // Purge old logs (older than 30 days)
                VANTAGE.Utilities.ScheduleChangeLogger.PurgeOldLogs();
                VANTAGE.Utilities.AppLogger.PurgeOldLogs();

                // Step 2: Initialize default app settings
                _splashWindow.UpdateStatus("Loading Application Settings...");
                await Task.Run(() =>
                {
                    SettingsManager.InitializeDefaultAppSettings();
                });

                // Step 3: Get current Windows username
                string windowsUsername = UserHelper.GetCurrentWindowsUsername();

                // Step 4: Check if user exists in database
                _splashWindow.UpdateStatus("Loading User Profile...");
                CurrentUser = await Task.Run(() => GetUser(windowsUsername));

                if (CurrentUser == null)
                {
                    _splashWindow.Close();

                    // Show access denied with option to request access
                    var result = MessageBox.Show(
                        $"User '{windowsUsername}' is not authorized to use MILESTONE.\n\n" +
                        "Would you like to submit an access request to the administrators?",
                        "Access Denied",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        await HandleAccessRequestAsync(windowsUsername, azureOnline);
                    }

                    this.Shutdown();
                    return;
                }

                CurrentUserID = CurrentUser.UserID;

                // Check admin status from Azure (only if online)
                if (azureOnline)
                {
                    CurrentUser.IsAdmin = await Task.Run(() => AzureDbManager.IsUserAdmin(CurrentUser.Username));
                }
                else
                {
                    // Offline - no admin access
                    CurrentUser.IsAdmin = false;
                }

                // Initialize user settings
                _splashWindow.UpdateStatus("Loading User Settings...");
                await Task.Run(() =>
                {
                    SettingsManager.InitializeDefaultUserSettings();
                });

                // Step 6a: Initialize column mappings
                await Task.Run(() =>
                {
                    ActivityRepository.InitializeMappings(null);
                });

                // Step 8: Open main window
                _splashWindow.UpdateStatus("Preparing Workspace...");
                try
                {
                    MainWindow mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();

                    // Close splash screen
                    _splashWindow.Close();
                    _splashWindow = null;

                    mainWindow.Activate();
                    mainWindow.Focus();
                }
                catch (Exception mainWindowEx)
                {
                    _splashWindow?.Close();
                    MessageBox.Show($"Failed to open main window: {mainWindowEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _splashWindow?.Close();
                MessageBox.Show($"Application startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }

        // Get existing user - returns null if user doesn't exist
        private static User? GetUser(string username)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT UserID, Username, FullName, Email
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
                    };
                }

                return null;
            }
            catch
            {
                throw;
            }
        }

        // Handle access request flow when user is not found
        private async Task HandleAccessRequestAsync(string windowsUsername, bool azureOnline)
        {
            // Check if Azure is available for sending email
            if (!azureOnline)
            {
                MessageBox.Show(
                    "Cannot submit access request while offline.\n\n" +
                    "Please check your internet connection and try again later, " +
                    "or contact your administrator directly.",
                    "Offline",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Show access request dialog
            var dialog = new AccessRequestDialog(windowsUsername);
            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult != true)
            {
                return;
            }

            // Send the access request email
            try
            {
                bool sent = await EmailService.SendAccessRequestEmailAsync(
                    dialog.WindowsUsername,
                    dialog.FullName,
                    dialog.Email);

                if (sent)
                {
                    MessageBox.Show(
                        "Your access request has been sent to the administrators.\n\n" +
                        "You will receive a response at the email address you provided.",
                        "Request Sent",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to send access request.\n\n" +
                        "Please contact your administrator directly.",
                        "Send Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "App.HandleAccessRequestAsync");
                MessageBox.Show(
                    $"Error sending access request:\n\n{ex.Message}\n\n" +
                    "Please contact your administrator directly.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}