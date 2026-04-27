using System.Collections.Generic;
using System.Windows;
using System.Linq;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;
using VANTAGE;

namespace VANTAGE.Dialogs
{
    public partial class FindReplaceDialog : Syncfusion.Windows.Shared.ChromelessWindow
    {
        private Syncfusion.UI.Xaml.Grid.SfDataGrid _dataGrid = null!;
        private string _columnMappingName = null!;

        public FindReplaceDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
        }
        
        public void SetTargetColumn(Syncfusion.UI.Xaml.Grid.SfDataGrid dataGrid, string columnMappingName, string columnHeaderText)
        {
            _dataGrid = dataGrid;
            _columnMappingName = columnMappingName;
            txtColumnName.Text = $"Column: {columnHeaderText}";
        }

        // Enable/disable controls based on Replace All Cells checkbox
        private void ChkReplaceAllCells_Changed(object sender, RoutedEventArgs e)
        {
            bool replaceAll = chkReplaceAllCells.IsChecked == true;

            // Mutually exclusive with Find Blanks
            if (replaceAll && chkFindBlanks.IsChecked == true)
            {
                chkFindBlanks.IsChecked = false;
            }

            UpdateFindControlsState();
        }

        // Enable/disable Find textbox based on Find blanks checkbox
        private void ChkFindBlanks_Changed(object sender, RoutedEventArgs e)
        {
            bool findBlanks = chkFindBlanks.IsChecked == true;

            // Mutually exclusive with Replace All Cells
            if (findBlanks && chkReplaceAllCells.IsChecked == true)
            {
                chkReplaceAllCells.IsChecked = false;
            }

            UpdateFindControlsState();
        }

        // Update control states based on checkbox selections
        private void UpdateFindControlsState()
        {
            bool replaceAll = chkReplaceAllCells.IsChecked == true;
            bool findBlanks = chkFindBlanks.IsChecked == true;
            bool disableFindControls = replaceAll || findBlanks;

            // Disable find-related controls when either mode is active
            txtFind.IsEnabled = !disableFindControls;
            lblFindWhat.Opacity = disableFindControls ? 0.5 : 1.0;
            chkMatchCase.IsEnabled = !disableFindControls;
            chkWholeCell.IsEnabled = !disableFindControls;
            btnCount.IsEnabled = !replaceAll; // Count still works for Find Blanks

            if (disableFindControls)
            {
                txtFind.Text = string.Empty;
            }
        }

