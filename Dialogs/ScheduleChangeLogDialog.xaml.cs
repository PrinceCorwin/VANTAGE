using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using VANTAGE.Data;
using VANTAGE.Models;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog for viewing and applying Schedule detail grid changes to Activities
    public partial class ScheduleChangeLogDialog : Window
    {
        private List<ScheduleChangeLogEntry> _changes = new();

        // Indicates if any changes were applied to Activities
        public bool ChangesApplied { get; private set; }

        public ScheduleChangeLogDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ScheduleChangeLogDialog_Loaded;
        }

        private void ScheduleChangeLogDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadChanges();
        }

        private void LoadChanges()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvChanges.Visibility = Visibility.Collapsed;
            txtNoChanges.Visibility = Visibility.Collapsed;

            try
            {
                _changes = ScheduleChangeLogger.ReadAllEntries();

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_changes.Count == 0)
                {
                    txtNoChanges.Visibility = Visibility.Visible;
                    btnApply.IsEnabled = false;
                }
                else
                {
                    lvChanges.ItemsSource = _changes;
                    lvChanges.Visibility = Visibility.Visible;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "ScheduleChangeLogDialog.LoadChanges");
                MessageBox.Show($"Error loading changes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectionSummary()
        {
            int selectedCount = _changes.Count(c => c.IsSelected);
            txtSelectionSummary.Text = $"{selectedCount} of {_changes.Count} change(s) selected";
            btnApply.IsEnabled = selectedCount > 0;
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var change in _changes)
                change.IsSelected = true;

            lvChanges.Items.Refresh();
            UpdateSelectionSummary();
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var change in _changes)
                change.IsSelected = false;

            lvChanges.Items.Refresh();
            UpdateSelectionSummary();
        }

        private void ItemCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectionSummary();

            // Update select all checkbox state
            int selectedCount = _changes.Count(c => c.IsSelected);
            if (selectedCount == 0)
                chkSelectAll.IsChecked = false;
            else if (selectedCount == _changes.Count)
                chkSelectAll.IsChecked = true;
            else
                chkSelectAll.IsChecked = null;
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selected = _changes.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No changes selected.", "Apply Changes",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            // Group by UniqueID+Field and take only the most recent change for each
            // This handles the case where the same field was edited multiple times
            var changesToApply = selected
                .GroupBy(c => new { c.UniqueID, c.Field })
                .Select(g => g.OrderByDescending(c => c.Timestamp).First())
                .ToList();

            int skippedCount = selected.Count - changesToApply.Count;
            string confirmMessage = $"Apply {changesToApply.Count} change(s) to live Activities?";
            if (skippedCount > 0)
                confirmMessage += $"\n\n({skippedCount} older duplicate change(s) will be skipped)";
            confirmMessage += "\n\nThis will overwrite the current values in the Activities table. This action cannot be undone.";

            var result = MessageBox.Show(confirmMessage, "Confirm Apply",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            btnApply.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                int successCount = 0;
                int failCount = 0;
                string username = App.CurrentUser?.Username ?? "Unknown";

                // Apply only the most recent change for each UniqueID+Field
                foreach (var change in changesToApply)
                {
                    bool success = await ApplyChangeToActivity(change, username);
                    if (success)
                        successCount++;
                    else
                        failCount++;
                }

                // Remove ALL selected entries from log files (including older duplicates)
                if (successCount > 0)
                {
                    ScheduleChangeLogger.RemoveAppliedEntries(selected);
                    ChangesApplied = true;
                }

                string message = $"Applied {successCount} change(s) to Activities.";
                if (skippedCount > 0)
                    message += $"\n{skippedCount} older duplicate(s) were skipped.";
                if (failCount > 0)
                    message += $"\n{failCount} change(s) failed (activity may not exist).";

                MessageBox.Show(message, "Apply Complete",
                    MessageBoxButton.OK, MessageBoxImage.None);

                // Reload to show remaining changes
                LoadChanges();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleChangeLogDialog.BtnApply_Click");
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnApply.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        // Applies a single change to the corresponding Activity in the local database
        private async Task<bool> ApplyChangeToActivity(ScheduleChangeLogEntry change, string username)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    // First check if activity exists
                    var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM Activities WHERE UniqueID = @uniqueId";
                    checkCmd.Parameters.AddWithValue("@uniqueId", change.UniqueID);
                    long count = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0);

                    if (count == 0)
                        return false;

                    // Build update command based on field
                    var updateCmd = connection.CreateCommand();
                    string columnName = change.Field;

                    // Map field names to column names and handle value conversion
                    object? newValue = null;
                    switch (columnName)
                    {
                        case "PercentEntry":
                            newValue = string.IsNullOrEmpty(change.NewValue) ? 0.0 :
                                double.TryParse(change.NewValue, out double pct) ? pct : 0.0;
                            break;
                        case "BudgetMHs":
                            newValue = string.IsNullOrEmpty(change.NewValue) ? 0.0 :
                                double.TryParse(change.NewValue, out double mhs) ? mhs : 0.0;
                            break;
                        case "ActStart":
                        case "ActFin":
                            // Dates are stored as TEXT
                            newValue = string.IsNullOrEmpty(change.NewValue) ? DBNull.Value : change.NewValue;
                            break;
                        default:
                            return false; // Unknown field
                    }

                    updateCmd.CommandText = $@"
                        UPDATE Activities
                        SET {columnName} = @newValue,
                            UpdatedBy = @updatedBy,
                            UpdatedUtcDate = @updatedDate,
                            LocalDirty = 1
                        WHERE UniqueID = @uniqueId";

                    updateCmd.Parameters.AddWithValue("@newValue", newValue ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@updatedBy", username);
                    updateCmd.Parameters.AddWithValue("@updatedDate", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("@uniqueId", change.UniqueID);

                    int rows = updateCmd.ExecuteNonQuery();
                    return rows > 0;
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"ScheduleChangeLogDialog.ApplyChangeToActivity: {change.UniqueID}");
                return false;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
