using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Microsoft.Data.SqlClient;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class AdminSnapshotsDialog : Window
    {
        private List<SnapshotGroupItem> _groups = new();

        public AdminSnapshotsDialog()
        {
            InitializeComponent();
            Loaded += AdminSnapshotsDialog_Loaded;
        }

        private async void AdminSnapshotsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSnapshotsAsync();
        }

        private async System.Threading.Tasks.Task LoadSnapshotsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvSnapshots.Visibility = Visibility.Collapsed;
            txtNoSnapshots.Visibility = Visibility.Collapsed;

            try
            {
                _groups = await System.Threading.Tasks.Task.Run(() =>
                {
                    var groupList = new List<SnapshotGroupItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT AssignedTo, ProjectID, WeekEndDate, COUNT(*) as SnapshotCount
                        FROM VMS_ProgressSnapshots
                        GROUP BY AssignedTo, ProjectID, WeekEndDate
                        ORDER BY AssignedTo, ProjectID, WeekEndDate DESC";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string username = reader.IsDBNull(0) ? "(Unknown)" : reader.GetString(0);
                            string projectId = reader.IsDBNull(1) ? "(Unknown)" : reader.GetString(1);
                            string weekEndDateStr = reader.GetString(2);
                            int count = reader.GetInt32(3);

                            if (DateTime.TryParse(weekEndDateStr, out DateTime weekEndDate))
                            {
                                var item = new SnapshotGroupItem
                                {
                                    Username = username,
                                    ProjectID = projectId,
                                    WeekEndDate = weekEndDate,
                                    WeekEndDateStr = weekEndDateStr,
                                    SnapshotCount = count
                                };
                                item.PropertyChanged += GroupItem_PropertyChanged;
                                groupList.Add(item);
                            }
                        }
                    }

                    // Check which groups have been uploaded to ProgressLog
                    // Key on Username + ProjectID + WeekEndDate to match group granularity
                    var uploadedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var uploadCmd = azureConn.CreateCommand())
                    {
                        uploadCmd.CommandText = @"
                            SELECT DISTINCT Username, ProjectID, WeekEndDate
                            FROM VMS_ProgressLogUploads";
                        using var uploadReader = uploadCmd.ExecuteReader();
                        while (uploadReader.Read())
                        {
                            string user = uploadReader.IsDBNull(0) ? "" : uploadReader.GetString(0);
                            string pid = uploadReader.IsDBNull(1) ? "" : uploadReader.GetString(1);
                            string wed = uploadReader.IsDBNull(2) ? "" : uploadReader.GetString(2);
                            uploadedSet.Add($"{user}|{pid}|{wed}");
                        }
                    }

                    foreach (var item in groupList)
                    {
                        item.IsUploaded = uploadedSet.Contains($"{item.Username}|{item.ProjectID}|{item.WeekEndDateStr}");
                    }

                    return groupList;
                });

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_groups.Count == 0)
                {
                    txtNoSnapshots.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                    btnDeleteAll.IsEnabled = false;
                }
                else
                {
                    lvSnapshots.ItemsSource = _groups;
                    lvSnapshots.Visibility = Visibility.Visible;
                    btnDeleteAll.IsEnabled = true;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AdminSnapshotsDialog.LoadSnapshotsAsync");
                MessageBox.Show($"Error loading snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GroupItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapshotGroupItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedGroups = _groups.Where(g => g.IsSelected).ToList();
            int groupCount = selectedGroups.Count;
            int snapshotCount = selectedGroups.Sum(g => g.SnapshotCount);

            txtSelectionSummary.Text = $"{groupCount} group(s) selected ({snapshotCount} snapshots)";
            btnDelete.IsEnabled = groupCount > 0;
            btnUploadToProgressLog.IsEnabled = groupCount > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _groups)
            {
                group.IsSelected = true;
            }
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _groups)
            {
                group.IsSelected = false;
            }
        }

        // Upload selected snapshots to VANTAGE_global_ProgressLog on Azure
        private async void BtnUploadToProgressLog_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroups = _groups.Where(g => g.IsSelected).ToList();

            if (selectedGroups.Count == 0)
            {
                MessageBox.Show("Please select at least one group to upload.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Azure connection
            if (!AzureDbManager.CheckConnection(out string errorMessage))
            {
                MessageBox.Show($"Cannot connect to Azure database:\n\n{errorMessage}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int totalSnapshots = selectedGroups.Sum(g => g.SnapshotCount);
            string currentUser = App.CurrentUser?.Username ?? Environment.UserName;

            // Check for existing uploads (duplicate warning)
            try
            {
                var duplicateWarnings = await System.Threading.Tasks.Task.Run(() =>
                {
                    var warnings = new List<string>();
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    foreach (var group in selectedGroups)
                    {
                        using var checkCmd = conn.CreateCommand();
                        checkCmd.CommandText = @"
                            SELECT COUNT(*), MAX(UploadUtcDate)
                            FROM VMS_ProgressLogUploads
                            WHERE ProjectID = @projectId
                              AND WeekEndDate = @weekEndDate";
                        checkCmd.Parameters.AddWithValue("@projectId", group.ProjectID);
                        checkCmd.Parameters.AddWithValue("@weekEndDate", group.WeekEndDateStr);

                        using var reader = checkCmd.ExecuteReader();
                        if (reader.Read() && reader.GetInt32(0) > 0)
                        {
                            string lastUpload = reader.IsDBNull(1) ? "unknown" : reader.GetString(1);
                            warnings.Add($"  {group.ProjectID} / {group.WeekEndDateDisplay} (last uploaded: {lastUpload})");
                        }
                    }
                    return warnings;
                });

                if (duplicateWarnings.Count > 0)
                {
                    string warningList = string.Join("\n", duplicateWarnings);
                    var dupResult = MessageBox.Show(
                        $"The following groups have already been uploaded to the Progress Log:\n\n{warningList}\n\n" +
                        "Uploading again will create duplicate records in the Progress Log. Continue?",
                        "Duplicate Upload Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (dupResult != MessageBoxResult.Yes)
                        return;
                }
            }
            catch (Exception ex)
            {
                // Don't block upload if duplicate check fails
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnUploadToProgressLog_Click.DuplicateCheck");
            }

            // Confirm upload
            var confirmResult = MessageBox.Show(
                $"Upload {totalSnapshots} snapshot(s) to VANTAGE_global_ProgressLog?\n\n" +
                $"Continue?",
                "Confirm Upload",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            // Disable buttons and show loading overlay
            btnUploadToProgressLog.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnDeleteAll.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ShowOperationLoading($"Uploading {totalSnapshots} snapshot(s) to Progress Log...");

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                int uploadedCount = await System.Threading.Tasks.Task.Run(() =>
                    UploadSnapshotsToProgressLog(selectedGroups, currentUser));
                stopwatch.Stop();

                MessageBox.Show(
                    $"Successfully uploaded {uploadedCount} snapshot(s) to Progress Log.\n\n" +
                    $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F1} seconds",
                    "Upload Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Mark uploaded groups in the UI
                foreach (var group in selectedGroups)
                    group.IsUploaded = true;
                lvSnapshots.Items.Refresh();

                AppLogger.Info(
                    $"Admin uploaded {uploadedCount} snapshots to ProgressLog",
                    "AdminSnapshotsDialog.BtnUploadToProgressLog_Click",
                    currentUser);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnUploadToProgressLog_Click");
                MessageBox.Show($"Error uploading snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOperationLoading();
                btnCancel.IsEnabled = true;
                UpdateSelectionSummary();
            }
        }

        // Performs the actual upload to VANTAGE_global_ProgressLog
        private int UploadSnapshotsToProgressLog(List<SnapshotGroupItem> selectedGroups, string currentUser)
        {
            var uploadTimestamp = DateTime.Now;

            using var azureConn = AzureDbManager.GetConnection();
            azureConn.Open();

            // Load column mappings (ColumnName -> AzureName where AzureName is not empty)
            var columnMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var mappingCmd = azureConn.CreateCommand())
            {
                mappingCmd.CommandText = @"
                    SELECT ColumnName, AzureName
                    FROM VMS_ColumnMappings
                    WHERE AzureName IS NOT NULL AND AzureName <> ''";
                using var reader = mappingCmd.ExecuteReader();
                while (reader.Read())
                {
                    string colName = reader.GetString(0).Trim();
                    string azureName = reader.GetString(1).Trim();
                    if (colName.Length > 0 && azureName.Length > 0 && !columnMappings.ContainsKey(colName))
                        columnMappings[colName] = azureName;
                }
            }

            // Load ProgressLog column max lengths for string truncation in SQL
            var columnMaxLengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var schemaCmd = azureConn.CreateCommand())
            {
                schemaCmd.CommandText = @"
                    SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'VANTAGE_global_ProgressLog'
                      AND DATA_TYPE IN ('nvarchar', 'varchar')
                      AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL";
                using var schemaReader = schemaCmd.ExecuteReader();
                while (schemaReader.Read())
                {
                    string colName = schemaReader.GetString(0);
                    int maxLen = schemaReader.GetInt32(1);
                    columnMaxLengths[colName] = maxLen;
                }
            }

            // Build WHERE clause for selected groups
            var whereClauses = new List<string>();
            var insertCmd = azureConn.CreateCommand();
            int paramIndex = 0;
            foreach (var group in selectedGroups)
            {
                whereClauses.Add($"(AssignedTo = @u{paramIndex} AND ProjectID = @p{paramIndex} AND WeekEndDate = @w{paramIndex})");
                insertCmd.Parameters.AddWithValue($"@u{paramIndex}", group.Username);
                insertCmd.Parameters.AddWithValue($"@p{paramIndex}", group.ProjectID);
                insertCmd.Parameters.AddWithValue($"@w{paramIndex}", group.WeekEndDateStr);
                paramIndex++;
            }
            string whereClause = string.Join(" OR ", whereClauses);

            // Get actual columns in VMS_ProgressSnapshots so we only map columns that exist
            var snapshotColumnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var colCmd = azureConn.CreateCommand())
            {
                colCmd.CommandText = @"
                    SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'VMS_ProgressSnapshots'";
                using var colReader = colCmd.ExecuteReader();
                while (colReader.Read())
                    snapshotColumnSet.Add(colReader.GetString(0));
            }

            // System columns that should never be sent to ProgressLog
            var excludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ActivityID", "UniqueID", "LocalDirty", "SyncVersion",
                "ExportedBy", "ExportedDate", "UpdatedBy", "UpdatedUtcDate",
                "CreatedBy", "CreatedDate", "AzureUploadUtcDate", "IsDeleted"
            };

            // Build dynamic INSERT...SELECT - data stays server-side, no client round-trip
            var insertCols = new List<string>();
            var selectExprs = new List<string>();

            foreach (var mapping in columnMappings)
            {
                string snapshotCol = mapping.Key;
                string azureCol = mapping.Value;

                // Skip system/internal columns and columns not in the snapshot table
                if (excludedColumns.Contains(snapshotCol) || !snapshotColumnSet.Contains(snapshotCol))
                    continue;

                insertCols.Add($"[{azureCol}]");

                // PercentEntry is 0-100 in Milestone but ProgressLog expects 0-1 decimal
                if (snapshotCol.Equals("PercentEntry", StringComparison.OrdinalIgnoreCase))
                {
                    selectExprs.Add("ISNULL([PercentEntry], 0) / 100.0");
                }
                // Apply LEFT() truncation for nvarchar columns with known max lengths
                else if (columnMaxLengths.TryGetValue(azureCol, out int maxLen) && maxLen > 0)
                    selectExprs.Add($"LEFT(CAST([{snapshotCol}] AS NVARCHAR(MAX)), {maxLen})");
                else
                    selectExprs.Add($"[{snapshotCol}]");
            }

            // Add calculated columns
            insertCols.Add("[Sch_Status]");
            selectExprs.Add("CASE WHEN ISNULL(PercentEntry, 0) = 0 THEN 'Not Started' WHEN PercentEntry >= 100 THEN 'Complete' ELSE 'In Progress' END");

            insertCols.Add("[Val_EarnedHours_Ind]");
            selectExprs.Add("CAST(ROUND(ISNULL(BudgetMHs, 0) * ISNULL(PercentEntry, 0) / 100.0, 3) AS NVARCHAR(50))");

            insertCols.Add("[Val_Client_Earned_EQ-QTY]");
            selectExprs.Add("CASE WHEN ISNULL(BudgetMHs, 0) > 0 AND ISNULL(PercentEntry, 0) > 0 THEN CAST(ROUND((ISNULL(PercentEntry, 0) / 100.0) * ISNULL(ClientEquivQty, 0), 3) AS NVARCHAR(50)) ELSE '0' END");

            insertCols.Add("[UserID]");
            selectExprs.Add("@userId");
            insertCmd.Parameters.AddWithValue("@userId", currentUser);

            insertCols.Add("[Timestamp]");
            selectExprs.Add("@uploadTimestamp");
            insertCmd.Parameters.AddWithValue("@uploadTimestamp", uploadTimestamp);

            insertCmd.CommandTimeout = 0;
            string sql = $@"
                INSERT INTO VANTAGE_global_ProgressLog ({string.Join(", ", insertCols)})
                SELECT {string.Join(", ", selectExprs)}
                FROM VMS_ProgressSnapshots
                WHERE {whereClause}";
            insertCmd.CommandText = sql;
            AppLogger.Info($"ProgressLog INSERT...SELECT: {insertCols.Count} columns", "AdminSnapshotsDialog.UploadSnapshotsToProgressLog");

            int insertedCount = insertCmd.ExecuteNonQuery();
            insertCmd.Dispose();

            if (insertedCount == 0)
                return 0;

            // Insert tracking records into VMS_ProgressLogUploads
            // One row per unique RespParty within each selected snapshot group
            try
            {
                string uploadUtcDateStr = uploadTimestamp.ToString("M/d/yyyy h:mm:ss tt");

                foreach (var group in selectedGroups)
                {
                    // Query distinct RespParty values and counts for this group's snapshots
                    using var respCmd = azureConn.CreateCommand();
                    respCmd.CommandText = @"
                        SELECT RespParty, COUNT(*) as RecordCount
                        FROM VMS_ProgressSnapshots
                        WHERE AssignedTo = @username
                          AND ProjectID = @projectId
                          AND WeekEndDate = @weekEndDate
                        GROUP BY RespParty";
                    respCmd.Parameters.AddWithValue("@username", group.Username);
                    respCmd.Parameters.AddWithValue("@projectId", group.ProjectID);
                    respCmd.Parameters.AddWithValue("@weekEndDate", group.WeekEndDateStr);

                    using var respReader = respCmd.ExecuteReader();
                    var respGroups = new List<(string RespParty, int Count)>();
                    while (respReader.Read())
                    {
                        string respParty = respReader.IsDBNull(0) ? "" : respReader.GetString(0);
                        int count = respReader.GetInt32(1);
                        respGroups.Add((respParty, count));
                    }
                    respReader.Close();

                    foreach (var (respParty, count) in respGroups)
                    {
                        using var trackCmd = azureConn.CreateCommand();
                        trackCmd.CommandText = @"
                            INSERT INTO VMS_ProgressLogUploads
                                (ProjectID, RespParty, WeekEndDate, UploadUtcDate, RecordCount, Username, UploadedBy)
                            VALUES
                                (@projectId, @respParty, @weekEndDate, @uploadUtcDate, @recordCount, @username, @uploadedBy)";
                        trackCmd.Parameters.AddWithValue("@projectId", group.ProjectID);
                        trackCmd.Parameters.AddWithValue("@respParty", respParty);
                        trackCmd.Parameters.AddWithValue("@weekEndDate", group.WeekEndDateStr);
                        trackCmd.Parameters.AddWithValue("@uploadUtcDate", uploadUtcDateStr);
                        trackCmd.Parameters.AddWithValue("@recordCount", count);
                        trackCmd.Parameters.AddWithValue("@username", group.Username);
                        trackCmd.Parameters.AddWithValue("@uploadedBy", currentUser);
                        trackCmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't fail the upload if tracking insert fails
                AppLogger.Error(ex, "AdminSnapshotsDialog.UploadSnapshotsToProgressLog.TrackingInsert");
            }

            // Update AzureUploadUtcDate on VMS_Activities for uploaded records
            // Server-side update using same WHERE clause - no client-side ID tracking needed
            using (var updateCmd = azureConn.CreateCommand())
            {
                updateCmd.CommandTimeout = 0;

                // Rebuild parameters for the subquery
                int upIdx = 0;
                var updateWhereClauses = new List<string>();
                foreach (var group in selectedGroups)
                {
                    updateWhereClauses.Add($"(AssignedTo = @uu{upIdx} AND ProjectID = @up{upIdx} AND WeekEndDate = @uw{upIdx})");
                    updateCmd.Parameters.AddWithValue($"@uu{upIdx}", group.Username);
                    updateCmd.Parameters.AddWithValue($"@up{upIdx}", group.ProjectID);
                    updateCmd.Parameters.AddWithValue($"@uw{upIdx}", group.WeekEndDateStr);
                    upIdx++;
                }

                updateCmd.CommandText = $@"
                    UPDATE VMS_Activities
                    SET AzureUploadUtcDate = @uploadDate
                    WHERE UniqueID IN (
                        SELECT UniqueID FROM VMS_ProgressSnapshots
                        WHERE {string.Join(" OR ", updateWhereClauses)}
                    )";
                updateCmd.Parameters.AddWithValue("@uploadDate", uploadTimestamp);
                updateCmd.ExecuteNonQuery();
            }

            return insertedCount;
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroups = _groups.Where(g => g.IsSelected).ToList();

            if (selectedGroups.Count == 0)
            {
                MessageBox.Show("Please select at least one group to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalSnapshots = selectedGroups.Sum(g => g.SnapshotCount);

            var groupSummary = selectedGroups
                .GroupBy(g => g.Username)
                .Select(u => $"  {u.Key}: {u.Sum(g => g.SnapshotCount)} snapshots")
                .ToList();

            string summaryText = string.Join("\n", groupSummary);

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to delete {totalSnapshots} snapshot(s)?\n\n" +
                $"By user:\n{summaryText}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            btnDeleteAll.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ShowOperationLoading($"Deleting {totalSnapshots} snapshot(s)...");

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    int deleted = 0;

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    foreach (var group in selectedGroups)
                    {
                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            DELETE FROM VMS_ProgressSnapshots
                            WHERE AssignedTo = @username
                              AND ProjectID = @projectId
                              AND WeekEndDate = @weekEndDate";
                        cmd.Parameters.AddWithValue("@username", group.Username);
                        cmd.Parameters.AddWithValue("@projectId", group.ProjectID);
                        cmd.Parameters.AddWithValue("@weekEndDate", group.WeekEndDateStr);

                        deleted += cmd.ExecuteNonQuery();
                    }

                    return deleted;
                });

                AppLogger.Info(
                    $"Admin deleted {deletedTotal} snapshots from {selectedGroups.Count} group(s)",
                    "AdminSnapshotsDialog.BtnDelete_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload the list
                await LoadSnapshotsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOperationLoading();
                btnCancel.IsEnabled = true;
            }
        }

        private async void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            int totalSnapshots = _groups.Sum(g => g.SnapshotCount);

            var confirmResult = MessageBox.Show(
                $"⚠️ DANGER: This will delete ALL {totalSnapshots} snapshot(s) from ALL users!\n\n" +
                "This action cannot be undone.\n\n" +
                "Are you absolutely sure?",
                "Delete All Snapshots",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            // Second confirmation - type DELETE
            if (!ShowDeleteConfirmation())
                return;

            btnDelete.IsEnabled = false;
            btnDeleteAll.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ShowOperationLoading($"Deleting all {totalSnapshots} snapshot(s)...");

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = "DELETE FROM VMS_ProgressSnapshots";
                    return cmd.ExecuteNonQuery();
                });

                AppLogger.Info(
                    $"Admin deleted ALL snapshots: {deletedTotal} total",
                    "AdminSnapshotsDialog.BtnDeleteAll_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted all {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload the list
                await LoadSnapshotsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnDeleteAll_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOperationLoading();
                btnCancel.IsEnabled = true;
            }
        }

        // Shows the loading overlay with a custom message
        private void ShowOperationLoading(string message)
        {
            txtOperationStatus.Text = message;
            pnlOperationLoading.Visibility = Visibility.Visible;
        }

        // Hides the loading overlay
        private void HideOperationLoading()
        {
            pnlOperationLoading.Visibility = Visibility.Collapsed;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private bool ShowDeleteConfirmation()
        {
            var confirmDialog = new Window
            {
                Title = "Confirm Delete All",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = ThemeHelper.BackgroundColor,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Type DELETE to confirm:",
                Foreground = ThemeHelper.ForegroundColor,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var textBox = new System.Windows.Controls.TextBox
            {
                Height = 30,
                FontSize = 14,
                Background = ThemeHelper.ControlBackground,
                Foreground = ThemeHelper.ForegroundColor,
                BorderBrush = ThemeHelper.SidebarBorder,
                Padding = new Thickness(5)
            };
            stack.Children.Add(textBox);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            bool confirmed = false;

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = ThemeHelper.SidebarBorder,
                Foreground = ThemeHelper.ForegroundColor
            };
            btnCancel.Click += (s, args) => confirmDialog.Close();

            var btnConfirm = new System.Windows.Controls.Button
            {
                Content = "Confirm",
                Width = 80,
                Height = 30,
                Background = ThemeHelper.ButtonDangerBackground,
                Foreground = ThemeHelper.ForegroundColor
            };
            btnConfirm.Click += (s, args) =>
            {
                if (textBox.Text == "DELETE")
                {
                    confirmed = true;
                    confirmDialog.Close();
                }
                else
                {
                    MessageBox.Show("You must type DELETE exactly.", "Invalid",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnConfirm);
            stack.Children.Add(btnPanel);

            confirmDialog.Content = stack;
            confirmDialog.ShowDialog();

            return confirmed;
        }
    }

    // Model for snapshot group
    public class SnapshotGroupItem : INotifyPropertyChanged
    {
        public string Username { get; set; } = string.Empty;
        public string ProjectID { get; set; } = string.Empty;
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

        public bool IsUploaded { get; set; }

        public string WeekEndDateDisplay => WeekEndDate.ToString("MM/dd/yyyy");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}