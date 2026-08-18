using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Admin manager for local databases: list, switch (restarts the app), create a
    // new empty database, rename, and delete. Opened from Admin menu -> Database
    // Manager (gated to admins). The active database is tracked in DatabaseRegistry.
    public partial class DatabaseManagerDialog : Window
    {
        // Grid row model. Keeps the underlying manifest FileName so actions can map
        // a selected row back to its registry entry.
        public class DatabaseRow
        {
            public string FileName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public string ActiveMarker => IsActive ? "Yes" : string.Empty; // Yes
            public string ActivitiesText { get; set; } = string.Empty;
            public string SizeText { get; set; } = string.Empty;
            public string LastUsedText { get; set; } = string.Empty;
        }

        private readonly ObservableCollection<DatabaseRow> _rows = new();

        public DatabaseManagerDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            grdDatabases.ItemsSource = _rows;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            SetBusy(true, "Loading databases...");
            try
            {
                await ReloadListAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Rebuild the grid from the manifest. Size and activity counts are read off
        // the UI thread since they touch each database file on disk.
        private async Task ReloadListAsync()
        {
            try
            {
                string? selectedFile = (grdDatabases.SelectedItem as DatabaseRow)?.FileName;

                var rows = await Task.Run(() =>
                {
                    var list = new System.Collections.Generic.List<DatabaseRow>();
                    foreach (var entry in DatabaseRegistry.Load())
                    {
                        string path = DatabaseRegistry.FullPath(entry.FileName);
                        list.Add(new DatabaseRow
                        {
                            FileName = entry.FileName,
                            Name = entry.Name,
                            IsActive = entry.IsActive,
                            ActivitiesText = FormatActivityCount(path),
                            SizeText = FormatSize(path),
                            LastUsedText = FormatTimestamp(entry.LastUsedUtc)
                        });
                    }
                    return list;
                });

                _rows.Clear();
                foreach (var row in rows) _rows.Add(row);

                // Restore selection (or select the active row by default).
                DatabaseRow? toSelect = null;
                foreach (var row in _rows)
                {
                    if (row.FileName == selectedFile) { toSelect = row; break; }
                    if (toSelect == null && row.IsActive) toSelect = row;
                }
                grdDatabases.SelectedItem = toSelect;

                txtStatus.Text = $"{_rows.Count} database(s). Active database is left untouched when you switch.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.ReloadListAsync");
                txtStatus.Text = "Failed to load databases - see log.";
            }
        }

        // === Switch ===

        private void BtnSwitch_Click(object sender, RoutedEventArgs e) => SwitchToSelected();

        private void GrdDatabases_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SwitchToSelected();

        private void SwitchToSelected()
        {
            if (grdDatabases.SelectedItem is not DatabaseRow row)
            {
                AppMessageBox.Show("Select a database to switch to.", "Database Manager",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (row.IsActive)
            {
                AppMessageBox.Show($"'{row.Name}' is already the active database.", "Database Manager",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Do not restart out from under an operation that must finish (Submit Week,
            // snapshot delete, ProgressLog upload, etc.).
            if (LongRunningOps.IsRunning)
            {
                AppMessageBox.Show(
                    "A long-running operation is still in progress. Wait for it to finish before switching databases.",
                    "Operation In Progress", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = AppMessageBox.Show(
                $"Switch to '{row.Name}'?\n\n" +
                "VANTAGE will restart to load it. Your current database is kept exactly as it is - " +
                "nothing is cleared, so you can switch back at any time.",
                "Switch Database", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                DatabaseRegistry.SetActive(row.FileName);
                AppLogger.Info($"Switched active database to '{row.Name}' ({row.FileName})",
                    "DatabaseManagerDialog.SwitchToSelected", App.CurrentUser?.Username ?? "Unknown");
                RestartApplication();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.SwitchToSelected");
                AppMessageBox.Show("Failed to switch databases - see log.", "Database Manager",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === Create ===

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            var input = new InputDialog("Create Database",
                "Name for the new database (e.g. the project it will hold):");
            if (input.ShowDialog(this) != true) return;

            string name = input.InputText;
            if (string.IsNullOrWhiteSpace(name))
            {
                AppMessageBox.Show("Enter a name for the database.", "Create Database",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (DatabaseRegistry.NameExists(name))
            {
                AppMessageBox.Show($"A database named '{name}' already exists. Choose a different name.",
                    "Create Database", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "Creating database...");
            try
            {
                string fileName = DatabaseRegistry.GenerateUniqueFileName(name);
                string path = DatabaseRegistry.FullPath(fileName);

                // Build the full schema in the new file, then register it (inactive).
                await Task.Run(() => DatabaseSetup.InitializeSchemaAtPath(path));

                string now = DateTime.UtcNow.ToString("o");
                DatabaseRegistry.Add(new DatabaseEntry
                {
                    Name = name,
                    FileName = fileName,
                    IsActive = false,
                    CreatedUtc = now,
                    LastUsedUtc = now
                });

                AppLogger.Info($"Created database '{name}' ({fileName})",
                    "DatabaseManagerDialog.BtnCreate_Click", App.CurrentUser?.Username ?? "Unknown");

                await ReloadListAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.BtnCreate_Click");
                AppMessageBox.Show("Failed to create the database - see log.", "Create Database",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SetBusy(false);
                return;
            }
            SetBusy(false);

            var switchNow = AppMessageBox.Show(
                $"'{name}' was created (empty). Switch to it now?\n\n" +
                "You can then sync a project into it. VANTAGE will restart.",
                "Database Created", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (switchNow == MessageBoxResult.Yes)
            {
                var row = FindRow(name);
                if (row != null)
                {
                    try
                    {
                        DatabaseRegistry.SetActive(row.FileName);
                        RestartApplication();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "DatabaseManagerDialog.BtnCreate_Click.Switch");
                    }
                }
            }
        }

        // === Rename ===

        private async void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (grdDatabases.SelectedItem is not DatabaseRow row)
            {
                AppMessageBox.Show("Select a database to rename.", "Database Manager",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var input = new InputDialog("Rename Database", "New name:", row.Name);
            if (input.ShowDialog(this) != true) return;

            string newName = input.InputText;
            if (string.IsNullOrWhiteSpace(newName))
            {
                AppMessageBox.Show("Enter a name.", "Rename Database",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.Equals(newName, row.Name, StringComparison.Ordinal)) return;
            if (DatabaseRegistry.NameExists(newName, row.FileName))
            {
                AppMessageBox.Show($"A database named '{newName}' already exists. Choose a different name.",
                    "Rename Database", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DatabaseRegistry.Rename(row.FileName, newName);
                await ReloadListAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.BtnRename_Click");
                AppMessageBox.Show("Failed to rename - see log.", "Rename Database",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === Delete ===

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (grdDatabases.SelectedItem is not DatabaseRow row)
            {
                AppMessageBox.Show("Select a database to delete.", "Database Manager",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (row.IsActive)
            {
                AppMessageBox.Show(
                    "You can't delete the active database. Switch to another database first, then delete this one.",
                    "Delete Database", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Warn loudly if the target holds unsynced work that would be lost.
            long dirty = await Task.Run(() => CountUnsynced(DatabaseRegistry.FullPath(row.FileName)));
            string dirtyWarning = dirty > 0
                ? $"\n\nWARNING: This database has {dirty:N0} record(s) that have NOT been synced to Azure. " +
                  "Deleting it will lose that work permanently."
                : string.Empty;

            var confirm = AppMessageBox.Show(
                $"Permanently delete '{row.Name}'?\n\n" +
                "The database file will be removed from this computer. This cannot be undone." +
                dirtyWarning,
                "Delete Database", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // Drop pooled handles so the file isn't locked when we delete it.
                SqliteConnection.ClearAllPools();
                DatabaseRegistry.Delete(row.FileName);
                AppLogger.Info($"Deleted database '{row.Name}' ({row.FileName})",
                    "DatabaseManagerDialog.BtnDelete_Click", App.CurrentUser?.Username ?? "Unknown");
                await ReloadListAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.BtnDelete_Click");
                AppMessageBox.Show("Failed to delete the database - see log.", "Delete Database",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === Helpers ===

        private DatabaseRow? FindRow(string name)
        {
            foreach (var row in _rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal)) return row;
            return null;
        }

        private static void RestartApplication()
        {
            // Same restart pattern used by the migration-failure recovery in App.xaml.cs.
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
                Process.Start(exePath);
            Application.Current.Shutdown();
        }

        // Ad-hoc read against a database file with pooling off so the handle is
        // released immediately (important for the delete path).
        private static long QueryScalarLong(string dbPath, string sql)
        {
            if (!File.Exists(dbPath)) return -1;
            using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }

        private static string FormatActivityCount(string dbPath)
        {
            try
            {
                long n = QueryScalarLong(dbPath, "SELECT COUNT(*) FROM Activities");
                return n < 0 ? "-" : n.ToString("N0");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.FormatActivityCount");
                return "-";
            }
        }

        private static long CountUnsynced(string dbPath)
        {
            try
            {
                long n = QueryScalarLong(dbPath, "SELECT COUNT(*) FROM Activities WHERE LocalDirty = 1");
                return n < 0 ? 0 : n;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseManagerDialog.CountUnsynced");
                return 0;
            }
        }

        private static string FormatSize(string dbPath)
        {
            try
            {
                if (!File.Exists(dbPath)) return "-";
                long bytes = new FileInfo(dbPath).Length;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
                if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):N1} MB";
                return $"{bytes / (1024.0 * 1024 * 1024):N2} GB";
            }
            catch
            {
                return "-";
            }
        }

        private static string FormatTimestamp(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "-";
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return "-";
        }

        private void SetBusy(bool busy, string? message = null)
        {
            if (message != null) txtBusy.Text = message;
            pnlBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