        // Count - count matches without replacing
        private void BtnCount_Click(object sender, RoutedEventArgs e)
        {
            bool findBlanks = chkFindBlanks.IsChecked == true;

            if (!findBlanks && string.IsNullOrEmpty(txtFind.Text))
            {
                AppMessageBox.Show("Please enter text to find, or check 'Find blanks' to find empty cells.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                AppMessageBox.Show("No column selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string findText = txtFind.Text;
            bool matchCase = chkMatchCase.IsChecked == true;
            bool wholeCell = chkWholeCell.IsChecked == true;

            var allActivities = _dataGrid.View.Records.Select(r => r.Data).Cast<Activity>().ToList();
            var editableActivities = allActivities.Where(a =>
                App.CurrentUser!.IsAdmin ||
                string.Equals(a.AssignedTo, App.CurrentUser?.Username, System.StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var provider = _dataGrid.View.GetPropertyAccessProvider();
            int matchCount = 0;
            int nonEditableMatches = 0;

            foreach (var activity in allActivities)
            {
                var currentValue = provider.GetValue(activity, _columnMappingName);
                string currentText = FormatValueForComparison(currentValue);

                bool isMatch;
                if (findBlanks)
                {
                    isMatch = currentValue == null || string.IsNullOrWhiteSpace(currentText);
                }
                else
                {
                    if (currentValue == null)
                        continue;

                    if (wholeCell)
                    {
                        isMatch = matchCase
                            ? currentText == findText
                            : currentText.Equals(findText, System.StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        isMatch = matchCase
                            ? currentText.Contains(findText)
                            : currentText.IndexOf(findText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }

                if (isMatch)
                {
                    matchCount++;
                    if (!editableActivities.Contains(activity))
                        nonEditableMatches++;
                }
            }

            string searchDesc = findBlanks ? "blank cells" : $"'{findText}'";
            string message = $"Found {matchCount:N0} match(es) for {searchDesc} in column '{_columnMappingName}'.";

            if (nonEditableMatches > 0)
                message += $"\n\n{nonEditableMatches:N0} of these are in records not owned by you (cannot be replaced).";

            int editableMatches = matchCount - nonEditableMatches;
            if (editableMatches > 0)
                message += $"\n\n{editableMatches:N0} can be replaced.";

            AppMessageBox.Show(message, "Find Results", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private async void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            bool replaceAllCells = chkReplaceAllCells.IsChecked == true;
            bool findBlanks = chkFindBlanks.IsChecked == true;

            if (!replaceAllCells && !findBlanks && string.IsNullOrEmpty(txtFind.Text))
            {
                AppMessageBox.Show("Please enter text to find, check 'Find blanks', or check 'Replace ALL cells'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                AppMessageBox.Show("No column selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Confirm Replace All Cells operation
            if (replaceAllCells)
            {
                var confirmResult = AppMessageBox.Show(
                    $"This will replace ALL values in column '{_columnMappingName}' with '{txtReplace.Text}'.\n\n" +
                    "This cannot be undone. Continue?",
                    "Confirm Replace All",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult != MessageBoxResult.Yes)
                    return;
            }

            string findText = txtFind.Text;
            string replaceText = txtReplace.Text ?? string.Empty;
            bool matchCase = chkMatchCase.IsChecked == true;
            bool wholeCell = chkWholeCell.IsChecked == true;

            // Show busy dialog
            string busyMessage = replaceAllCells ? "Replacing all values..." : "Finding matches...";
            var busyDialog = new BusyDialog(this, busyMessage);
            busyDialog.Show();
            btnReplaceAll.IsEnabled = false;

            try
            {
                var allActivities = _dataGrid.View.Records.Select(r => r.Data).Cast<Activity>().ToList();

                var editableActivities = allActivities.Where(a =>
                    App.CurrentUser!.IsAdmin ||
                    string.Equals(a.AssignedTo, App.CurrentUser?.Username, System.StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var provider = _dataGrid.View.GetPropertyAccessProvider();

                var column = _dataGrid.Columns.FirstOrDefault(c => c.MappingName == _columnMappingName);
                if (column == null)
                {
                    busyDialog.Close();
                    AppMessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Phase A: Find matches and compute new values in memory
                int matchCount = 0;
                int conversionFailures = 0;
                var updates = new List<(string UniqueID, object? NewValue)>();
                var derivedColumns = new Dictionary<string, List<(string UniqueID, object? Value)>>();
                bool isProgressField = _columnMappingName is "PercentEntry" or "EarnQtyEntry" or "Quantity" or "BudgetMHs";
                bool requiresValidation = _columnMappingName is "PercentEntry" or "EarnQtyEntry" or "ActStart" or "ActFin";
                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                var now = DateTime.UtcNow;

                // Snapshots let us roll every mutated row back to its original state if any row violates a rule.
                var snapshots = new List<(Activity activity, object? origValue, double origPercent, DateTime? origActStart, DateTime? origActFin)>();
                var failures = new List<(Activity activity, string error)>();

                foreach (var activity in editableActivities)
                {
                    var currentValue = provider.GetValue(activity, _columnMappingName);
                    string currentText = FormatValueForComparison(currentValue);

                    bool isMatch;
                    if (replaceAllCells)
                    {
                        // Replace All Cells mode - every cell is a match
                        isMatch = true;
                    }
                    else if (findBlanks)
                    {
                        // Match if value is null, empty, or whitespace-only
                        isMatch = currentValue == null || string.IsNullOrWhiteSpace(currentText);
                    }
                    else
                    {
                        // Skip null values when not finding blanks
                        if (currentValue == null)
                            continue;

                        if (wholeCell)
                        {
                            isMatch = matchCase
                                ? currentText == findText
                                : currentText.Equals(findText, System.StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            isMatch = matchCase
                                ? currentText.Contains(findText)
                                : currentText.IndexOf(findText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    }

                    if (!isMatch) continue;

                    matchCount++;

                    // When finding blanks, always replace entire cell
                    string newTextValue = findBlanks || wholeCell
                        ? replaceText
                        : matchCase
                            ? currentText.Replace(findText, replaceText)
                            : System.Text.RegularExpressions.Regex.Replace(
                                currentText,
                                System.Text.RegularExpressions.Regex.Escape(findText),
                                replaceText,
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    try
                    {
                        // Get property type from Activity class (currentValue may be null when finding blanks)
                        var propertyType = currentValue?.GetType() ?? typeof(Activity).GetProperty(_columnMappingName)?.PropertyType ?? typeof(string);
                        object? newValue = ConvertToPropertyType(newTextValue, propertyType);

                        if (requiresValidation)
                        {
                            snapshots.Add((activity, currentValue, activity.PercentEntry, activity.ActStart, activity.ActFin));
                        }

                        // Update in-memory object
                        provider.SetValue(activity, _columnMappingName, newValue);

                        if (isProgressField)
                        {
                            activity.RecalculateDerivedFields(_columnMappingName);

                            // Handle ActStart/ActFin date clearing based on resulting PercentEntry
                            double resultPercent = activity.PercentEntry;
                            if (resultPercent == 0)
                            {
                                // 0% → clear both dates
                                activity.ActStart = null;
                                activity.ActFin = null;
                            }
                            else if (resultPercent < 100)
                            {
                                // >0 but <100 → clear ActFin only
                                activity.ActFin = null;
                            }
                        }
                        else if (_columnMappingName == "ActFin" && activity.ActFin != null && activity.PercentEntry < 100)
                        {
                            // Setting a Finish date implies the activity is complete — auto-bump % to 100.
                            activity.PercentEntry = 100;
                        }

                        if (requiresValidation)
                        {
                            string? error = ActivityValidator.Validate(activity.PercentEntry, activity.ActStart, activity.ActFin);
                            if (error != null)
                            {
                                failures.Add((activity, error));
                                continue;
                            }
                        }

                        // Collect derived field values for DB update when the mutation touched percent/dates
                        if (isProgressField || _columnMappingName == "ActFin")
                        {
                            CollectDerivedValue(derivedColumns, "EarnQtyEntry", activity.UniqueID, activity.EarnQtyEntry);
                            CollectDerivedValue(derivedColumns, "PercentEntry", activity.UniqueID, activity.PercentEntry);
                            CollectDerivedValue(derivedColumns, "ActStart", activity.UniqueID,
                                activity.ActStart?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)"");
                            CollectDerivedValue(derivedColumns, "ActFin", activity.UniqueID,
                                activity.ActFin?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)"");
                        }

                        activity.LocalDirty = 1;
                        activity.UpdatedBy = currentUser;
                        activity.UpdatedUtcDate = now;

                        updates.Add((activity.UniqueID, newValue));
                    }
                    catch
                    {
                        conversionFailures++;
                    }
                }

                if (failures.Count > 0)
                {
                    // Roll every mutated row back to its pre-edit state and abort without writing to the DB.
                    foreach (var s in snapshots)
                    {
                        provider.SetValue(s.activity, _columnMappingName, s.origValue);
                        s.activity.PercentEntry = s.origPercent;
                        s.activity.ActStart = s.origActStart;
                        s.activity.ActFin = s.origActFin;
                    }

                    busyDialog.Close();

                    var preview = failures.Take(10)
                        .Select(f => $"  • {f.activity.ActivityID}: {f.error}");
                    string detail = string.Join("\n", preview);
                    if (failures.Count > 10)
                        detail += $"\n  …and {failures.Count - 10:N0} more";

                    AppMessageBox.Show(
                        $"Replace aborted: {failures.Count:N0} row(s) would violate validation rules. No changes were saved.\n\n{detail}",
                        "Validation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (updates.Count == 0)
                {
                    busyDialog.Close();
                    string noMatchMsg = matchCount == 0
                        ? "No matches found."
                        : $"Found {matchCount} match(es) but all failed type conversion.";
                    AppMessageBox.Show(noMatchMsg, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                // Phase B: Batch write to database in single transaction
                busyDialog.UpdateStatus($"Saving {updates.Count:N0} changes...");

                // Only include derived columns that differ from the target column
                var filteredDerived = derivedColumns
                    .Where(kv => kv.Key != _columnMappingName)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                int replaceCount = await ActivityRepository.BulkUpdateColumnAsync(
                    _columnMappingName,
                    updates,
                    currentUser,
                    filteredDerived.Count > 0 ? filteredDerived : null);

                // Phase C: INotifyPropertyChanged propagates the new values to the grid cells. We deliberately
                // skip View.Refresh() here so rows that no longer match the active filter remain visible until
                // the user explicitly re-applies filters or hits the Refresh button.
                busyDialog.Close();

                int skippedCount = allActivities.Count - editableActivities.Count;
                string message = $"Replaced {replaceCount:N0} occurrence(s) in column '{_columnMappingName}'.";

                if (skippedCount > 0)
                    message += $"\n\n{skippedCount:N0} record(s) skipped (not owned by you).";

                if (conversionFailures > 0)
                    message += $"\n\n{conversionFailures:N0} match(es) skipped (conversion failed).";

                AppMessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.None);

                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                busyDialog.Close();
                AppMessageBox.Show($"Error during replace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnReplaceAll.IsEnabled = true;
            }
        }

        // Collects a derived field value into the lookup dictionary
        private static void CollectDerivedValue(
            Dictionary<string, List<(string UniqueID, object? Value)>> dict,
            string columnName, string uniqueId, object? value)
        {
            if (!dict.ContainsKey(columnName))
                dict[columnName] = new List<(string UniqueID, object? Value)>();
            dict[columnName].Add((uniqueId, value));
        }

        // Helper method to convert string back to the original property type
        private object? ConvertToPropertyType(string value, System.Type targetType)
        {
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(int?))
                    return null;
                return int.Parse(value);
            }

            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(double?))
                    return null;
                return double.Parse(value);
            }

            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(DateTime?))
                    return null;
                return DateTime.Parse(value);
            }

            // Add more types as needed
            return System.Convert.ChangeType(value, targetType);
        }

        // Formats a cell value to its display text (dates use short date to match grid display)
        private static string FormatValueForComparison(object? value)
        {
            if (value == null) return string.Empty;
            if (value is DateTime dt) return dt.ToString("d"); // ShortDate pattern
            return value.ToString() ?? string.Empty;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}