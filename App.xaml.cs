using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Models;
using VANTAGE.Services.AI;
using VANTAGE.Services.Plugins;
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

        // Active AI Takeoff session (lifted from TakeoffView so the user can
        // navigate away mid-batch and return to a live view).
        public static TakeoffSession? CurrentTakeoff { get; private set; }

        // Sticky "any non-cancelled takeoff has finished since startup" flag.
        // Drives the bottom-bar "Takeoff: Complete" indicator.
        public static bool HasCompletedTakeoffSinceStartup { get; private set; }

        // Raised whenever CurrentTakeoff is replaced (including set -> null).
        public static event EventHandler? CurrentTakeoffChanged;

        // Replace the active takeoff session. Wires the new session's Completed
        // event so HasCompletedTakeoffSinceStartup latches on the first
        // successfully-finished batch since app launch.
        public static void SetCurrentTakeoff(TakeoffSession? session)
        {
            var previous = CurrentTakeoff;
            if (previous != null)
                previous.Completed -= OnTakeoffSessionCompleted;

            CurrentTakeoff = session;

            if (session != null)
                session.Completed += OnTakeoffSessionCompleted;

            CurrentTakeoffChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void OnTakeoffSessionCompleted(object? sender, EventArgs e)
        {
            if (sender is TakeoffSession s && s.CompletedSuccessfully)
                HasCompletedTakeoffSinceStartup = true;
        }

        // Loading splash window
        private LoadingSplashWindow? _splashWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Force tooltip initial delay on every hover (disable instant-show after first tooltip)
            ToolTipService.BetweenShowDelayProperty.OverrideMetadata(
                typeof(FrameworkElement), new FrameworkPropertyMetadata(0));

            // Register Syncfusion license FIRST (before any UI components or database setup)
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXxcdXVSQmJfVEx3XENWYEo=");

            // Handle --uninstall before any other initialization
            if (Environment.GetCommandLineArgs().Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                UninstallService.RunUninstall();
                return;
            }

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

                // Step 0b: Auto-update installed plugins from the plugin feed
                _splashWindow.UpdateStatus("Checking plugin updates...");
                var pluginUpdateSummary = await PluginAutoUpdateService.CheckAndUpdateInstalledPluginsAsync(
                    status => _splashWindow?.UpdateStatus(status));

                if (pluginUpdateSummary.UpdatedCount > 0)
                {
                    _splashWindow.UpdateStatus($"Updated {pluginUpdateSummary.UpdatedCount} plugin(s).");
                    await Task.Delay(400);
                }

                // Step 1: Initialize database and run migrations
                _splashWindow.UpdateStatus("Initializing Database...");
                try
                {
                    await Task.Run(() =>
                    {
                        DatabaseSetup.InitializeDatabase(status =>
                        {
                            // Update splash from background thread
                            _splashWindow?.Dispatcher.Invoke(() => _splashWindow?.UpdateStatus(status));
                        });
                    });
                }
                catch (MigrationException ex)
                {
                    _splashWindow?.Close();

                    var result = AppMessageBox.Show(
                        $"Database migration failed during upgrade to version {ex.FailedVersion}.\n\n" +
                        $"Error: {ex.InnerException?.Message}\n\n" +
                        "Options:\n" +
                        "- YES: Delete local database and sync fresh from Azure\n" +
                        "- NO: Exit application (database may be unusable)\n\n" +
                        "Your work is safely stored in Azure. Deleting the local database " +
                        "will re-download all data on next startup.",
                        "Database Migration Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Delete local database and restart
                        try
                        {
                            System.IO.File.Delete(DatabaseSetup.DbPath);
                            AppLogger.Info("User chose to delete local database after migration failure", "App.OnStartup");
                        }
                        catch (Exception deleteEx)
                        {
                            AppLogger.Error(deleteEx, "App.OnStartup - Failed to delete local database");
                        }

                        // Restart the app
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            System.Diagnostics.Process.Start(exePath);
                        }
                    }

                    this.Shutdown();
                    return;
                }

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
                        AppMessageBox.Show(
                            $"Failed to sync reference tables from Azure database:\n\n{mirrorEx.Message}\n\n" +
                            "The application will continue, but some data may be outdated.",
                            "Sync Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                VANTAGE.Utilities.AppLogger.Initialize();
                VANTAGE.Utilities.AppLogger.Info("Milestone starting up...", "App.OnStartup");

                // Purge old logs (older than 15 days)
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
                    var result = AppMessageBox.Show(
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

                // Check admin/estimator/manager status from Azure (only if online)
                if (azureOnline)
                {
                    CurrentUser.IsAdmin = await Task.Run(() => AzureDbManager.IsUserAdmin(CurrentUser.Username));
                    CurrentUser.IsEstimator = await Task.Run(() => AzureDbManager.IsUserEstimator(CurrentUser.Username));
                    CurrentUser.IsManager = await Task.Run(() => AzureDbManager.IsUserManager(CurrentUser.Username));
                }
                else
                {
                    // Offline - no admin, estimator, or manager access
                    CurrentUser.IsAdmin = false;
                    CurrentUser.IsEstimator = false;
                    CurrentUser.IsManager = false;
                }

                // Initialize user settings
                _splashWindow.UpdateStatus("Loading User Settings...");
                await Task.Run(() =>
                {
                    SettingsManager.InitializeDefaultUserSettings();
                });

                // Apply user's saved theme (before MainWindow is created)
                _splashWindow.UpdateStatus("Applying theme...");
                ThemeManager.LoadThemeFromSettings();

                // Step 6a: Initialize column mappings
                await Task.Run(() =>
                {
                    ActivityRepository.InitializeMappings(null);
                });

                // Step 6b: One-time backfill of local ProgressSnapshots mirror.
                // If the user already has an imported P6 file but the local snapshot mirror is empty
                // (e.g. first launch after upgrade), pull the matching week's snapshots from Azure
                // so the Schedule module is fast immediately. Best-effort — silent if Azure offline.
                if (azureOnline && CurrentUser != null)
                {
                    _splashWindow.UpdateStatus("Preparing Schedule data...");
                    await VANTAGE.Repositories.ScheduleRepository
                        .BackfillLocalSnapshotsIfNeededAsync(CurrentUser.Username);
                }

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

                    // Auto-show release notes after an update (must be after splash closes)
                    mainWindow.ShowReleaseNotesIfVersionChanged();
                }
                catch (Exception mainWindowEx)
                {
                    _splashWindow?.Close();
                    AppMessageBox.Show($"Failed to open main window: {mainWindowEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _splashWindow?.Close();
                AppMessageBox.Show($"Application startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                AppMessageBox.Show(
                    "Cannot submit access request while offline.\n\n" +
                    "Please check your internet connection and try again later, " +
                    "or contact your administrator directly.",
                    "Offline",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
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
                    AppMessageBox.Show(
                        "Your access request has been sent to the administrators.\n\n" +
                        "You will receive a response at the email address you provided.",
                        "Request Sent",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
                }
                else
                {
                    AppMessageBox.Show(
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
                AppMessageBox.Show(
                    $"Error sending access request:\n\n{ex.Message}\n\n" +
                    "Please contact your administrator directly.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
