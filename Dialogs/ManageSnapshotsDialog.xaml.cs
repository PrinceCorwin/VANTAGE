using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using VANTAGE.Data;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;
using VANTAGE.Views;

namespace VANTAGE.Dialogs
{
    public partial class ManageSnapshotsDialog : Window
    {
        private List<SnapshotWeekItem> _weeks = new();

        public ManageSnapshotsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ManageSnapshotsDialog_Loaded;
        }

        private async void ManageSnapshotsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null)
            {
                MessageBox.Show("No user logged in.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            txtUserInfo.Text = $"Snapshots for: {App.CurrentUser.Username}";
            await LoadSnapshotsAsync();
        }

        private async System.Threading.Tasks.Task LoadSnapshotsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvWeeks.Visibility = Visibility.Collapsed;
            txtNoSnapshots.Visibility = Visibility.Collapsed;

            try
            {
                _weeks = await System.Threading.Tasks.Task.Run(() =>
                {
                    var weekList = new List<SnapshotWeekItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT WeekEndDate, COUNT(*) as SnapshotCount
                        FROM VMS_ProgressSnapshots
                        WHERE AssignedTo = @username
                        GROUP BY WeekEndDate
                        ORDER BY WeekEndDate DESC";
                    cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string weekEndDateStr = reader.GetString(0);
                        int count = reader.GetInt32(1);

                        if (DateTime.TryParse(weekEndDateStr, out DateTime weekEndDate))
                        {
                            var item = new SnapshotWeekItem
                            {
                                WeekEndDate = weekEndDate,
                                WeekEndDateStr = weekEndDateStr,
                                SnapshotCount = count
                            };
                            item.PropertyChanged += WeekItem_PropertyChanged;
                            weekList.Add(item);
                        }
                    }

                    return weekList;
                });

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_weeks.Count == 0)
                {
                    txtNoSnapshots.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                }
                else
                {
                    lvWeeks.ItemsSource = _weeks;
                    lvWeeks.Visibility = Visibility.Visible;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "ManageSnapshotsDialog.LoadSnapshotsAsync");
                MessageBox.Show($"Error loading snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WeekItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapshotWeekItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();
            int weekCount = selectedWeeks.Count;
            int snapshotCount = selectedWeeks.Sum(w => w.SnapshotCount);

            txtSelectionSummary.Text = $"{weekCount} week(s) selected ({snapshotCount:N0} snapshots)";
            btnDelete.IsEnabled = weekCount > 0;    // 1 or more weeks selected
            btnRevert.IsEnabled = weekCount == 1;   // exactly 1 week selected
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();

            if (selectedWeeks.Count == 0)
            {
                MessageBox.Show("Please select at least one week to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalSnapshots = selectedWeeks.Sum(w => w.SnapshotCount);
            string weekList = string.Join("\n", selectedWeeks.Select(w => $"  - {w.WeekEndDateDisplay} ({w.SnapshotCount} snapshots)"));

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to delete {totalSnapshots} snapshot(s) from the following weeks?\n\n{weekList}\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            btnRevert.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    int deleted = 0;

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    foreach (var week in selectedWeeks)
                    {
                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            DELETE FROM VMS_ProgressSnapshots
                            WHERE AssignedTo = @username
                              AND WeekEndDate = @weekEndDate";
                        cmd.Parameters.AddWithValue("@username", App.CurrentUser!.Username);
                        cmd.Parameters.AddWithValue("@weekEndDate", week.WeekEndDateStr);

                        deleted += cmd.ExecuteNonQuery();
                    }

                    return deleted;
                });

                AppLogger.Info(
                    $"Deleted {deletedTotal} snapshots from {selectedWeeks.Count} week(s)",
                    "ManageSnapshotsDialog.BtnDelete_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageSnapshotsDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                btnCancel.IsEnabled = true;
                UpdateSelectionSummary(); // Re-enables delete/revert based on selection
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #region Revert Logic

        private async void BtnRevert_Click(object sender, RoutedEventArgs e)
        {
            // Validate exactly 1 week selected
            var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();

            if (selectedWeeks.Count == 0)
            {
                MessageBox.Show("Please select a snapshot week to revert to.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedWeeks.Count > 1)
            {
                MessageBox.Show("Please select only one snapshot week to revert to.", "Multiple Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedWeek = selectedWeeks[0];
            string currentUser = App.CurrentUser!.Username;

            // Show warning dialog with backup option
            var warningResult = ShowRevertWarningDialog(selectedWeek);
            if (warningResult == RevertWarningResult.Cancel)
                return;

            // Disable buttons during operation
            btnDelete.IsEnabled = false;
            btnRevert.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                var busyDialog = new BusyDialog(this, "Preparing revert...");
                busyDialog.Show();

                // Create backup if requested
                if (warningResult == RevertWarningResult.CreateBackupFirst)
                {
                    busyDialog.UpdateStatus("Creating backup snapshot...");
                    var backupResult = await CreateBackupSnapshotAsync(currentUser);

                    if (!backupResult.Success)
                    {
                        busyDialog.Close();
                        MessageBox.Show($"Failed to create backup snapshot:\n{backupResult.ErrorMessage}\n\nRevert cancelled.",
                            "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    busyDialog.UpdateStatus($"Backup created ({backupResult.Count} records)...");
                }

                // Pre-sync: push pending changes, then pull
                busyDialog.UpdateStatus("Syncing pending changes...");

                if (!AzureDbManager.CheckConnection(out string connError))
                {
                    busyDialog.Close();
                    MessageBox.Show($"Cannot connect to Azure:\n{connError}\n\nRevert cancelled.",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var userProjects = await GetUserProjectsAsync(currentUser);
                if (userProjects.Count > 0)
                {
                    await SyncManager.PushRecordsAsync(userProjects);
                    await SyncManager.PullRecordsAsync(userProjects, currentUser);
                }

                // Execute revert
                busyDialog.UpdateStatus("Loading snapshot data...");
                var revertResult = await ExecuteRevertAsync(selectedWeek.WeekEndDateStr, currentUser,
                    status => Dispatcher.Invoke(() => busyDialog.UpdateStatus(status)));

                if (!revertResult.Success)
                {
                    busyDialog.Close();
                    MessageBox.Show($"Revert failed:\n{revertResult.ErrorMessage}",
                        "Revert Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                busyDialog.Close();

                // Show results
                ShowRevertResultsDialog(revertResult);

                // Refresh ProgressView if loaded (MainWindow handles this on dialog close)
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageSnapshotsDialog.BtnRevert_Click");
                MessageBox.Show($"Error during revert:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCancel.IsEnabled = true;
                UpdateSelectionSummary();
            }
        }

        // Shows warning dialog with backup options, returns user choice
        private RevertWarningResult ShowRevertWarningDialog(SnapshotWeekItem selectedWeek)
        {
            var result = RevertWarningResult.Cancel;

            var dialog = new Window
            {
                Title = "Revert to Snapshot",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = (Brush)Application.Current.Resources["BackgroundColor"]
            };

            Syncfusion.SfSkinManager.SfSkinManager.SetTheme(dialog,
                new Syncfusion.SfSkinManager.Theme(ThemeManager.GetSyncfusionThemeName()));

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Warning header
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "\u26A0",
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "REVERT TO SNAPSHOT",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Message
            var messageText = $"This will replace your current activity records with data from:\n\n" +
                              $"    Week ending: {selectedWeek.WeekEndDate:MM/dd/yyyy}\n" +
                              $"    Records: {selectedWeek.SnapshotCount:N0} snapshots\n\n" +
                              $"Your current progress will be OVERWRITTEN.\n" +
                              $"Records you no longer own will be skipped.\n\n" +
                              $"Would you like to create a backup snapshot first?";

            var messageBlock = new TextBlock
            {
                Text = messageText,
                Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            };
            Grid.SetRow(messageBlock, 1);
            grid.Children.Add(messageBlock);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var btnCancelDialog = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBorder"]
            };
            btnCancelDialog.Click += (s, e) => { result = RevertWarningResult.Cancel; dialog.Close(); };

            var btnSkip = new Button
            {
                Content = "Skip Backup",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBorder"]
            };
            btnSkip.Click += (s, e) => { result = RevertWarningResult.SkipBackup; dialog.Close(); };

            var btnBackup = new Button
            {
                Content = "Create Backup First",
                Width = 140,
                Height = 35,
                Background = (Brush)Application.Current.Resources["AccentColor"],
                Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (Brush)Application.Current.Resources["AccentColor"]
            };
            btnBackup.Click += (s, e) => { result = RevertWarningResult.CreateBackupFirst; dialog.Close(); };

            buttonPanel.Children.Add(btnCancelDialog);
            buttonPanel.Children.Add(btnSkip);
            buttonPanel.Children.Add(btnBackup);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();

            return result;
        }

        // Creates backup snapshot using today's date
        private async Task<(bool Success, int Count, string? ErrorMessage)> CreateBackupSnapshotAsync(string username)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string backupWeekEndDate = DateTime.Today.ToString("yyyy-MM-dd");

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    // Insert current Activities as backup snapshots
                    var cmd = azureConn.CreateCommand();
                    cmd.CommandTimeout = 0;
                    cmd.CommandText = @"
                        INSERT INTO VMS_ProgressSnapshots (
                            UniqueID, WeekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                            Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                            ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                            DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                            EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                            MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                            PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                            ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                            ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                            Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo, SystemNO, TagNO,
                            UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                            UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                            UpdatedBy, UpdatedUtcDate, UOM, WorkPackage, XRay
                        )
                        SELECT
                            UniqueID, @weekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                            Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                            ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                            DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                            EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                            MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                            PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                            ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                            ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                            Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo, SystemNO, TagNO,
                            UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                            UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                            @updatedBy, @updatedUtcDate, UOM, WorkPackage, XRay
                        FROM VMS_Activities
                        WHERE AssignedTo = @username
                          AND IsDeleted = 0
                          AND NOT EXISTS (
                              SELECT 1 FROM VMS_ProgressSnapshots ps
                              WHERE ps.UniqueID = VMS_Activities.UniqueID
                                AND ps.WeekEndDate = @weekEndDate
                          )";

                    cmd.Parameters.AddWithValue("@weekEndDate", backupWeekEndDate);
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@updatedBy", username);
                    cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                    int count = cmd.ExecuteNonQuery();

                    AppLogger.Info($"Created backup snapshot with {count} records for {backupWeekEndDate}",
                        "ManageSnapshotsDialog.CreateBackupSnapshotAsync", username);

                    return (true, count, null);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ManageSnapshotsDialog.CreateBackupSnapshotAsync");
                    return (false, 0, ex.Message);
                }
            });
        }

        // Gets distinct ProjectIDs for current user
        private async Task<List<string>> GetUserProjectsAsync(string username)
        {
            return await Task.Run(() =>
            {
                var projects = new List<string>();

                try
                {
                    using var conn = DatabaseSetup.GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT DISTINCT ProjectID
                        FROM Activities
                        WHERE AssignedTo = @username
                          AND ProjectID IS NOT NULL
                          AND ProjectID != ''";
                    cmd.Parameters.AddWithValue("@username", username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        projects.Add(reader.GetString(0));
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ManageSnapshotsDialog.GetUserProjectsAsync");
                }

                return projects;
            });
        }

        // Executes the revert operation
        private async Task<RevertResult> ExecuteRevertAsync(string weekEndDate, string username, Action<string> updateStatus)
        {
            var result = new RevertResult();

            try
            {
                // Load snapshot records from Azure
                updateStatus("Loading snapshot records from Azure...");
                var snapshots = await LoadSnapshotsFromAzureAsync(weekEndDate, username);

                if (snapshots.Count == 0)
                {
                    result.ErrorMessage = "No snapshot records found for the selected week.";
                    return result;
                }

                updateStatus($"Found {snapshots.Count:N0} snapshot records...");

                // Get current ownership state from local database
                updateStatus("Checking current record ownership...");
                var currentState = await GetCurrentActivityStateAsync(snapshots.Select(s => s.UniqueID).ToList());

                // Categorize records
                var toRestore = new List<SnapshotData>();

                foreach (var snapshot in snapshots)
                {
                    if (!currentState.TryGetValue(snapshot.UniqueID, out var currentOwner))
                    {
                        result.SkippedRecords.Add(new SkippedRecordItem
                        {
                            UniqueID = snapshot.UniqueID,
                            Reason = "Record no longer exists"
                        });
                    }
                    else if (!string.Equals(currentOwner, username, StringComparison.OrdinalIgnoreCase))
                    {
                        result.SkippedRecords.Add(new SkippedRecordItem
                        {
                            UniqueID = snapshot.UniqueID,
                            Reason = $"Now assigned to {currentOwner}"
                        });
                    }
                    else
                    {
                        toRestore.Add(snapshot);
                    }
                }

                if (toRestore.Count == 0)
                {
                    result.ErrorMessage = "No records can be restored. All records have either changed ownership or no longer exist.";
                    return result;
                }

                // Update local Activities with snapshot values
                updateStatus($"Restoring {toRestore.Count:N0} records...");
                int restored = await RestoreActivitiesAsync(toRestore, username, updateStatus);

                result.RestoredCount = restored;
                result.Success = true;

                AppLogger.Info(
                    $"Reverted to snapshot {weekEndDate}: {restored} restored, {result.SkippedRecords.Count} skipped",
                    "ManageSnapshotsDialog.ExecuteRevertAsync", username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageSnapshotsDialog.ExecuteRevertAsync");
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        // Loads snapshot data from Azure
        private async Task<List<SnapshotData>> LoadSnapshotsFromAzureAsync(string weekEndDate, string username)
        {
            return await Task.Run(() =>
            {
                var snapshots = new List<SnapshotData>();

                using var azureConn = AzureDbManager.GetConnection();
                azureConn.Open();

                var cmd = azureConn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        UniqueID, Area, AzureUploadUtcDate,
                        Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                        ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                        DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                        EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                        MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                        PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                        ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                        ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                        Service, ShopField, ShtNO, SubArea, PjtSystem, PjtSystemNo, SystemNO, TagNO,
                        UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                        UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                        UOM, WorkPackage, XRay
                    FROM VMS_ProgressSnapshots
                    WHERE AssignedTo = @username
                      AND WeekEndDate = @weekEndDate";
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    snapshots.Add(MapReaderToSnapshotData(reader));
                }

                return snapshots;
            });
        }

        // Maps SqlDataReader to SnapshotData
        private static SnapshotData MapReaderToSnapshotData(SqlDataReader reader)
        {
            return new SnapshotData
            {
                UniqueID = GetStringOrEmpty(reader, 0),
                Area = GetStringOrEmpty(reader, 1),
                AzureUploadUtcDate = GetNullableString(reader, 2),
                Aux1 = GetStringOrEmpty(reader, 3),
                Aux2 = GetStringOrEmpty(reader, 4),
                Aux3 = GetStringOrEmpty(reader, 5),
                BaseUnit = GetDoubleOrZero(reader, 6),
                BudgetHoursGroup = GetDoubleOrZero(reader, 7),
                BudgetHoursROC = GetDoubleOrZero(reader, 8),
                BudgetMHs = GetDoubleOrZero(reader, 9),
                ChgOrdNO = GetStringOrEmpty(reader, 10),
                ClientBudget = GetDoubleOrZero(reader, 11),
                ClientCustom3 = GetDoubleOrZero(reader, 12),
                ClientEquivQty = GetDoubleOrZero(reader, 13),
                CompType = GetStringOrEmpty(reader, 14),
                CreatedBy = GetStringOrEmpty(reader, 15),
                DateTrigger = GetIntOrZero(reader, 16),
                Description = GetStringOrEmpty(reader, 17),
                DwgNO = GetStringOrEmpty(reader, 18),
                EarnQtyEntry = GetDoubleOrZero(reader, 19),
                EarnedMHsRoc = GetDoubleOrZero(reader, 20),
                EqmtNO = GetStringOrEmpty(reader, 21),
                EquivQTY = GetStringOrEmpty(reader, 22),
                EquivUOM = GetStringOrEmpty(reader, 23),
                Estimator = GetStringOrEmpty(reader, 24),
                HexNO = GetIntOrZero(reader, 25),
                HtTrace = GetStringOrEmpty(reader, 26),
                InsulType = GetStringOrEmpty(reader, 27),
                LineNumber = GetStringOrEmpty(reader, 28),
                MtrlSpec = GetStringOrEmpty(reader, 29),
                Notes = GetStringOrEmpty(reader, 30),
                PaintCode = GetStringOrEmpty(reader, 31),
                PercentEntry = GetDoubleOrZero(reader, 32),
                PhaseCategory = GetStringOrEmpty(reader, 33),
                PhaseCode = GetStringOrEmpty(reader, 34),
                PipeGrade = GetStringOrEmpty(reader, 35),
                PipeSize1 = GetDoubleOrZero(reader, 36),
                PipeSize2 = GetDoubleOrZero(reader, 37),
                PrevEarnMHs = GetDoubleOrZero(reader, 38),
                PrevEarnQTY = GetDoubleOrZero(reader, 39),
                ProgDate = GetNullableString(reader, 40),
                ProjectID = GetStringOrEmpty(reader, 41),
                Quantity = GetDoubleOrZero(reader, 42),
                RevNO = GetStringOrEmpty(reader, 43),
                RFINO = GetStringOrEmpty(reader, 44),
                ROCBudgetQTY = GetDoubleOrZero(reader, 45),
                ROCID = GetStringOrEmpty(reader, 46),
                ROCPercent = GetDoubleOrZero(reader, 47),
                ROCStep = GetStringOrEmpty(reader, 48),
                SchedActNO = GetStringOrEmpty(reader, 49),
                SchFinish = GetNullableString(reader, 50),
                SchStart = GetNullableString(reader, 51),
                SecondActno = GetStringOrEmpty(reader, 52),
                SecondDwgNO = GetStringOrEmpty(reader, 53),
                Service = GetStringOrEmpty(reader, 54),
                ShopField = GetStringOrEmpty(reader, 55),
                ShtNO = GetStringOrEmpty(reader, 56),
                SubArea = GetStringOrEmpty(reader, 57),
                PjtSystem = GetStringOrEmpty(reader, 58),
                PjtSystemNo = GetStringOrEmpty(reader, 59),
                SystemNO = GetStringOrEmpty(reader, 60),
                TagNO = GetStringOrEmpty(reader, 61),
                UDF1 = GetStringOrEmpty(reader, 62),
                UDF2 = GetStringOrEmpty(reader, 63),
                UDF3 = GetStringOrEmpty(reader, 64),
                UDF4 = GetStringOrEmpty(reader, 65),
                UDF5 = GetStringOrEmpty(reader, 66),
                UDF6 = GetStringOrEmpty(reader, 67),
                UDF7 = GetStringOrEmpty(reader, 68),
                UDF8 = GetStringOrEmpty(reader, 69),
                UDF9 = GetStringOrEmpty(reader, 70),
                UDF10 = GetStringOrEmpty(reader, 71),
                UDF11 = GetStringOrEmpty(reader, 72),
                UDF12 = GetStringOrEmpty(reader, 73),
                UDF13 = GetStringOrEmpty(reader, 74),
                UDF14 = GetStringOrEmpty(reader, 75),
                UDF15 = GetStringOrEmpty(reader, 76),
                UDF16 = GetStringOrEmpty(reader, 77),
                UDF17 = GetStringOrEmpty(reader, 78),
                RespParty = GetStringOrEmpty(reader, 79),
                UDF20 = GetStringOrEmpty(reader, 80),
                UOM = GetStringOrEmpty(reader, 81),
                WorkPackage = GetStringOrEmpty(reader, 82),
                XRay = GetStringOrEmpty(reader, 83)
            };
        }

        // Helper for safe value reading - handles any data type
        private static string GetStringOrEmpty(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return string.Empty;
            var value = reader.GetValue(ordinal);
            return value?.ToString() ?? string.Empty;
        }

        private static string? GetNullableString(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            var value = reader.GetValue(ordinal);
            return value?.ToString();
        }

        private static double GetDoubleOrZero(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return 0;
            try { return Convert.ToDouble(reader.GetValue(ordinal)); }
            catch { return 0; }
        }

        private static int GetIntOrZero(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return 0;
            try { return Convert.ToInt32(reader.GetValue(ordinal)); }
            catch { return 0; }
        }

        // Gets current ownership state for given UniqueIDs
        private async Task<Dictionary<string, string>> GetCurrentActivityStateAsync(List<string> uniqueIds)
        {
            return await Task.Run(() =>
            {
                var ownership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using var conn = DatabaseSetup.GetConnection();
                conn.Open();

                // Process in batches to avoid parameter limits
                const int batchSize = 500;
                for (int i = 0; i < uniqueIds.Count; i += batchSize)
                {
                    var batch = uniqueIds.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
                        SELECT UniqueID, AssignedTo
                        FROM Activities
                        WHERE UniqueID IN ({placeholders})";

                    for (int j = 0; j < batch.Count; j++)
                    {
                        cmd.Parameters.AddWithValue($"@id{j}", batch[j]);
                    }

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string uniqueId = reader.GetString(0);
                        string assignedTo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        ownership[uniqueId] = assignedTo;
                    }
                }

                return ownership;
            });
        }

        // Restores activities with snapshot values
        private async Task<int> RestoreActivitiesAsync(List<SnapshotData> snapshots, string username, Action<string> updateStatus)
        {
            return await Task.Run(() =>
            {
                int restored = 0;
                string updatedUtcDate = DateTime.UtcNow.ToString("o");

                using var conn = DatabaseSetup.GetConnection();
                conn.Open();

                using var transaction = conn.BeginTransaction();

                try
                {
                    int processed = 0;
                    foreach (var snapshot in snapshots)
                    {
                        var cmd = conn.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = BuildRestoreUpdateSql();
                        AddRestoreParameters(cmd, snapshot, username, updatedUtcDate);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0) restored++;

                        processed++;
                        if (processed % 100 == 0)
                        {
                            updateStatus($"Restored {processed:N0} of {snapshots.Count:N0} records...");
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                return restored;
            });
        }

        // Builds UPDATE SQL for restoring all fields
        private static string BuildRestoreUpdateSql()
        {
            return @"
                UPDATE Activities SET
                    Area = @area,
                    Aux1 = @aux1,
                    Aux2 = @aux2,
                    Aux3 = @aux3,
                    BaseUnit = @baseUnit,
                    BudgetHoursGroup = @budgetHoursGroup,
                    BudgetHoursROC = @budgetHoursROC,
                    BudgetMHs = @budgetMHs,
                    ChgOrdNO = @chgOrdNO,
                    ClientBudget = @clientBudget,
                    ClientCustom3 = @clientCustom3,
                    ClientEquivQty = @clientEquivQty,
                    CompType = @compType,
                    DateTrigger = @dateTrigger,
                    Description = @description,
                    DwgNO = @dwgNO,
                    EarnQtyEntry = @earnQtyEntry,
                    EarnedMHsRoc = @earnedMHsRoc,
                    EqmtNO = @eqmtNO,
                    EquivQTY = @equivQTY,
                    EquivUOM = @equivUOM,
                    Estimator = @estimator,
                    HexNO = @hexNO,
                    HtTrace = @htTrace,
                    InsulType = @insulType,
                    LineNumber = @lineNumber,
                    MtrlSpec = @mtrlSpec,
                    Notes = @notes,
                    PaintCode = @paintCode,
                    PercentEntry = @percentEntry,
                    PhaseCategory = @phaseCategory,
                    PhaseCode = @phaseCode,
                    PipeGrade = @pipeGrade,
                    PipeSize1 = @pipeSize1,
                    PipeSize2 = @pipeSize2,
                    PrevEarnMHs = @prevEarnMHs,
                    PrevEarnQTY = @prevEarnQTY,
                    ProgDate = @progDate,
                    ProjectID = @projectID,
                    Quantity = @quantity,
                    RevNO = @revNO,
                    RFINO = @rfiNO,
                    ROCBudgetQTY = @rocBudgetQTY,
                    ROCID = @rocID,
                    ROCPercent = @rocPercent,
                    ROCStep = @rocStep,
                    SchedActNO = @schedActNO,
                    SchFinish = @schFinish,
                    SchStart = @schStart,
                    SecondActno = @secondActno,
                    SecondDwgNO = @secondDwgNO,
                    Service = @service,
                    ShopField = @shopField,
                    ShtNO = @shtNO,
                    SubArea = @subArea,
                    PjtSystem = @pjtSystem,
                    PjtSystemNo = @pjtSystemNo,
                    SystemNO = @systemNO,
                    TagNO = @tagNO,
                    UDF1 = @udf1,
                    UDF2 = @udf2,
                    UDF3 = @udf3,
                    UDF4 = @udf4,
                    UDF5 = @udf5,
                    UDF6 = @udf6,
                    UDF7 = @udf7,
                    UDF8 = @udf8,
                    UDF9 = @udf9,
                    UDF10 = @udf10,
                    UDF11 = @udf11,
                    UDF12 = @udf12,
                    UDF13 = @udf13,
                    UDF14 = @udf14,
                    UDF15 = @udf15,
                    UDF16 = @udf16,
                    UDF17 = @udf17,
                    RespParty = @respParty,
                    UDF20 = @udf20,
                    UOM = @uom,
                    WorkPackage = @workPackage,
                    XRay = @xRay,
                    UpdatedBy = @updatedBy,
                    UpdatedUtcDate = @updatedUtcDate,
                    LocalDirty = 1
                WHERE UniqueID = @uniqueId";
        }

        // Adds parameters for restore UPDATE
        private static void AddRestoreParameters(SqliteCommand cmd, SnapshotData snapshot, string username, string updatedUtcDate)
        {
            cmd.Parameters.AddWithValue("@uniqueId", snapshot.UniqueID);
            cmd.Parameters.AddWithValue("@area", snapshot.Area);
            cmd.Parameters.AddWithValue("@aux1", snapshot.Aux1);
            cmd.Parameters.AddWithValue("@aux2", snapshot.Aux2);
            cmd.Parameters.AddWithValue("@aux3", snapshot.Aux3);
            cmd.Parameters.AddWithValue("@baseUnit", snapshot.BaseUnit);
            cmd.Parameters.AddWithValue("@budgetHoursGroup", snapshot.BudgetHoursGroup);
            cmd.Parameters.AddWithValue("@budgetHoursROC", snapshot.BudgetHoursROC);
            cmd.Parameters.AddWithValue("@budgetMHs", snapshot.BudgetMHs);
            cmd.Parameters.AddWithValue("@chgOrdNO", snapshot.ChgOrdNO);
            cmd.Parameters.AddWithValue("@clientBudget", snapshot.ClientBudget);
            cmd.Parameters.AddWithValue("@clientCustom3", snapshot.ClientCustom3);
            cmd.Parameters.AddWithValue("@clientEquivQty", snapshot.ClientEquivQty);
            cmd.Parameters.AddWithValue("@compType", snapshot.CompType);
            cmd.Parameters.AddWithValue("@dateTrigger", snapshot.DateTrigger);
            cmd.Parameters.AddWithValue("@description", snapshot.Description);
            cmd.Parameters.AddWithValue("@dwgNO", snapshot.DwgNO);
            cmd.Parameters.AddWithValue("@earnQtyEntry", snapshot.EarnQtyEntry);
            cmd.Parameters.AddWithValue("@earnedMHsRoc", snapshot.EarnedMHsRoc);
            cmd.Parameters.AddWithValue("@eqmtNO", snapshot.EqmtNO);
            cmd.Parameters.AddWithValue("@equivQTY", snapshot.EquivQTY);
            cmd.Parameters.AddWithValue("@equivUOM", snapshot.EquivUOM);
            cmd.Parameters.AddWithValue("@estimator", snapshot.Estimator);
            cmd.Parameters.AddWithValue("@hexNO", snapshot.HexNO);
            cmd.Parameters.AddWithValue("@htTrace", snapshot.HtTrace);
            cmd.Parameters.AddWithValue("@insulType", snapshot.InsulType);
            cmd.Parameters.AddWithValue("@lineNumber", snapshot.LineNumber);
            cmd.Parameters.AddWithValue("@mtrlSpec", snapshot.MtrlSpec);
            cmd.Parameters.AddWithValue("@notes", snapshot.Notes);
            cmd.Parameters.AddWithValue("@paintCode", snapshot.PaintCode);
            cmd.Parameters.AddWithValue("@percentEntry", snapshot.PercentEntry);
            cmd.Parameters.AddWithValue("@phaseCategory", snapshot.PhaseCategory);
            cmd.Parameters.AddWithValue("@phaseCode", snapshot.PhaseCode);
            cmd.Parameters.AddWithValue("@pipeGrade", snapshot.PipeGrade);
            cmd.Parameters.AddWithValue("@pipeSize1", snapshot.PipeSize1);
            cmd.Parameters.AddWithValue("@pipeSize2", snapshot.PipeSize2);
            cmd.Parameters.AddWithValue("@prevEarnMHs", snapshot.PrevEarnMHs);
            cmd.Parameters.AddWithValue("@prevEarnQTY", snapshot.PrevEarnQTY);
            cmd.Parameters.AddWithValue("@progDate", snapshot.ProgDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@projectID", snapshot.ProjectID);
            cmd.Parameters.AddWithValue("@quantity", snapshot.Quantity);
            cmd.Parameters.AddWithValue("@revNO", snapshot.RevNO);
            cmd.Parameters.AddWithValue("@rfiNO", snapshot.RFINO);
            cmd.Parameters.AddWithValue("@rocBudgetQTY", snapshot.ROCBudgetQTY);
            cmd.Parameters.AddWithValue("@rocID", snapshot.ROCID);
            cmd.Parameters.AddWithValue("@rocPercent", snapshot.ROCPercent);
            cmd.Parameters.AddWithValue("@rocStep", snapshot.ROCStep);
            cmd.Parameters.AddWithValue("@schedActNO", snapshot.SchedActNO);
            cmd.Parameters.AddWithValue("@schFinish", snapshot.SchFinish ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@schStart", snapshot.SchStart ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@secondActno", snapshot.SecondActno);
            cmd.Parameters.AddWithValue("@secondDwgNO", snapshot.SecondDwgNO);
            cmd.Parameters.AddWithValue("@service", snapshot.Service);
            cmd.Parameters.AddWithValue("@shopField", snapshot.ShopField);
            cmd.Parameters.AddWithValue("@shtNO", snapshot.ShtNO);
            cmd.Parameters.AddWithValue("@subArea", snapshot.SubArea);
            cmd.Parameters.AddWithValue("@pjtSystem", snapshot.PjtSystem);
            cmd.Parameters.AddWithValue("@pjtSystemNo", snapshot.PjtSystemNo);
            cmd.Parameters.AddWithValue("@systemNO", snapshot.SystemNO);
            cmd.Parameters.AddWithValue("@tagNO", snapshot.TagNO);
            cmd.Parameters.AddWithValue("@udf1", snapshot.UDF1);
            cmd.Parameters.AddWithValue("@udf2", snapshot.UDF2);
            cmd.Parameters.AddWithValue("@udf3", snapshot.UDF3);
            cmd.Parameters.AddWithValue("@udf4", snapshot.UDF4);
            cmd.Parameters.AddWithValue("@udf5", snapshot.UDF5);
            cmd.Parameters.AddWithValue("@udf6", snapshot.UDF6);
            cmd.Parameters.AddWithValue("@udf7", snapshot.UDF7);
            cmd.Parameters.AddWithValue("@udf8", snapshot.UDF8);
            cmd.Parameters.AddWithValue("@udf9", snapshot.UDF9);
            cmd.Parameters.AddWithValue("@udf10", snapshot.UDF10);
            cmd.Parameters.AddWithValue("@udf11", snapshot.UDF11);
            cmd.Parameters.AddWithValue("@udf12", snapshot.UDF12);
            cmd.Parameters.AddWithValue("@udf13", snapshot.UDF13);
            cmd.Parameters.AddWithValue("@udf14", snapshot.UDF14);
            cmd.Parameters.AddWithValue("@udf15", snapshot.UDF15);
            cmd.Parameters.AddWithValue("@udf16", snapshot.UDF16);
            cmd.Parameters.AddWithValue("@udf17", snapshot.UDF17);
            cmd.Parameters.AddWithValue("@respParty", snapshot.RespParty);
            cmd.Parameters.AddWithValue("@udf20", snapshot.UDF20);
            cmd.Parameters.AddWithValue("@uom", snapshot.UOM);
            cmd.Parameters.AddWithValue("@workPackage", snapshot.WorkPackage);
            cmd.Parameters.AddWithValue("@xRay", snapshot.XRay);
            cmd.Parameters.AddWithValue("@updatedBy", username);
            cmd.Parameters.AddWithValue("@updatedUtcDate", updatedUtcDate);
        }

        // Shows revert results dialog
        private void ShowRevertResultsDialog(RevertResult result)
        {
            string message = $"Revert Complete\n\n" +
                             $"Restored: {result.RestoredCount:N0} records";

            if (result.SkippedRecords.Count > 0)
            {
                int ownershipSkipped = result.SkippedRecords.Count(r => r.Reason.StartsWith("Now assigned to"));
                int notFoundSkipped = result.SkippedRecords.Count(r => r.Reason == "Record no longer exists");

                if (ownershipSkipped > 0)
                    message += $"\nSkipped (ownership changed): {ownershipSkipped:N0}";
                if (notFoundSkipped > 0)
                    message += $"\nSkipped (no longer exists): {notFoundSkipped:N0}";

                message += "\n\nWould you like to view the skipped records?";

                var viewResult = MessageBox.Show(message, "Revert Complete",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (viewResult == MessageBoxResult.Yes)
                {
                    var skippedDialog = new SkippedRecordsDialog(result.SkippedRecords);
                    skippedDialog.Owner = this;
                    skippedDialog.ShowDialog();
                }
            }
            else
            {
                MessageBox.Show(message, "Revert Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }

    #region Supporting Types

    // Result of warning dialog
    public enum RevertWarningResult
    {
        Cancel,
        CreateBackupFirst,
        SkipBackup
    }

    // Result of revert operation
    public class RevertResult
    {
        public bool Success { get; set; }
        public int RestoredCount { get; set; }
        public List<SkippedRecordItem> SkippedRecords { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    // Data container for snapshot fields to restore
    public class SnapshotData
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string? AzureUploadUtcDate { get; set; }
        public string Aux1 { get; set; } = string.Empty;
        public string Aux2 { get; set; } = string.Empty;
        public string Aux3 { get; set; } = string.Empty;
        public double BaseUnit { get; set; }
        public double BudgetHoursGroup { get; set; }
        public double BudgetHoursROC { get; set; }
        public double BudgetMHs { get; set; }
        public string ChgOrdNO { get; set; } = string.Empty;
        public double ClientBudget { get; set; }
        public double ClientCustom3 { get; set; }
        public double ClientEquivQty { get; set; }
        public string CompType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public int DateTrigger { get; set; }
        public string Description { get; set; } = string.Empty;
        public string DwgNO { get; set; } = string.Empty;
        public double EarnQtyEntry { get; set; }
        public double EarnedMHsRoc { get; set; }
        public string EqmtNO { get; set; } = string.Empty;
        public string EquivQTY { get; set; } = string.Empty;
        public string EquivUOM { get; set; } = string.Empty;
        public string Estimator { get; set; } = string.Empty;
        public int HexNO { get; set; }
        public string HtTrace { get; set; } = string.Empty;
        public string InsulType { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;
        public string MtrlSpec { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string PaintCode { get; set; } = string.Empty;
        public double PercentEntry { get; set; }
        public string PhaseCategory { get; set; } = string.Empty;
        public string PhaseCode { get; set; } = string.Empty;
        public string PipeGrade { get; set; } = string.Empty;
        public double PipeSize1 { get; set; }
        public double PipeSize2 { get; set; }
        public double PrevEarnMHs { get; set; }
        public double PrevEarnQTY { get; set; }
        public string? ProgDate { get; set; }
        public string ProjectID { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string RevNO { get; set; } = string.Empty;
        public string RFINO { get; set; } = string.Empty;
        public double ROCBudgetQTY { get; set; }
        public string ROCID { get; set; } = string.Empty;
        public double ROCPercent { get; set; }
        public string ROCStep { get; set; } = string.Empty;
        public string SchedActNO { get; set; } = string.Empty;
        public string? SchFinish { get; set; }
        public string? SchStart { get; set; }
        public string SecondActno { get; set; } = string.Empty;
        public string SecondDwgNO { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string ShopField { get; set; } = string.Empty;
        public string ShtNO { get; set; } = string.Empty;
        public string SubArea { get; set; } = string.Empty;
        public string PjtSystem { get; set; } = string.Empty;
        public string PjtSystemNo { get; set; } = string.Empty;
        public string SystemNO { get; set; } = string.Empty;
        public string TagNO { get; set; } = string.Empty;
        public string UDF1 { get; set; } = string.Empty;
        public string UDF2 { get; set; } = string.Empty;
        public string UDF3 { get; set; } = string.Empty;
        public string UDF4 { get; set; } = string.Empty;
        public string UDF5 { get; set; } = string.Empty;
        public string UDF6 { get; set; } = string.Empty;
        public string UDF7 { get; set; } = string.Empty;
        public string UDF8 { get; set; } = string.Empty;
        public string UDF9 { get; set; } = string.Empty;
        public string UDF10 { get; set; } = string.Empty;
        public string UDF11 { get; set; } = string.Empty;
        public string UDF12 { get; set; } = string.Empty;
        public string UDF13 { get; set; } = string.Empty;
        public string UDF14 { get; set; } = string.Empty;
        public string UDF15 { get; set; } = string.Empty;
        public string UDF16 { get; set; } = string.Empty;
        public string UDF17 { get; set; } = string.Empty;
        public string RespParty { get; set; } = string.Empty;
        public string UDF20 { get; set; } = string.Empty;
        public string UOM { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public string XRay { get; set; } = string.Empty;
    }

    #endregion

    // Model for week selection
    public class SnapshotWeekItem : INotifyPropertyChanged
    {
        public DateTime WeekEndDate { get; set; }
        public string WeekEndDateStr { get; set; } = string.Empty;
        public int SnapshotCount { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string WeekEndDateDisplay => $"Week ending {WeekEndDate:MM/dd/yyyy}";
        public string SnapshotCountDisplay => $"({SnapshotCount} snapshots)";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}