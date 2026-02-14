using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Models;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ProrateDialog : Window
    {
        private readonly List<Activity> _allFilteredActivities;
        private readonly Action _refreshCallback;
        private List<Activity> _eligibleActivities = new();
        private bool _includePlaceholders = false;
        private bool _placeholderPromptShown = false;
        private bool _isLoaded = false;

        public int UpdatedCount { get; private set; }

        public ProrateDialog(List<Activity> filteredActivities, Action refreshCallback)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _allFilteredActivities = filteredActivities;
            _refreshCallback = refreshCallback;
            Loaded += (s, e) => { _isLoaded = true; UpdatePreview(); };
        }

        // Calculate which activities are eligible based on current settings
        private List<Activity> GetEligibleActivities()
        {
            var eligible = new List<Activity>();
            bool keepEarned = rbKeepEarned.IsChecked == true;

            foreach (var activity in _allFilteredActivities)
            {
                // Skip if not owned by current user (unless admin)
                if (!App.CurrentUser!.IsAdmin &&
                    !string.Equals(activity.AssignedTo, App.CurrentUser.Username, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip 100% complete activities if keeping earned (no room to adjust)
                if (keepEarned && activity.PercentEntry >= 100)
                    continue;

                // Skip placeholders if user chose not to include them
                if (!_includePlaceholders && activity.BudgetMHs < 0.01)
                    continue;

                eligible.Add(activity);
            }

            return eligible;
        }

        // Count placeholder activities (BudgetMHs < 0.01)
        private int CountPlaceholders()
        {
            bool keepEarned = rbKeepEarned.IsChecked == true;
            return _allFilteredActivities.Count(a =>
                a.BudgetMHs < 0.01 &&
                (App.CurrentUser!.IsAdmin ||
                 string.Equals(a.AssignedTo, App.CurrentUser.Username, StringComparison.OrdinalIgnoreCase)) &&
                (!keepEarned || a.PercentEntry < 100));
        }

        private void UpdatePreview()
        {
            if (!_isLoaded) return;

            _eligibleActivities = GetEligibleActivities();
            double currentTotal = _eligibleActivities.Sum(a => a.BudgetMHs);

            txtEligibleCount.Text = _eligibleActivities.Count.ToString("N0");
            txtCurrentTotal.Text = currentTotal.ToString("N3");

            // Calculate new total based on operation
            double newTotal = currentTotal;
            if (double.TryParse(txtAmount.Text, out double amount) && amount > 0)
            {
                if (rbNewTotal.IsChecked == true)
                    newTotal = amount;
                else if (rbAdd.IsChecked == true)
                    newTotal = currentTotal + amount;
                else if (rbSubtract.IsChecked == true)
                    newTotal = Math.Max(0, currentTotal - amount);

                txtNewTotal.Text = newTotal.ToString("N3");
                btnApply.IsEnabled = _eligibleActivities.Count > 0 && newTotal > 0;
            }
            else
            {
                txtNewTotal.Text = currentTotal.ToString("N3");
                btnApply.IsEnabled = false;
            }

            // Show skipped info
            UpdateSkippedInfo();
        }

        private void UpdateSkippedInfo()
        {
            var messages = new List<string>();

            // Count 100% complete that would be skipped
            if (rbKeepEarned.IsChecked == true)
            {
                int completeCount = _allFilteredActivities.Count(a =>
                    a.PercentEntry >= 100 &&
                    (App.CurrentUser!.IsAdmin ||
                     string.Equals(a.AssignedTo, App.CurrentUser.Username, StringComparison.OrdinalIgnoreCase)));
                if (completeCount > 0)
                    messages.Add($"{completeCount} complete (100%) activities will be skipped");
            }

            // Count non-owned activities
            if (!App.CurrentUser!.IsAdmin)
            {
                int nonOwnedCount = _allFilteredActivities.Count(a =>
                    !string.Equals(a.AssignedTo, App.CurrentUser.Username, StringComparison.OrdinalIgnoreCase));
                if (nonOwnedCount > 0)
                    messages.Add($"{nonOwnedCount} activities owned by others will be skipped");
            }

            // Count placeholders if not included
            if (!_includePlaceholders)
            {
                int placeholderCount = CountPlaceholders();
                if (placeholderCount > 0)
                    messages.Add($"{placeholderCount} placeholder activities (< 0.01 MHs) excluded");
            }

            txtSkippedInfo.Text = messages.Count > 0 ? string.Join("; ", messages) : "";
        }

        private void Operation_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void Preserve_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void TxtAmount_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        // Only allow digits and one decimal point
        private void TxtAmount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string newChar = e.Text;

            // Allow digits
            if (char.IsDigit(newChar, 0))
            {
                e.Handled = false;
                return;
            }

            // Allow one decimal point if not already present
            if (newChar == "." && !txtAmount.Text.Contains('.'))
            {
                e.Handled = false;
                return;
            }

            // Block everything else
            e.Handled = true;
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Check for placeholders before applying
            if (!_placeholderPromptShown)
            {
                int placeholderCount = CountPlaceholders();
                if (placeholderCount > 0)
                {
                    var result = MessageBox.Show(
                        $"{placeholderCount} activities have < 0.01 MHs and may be placeholders.\n\nInclude them in the prorate?",
                        "Placeholder Activities",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    _includePlaceholders = (result == MessageBoxResult.Yes);
                    _placeholderPromptShown = true;
                    UpdatePreview();

                    // Re-check if we still have eligible activities
                    if (_eligibleActivities.Count == 0)
                    {
                        MessageBox.Show("No eligible activities to prorate.", "No Activities",
                            MessageBoxButton.OK, MessageBoxImage.None);
                        return;
                    }
                }
            }

            if (!double.TryParse(txtAmount.Text, out double amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid positive amount.", "Invalid Amount",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAmount.Focus();
                return;
            }

            // Calculate new total
            double currentTotal = _eligibleActivities.Sum(a => a.BudgetMHs);
            double newTotal;
            if (rbNewTotal.IsChecked == true)
                newTotal = amount;
            else if (rbAdd.IsChecked == true)
                newTotal = currentTotal + amount;
            else
                newTotal = currentTotal - amount;

            // Validate new total
            if (newTotal <= 0)
            {
                MessageBox.Show("Cannot reduce total to zero or negative.", "Invalid Operation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Calculate change to distribute
            double totalChange = newTotal - currentTotal;

            // Confirm operation
            string operation = rbNewTotal.IsChecked == true ? "set to" :
                              (rbAdd.IsChecked == true ? "add" : "subtract");
            string preserve = rbKeepPercent.IsChecked == true ? "Keep Percent Complete" : "Keep Earned MHs";

            var confirmResult = MessageBox.Show(
                $"Prorate {_eligibleActivities.Count} activities?\n\n" +
                $"Operation: {operation} {amount:N3} MHs\n" +
                $"Current Total: {currentTotal:N3} MHs\n" +
                $"New Total: {newTotal:N3} MHs\n" +
                $"Preserve: {preserve}",
                "Confirm Prorate",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.OK)
                return;

            btnApply.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                bool keepPercent = rbKeepPercent.IsChecked == true;
                int successCount = 0;
                int clampedCount = 0;

                foreach (var activity in _eligibleActivities)
                {
                    // Calculate this activity's share of the change
                    double share = currentTotal > 0 ? activity.BudgetMHs / currentTotal : 0;
                    double activityChange = totalChange * share;
                    double newBudgetMHs = activity.BudgetMHs + activityChange;

                    // Enforce minimum constraint
                    if (newBudgetMHs < 0.001)
                    {
                        newBudgetMHs = 0.001;
                        clampedCount++;
                    }

                    // Round to 3 decimal places
                    newBudgetMHs = Math.Round(newBudgetMHs, 3);

                    // Store current earned for Keep Earned mode
                    double currentEarned = activity.EarnMHsCalc;

                    // Update BudgetMHs
                    activity.BudgetMHs = newBudgetMHs;

                    // Recalculate based on preserve mode
                    if (keepPercent)
                    {
                        // PercentEntry stays the same, EarnMHsCalc recalculates automatically
                        // (EarnMHsCalc is a calculated property: PercentEntry / 100 * BudgetMHs)
                    }
                    else
                    {
                        // Keep Earned - recalculate PercentEntry
                        if (newBudgetMHs > 0)
                        {
                            activity.PercentEntry = Math.Round((currentEarned / newBudgetMHs) * 100, 3);
                            // Cap at 100%
                            if (activity.PercentEntry > 100)
                                activity.PercentEntry = 100;
                        }
                    }

                    // Update tracking fields
                    activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                    activity.UpdatedUtcDate = DateTime.UtcNow;
                    activity.LocalDirty = 1;

                    // Save to database
                    bool success = await ActivityRepository.UpdateActivityInDatabase(activity);
                    if (success) successCount++;
                }

                UpdatedCount = successCount;

                // Log the operation
                AppLogger.Info(
                    $"Prorated {successCount} activities: {operation} {amount:N3} MHs, preserve={preserve}",
                    "ProrateDialog.BtnApply_Click",
                    App.CurrentUser?.Username);

                // Show result
                string message = $"Successfully updated {successCount} activities.";
                if (clampedCount > 0)
                    message += $"\n\n{clampedCount} activities were clamped to minimum 0.001 MHs.";

                MessageBox.Show(message, "Prorate Complete",
                    MessageBoxButton.OK, MessageBoxImage.None);

                // Refresh the grid
                _refreshCallback?.Invoke();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProrateDialog.BtnApply_Click");
                MessageBox.Show($"Error applying prorate:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnApply.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
