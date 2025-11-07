using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class DeletedRecordsView : Window
    {
        private List<Activity> _deletedActivities;

        public DeletedRecordsView()
        {
            InitializeComponent();
            LoadDeletedRecords();
            LoadAutoPurgeSetting();
        }

        private async void LoadDeletedRecords()
        {
            try
            {
                txtStatus.Text = "Loading deleted records...";
                _deletedActivities = await ActivityRepository.GetDeletedActivitiesAsync();
                sfDeletedActivities.ItemsSource = _deletedActivities;
                txtRecordCount.Text = $"{_deletedActivities.Count} deleted records";
                txtStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading deleted records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error loading records";
            }
        }

        private void LoadAutoPurgeSetting()
        {
            // Load from UserSettings (or AppSettings if you add it)
            string setting = SettingsManager.GetUserSetting(App.CurrentUserID, "AutoPurgeDays") ?? "Never";

            foreach (ComboBoxItem item in cmbAutoPurgeDays.Items)
            {
                if (item.Content.ToString() == setting)
                {
                    cmbAutoPurgeDays.SelectedItem = item;
                    break;
                }
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to restore.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Restore {selectedActivities.Count} record(s) back to the active database?\n\n" +
                $"These records will be available again in the Progress view.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Restoring records...";
                var activityIds = selectedActivities.Select(a => a.ActivityID).ToList();
                int successCount = await ActivityRepository.RestoreActivitiesAsync(activityIds);

                if (successCount > 0)
                {
                    MessageBox.Show($"Successfully restored {successCount} record(s).",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // TODO: Add logging when logging system is implemented
                    // Logger.Info($"Admin {App.CurrentUser.Username} restored {successCount} records");

                    LoadDeletedRecords(); // Refresh grid
                }
                else
                {
                    MessageBox.Show("Failed to restore records.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                txtStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error";
            }
        }

        private async void BtnPurge_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to purge.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"PERMANENTLY DELETE {selectedActivities.Count} record(s)?\n\n" +
                $"⚠️ WARNING: This action CANNOT be undone!\n" +
                $"⚠️ These records will be gone FOREVER!\n\n" +
                $"Are you absolutely sure?",
                "⚠️ PERMANENT DELETION WARNING ⚠️",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Double confirmation for safety
            var doubleCheck = MessageBox.Show(
                "This is your FINAL warning.\n\n" +
                "Click YES to PERMANENTLY DELETE these records.",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (doubleCheck != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Purging records...";
                var activityIds = selectedActivities.Select(a => a.ActivityID).ToList();
                int successCount = await ActivityRepository.PurgeDeletedActivitiesAsync(activityIds);

                if (successCount > 0)
                {
                    MessageBox.Show($"Permanently deleted {successCount} record(s).",
                        "Purged", MessageBoxButton.OK, MessageBoxImage.Information);

                    // TODO: Add logging when logging system is implemented
                    // Logger.Warning($"Admin {App.CurrentUser.Username} purged {successCount} records");

                    LoadDeletedRecords(); // Refresh grid
                }
                else
                {
                    MessageBox.Show("Failed to purge records.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                txtStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error purging records: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error";
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var selectedActivities = sfDeletedActivities.SelectedItems.Cast<Activity>().ToList();

            if (!selectedActivities.Any())
            {
                MessageBox.Show("Please select one or more records to export.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TODO: Implement Excel export for deleted records when ExcelExporter is complete
            MessageBox.Show($"Export {selectedActivities.Count} deleted records feature coming soon!\n\n" +
                "This will export selected deleted records to Excel.",
                "Not Yet Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDeletedRecords();
        }

        private void CmbAutoPurgeDays_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbAutoPurgeDays.SelectedItem == null) return;

            var selected = (cmbAutoPurgeDays.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Save to UserSettings
            SettingsManager.SetUserSetting(App.CurrentUserID, "AutoPurgeDays", selected, "text");

            if (selected != "Never")
            {
                MessageBox.Show(
                    $"Auto-purge enabled: Records older than {selected} will be permanently deleted on app startup.\n\n" +
                    "This helps keep the database clean and performant.",
                    "Auto-Purge Enabled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}