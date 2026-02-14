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
                MessageBox.Show("Please enter text to find, or check 'Find blanks' to find empty cells.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                MessageBox.Show("No column selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                string currentText = currentValue?.ToString() ?? string.Empty;

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

            MessageBox.Show(message, "Find Results", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private async void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            bool replaceAllCells = chkReplaceAllCells.IsChecked == true;
            bool findBlanks = chkFindBlanks.IsChecked == true;

            if (!replaceAllCells && !findBlanks && string.IsNullOrEmpty(txtFind.Text))
            {
                MessageBox.Show("Please enter text to find, check 'Find blanks', or check 'Replace ALL cells'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                MessageBox.Show("No column selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Confirm Replace All Cells operation
            if (replaceAllCells)
            {
                var confirmResult = MessageBox.Show(
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
                    MessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Phase A: Find matches and compute new values in memory
                int matchCount = 0;
                int conversionFailures = 0;
                var updates = new List<(string UniqueID, object? NewValue)>();
                var derivedColumns = new Dictionary<string, List<(string UniqueID, object? Value)>>();
                bool isProgressField = _columnMappingName is "PercentEntry" or "EarnQtyEntry" or "Quantity" or "BudgetMHs";
                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                var now = DateTime.UtcNow;

                foreach (var activity in editableActivities)
                {
                    var currentValue = provider.GetValue(activity, _columnMappingName);
                    string currentText = currentValue?.ToString() ?? string.Empty;

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

                        // Update in-memory object
                        provider.SetValue(activity, _columnMappingName, newValue);

                        if (isProgressField)
                        {
                            activity.RecalculateDerivedFields(_columnMappingName);

                            // Collect derived field values for DB update
                            CollectDerivedValue(derivedColumns, "EarnMHsCalc", activity.UniqueID, activity.EarnMHsCalc);
                            CollectDerivedValue(derivedColumns, "EarnQtyEntry", activity.UniqueID, activity.EarnQtyEntry);
                            CollectDerivedValue(derivedColumns, "PercentEntry", activity.UniqueID, activity.PercentEntry);
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

                if (updates.Count == 0)
                {
                    busyDialog.Close();
                    string noMatchMsg = matchCount == 0
                        ? "No matches found."
                        : $"Found {matchCount} match(es) but all failed type conversion.";
                    MessageBox.Show(noMatchMsg, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.None);
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

                // Phase C: Refresh grid
                _dataGrid.View.Refresh();

                busyDialog.Close();

                int skippedCount = allActivities.Count - editableActivities.Count;
                string message = $"Replaced {replaceCount:N0} occurrence(s) in column '{_columnMappingName}'.";

                if (skippedCount > 0)
                    message += $"\n\n{skippedCount:N0} record(s) skipped (not owned by you).";

                if (conversionFailures > 0)
                    message += $"\n\n{conversionFailures:N0} match(es) skipped (conversion failed).";

                MessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.None);

                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                busyDialog.Close();
                MessageBox.Show($"Error during replace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}